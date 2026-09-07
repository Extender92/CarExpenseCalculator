using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

public enum ServiceDocumentationStatus { Documented, Partial, Absent }

// A current value object attached to the existing vehicle aggregate by later storage.
// Null inputs are normalized to Unknown; there are no economic defaults or derived totals.
public sealed record VehicleComparisonFacts
{
    public VehicleFact<decimal>? PurchasePriceSek { get; init; }
    public VehicleFact<decimal>? OdometerKilometres { get; init; }
    public VehicleFact<int>? OwnerCount { get; init; }
    public VehicleFact<bool>? TowBar { get; init; }
    public VehicleFact<Transmission>? Transmission { get; init; }
    public VehicleFact<int>? Seats { get; init; }
    public VehicleFact<int>? ModelYear { get; init; }
    public VehicleFact<FuelTypeSet>? FuelTypes { get; init; }
    public VehicleFact<BodyType>? BodyType { get; init; }
    public VehicleFact<Drivetrain>? Drivetrain { get; init; }
    public VehicleFact<string>? Locality { get; init; }
    public VehicleFact<string>? County { get; init; }
    public VehicleFact<int>? TowingCapacityKilograms { get; init; }
    public VehicleFact<DateOnly>? InspectionValidThrough { get; init; }
    public VehicleFact<ServiceDocumentationStatus>? ServiceDocumentation { get; init; }
    // Independently evidenced supporting facts never imply documentation completeness.
    public VehicleFact<DateOnly>? LastServiceDate { get; init; }
    public VehicleFact<decimal>? LastServiceOdometerKilometres { get; init; }
    public VehicleFact<string>? ServiceNotes { get; init; }
}
