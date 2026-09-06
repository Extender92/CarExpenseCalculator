using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdPaymentCalendarTests
{
    [Fact]
    public void A2_reconciles_purchase_cash_principal_and_cost_and_stops_fees()
    {
        var profile = PaymentExamples.Profile() with
        {
            PurchaseCashSek = 30_000, StartupBudgetSek = 500,
            LoanTerms = new() { TermMonths = 10, AnnualNominalInterestRatePercent = CostExamples.Value(0), SetupFeeSek = 500, MonthlyFeeSek = 25 },
        };
        var car = CostExamples.Car(price: 80_000) with { Residual = HouseholdResidualInput.FixedAmount(CostExamples.Value(60_000), 12) };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(80_750m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(20_750m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Equal(30_000m, result.Reconciliation.PurchaseCash.CompleteTotalSek);
        Assert.Equal(50_000m, result.Reconciliation.PrincipalRepaid.CompleteTotalSek);
        Assert.Equal(500m, result.StartupBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.StartupBudget.Status);
        Assert.Equal(0m, result.Payments.Months[11].Outflow.CompleteTotalSek);
        Assert.Equal(0m, result.Payments.Months[12].Outflow.CompleteTotalSek);
        Assert.Equal(CostSectionState.NotApplicable, result.Lease.Cost.State);
    }

    [Fact]
    public void A7_pays_a_full_annual_bill_but_accrues_only_the_selected_period()
    {
        var car = PaymentExamples.Car() with { Tax = PaymentExamples.AnnualTax(3) };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile(6) with { MonthlyBudgetSek = 200 });
        Assert.Equal(600m, result.Tax.Cost.CompleteTotalSek);
        Assert.Equal(1200m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(1200m, result.Payments.Months[3].Outflow.CompleteTotalSek);
        Assert.Equal(new CalendarMonth(2026, 3), result.Payments.Months[3].CalendarMonth);
        Assert.Equal(200m, result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.MonthlyBudget.Status);
        Assert.Equal(600m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(2026, 12, 3, 1, 2027, 1, 2)]
    [InlineData(9989, 12, 120, 11, 9990, 11, 12)]
    public void Calendar_mapping_crosses_years_and_handles_the_maximum_supported_start(int year, int start, int months, int due, int expectedYear, int expectedMonth, int firstBill)
    {
        var result = CostExamples.Calculate(PaymentExamples.Car() with { Tax = PaymentExamples.AnnualTax(due) },
            PaymentExamples.Profile(months) with { StartMonth = new(year, start) });
        Assert.Equal(new CalendarMonth(expectedYear, expectedMonth), result.Payments.Months[firstBill].CalendarMonth);
        Assert.Equal(1200m, result.Payments.Months[firstBill].Outflow.CompleteTotalSek);
        Assert.Equal(months + 1, result.Payments.Months.Count);
        Assert.Null(result.Payments.Months[0].CalendarMonth);
    }

    [Fact]
    public void An_annual_due_month_outside_the_horizon_creates_no_bill_or_refund()
    {
        var result = CostExamples.Calculate(PaymentExamples.Car() with { Tax = PaymentExamples.AnnualTax(3) },
            PaymentExamples.Profile(2) with { StartMonth = new(2026, 4) });
        Assert.Equal(200m, result.Tax.Cost.CompleteTotalSek);
        Assert.Equal(0m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(0m, result.Payments.ExternalInflow.CompleteTotalSek);
        Assert.Equal(200m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_annual_timing_preserves_cost_but_prevents_a_false_budget_pass(bool missingStart)
    {
        var car = PaymentExamples.Car() with { Tax = PaymentExamples.AnnualTax(missingStart ? 3 : null) };
        var profile = PaymentExamples.Profile(6) with { StartMonth = missingStart ? null : new CalendarMonth(2026, 1), MonthlyBudgetSek = 1000 };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(600m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Null(result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Unknown, result.MonthlyBudget.Status);
        Assert.Equal(0m, result.StartupBudget.FundingRequired.CompleteTotalSek);
        Assert.Null(result.Payments.Months[1].Outflow.CompleteTotalSek);
        Assert.Equal(1200m, Assert.Single(result.Payments.Sources, source => source.Category == "tax").UnscheduledAmount.KnownSubtotalSek);
    }

    [Fact]
    public void Missing_calendar_labels_do_not_invalidate_known_relative_payments_or_average()
    {
        var result = CostExamples.Calculate(PaymentExamples.Car() with { Insurance = CostExamples.Category("insurance", 200, HouseholdCostCadence.Monthly) },
            PaymentExamples.Profile(3) with { StartMonth = null, MonthlyBudgetSek = 200 });
        Assert.Equal(600m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Null(result.Payments.CalendarStatus.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.MonthlyBudget.Status);
        Assert.Null(result.Payments.Months[1].CalendarMonth);
    }

    [Fact]
    public void A8_reserve_is_internal_saving_and_never_a_workshop_bill()
    {
        var car = PaymentExamples.Car() with
        {
            Service = CostExamples.Category("service", 1200, HouseholdCostCadence.Once, 4),
            Repairs = CostExamples.Category("repair", 2400, HouseholdCostCadence.Once, 2),
            AdditionalRepairAllowancePerMonthSek = CostExamples.Value(300),
        };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile());
        Assert.Equal(7200m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(3600m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(3600m, result.Payments.InternalSaving.CompleteTotalSek);
        Assert.Equal(600m, result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(7200m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Equal(HouseholdPaymentDirection.InternalSaving, Assert.Single(result.Payments.Sources, source => source.Category == "repairAllowance").Direction);
        Assert.Equal(2400m, result.Payments.Months[2].Outflow.CompleteTotalSek);
        Assert.Equal(300m, result.Payments.Months[2].InternalSaving.CompleteTotalSek);
    }

    [Theory]
    [InlineData(200, HouseholdBudgetStatus.WithinLimit)]
    [InlineData(199, HouseholdBudgetStatus.Exceeded)]
    [InlineData(0, HouseholdBudgetStatus.Exceeded)]
    public void A11_uses_the_average_and_keeps_startup_separate(int limit, HouseholdBudgetStatus expected)
    {
        var car = PaymentExamples.Car() with
        {
            Repairs = CostExamples.Category("repair", 2400, HouseholdCostCadence.Once, 2),
            CustomCosts = CostExamples.Category("startup", 500, HouseholdCostCadence.Once, 0),
        };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile() with { MonthlyBudgetSek = limit, StartupBudgetSek = 500 });
        Assert.Equal(expected, result.MonthlyBudget.Status);
        Assert.Equal(200m, result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.StartupBudget.Status);
        Assert.Equal(500m, result.StartupBudget.FundingRequired.CompleteTotalSek);
    }

    [Fact]
    public void Unused_purchase_cash_does_not_fund_startup_and_no_loan_creates_no_fees()
    {
        var result = CostExamples.Calculate(CostExamples.Car(price: 25000) with { CustomCosts = CostExamples.Category("start", 500, HouseholdCostCadence.Once, 0) },
            PaymentExamples.Profile() with { PurchaseCashSek = 30000, StartupBudgetSek = 0 });
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.StartupBudget.Status);
        Assert.Equal(500m, result.StartupBudget.FundingRequired.CompleteTotalSek);
        Assert.DoesNotContain(result.Payments.Sources, source => source.Category == "loanFees");
    }

    [Fact]
    public void Known_subtotal_can_exceed_a_limit_while_unknown_timing_prevents_other_passes()
    {
        var car = PaymentExamples.Car() with
        {
            Insurance = CostExamples.Category("insurance", 500, HouseholdCostCadence.Monthly),
            Repairs = CostExamples.Category("undated", 100, HouseholdCostCadence.Once),
        };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile() with { MonthlyBudgetSek = 400, StartupBudgetSek = 1000 });
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.MonthlyBudget.Status);
        Assert.Null(result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(500m, result.MonthlyBudget.FundingRequired.KnownSubtotalSek);
        Assert.Equal(HouseholdBudgetStatus.Unknown, result.StartupBudget.Status);
    }

    [Fact]
    public void Known_loan_fees_survive_a_missing_interest_rate()
    {
        var result = CostExamples.Calculate(CostExamples.Car(price: 10000), PaymentExamples.Profile() with
        {
            PurchaseCashSek = 0, MonthlyBudgetSek = 20, StartupBudgetSek = 500,
            LoanTerms = new() { TermMonths = 6, MonthlyFeeSek = 50, SetupFeeSek = 500 },
        });
        Assert.Equal(800m, result.Payments.ExternalOutflow.KnownSubtotalSek);
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.MonthlyBudget.Status);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.StartupBudget.Status);
        Assert.Equal(0m, result.Payments.Months[7].Outflow.CompleteTotalSek);
    }

    [Fact]
    public void Budget_comparison_and_cash_aggregation_precede_display_rounding()
    {
        var car = PaymentExamples.Car() with
        {
            Tax = CostExamples.Category("tax", 0.004m, HouseholdCostCadence.Monthly),
            Insurance = CostExamples.Category("insurance", 0.004m, HouseholdCostCadence.Monthly),
        };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile(1) with { MonthlyBudgetSek = 0.007m });
        Assert.Equal(0.01m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(0.01m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.MonthlyBudget.Status);
    }

    [Fact]
    public void Invalid_budget_limits_are_local_and_absent_limits_are_not_configured()
    {
        var result = CostExamples.Calculate(PaymentExamples.Car(), PaymentExamples.Profile() with { StartupBudgetSek = -1 });
        Assert.Equal(HouseholdBudgetStatus.Invalid, result.StartupBudget.Status);
        Assert.Equal(HouseholdBudgetStatus.NotConfigured, result.MonthlyBudget.Status);
        Assert.Equal(0m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(0m, result.Totals.OwnershipCost.CompleteTotalSek);
    }
}

internal static class PaymentExamples
{
    public static HouseholdProfileInput Profile(int months = 12) => CostExamples.Profile(months) with { StartMonth = new(2026, 1) };
    public static VehicleCostInput Car() => CostExamples.Car(price: 0);
    public static HouseholdCostCategoryInput AnnualTax(int? due) => HouseholdCostCategoryInput.FromItems([
        new("tax", "Annual tax", CostExamples.Value(1200), HouseholdCostCadence.Annual, DueMonthOfYear: due),
    ]);
    public static HouseholdLeaseInput Lease(int term = 24) => new(Enumerable.Range(1, term).Select(month => new HouseholdLeasePayment(month, 2000)), [], [])
    {
        TermMonths = term, UpfrontNonRefundableSek = 6000, RefundableDepositSek = 3000,
        DepositRefundSek = CostExamples.Value(3000), IncludedDistanceKilometres = 24000,
        ExcessDistancePricePerKilometreSek = CostExamples.Value(1), PriceBasis = LeasePriceBasis.Quoted, EnergyIncluded = true,
    };
    public static VehicleCostInput LeaseCar(HouseholdLeaseInput? lease = null) => Car() with
    {
        AcquisitionType = AcquisitionType.Lease, PriceSek = null, Residual = null, Lease = lease ?? Lease(),
    };
}
