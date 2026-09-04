using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.Infrastructure.ListingExtraction;

internal sealed class CodexListingExtractionService(
    HttpClient httpClient,
    ListingDraftProcessor draftProcessor) : IListingExtractionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly JsonSerializerOptions ProblemSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ListingExtractionOutcome> ExtractAsync(
        ListingUrl listingUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingUrl);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/internal/listing-extractions",
                new ListingExtractionRequest(
                    listingUrl.Value,
                    ListingExtractionContractVersions.Prompt,
                    ListingExtractionContractVersions.Schema),
                SerializerOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await MapFailureAsync(response, cancellationToken);
            }

            var extraction = await response.Content.ReadFromJsonAsync<ListingExtractionResponse>(
                SerializerOptions,
                cancellationToken);
            if (!TryValidateResponse(extraction, out var sources))
            {
                return InvalidResponse();
            }

            var provenance = new FieldProvenance(
                FieldOrigin.Listing,
                ExtractionMethod.Ai,
                VerificationStatus.Unverified,
                listingUrl);
            var draft = MapDraft(extraction!.Draft, provenance);
            ListingProcessingResult processingResult;
            try
            {
                processingResult = draftProcessor.ProcessExtraction(listingUrl, sources!, draft);
            }
            catch (ListingValidationException)
            {
                return InvalidResponse();
            }

            return new ListingExtractionSuccess(
                listingUrl,
                extraction.RequestedModel,
                extraction.PromptVersion,
                extraction.SchemaVersion,
                extraction.AnalyzedAtUtc,
                processingResult);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ListingExtractionFailure(ListingExtractionFailureCode.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new ListingExtractionFailure(ListingExtractionFailureCode.ProviderUnavailable);
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
    }

    public async Task<ListingExtractionConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("/internal/status", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UnconfiguredStatus();
            }

            var status = await response.Content.ReadFromJsonAsync<ListingExtractorStatusResponse>(
                SerializerOptions,
                cancellationToken);
            if (status is null
                || status.RequestedModel != ListingExtractionRuntime.RequestedModel
                || status.ReasoningEffort != ListingExtractionRuntime.ReasoningEffort
                || status.CodexCliVersion != ListingExtractionRuntime.CodexCliVersion
                || status.PromptVersion != ListingExtractionContractVersions.Prompt
                || status.SchemaVersion != ListingExtractionContractVersions.Schema)
            {
                return UnconfiguredStatus();
            }

            return new ListingExtractionConfigurationStatus(
                status.Configured,
                status.RequestedModel,
                status.PromptVersion,
                status.SchemaVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnconfiguredStatus();
        }
        catch (HttpRequestException)
        {
            return UnconfiguredStatus();
        }
        catch (JsonException)
        {
            return UnconfiguredStatus();
        }
    }

    private static bool TryValidateResponse(
        ListingExtractionResponse? response,
        out IReadOnlyList<ListingUrl>? sources)
    {
        sources = null;
        if (response is null
            || response.RequestedModel != ListingExtractionRuntime.RequestedModel
            || response.PromptVersion != ListingExtractionContractVersions.Prompt
            || response.SchemaVersion != ListingExtractionContractVersions.Schema
            || response.AnalyzedAtUtc.Offset != TimeSpan.Zero
            || response.Sources is null
            || response.Draft is null)
        {
            return false;
        }

        var parsedSources = new List<ListingUrl>(response.Sources.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sourceValue in response.Sources)
        {
            if (!ListingUrl.TryParse(sourceValue, out var source)
                || !source!.Value.Equals(sourceValue, StringComparison.Ordinal)
                || !seen.Add(source.Value))
            {
                return false;
            }

            parsedSources.Add(source);
        }

        sources = Array.AsReadOnly(parsedSources.ToArray());
        return true;
    }

    private static ListingDraft MapDraft(
        ExtractedListingDraft source,
        FieldProvenance provenance)
    {
        return new ListingDraft
        {
            RegistrationNumber = TryRegistrationNumber(source.RegistrationNumber, provenance),
            Make = Value(source.Make, provenance),
            Model = Value(source.Model, provenance),
            Variant = Value(source.Variant, provenance),
            ModelYear = Value(source.ModelYear, provenance),
            Vin = Value(source.Vin, provenance),
            VehicleLabel = null,
            PriceSek = Value(source.PriceSek, provenance),
            OdometerKilometres = Value(source.OdometerKilometres, provenance),
            SellerType = EnumValue<SellerType>(source.SellerType, provenance),
            Locality = Value(source.Locality, provenance),
            County = Value(source.County, provenance),
            PublishedDate = DateValue(source.PublishedDate, provenance),
            UpdatedDate = DateValue(source.UpdatedDate, provenance),
            ImageCount = Value(source.ImageCount, provenance),
            FuelTypes = EnumCollection<FuelType>(source.FuelTypes, provenance),
            Transmission = EnumValue<Transmission>(source.Transmission, provenance),
            Drivetrain = EnumValue<Drivetrain>(source.Drivetrain, provenance),
            BodyType = EnumValue<BodyType>(source.BodyType, provenance),
            Colour = Value(source.Colour, provenance),
            Horsepower = Value(source.Horsepower, provenance),
            EngineDisplacementCubicCentimetres = Value(
                source.EngineDisplacementCubicCentimetres,
                provenance),
            EnergyConsumptions = EnergyCollection(source.EnergyConsumptions, provenance),
            AnnualVehicleTaxSek = Value(source.AnnualVehicleTaxSek, provenance),
            OwnerCount = Value(source.OwnerCount, provenance),
            FirstRegistrationDate = DateValue(source.FirstRegistrationDate, provenance),
            LastInspectionDate = DateValue(source.LastInspectionDate, provenance),
            NextInspectionDate = DateValue(source.NextInspectionDate, provenance),
            TowBar = Value(source.TowBar, provenance),
            Equipment = Collection(source.Equipment, provenance),
            SellerClaims = Collection(source.SellerClaims, provenance),
            ConditionNotes = Collection(source.ConditionNotes, provenance),
        };
    }

    private static SourcedValue<RegistrationNumber>? TryRegistrationNumber(
        string? value,
        FieldProvenance provenance)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return new SourcedValue<RegistrationNumber>(RegistrationNumber.Parse(value), provenance);
        }
        catch (RegistrationNumberValidationException)
        {
            return null;
        }
    }

    private static SourcedValue<DateOnly>? DateValue(
        string? value,
        FieldProvenance provenance)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? new SourcedValue<DateOnly>(parsed, provenance)
            : null;
    }

    private static SourcedValue<T>? Value<T>(T? value, FieldProvenance provenance)
        where T : struct
    {
        return value.HasValue ? new SourcedValue<T>(value.Value, provenance) : null;
    }

    private static SourcedValue<string>? Value(string? value, FieldProvenance provenance)
    {
        return value is null ? null : new SourcedValue<string>(value, provenance);
    }

    private static SourcedValue<TEnum>? EnumValue<TEnum>(
        string? value,
        FieldProvenance provenance)
        where TEnum : struct, Enum
    {
        if (value is null)
        {
            return null;
        }

        var parsed = Enum.TryParse<TEnum>(value, ignoreCase: true, out var enumValue)
            ? enumValue
            : (TEnum)Enum.ToObject(typeof(TEnum), -1);
        return new SourcedValue<TEnum>(parsed, provenance);
    }

    private static SourcedCollection<TEnum>? EnumCollection<TEnum>(
        IReadOnlyList<string>? values,
        FieldProvenance provenance)
        where TEnum : struct, Enum
    {
        if (values is null)
        {
            return null;
        }

        return new SourcedCollection<TEnum>(
            values.Select(value => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
                ? parsed
                : (TEnum)Enum.ToObject(typeof(TEnum), -1)),
            provenance);
    }

    private static SourcedCollection<string>? Collection(
        IReadOnlyList<string>? values,
        FieldProvenance provenance)
    {
        return values is null ? null : new SourcedCollection<string>(values, provenance);
    }

    private static SourcedCollection<EnergyConsumption>? EnergyCollection(
        IReadOnlyList<ExtractedEnergyConsumption>? values,
        FieldProvenance provenance)
    {
        if (values is null)
        {
            return null;
        }

        return new SourcedCollection<EnergyConsumption>(
            values.Select(value => new EnergyConsumption(
                value.Label!,
                Enum.TryParse<EnergyUnit>(value.Unit, ignoreCase: true, out var unit)
                    ? unit
                    : (EnergyUnit)(-1),
                value.ConsumptionPer100Kilometres ?? -1m)),
            provenance);
    }

    private static async Task<ListingExtractionOutcome> MapFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ListingExtractorProblem? problem;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ListingExtractorProblem>(
                ProblemSerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }

        return (response.StatusCode, problem?.Code) switch
        {
            (HttpStatusCode.ServiceUnavailable, ListingExtractorProblemCodes.NotConfigured) =>
                new ListingExtractionFailure(ListingExtractionFailureCode.NotConfigured),
            (HttpStatusCode.TooManyRequests, ListingExtractorProblemCodes.RateLimited) =>
                new ListingExtractionFailure(ListingExtractionFailureCode.RateLimited),
            (HttpStatusCode.ServiceUnavailable, ListingExtractorProblemCodes.TimedOut) =>
                new ListingExtractionFailure(ListingExtractionFailureCode.TimedOut),
            (HttpStatusCode.ServiceUnavailable, ListingExtractorProblemCodes.ProviderUnavailable) =>
                new ListingExtractionFailure(ListingExtractionFailureCode.ProviderUnavailable),
            (HttpStatusCode.ServiceUnavailable, ListingExtractorProblemCodes.InvalidOutput) =>
                InvalidResponse(),
            _ => InvalidResponse(),
        };
    }

    private static ListingExtractionFailure InvalidResponse() =>
        new(ListingExtractionFailureCode.InvalidProviderResponse);

    private static ListingExtractionConfigurationStatus UnconfiguredStatus() =>
        new(false, null, null, null);
}
