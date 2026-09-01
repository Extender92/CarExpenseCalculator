using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Core.Listings;

public sealed record ListingDraft
{
    public SourcedValue<RegistrationNumber>? RegistrationNumber { get; init; }

    public SourcedValue<string>? Make { get; init; }

    public SourcedValue<string>? Model { get; init; }

    public SourcedValue<string>? Variant { get; init; }

    public SourcedValue<int>? ModelYear { get; init; }

    public SourcedValue<string>? Vin { get; init; }

    public SourcedValue<string>? VehicleLabel { get; init; }

    public SourcedValue<decimal>? PriceSek { get; init; }

    public SourcedValue<decimal>? OdometerKilometres { get; init; }

    public SourcedValue<SellerType>? SellerType { get; init; }

    public SourcedValue<string>? Location { get; init; }

    public SourcedValue<DateOnly>? PublishedDate { get; init; }

    public SourcedValue<DateOnly>? UpdatedDate { get; init; }

    public SourcedValue<int>? ImageCount { get; init; }

    public SourcedCollection<FuelType>? FuelTypes { get; init; }

    public SourcedValue<Transmission>? Transmission { get; init; }

    public SourcedValue<Drivetrain>? Drivetrain { get; init; }

    public SourcedValue<BodyType>? BodyType { get; init; }

    public SourcedValue<string>? Colour { get; init; }

    public SourcedValue<int>? Horsepower { get; init; }

    public SourcedValue<decimal>? EngineDisplacementCubicCentimetres { get; init; }

    public SourcedCollection<EnergyConsumption>? EnergyConsumptions { get; init; }

    public SourcedValue<decimal>? AnnualVehicleTaxSek { get; init; }

    public SourcedValue<int>? OwnerCount { get; init; }

    public SourcedValue<DateOnly>? FirstRegistrationDate { get; init; }

    public SourcedValue<DateOnly>? LastInspectionDate { get; init; }

    public SourcedValue<DateOnly>? NextInspectionDate { get; init; }

    public SourcedValue<bool>? TowBar { get; init; }

    public SourcedCollection<string>? Equipment { get; init; }

    public SourcedCollection<string>? SellerClaims { get; init; }

    public SourcedCollection<string>? ConditionNotes { get; init; }
}
