using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

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
}
