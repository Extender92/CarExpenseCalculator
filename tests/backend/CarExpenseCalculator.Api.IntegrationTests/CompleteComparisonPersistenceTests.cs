using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;
using static CarExpenseCalculator.Api.IntegrationTests.CompleteComparisonApiData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class CompleteComparisonPersistenceTests(SavedCostScenarioApiFactory factory)
    : IClassFixture<SavedCostScenarioApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }
    private Task<JsonNode> Baseline() => ReadBaseline(_client);
    private static async Task<JsonNode> ReadBaseline(HttpClient client) => await Json(await client.GetAsync("/api/comparisons/baseline"));
    private Task<HttpResponseMessage> Post(JsonNode request) => _client.PostAsJsonAsync(Route, request);

    [Fact]
    public async Task More_than_2_MiB_of_saved_input_is_compared_with_a_compact_request()
    {
        long bytes = 0;
        for (var i = 100; i < 150; i++)
        {
            var cost = Vehicle($"ABC{i}");
            cost["customCosts"]!["items"] = new JsonArray(Enumerable.Range(0, 50).Select(j => (JsonNode)new JsonObject
            {
                ["key"] = $"extra-{j}", ["label"] = "Written estimate", ["evidenceNote"] = new string('x', 1000),
                ["amountSek"] = new JsonObject { ["single"] = 1 }, ["cadence"] = "once", ["monthOffset"] = 1,
            }).ToArray());
            bytes += System.Text.Encoding.UTF8.GetByteCount(cost.ToJsonString());
            await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create($"ABC{i}", cost)), HttpStatusCode.Created);
        }
        Assert.True(bytes > 2 * 1024 * 1024);
        var request = Stored(await Baseline());
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(request.ToJsonString()) < 2048);
        var result = await Json(await Post(request));
        foreach (var view in Views(result))
        {
            Assert.Equal(50, view["candidates"]!.AsArray().Count);
            Assert.All(view["candidates"]!.AsArray(), x => Assert.Equal(10050m, x!["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]!.GetValue<decimal>()));
        }
    }

    [Theory]
    [InlineData(100000, "unknown")]
    [InlineData(0, "exceeded")]
    [InlineData(null, "notConfigured")]
    [InlineData(-1, "invalid")]
    public async Task Whole_set_keeps_maximal_legacy_reviews_and_budget_authority(int? budget, string status)
    {
        var scenario = SavedCostScenarioTestData.Complete() with
        {
            OtherRecurringCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.NamedRecurringCostInput
                { Label = $"Recurring {i}", AmountSek = i, Cadence = Contracts.ManualCalculations.RecurringCostCadence.Annual }).ToArray(),
            OtherOneTimeCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.OneTimeCostInput { Label = $"Once {i}", AmountSek = i }).ToArray(),
        };
        await Json(await _client.PostAsJsonAsync("/api/saved-cost-scenarios", new { registrationNumber = "ABC123", scenario }), HttpStatusCode.Created);
        await factory.ExecuteDatabaseCommandAsync("UPDATE saved_cost_scenarios SET result_schema_version=999, result_snapshot=jsonb_build_object()");
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        var items = transition["vehicles"]![0]!["legacy"]!["items"]!.AsArray();
        Assert.Equal(104, items.Count);
        var pending = await Json(await Post(Stored(await Baseline())));
        Assert.Equal("legacyPending", pending["views"]!["baseline"]!["candidates"]![0]!["storedInputState"]!.GetValue<string>());
        var cost = Vehicle();
        cost["customCosts"]!["items"] = JsonNode.Parse("""[{"key":"known","label":"Known","amountSek":{"single":10},"cadence":"monthly"}]""");
        var id = transition["vehicles"]![0]!["vehicleId"]!.GetValue<Guid>();
        await Json(await _client.PostAsJsonAsync("/api/household-transition", new JsonObject
        {
            ["expectedTransitionRevision"] = transition["revision"]!.DeepClone(), ["expectedProfileRevision"] = 0, ["profile"] = Profile(),
            ["vehicles"] = new JsonArray(new JsonObject { ["vehicleId"] = id, ["expectedRevision"] = 1,
                ["cost"] = new JsonObject { ["input"] = cost, ["legacyDecisions"] = new JsonArray(items.Select(x => (JsonNode)new JsonObject
                    { ["key"] = x!["input"]!["key"]!.DeepClone(), ["disposition"] = "keepForReview" }).ToArray()) } }),
        }));
        var input = Stored(await Baseline()); input["profile"]!["monthlyBudgetSek"] = budget;
        var result = await Json(await Post(input));
        foreach (var view in Views(result))
        {
            var car = view["candidates"]![0]!;
            Assert.Equal(104, car["unresolvedLegacyItems"]!.AsArray().Count);
            Assert.Null(car["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]);
            Assert.Equal(status, car["cost"]!["monthlyBudget"]!["status"]!.GetValue<string>());
        }
        var current = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        var edit = Override(current);
        edit["legacyDecisions"] = new JsonArray(items.Select(x => (JsonNode)new JsonObject
            { ["key"] = x!["input"]!["key"]!.DeepClone(), ["disposition"] = "discard" }).ToArray());
        input["overrides"] = new JsonArray(edit);
        result = await Json(await Post(input));
        Assert.All(Views(result), view => Assert.Empty(view["candidates"]![0]!["unresolvedLegacyItems"]!.AsArray()));
        Assert.Equal(104, (await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}")))["unresolvedLegacyItems"]!.AsArray().Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(250)]
    public async Task Server_membership_includes_every_saved_car_without_uploading_their_inputs(int count)
    {
        for (var i = 100; i < 100 + count; i++)
            await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create($"ABC{i:000}")), HttpStatusCode.Created);
        var baseline = await Baseline();
        Assert.Null(baseline["profile"]); Assert.Null(baseline["rules"]);
        Assert.Equal(count, baseline["candidateCount"]!.GetValue<int>());
        var result = await Json(await Post(Stored(baseline)));
        Assert.Equal(baseline["baselineToken"]!.GetValue<string>(), result["baselineToken"]!.GetValue<string>());
        foreach (var view in Views(result))
        {
            Assert.Equal(count, view["candidates"]!.AsArray().Count);
            Assert.Equal(Enumerable.Range(100, count).Select(i => $"ABC{i:000}"),
                view["candidates"]!.AsArray().Select(x => x!["registrationNumber"]!.GetValue<string>()));
            Assert.All(view["candidates"]!.AsArray(), x => Assert.False(x!["unsaved"]!["costs"]!.GetValue<bool>()));
        }
        Assert.Equal(baseline.ToJsonString(), (await Baseline()).ToJsonString());
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("rules")]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("facts")]
    [InlineData("cost")]
    public async Task Changed_untouched_car_or_profile_invalidates_the_complete_baseline(string change)
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var id = vehicle["vehicleId"]!.GetValue<Guid>();
        var request = Stored(await Baseline());
        switch (change)
        {
            case "profile": await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() })); break;
            case "rules": await Json(await _client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = new { } })); break;
            case "add": await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create("DEF456")), HttpStatusCode.Created); break;
            case "delete": Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/vehicle-cost-inputs/{id}?expectedRevision=1")).StatusCode); break;
            case "facts": await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1, input = new { } })); break;
            default: await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", new { expectedRevision = 1, cost = new { input = Vehicle() } })); break;
        }
        var conflict = await Json(await Post(request), HttpStatusCode.Conflict);
        Assert.Equal("comparisonBaselineConflict", conflict["code"]!.GetValue<string>());
        Assert.Equal((await Baseline())["baselineToken"]!.GetValue<string>(), conflict["actualBaselineToken"]!.GetValue<string>());
    }

    [Fact]
    public async Task Overrides_do_not_remove_other_cars_and_confirmation_is_captured_once_for_all_views()
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        vehicle = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicle["vehicleId"]!.GetValue<Guid>()}"));
        await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create("DEF456")), HttpStatusCode.Created);
        var request = Stored(await Baseline());
        var edit = Override(vehicle);
        edit["facts"] = new JsonObject { ["costConfirmation"] = "confirm" };
        edit["costInput"] = Vehicle("ABC123");
        edit["costInput"]!["priceSek"] = 35000;
        edit["costInput"]!["residual"]!["value"]!["single"] = 25000;
        request["overrides"] = new JsonArray(edit);
        var result = await Json(await Post(request));
        string? confirmedAt = null;
        foreach (var view in Views(result))
        {
            Assert.Equal(2, view["candidates"]!.AsArray().Count);
            var car = view["candidates"]![0]!;
            Assert.Equal(35000, car["effectiveCostInput"]!["priceSek"]!.GetValue<decimal>());
            Assert.True(car["unsaved"]!["costs"]!.GetValue<bool>());
            confirmedAt ??= car["costConfirmedAt"]!.GetValue<string>();
            Assert.Equal(confirmedAt, car["costConfirmedAt"]!.GetValue<string>());
        }
        var persisted = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicle["vehicleId"]!.GetValue<Guid>()}"));
        Assert.Equal(vehicle.ToJsonString(), persisted.ToJsonString());
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("listing")]
    [InlineData("missing")]
    public async Task A_fresh_global_token_does_not_authorize_stale_or_unrelated_overrides(string change)
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var request = Stored(await Baseline()); var edit = Override(vehicle);
        switch (change)
        {
            case "identity": edit["registrationNumber"] = "DEF456"; break;
            case "revision": edit["storedBase"]!["vehicleRevision"] = 0; break;
            case "listing": edit["storedBase"]!["listing"]!["version"] = 2; break;
            default: edit["vehicleId"] = Guid.NewGuid(); break;
        }
        request["overrides"] = new JsonArray(edit);
        await Json(await Post(request), change == "missing" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict);
        Assert.Equal(1, (await Baseline())["candidateCount"]!.GetValue<int>());
    }
}
