using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class HouseholdPersistenceEndpointTests(SavedCostScenarioApiFactory factory)
    : IClassFixture<SavedCostScenarioApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Empty_profile_and_draft_have_different_semantics_and_no_defaults()
    {
        var missing = await Json(await _client.GetAsync("/api/household-profile"), HttpStatusCode.NotFound);
        Assert.Equal(0, missing["actualRevision"]!.GetValue<long>());
        var draft = await Json(await _client.GetAsync("/api/vehicle-draft"));
        Assert.Null(draft["input"]);
        Assert.Equal(0, draft["revision"]!.GetValue<long>());
        var saved = await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = new { } }));
        Assert.Null(saved["input"]!["purchaseCashSek"]);
        Assert.Null(saved["input"]!["periodMonths"]);
        Assert.Equal(1, saved["revision"]!.GetValue<long>());
        var deleted = await Json(await _client.DeleteAsync("/api/vehicle-draft?expectedRevision=0"));
        Assert.Equal(1, deleted["revision"]!.GetValue<long>());
        await Json(await _client.PostAsJsonAsync("/api/vehicle-draft/adopt", new { expectedRevision = 1 }), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Competing_first_profile_saves_have_one_winner_and_no_retry()
    {
        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(_client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }),
            other.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = new { purchaseCashSek = 7 } }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        var problem = await Json(Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        Assert.Equal("profileRevisionConflict", problem["code"]!.GetValue<string>());
        Assert.Equal(0, problem["expectedRevision"]!.GetValue<long>());
        Assert.Equal(1, problem["actualRevision"]!.GetValue<long>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_inputs_round_trip_and_full_replacement_keeps_precision(bool leasing)
    {
        var input = leasing ? Lease() : Vehicle();
        input["additionalRepairAllowancePerMonthSek"] = JsonNode.Parse("""{"favorable":0,"baseline":123.1234567890123456789,"cautious":1}""");
        input["service"] = JsonNode.Parse("""{"isIncluded":true,"items":[{"key":"extra","label":"Extra","amountSek":{"single":1.1234567890123456789},"cadence":"once","monthOffset":0,"evidenceNote":"Quoted","sourceUrl":"https://example.com/quote"}]}""");
        var created = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create(" abc-123 ", input)), HttpStatusCode.Created);
        Assert.Equal("ABC123", created["registrationNumber"]!.GetValue<string>());
        Assert.Equal("current", created["state"]!.GetValue<string>());
        var id = created["vehicleId"]!.GetValue<Guid>();
        var fetched = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal(123.1234567890123456789m, fetched["input"]!["additionalRepairAllowancePerMonthSek"]!["baseline"]!.GetValue<decimal>());
        Assert.Equal(1.1234567890123456789m, fetched["input"]!["service"]!["items"]![0]!["amountSek"]!["single"]!.GetValue<decimal>());
        Assert.Equal("Quoted", fetched["input"]!["service"]!["items"]![0]!["evidenceNote"]!.GetValue<string>());
        var replacement = new { expectedRevision = 1, cost = new { input = new { candidateKey = "replacement", priceSek = (decimal?)null } } };
        var replaced = await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", replacement));
        Assert.Equal(2, replaced["revision"]!.GetValue<long>());
        Assert.Null(replaced["input"]!["service"]);
        Assert.Null(replaced["input"]!["lease"]);
        Assert.Single((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/household-profile")).StatusCode);
    }

    [Fact]
    public async Task Numeric_save_validation_is_400_with_http_field_path_and_no_write()
    {
        var request = Create(input: Vehicle().With("priceSek", JsonValue.Create(-1)));
        var problem = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", request), HttpStatusCode.BadRequest);
        Assert.Contains("cost.input.priceSek", problem["fieldErrors"]!.ToJsonString());
        Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
        problem = await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = new { purchaseCashSek = -1 } }), HttpStatusCode.BadRequest);
        Assert.Contains("input.purchaseCashSek", problem["fieldErrors"]!.ToJsonString());
        await Json(await _client.GetAsync("/api/household-profile"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Concurrent_create_and_replace_report_identity_and_current_revision()
    {
        using var other = factory.CreateClient();
        var creates = await Task.WhenAll(_client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), other.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()));
        var saved = await Json(Assert.Single(creates, x => x.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created);
        var duplicate = await Json(Assert.Single(creates, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        Assert.Equal("registrationNumberConflict", duplicate["code"]!.GetValue<string>());
        var id = saved["vehicleId"]!.GetValue<Guid>();
        Assert.Equal(id, duplicate["vehicleId"]!.GetValue<Guid>());
        var request = new { expectedRevision = 1, cost = new { input = Vehicle() } };
        var edits = await Task.WhenAll(_client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", request), other.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", request));
        Assert.Single(edits, x => x.StatusCode == HttpStatusCode.OK);
        var conflict = await Json(Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
        Assert.Equal(2, conflict["actualRevision"]!.GetValue<long>());
    }

    [Fact]
    public async Task Draft_replacement_requires_explicit_choice_and_old_calls_cannot_recreate_empty_slot()
    {
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", Draft("ABC123", 0)));
        var denied = await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", Draft("DEF456", 1)), HttpStatusCode.Conflict);
        Assert.Equal("draftReplacementRequired", denied["code"]!.GetValue<string>());
        var replace = Draft("DEF456", 1).With("replaceExisting", JsonValue.Create(true));
        var replaced = await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", replace));
        Assert.Equal(2, replaced["revision"]!.GetValue<long>());
        await Json(await _client.DeleteAsync("/api/vehicle-draft?expectedRevision=2"));
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", replace), HttpStatusCode.Conflict);
        var empty = await Json(await _client.GetAsync("/api/vehicle-draft"));
        Assert.Null(empty["input"]);
        Assert.Equal(3, empty["revision"]!.GetValue<long>());
    }

    [Fact]
    public async Task Draft_for_new_registration_cannot_adopt_someone_elses_created_vehicle()
    {
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", Draft("ABC123", 0)));
        await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        var failed = await Json(await _client.PostAsJsonAsync("/api/vehicle-draft/adopt", new { expectedRevision = 1 }), HttpStatusCode.Conflict);
        Assert.Equal("registrationNumberConflict", failed["code"]!.GetValue<string>());
        var draft = await Json(await _client.GetAsync("/api/vehicle-draft"));
        Assert.NotNull(draft["input"]);
        Assert.Equal(1, draft["revision"]!.GetValue<long>());
    }

    [Fact]
    public async Task Reviewed_listing_draft_round_trips_and_adopts_both_parts_once()
    {
        var draft = Draft("ABC123", 0);
        draft["input"]!["listing"] = JsonSerializer.SerializeToNode(SavedListingTestData.Complete("ABC123"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        draft["input"]!["cost"]!["listingLinkMode"] = "current";
        var saved = await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", draft));
        Assert.Equal(123456.123456789012345m, saved["input"]!["listing"]!["draft"]!["priceSek"]!["value"]!.GetValue<decimal>());
        Assert.Equal("unverified", saved["input"]!["listing"]!["draft"]!["ownerCount"]!["provenance"]!["verification"]!.GetValue<string>());
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-draft/adopt", new { expectedRevision = 1 }));
        Assert.Equal(1, vehicle["revision"]!.GetValue<long>());
        Assert.Equal(1, vehicle["sourceListingVersion"]!.GetValue<long>());
        Assert.False(vehicle["needsListingReview"]!.GetValue<bool>());
        var id = vehicle["vehicleId"]!.GetValue<Guid>();
        await Json(await _client.GetAsync($"/api/saved-listings/{id}"));
        var empty = await Json(await _client.GetAsync("/api/vehicle-draft"));
        Assert.Null(empty["input"]); Assert.Equal(2, empty["revision"]!.GetValue<long>());
        // Applying only costs keeps the listing and changes the aggregate once.
        var costOnly = Draft("ABC123", 2, id, 1);
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", costOnly));
        vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-draft/adopt", new { expectedRevision = 3 }));
        Assert.Equal(2, vehicle["revision"]!.GetValue<long>());
        Assert.Equal(1, vehicle["currentListingVersion"]!.GetValue<long>());
    }

    [Theory]
    [InlineData("vehicle-cost-inputs")]
    [InlineData("saved-cost-scenarios")]
    [InlineData("saved-listings")]
    public async Task Every_delete_route_removes_entire_vehicle_and_matching_draft_but_keeps_profile(string route)
    {
        await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }));
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/saved-listings", new
            { registrationNumber = "ABC123", listing = SavedListingTestData.Complete("ABC123") }), HttpStatusCode.Created);
        var id = vehicle["vehicleId"]!.GetValue<Guid>();
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", new { expectedRevision = 1, cost = new { input = Vehicle() } }));
        var staleDraft = Draft("ABC123", 0, id, 2);
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", staleDraft));
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/{route}/{id}?expectedRevision=2")).StatusCode);
        Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
        Assert.Null((await Json(await _client.GetAsync("/api/vehicle-draft")))["input"]);
        Assert.Equal(1, (await Json(await _client.GetAsync("/api/household-profile")))["revision"]!.GetValue<long>());
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", staleDraft), HttpStatusCode.Conflict);
        staleDraft["expectedRevision"] = 2;
        await Json(await _client.PutAsJsonAsync("/api/vehicle-draft", staleDraft), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Legacy_recovery_ignores_unreadable_result_then_confirms_and_guards_v1_writes()
    {
        var legacy = await Legacy("ABC123");
        var id = legacy["vehicleId"]!.GetValue<Guid>();
        await factory.ExecuteDatabaseCommandAsync("UPDATE saved_cost_scenarios SET calculation_version = 99, result_schema_version = 98, result_snapshot = jsonb_build_object()");
        var current = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal("legacyPending", current["state"]!.GetValue<string>());
        Assert.NotNull(current["legacy"]!["input"]);
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        await Json(await _client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }), HttpStatusCode.Conflict);
        await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", new { expectedRevision = 1, cost = new { input = Vehicle() } }), HttpStatusCode.Conflict);
        var confirmation = Confirm(transition);
        var confirmed = await Json(await _client.PostAsJsonAsync("/api/household-transition", confirmation));
        Assert.Empty(confirmed["vehicles"]!.AsArray());
        current = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Null(current["legacy"]);
        Assert.NotEmpty(current["unresolvedLegacyItems"]!.AsArray());
        var preview = Preview(current["input"]!.DeepClone().AsObject());
        preview["vehicles"]![0]!["unresolvedLegacyItems"] = new JsonArray(current["unresolvedLegacyItems"]!.AsArray().Select(x => x!["input"]!.DeepClone()).ToArray());
        Assert.False((await Json(await Post(_client, preview)))["vehicles"]![0]!["isCostComparable"]!.GetValue<bool>());
        var oldWrite = await Json(await _client.PutAsJsonAsync($"/api/saved-cost-scenarios/{id}", new
            { expectedRevision = 2, scenario = SavedCostScenarioTestData.Replacement(), listingLinkMode = "preserve" }), HttpStatusCode.Conflict);
        Assert.Equal("householdTransitionRequired", oldWrite["code"]!.GetValue<string>());
        Assert.Equal($"/api/vehicle-cost-inputs/{id}", oldWrite["recoveryRoute"]!.GetValue<string>());
        await Json(await _client.GetAsync($"/api/saved-cost-scenarios/{id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transition_conflicts_and_failed_decisions_leave_profile_and_all_cars_unmodified()
    {
        await Legacy("ABC123"); await Legacy("DEF456");
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        var confirmation = Confirm(transition);
        confirmation["vehicles"]![1]!["cost"]!["legacyDecisions"] = new JsonArray();
        await Json(await _client.PostAsJsonAsync("/api/household-transition", confirmation), HttpStatusCode.BadRequest);
        var after = await Json(await _client.GetAsync("/api/household-transition"));
        Assert.Equal(transition.ToJsonString(), after.ToJsonString());
        confirmation = Confirm(transition);
        confirmation["expectedTransitionRevision"] = 0;
        await Json(await _client.PostAsJsonAsync("/api/household-transition", confirmation), HttpStatusCode.Conflict);
        confirmation = Confirm(transition);
        confirmation["vehicles"]!.AsArray().RemoveAt(1);
        await Json(await _client.PostAsJsonAsync("/api/household-transition", confirmation), HttpStatusCode.Conflict);
        using var other = factory.CreateClient();
        var responses = await Task.WhenAll(_client.PostAsJsonAsync("/api/household-transition", Confirm(transition)),
            other.PostAsJsonAsync("/api/household-transition", Confirm(transition)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task Corrupt_current_payload_or_unsupported_storage_version_is_typed(bool version, HttpStatusCode status)
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        await factory.ExecuteDatabaseCommandAsync(version ? "UPDATE vehicle_cost_inputs SET schema_version = 99"
            : "UPDATE vehicle_cost_inputs SET input = jsonb_build_object()");
        var problem = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicle["vehicleId"]!.GetValue<Guid>()}"), status);
        Assert.Equal(version ? "unsupportedHouseholdInputVersion" : "householdStorageUnavailable", problem["code"]!.GetValue<string>());
        Assert.DoesNotContain("Npgsql", problem.ToJsonString());
    }

    [Fact]
    public async Task Null_elements_in_stored_collections_are_unreadable_input_not_a_server_fault()
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create()), HttpStatusCode.Created);
        await factory.ExecuteDatabaseCommandAsync("UPDATE vehicle_cost_inputs SET input = jsonb_set(input, ARRAY['input','energySources'], jsonb_build_array(NULL::jsonb))");
        await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicle["vehicleId"]!.GetValue<Guid>()}"), HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Listing_and_transition_are_not_limited_to_100_saved_cars()
    {
        for (var i = 0; i < 101; i++) await Legacy($"ABC{i:000}");
        var list = await Json(await _client.GetAsync("/api/vehicle-cost-inputs"));
        Assert.Equal(101, list.AsArray().Count);
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        Assert.Equal(101, transition["vehicles"]!.AsArray().Count);
        var confirmed = await Json(await _client.PostAsJsonAsync("/api/household-transition", Confirm(transition)));
        Assert.Empty(confirmed["vehicles"]!.AsArray());
        list = await Json(await _client.GetAsync("/api/vehicle-cost-inputs"));
        Assert.Equal(101, list.AsArray().Count);
        Assert.All(list.AsArray(), x => Assert.Equal("current", x!["state"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Maximum_legacy_collections_remain_separate_and_round_trip_through_review()
    {
        var scenario = SavedCostScenarioTestData.Complete() with
        {
            OtherRecurringCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.NamedRecurringCostInput
                { Label = $"Recurring {i}", AmountSek = i, Cadence = Contracts.ManualCalculations.RecurringCostCadence.Annual }).ToArray(),
            OtherOneTimeCosts = Enumerable.Range(0, 50).Select(i => new Contracts.ManualCalculations.OneTimeCostInput
                { Label = $"Once {i}", AmountSek = i }).ToArray(),
        };
        await Json(await _client.PostAsJsonAsync("/api/saved-cost-scenarios", new { registrationNumber = "ABC123", scenario }), HttpStatusCode.Created);
        var transition = await Json(await _client.GetAsync("/api/household-transition"));
        var items = transition["vehicles"]![0]!["legacy"]!["items"]!.AsArray();
        Assert.Equal(104, items.Count);
        Assert.Equal(50, items.Count(x => x!["input"]!["kind"]!.GetValue<string>() == "recurring"));
        Assert.Equal(50, items.Count(x => x!["input"]!["kind"]!.GetValue<string>() == "oneTime"));
        await Json(await _client.PostAsJsonAsync("/api/household-transition", Confirm(transition)));
        var id = transition["vehicles"]![0]!["vehicleId"]!.GetValue<Guid>();
        var current = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}"));
        Assert.Equal(items.ToJsonString(), current["unresolvedLegacyItems"]!.ToJsonString());
    }

    private Task<JsonNode> Legacy(string registration) => CreateLegacy(registration);
    private async Task<JsonNode> CreateLegacy(string registration = "ABC123") => await Json(await _client.PostAsJsonAsync("/api/saved-cost-scenarios",
        new { registrationNumber = registration, scenario = SavedCostScenarioTestData.Complete() }), HttpStatusCode.Created);

    private static JsonObject Draft(string registration, long revision, Guid? id = null, long? baseRevision = null) => new()
    {
        ["expectedRevision"] = revision, ["input"] = new JsonObject
        {
            ["registrationNumber"] = registration, ["cost"] = new JsonObject { ["input"] = Vehicle() },
            ["baseVehicleId"] = id is null ? null : JsonValue.Create(id.Value), ["baseVehicleRevision"] = baseRevision is null ? null : JsonValue.Create(baseRevision.Value),
        },
    };
    private static JsonObject Confirm(JsonNode transition) => new()
    {
        ["expectedTransitionRevision"] = transition["revision"]!.DeepClone(),
        ["expectedProfileRevision"] = transition["profile"]!["revision"]!.DeepClone(), ["profile"] = Profile(),
        ["vehicles"] = new JsonArray(transition["vehicles"]!.AsArray().Select(x => (JsonNode)new JsonObject
        {
            ["vehicleId"] = x!["vehicleId"]!.DeepClone(), ["expectedRevision"] = x["revision"]!.DeepClone(),
            ["cost"] = new JsonObject
            {
                ["input"] = Vehicle(x["registrationNumber"]!.GetValue<string>()),
                ["legacyDecisions"] = new JsonArray(x["legacy"]!["items"]!.AsArray().Select(item => (JsonNode)new JsonObject
                    { ["key"] = item!["input"]!["key"]!.DeepClone(), ["disposition"] = "keepForReview" }).ToArray()),
            },
        }).ToArray()),
    };
}
