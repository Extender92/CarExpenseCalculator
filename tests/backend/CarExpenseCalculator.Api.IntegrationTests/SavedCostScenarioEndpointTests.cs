using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class SavedCostScenarioEndpointTests(SavedCostScenarioApiFactory factory)
    : IClassFixture<SavedCostScenarioApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_normalizes_and_returns_the_authoritative_saved_result()
    {
        using var response = await CreateAsync(" abc-12d ", SavedCostScenarioTestData.Complete());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
        var saved = await ReadSavedScenarioAsync(response);
        Assert.Equal(7, saved.VehicleId.Version);
        Assert.Equal("ABC12D", saved.RegistrationNumber);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(1, saved.CalculationVersion);
        Assert.Equal(1, saved.ResultSchemaVersion);
        Assert.Null(saved.SourceListingVersion);
        Assert.Null(saved.CurrentListingVersion);
        Assert.False(saved.IsListingOutdated);
        Assert.False(saved.HasSavedListing);
        Assert.Equal("Example car", saved.Scenario.VehicleLabel);
        Assert.Equal("Petrol", saved.Scenario.EnergySources[0].Label);
        Assert.Equal("Parking", saved.Scenario.OtherRecurringCosts[0].Label);
        Assert.Equal("Initial repair", saved.Scenario.OtherOneTimeCosts[0].Label);
        Assert.Equal(64_000m, saved.Result.CashFlow.KnownTotalSek);
        Assert.Equal(49_000m, saved.Result.NetOwnershipCost?.KnownTotalSek);
        Assert.NotEqual(default, saved.CreatedAtUtc);
        Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);
        Assert.Equal(saved.UpdatedAtUtc, saved.CalculatedAtUtc);
        Assert.Equal(
            $"/api/saved-cost-scenarios/{saved.VehicleId}",
            response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_recalculates_and_never_trusts_a_submitted_result_property()
    {
        const string request = """
            {
              "registrationNumber": "ABC123",
              "scenario": {
                "calculationPeriodMonths": 12,
                "purchasePriceSek": 10000,
                "annualDistanceKilometres": 0,
                "energySources": [],
                "vehicleTax": null,
                "insurance": null,
                "maintenanceAndRepairs": null,
                "otherRecurringCosts": [],
                "otherOneTimeCosts": []
              },
              "result": { "cashFlow": { "knownTotalSek": 1 } }
            }
            """;

        using var response = await SendJsonAsync(HttpMethod.Post, "/api/saved-cost-scenarios", request);

        var saved = await ReadSavedScenarioAsync(response);
        Assert.Equal(10_000m, saved.Result.CashFlow.KnownTotalSek);
        Assert.Null(saved.Scenario.VehicleTax);
        Assert.Null(saved.Scenario.Insurance);
        Assert.Null(saved.Scenario.MaintenanceAndRepairs);
        Assert.Null(saved.Scenario.Financing);
        Assert.Null(saved.Result.NetOwnershipCost);
    }

    [Fact]
    public async Task Duplicate_registration_returns_a_typed_conflict()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Incomplete());
        var created = await ReadSavedScenarioAsync(createdResponse);

        using var duplicateResponse = await CreateAsync(
            "abc-123",
            SavedCostScenarioTestData.Complete());

        var problem = await ReadSavedProblemAsync(duplicateResponse, HttpStatusCode.Conflict);
        Assert.Equal("registrationNumberConflict", problem.Code);
        Assert.Equal(created.VehicleId, problem.ExistingVehicleId);
    }

    [Fact]
    public async Task List_returns_ordered_summaries_for_complete_and_incomplete_scenarios()
    {
        using var emptyResponse = await _client.GetAsync("/api/saved-cost-scenarios");
        emptyResponse.EnsureSuccessStatusCode();
        Assert.Empty((await emptyResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioSummaryResponse[]>())!);

        using var firstResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Complete(" First "));
        var first = await ReadSavedScenarioAsync(firstResponse);
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        using var secondResponse = await CreateAsync(
            "DEF456",
            SavedCostScenarioTestData.Incomplete(" Second "));
        var second = await ReadSavedScenarioAsync(secondResponse);

        using var listResponse = await _client.GetAsync("/api/saved-cost-scenarios");
        listResponse.EnsureSuccessStatusCode();
        var summaries = await listResponse.Content
            .ReadFromJsonAsync<SavedCostScenarioSummaryResponse[]>();

        Assert.NotNull(summaries);
        Assert.Collection(
            summaries,
            summary =>
            {
                Assert.Equal(second.VehicleId, summary.VehicleId);
                Assert.Equal("DEF456", summary.RegistrationNumber);
                Assert.Equal("Second", summary.VehicleLabel);
                Assert.Equal(10_000m, summary.CashFlowKnownTotalSek);
                Assert.Null(summary.NetOwnershipCostKnownTotalSek);
                Assert.False(summary.Completeness.IsComplete);
                Assert.Null(summary.SourceListingVersion);
                Assert.False(summary.IsListingOutdated);
                Assert.False(summary.HasSavedListing);
            },
            summary =>
            {
                Assert.Equal(first.VehicleId, summary.VehicleId);
                Assert.Equal("ABC123", summary.RegistrationNumber);
                Assert.Equal(64_000m, summary.CashFlowKnownTotalSek);
                Assert.Equal(49_000m, summary.NetOwnershipCostKnownTotalSek);
                Assert.True(summary.Completeness.IsComplete);
            });
    }

    [Fact]
    public async Task Get_supports_uuid_and_normalized_registration_lookup()
    {
        using var createdResponse = await CreateAsync(
            "ABC12D",
            SavedCostScenarioTestData.Complete());
        var created = await ReadSavedScenarioAsync(createdResponse);

        using var idResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        using var registrationResponse = await _client.GetAsync(
            "/api/saved-cost-scenarios/by-registration/abc-12d");

        AssertSavedScenarioEquivalent(created, await ReadSavedScenarioAsync(idResponse));
        AssertSavedScenarioEquivalent(
            created,
            await ReadSavedScenarioAsync(registrationResponse));
    }

    [Fact]
    public async Task Missing_and_invalid_lookups_return_the_documented_problems()
    {
        using var idResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{Guid.CreateVersion7()}");
        var idProblem = await ReadSavedProblemAsync(idResponse, HttpStatusCode.NotFound);
        Assert.Equal("savedCostScenarioNotFound", idProblem.Code);

        using var registrationResponse = await _client.GetAsync(
            "/api/saved-cost-scenarios/by-registration/DEF456");
        var registrationProblem = await ReadSavedProblemAsync(
            registrationResponse,
            HttpStatusCode.NotFound);
        Assert.Equal("savedCostScenarioNotFound", registrationProblem.Code);

        using var invalidResponse = await _client.GetAsync(
            "/api/saved-cost-scenarios/by-registration/INVALID");
        var validationProblem = await ReadValidationProblemAsync(invalidResponse);
        Assert.Contains("registrationNumber", validationProblem.Errors.Keys);
    }

    [Fact]
    public async Task Invalid_create_registration_returns_validation_problem()
    {
        using var response = await CreateAsync(
            "INVALID",
            SavedCostScenarioTestData.Incomplete());

        var problem = await ReadValidationProblemAsync(response);
        Assert.Equal(["registrationNumber"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Replace_fully_overwrites_the_scenario_and_increments_revision()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Complete());
        var created = await ReadSavedScenarioAsync(createdResponse);
        var request = new ReplaceSavedCostScenarioRequest
        {
            ExpectedRevision = created.Revision,
            Scenario = SavedCostScenarioTestData.Replacement(),
            ListingLinkMode = ListingLinkMode.Preserve,
        };

        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            request);

        var replaced = await ReadSavedScenarioAsync(replaceResponse);
        Assert.Equal(created.VehicleId, replaced.VehicleId);
        Assert.Equal("ABC123", replaced.RegistrationNumber);
        Assert.Equal(2, replaced.Revision);
        AssertTimestampEquivalent(created.CreatedAtUtc, replaced.CreatedAtUtc);
        Assert.True(replaced.UpdatedAtUtc >= created.UpdatedAtUtc);
        Assert.Equal("Replacement car", replaced.Scenario.VehicleLabel);
        Assert.Empty(replaced.Scenario.EnergySources);
        Assert.Equal("Storage", Assert.Single(replaced.Scenario.OtherRecurringCosts).Label);
        Assert.Equal("Inspection", Assert.Single(replaced.Scenario.OtherOneTimeCosts).Label);
        Assert.Equal(13_100m, replaced.Result.CashFlow.KnownTotalSek);
        Assert.Equal(5_100m, replaced.Result.NetOwnershipCost?.KnownTotalSek);

        using var getResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        AssertSavedScenarioEquivalent(replaced, await ReadSavedScenarioAsync(getResponse));
    }

    [Fact]
    public async Task Current_listing_link_requires_a_saved_listing()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Complete());
        var created = await ReadSavedScenarioAsync(createdResponse);

        using var response = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            new ReplaceSavedCostScenarioRequest
            {
                ExpectedRevision = created.Revision,
                Scenario = SavedCostScenarioTestData.Replacement(),
                ListingLinkMode = ListingLinkMode.Current,
            });

        var problem = await ReadSavedProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("listingLinkUnavailable", problem.Code);
        using var unchangedResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        var unchanged = await ReadSavedScenarioAsync(unchangedResponse);
        Assert.Equal(created.Revision, unchanged.Revision);
        Assert.Null(unchanged.SourceListingVersion);
        Assert.False(unchanged.HasSavedListing);
    }

    [Fact]
    public async Task Stale_replace_returns_revision_conflict_without_changing_data()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Incomplete());
        var created = await ReadSavedScenarioAsync(createdResponse);
        var firstReplacement = new ReplaceSavedCostScenarioRequest
        {
            ExpectedRevision = created.Revision,
            Scenario = SavedCostScenarioTestData.Replacement(),
            ListingLinkMode = ListingLinkMode.Preserve,
        };
        using var replacedResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            firstReplacement);
        var replaced = await ReadSavedScenarioAsync(replacedResponse);

        var staleReplacement = new ReplaceSavedCostScenarioRequest
        {
            ExpectedRevision = created.Revision,
            Scenario = SavedCostScenarioTestData.Complete("Must not be saved"),
            ListingLinkMode = ListingLinkMode.Preserve,
        };
        using var staleResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}",
            staleReplacement);

        var problem = await ReadSavedProblemAsync(staleResponse, HttpStatusCode.Conflict);
        Assert.Equal("revisionConflict", problem.Code);
        Assert.Equal(1, problem.ExpectedRevision);
        Assert.Equal(2, problem.ActualRevision);
        using var getResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        AssertSavedScenarioEquivalent(replaced, await ReadSavedScenarioAsync(getResponse));
    }

    [Fact]
    public async Task Delete_rejects_stale_revision_then_permanently_removes_the_aggregate()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Complete());
        var created = await ReadSavedScenarioAsync(createdResponse);

        using var staleResponse = await _client.DeleteAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}?expectedRevision=2");
        var conflict = await ReadSavedProblemAsync(staleResponse, HttpStatusCode.Conflict);
        Assert.Equal("revisionConflict", conflict.Code);
        Assert.Equal(2, conflict.ExpectedRevision);
        Assert.Equal(1, conflict.ActualRevision);

        using var existingResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        existingResponse.EnsureSuccessStatusCode();

        using var deleteResponse = await _client.DeleteAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}?expectedRevision=1");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var missingResponse = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");
        _ = await ReadSavedProblemAsync(missingResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replace_and_delete_return_not_found_for_missing_vehicle()
    {
        var missingId = Guid.CreateVersion7();
        var replacement = new ReplaceSavedCostScenarioRequest
        {
            ExpectedRevision = 1,
            Scenario = SavedCostScenarioTestData.Incomplete(),
            ListingLinkMode = ListingLinkMode.Preserve,
        };

        using var replaceResponse = await _client.PutAsJsonAsync(
            $"/api/saved-cost-scenarios/{missingId}",
            replacement);
        var replaceProblem = await ReadSavedProblemAsync(
            replaceResponse,
            HttpStatusCode.NotFound);
        Assert.Equal("savedCostScenarioNotFound", replaceProblem.Code);

        using var deleteResponse = await _client.DeleteAsync(
            $"/api/saved-cost-scenarios/{missingId}?expectedRevision=1");
        var deleteProblem = await ReadSavedProblemAsync(
            deleteResponse,
            HttpStatusCode.NotFound);
        Assert.Equal("savedCostScenarioNotFound", deleteProblem.Code);
    }

    [Fact]
    public async Task Unsupported_stored_version_returns_a_typed_conflict()
    {
        using var createdResponse = await CreateAsync(
            "ABC123",
            SavedCostScenarioTestData.Incomplete());
        var created = await ReadSavedScenarioAsync(createdResponse);
        await factory.ExecuteDatabaseCommandAsync(
            "UPDATE saved_cost_scenarios SET calculation_version = 99, result_schema_version = 98");

        using var response = await _client.GetAsync(
            $"/api/saved-cost-scenarios/{created.VehicleId}");

        var problem = await ReadSavedProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("unsupportedSavedScenarioVersion", problem.Code);
        Assert.Equal(99, problem.CalculationVersion);
        Assert.Equal(98, problem.ResultSchemaVersion);
    }

    [Fact]
    public async Task Semantic_validation_paths_are_prefixed_with_scenario()
    {
        var request = new CreateSavedCostScenarioRequest
        {
            RegistrationNumber = "ABC123",
            Scenario = SavedCostScenarioTestData.Incomplete() with
            {
                CalculationPeriodMonths = 0,
                PurchasePriceSek = -1m,
            },
        };

        using var response = await _client.PostAsJsonAsync("/api/saved-cost-scenarios", request);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Equal(
            ["scenario.calculationPeriodMonths", "scenario.purchasePriceSek"],
            problem.Errors.Keys);
    }

    [Theory]
    [InlineData("""
        { "registrationNumber": "ABC123" }
        """)]
    [InlineData("""
        {
          "registrationNumber": "ABC123",
          "scenario": {
            "calculationPeriodMonths": 12,
            "purchasePriceSek": 10000,
            "annualDistanceKilometres": 100,
            "energySources": [{
              "label": "Fuel",
              "unit": 0,
              "consumptionPer100Kilometres": 5,
              "pricePerUnitSek": 20,
              "distanceSharePercent": 100
            }],
            "vehicleTax": null,
            "insurance": null,
            "maintenanceAndRepairs": null,
            "otherRecurringCosts": [],
            "otherOneTimeCosts": []
          }
        }
        """)]
    [InlineData("""
        {
          "registrationNumber": "ABC123",
          "scenario": {
            "calculationPeriodMonths": 12,
            "purchasePriceSek": 10000,
            "annualDistanceKilometres": 0,
            "energySources": [],
            "vehicleTax": null,
            "insurance": null,
            "maintenanceAndRepairs": null,
            "otherRecurringCosts": [],
            "otherOneTimeCosts": []
          }
        }
        trailing
        """)]
    public async Task Invalid_json_contracts_return_validation_problem(string json)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "/api/saved-cost-scenarios", json);

        _ = await ReadValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("/api/saved-cost-scenarios/00000000-0000-0000-0000-000000000001", "{}")]
    [InlineData("/api/saved-cost-scenarios/00000000-0000-0000-0000-000000000001", "{\"expectedRevision\":0,\"scenario\":null}")]
    public async Task Invalid_replacement_contracts_return_validation_problem(string route, string json)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, route, json);

        _ = await ReadValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unsupported\"")]
    public async Task Replacement_rejects_numeric_and_unsupported_listing_link_modes(string mode)
    {
        var scenario = System.Text.Json.JsonSerializer.Serialize(
            SavedCostScenarioTestData.Incomplete(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var json = $$"""
            {
              "expectedRevision": 1,
              "scenario": {{scenario}},
              "listingLinkMode": {{mode}}
            }
            """;

        using var response = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/saved-cost-scenarios/{Guid.CreateVersion7()}",
            json);

        _ = await ReadValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?expectedRevision=0")]
    public async Task Delete_requires_a_positive_revision(string query)
    {
        using var response = await _client.DeleteAsync(
            $"/api/saved-cost-scenarios/{Guid.CreateVersion7()}{query}");

        _ = await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Openapi_exposes_all_saved_scenario_operations()
    {
        using var response = await _client.GetAsync("/api/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var paths = document.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/saved-cost-scenarios").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/saved-cost-scenarios").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/saved-cost-scenarios/{vehicleId}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/saved-cost-scenarios/{vehicleId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/saved-cost-scenarios/{vehicleId}").TryGetProperty("delete", out _));
        Assert.True(paths
            .GetProperty("/api/saved-cost-scenarios/by-registration/{registrationNumber}")
            .TryGetProperty("get", out _));

        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            ["expectedRevision", "scenario", "listingLinkMode"],
            schemas.GetProperty("ReplaceSavedCostScenarioRequest")
                .GetProperty("required")
                .EnumerateArray()
                .Select(property => property.GetString()!)
                .ToArray());
        Assert.Equal(
            ["preserve", "current"],
            schemas.GetProperty("ListingLinkMode")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());
    }

    private Task<HttpResponseMessage> CreateAsync(
        string registrationNumber,
        CarExpenseCalculator.Api.Contracts.ManualCalculations.ManualCalculationRequest scenario)
    {
        return _client.PostAsJsonAsync(
            "/api/saved-cost-scenarios",
            new CreateSavedCostScenarioRequest
            {
                RegistrationNumber = registrationNumber,
                Scenario = scenario,
            });
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string route,
        string json)
    {
        using var request = new HttpRequestMessage(method, route)
        {
            Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        return await _client.SendAsync(request);
    }

    private static async Task<SavedCostScenarioResponse> ReadSavedScenarioAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<SavedCostScenarioResponse>();
        Assert.NotNull(saved);
        return saved;
    }

    private static async Task<SavedCostScenarioProblemDetails> ReadSavedProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<SavedCostScenarioProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.NotEmpty(problem.Code);
        return problem;
    }

    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEmpty(problem.Errors);
        return problem;
    }

    private static void AssertSavedScenarioEquivalent(
        SavedCostScenarioResponse expected,
        SavedCostScenarioResponse actual)
    {
        Assert.Equal(expected.VehicleId, actual.VehicleId);
        Assert.Equal(expected.RegistrationNumber, actual.RegistrationNumber);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.CalculationVersion, actual.CalculationVersion);
        Assert.Equal(expected.ResultSchemaVersion, actual.ResultSchemaVersion);
        Assert.Equal(expected.SourceListingVersion, actual.SourceListingVersion);
        Assert.Equal(expected.CurrentListingVersion, actual.CurrentListingVersion);
        Assert.Equal(expected.IsListingOutdated, actual.IsListingOutdated);
        Assert.Equal(expected.HasSavedListing, actual.HasSavedListing);
        AssertTimestampEquivalent(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertTimestampEquivalent(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        AssertTimestampEquivalent(expected.CalculatedAtUtc, actual.CalculatedAtUtc);
        Assert.Equivalent(expected.Scenario, actual.Scenario, strict: true);
        Assert.Equivalent(expected.Result, actual.Result, strict: true);
    }

    private static void AssertTimestampEquivalent(
        DateTimeOffset expected,
        DateTimeOffset actual)
    {
        Assert.InRange(
            (expected - actual).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1));
    }
}
