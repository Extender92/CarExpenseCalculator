using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.ListingAnalyses;

public sealed record ListingAnalysisRequest
{
    [Required(AllowEmptyStrings = true)]
    public required string Url { get; init; }
}

public sealed record ListingAnalysisResponse(
    string SubmittedUrl,
    string NormalizedUrl,
    ListingAnalysisStatus Status,
    DateTimeOffset AnalyzedAtUtc,
    string RequestedModel,
    int PromptVersion,
    int SchemaVersion,
    IReadOnlyList<ListingAnalysisSourceResponse> Sources,
    ListingDraftResponse Listing,
    IReadOnlyList<ListingFieldCode> MissingFields);

public sealed record ListingAnalysisSourceResponse(
    string Url,
    bool MatchesSubmittedUrl);

public sealed record ListingDraftResponse(
    SourcedValueResponse<string>? RegistrationNumber,
    SourcedValueResponse<string>? Make,
    SourcedValueResponse<string>? Model,
    SourcedValueResponse<string>? Variant,
    SourcedValueResponse<int>? ModelYear,
    SourcedValueResponse<string>? Vin,
    SourcedValueResponse<string>? VehicleLabel,
    SourcedValueResponse<decimal>? PriceSek,
    SourcedValueResponse<decimal>? OdometerKilometres,
    SourcedValueResponse<SellerType>? SellerType,
    SourcedValueResponse<string>? Location,
    SourcedValueResponse<DateOnly>? PublishedDate,
    SourcedValueResponse<DateOnly>? UpdatedDate,
    SourcedValueResponse<int>? ImageCount,
    SourcedCollectionResponse<FuelType>? FuelTypes,
    SourcedValueResponse<Transmission>? Transmission,
    SourcedValueResponse<Drivetrain>? Drivetrain,
    SourcedValueResponse<BodyType>? BodyType,
    SourcedValueResponse<string>? Colour,
    SourcedValueResponse<int>? Horsepower,
    SourcedValueResponse<decimal>? EngineDisplacementCubicCentimetres,
    SourcedCollectionResponse<EnergyConsumptionResponse>? EnergyConsumptions,
    SourcedValueResponse<decimal>? AnnualVehicleTaxSek,
    SourcedValueResponse<int>? OwnerCount,
    SourcedValueResponse<DateOnly>? FirstRegistrationDate,
    SourcedValueResponse<DateOnly>? LastInspectionDate,
    SourcedValueResponse<DateOnly>? NextInspectionDate,
    SourcedValueResponse<bool>? TowBar,
    SourcedCollectionResponse<string>? Equipment,
    SourcedCollectionResponse<string>? SellerClaims,
    SourcedCollectionResponse<string>? ConditionNotes);

public sealed record SourcedValueResponse<T>(
    T Value,
    FieldProvenanceResponse Provenance)
    where T : notnull;

public sealed record SourcedCollectionResponse<T>(
    IReadOnlyList<T> Values,
    FieldProvenanceResponse Provenance)
    where T : notnull;

public sealed record FieldProvenanceResponse(
    FieldOrigin Origin,
    ExtractionMethod ExtractionMethod,
    VerificationStatus Verification,
    string SourceUrl);

public sealed record EnergyConsumptionResponse(
    string Label,
    EnergyUnit Unit,
    decimal ConsumptionPer100Kilometres);
