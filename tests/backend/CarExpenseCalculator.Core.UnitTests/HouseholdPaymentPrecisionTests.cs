using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdPaymentPrecisionTests
{
    [Fact]
    public void A_positive_unrepresentable_average_cannot_pass_a_zero_budget()
    {
        var car = PaymentExamples.Car() with { CustomCosts = CostExamples.Category("tiny", 0.0000000000000000000000000001m, HouseholdCostCadence.Once, 1) };
        var result = CostExamples.Calculate(car, PaymentExamples.Profile(120) with { MonthlyBudgetSek = 0 });
        Assert.Equal(HouseholdBudgetStatus.Invalid, result.MonthlyBudget.Status);
        Assert.Contains(result.MonthlyBudget.FundingRequired.Errors, error => error.Code == "calculationOutOfRange");
        Assert.NotNull(result.Payments.ExternalOutflow.CompleteTotalSek);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Positive_lease_distance_or_excess_charge_below_precision_remains_an_error(bool tinyDistance)
    {
        var lease = PaymentExamples.Lease(1) with
        {
            IncludedDistanceKilometres = 0,
            ExcessDistancePricePerKilometreSek = CostExamples.Value(tinyDistance ? 1m : 0.0000000000000000000000000001m),
        };
        var result = CostExamples.Calculate(PaymentExamples.LeaseCar(lease), PaymentExamples.Profile(1) with
        {
            AnnualDistanceKilometres = tinyDistance ? 0.0000000000000000000000000001m : 1m,
        });
        Assert.Contains(result.Lease.Cost.Errors, error => error.Code == "calculationOutOfRange");
        Assert.Null(result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(9000m, result.StartupBudget.FundingRequired.CompleteTotalSek);
    }

    [Fact]
    public void Monthly_energy_uses_unrounded_totals_and_identifies_estimates()
    {
        var car = CostExamples.Car(price: 0, sources: [CostExamples.Petrol()]);
        var profile = PaymentExamples.Profile(3) with { AnnualDistanceKilometres = 1 };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(0.30m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.All(result.Payments.Months.Skip(1), month => Assert.Equal(0.10m, month.Outflow.CompleteTotalSek));
        Assert.Equal(0.30m, result.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.True(Assert.Single(result.Payments.Sources, source => source.Category == "energy").IsEstimate);
    }

    [Fact]
    public void Tiny_energy_distribution_conserves_the_period_amount_before_rounding()
    {
        var result = CostExamples.Calculate(CostExamples.Car(price: 0, sources: [CostExamples.Petrol()]),
            PaymentExamples.Profile(3) with { AnnualDistanceKilometres = 0.02m, MonthlyBudgetSek = 0.002m });
        Assert.All(result.Payments.Months, month => Assert.Equal(0m, month.Outflow.CompleteTotalSek));
        Assert.Equal(0.01m, result.Payments.ExternalOutflow.CompleteTotalSek);
        Assert.Equal(0.01m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.WithinLimit, result.MonthlyBudget.Status);
    }

    [Fact]
    public void Period_overflow_preserves_representable_months_and_the_monthly_budget()
    {
        var profile = PaymentExamples.Profile(120) with
        {
            AnnualDistanceKilometres = 1_000_000, ChargingLossPercent = CostExamples.Value(99.9999999999999m),
            HomeChargingSharePercent = CostExamples.Value(100), HomeChargingPricePerKilowattHourSek = CostExamples.Value(100_000),
            MonthlyBudgetSek = 100_000_000,
        };
        var source = CostExamples.Electricity() with { ConsumptionPer100Kilometres = CostExamples.Value(6000) };
        var result = CostExamples.Calculate(CostExamples.Car(price: 0, sources: [source, source with { Key = "second" }]), profile);
        Assert.Null(result.Payments.ExternalOutflow.KnownSubtotalSek);
        Assert.Contains(result.Payments.ExternalOutflow.Errors, error => error.Code == "calculationOutOfRange");
        Assert.All(result.Payments.Months.Skip(1), month => Assert.Equal(1_000_000_000_000_000_000_000_000_000m, month.Outflow.CompleteTotalSek));
        Assert.Equal(1_000_000_000_000_000_000_000_000_000m, result.MonthlyBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.MonthlyBudget.Status);
    }

    [Fact]
    public void Included_energy_preserves_known_quantities_without_charging_twice()
    {
        var original = PaymentExamples.LeaseCar();
        var car = VehicleCostInput.ForLease("lease", original.Lease, [CostExamples.Petrol()]) with
        {
            Tax = original.Tax, Insurance = original.Insurance, Service = original.Service, Repairs = original.Repairs,
            CustomCosts = original.CustomCosts, AdditionalRepairAllowancePerMonthSek = original.AdditionalRepairAllowancePerMonthSek,
        };
        var profile = new HouseholdProfileInput { PeriodMonths = 24, AnnualDistanceKilometres = 15000, StartMonth = new(2026, 1) };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(1800m, Assert.Single(result.Energy.Sources).PurchasedQuantity);
        Assert.Equal(0m, result.Energy.Cost.CompleteTotalSek);
        Assert.Equal(0m, Assert.Single(result.Energy.Sources).Cost.CompleteTotalSek);
        Assert.Equal(60000m, result.Totals.OwnershipCost.CompleteTotalSek);
    }
}
