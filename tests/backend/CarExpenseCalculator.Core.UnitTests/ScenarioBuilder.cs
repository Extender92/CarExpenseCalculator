using CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Core.UnitTests;

internal sealed class ScenarioBuilder
{
    public string? VehicleLabel { get; set; } = "Test vehicle";

    public int CalculationPeriodMonths { get; set; } = 12;

    public decimal PurchasePriceSek { get; set; } = 10_000m;

    public decimal? ExpectedResidualValueSek { get; set; } = 5_000m;

    public decimal AnnualDistanceKilometres { get; set; }

    public FinancingTerms? Financing { get; set; }

    public IEnumerable<EnergySource> EnergySources { get; set; } = [];

    public RecurringCost? VehicleTax { get; set; } = ZeroRecurringCost;

    public RecurringCost? Insurance { get; set; } = ZeroRecurringCost;

    public RecurringCost? MaintenanceAndRepairs { get; set; } = ZeroRecurringCost;

    public IEnumerable<NamedRecurringCost> OtherRecurringCosts { get; set; } = [];

    public IEnumerable<OneTimeCost> OtherOneTimeCosts { get; set; } = [];

    public CostScenario Build()
    {
        return new CostScenario(
            VehicleLabel,
            CalculationPeriodMonths,
            PurchasePriceSek,
            ExpectedResidualValueSek,
            AnnualDistanceKilometres,
            Financing,
            EnergySources,
            VehicleTax,
            Insurance,
            MaintenanceAndRepairs,
            OtherRecurringCosts,
            OtherOneTimeCosts);
    }

    private static RecurringCost ZeroRecurringCost =>
        new(0m, RecurringCostCadence.Annual);
}
