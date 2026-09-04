using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.SavedListings;

public sealed record CreateSavedListingRequest
{
    [Required(AllowEmptyStrings = true)]
    public required string RegistrationNumber { get; init; }

    [Required]
    public required ReviewedListingInput Listing { get; init; }
}

public sealed record ReplaceSavedListingRequest
{
    [Range(typeof(long), "1", "9223372036854775807")]
    public required long ExpectedRevision { get; init; }

    [Required]
    public required ReviewedListingInput Listing { get; init; }
}

public sealed record ReviewedListingInput
{
    [Required(AllowEmptyStrings = true)]
    public required string SubmittedUrl { get; init; }

    public required DateTimeOffset AnalyzedAtUtc { get; init; }

    public required string? RequestedModel { get; init; }

    public required int? PromptVersion { get; init; }

    public required int? SchemaVersion { get; init; }

    [Required]
    public required IReadOnlyList<string> Sources { get; init; }

    [Required]
    public required ListingDraftInput Draft { get; init; }
}

public sealed record ListingDraftInput
{
    public required SourcedValueInput<string>? RegistrationNumber { get; init; }

    public required SourcedValueInput<string>? Make { get; init; }

    public required SourcedValueInput<string>? Model { get; init; }

    public required SourcedValueInput<string>? Variant { get; init; }

    public required SourcedValueInput<int>? ModelYear { get; init; }

    public required SourcedValueInput<string>? Vin { get; init; }

    public required SourcedValueInput<string>? VehicleLabel { get; init; }

    public required SourcedValueInput<decimal>? PriceSek { get; init; }

    public required SourcedValueInput<decimal>? OdometerKilometres { get; init; }

    public required SourcedValueInput<SellerType>? SellerType { get; init; }

    public required SourcedValueInput<string>? Locality { get; init; }

    public required SourcedValueInput<string>? County { get; init; }

    public required SourcedValueInput<DateOnly>? PublishedDate { get; init; }

    public required SourcedValueInput<DateOnly>? UpdatedDate { get; init; }

    public required SourcedValueInput<int>? ImageCount { get; init; }

    public required SourcedCollectionInput<FuelType>? FuelTypes { get; init; }

    public required SourcedValueInput<Transmission>? Transmission { get; init; }

    public required SourcedValueInput<Drivetrain>? Drivetrain { get; init; }

    public required SourcedValueInput<BodyType>? BodyType { get; init; }

    public required SourcedValueInput<string>? Colour { get; init; }

    public required SourcedValueInput<int>? Horsepower { get; init; }

    public required SourcedValueInput<decimal>? EngineDisplacementCubicCentimetres { get; init; }

    public required SourcedCollectionInput<EnergyConsumptionInput>? EnergyConsumptions { get; init; }

    public required SourcedValueInput<decimal>? AnnualVehicleTaxSek { get; init; }

    public required SourcedValueInput<int>? OwnerCount { get; init; }

    public required SourcedValueInput<DateOnly>? FirstRegistrationDate { get; init; }

    public required SourcedValueInput<DateOnly>? LastInspectionDate { get; init; }

    public required SourcedValueInput<DateOnly>? NextInspectionDate { get; init; }

    public required SourcedValueInput<bool>? TowBar { get; init; }

    public required SourcedCollectionInput<string>? Equipment { get; init; }

    public required SourcedCollectionInput<string>? SellerClaims { get; init; }

    public required SourcedCollectionInput<string>? ConditionNotes { get; init; }
}

public sealed record SourcedValueInput<T>
    where T : notnull
{
    [Required(AllowEmptyStrings = true)]
    public required T Value { get; init; }

    [Required]
    public required FieldProvenanceInput Provenance { get; init; }
}

public sealed record SourcedCollectionInput<T>
    where T : notnull
{
    [Required]
    public required IReadOnlyList<T> Values { get; init; }

    [Required]
    public required FieldProvenanceInput Provenance { get; init; }
}

public sealed record FieldProvenanceInput
{
    public required FieldOrigin Origin { get; init; }

    public required ExtractionMethod ExtractionMethod { get; init; }

    public required VerificationStatus Verification { get; init; }

    [Required(AllowEmptyStrings = true)]
    public required string SourceUrl { get; init; }
}

public sealed record EnergyConsumptionInput
{
    [Required(AllowEmptyStrings = true)]
    public required string Label { get; init; }

    public required EnergyUnit Unit { get; init; }

    public required decimal ConsumptionPer100Kilometres { get; init; }
}
