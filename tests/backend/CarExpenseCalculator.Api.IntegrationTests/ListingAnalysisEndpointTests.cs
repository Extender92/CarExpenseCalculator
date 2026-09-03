using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using CarExpenseCalculator.Api.Controllers;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using CoreListingAnalysisStatus = CarExpenseCalculator.Core.Listings.ListingAnalysisStatus;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ListingAnalysisEndpointTests : IClassFixture<ListingAnalysisApiFactory>
{
    private readonly FakeListingExtractionService _extractionService;
    private readonly HttpClient _client;

    public ListingAnalysisEndpointTests(ListingAnalysisApiFactory factory)
    {
        _extractionService = factory.ExtractionService;
        _extractionService.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Complete_analysis_returns_normalized_source_aware_result_without_postgres()
    {
        _extractionService.ExtractionHandler = (url, _) =>
            Task.FromResult<ListingExtractionOutcome>(ListingAnalysisTestData.Complete(url));
        const string submittedUrl = "HTTP://EXAMPLE.COM:80/item/1?CI=2#details";

        using var response = await _client.PostAsJsonAsync(
            "/api/listing-analyses",
            new { url = $"  {submittedUrl}  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<ListingAnalysisResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(submittedUrl, payload.SubmittedUrl);
        Assert.Equal("http://example.com/item/1?CI=2", payload.NormalizedUrl);
        Assert.Equal(CarExpenseCalculator.Api.Contracts.ListingAnalyses.ListingAnalysisStatus.Complete,
            payload.Status);
        Assert.Equal(ListingAnalysisTestData.AnalyzedAtUtc, payload.AnalyzedAtUtc);
        Assert.Equal("gpt-5.6-luna", payload.RequestedModel);
        Assert.Equal(1, payload.PromptVersion);
        Assert.Equal(1, payload.SchemaVersion);
        Assert.Equal([false, true], payload.Sources.Select(source => source.MatchesSubmittedUrl));
        Assert.Equal("ABC12D", payload.Listing.RegistrationNumber!.Value);
        Assert.Equal(89_900.50m, payload.Listing.PriceSek!.Value);
        Assert.Equal(198_765.432m, payload.Listing.OdometerKilometres!.Value);
        Assert.False(payload.Listing.TowBar!.Value);
        Assert.Empty(payload.MissingFields);
        using var document = JsonDocument.Parse(json);
        var serializedListing = document.RootElement.GetProperty("listing");
        Assert.Equal("complete", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("dealer", serializedListing.GetProperty("sellerType").GetProperty("value").GetString());
        Assert.Equal("diesel", serializedListing.GetProperty("fuelTypes").GetProperty("values")[1].GetString());
        Assert.Equal("litre", serializedListing.GetProperty("energyConsumptions").GetProperty("values")[0]
            .GetProperty("unit").GetString());
        var provenance = serializedListing.GetProperty("make").GetProperty("provenance");
        Assert.Equal("listing", provenance.GetProperty("origin").GetString());
        Assert.Equal("ai", provenance.GetProperty("extractionMethod").GetString());
        Assert.Equal("unverified", provenance.GetProperty("verification").GetString());
        Assert.Equal(1, _extractionService.ExtractionCallCount);
    }

    [Theory]
    [InlineData(CoreListingAnalysisStatus.Complete, "complete")]
    [InlineData(CoreListingAnalysisStatus.Partial, "partial")]
    [InlineData(CoreListingAnalysisStatus.Unavailable, "unavailable")]
    public async Task Every_successful_analysis_status_returns_http_200(
        CoreListingAnalysisStatus status,
        string expectedStatus)
    {
        _extractionService.ExtractionHandler = (url, _) =>
            Task.FromResult<ListingExtractionOutcome>(ListingAnalysisTestData.Success(url, status));

        using var response = await _client.PostAsJsonAsync(
            "/api/listing-analyses",
            new { url = "https://example.com/item/1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(expectedStatus, payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task Response_includes_every_nullable_field_and_preserves_zero_false_and_known_empty()
    {
        _extractionService.ExtractionHandler = (url, _) =>
        {
            var provenance = ListingAnalysisTestData.ListingProvenance(url);
            var listing = new ListingDraft
            {
                PriceSek = ListingAnalysisTestData.Value(0m, provenance),
                OdometerKilometres = ListingAnalysisTestData.Value(0m, provenance),
                ImageCount = ListingAnalysisTestData.Value(0, provenance),
                TowBar = ListingAnalysisTestData.Value(false, provenance),
                Equipment = ListingAnalysisTestData.Collection(Array.Empty<string>(), provenance),
            };
            return Task.FromResult<ListingExtractionOutcome>(
                ListingAnalysisTestData.Success(url, CoreListingAnalysisStatus.Partial, listing));
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/listing-analyses",
            new { url = "https://example.com/item/1" });

        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var listing = payload.RootElement.GetProperty("listing");
        Assert.Equal(
            [
                "registrationNumber", "make", "model", "variant", "modelYear", "vin", "vehicleLabel",
                "priceSek", "odometerKilometres", "sellerType", "location", "publishedDate", "updatedDate",
                "imageCount", "fuelTypes", "transmission", "drivetrain", "bodyType", "colour", "horsepower",
                "engineDisplacementCubicCentimetres", "energyConsumptions", "annualVehicleTaxSek", "ownerCount",
                "firstRegistrationDate", "lastInspectionDate", "nextInspectionDate", "towBar", "equipment",
                "sellerClaims", "conditionNotes",
            ],
            listing.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.Null, listing.GetProperty("make").ValueKind);
        Assert.Equal(0m, listing.GetProperty("priceSek").GetProperty("value").GetDecimal());
        Assert.Equal(0m, listing.GetProperty("odometerKilometres").GetProperty("value").GetDecimal());
        Assert.Equal(0, listing.GetProperty("imageCount").GetProperty("value").GetInt32());
        Assert.False(listing.GetProperty("towBar").GetProperty("value").GetBoolean());
        Assert.Empty(listing.GetProperty("equipment").GetProperty("values").EnumerateArray());
    }

    public static TheoryData<string, string> InvalidUrls => new()
    {
        { " ", "URL is required." },
        { "/relative", "URL must be an absolute, well-formed HTTP or HTTPS URL." },
        { "ftp://example.com/item/1", "URL scheme must be HTTP or HTTPS." },
        { "https://user:secret@example.com/item/1", "URL credentials are not allowed." },
        { "https://localhost/item/1", "Local host names are not allowed." },
        { "https://127.0.0.1/item/1", "Private, reserved, and other non-public IP addresses are not allowed." },
        { "https://example.com:0/item/1", "URL port must be between 1 and 65535." },
        { $"https://example.com/{new string('a', 2_050)}", "URL cannot exceed 2048 characters." },
    };

    public static TheoryData<ListingUrlValidationErrorCode, string> UrlValidationMessages => new()
    {
        { ListingUrlValidationErrorCode.Required, "URL is required." },
        { ListingUrlValidationErrorCode.TooLong, "URL cannot exceed 2048 characters." },
        { ListingUrlValidationErrorCode.Malformed, "URL must be an absolute, well-formed HTTP or HTTPS URL." },
        { ListingUrlValidationErrorCode.UnsupportedScheme, "URL scheme must be HTTP or HTTPS." },
        { ListingUrlValidationErrorCode.CredentialsNotAllowed, "URL credentials are not allowed." },
        { ListingUrlValidationErrorCode.MissingHost, "URL must contain a host." },
        { ListingUrlValidationErrorCode.LocalHostNotAllowed, "Local host names are not allowed." },
        { ListingUrlValidationErrorCode.NonPublicIpAddress, "Private, reserved, and other non-public IP addresses are not allowed." },
        { ListingUrlValidationErrorCode.InvalidPort, "URL port must be between 1 and 65535." },
    };

    [Theory]
    [MemberData(nameof(UrlValidationMessages))]
    public void Every_url_validation_category_has_a_stable_safe_message(
        ListingUrlValidationErrorCode code,
        string expectedMessage)
    {
        Assert.Equal(expectedMessage, ListingAnalysesController.GetUrlValidationMessage(code));
    }

    [Theory]
    [MemberData(nameof(InvalidUrls))]
    public async Task Invalid_url_returns_safe_validation_problem(string url, string expectedMessage)
    {
        using var response = await _client.PostAsJsonAsync("/api/listing-analyses", new { url });

        var problem = await ReadValidationProblemAsync(response);
        Assert.Equal([expectedMessage], problem.Errors["url"]);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"url\":null}")]
    [InlineData("{\"url\":123}")]
    [InlineData("{\"url\":")]
    public async Task Missing_null_or_malformed_url_returns_standard_validation_problem(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

        using var response = await _client.PostAsync("/api/listing-analyses", content);

        _ = await ReadValidationProblemAsync(response);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Theory]
    [InlineData(ListingExtractionFailureCode.RateLimited, HttpStatusCode.TooManyRequests,
        "listingAnalysisRateLimited", "Listing analysis is temporarily rate limited.")]
    [InlineData(ListingExtractionFailureCode.NotConfigured, HttpStatusCode.ServiceUnavailable,
        "listingAnalysisNotConfigured", "Listing analysis is not configured.")]
    [InlineData(ListingExtractionFailureCode.TimedOut, HttpStatusCode.ServiceUnavailable,
        "listingAnalysisTimedOut", "Listing analysis timed out.")]
    [InlineData(ListingExtractionFailureCode.ProviderUnavailable, HttpStatusCode.ServiceUnavailable,
        "listingAnalysisProviderUnavailable", "Listing analysis provider is unavailable.")]
    [InlineData(ListingExtractionFailureCode.InvalidProviderResponse, HttpStatusCode.ServiceUnavailable,
        "listingAnalysisInvalidProviderResponse", "Listing analysis returned an invalid provider response.")]
    public async Task Provider_failure_returns_typed_safe_problem_without_retry(
        ListingExtractionFailureCode failure,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedTitle)
    {
        _extractionService.ExtractionHandler = (_, _) =>
            Task.FromResult<ListingExtractionOutcome>(new ListingExtractionFailure(failure));

        using var response = await _client.PostAsJsonAsync(
            "/api/listing-analyses",
            new { url = "https://example.com/private-listing?secret=marker" });

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Retry-After"));
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ListingAnalysisProblemDetails>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedCode, problem.Code);
        Assert.DoesNotContain("private-listing", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codex-extractor", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_and_not_retried()
    {
        _extractionService.ExtractionHandler = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.PostAsJsonAsync(
                "/api/listing-analyses",
                new { url = "https://example.com/item/1" },
                cancellation.Token));

        Assert.Equal(1, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task System_status_reports_configuration_independently_from_database_health()
    {
        _extractionService.StatusHandler = _ => Task.FromResult(
            new ListingExtractionConfigurationStatus(true, "gpt-5.6-luna", 1, 1));

        using var response = await _client.GetAsync("/api/system/status");

        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("degraded", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal("unavailable", payload.RootElement.GetProperty("database").GetString());
        Assert.True(payload.RootElement.GetProperty("integrations")
            .GetProperty("codexListingExtractionConfigured").GetBoolean());
        var features = payload.RootElement.GetProperty("features");
        Assert.False(features.GetProperty("ruleBasedSearch").GetBoolean());
        Assert.True(features.GetProperty("urlAnalysis").GetBoolean());
        Assert.True(features.GetProperty("manualCalculator").GetBoolean());
        Assert.False(features.GetProperty("aiReview").GetBoolean());
        Assert.Equal(1, _extractionService.StatusCallCount);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task System_status_reports_an_unconfigured_extractor_without_starting_extraction()
    {
        _extractionService.StatusHandler = _ => Task.FromResult(
            new ListingExtractionConfigurationStatus(false, null, null, null));

        using var response = await _client.GetAsync("/api/system/status");

        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(payload.RootElement.GetProperty("integrations")
            .GetProperty("codexListingExtractionConfigured").GetBoolean());
        Assert.Equal(1, _extractionService.StatusCallCount);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task System_status_bounds_a_hanging_configuration_check_without_starting_extraction()
    {
        _extractionService.StatusHandler = async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        };
        var stopwatch = Stopwatch.StartNew();

        using var response = await _client.GetAsync("/api/system/status");

        stopwatch.Stop();
        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(payload.RootElement.GetProperty("integrations")
            .GetProperty("codexListingExtractionConfigured").GetBoolean());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal(1, _extractionService.StatusCallCount);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task System_status_propagates_caller_cancellation()
    {
        _extractionService.StatusHandler = async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.GetAsync("/api/system/status", cancellation.Token));

        Assert.Equal(1, _extractionService.StatusCallCount);
        Assert.Equal(0, _extractionService.ExtractionCallCount);
    }

    [Fact]
    public async Task Openapi_exposes_required_nullable_listing_contract_and_closed_enums()
    {
        using var response = await _client.GetAsync("/api/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty("/api/listing-analyses").GetProperty("post");
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("429", out _));
        Assert.True(responses.TryGetProperty("503", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["url"], RequiredProperties(schemas.GetProperty("ListingAnalysisRequest")));
        Assert.Equal(
            [
                "submittedUrl", "normalizedUrl", "status", "analyzedAtUtc", "requestedModel",
                "promptVersion", "schemaVersion", "sources", "listing", "missingFields",
            ],
            RequiredProperties(schemas.GetProperty("ListingAnalysisResponse")));
        Assert.Equal(
            [
                "registrationNumber", "make", "model", "variant", "modelYear", "vin", "vehicleLabel",
                "priceSek", "odometerKilometres", "sellerType", "location", "publishedDate", "updatedDate",
                "imageCount", "fuelTypes", "transmission", "drivetrain", "bodyType", "colour", "horsepower",
                "engineDisplacementCubicCentimetres", "energyConsumptions", "annualVehicleTaxSek", "ownerCount",
                "firstRegistrationDate", "lastInspectionDate", "nextInspectionDate", "towBar", "equipment",
                "sellerClaims", "conditionNotes",
            ],
            RequiredProperties(schemas.GetProperty("ListingDraftResponse")));
        Assert.Equal(
            ["complete", "partial", "unavailable"],
            EnumValues(schemas.GetProperty("ListingAnalysisStatus")));
        Assert.Equal(30, EnumValues(schemas.GetProperty("ListingFieldCode")).Count);
        Assert.Equal(
            ["unverified", "userConfirmed", "registryVerified"],
            EnumValues(schemas.GetProperty("VerificationStatus")));
        Assert.Contains(schemas.GetProperty("ListingDraftResponse")
            .GetProperty("properties").GetProperty("make").GetProperty("oneOf")
            .EnumerateArray(), alternative =>
                alternative.TryGetProperty("type", out var type)
                && type.GetString() == "null");
        Assert.Equal(
            ["codexListingExtractionConfigured"],
            RequiredProperties(schemas.GetProperty("IntegrationStatusResponse")));
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

    private static IReadOnlyList<string> RequiredProperties(JsonElement schema)
    {
        return schema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumValues(JsonElement schema)
    {
        return schema.GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }
}
