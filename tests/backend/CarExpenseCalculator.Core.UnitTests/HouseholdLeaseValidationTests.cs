using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdLeaseValidationTests
{
    [Theory]
    [InlineData("mixedPurchase")]
    [InlineData("mixedLease")]
    [InlineData("enum")]
    [InlineData("pricingEnum")]
    [InlineData("duplicateMonth")]
    [InlineData("nullPayment")]
    [InlineData("tooManyPayments")]
    [InlineData("tooManyCharges")]
    [InlineData("nullCharge")]
    [InlineData("duplicateCategoryKey")]
    [InlineData("duplicateLeaseKey")]
    [InlineData("endOffset")]
    [InlineData("badEvidence")]
    public void Malformed_lease_structures_are_rejected(string defect)
    {
        var lease = PaymentExamples.Lease();
        var car = PaymentExamples.LeaseCar(lease);
        car = defect switch
        {
            "mixedPurchase" => PaymentExamples.Car() with { Lease = lease },
            "mixedLease" => car with { PriceSek = 100 },
            "enum" => car with { AcquisitionType = (AcquisitionType)99 },
            "pricingEnum" => car with { Lease = lease with { PriceBasis = (LeasePriceBasis)99 } },
            "duplicateMonth" => car with { Lease = new([new(1, 10), new(1, 20)]) },
            "nullPayment" => car with { Lease = new([null!]) },
            "tooManyPayments" => car with { Lease = new(Enumerable.Range(1, 121).Select(month => new HouseholdLeasePayment(month, 1))) },
            "tooManyCharges" => car with { Lease = new(endFees: Enumerable.Range(1, 51).Select(index => new HouseholdLeaseCharge(index.ToString(), "Fee", CostExamples.Value(1)))) },
            "nullCharge" => car with { Lease = new(endFees: [null!]) },
            "duplicateCategoryKey" => car with { Service = CostExamples.Category("same", 1, HouseholdCostCadence.Monthly), Lease = new(endFees: [new(" same ", "Fee", CostExamples.Value(1))]) },
            "duplicateLeaseKey" => car with { Lease = new(endFees: [new("same", "Fee", CostExamples.Value(1))], otherPayments: [new("same", "Extra", CostExamples.Value(1), 1)]) },
            "endOffset" => car with { Lease = new(endFees: [new("end", "Fee", CostExamples.Value(1), 1)]) },
            _ => car with { Lease = new(otherPayments: [new("extra", "Fee", CostExamples.Value(1), 1, SourceUrl: "file:///private")]) },
        };
        Assert.Throws<HouseholdInputValidationException>(() => CostExamples.Calculate(car, PaymentExamples.Profile(24)));
    }

    [Theory]
    [InlineData("term")]
    [InlineData("upfront")]
    [InlineData("deposit")]
    [InlineData("refund")]
    [InlineData("distance")]
    [InlineData("rate")]
    [InlineData("inactiveRefund")]
    public void Supplied_numeric_errors_remain_local_and_are_never_clamped(string defect)
    {
        var lease = PaymentExamples.Lease();
        lease = defect switch
        {
            "term" => lease with { TermMonths = 121 },
            "upfront" => lease with { UpfrontNonRefundableSek = -1 },
            "deposit" => lease with { RefundableDepositSek = 100_000_001 },
            "refund" => lease with { DepositRefundSek = CostExamples.Value(3001) },
            "distance" => lease with { IncludedDistanceKilometres = 10_000_001 },
            "rate" => lease with { ExcessDistancePricePerKilometreSek = CostExamples.Value(100_001) },
            _ => lease with { DepositRefundSek = SensitivityValue.Scenarios(3000, 3000, -1) },
        };
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(24) with { AnnualDistanceKilometres = 15000 });
        Assert.Contains(result.InputErrors, error => error.Code == "outOfRange");
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.True(result.Payments.Months.Count <= 121);
    }

    [Fact]
    public void Invalid_payment_offsets_are_not_silently_dropped()
    {
        var lease = new HouseholdLeaseInput([new(1, 100), new(2, 200)], [], [])
        {
            TermMonths = 1, UpfrontNonRefundableSek = 0, RefundableDepositSek = 0,
            IncludedDistanceKilometres = 0, PriceBasis = LeasePriceBasis.Quoted, EnergyIncluded = true,
        };
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(1) with { MonthlyBudgetSek = 1000 });
        Assert.Equal(100m, result.Payments.ExternalOutflow.KnownSubtotalSek);
        Assert.Null(result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Invalid, result.MonthlyBudget.Status);
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Constructors_snapshot_collections_and_results_cannot_be_modified()
    {
        var payments = new List<HouseholdLeasePayment> { new(1, 10) };
        var end = new List<HouseholdLeaseCharge> { new("end", "Fee", CostExamples.Value(20)) };
        var extras = new List<HouseholdCostItem> { new("extra", "Extra", CostExamples.Value(30), HouseholdCostCadence.Once, 1) };
        var lease = new HouseholdLeaseInput(payments, end, []) { TermMonths = 1 };
        var category = HouseholdCostCategoryInput.Included(extras);
        payments.Clear(); end.Clear(); extras.Clear();
        Assert.Single(lease.MonthlyPayments!);
        Assert.Single(lease.EndFees!);
        Assert.Single(category.Items);
        Assert.Throws<NotSupportedException>(() => ((IList<HouseholdLeasePayment>)lease.MonthlyPayments!).Clear());
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(1));
        Assert.Throws<NotSupportedException>(() => ((IList<HouseholdPaymentMonth>)result.Payments.Months).Clear());
    }

    [Fact]
    public void Maximum_contract_and_charge_collections_produce_a_bounded_calendar()
    {
        var lease = new HouseholdLeaseInput(Enumerable.Range(1, 120).Select(month => new HouseholdLeasePayment(month, 1)),
            Enumerable.Range(1, 50).Select(index => new HouseholdLeaseCharge($"end{index}", "End", CostExamples.Value(1))),
            Enumerable.Range(1, 50).Select(index => new HouseholdLeaseCharge($"extra{index}", "Extra", CostExamples.Value(1), index)))
        {
            TermMonths = 120, UpfrontNonRefundableSek = 0, RefundableDepositSek = 0,
            IncludedDistanceKilometres = 10_000_000, PriceBasis = LeasePriceBasis.Quoted, EnergyIncluded = true,
        };
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(120));
        Assert.Equal(121, result.Payments.Months.Count);
        Assert.Equal(220m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(220m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Unknown_end_obligations_affect_only_periods_including_the_contract_end()
    {
        var lease = new HouseholdLeaseInput([new(1, 100), new(2, 100)], null, [])
        {
            TermMonths = 2, UpfrontNonRefundableSek = 0, RefundableDepositSek = 0,
            IncludedDistanceKilometres = 0, PriceBasis = LeasePriceBasis.Quoted, EnergyIncluded = true,
        };
        var beforeEnd = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(1) with { MonthlyBudgetSek = 100 });
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, beforeEnd.MonthlyBudget.Status);
        var atEnd = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(2) with { MonthlyBudgetSek = 100 });
        Assert.Null(atEnd.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Unknown, atEnd.MonthlyBudget.Status);
        Assert.Equal(0m, atEnd.StartupBudget.FundingRequired.CompleteTotalSek);
    }
}
