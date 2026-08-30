using CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

internal static class ScenarioFactory
{
    public static CostScenario Complete(string label = "  Volvo V70  ") => new(
        label,
        24,
        123_456.123456789012345m,
        80_000.123456789012345m,
        18_500.123456789012345m,
        new FinancingTerms(23_456.123456789012345m, 5.25m, 60),
        [
            new EnergySource("  Bensin  ", EnergyUnit.Litre, 7.45m, 20.123456789012345m, 65m),
            new EnergySource("El", EnergyUnit.KilowattHour, 18.75m, 2.345678901234567m, 35m),
        ],
        new RecurringCost(2_400m, RecurringCostCadence.Annual),
        new RecurringCost(650.123456789012345m, RecurringCostCadence.Monthly),
        new RecurringCost(8_000m, RecurringCostCadence.Annual),
        [
            new NamedRecurringCost("  Parkering  ", 450.123456789012345m, RecurringCostCadence.Monthly),
            new NamedRecurringCost("Däckhotell", 2_000m, RecurringCostCadence.Annual),
        ],
        [
            new OneTimeCost("  Besiktning  ", 600.123456789012345m),
            new OneTimeCost("Tillbehör", 1_500m),
        ]);

    public static CostScenario Replacement() => new(
        "Saab 9-5",
        12,
        40_000m,
        null,
        0m,
        null,
        [],
        null,
        new RecurringCost(0m, RecurringCostCadence.Monthly),
        null,
        [new NamedRecurringCost("Garage", 300m, RecurringCostCadence.Monthly)],
        [new OneTimeCost("Service", 2_500m)]);
}

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    public void Advance(TimeSpan duration)
    {
        utcNow = utcNow.Add(duration);
    }
}
