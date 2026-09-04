using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using CarExpenseCalculator.Api.Contracts.SavedListings;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class SavedListingEndpointTests(SavedListingApiFactory factory)
    : IClassFixture<SavedListingApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_returns_the_complete_normalized_authoritative_resource()
    {
        using var response = await CreateAsync(" abc-12d ", SavedListingTestData.Complete());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
        var saved = await ReadSavedListingAsync(response);
        Assert.Equal(7, saved.VehicleId.Version);
        Assert.Equal("ABC12D", saved.RegistrationNumber);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(1, saved.ListingVersion);
        Assert.Equal(1, saved.ListingSchemaVersion);
        Assert.Equal(SavedListingTestData.AnalyzedAtUtc, saved.AnalyzedAtUtc);
        Assert.Equal("https://example.com/listings/abc12d?campaign=Autumn", saved.NormalizedUrl);
        Assert.Equal("gpt-5.6-luna", saved.RequestedModel);
        Assert.Equal(2, saved.PromptVersion);
        Assert.Equal(2, saved.SchemaVersion);
        Assert.Equal([false, true], saved.Sources.Select(source => source.MatchesSubmittedUrl));
        Assert.Equal("ABC12D", saved.Listing.RegistrationNumber!.Value);
        Assert.Equal("Volvo", saved.Listing.Make!.Value);
        Assert.Equal("V70", saved.Listing.Model!.Value);
        Assert.Equal("Jönköpings län", saved.Listing.County!.Value);
        Assert.False(saved.Listing.TowBar!.Value);
        Assert.Equal(["Dragkrok", "Värmare"], saved.Listing.Equipment!.Values);
        Assert.Empty(saved.MissingFields);
        Assert.False(saved.HasSavedCostScenario);
        Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);
        Assert.Equal($"/api/saved-listings/{saved.VehicleId}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(0, factory.ExtractionService.ExtractionCallCount);
        Assert.Equal(0, factory.ExtractionService.StatusCallCount);
    }

    [Fact]
    public async Task Manual_create_fills_registration_and_preserves_unknown_zero_false_and_known_empty()
    {
        using var response = await CreateAsync(" ABC-123 ", SavedListingTestData.ManualOnly());
        var saved = await ReadSavedListingAsync(response);

        Assert.Equal("ABC123", saved.RegistrationNumber);
        Assert.Equal("ABC123", saved.Listing.RegistrationNumber!.Value);
        Assert.Equal("user", JsonName(saved.Listing.RegistrationNumber.Provenance.Origin));
        Assert.Equal("manual", JsonName(saved.Listing.RegistrationNumber.Provenance.ExtractionMethod));
        Assert.Equal("userConfirmed", JsonName(saved.Listing.RegistrationNumber.Provenance.Verification));
        Assert.Null(saved.RequestedModel);
        Assert.Null(saved.PromptVersion);
        Assert.Null(saved.SchemaVersion);
        Assert.Empty(saved.Sources);
        Assert.Null(saved.Listing.Model);
        Assert.Equal(0m, saved.Listing.PriceSek!.Value);
        Assert.Equal(0m, saved.Listing.OdometerKilometres!.Value);
        Assert.Equal(0, saved.Listing.OwnerCount!.Value);
        Assert.False(saved.Listing.TowBar!.Value);
        Assert.Empty(saved.Listing.FuelTypes!.Values);
        Assert.Empty(saved.Listing.EnergyConsumptions!.Values);
        Assert.Empty(saved.Listing.Equipment!.Values);
        Assert.Empty(saved.Listing.SellerClaims!.Values);
        Assert.Empty(saved.Listing.ConditionNotes!.Values);
        Assert.Equal("unavailable", JsonName(saved.Status));
        Assert.Contains(saved.MissingFields, field => JsonName(field) == "model");
    }

    [Fact]
    public async Task Duplicate_registration_returns_existing_identity_and_never_overwrites()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly("Original"));
        var created = await ReadSavedListingAsync(createdResponse);

        using var duplicateResponse = await CreateAsync(
            "abc-123",
            SavedListingTestData.ManualOnly("Replacement"));

        var problem = await ReadSavedProblemAsync(duplicateResponse, HttpStatusCode.Conflict);
        Assert.Equal("registrationNumberConflict", problem.Code);
        Assert.Equal(created.VehicleId, problem.ExistingVehicleId);
        Assert.Equal(created.Revision, problem.ActualRevision);
        using var getResponse = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        Assert.Equal("Original", (await ReadSavedListingAsync(getResponse)).Listing.VehicleLabel!.Value);
    }

    [Fact]
    public async Task List_returns_empty_then_current_summaries_in_store_order()
    {
        using var emptyResponse = await _client.GetAsync("/api/saved-listings");
        emptyResponse.EnsureSuccessStatusCode();
        Assert.Empty((await emptyResponse.Content.ReadFromJsonAsync<SavedListingSummaryResponse[]>())!);

        using var firstResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly("First"));
        var first = await ReadSavedListingAsync(firstResponse);
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        using var secondResponse = await CreateAsync(
            "DEF456",
            SavedListingTestData.Complete("DEF456", "Second"));
        var second = await ReadSavedListingAsync(secondResponse);

        using var listResponse = await _client.GetAsync("/api/saved-listings");
        listResponse.EnsureSuccessStatusCode();
        var summaries = await listResponse.Content.ReadFromJsonAsync<SavedListingSummaryResponse[]>();

        Assert.NotNull(summaries);
        Assert.Collection(
            summaries,
            summary =>
            {
                Assert.Equal(second.VehicleId, summary.VehicleId);
                Assert.Equal("DEF456", summary.RegistrationNumber);
                Assert.Equal("Second", summary.VehicleLabel);
                Assert.Equal("Volvo", summary.Make);
                Assert.Equal("V70", summary.Model);
                Assert.Equal(2016, summary.ModelYear);
                Assert.Equal(123_456.123456789012345m, summary.PriceSek);
                Assert.Equal(185_000.123456789012345m, summary.OdometerKilometres);
                Assert.Equal(0, summary.MissingFieldCount);
            },
            summary =>
            {
                Assert.Equal(first.VehicleId, summary.VehicleId);
                Assert.Equal("ABC123", summary.RegistrationNumber);
                Assert.Equal("First", summary.VehicleLabel);
                Assert.Null(summary.Model);
                Assert.True(summary.MissingFieldCount > 0);
            });
    }

    [Fact]
    public async Task Get_supports_uuid_and_formatted_registration_and_reports_missing_resources()
    {
        using var createdResponse = await CreateAsync(
            "ABC12D",
            SavedListingTestData.Complete());
        var created = await ReadSavedListingAsync(createdResponse);

        using var idResponse = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        using var registrationResponse = await _client.GetAsync(
            "/api/saved-listings/by-registration/abc-12d");
        AssertEquivalent(created, await ReadSavedListingAsync(idResponse));
        AssertEquivalent(created, await ReadSavedListingAsync(registrationResponse));

        using var missingIdResponse = await _client.GetAsync(
            $"/api/saved-listings/{Guid.CreateVersion7()}");
        Assert.Equal("savedListingNotFound",
            (await ReadSavedProblemAsync(missingIdResponse, HttpStatusCode.NotFound)).Code);
        using var missingRegistrationResponse = await _client.GetAsync(
            "/api/saved-listings/by-registration/DEF456");
        Assert.Equal("savedListingNotFound",
            (await ReadSavedProblemAsync(missingRegistrationResponse, HttpStatusCode.NotFound)).Code);
        using var invalidRegistrationResponse = await _client.GetAsync(
            "/api/saved-listings/by-registration/INVALID");
        var validation = await ReadValidationProblemAsync(invalidRegistrationResponse);
        Assert.Contains("registrationNumber", validation.Errors.Keys);
    }

    [Fact]
    public async Task Put_attaches_the_first_listing_to_a_scenario_only_vehicle()
    {
        using var scenarioResponse = await _client.PostAsJsonAsync(
            "/api/saved-cost-scenarios",
            new CreateSavedCostScenarioRequest
            {
                RegistrationNumber = "ABC123",
                Scenario = SavedCostScenarioTestData.Incomplete("Scenario first"),
            });
        scenarioResponse.EnsureSuccessStatusCode();
        var scenario = (await scenarioResponse.Content.ReadFromJsonAsync<SavedCostScenarioResponse>())!;

        using var attachResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{scenario.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = scenario.Revision,
                Listing = SavedListingTestData.Complete("ABC123", "Listing attached"),
            });
        var attached = await ReadSavedListingAsync(attachResponse);

        Assert.Equal(scenario.VehicleId, attached.VehicleId);
        Assert.Equal(2, attached.Revision);
        Assert.Equal(1, attached.ListingVersion);
        Assert.True(attached.HasSavedCostScenario);
        Assert.Equal("Listing attached", attached.Listing.VehicleLabel!.Value);
    }

    [Fact]
    public async Task Put_fully_replaces_a_listing_and_preserves_an_attached_scenario()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.Complete("ABC123"));
        var created = await ReadSavedListingAsync(createdResponse);
        using var attachScenarioResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            new ReplaceSavedCostScenarioRequest
            {
                ExpectedRevision = created.Revision,
                Scenario = SavedCostScenarioTestData.Incomplete("Scenario attached"),
                ListingLinkMode = ListingLinkMode.Preserve,
            });
        attachScenarioResponse.EnsureSuccessStatusCode();
        var attachedScenario = (await attachScenarioResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioResponse>())!;
        Assert.Null(attachedScenario.SourceListingVersion);
        Assert.Equal(1, attachedScenario.CurrentListingVersion);
        Assert.False(attachedScenario.IsListingOutdated);
        Assert.True(attachedScenario.HasSavedListing);

        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{created.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = attachedScenario.Revision,
                Listing = SavedListingTestData.ManualOnly(vehicleLabel: null),
            });
        var replaced = await ReadSavedListingAsync(replaceResponse);

        Assert.Equal(3, replaced.Revision);
        Assert.Equal(2, replaced.ListingVersion);
        Assert.True(replaced.HasSavedCostScenario);
        Assert.Null(replaced.Listing.VehicleLabel);
        Assert.Equal("Saab", replaced.Listing.Make!.Value);
        Assert.Null(replaced.Listing.Model);
        Assert.Empty(replaced.Sources);
        Assert.Empty(replaced.Listing.Equipment!.Values);
        using var scenarioGet = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        scenarioGet.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Linked_scenario_becomes_outdated_after_listing_replacement_and_can_be_reviewed()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.Complete("ABC123"));
        var listing = await ReadSavedListingAsync(createdResponse);

        using var attachResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{listing.VehicleId}",
            new ReplaceSavedCostScenarioRequest
            {
                ExpectedRevision = listing.Revision,
                Scenario = SavedCostScenarioTestData.Complete("Linked"),
                ListingLinkMode = ListingLinkMode.Current,
            });
        var linked = (await attachResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioResponse>())!;
        Assert.Equal(1, linked.SourceListingVersion);
        Assert.Equal(1, linked.CurrentListingVersion);
        Assert.False(linked.IsListingOutdated);
        Assert.True(linked.HasSavedListing);

        using var currentListingResponse = await _client.GetAsync(
            $"/api/saved-listings/{listing.VehicleId}");
        var currentListing = await ReadSavedListingAsync(currentListingResponse);
        Assert.Equal(1, currentListing.SavedCostScenarioSourceListingVersion);
        Assert.False(currentListing.SavedCostScenarioOutdated);

        using var replaceListingResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{listing.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = linked.Revision,
                Listing = SavedListingTestData.ManualOnly("Updated listing"),
            });
        var refreshed = await ReadSavedListingAsync(replaceListingResponse);
        Assert.Equal(2, refreshed.ListingVersion);
        Assert.Equal(1, refreshed.SavedCostScenarioSourceListingVersion);
        Assert.True(refreshed.SavedCostScenarioOutdated);

        using var scenarioResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{listing.VehicleId}");
        var staleScenario = (await scenarioResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioResponse>())!;
        Assert.Equal(1, staleScenario.SourceListingVersion);
        Assert.Equal(2, staleScenario.CurrentListingVersion);
        Assert.True(staleScenario.IsListingOutdated);

        using var reviewResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{listing.VehicleId}",
            new ReplaceSavedCostScenarioRequest
            {
                ExpectedRevision = refreshed.Revision,
                Scenario = staleScenario.Scenario,
                ListingLinkMode = ListingLinkMode.Current,
            });
        var reviewed = (await reviewResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioResponse>())!;
        Assert.Equal(2, reviewed.SourceListingVersion);
        Assert.Equal(2, reviewed.CurrentListingVersion);
        Assert.False(reviewed.IsListingOutdated);

        using var listResponse = await _client.GetAsync("/api/saved-listings");
        var summary = Assert.Single((await listResponse.Content
            .ReadFromJsonAsync<SavedListingSummaryResponse[]>())!);
        Assert.Equal(2, summary.SavedCostScenarioSourceListingVersion);
        Assert.False(summary.SavedCostScenarioOutdated);
    }

    [Fact]
    public async Task Stale_replace_and_delete_return_conflicts_without_changing_data()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly("Original"));
        var created = await ReadSavedListingAsync(createdResponse);
        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{created.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = created.Revision,
                Listing = SavedListingTestData.ManualOnly("Current"),
            });
        var current = await ReadSavedListingAsync(replaceResponse);

        using var staleReplaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{created.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = created.Revision,
                Listing = SavedListingTestData.ManualOnly("Must not save"),
            });
        var replaceProblem = await ReadSavedProblemAsync(
            staleReplaceResponse,
            HttpStatusCode.Conflict);
        Assert.Equal("revisionConflict", replaceProblem.Code);
        Assert.Equal(1, replaceProblem.ExpectedRevision);
        Assert.Equal(2, replaceProblem.ActualRevision);

        using var staleDeleteResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{created.VehicleId}?expectedRevision=1");
        var deleteProblem = await ReadSavedProblemAsync(
            staleDeleteResponse,
            HttpStatusCode.Conflict);
        Assert.Equal("revisionConflict", deleteProblem.Code);
        Assert.Equal(2, deleteProblem.ActualRevision);

        using var getResponse = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        AssertEquivalent(current, await ReadSavedListingAsync(getResponse));
    }

    [Fact]
    public async Task Delete_permanently_removes_a_combined_aggregate()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly("Combined"));
        var created = await ReadSavedListingAsync(createdResponse);
        using var attachScenarioResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            new ReplaceSavedCostScenarioRequest
            {
                ExpectedRevision = created.Revision,
                Scenario = SavedCostScenarioTestData.Incomplete("Combined"),
                ListingLinkMode = ListingLinkMode.Preserve,
            });
        var scenario = (await attachScenarioResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioResponse>())!;

        using var deleteResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{created.VehicleId}?expectedRevision={scenario.Revision}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var listingGet = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        Assert.Equal("savedListingNotFound",
            (await ReadSavedProblemAsync(listingGet, HttpStatusCode.NotFound)).Code);
        using var scenarioGet = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        Assert.Equal(HttpStatusCode.NotFound, scenarioGet.StatusCode);
    }

    [Fact]
    public async Task Missing_replace_delete_and_invalid_revisions_use_documented_problems()
    {
        var missingId = Guid.CreateVersion7();
        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{missingId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = 1,
                Listing = SavedListingTestData.ManualOnly(),
            });
        Assert.Equal("savedListingNotFound",
            (await ReadSavedProblemAsync(replaceResponse, HttpStatusCode.NotFound)).Code);

        using var deleteResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{missingId}?expectedRevision=1");
        Assert.Equal("savedListingNotFound",
            (await ReadSavedProblemAsync(deleteResponse, HttpStatusCode.NotFound)).Code);

        using var invalidReplaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{missingId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = 0,
                Listing = SavedListingTestData.ManualOnly(),
            });
        _ = await ReadValidationProblemAsync(invalidReplaceResponse);
        using var invalidDeleteResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{missingId}?expectedRevision=0");
        _ = await ReadValidationProblemAsync(invalidDeleteResponse);
        using var missingDeleteRevisionResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{missingId}");
        _ = await ReadValidationProblemAsync(missingDeleteRevisionResponse);
    }

    [Fact]
    public async Task Unsupported_stored_version_returns_conflict_and_put_changes_nothing()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly("Original"));
        var created = await ReadSavedListingAsync(createdResponse);
        await factory.ExecuteDatabaseCommandAsync(
            "ALTER TABLE vehicle_listings DROP CONSTRAINT ck_vehicle_listings_versions; "
            + "UPDATE vehicle_listings SET listing_schema_version = 99");

        try
        {
            using var getResponse = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
            var getProblem = await ReadSavedProblemAsync(getResponse, HttpStatusCode.Conflict);
            Assert.Equal("unsupportedSavedListingVersion", getProblem.Code);
            Assert.Equal(99, getProblem.ListingSchemaVersion);
            Assert.Null(getProblem.PromptVersion);
            Assert.Null(getProblem.SchemaVersion);

            using var replaceResponse = await _client.PutAsJsonAsync(
                $"/api/saved-listings/{created.VehicleId}",
                new ReplaceSavedListingRequest
                {
                    ExpectedRevision = created.Revision,
                    Listing = SavedListingTestData.ManualOnly("Must not save"),
                });
            var replaceProblem = await ReadSavedProblemAsync(
                replaceResponse,
                HttpStatusCode.Conflict);
            Assert.Equal("unsupportedSavedListingVersion", replaceProblem.Code);
        }
        finally
        {
            await factory.ExecuteDatabaseCommandAsync(
                "UPDATE vehicle_listings SET listing_schema_version = 1; "
                + "ALTER TABLE vehicle_listings ADD CONSTRAINT ck_vehicle_listings_versions "
                + "CHECK (listing_version >= 1 AND listing_schema_version = 1)");
        }

        using var restoredGet = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        var unchanged = await ReadSavedListingAsync(restoredGet);
        Assert.Equal("Original", unchanged.Listing.VehicleLabel!.Value);
        Assert.Equal(1, unchanged.Revision);
        Assert.Equal(1, unchanged.ListingVersion);
    }

    [Fact]
    public async Task Semantic_and_mapping_validation_paths_are_prefixed_consistently()
    {
        var manual = SavedListingTestData.ManualOnly() with
        {
            Draft = SavedListingTestData.ManualOnly().Draft with
            {
                Make = SavedListingTestData.Value(
                    " ",
                    SavedListingTestData.Manual("https://example.com/manual/abc123")),
                PriceSek = SavedListingTestData.Value(
                    -1m,
                    SavedListingTestData.Manual("https://example.com/manual/abc123")),
            },
        };
        using var semanticResponse = await CreateAsync("ABC123", manual);
        var semantic = await ReadValidationProblemAsync(semanticResponse);
        Assert.Equal(
            ["listing.draft.make.value", "listing.draft.priceSek.value"],
            semantic.Errors.Keys);

        var invalidUrls = SavedListingTestData.ManualOnly() with
        {
            Sources = ["https://localhost/listing"],
            Draft = SavedListingTestData.EmptyDraft() with
            {
                Make = SavedListingTestData.Value(
                    "Volvo",
                    SavedListingTestData.Manual("http://127.0.0.1/listing")),
            },
        };
        using var mappingResponse = await CreateAsync("ABC123", invalidUrls);
        var mapping = await ReadValidationProblemAsync(mappingResponse);
        Assert.Equal(
            ["listing.sources[0]", "listing.draft.make.provenance.sourceUrl"],
            mapping.Errors.Keys);

        var incompleteMetadata = SavedListingTestData.ManualOnly() with
        {
            RequestedModel = "gpt-5.6-luna",
        };
        using var metadataResponse = await CreateAsync("ABC123", incompleteMetadata);
        var metadata = await ReadValidationProblemAsync(metadataResponse);
        Assert.Contains("listing.requestedModel", metadata.Errors.Keys);

        var mismatchedRegistration = SavedListingTestData.Complete("DEF456");
        using var mismatchResponse = await CreateAsync("ABC123", mismatchedRegistration);
        var mismatch = await ReadValidationProblemAsync(mismatchResponse);
        Assert.Contains("listing.draft.registrationNumber.value", mismatch.Errors.Keys);
    }

    [Fact]
    public async Task Missing_nullable_properties_malformed_json_and_numeric_enums_return_validation_problems()
    {
        var create = new CreateSavedListingRequest
        {
            RegistrationNumber = "ABC123",
            Listing = SavedListingTestData.ManualOnly(),
        };
        var node = JsonSerializer.SerializeToNode(create, JsonOptions())!.AsObject();

        var missingMetadata = node.DeepClone().AsObject();
        missingMetadata["listing"]!.AsObject().Remove("requestedModel");
        using var missingMetadataResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/saved-listings",
            missingMetadata.ToJsonString());
        _ = await ReadValidationProblemAsync(missingMetadataResponse);

        var missingNullableDraftField = node.DeepClone().AsObject();
        missingNullableDraftField["listing"]!["draft"]!.AsObject().Remove("county");
        using var missingDraftResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/saved-listings",
            missingNullableDraftField.ToJsonString());
        _ = await ReadValidationProblemAsync(missingDraftResponse);

        var nullSource = node.DeepClone().AsObject();
        nullSource["listing"]!["sources"] = new JsonArray((JsonNode?)null);
        using var nullSourceResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/saved-listings",
            nullSource.ToJsonString());
        _ = await ReadValidationProblemAsync(nullSourceResponse);

        var numericEnum = JsonSerializer.SerializeToNode(
            new CreateSavedListingRequest
            {
                RegistrationNumber = "ABC123",
                Listing = SavedListingTestData.Complete("ABC123"),
            },
            JsonOptions())!.AsObject();
        numericEnum["listing"]!["draft"]!["sellerType"]!["value"] = 1;
        using var numericEnumResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/saved-listings",
            numericEnum.ToJsonString());
        _ = await ReadValidationProblemAsync(numericEnumResponse);

        using var malformedResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/saved-listings",
            "{ \"registrationNumber\": \"ABC123\", \"listing\": ");
        _ = await ReadValidationProblemAsync(malformedResponse);
    }

    [Fact]
    public async Task Saved_lifecycle_never_invokes_the_extractor()
    {
        using var createResponse = await CreateAsync(
            "ABC123",
            SavedListingTestData.ManualOnly());
        var created = await ReadSavedListingAsync(createResponse);
        using var listResponse = await _client.GetAsync("/api/saved-listings");
        listResponse.EnsureSuccessStatusCode();
        using var idResponse = await _client.GetAsync($"/api/saved-listings/{created.VehicleId}");
        idResponse.EnsureSuccessStatusCode();
        using var registrationResponse = await _client.GetAsync(
            "/api/saved-listings/by-registration/ABC123");
        registrationResponse.EnsureSuccessStatusCode();
        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-listings/{created.VehicleId}",
            new ReplaceSavedListingRequest
            {
                ExpectedRevision = created.Revision,
                Listing = SavedListingTestData.ManualOnly("Updated"),
            });
        var replaced = await ReadSavedListingAsync(replaceResponse);
        using var deleteResponse = await _client.DeleteAsync(
            $"/api/saved-listings/{created.VehicleId}?expectedRevision={replaced.Revision}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.Equal(0, factory.ExtractionService.ExtractionCallCount);
        Assert.Equal(0, factory.ExtractionService.StatusCallCount);
    }

    [Fact]
    public async Task Openapi_exposes_all_routes_required_nullability_and_versioned_problems()
    {
        using var response = await _client.GetAsync("/api/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/saved-listings").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/saved-listings").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/saved-listings/{vehicleId}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/saved-listings/{vehicleId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/saved-listings/{vehicleId}").TryGetProperty("delete", out _));
        Assert.True(paths
            .GetProperty("/api/saved-listings/by-registration/{registrationNumber}")
            .TryGetProperty("get", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            ["registrationNumber", "listing"],
            RequiredProperties(schemas.GetProperty("CreateSavedListingRequest")));
        Assert.Equal(
            ["expectedRevision", "listing"],
            RequiredProperties(schemas.GetProperty("ReplaceSavedListingRequest")));
        Assert.Equal(
            [
                "submittedUrl", "analyzedAtUtc", "requestedModel", "promptVersion",
                "schemaVersion", "sources", "draft",
            ],
            RequiredProperties(schemas.GetProperty("ReviewedListingInput")));
        Assert.Equal(
            [
                "registrationNumber", "make", "model", "variant", "modelYear", "vin", "vehicleLabel",
                "priceSek", "odometerKilometres", "sellerType", "locality", "county", "publishedDate",
                "updatedDate", "imageCount", "fuelTypes", "transmission", "drivetrain", "bodyType",
                "colour", "horsepower", "engineDisplacementCubicCentimetres", "energyConsumptions",
                "annualVehicleTaxSek", "ownerCount", "firstRegistrationDate", "lastInspectionDate",
                "nextInspectionDate", "towBar", "equipment", "sellerClaims", "conditionNotes",
            ],
            RequiredProperties(schemas.GetProperty("ListingDraftInput")));
        Assert.Equal(
            [
                "vehicleId", "registrationNumber", "revision", "listingVersion", "listingSchemaVersion",
                "createdAtUtc", "updatedAtUtc", "analyzedAtUtc", "submittedUrl", "normalizedUrl", "status",
                "requestedModel", "promptVersion", "schemaVersion", "sources", "listing", "missingFields",
                "hasSavedCostScenario", "savedCostScenarioSourceListingVersion",
                "savedCostScenarioOutdated",
            ],
            RequiredProperties(schemas.GetProperty("SavedListingResponse")));
        Assert.Equal(["code"], RequiredProperties(schemas.GetProperty("SavedListingProblemDetails")));
        Assert.True(HasNullType(
            schemas.GetProperty("SavedListingResponse")
                .GetProperty("properties")
                .GetProperty("savedCostScenarioSourceListingVersion")));
        Assert.True(HasNullType(
            schemas.GetProperty("ReviewedListingInput")
                .GetProperty("properties")
                .GetProperty("requestedModel")));
        Assert.Equal("uuid", schemas.GetProperty("SavedListingResponse")
            .GetProperty("properties").GetProperty("vehicleId").GetProperty("format").GetString());
        Assert.Equal("date-time", schemas.GetProperty("SavedListingResponse")
            .GetProperty("properties").GetProperty("updatedAtUtc").GetProperty("format").GetString());
    }

    private async Task<HttpResponseMessage> CreateAsync(
        string registrationNumber,
        ReviewedListingInput listing)
    {
        return await _client.PostAsJsonAsync(
            "/api/saved-listings",
            new CreateSavedListingRequest
            {
                RegistrationNumber = registrationNumber,
                Listing = listing,
            });
    }

    private static async Task<SavedListingResponse> ReadSavedListingAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<SavedListingResponse>();
        return Assert.IsType<SavedListingResponse>(saved);
    }

    private static async Task<SavedListingProblemDetails> ReadSavedProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<SavedListingProblemDetails>();
        return Assert.IsType<SavedListingProblemDetails>(problem);
    }

    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        return Assert.IsType<ValidationProblemDetails>(problem);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        string json)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        return await _client.SendAsync(request);
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);

    private static string JsonName<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static IReadOnlyList<string> RequiredProperties(JsonElement schema)
    {
        return schema.GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString()!)
            .ToArray();
    }

    private static bool HasNullType(JsonElement schema)
    {
        if (schema.TryGetProperty("oneOf", out var alternatives))
        {
            return alternatives.EnumerateArray().Any(alternative =>
                alternative.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "null");
        }

        return schema.TryGetProperty("type", out var declaredType)
            && declaredType.ValueKind == JsonValueKind.Array
            && declaredType.EnumerateArray().Any(type => type.GetString() == "null");
    }

    private static void AssertEquivalent(SavedListingResponse expected, SavedListingResponse actual)
    {
        Assert.Equal(expected.VehicleId, actual.VehicleId);
        Assert.Equal(expected.RegistrationNumber, actual.RegistrationNumber);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.ListingVersion, actual.ListingVersion);
        Assert.Equal(
            expected.SavedCostScenarioSourceListingVersion,
            actual.SavedCostScenarioSourceListingVersion);
        Assert.Equal(expected.SavedCostScenarioOutdated, actual.SavedCostScenarioOutdated);
        Assert.Equal(expected.NormalizedUrl, actual.NormalizedUrl);
        Assert.Equivalent(expected.Listing, actual.Listing, strict: true);
        Assert.Equal(expected.MissingFields, actual.MissingFields);
    }
}
