using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;

namespace CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

internal sealed class VehicleEntity
{
    public Guid Id { get; set; }

    public required string RegistrationNumber { get; set; }

    public string? VehicleLabel { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public SavedCostScenarioEntity? Scenario { get; set; }

    public VehicleListingEntity? Listing { get; set; }

    public VehicleCostInputEntity? HouseholdCostInput { get; set; }

    public Comparisons.VehicleComparisonFactsEntity? ComparisonFacts { get; set; }
}
