using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

internal sealed record DraftEnergyConsumption(string Label, EnergyUnit Unit, decimal ConsumptionPer100Kilometres)
{
    public static DraftEnergyConsumption FromCore(EnergyConsumption x) => new(x.Label, x.Unit, x.ConsumptionPer100Kilometres);
    public EnergyConsumption ToCore() => new(Label, Unit, ConsumptionPer100Kilometres);
}
