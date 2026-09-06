using System.Globalization;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdCostCalculatorTests
{
    [Fact]
    public void A2_composes_ownership_cost_without_purchase_cash_or_principal()
    {
        var profile = CostExamples.Profile() with
        {
            PurchaseCashSek = 30_000m,
            LoanTerms = new() { AnnualNominalInterestRatePercent = CostExamples.Value(0), TermMonths = 10, SetupFeeSek = 500, MonthlyFeeSek = 25 },
        };
        var car = CostExamples.Car(price: 80_000) with { Residual = HouseholdResidualInput.FixedAmount(CostExamples.Value(60_000), 12) };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(20_750m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(1_729.17m, result.Totals.MonthlyCost.CompleteTotalSek);
        Assert.Equal(80_750m, result.FinancingDetails.AcquisitionCashOutflowSek);
        Assert.Equal(750m, result.Financing.CompleteTotalSek);
        Assert.Equal(60_000m, result.Totals.EndEquity.CompleteTotalSek);
    }

    [Theory]
    [InlineData(12, "90000")]
    [InlineData(24, "81000")]
    [InlineData(6, "94868.33")]
    public void A3_compounds_percentage_depreciation(int months, string expectedResidual)
    {
        var car = CostExamples.Car() with { Residual = HouseholdResidualInput.AnnualPercentage(CostExamples.Value(10)) };
        var result = CostExamples.Calculate(car, CostExamples.Profile(months));
        Assert.Equal(decimal.Parse(expectedResidual, CultureInfo.InvariantCulture), result.Depreciation.ResidualValueSek);
        Assert.Equal(CostSectionState.Complete, result.Totals.OwnershipCost.State);
    }

    [Theory]
    [InlineData(1, "99125838.90")]
    [InlineData(6, "94868329.81")]
    [InlineData(13, "89213255.01")]
    [InlineData(119, "35175333.09")]
    public void Fractional_residual_matches_independent_70_digit_decimal_references(int months, string expected)
    {
        // Independently computed using Python Decimal exp(log(0.9) * months/12), precision 70.
        var car = CostExamples.Car(price: 100_000_000) with { Residual = HouseholdResidualInput.AnnualPercentage(CostExamples.Value(10)) };
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture),
            CostExamples.Calculate(car, CostExamples.Profile(months)).Depreciation.ResidualValueSek);
    }

    [Theory]
    [InlineData(0, 1, 100000)]
    [InlineData(0, 120, 100000)]
    [InlineData(100, 1, 0)]
    [InlineData(100, 120, 0)]
    public void Depreciation_endpoints_are_exact(int percent, int months, int expected)
    {
        var car = CostExamples.Car() with { Residual = HouseholdResidualInput.AnnualPercentage(CostExamples.Value(percent)) };
        Assert.Equal((decimal)expected, CostExamples.Calculate(car, CostExamples.Profile(months)).Depreciation.ResidualValueSek);
    }

    [Fact]
    public void Near_total_depreciation_does_not_lose_root_precision_to_decimal_underflow()
    {
        var car = CostExamples.Car(price: 100_000_000) with
        {
            Residual = HouseholdResidualInput.AnnualPercentage(CostExamples.Value(99.99999999999999999999999999m)),
        };
        // 100,000,000 * exp(log(1e-28)/12) from an independent 70-digit reference.
        Assert.Equal(464_158.88m, CostExamples.Calculate(car, CostExamples.Profile(1)).Depreciation.ResidualValueSek);
    }

    [Fact]
    public void A4_fixed_residual_retains_its_horizon_and_operating_costs_during_mismatch()
    {
        var enteredResidual = HouseholdResidualInput.FixedAmount(CostExamples.Value(60_000), 24);
        var car = CostExamples.Car() with
        {
            Residual = enteredResidual,
            Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual),
        };
        var mismatch = CostExamples.Calculate(car, CostExamples.Profile(36));
        Assert.Null(mismatch.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Contains(mismatch.Depreciation.Cost.Errors, error => error.Code == "residualHorizonMismatch");
        Assert.Equal(3600m, mismatch.Tax.Cost.CompleteTotalSek);
        Assert.Same(enteredResidual, car.Residual);
        Assert.Equal(60_000m, CostExamples.Calculate(car, CostExamples.Profile(24)).Depreciation.ResidualValueSek);
    }

    [Fact]
    public void A7_annual_accrual_does_not_need_a_calendar_start_or_due_month()
    {
        var car = CostExamples.Car() with { Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual) };
        var result = CostExamples.Calculate(car, CostExamples.Profile(6));
        Assert.Equal(600m, result.Tax.Cost.CompleteTotalSek);
        Assert.Equal(100m, result.Totals.MonthlyCost.CompleteTotalSek);
    }

    [Fact]
    public void A8_keeps_service_known_repairs_and_additional_allowance_separate_and_counts_once()
    {
        var car = CostExamples.Car() with
        {
            Service = CostExamples.Category("service", 1200, HouseholdCostCadence.Once, 4),
            Repairs = CostExamples.Category("repair", 2400, HouseholdCostCadence.Once, 2),
            AdditionalRepairAllowancePerMonthSek = CostExamples.Value(300),
        };
        var result = CostExamples.Calculate(car);
        Assert.Equal(1200m, result.Service.Cost.CompleteTotalSek);
        Assert.Equal(2400m, result.Repairs.Cost.CompleteTotalSek);
        Assert.Equal(3600m, result.RepairAllowance.CompleteTotalSek);
        Assert.Equal(7200m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(600m, result.Totals.MonthlyCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(12, 100)]
    [InlineData(13, 0)]
    [InlineData(120, 0)]
    public void One_time_costs_apply_once_only_inside_the_selected_period(int month, int expected)
    {
        var car = CostExamples.Car() with { CustomCosts = CostExamples.Category("custom", 100, HouseholdCostCadence.Once, month) };
        Assert.Equal((decimal)expected, CostExamples.Calculate(car).CustomCosts.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Missing_event_timing_preserves_known_items_and_explicit_out_of_period_events_need_no_amount()
    {
        var car = CostExamples.Car() with
        {
            Repairs = HouseholdCostCategoryInput.FromItems([
                new("known", "Known", CostExamples.Value(500), HouseholdCostCadence.Once, 2),
                new("undated", "Undated", CostExamples.Value(700), HouseholdCostCadence.Once),
                new("later", "Later", null, HouseholdCostCadence.Once, 13),
            ]),
        };
        var result = CostExamples.Calculate(car).Repairs;
        Assert.Equal(500m, result.Cost.KnownSubtotalSek);
        Assert.Null(result.Cost.CompleteTotalSek);
        Assert.Equal(["vehicles[0].repairs.items[1].monthOffset"], result.Cost.MissingComponents);
        Assert.Equal(0m, result.Items[2].Cost.CompleteTotalSek);
    }

    [Theory]
    [InlineData("tax")]
    [InlineData("insurance")]
    [InlineData("service")]
    [InlineData("repairs")]
    [InlineData("allowance")]
    [InlineData("custom")]
    public void Every_missing_applicable_cost_blocks_only_complete_total(string missingCategory)
    {
        var car = CostExamples.Car() with { Residual = HouseholdResidualInput.AnnualPercentage(CostExamples.Value(20)) };
        car = missingCategory switch
        {
            "tax" => car with { Tax = null },
            "insurance" => car with { Insurance = null },
            "service" => car with { Service = null },
            "repairs" => car with { Repairs = null },
            "allowance" => car with { AdditionalRepairAllowancePerMonthSek = null },
            _ => car with { CustomCosts = null },
        };
        var result = CostExamples.Calculate(car);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(20_000m, result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Equal(20_000m, result.Depreciation.Cost.CompleteTotalSek);
        Assert.NotEmpty(result.Totals.OwnershipCost.MissingComponents);
    }

    [Fact]
    public void Missing_residual_does_not_hide_operating_or_financing_sections()
    {
        var result = CostExamples.Calculate(CostExamples.Car() with
        {
            Residual = null, Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual),
        });
        Assert.Equal(1200m, result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(0m, result.Financing.CompleteTotalSek);
        Assert.Equal(CostSectionState.Unavailable, result.Depreciation.Cost.State);
    }

    [Fact]
    public void Missing_fees_preserve_known_interest_and_fees_and_do_not_block_equity()
    {
        var profile = CostExamples.Profile() with
        {
            PurchaseCashSek = 0,
            LoanTerms = new() { TermMonths = 12, AnnualNominalInterestRatePercent = CostExamples.Value(0), SetupFeeSek = 500 },
        };
        var result = CostExamples.Calculate(CostExamples.Car(), profile);
        Assert.Equal(500m, result.Financing.KnownSubtotalSek);
        Assert.Null(result.Financing.CompleteTotalSek);
        Assert.Equal(100_000m, result.Totals.EndEquity.CompleteTotalSek);
    }

    [Fact]
    public void Negative_end_equity_is_not_an_extra_expense_or_validation_error()
    {
        var profile = CostExamples.Profile(6) with
        {
            PurchaseCashSek = 0,
            LoanTerms = new() { TermMonths = 12, AnnualNominalInterestRatePercent = CostExamples.Value(0), SetupFeeSek = 0, MonthlyFeeSek = 0 },
        };
        var car = CostExamples.Car(price: 120_000) with { Residual = HouseholdResidualInput.FixedAmount(CostExamples.Value(10_000), 6) };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(-50_000m, result.Totals.EndEquity.CompleteTotalSek);
        Assert.Equal(110_000m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Empty(result.Totals.EndEquity.Errors);
    }

    [Fact]
    public void Display_rounding_happens_after_composition_and_each_derived_rate()
    {
        var car = CostExamples.Car(price: 0) with
        {
            Tax = CostExamples.Category("tax", 0.004m, HouseholdCostCadence.Monthly),
            Insurance = CostExamples.Category("insurance", 0.004m, HouseholdCostCadence.Monthly),
        };
        var result = CostExamples.Calculate(car, CostExamples.Profile(1));
        Assert.Equal(0m, result.Tax.Cost.CompleteTotalSek);
        Assert.Equal(0m, result.Insurance.Cost.CompleteTotalSek);
        Assert.Equal(0.01m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(0.01m, result.Totals.MonthlyCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(SensitivityMode.Favorable, 6960, 9960)]
    [InlineData(SensitivityMode.Baseline, 7040, 9040)]
    [InlineData(SensitivityMode.Cautious, 4720, 5720)]
    public void One_explicit_mode_updates_all_candidates_without_reordering_assumptions(SensitivityMode mode, int expectedA, int expectedB)
    {
        var prices = new[] { new HouseholdEnergyPrice(FuelType.Petrol, EnergyUnit.Litre, SensitivityValue.Scenarios(10, 20, 30)) };
        var profile = CostExamples.Profile(12, 12_000, prices) with { ActiveSensitivityMode = mode };
        var source = CostExamples.Petrol() with { ConsumptionPer100Kilometres = SensitivityValue.Scenarios(3, 2, 1) };
        var car = CostExamples.Car(sources: [source]) with
        {
            Residual = HouseholdResidualInput.AnnualPercentage(SensitivityValue.Scenarios(3, 2, 1)),
            Service = HouseholdCostCategoryInput.FromItems([new("service", "Service", SensitivityValue.Scenarios(30, 20, 10), HouseholdCostCadence.Monthly)]),
        };
        var result = new HouseholdCostCalculator().Calculate(profile, [car, car with { CandidateKey = "second", PriceSek = 200_000 }]);
        Assert.Equal((decimal)expectedA, result.Vehicles[0].Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal((decimal)expectedB, result.Vehicles[1].Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(mode, result.ActiveSensitivityMode);
        Assert.Equal("SEK", result.Currency);
        Assert.Equal(1, result.CalculationVersion);
        Assert.Equal(1, result.ResultSchemaVersion);
    }
}

internal static class CostExamples
{
    public static SensitivityValue Value(decimal value) => SensitivityValue.Constant(value);

    public static HouseholdProfileInput Profile(int months = 12, decimal annualKm = 0, IEnumerable<HouseholdEnergyPrice>? prices = null) =>
        new(prices ?? [new(FuelType.Petrol, EnergyUnit.Litre, Value(20))])
        {
            PeriodMonths = months, AnnualDistanceKilometres = annualKm, PurchaseCashSek = 100_000_000,
        };

    public static VehicleCostInput Car(string key = "car", decimal price = 100_000, IEnumerable<HouseholdEnergySource>? sources = null) =>
        new(key, price, sources)
        {
            Residual = HouseholdResidualInput.AnnualPercentage(Value(0)),
            Tax = HouseholdCostCategoryInput.KnownZero(), Insurance = HouseholdCostCategoryInput.KnownZero(),
            Service = HouseholdCostCategoryInput.KnownZero(), Repairs = HouseholdCostCategoryInput.KnownZero(),
            CustomCosts = HouseholdCostCategoryInput.KnownZero(), AdditionalRepairAllowancePerMonthSek = Value(0),
        };

    public static HouseholdEnergySource Petrol() => new("petrol", FuelType.Petrol, EnergyUnit.Litre, Value(6), ConsumptionBasis.WholeDistance);
    public static HouseholdEnergySource Electricity(ElectricityBasis basis = ElectricityBasis.Battery) =>
        new("electricity", FuelType.Electricity, EnergyUnit.KilowattHour, Value(18), ConsumptionBasis.WholeDistance, basis);

    public static HouseholdCostCategoryInput Category(string key, decimal amount, HouseholdCostCadence cadence, int? month = null) =>
        HouseholdCostCategoryInput.FromItems([new(key, key, Value(amount), cadence, month)]);

    public static VehicleCostResult Calculate(VehicleCostInput car, HouseholdProfileInput? profile = null) =>
        Assert.Single(new HouseholdCostCalculator().Calculate(profile ?? Profile(), [car]).Vehicles);
}
