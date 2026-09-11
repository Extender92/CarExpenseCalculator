using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;
using CarExpenseCalculator.Infrastructure.ListingExtraction;

namespace CarExpenseCalculator.Infrastructure.UnitTests;

public sealed class CodexListingExtractionServiceTests
{
    private static readonly ListingUrl ListingUrlValue = ListingUrl.Parse("https://example.com/item/1?ci=2");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Success_maps_every_value_to_core_provenance_and_classification()
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna",
            ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema,
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
            ["https://example.com/item/1"],
            new ExtractedListingDraft
            {
                RegistrationNumber = "abc 123",
                Make = " Volvo ",
                Model = "V70",
                Variant = "D4",
                ModelYear = 2015,
                Vin = " yv1test ",
                PriceSek = 100_000m,
                OdometerKilometres = 200_000m,
                SellerType = "dealer",
                Locality = " Göteborg ",
                County = " Västra Götalands län ",
                PublishedDate = "2026-09-01",
                UpdatedDate = "2026-09-02",
                ImageCount = 10,
                FuelTypes = ["diesel"],
                Transmission = "automatic",
                Drivetrain = "frontWheelDrive",
                BodyType = "wagon",
                Colour = "Blå",
                Horsepower = 181,
                EngineDisplacementCubicCentimetres = 1969m,
                EnergyConsumptions = [new(" Diesel ", "litre", 5.2m)],
                AnnualVehicleTaxSek = 2_400m,
                OwnerCount = 3,
                FirstRegistrationDate = "2015-02-03",
                LastInspectionDate = "2026-02-03",
                NextInspectionDate = "2027-04-30",
                TowBar = true,
                Equipment = ["Dragkrok"],
                SellerClaims = ["Servad enligt plan"],
                ConditionNotes = ["Normalt bruksslitage"],
            });
        var handler = StubHandler.Json(HttpStatusCode.OK, WithContent(response));
        var service = CreateService(handler);

        var outcome = await service.ExtractAsync(ListingUrlValue);

        var success = Assert.IsType<ListingExtractionSuccess>(outcome);
        var listing = success.ProcessingResult.Listing;
        Assert.Equal(ListingAnalysisStatus.Complete, success.ProcessingResult.Status);
        Assert.Equal("ABC123", listing.RegistrationNumber!.Value.Value);
        Assert.Equal("Volvo", listing.Make!.Value);
        Assert.Equal("V70", listing.Model!.Value);
        Assert.Equal("D4", listing.Variant!.Value);
        Assert.Equal(2015, listing.ModelYear!.Value);
        Assert.Equal("YV1TEST", listing.Vin!.Value);
        Assert.Null(listing.VehicleLabel);
        Assert.Equal(100_000m, listing.PriceSek!.Value);
        Assert.Equal(200_000m, listing.OdometerKilometres!.Value);
        Assert.Equal(SellerType.Dealer, listing.SellerType!.Value);
        Assert.Equal("Göteborg", listing.Locality!.Value);
        Assert.Equal("Västra Götalands län", listing.County!.Value);
        Assert.Equal(new DateOnly(2026, 9, 1), listing.PublishedDate!.Value);
        Assert.Equal(new DateOnly(2026, 9, 2), listing.UpdatedDate!.Value);
        Assert.Equal(10, listing.ImageCount!.Value);
        Assert.Equal([FuelType.Diesel], listing.FuelTypes!.Values);
        Assert.Equal(Transmission.Automatic, listing.Transmission!.Value);
        Assert.Equal(Drivetrain.FrontWheelDrive, listing.Drivetrain!.Value);
        Assert.Equal(BodyType.Wagon, listing.BodyType!.Value);
        Assert.Equal("Blå", listing.Colour!.Value);
        Assert.Equal(181, listing.Horsepower!.Value);
        Assert.Equal(1969m, listing.EngineDisplacementCubicCentimetres!.Value);
        Assert.Equal("Diesel", listing.EnergyConsumptions!.Values[0].Label);
        Assert.Equal(EnergyUnit.Litre, listing.EnergyConsumptions.Values[0].Unit);
        Assert.Equal(5.2m, listing.EnergyConsumptions.Values[0].ConsumptionPer100Kilometres);
        Assert.Equal(2_400m, listing.AnnualVehicleTaxSek!.Value);
        Assert.Equal(3, listing.OwnerCount!.Value);
        Assert.Equal(new DateOnly(2015, 2, 3), listing.FirstRegistrationDate!.Value);
        Assert.Equal(new DateOnly(2026, 2, 3), listing.LastInspectionDate!.Value);
        Assert.Equal(new DateOnly(2027, 4, 30), listing.NextInspectionDate!.Value);
        Assert.True(listing.TowBar!.Value);
        Assert.Equal(["Dragkrok"], listing.Equipment!.Values);
        Assert.Equal(["Servad enligt plan"], listing.SellerClaims!.Values);
        Assert.Equal(["Normalt bruksslitage"], listing.ConditionNotes!.Values);
        Assert.Equal(FieldOrigin.Listing, listing.Make.Provenance.Origin);
        Assert.Equal(ExtractionMethod.Ai, listing.Make.Provenance.ExtractionMethod);
        Assert.Equal(VerificationStatus.Unverified, listing.Make.Provenance.Verification);
        Assert.Equal(ListingUrlValue.Value, listing.Make.Provenance.SourceUrl.Value);
        Assert.Equal(ListingUrlValue.Value, handler.Request!.NormalizedUrl);
        Assert.Equal(ListingExtractionContractVersions.Prompt, handler.Request.PromptVersion);
        Assert.Equal(ListingExtractionContractVersions.Schema, handler.Request.SchemaVersion);
    }

    [Fact]
    public async Task Mismatched_application_retrieval_is_rejected_in_the_new_pipeline()
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna",
            ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema,
            DateTimeOffset.UtcNow,
            ["https://example.com/another-item"],
            new ExtractedListingDraft { Make = "Volvo", Equipment = [] });
        var service = CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)));

        var outcome = await service.ExtractAsync(ListingUrlValue);

        Assert.Equal(ListingExtractionFailureCode.InvalidProviderResponse,
            Assert.IsType<ListingExtractionFailure>(outcome).Code);
    }

    [Fact]
    public async Task Malformed_detail_collection_is_an_invalid_response_not_an_unhandled_error()
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna", ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema, DateTimeOffset.UtcNow, [],
            new ExtractedListingDraft
            {
                Details = new ExtractedListingDetails { Specifications = [null!] }
            });
        var service = CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)));
        var result = Assert.IsType<ListingExtractionFailure>(await service.ExtractAsync(ListingUrlValue));
        Assert.Equal(ListingExtractionFailureCode.InvalidProviderResponse, result.Code);
    }

    [Theory]
    [InlineData("http://example.com/item/1", "https://example.com/item/1", true)]
    [InlineData("https://example.com/item/1", "http://example.com/item/1", false)]
    public async Task Source_matching_preserves_the_directional_https_rule(
        string submitted,
        string opened,
        bool expectedMatch)
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna",
            ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema,
            DateTimeOffset.UtcNow,
            [opened],
            new ExtractedListingDraft { Make = "Volvo" });

        var outcome = await CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)))
            .ExtractAsync(ListingUrl.Parse(submitted));

        if (!expectedMatch)
        {
            Assert.Equal(ListingExtractionFailureCode.InvalidProviderResponse, Assert.IsType<ListingExtractionFailure>(outcome).Code);
            return;
        }
        var success = Assert.IsType<ListingExtractionSuccess>(outcome);
        Assert.Equal(
            ListingAnalysisStatus.Partial,
            success.ProcessingResult.Status);
        Assert.Equal(expectedMatch, success.ProcessingResult.Sources.Single().MatchesSubmittedUrl);
    }

    [Fact]
    public async Task Known_empty_collection_remains_distinct_from_unknown_collection()
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna",
            ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema,
            DateTimeOffset.UtcNow,
            ["https://example.com/item/1"],
            new ExtractedListingDraft { Equipment = [], SellerClaims = null });
        var service = CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)));

        var outcome = await service.ExtractAsync(ListingUrlValue);

        var listing = Assert.IsType<ListingExtractionSuccess>(outcome).ProcessingResult.Listing;
        Assert.Empty(listing.Equipment!.Values);
        Assert.Null(listing.SellerClaims);
        Assert.DoesNotContain(ListingFieldCode.Equipment, Assert.IsType<ListingExtractionSuccess>(outcome).ProcessingResult.MissingFields);
        Assert.Contains(ListingFieldCode.SellerClaims, Assert.IsType<ListingExtractionSuccess>(outcome).ProcessingResult.MissingFields);
    }

    [Theory]
    [InlineData(400, ListingExtractorProblemCodes.SourceUnsupported, ListingExtractionFailureCode.SourceUnsupported)]
    [InlineData(429, ListingExtractorProblemCodes.SourceRateLimited, ListingExtractionFailureCode.SourceRateLimited)]
    [InlineData(503, ListingExtractorProblemCodes.SourceBlocked, ListingExtractionFailureCode.SourceBlocked)]
    [InlineData(503, ListingExtractorProblemCodes.SourceUnavailable, ListingExtractionFailureCode.SourceUnavailable)]
    [InlineData(503, ListingExtractorProblemCodes.SourceInvalidContent, ListingExtractionFailureCode.SourceInvalidContent)]
    [InlineData(503, ListingExtractorProblemCodes.NotConfigured, ListingExtractionFailureCode.NotConfigured)]
    [InlineData(429, ListingExtractorProblemCodes.RateLimited, ListingExtractionFailureCode.RateLimited)]
    [InlineData(503, ListingExtractorProblemCodes.TimedOut, ListingExtractionFailureCode.TimedOut)]
    [InlineData(503, ListingExtractorProblemCodes.ProviderUnavailable, ListingExtractionFailureCode.ProviderUnavailable)]
    [InlineData(503, ListingExtractorProblemCodes.InvalidOutput, ListingExtractionFailureCode.InvalidProviderResponse)]
    public async Task Typed_sidecar_failures_map_to_provider_neutral_outcomes(
        int statusCode,
        string code,
        ListingExtractionFailureCode expected)
    {
        var handler = StubHandler.Json(
            (HttpStatusCode)statusCode,
            new { type = "about:blank", title = "safe", status = statusCode, code });

        var outcome = await CreateService(handler).ExtractAsync(ListingUrlValue);

        Assert.Equal(expected, Assert.IsType<ListingExtractionFailure>(outcome).Code);
    }

    [Fact]
    public async Task Invalid_or_duplicate_source_contract_is_rejected()
    {
        var response = new ListingExtractionResponse(
            "gpt-5.6-luna",
            ListingExtractionContractVersions.Prompt,
            ListingExtractionContractVersions.Schema,
            DateTimeOffset.UtcNow,
            ["https://example.com/item/1", "https://example.com/item/1"],
            new ExtractedListingDraft());

        var outcome = await CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)))
            .ExtractAsync(ListingUrlValue);

        Assert.Equal(
            ListingExtractionFailureCode.InvalidProviderResponse,
            Assert.IsType<ListingExtractionFailure>(outcome).Code);
    }

    [Fact]
    public async Task Mismatched_requested_model_is_rejected_as_an_invalid_provider_response()
    {
        var response = new ListingExtractionResponse(
            "different-model",
            1,
            1,
            DateTimeOffset.UtcNow,
            ["https://example.com/item/1"],
            new ExtractedListingDraft());

        var outcome = await CreateService(StubHandler.Json(HttpStatusCode.OK, WithContent(response)))
            .ExtractAsync(ListingUrlValue);

        Assert.Equal(
            ListingExtractionFailureCode.InvalidProviderResponse,
            Assert.IsType<ListingExtractionFailure>(outcome).Code);
    }

    [Fact]
    public async Task Configuration_status_does_not_start_an_extraction()
    {
        var handler = StubHandler.Json(
            HttpStatusCode.OK,
            new ListingExtractorStatusResponse(
                true,
                "gpt-5.6-luna",
                "medium",
                "0.153.0",
                ListingExtractionContractVersions.Prompt,
                ListingExtractionContractVersions.Schema));

        var status = await CreateService(handler).GetStatusAsync();

        Assert.True(status.Configured);
        Assert.Equal("gpt-5.6-luna", status.RequestedModel);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Network_failure_is_provider_unavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var outcome = await service.ExtractAsync(ListingUrlValue);

        Assert.Equal(
            ListingExtractionFailureCode.ProviderUnavailable,
            Assert.IsType<ListingExtractionFailure>(outcome).Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_remains_an_operation_cancellation()
    {
        var service = CreateService(new CancellingHandler());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExtractAsync(ListingUrlValue, cancellation.Token));
    }

    [Fact]
    public async Task Original_content_replaces_model_rewrites_without_confirming_any_fact()
    {
        var original = new RetrievedListingContent(ListingUrlValue.Value, "Original rubrik", "Original underrubrik", "1",
            "35 000 kr", "Första stycket.\n\nAndra stycket.", [new("Vikt", "1 370 kg")], ["ABS"],
            [new("Skulder?", "Nej")], null, null);
        var response = new ListingExtractionResponse("gpt-5.6-luna", 4, 3, DateTimeOffset.UtcNow,
            [ListingUrlValue.Value], new()
            {
                Make = "Audi", PriceSek = 35_000, Equipment = ["ABS", "Vinterdäck från beskrivningen"],
                Details = new() { Title = "Påhittad rubrik", Description = "Sammanfattning", SellerAnswers = [new("Skulder?", "Ja")] },
            }, original);
        var result = Assert.IsType<ListingExtractionSuccess>(await CreateService(StubHandler.Json(HttpStatusCode.OK, response)).ExtractAsync(ListingUrlValue));
        var draft = result.ProcessingResult.Listing;
        Assert.Equal(original.Title, draft.Details!.Title!.Value);
        Assert.Equal(original.Description, draft.Details.Description!.Value);
        Assert.Equal(original.Equipment, draft.Equipment!.Values);
        Assert.Equal("Nej", Assert.Single(draft.Details.SellerAnswers!).Value.Answer);
        Assert.Equal(ExtractionMethod.Html, draft.Details.Title.Provenance.ExtractionMethod);
        Assert.Equal(ExtractionMethod.Html, draft.Equipment.Provenance.ExtractionMethod);
        Assert.Equal(ExtractionMethod.Ai, draft.PriceSek!.Provenance.ExtractionMethod);
        Assert.Equal(VerificationStatus.Unverified, draft.Details.Title.Provenance.Verification);
        Assert.Equal(VerificationStatus.Unverified, draft.PriceSek.Provenance.Verification);
    }

    [Fact]
    public async Task Missing_application_content_is_not_fabricated_from_model_output()
    {
        var response = new ListingExtractionResponse("gpt-5.6-luna", 4, 3, DateTimeOffset.UtcNow,
            [ListingUrlValue.Value], new() { Make = "Audi", Equipment = ["ABS"] });
        var result = Assert.IsType<ListingExtractionFailure>(await CreateService(StubHandler.Json(HttpStatusCode.OK, response)).ExtractAsync(ListingUrlValue));
        Assert.Equal(ListingExtractionFailureCode.InvalidProviderResponse, result.Code);
    }

    [Theory]
    [InlineData("dealer", "private", SellerType.Dealer)]
    [InlineData("private", "dealer", SellerType.Private)]
    [InlineData("dealer", null, SellerType.Dealer)]
    [InlineData(null, "dealer", null)]
    public async Task Retrieved_seller_type_is_authoritative_and_remains_unverified(string? captured, string? model, SellerType? expected)
    {
        var content = new RetrievedListingContent(ListingUrlValue.Value, "Testbil", null, "1",
            null, null, null, null, null, null, null, captured);
        var response = new ListingExtractionResponse("gpt-5.6-luna", 4, 3, DateTimeOffset.UtcNow,
            [ListingUrlValue.Value], new() { Make = "Test", SellerType = model }, content);
        var result = Assert.IsType<ListingExtractionSuccess>(await CreateService(StubHandler.Json(HttpStatusCode.OK, response)).ExtractAsync(ListingUrlValue));
        var seller = result.ProcessingResult.Listing.SellerType;
        Assert.Equal(expected, seller?.Value);
        if (expected is not null)
        {
            Assert.Equal(new FieldProvenance(FieldOrigin.Listing, ExtractionMethod.Html, VerificationStatus.Unverified, ListingUrlValue), seller!.Provenance);
        }
    }

    [Fact]
    public async Task Invalid_captured_seller_type_is_rejected_instead_of_silently_dropped()
    {
        var content = new RetrievedListingContent(ListingUrlValue.Value, "Testbil", null, "1",
            null, null, null, null, null, null, null, "registryVerified");
        var response = new ListingExtractionResponse("gpt-5.6-luna", 4, 3, DateTimeOffset.UtcNow,
            [ListingUrlValue.Value], new() { Make = "Test" }, content);
        var result = Assert.IsType<ListingExtractionFailure>(await CreateService(StubHandler.Json(HttpStatusCode.OK, response)).ExtractAsync(ListingUrlValue));
        Assert.Equal(ListingExtractionFailureCode.InvalidProviderResponse, result.Code);
    }

    [Theory]
    [InlineData("Testgatan 1, 60361 Norrköping", null, "60361")]
    [InlineData("149 91 Nynäshamn", "99999", "14991")]
    [InlineData("149\u00a091 Nynäshamn", null, "14991")]
    [InlineData("Testgatan 12345", null, null)]
    [InlineData("Norrköping", null, null)]
    [InlineData("12345 Ort, 67890 Annan ort", null, null)]
    public async Task Explicit_postcode_survives_model_omission_without_inferring_other_location_numbers(string location, string? model, string? expected)
    {
        var content = new RetrievedListingContent(ListingUrlValue.Value, "Testbil", null, "1",
            null, null, null, null, null, location, null);
        var response = new ListingExtractionResponse("gpt-5.6-luna", 4, 3, DateTimeOffset.UtcNow,
            [ListingUrlValue.Value], new() { Make = "Test", Details = new() { PostalCode = model } }, content);
        var result = Assert.IsType<ListingExtractionSuccess>(await CreateService(StubHandler.Json(HttpStatusCode.OK, response)).ExtractAsync(ListingUrlValue));
        var postcode = result.ProcessingResult.Listing.Details!.PostalCode;
        Assert.Equal(expected, postcode?.Value);
        if (expected is not null)
            Assert.Equal(new FieldProvenance(FieldOrigin.Listing, ExtractionMethod.Html, VerificationStatus.Unverified, ListingUrlValue), postcode!.Provenance);
        Assert.Null(result.ProcessingResult.Listing.Locality);
        Assert.Null(result.ProcessingResult.Listing.County);
    }

    private static ListingExtractionResponse WithContent(ListingExtractionResponse response) => response with
    {
        RetrievedContent = new(response.Sources.FirstOrDefault() ?? ListingUrlValue.Value, "Testbil", null, "1",
            null, null, null, response.Draft.Equipment, null, null, null, response.Draft.SellerType),
    };

    private static CodexListingExtractionService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://codex-extractor:8080") };
        return new CodexListingExtractionService(client, new ListingDraftProcessor());
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        public ListingExtractionRequest? Request { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public int CallCount { get; private set; }

        public static StubHandler Json(HttpStatusCode statusCode, object body) =>
            new(async request =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = JsonContent.Create(body, options: JsonOptions),
                };
                await Task.CompletedTask;
                return response;
            });

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            if (request.Content is not null)
            {
                Request = await request.Content.ReadFromJsonAsync<ListingExtractionRequest>(
                    JsonOptions,
                    cancellationToken);
            }

            return await responseFactory(request);
        }
    }

    private sealed class CancellingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
}
