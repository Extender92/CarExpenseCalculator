using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdLeaseCalculatorTests
{
    [Fact]
    public void A9_calculates_full_lease_cost_payments_deposit_and_budget()
    {
        var result = Calculate(PaymentExamples.Lease(), 24);
        Assert.Equal(60_000m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(2500m, result.Totals.MonthlyCost.CompleteTotalSek);
        Assert.Equal(20m, result.Totals.CostPerMil.CompleteTotalSek);
        Assert.Equal(6000m, result.Lease.ExcessDistanceKilometres);
        Assert.Equal(63_000m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(3000m, result.Payments.ExternalInflow.CompleteTotalSek);
        Assert.Equal(60_000m, result.Payments.NetExternalCashFlow.CompleteTotalSek);
        Assert.Equal(60_000m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Equal(9000m, result.StartupBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(2250m, result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(8000m, result.Payments.Months[24].Outflow.CompleteTotalSek);
        Assert.Equal(3000m, result.Payments.Months[24].Inflow.CompleteTotalSek);
        Assert.Null(result.FinancingDetails);
        Assert.Equal(CostSectionState.NotApplicable, result.Financing.State);
        Assert.Equal(CostSectionState.NotApplicable, result.Depreciation.Cost.State);
        Assert.Equal(CostSectionState.NotApplicable, result.Totals.EndEquity.State);
    }

    [Theory]
    [InlineData(12, 33000, 0, 30000, HouseholdBudgetStatus.WithinLimit)]
    [InlineData(36, 63000, 3000, 60000, HouseholdBudgetStatus.Unknown)]
    public void A10_preserves_covered_payments_without_a_comparable_total(int months, int outflow, int refund, int partialCost, HouseholdBudgetStatus budget)
    {
        var result = Calculate(PaymentExamples.Lease(), months, monthlyLimit: 3000);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal((decimal)partialCost, result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Contains(result.Lease.Cost.Errors, error => error.Code == "leaseHorizonMismatch");
        Assert.Equal((decimal)outflow, result.Payments.ExternalOutflow.KnownSubtotalSek);
        Assert.Equal((decimal)refund, result.Payments.ExternalInflow.CompleteTotalSek);
        Assert.Equal(Math.Min(months, 24) + 1, result.Payments.Months.Count);
        Assert.Equal(budget, result.MonthlyBudget.Status);
        Assert.Null(result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(0, 63000)]
    [InlineData(2500, 60500)]
    [InlineData(3000, 60000)]
    public void Only_the_withheld_deposit_becomes_cost_without_a_second_payment(int refund, int expectedCost)
    {
        var result = Calculate(PaymentExamples.Lease() with { DepositRefundSek = CostExamples.Value(refund) }, 24);
        Assert.Equal((decimal)expectedCost, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal((decimal)expectedCost, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Equal(3000m - refund, result.Lease.DepositWithheld.CompleteTotalSek);
        Assert.Equal(63000m, result.Payments.ExternalOutflow.CompleteTotalSek);
    }

    [Fact]
    public void Unknown_refund_blocks_cost_but_not_known_expenditure_budgets()
    {
        var result = Calculate(PaymentExamples.Lease() with { DepositRefundSek = null }, 24, 2250);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(60000m, result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Equal(0m, result.Lease.DepositWithheld.KnownSubtotalSek);
        Assert.Equal(63000m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Null(result.Payments.NetExternalCashFlow.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.MonthlyBudget.Status);
        Assert.Equal(9000m, result.StartupBudget.FundingRequired.CompleteTotalSek);
    }

    [Fact]
    public void No_deposit_needs_no_unused_refund_assumption()
    {
        var result = Calculate(PaymentExamples.Lease() with { RefundableDepositSek = 0, DepositRefundSek = null }, 24);
        Assert.Equal(60000m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(0m, result.Payments.ExternalInflow.CompleteTotalSek);
    }

    [Fact]
    public void Included_services_and_explicit_extras_are_distinct_and_counted_once()
    {
        var car = PaymentExamples.LeaseCar() with
        {
            Insurance = HouseholdCostCategoryInput.Included(),
            Service = HouseholdCostCategoryInput.Included([new("extra", "Excluded service", CostExamples.Value(600), HouseholdCostCadence.Once, 4)]),
        };
        var result = CostExamples.Calculate(car, Profile(24));
        Assert.True(result.Service.IsIncluded);
        Assert.True(result.Insurance.IsIncluded);
        Assert.True(result.Energy.IsIncluded);
        Assert.Equal(600m, result.Service.Cost.CompleteTotalSek);
        Assert.Equal(60600m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(63600m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(60600m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Long_horizon_does_not_extend_operating_costs_energy_or_reserve_after_the_contract()
    {
        var lease = PaymentExamples.Lease() with { EnergyIncluded = false };
        var car = PaymentExamples.LeaseCar(lease) with
        {
            Insurance = CostExamples.Category("insurance", 100, HouseholdCostCadence.Monthly),
            AdditionalRepairAllowancePerMonthSek = CostExamples.Value(50),
        };
        // Keep the immutable source collection through the lease factory.
        car = VehicleCostInput.ForLease(car.CandidateKey, lease, [CostExamples.Petrol()]) with
        {
            Tax = car.Tax, Insurance = car.Insurance, Service = car.Service, Repairs = car.Repairs,
            CustomCosts = car.CustomCosts, AdditionalRepairAllowancePerMonthSek = car.AdditionalRepairAllowancePerMonthSek,
        };
        var result = CostExamples.Calculate(car, Profile(36) with { MonthlyBudgetSek = 10000 });
        Assert.Equal(2400m, result.Insurance.Cost.CompleteTotalSek);
        Assert.Equal(1200m, result.RepairAllowance.CompleteTotalSek);
        Assert.Equal(36000m, result.Energy.Cost.CompleteTotalSek);
        Assert.Equal(1200m, result.Payments.InternalSaving.KnownSubtotalSek);
        Assert.Equal(25, result.Payments.Months.Count);
        Assert.Equal(45000m, result.Totals.DistanceKilometres);
        Assert.Equal(HouseholdBudgetStatus.Unknown, result.MonthlyBudget.Status);
        Assert.Contains(result.MonthlyBudget.FundingRequired.MissingComponents, value => value.EndsWith("uncoveredMonths", StringComparison.Ordinal));
    }

    [Fact]
    public void Unresolved_price_terms_preserve_quotes_but_prevent_cost_and_budget_passes()
    {
        var result = Calculate(PaymentExamples.Lease() with { PriceBasis = LeasePriceBasis.Unresolved }, 24, 10000);
        Assert.Equal(60000m, result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Unknown, result.MonthlyBudget.Status);
        Assert.Contains(result.Lease.Cost.MissingComponents, value => value.EndsWith("priceBasis", StringComparison.Ordinal));
        var estimated = Calculate(PaymentExamples.Lease() with { PriceBasis = LeasePriceBasis.Estimated }, 24);
        Assert.True(estimated.Lease.IsEstimate);
        Assert.Equal(60000m, estimated.Totals.OwnershipCost.CompleteTotalSek);
        Assert.All(estimated.Payments.Sources.Where(source => source.Category == "leasePayments"), source => Assert.True(source.IsEstimate));
    }

    [Fact]
    public void Absent_payment_month_is_unknown_while_an_explicit_zero_is_complete()
    {
        var original = PaymentExamples.Lease();
        HouseholdLeaseInput Change(IEnumerable<HouseholdLeasePayment> payments) => new(payments, [], [])
        {
            TermMonths = original.TermMonths, UpfrontNonRefundableSek = original.UpfrontNonRefundableSek,
            RefundableDepositSek = original.RefundableDepositSek, DepositRefundSek = original.DepositRefundSek,
            IncludedDistanceKilometres = original.IncludedDistanceKilometres, ExcessDistancePricePerKilometreSek = original.ExcessDistancePricePerKilometreSek,
            PriceBasis = original.PriceBasis, EnergyIncluded = true,
        };
        var missing = Calculate(Change(original.MonthlyPayments!.Where(payment => payment.MonthOffset != 4)), 24);
        Assert.Null(missing.Lease.Cost.CompleteTotalSek);
        Assert.Equal(61000m, missing.Payments.ExternalOutflow.KnownSubtotalSek);
        Assert.Null(missing.Payments.Months[4].Outflow.CompleteTotalSek);
        var zero = Calculate(Change(original.MonthlyPayments!.Select(payment => payment.MonthOffset == 4 ? payment with { AmountSek = 0 } : payment)), 24);
        Assert.Equal(58000m, zero.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(61000m, zero.Payments.ExternalOutflow.CompleteTotalSek);
    }

    [Fact]
    public void Zero_excess_distance_does_not_need_an_unused_rate()
    {
        var result = Calculate(PaymentExamples.Lease() with { IncludedDistanceKilometres = 30000, ExcessDistancePricePerKilometreSek = null }, 24);
        Assert.Equal(0m, result.Lease.ExcessDistanceKilometres);
        Assert.Equal(54000m, result.Totals.OwnershipCost.CompleteTotalSek);
        var unknown = Calculate(PaymentExamples.Lease() with { ExcessDistancePricePerKilometreSek = null }, 24);
        Assert.Equal(6000m, unknown.Lease.ExcessDistanceKilometres);
        Assert.Null(unknown.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void End_fees_and_dated_extras_are_scheduled_once_and_sensitivity_is_shared()
    {
        var lease = new HouseholdLeaseInput([new(1, 100), new(2, 100)],
            [new("end", "End fee", SensitivityValue.Scenarios(10, 20, 30))],
            [new("extra", "Startup extra", CostExamples.Value(50), 0)])
        {
            TermMonths = 2, UpfrontNonRefundableSek = 0, RefundableDepositSek = 0,
            IncludedDistanceKilometres = 10000, PriceBasis = LeasePriceBasis.Quoted, EnergyIncluded = true,
        };
        var profile = Profile(2) with { ActiveSensitivityMode = SensitivityMode.Cautious };
        var cars = new[] { PaymentExamples.LeaseCar(lease), PaymentExamples.LeaseCar(lease) with { CandidateKey = "second" } };
        var results = new HouseholdCostCalculator().Calculate(profile, cars);
        Assert.All(results.Vehicles, result =>
        {
            Assert.Equal(280m, result.Totals.OwnershipCost.CompleteTotalSek);
            Assert.Equal(50m, result.StartupBudget.FundingRequired.CompleteTotalSek);
            Assert.Equal(130m, result.Payments.Months[2].Outflow.CompleteTotalSek);
            Assert.Equal(280m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        });
    }

    [Fact]
    public void Purchase_assumptions_do_not_block_a_lease_or_invent_purchase_results()
    {
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(), Profile(24) with { PurchaseCashSek = null, LoanTerms = null });
        Assert.Equal(60000m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.DoesNotContain(result.Payments.Sources, source => source.Category is "purchaseCash" or "principal" or "interest" or "loanFees");
    }

    [Fact]
    public void Missing_lease_and_invalid_car_values_preserve_other_candidates()
    {
        var cars = new[]
        {
            PaymentExamples.LeaseCar() with { Lease = null },
            PaymentExamples.LeaseCar(PaymentExamples.Lease() with { UpfrontNonRefundableSek = -1 }) with { CandidateKey = "invalid" },
            CostExamples.Car("purchase", 0, [CostExamples.Petrol()]),
        };
        var result = new HouseholdCostCalculator().Calculate(Profile(24), cars);
        Assert.Null(result.Vehicles[0].Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(CostSectionState.Invalid, result.Vehicles[1].Lease.Cost.State);
        Assert.Equal(54000m, result.Vehicles[1].Lease.Cost.KnownSubtotalSek);
        Assert.Equal(36000m, result.Vehicles[2].Totals.OwnershipCost.CompleteTotalSek);
    }

    private static HouseholdProfileInput Profile(int months) => PaymentExamples.Profile(months) with { AnnualDistanceKilometres = 15000 };
    private static VehicleCostResult Calculate(HouseholdLeaseInput lease, int months, decimal? monthlyLimit = null) =>
        CostExamples.Calculate(PaymentExamples.LeaseCar(lease), Profile(months) with { MonthlyBudgetSek = monthlyLimit });
}
