using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

internal sealed class VehicleListingEntity
{
    public Guid Id { get; set; }

    public Guid VehicleId { get; set; }

    public required VehicleEntity Vehicle { get; set; }

    public long ListingVersion { get; set; }

    public int ListingSchemaVersion { get; set; }

    public required string SubmittedUrl { get; set; }

    public required string NormalizedUrl { get; set; }

    public ListingAnalysisStatus Status { get; set; }

    public required string[] MissingFields { get; set; }

    public string? RequestedModel { get; set; }

    public int? PromptVersion { get; set; }

    public int? ExtractionSchemaVersion { get; set; }

    public DateTimeOffset AnalyzedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Variant { get; set; }

    public int? ModelYear { get; set; }

    public string? Vin { get; set; }

    public decimal? PriceSek { get; set; }

    public decimal? OdometerKilometres { get; set; }

    public SellerType? SellerType { get; set; }

    public string? Locality { get; set; }

    public string? County { get; set; }

    public DateOnly? PublishedDate { get; set; }

    public DateOnly? AdvertisedUpdatedDate { get; set; }

    public int? ImageCount { get; set; }

    public string[]? FuelTypes { get; set; }

    public Transmission? Transmission { get; set; }

    public Drivetrain? Drivetrain { get; set; }

    public BodyType? BodyType { get; set; }

    public string? Colour { get; set; }

    public int? Horsepower { get; set; }

    public decimal? EngineDisplacementCubicCentimetres { get; set; }

    public string? EnergyConsumptionsJson { get; set; }

    public decimal? AnnualVehicleTaxSek { get; set; }

    public int? OwnerCount { get; set; }

    public DateOnly? FirstRegistrationDate { get; set; }

    public DateOnly? LastInspectionDate { get; set; }

    public DateOnly? NextInspectionDate { get; set; }

    public bool? TowBar { get; set; }

    public bool EquipmentKnown { get; set; }

    public string? SellerClaimsJson { get; set; }

    public string? ConditionNotesJson { get; set; }

    public required string FieldProvenanceJson { get; set; }

    public List<ListingSourceEntity> Sources { get; } = [];

    public List<ListingEquipmentEntity> Equipment { get; } = [];
}

internal sealed class ListingSourceEntity
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public required VehicleListingEntity Listing { get; set; }

    public int Position { get; set; }

    public required string Url { get; set; }

    public bool MatchesSubmittedUrl { get; set; }
}

internal sealed class ListingEquipmentEntity
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public required VehicleListingEntity Listing { get; set; }

    public int Position { get; set; }

    public required string Value { get; set; }
}
