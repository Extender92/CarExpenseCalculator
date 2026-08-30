using CarExpenseCalculator.Core.CostScenarios;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class CostScenarioCalculatorTests
{
    private readonly CostScenarioCalculator _calculator = new();

    [Fact]
    public void Worked_example_matches_the_accepted_specification()
    {
        var scenario = new CostScenario(
            "Example car",
            12,
            20_000m,
            15_000m,
            15_000m,
            new FinancingTerms(5_000m, 0m, 12),
            [new EnergySource("Petrol", EnergyUnit.Litre, 8m, 20m, 100m)],
            new RecurringCost(2_400m, RecurringCostCadence.Annual),
            new RecurringCost(500m, RecurringCostCadence.Monthly),
            new RecurringCost(6_000m, RecurringCostCadence.Annual),
            [new NamedRecurringCost("Parking", 300m, RecurringCostCadence.Monthly)],
            [new OneTimeCost("Initial repair", 2_000m)]);

        var result = _calculator.Calculate(scenario);

        Assert.Equal("SEK", result.Currency);
        Assert.Equal(15_000m, result.TotalDistanceKilometres);
        Assert.Equal(1_200m, result.Energy.Sources[0].ConsumedQuantity);
        Assert.Equal(24_000m, result.Energy.TotalCostSek);
        Assert.Equal(44_000m, result.CashFlow.KnownOperatingCostSek);
        Assert.Equal(64_000m, result.CashFlow.KnownTotalSek);
        Assert.Equal(5_333.33m, result.CashFlow.AveragePerMonthSek);
        Assert.Equal(49_000m, result.NetOwnershipCost!.KnownTotalSek);
        Assert.Equal(4_083.33m, result.NetOwnershipCost.AveragePerMonthSek);
        Assert.Equal(15_000m, result.NetOwnershipCost.EstimatedEquityAtPeriodEndSek);
        Assert.Equal(1_250m, result.Financing!.MonthlyPaymentSek);
        Assert.Equal(0m, result.Financing.InterestPaidSek);
        Assert.Equal(0m, result.Financing.RemainingPrincipalSek);
        Assert.True(result.Completeness.IsComplete);
    }

    [Fact]
    public void Cash_purchase_with_zero_distance_needs_no_energy_source()
    {
        var scenario = new ScenarioBuilder
        {
            PurchasePriceSek = 12_000m,
            ExpectedResidualValueSek = 8_000m,
            AnnualDistanceKilometres = 0m,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Null(result.Financing);
        Assert.Empty(result.Energy.Sources);
        Assert.Equal(0m, result.Energy.TotalCostSek);
        Assert.Equal(12_000m, result.CashFlow.AcquisitionCashPaidSek);
        Assert.Equal(12_000m, result.CashFlow.KnownTotalSek);
        Assert.Equal(4_000m, result.NetOwnershipCost!.KnownTotalSek);
    }

    [Fact]
    public void Missing_standard_costs_return_known_totals_and_stable_missing_order()
    {
        var scenario = new ScenarioBuilder
        {
            ExpectedResidualValueSek = null,
            VehicleTax = null,
            Insurance = null,
            MaintenanceAndRepairs = null,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(10_000m, result.CashFlow.KnownTotalSek);
        Assert.Null(result.CashFlow.VehicleTaxSek);
        Assert.Null(result.CashFlow.InsuranceSek);
        Assert.Null(result.CashFlow.MaintenanceAndRepairsSek);
        Assert.False(result.CashFlow.IsComplete);
        Assert.False(result.Completeness.IsComplete);
        Assert.False(result.Completeness.IsCashFlowComplete);
        Assert.False(result.Completeness.IsNetOwnershipCostAvailable);
        Assert.Equal(
            [
                MissingCostCategory.VehicleTax,
                MissingCostCategory.Insurance,
                MissingCostCategory.MaintenanceAndRepairs,
                MissingCostCategory.ResidualValue,
            ],
            result.Completeness.MissingCategories);
        Assert.Null(result.NetOwnershipCost);
    }

    [Fact]
    public void Explicit_zero_standard_costs_are_complete()
    {
        var scenario = new ScenarioBuilder
        {
            PurchasePriceSek = 0m,
            ExpectedResidualValueSek = 0m,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.True(result.Completeness.IsComplete);
        Assert.True(result.CashFlow.IsComplete);
        Assert.Empty(result.Completeness.MissingCategories);
        Assert.Equal(0m, result.CashFlow.KnownTotalSek);
        Assert.Equal(0m, result.NetOwnershipCost!.KnownTotalSek);
    }

    [Fact]
    public void Missing_residual_value_does_not_make_cash_flow_incomplete()
    {
        var scenario = new ScenarioBuilder
        {
            ExpectedResidualValueSek = null,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.True(result.CashFlow.IsComplete);
        Assert.True(result.Completeness.IsCashFlowComplete);
        Assert.False(result.Completeness.IsComplete);
        Assert.False(result.Completeness.IsNetOwnershipCostAvailable);
        Assert.Equal([MissingCostCategory.ResidualValue], result.Completeness.MissingCategories);
        Assert.Null(result.NetOwnershipCost);
    }

    [Fact]
    public void Available_net_cost_is_marked_incomplete_when_a_standard_cost_is_unknown()
    {
        var scenario = new ScenarioBuilder
        {
            VehicleTax = null,
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.True(result.Completeness.IsNetOwnershipCostAvailable);
        Assert.NotNull(result.NetOwnershipCost);
        Assert.False(result.NetOwnershipCost.IsComplete);
        Assert.Equal([MissingCostCategory.VehicleTax], result.Completeness.MissingCategories);
    }

    [Fact]
    public void Positive_interest_annuity_uses_decimal_calculation_without_intermediate_rounding()
    {
        var scenario = new ScenarioBuilder
        {
            CalculationPeriodMonths = 24,
            PurchasePriceSek = 100_000m,
            ExpectedResidualValueSek = 60_000m,
            Financing = new FinancingTerms(20_000m, 5m, 60),
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(1_509.70m, result.Financing!.MonthlyPaymentSek);
        Assert.Equal(24, result.Financing.PaymentsMade);
        Assert.Equal(36_232.77m, result.Financing.LoanPaymentsDuringPeriodSek);
        Assert.Equal(29_627.84m, result.Financing.PrincipalRepaidSek);
        Assert.Equal(6_604.92m, result.Financing.InterestPaidSek);
        Assert.Equal(50_372.16m, result.Financing.RemainingPrincipalSek);
    }

    [Theory]
    [InlineData(6, 6, 6_000, 6_000)]
    [InlineData(12, 12, 12_000, 0)]
    [InlineData(24, 12, 12_000, 0)]
    public void Payments_stop_at_the_end_of_a_zero_interest_loan(
        int calculationPeriodMonths,
        int expectedPaymentsMade,
        decimal expectedPayments,
        decimal expectedRemainingPrincipal)
    {
        var scenario = new ScenarioBuilder
        {
            CalculationPeriodMonths = calculationPeriodMonths,
            PurchasePriceSek = 12_000m,
            ExpectedResidualValueSek = 0m,
            Financing = new FinancingTerms(0m, 0m, 12),
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(expectedPaymentsMade, result.Financing!.PaymentsMade);
        Assert.Equal(expectedPayments, result.Financing.LoanPaymentsDuringPeriodSek);
        Assert.Equal(expectedRemainingPrincipal, result.Financing.RemainingPrincipalSek);
    }

    [Fact]
    public void Partial_year_prorates_annual_and_monthly_costs()
    {
        var scenario = new ScenarioBuilder
        {
            CalculationPeriodMonths = 6,
            PurchasePriceSek = 0m,
            ExpectedResidualValueSek = 0m,
            VehicleTax = new RecurringCost(1_200m, RecurringCostCadence.Annual),
            Insurance = new RecurringCost(100m, RecurringCostCadence.Monthly),
            MaintenanceAndRepairs = new RecurringCost(2_400m, RecurringCostCadence.Annual),
            OtherRecurringCosts =
            [
                new NamedRecurringCost("Annual", 1_200m, RecurringCostCadence.Annual),
                new NamedRecurringCost("Monthly", 50m, RecurringCostCadence.Monthly),
            ],
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(600m, result.CashFlow.VehicleTaxSek);
        Assert.Equal(600m, result.CashFlow.InsuranceSek);
        Assert.Equal(1_200m, result.CashFlow.MaintenanceAndRepairsSek);
        Assert.Equal(900m, result.CashFlow.OtherRecurringCostSek);
        Assert.Equal(3_300m, result.CashFlow.KnownTotalSek);
        Assert.Equal(550m, result.CashFlow.AveragePerMonthSek);
        Assert.Equal(6_600m, result.CashFlow.AveragePerYearSek);
    }

    [Fact]
    public void Two_energy_sources_preserve_order_labels_units_and_separate_costs()
    {
        var scenario = new ScenarioBuilder
        {
            AnnualDistanceKilometres = 12_000m,
            EnergySources =
            [
                new EnergySource("  Energy  ", EnergyUnit.Litre, 5m, 20m, 40m),
                new EnergySource("Energy", EnergyUnit.KilowattHour, 20m, 2m, 60m),
            ],
            OtherRecurringCosts =
            [
                new NamedRecurringCost(" Duplicate ", 1m, RecurringCostCadence.Monthly),
                new NamedRecurringCost("Duplicate", 2m, RecurringCostCadence.Monthly),
            ],
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal("Energy", result.Energy.Sources[0].Label);
        Assert.Equal(EnergyUnit.Litre, result.Energy.Sources[0].Unit);
        Assert.Equal(4_800m, result.Energy.Sources[0].AllocatedDistanceKilometres);
        Assert.Equal(240m, result.Energy.Sources[0].ConsumedQuantity);
        Assert.Equal(4_800m, result.Energy.Sources[0].CostSek);
        Assert.Equal("Energy", result.Energy.Sources[1].Label);
        Assert.Equal(EnergyUnit.KilowattHour, result.Energy.Sources[1].Unit);
        Assert.Equal(7_200m, result.Energy.Sources[1].AllocatedDistanceKilometres);
        Assert.Equal(1_440m, result.Energy.Sources[1].ConsumedQuantity);
        Assert.Equal(2_880m, result.Energy.Sources[1].CostSek);
        Assert.Equal(7_680m, result.Energy.TotalCostSek);
        Assert.Equal(["Duplicate", "Duplicate"], result.OtherRecurringCosts.Select(item => item.Label));
    }

    [Fact]
    public void Constructor_defensively_copies_input_collections()
    {
        var energySources = new List<EnergySource>();
        var recurringCosts = new List<NamedRecurringCost>();
        var oneTimeCosts = new List<OneTimeCost>();
        var scenario = new CostScenario(
            null,
            12,
            0m,
            0m,
            0m,
            null,
            energySources,
            new RecurringCost(0m, RecurringCostCadence.Annual),
            new RecurringCost(0m, RecurringCostCadence.Annual),
            new RecurringCost(0m, RecurringCostCadence.Annual),
            recurringCosts,
            oneTimeCosts);

        energySources.Add(new EnergySource("Late", EnergyUnit.Litre, 1m, 1m, 100m));
        recurringCosts.Add(new NamedRecurringCost("Late", 1m, RecurringCostCadence.Monthly));
        oneTimeCosts.Add(new OneTimeCost("Late", 1m));

        var result = _calculator.Calculate(scenario);

        Assert.Empty(scenario.EnergySources);
        Assert.Empty(scenario.OtherRecurringCosts);
        Assert.Empty(scenario.OtherOneTimeCosts);
        Assert.Empty(result.Energy.Sources);
    }

    [Fact]
    public void Result_rounds_midpoints_away_from_zero()
    {
        var scenario = new ScenarioBuilder
        {
            PurchasePriceSek = 0m,
            ExpectedResidualValueSek = 0m,
            OtherOneTimeCosts = [new OneTimeCost("Boundary", 1.005m)],
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.Equal(1.01m, result.OtherOneTimeCosts[0].AmountSek);
        Assert.Equal(1.01m, result.CashFlow.OtherOneTimeCostSek);
        Assert.Equal(1.01m, result.CashFlow.KnownTotalSek);
    }

    [Fact]
    public void Aggregate_is_not_reconstructed_from_rounded_components()
    {
        var scenario = new ScenarioBuilder
        {
            PurchasePriceSek = 0m,
            ExpectedResidualValueSek = 0m,
            OtherOneTimeCosts =
            [
                new OneTimeCost("First", 0.004m),
                new OneTimeCost("Second", 0.004m),
            ],
        }.Build();

        var result = _calculator.Calculate(scenario);

        Assert.All(result.OtherOneTimeCosts, item => Assert.Equal(0m, item.AmountSek));
        Assert.Equal(0.01m, result.CashFlow.OtherOneTimeCostSek);
        Assert.Equal(0.01m, result.CashFlow.KnownTotalSek);
    }
}
