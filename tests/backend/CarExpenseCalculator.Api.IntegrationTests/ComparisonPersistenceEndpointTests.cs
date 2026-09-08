using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ComparisonPersistenceEndpointTests(SavedCostScenarioApiFactory factory)
    : IClassFixture<SavedCostScenarioApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private static JsonObject Stored(Guid id, long vehicleRevision, long profileRevision = 0, long ruleRevision = 0, long? listingVersion = null) => new()
    {
        ["mode"] = "stored", ["requestId"] = "stored-1", ["profile"] = Profile(), ["rules"] = ComparisonApiTestData.Rules(),
        ["asOfDate"] = "2026-09-08", ["storedBase"] = new JsonObject { ["householdProfileRevision"] = profileRevision, ["ruleProfileRevision"] = ruleRevision },
        ["candidates"] = new JsonArray(new JsonObject { ["vehicleId"] = id, ["registrationNumber"] = "ABC123",
            ["storedBase"] = new JsonObject { ["vehicleRevision"] = vehicleRevision, ["listing"] = new JsonObject { ["version"] = listingVersion } } }),
    };
    private Task<HttpResponseMessage> Post(JsonObject request) => ComparisonApiTestData.Post(_client, request);

    [Theory]
    [InlineData(100000, "unknown")]
    [InlineData(0, "exceeded")]
    [InlineData(null, "notConfigured")]
    [InlineData(-1, "invalid")]
    public async Task Stored_review_items_cannot_be_omitted_to_restore_complete_costs_or_budget(int? budget, string status)
    {
        var scenario = SavedCostScenarioTestData.Complete() with
        {
            OtherRecurringCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.NamedRecurringCostInput
                { Label = $"Recurring {i}", AmountSek = i, Cadence = Contracts.ManualCalculations.RecurringCostCadence.Annual }).ToArray(),
            OtherOneTimeCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.OneTimeCostInput
                { Label = $"Once {i}", AmountSek = i }).ToArray(),
        };
        var old = await Json(await _client.PostAsJsonAsync("/api/saved-cost-scenarios", new { registrationNumber = "ABC123", scenario }), HttpStatusCode.Created);
        var id = old["vehicleId"]!.GetValue<Guid>();
        await factory.ExecuteDatabaseCommandAsync("UPDATE saved_cost_scenarios SET result_schema_version=999, result_snapshot=jsonb_build_object()");
        var pending = await Json(await Post(Stored(id, 1)));
        Assert.Equal("legacyPending", pending["candidates"]![0]!["storedInputState"]!.GetValue<string>());
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        var items = transition["vehicles"]![0]!["legacy"]!["items"]!.AsArray();
        Assert.Equal(104, items.Count);
        var decisions = new JsonArray(items.Select(x => (JsonNode)new JsonObject
            { ["key"] = x!["input"]!["key"]!.DeepClone(), ["disposition"] = "keepForReview" }).ToArray());
        var cost = Vehicle();
        cost["customCosts"]!["items"] = JsonNode.Parse("""
            [{"key":"known","label":"Known contribution","amountSek":{"single":10},"cadence":"monthly"}]
            """);
        await Json(await _client.PostAsJsonAsync("/api/household-transition", new JsonObject
        {
            ["expectedTransitionRevision"] = transition["revision"]!.DeepClone(), ["expectedProfileRevision"] = 0, ["profile"] = Profile(),
            ["vehicles"] = new JsonArray(new JsonObject { ["vehicleId"] = id, ["expectedRevision"] = 1,
                ["cost"] = new JsonObject { ["input"] = cost, ["legacyDecisions"] = decisions } }),
        }));
        var input = Stored(id, 2, 1); input["profile"]!["monthlyBudgetSek"] = budget;
        var result = await Json(await Post(input)); var car = result["candidates"]![0]!;
        Assert.Equal(104, car["unresolvedLegacyItems"]!.AsArray().Count);
        Assert.Null(car["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]);
        Assert.Equal(status, car["cost"]!["monthlyBudget"]!["status"]!.GetValue<string>());
        input["candidates"]![0]!["legacyDecisions"] = new JsonArray(items.Select(x => (JsonNode)new JsonObject
            { ["key"] = x!["input"]!["key"]!.DeepClone(), ["disposition"] = "discard" }).ToArray());
        result = await Json(await Post(input));
        Assert.Empty(result["candidates"]![0]!["unresolvedLegacyItems"]!.AsArray());
        Assert.NotNull(result["candidates"]![0]!["cost"]!["totals"]!["ownershipCost"]!["completeTotalSek"]);
        var persisted = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal(104, persisted["unresolvedLegacyItems"]!.AsArray().Count);
    }


    [Fact]
    public async Task Empty_resources_create_explicitly_and_fact_writes_share_the_vehicle_revision()
    {
        await Json(await _client.GetAsync("/api/rule-profile"), HttpStatusCode.NotFound);
        var rules = await Json(await _client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = ComparisonApiTestData.Rules() }));
        Assert.Equal(1, rules["revision"]!.GetValue<long>());
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var id = vehicle["vehicleId"]!.GetValue<Guid>();
        var empty = await Json(await _client.GetAsync($"/api/vehicle-facts/{id}"));
        Assert.Null(empty["input"]); Assert.Null(empty["costConfirmedAt"]);
        var saved = await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1,
            input = new { edits = new { seats = ComparisonApiTestData.Manual(5), towBar = ComparisonApiTestData.Manual(false) } } }));
        Assert.Equal(2, saved["revision"]!.GetValue<long>()); Assert.Null(saved["costConfirmedAt"]);
        var costs = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal(2, costs["revision"]!.GetValue<long>());
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1, input = new { } }), HttpStatusCode.Conflict);
        await Json(await _client.GetAsync($"/api/vehicle-facts/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stored_preview_uses_current_cost_price_and_only_explicit_matching_confirmation()
    {
        var cost = Vehicle(); cost["priceSek"] = 40000;
        var saved = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create(input: cost)), HttpStatusCode.Created);
        var id = saved["vehicleId"]!.GetValue<Guid>();
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1,
            input = new { edits = new { purchasePriceSek = ComparisonApiTestData.Manual(40000), transmission = ComparisonApiTestData.Manual("automatic") },
                costConfirmation = "confirm" } }));
        var input = Stored(id, 2);
        var result = await Json(await Post(input));
        Assert.True(result["storageChecked"]!.GetValue<bool>());
        Assert.Equal(85, result["candidates"]![0]!["score"]!["lower"]!.GetValue<decimal>());
        input["candidates"]![0]!["costInput"] = cost.DeepClone();
        input["candidates"]![0]!["costInput"]!["priceSek"] = 35000;
        result = await Json(await Post(input));
        Assert.Null(result["candidates"]![0]!["costConfirmedAt"]);
        Assert.Equal(40, result["candidates"]![0]!["score"]!["lower"]!.GetValue<decimal>());
        Assert.Equal(100, result["candidates"]![0]!["score"]!["upper"]!.GetValue<decimal>());
        Assert.True(result["candidates"]![0]!["unsaved"]!["costs"]!.GetValue<bool>());
        input["candidates"]![0]!["facts"] = new JsonObject { ["costConfirmation"] = "confirm" };
        result = await Json(await Post(input));
        Assert.Equal(88.75m, result["candidates"]![0]!["score"]!["lower"]!.GetValue<decimal>());
        input["candidates"]![0]!["costInput"]!["priceSek"] = null;
        input["candidates"]![0]!["facts"] = new JsonObject();
        result = await Json(await Post(input));
        Assert.Null(result["candidates"]![0]!["contributions"]![0]!["assessment"]!["actual"]);
        var persisted = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal(40000, persisted["input"]!["priceSek"]!.GetValue<decimal>());
        Assert.Equal(2, persisted["revision"]!.GetValue<long>());
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("profile")]
    [InlineData("rules")]
    [InlineData("listing")]
    [InlineData("registration")]
    public async Task Stored_preview_rejects_mixed_or_stale_source_revisions(string changed)
    {
        var saved = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var id = saved["vehicleId"]!.GetValue<Guid>();
        var input = Stored(id, 1);
        if (changed == "vehicle") await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1, input = new { } }));
        if (changed == "profile") await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }));
        if (changed == "rules") await Json(await _client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = new { } }));
        if (changed == "listing") input["candidates"]![0]!["storedBase"]!["listing"]!["version"] = 99;
        if (changed == "registration") input["candidates"]![0]!["registrationNumber"] = "DEF456";
        await Json(await Post(input), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_clients_cannot_both_create_the_rule_profile()
    {
        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(_client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = new { } }),
            other.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = ComparisonApiTestData.Rules() }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        var error = await Json(Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        Assert.Equal("ruleProfileRevisionConflict", error["code"]!.GetValue<string>());
        Assert.Equal(1, error["actualRevision"]!.GetValue<long>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Corrupt_and_unsupported_comparison_payloads_are_not_overwritten(bool unsupported)
    {
        var saved = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var id = saved["vehicleId"]!.GetValue<Guid>();
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 1, input = new { } }));
        await factory.ExecuteDatabaseCommandAsync(unsupported
            ? "UPDATE vehicle_comparison_facts SET schema_version=999"
            : "UPDATE vehicle_comparison_facts SET input=jsonb_build_object()");
        var status = unsupported ? HttpStatusCode.Conflict : HttpStatusCode.ServiceUnavailable;
        var problem = await Json(await _client.GetAsync($"/api/vehicle-facts/{id}"), status);
        Assert.DoesNotContain("Password", problem.ToJsonString());
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", new { expectedRevision = 2, input = new { } }), status);
    }

    [Fact]
    public async Task Large_revisions_and_decimal_inputs_are_not_coerced_through_floating_point()
    {
        var saved = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var id = saved["vehicleId"]!.GetValue<Guid>();
        await factory.ExecuteDatabaseCommandAsync("UPDATE vehicles SET revision=9007199254740993");
        var input = new { expectedRevision = 9007199254740993L, input = new { edits = new {
            odometerKilometres = ComparisonApiTestData.Manual(123456.1234567890123456789012m) } } };
        var response = await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}", input));
        Assert.Equal(9007199254740994L, response["revision"]!.GetValue<long>());
        Assert.Equal(123456.1234567890123456789012m, response["input"]!["facts"]!["odometerKilometres"]!["observations"]![0]!["value"]!.GetValue<decimal>());
    }
}
