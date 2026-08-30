using CarExpenseCalculator.Core.CostScenarios;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class CostScenarioValidationTests
{
    private readonly CostScenarioCalculator _calculator = new();

    [Fact]
    public void Null_scenario_is_a_programmer_error()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Calculate(null!));
    }

    [Fact]
    public void Required_collections_cannot_be_null()
    {
        Assert.Throws<ArgumentNullException>(() => new CostScenario(
            null, 1, 0m, 0m, 0m, null, null!, null, null, null, [], []));
        Assert.Throws<ArgumentNullException>(() => new CostScenario(
            null, 1, 0m, 0m, 0m, null, [], null, null, null, null!, []));
        Assert.Throws<ArgumentNullException>(() => new CostScenario(
            null, 1, 0m, 0m, 0m, null, [], null, null, null, [], null!));
    }

    [Fact]
    public void Independent_validation_errors_are_accumulated_in_field_order()
    {
        var scenario = new ScenarioBuilder
        {
            VehicleLabel = "   ",
            CalculationPeriodMonths = 0,
            PurchasePriceSek = -1m,
            ExpectedResidualValueSek = -2m,
            AnnualDistanceKilometres = 1m,
            VehicleTax = new RecurringCost(-1m, (RecurringCostCadence)99),
        }.Build();

        var exception = Assert.Throws<CostScenarioValidationException>(() => _calculator.Calculate(scenario));

        Assert.Equal("vehicleLabel", exception.Errors[0].Path);
        AssertPaths(
            exception,
            "vehicleLabel",
            "calculationPeriodMonths",
            "purchasePriceSek",
            "expectedResidualValueSek",
            "energySources",
            "vehicleTax.amountSek",
            "vehicleTax.cadence");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Calculation_period_must_be_between_one_and_120_months(int months)
    {
        var scenario = new ScenarioBuilder { CalculationPeriodMonths = months }.Build();

        AssertInvalid(scenario, "calculationPeriodMonths");
    }

    [Fact]
    public void Money_distance_and_residual_ranges_are_enforced()
    {
        AssertInvalid(new ScenarioBuilder { PurchasePriceSek = -0.01m }.Build(), "purchasePriceSek");
        AssertInvalid(new ScenarioBuilder { PurchasePriceSek = 100_000_000.01m }.Build(), "purchasePriceSek");
        AssertInvalid(
            new ScenarioBuilder { ExpectedResidualValueSek = -0.01m }.Build(),
            "expectedResidualValueSek");
        AssertInvalid(
            new ScenarioBuilder { ExpectedResidualValueSek = 10_000.01m }.Build(),
            "expectedResidualValueSek");
        AssertInvalid(
            new ScenarioBuilder { AnnualDistanceKilometres = -0.001m }.Build(),
            "annualDistanceKilometres");
        AssertInvalid(
            new ScenarioBuilder { AnnualDistanceKilometres = 1_000_000.001m }.Build(),
            "annualDistanceKilometres");
    }

    [Fact]
    public void Financing_requires_a_positive_purchase_and_valid_down_payment()
    {
        AssertInvalid(
            new ScenarioBuilder
            {
                PurchasePriceSek = 0m,
                ExpectedResidualValueSek = 0m,
                Financing = new FinancingTerms(0m, 0m, 12),
            }.Build(),
            "financing");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(-0.01m, 0m, 12) }.Build(),
            "financing.downPaymentSek");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(10_000m, 0m, 12) }.Build(),
            "financing.downPaymentSek");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(11_000m, 0m, 12) }.Build(),
            "financing.downPaymentSek");
    }

    [Fact]
    public void Financing_rate_and_term_ranges_are_enforced()
    {
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(0m, -0.001m, 12) }.Build(),
            "financing.annualNominalInterestRatePercent");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(0m, 100.001m, 12) }.Build(),
            "financing.annualNominalInterestRatePercent");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(0m, 0m, 0) }.Build(),
            "financing.termMonths");
        AssertInvalid(
            new ScenarioBuilder { Financing = new FinancingTerms(0m, 0m, 121) }.Build(),
            "financing.termMonths");
    }

    [Fact]
    public void Positive_distance_requires_one_or_two_energy_sources_with_exact_shares()
    {
        AssertInvalid(
            new ScenarioBuilder { AnnualDistanceKilometres = 1m }.Build(),
            "energySources");
        AssertInvalid(
            new ScenarioBuilder
            {
                EnergySources =
                [
                    ValidEnergySource(34m),
                    ValidEnergySource(33m),
                    ValidEnergySource(33m),
                ],
            }.Build(),
            "energySources");
        AssertInvalid(
            new ScenarioBuilder { EnergySources = [ValidEnergySource(99.999m)] }.Build(),
            "energySources");
    }

    [Fact]
    public void Energy_source_fields_are_validated()
    {
        AssertInvalid(
            new ScenarioBuilder
            {
                EnergySources =
                [new EnergySource(" ", (EnergyUnit)99, 0m, -1m, 0m)],
            }.Build(),
            "energySources[0].label",
            "energySources[0].unit",
            "energySources[0].consumptionPer100Kilometres",
            "energySources[0].pricePerUnitSek",
            "energySources[0].distanceSharePercent",
            "energySources");
        AssertInvalid(
            new ScenarioBuilder
            {
                EnergySources =
                [new EnergySource(new string('x', 121), EnergyUnit.Litre, 10_000.001m, 100_000.001m, 100.001m)],
            }.Build(),
            "energySources[0].label",
            "energySources[0].consumptionPer100Kilometres",
            "energySources[0].pricePerUnitSek",
            "energySources[0].distanceSharePercent",
            "energySources");
    }

    [Fact]
    public void Extreme_invalid_energy_shares_are_reported_without_decimal_overflow()
    {
        var scenario = new ScenarioBuilder
        {
            EnergySources =
            [
                ValidEnergySource(decimal.MaxValue),
                ValidEnergySource(decimal.MaxValue),
            ],
        }.Build();

        AssertInvalid(
            scenario,
            "energySources[0].distanceSharePercent",
            "energySources[1].distanceSharePercent",
            "energySources");
    }

    [Fact]
    public void Null_energy_source_is_reported_without_calculation()
    {
        var scenario = new ScenarioBuilder
        {
            EnergySources = [null!],
        }.Build();

        AssertInvalid(scenario, "energySources[0]", "energySources");
    }

    [Fact]
    public void Standard_recurring_costs_validate_amount_and_cadence()
    {
        var scenario = new ScenarioBuilder
        {
            Insurance = new RecurringCost(100_000_000.01m, (RecurringCostCadence)99),
        }.Build();

        AssertInvalid(scenario, "insurance.amountSek", "insurance.cadence");
    }

    [Fact]
    public void Custom_recurring_costs_validate_count_label_amount_and_cadence()
    {
        var tooMany = Enumerable.Range(0, 51)
            .Select(index => new NamedRecurringCost($"Cost {index}", 0m, RecurringCostCadence.Annual))
            .ToArray();
        AssertInvalid(
            new ScenarioBuilder { OtherRecurringCosts = tooMany }.Build(),
            "otherRecurringCosts");

        var invalid = new ScenarioBuilder
        {
            OtherRecurringCosts =
            [new NamedRecurringCost(" ", -1m, (RecurringCostCadence)99)],
        }.Build();
        AssertInvalid(
            invalid,
            "otherRecurringCosts[0].label",
            "otherRecurringCosts[0].amountSek",
            "otherRecurringCosts[0].cadence");
    }

    [Fact]
    public void One_time_costs_validate_count_label_and_amount()
    {
        var tooMany = Enumerable.Range(0, 51)
            .Select(index => new OneTimeCost($"Cost {index}", 0m))
            .ToArray();
        AssertInvalid(
            new ScenarioBuilder { OtherOneTimeCosts = tooMany }.Build(),
            "otherOneTimeCosts");

        var invalid = new ScenarioBuilder
        {
            OtherOneTimeCosts = [new OneTimeCost(new string('x', 121), 100_000_000.01m)],
        }.Build();
        AssertInvalid(
            invalid,
            "otherOneTimeCosts[0].label",
            "otherOneTimeCosts[0].amountSek");
    }

    [Fact]
    public void Null_custom_costs_are_reported_without_calculation()
    {
        AssertInvalid(
            new ScenarioBuilder { OtherRecurringCosts = [null!] }.Build(),
            "otherRecurringCosts[0]");
        AssertInvalid(
            new ScenarioBuilder { OtherOneTimeCosts = [null!] }.Build(),
            "otherOneTimeCosts[0]");
    }

    [Fact]
    public void Optional_vehicle_label_is_validated_after_trimming()
    {
        AssertInvalid(new ScenarioBuilder { VehicleLabel = " " }.Build(), "vehicleLabel");
        AssertInvalid(
            new ScenarioBuilder { VehicleLabel = $"  {new string('x', 121)}  " }.Build(),
            "vehicleLabel");
    }

    [Fact]
    public void Documented_maximum_values_are_accepted()
    {
        var maximumLengthLabel = new string('x', 120);
        var scenario = new ScenarioBuilder
        {
            VehicleLabel = maximumLengthLabel,
            CalculationPeriodMonths = 120,
            PurchasePriceSek = 100_000_000m,
            ExpectedResidualValueSek = 100_000_000m,
            AnnualDistanceKilometres = 1_000_000m,
            Financing = new FinancingTerms(0m, 100m, 120),
            EnergySources =
            [new EnergySource(maximumLengthLabel, EnergyUnit.Kilogram, 10_000m, 100_000m, 100m)],
            VehicleTax = new RecurringCost(100_000_000m, RecurringCostCadence.Monthly),
            Insurance = new RecurringCost(100_000_000m, RecurringCostCadence.Monthly),
            MaintenanceAndRepairs = new RecurringCost(100_000_000m, RecurringCostCadence.Monthly),
            OtherRecurringCosts = Enumerable.Range(0, 50)
                .Select(index => new NamedRecurringCost(
                    $"Recurring {index}",
                    100_000_000m,
                    RecurringCostCadence.Monthly)),
            OtherOneTimeCosts = Enumerable.Range(0, 50)
                .Select(index => new OneTimeCost($"One time {index}", 100_000_000m)),
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(120, result.CalculationPeriodMonths);
        Assert.Equal(10_000_000m, result.TotalDistanceKilometres);
        Assert.Equal(50, result.OtherRecurringCosts.Count);
        Assert.Equal(50, result.OtherOneTimeCosts.Count);
        Assert.Equal(0m, result.Financing!.RemainingPrincipalSek);
    }

    [Fact]
    public void Documented_minimum_values_are_accepted()
    {
        var scenario = new ScenarioBuilder
        {
            VehicleLabel = null,
            CalculationPeriodMonths = 1,
            PurchasePriceSek = 0m,
            ExpectedResidualValueSek = 0m,
            AnnualDistanceKilometres = 0m,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(0m, result.CashFlow.KnownTotalSek);
        Assert.True(result.Completeness.IsComplete);
    }

    private static EnergySource ValidEnergySource(decimal sharePercent)
    {
        return new EnergySource("Energy", EnergyUnit.Litre, 1m, 1m, sharePercent);
    }

    private void AssertInvalid(CostScenario scenario, params string[] expectedPaths)
    {
        var exception = Assert.Throws<CostScenarioValidationException>(() => _calculator.Calculate(scenario));
        AssertPaths(exception, expectedPaths);
    }

    private static void AssertPaths(
        CostScenarioValidationException exception,
        params string[] expectedPaths)
    {
        foreach (var path in expectedPaths)
        {
            Assert.Contains(exception.Errors, error => error.Path == path);
        }
    }
}
