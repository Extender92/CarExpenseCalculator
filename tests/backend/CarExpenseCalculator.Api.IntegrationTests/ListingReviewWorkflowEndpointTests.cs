using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ListingReviewWorkflowEndpointTests(SavedCostScenarioApiFactory factory)
    : IClassFixture<SavedCostScenarioApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private static JsonObject Input(string page = "123", string? registration = null)
    {
        var url = $"https://www.blocket.se/mobility/item/{page}?ci=3";
        var original = SavedListingTestData.Complete();
        var input = original with { SubmittedUrl = url, Sources = [url], PromptVersion = 4, SchemaVersion = 3,
            Draft = original.Draft with { RegistrationNumber = null } };
        var node = JsonSerializer.SerializeToNode(input, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        foreach (var field in node["draft"]!.AsObject())
            if (field.Value is JsonObject obj && obj["provenance"] is JsonObject source) {
                source["sourceUrl"] = url;
                source["origin"] = "listing"; source["extractionMethod"] = "html"; source["verification"] = "unverified";
            }
        if (registration is not null) node["draft"]!["registrationNumber"] = new JsonObject {
            ["value"] = registration, ["provenance"] = new JsonObject { ["origin"] = "user", ["extractionMethod"] = "manual",
                ["verification"] = "unverified", ["sourceUrl"] = url } };
        return node;
    }
    private async Task<JsonNode> CreateDraft(JsonObject input) => await Json(await _client.PostAsJsonAsync("/api/listing-review-drafts", new { input }), HttpStatusCode.Created);

    [Fact]
    public async Task Multiple_unregistered_drafts_round_trip_without_creating_inventory_and_conflicts_preserve_original()
    {
        var first = await CreateDraft(Input()); var second = await CreateDraft(Input("456"));
        Assert.NotEqual(first["id"]!.GetValue<Guid>(), second["id"]!.GetValue<Guid>());
        Assert.Equal(2, (await Json(await _client.GetAsync("/api/listing-review-drafts"))).AsArray().Count);
        Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
        var id = first["id"]!.GetValue<Guid>();
        var read = await Json(await _client.GetAsync($"/api/listing-review-drafts/{id}"));
        Assert.Null(read["input"]!["draft"]!["registrationNumber"]);
        Assert.Equal(123456.123456789012345m, read["input"]!["draft"]!["priceSek"]!["value"]!.GetValue<decimal>());
        var samePage = Input().With("submittedUrl", JsonValue.Create("https://blocket.se/mobility/item/123?ci=7"));
        var duplicate = await Json(await _client.PostAsJsonAsync("/api/listing-review-drafts", new { input = samePage }), HttpStatusCode.Conflict);
        Assert.Equal("reviewDraftAlreadyExists", duplicate["code"]!.GetValue<string>());
        Assert.Equal(id, duplicate["reviewDraftId"]!.GetValue<Guid>());
        var updated = await Json(await _client.PutAsJsonAsync($"/api/listing-review-drafts/{id}", new { expectedRevision = 1, input = Input(registration: "TST123") }));
        Assert.Equal(2, updated["revision"]!.GetValue<long>());
        await Json(await _client.DeleteAsync($"/api/listing-review-drafts/{id}?expectedRevision=1"), HttpStatusCode.Conflict);
        await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 1 }), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/listing-review-drafts/{id}?expectedRevision=2")).StatusCode);
        await Json(await _client.GetAsync($"/api/listing-review-drafts/{id}"), HttpStatusCode.NotFound);
        Assert.Single((await Json(await _client.GetAsync("/api/listing-review-drafts"))).AsArray());
    }

    [Fact]
    public async Task Adoption_requires_registration_and_consumes_only_the_chosen_draft_atomically()
    {
        var created = await CreateDraft(Input()); var id = created["id"]!.GetValue<Guid>();
        await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 1 }), HttpStatusCode.BadRequest);
        await Json(await _client.GetAsync($"/api/listing-review-drafts/{id}"));
        await Json(await _client.PutAsJsonAsync($"/api/listing-review-drafts/{id}", new { expectedRevision = 1, input = Input(registration: "TST123") }));
        var adopted = await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 2 }));
        Assert.Equal("TST123", adopted["registrationNumber"]!.GetValue<string>());
        Assert.Equal(3, adopted["listingSchemaVersion"]!.GetValue<int>());
        Assert.Equal("unverified", adopted["listing"]!["registrationNumber"]!["provenance"]!["verification"]!.GetValue<string>());
        await Json(await _client.GetAsync($"/api/listing-review-drafts/{id}"), HttpStatusCode.NotFound);
        var vehicleId = adopted["vehicleId"]!.GetValue<Guid>();
        var cost = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicleId}"));
        Assert.Null(cost["input"]);
        var facts = await Json(await _client.GetAsync($"/api/vehicle-facts/{vehicleId}"));
        Assert.Null(facts["input"]); Assert.Null(facts["costConfirmedAt"]);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/saved-listings/{vehicleId}?expectedRevision=1")).StatusCode);
        Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
    }

    [Fact]
    public async Task Existing_registration_requires_both_revisions_and_preserves_costs_and_reviewed_facts()
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create("TST123")), HttpStatusCode.Created);
        var vehicleId = vehicle["vehicleId"]!.GetValue<Guid>();
        var originalFacts = await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{vehicleId}", new { expectedRevision = 1,
            input = new { edits = new { seats = new { kind = "manual", manual = new { value = 5 } } } } }));
        var draft = await CreateDraft(Input(registration: "TST123")); var id = draft["id"]!.GetValue<Guid>();
        await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 1 }), HttpStatusCode.Conflict);
        await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 1, existingVehicleId = vehicleId, expectedVehicleRevision = 1 }), HttpStatusCode.Conflict);
        await Json(await _client.GetAsync($"/api/listing-review-drafts/{id}"));
        await Json(await _client.PostAsJsonAsync($"/api/listing-review-drafts/{id}/adopt", new { expectedRevision = 1, existingVehicleId = vehicleId, expectedVehicleRevision = 2 }));
        var cost = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{vehicleId}"));
        Assert.Equal(50000m, cost["input"]!["priceSek"]!.GetValue<decimal>());
        var facts = await Json(await _client.GetAsync($"/api/vehicle-facts/{vehicleId}"));
        Assert.Equal(originalFacts["input"]!.ToJsonString(), facts["input"]!.ToJsonString());
        Assert.Equal(3, facts["revision"]!.GetValue<long>());
    }

    [Fact]
    public async Task Preview_is_read_only_validates_source_revision_and_saved_cost_claims()
    {
        var listing = Input(registration: "TST123");
        var adopted = await Json(await _client.PostAsJsonAsync("/api/saved-listings", new { registrationNumber = "TST123", listing }), HttpStatusCode.Created);
        var id = adopted["vehicleId"]!.GetValue<Guid>();
        var request = new JsonObject { ["vehicleId"] = id, ["expectedVehicleRevision"] = 1, ["expectedListingVersion"] = 1,
            ["target"] = new JsonObject { ["candidateKey"] = "TST123", ["priceSek"] = 0 } };
        var preview = await Json(await _client.PostAsJsonAsync("/api/listing-reuse/preview", request));
        Assert.True(preview["purchasePrice"]!["requiresReplacement"]!.GetValue<bool>());
        Assert.Equal(123456.123456789012345m, preview["purchasePrice"]!["value"]!.GetValue<decimal>());
        Assert.Equal(1, (await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}")))["revision"]!.GetValue<long>());
        var target = request["target"]!.DeepClone();
        target["priceSek"] = preview["purchasePrice"]!["value"]!.DeepClone();
        target["priceSource"] = preview["purchasePrice"]!["source"]!.DeepClone();
        var write = new JsonObject { ["expectedRevision"] = 1, ["cost"] = new JsonObject { ["input"] = target } };
        var saved = await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", write));
        Assert.Equal(target["priceSource"]!.ToJsonString(), saved["input"]!["priceSource"]!.ToJsonString());
        await Json(await _client.PostAsJsonAsync("/api/listing-reuse/preview", request), HttpStatusCode.Conflict);
        write["expectedRevision"] = 2; target["priceSek"] = 1;
        var error = await Json(await _client.PutAsJsonAsync($"/api/vehicle-cost-inputs/{id}", write), HttpStatusCode.BadRequest);
        Assert.Contains("invalidListingSource", error.ToJsonString());
        Assert.Equal(2, (await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{id}")))["revision"]!.GetValue<long>());
    }

    [Fact]
    public async Task Unsaved_preview_rejects_incomplete_extraction_metadata_without_a_write()
    {
        var input = Input(); input["promptVersion"] = null;
        await Json(await _client.PostAsJsonAsync("/api/listing-reuse/preview", new { unsavedListing = input,
            target = new { candidateKey = "draft", priceSek = (decimal?)null } }), HttpStatusCode.BadRequest);
        Assert.Empty((await Json(await _client.GetAsync("/api/listing-review-drafts"))).AsArray());
        Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray());
    }

    [Fact]
    public async Task Manual_edit_confirmation_and_later_edit_have_distinct_evidence_and_server_timestamps()
    {
        var vehicle = await Json(await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create("TST123")), HttpStatusCode.Created);
        var id = vehicle["vehicleId"]!.GetValue<Guid>();
        async Task<JsonNode> Edit(int revision, string kind, int? value = null) => await Json(await _client.PutAsJsonAsync($"/api/vehicle-facts/{id}",
            new { expectedRevision = revision, input = new { edits = new { ownerCount = new { kind, manual = value.HasValue ? new { value = value.Value } : null } } } }));
        var edited = await Edit(1, "editManual", 0);
        JsonNode Evidence(JsonNode node) => node["input"]!["facts"]!["ownerCount"]!["observations"]![0]!["evidence"]!;
        Assert.Equal("unverified", Evidence(edited)["verification"]!.GetValue<string>()); Assert.Null(Evidence(edited)["confirmedAt"]);
        var confirmed = await Edit(2, "confirmCurrent");
        Assert.Equal("userConfirmed", Evidence(confirmed)["verification"]!.GetValue<string>()); Assert.NotNull(Evidence(confirmed)["confirmedAt"]);
        var later = await Edit(3, "editManual", 1);
        Assert.Equal("unverified", Evidence(later)["verification"]!.GetValue<string>()); Assert.Null(Evidence(later)["confirmedAt"]);
        Assert.Null(later["costConfirmedAt"]);
    }

    [Theory]
    [InlineData("{\"mode\":\"override\",\"value\":null}", true)]
    [InlineData("{\"mode\":\"override\",\"value\":{\"single\":0}}", true)]
    [InlineData("{\"mode\":\"override\",\"value\":{\"favorable\":100,\"baseline\":30,\"cautious\":0}}", true)]
    [InlineData("{\"mode\":\"override\",\"value\":{\"favorable\":100,\"baseline\":30}}", false)]
    [InlineData("{\"mode\":\"inherit\",\"value\":{\"single\":20}}", false)]
    public async Task Electric_share_validates_complete_shape_and_round_trips_explicit_unknown(string json, bool valid)
    {
        var input = Vehicle(); input["electricDrivingShare"] = JsonNode.Parse(json);
        var response = await _client.PostAsJsonAsync("/api/vehicle-cost-inputs", Create(input: input));
        var result = await Json(response, valid ? HttpStatusCode.Created : HttpStatusCode.BadRequest);
        if (!valid) { Assert.Empty((await Json(await _client.GetAsync("/api/vehicle-cost-inputs"))).AsArray()); return; }
        var fetched = await Json(await _client.GetAsync($"/api/vehicle-cost-inputs/{result["vehicleId"]!.GetValue<Guid>()}"));
        Assert.Equal(input["electricDrivingShare"]!["mode"]!.ToJsonString(), fetched["input"]!["electricDrivingShare"]!["mode"]!.ToJsonString());
        Assert.Equal(input["electricDrivingShare"]!["value"]?["single"]?.ToJsonString(), fetched["input"]!["electricDrivingShare"]!["value"]?["single"]?.ToJsonString());
    }
}
