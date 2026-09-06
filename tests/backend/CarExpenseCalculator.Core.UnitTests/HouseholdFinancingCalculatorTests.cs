using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdFinancingCalculatorTests
{
    private readonly HouseholdFinancingCalculator _calculator = new();

    [Fact]
    public void Each_alternative_uses_the_full_shared_cash_budget_from_example_A1()
    {
        var profile = Profile() with { PurchaseCashSek = 30_000m };
        var result = _calculator.Calculate(profile,
            [new("below", 25_000m), new("equal", 30_000m), new("above", 80_000m)]);

        Assert.Equal("SEK", result.Currency);
        Assert.Equal(["below", "equal", "above"], result.Vehicles.Select(row => row.CandidateKey));
        Assert.Equal(new PurchaseCashAllocation(25_000m, 0m, 5_000m), result.Vehicles[0].Allocation);
        Assert.Equal(new PurchaseCashAllocation(30_000m, 0m, 0m), result.Vehicles[1].Allocation);
        Assert.Equal(new PurchaseCashAllocation(30_000m, 50_000m, 0m), result.Vehicles[2].Allocation);
        Assert.All(result.Vehicles, row => Assert.Equal(FinancingState.Complete, row.State));
    }

    [Fact]
    public void Financing_portion_of_A2_counts_setup_once_and_stops_fees_at_term()
    {
        var profile = Profile(0m, 10, 12) with
        {
            PurchaseCashSek = 30_000m,
            LoanTerms = Terms(0m, 10) with { SetupFeeSek = 500m, MonthlyFeeSek = 25m },
        };
        var result = Calculate(profile, 80_000m);

        Assert.Equal(FinancingState.Complete, result.State);
        Assert.Equal(5_000m, result.Loan!.MonthlyInstallmentSek);
        Assert.Equal(50_000m, result.Loan.PrincipalRepaidSek);
        Assert.Equal(50_000m, result.Loan.PaymentsDuringPeriodSek);
        Assert.Equal(0m, result.Loan.InterestPaidSek);
        Assert.Equal(0m, result.Loan.RemainingPrincipalSek);
        Assert.Equal(500m, result.SetupFeeSek);
        Assert.Equal(250m, result.MonthlyFeesDuringPeriodSek);
        Assert.Equal(750m, result.FinancingCostDuringPeriodSek);
        Assert.Equal(80_750m, result.AcquisitionCashOutflowSek);
        Assert.Equal(Enumerable.Range(1, 10), result.Loan.Installments.Select(row => row.MonthOffset));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25000, 30000)]
    [InlineData(30000, 30000)]
    public void Zero_principal_needs_no_horizon_or_loan_assumptions(int price, int cash)
    {
        var result = Calculate(new HouseholdProfileInput { PurchaseCashSek = cash }, price);

        Assert.Equal(FinancingState.Complete, result.State);
        Assert.Null(result.Loan);
        Assert.Equal(0m, result.SetupFeeSek);
        Assert.Equal(0m, result.MonthlyFeesDuringPeriodSek);
        Assert.Equal(0m, result.FinancingCostDuringPeriodSek);
        Assert.Equal((decimal)price, result.AcquisitionCashOutflowSek);
        Assert.Empty(result.MissingComponents);
    }

    [Fact]
    public void Irrelevant_invalid_loan_terms_are_reported_without_creating_a_loan_for_a_cash_purchase()
    {
        var profile = Profile() with { PurchaseCashSek = 30_000m, LoanTerms = Terms(101m, 0) };
        var preview = _calculator.Calculate(profile, [new("cash", 25_000m), new("borrow", 50_000m)]);

        Assert.Equal(2, preview.ProfileErrors.Count);
        Assert.Equal(FinancingState.Complete, preview.Vehicles[0].State);
        Assert.Equal(0m, preview.Vehicles[0].FinancingCostDuringPeriodSek);
        Assert.Equal(FinancingState.Invalid, preview.Vehicles[1].State);
        Assert.Null(preview.Vehicles[1].Loan);
    }

    [Fact]
    public void Positive_interest_matches_an_independent_high_precision_reference()
    {
        // Reference uses a 50-digit decimal closed-form annuity and balance.
        var result = Calculate(Profile(6m, 24, 12), 50_000m);
        var loan = result.Loan!;

        Near(2216.0305126378452369645486624m, loan.MonthlyInstallmentSek);
        Near(25747.907984331467924885066832m, loan.RemainingPrincipalSek);
        Near(2340.2741359856107684596507809m, loan.InterestPaidSek);
        Assert.Equal(12, loan.Installments.Count);
        Assert.Equal(loan.InterestPaidSek, result.FinancingCostDuringPeriodSek);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(0, 12, 5)]
    [InlineData(0, 12, 12)]
    [InlineData(0, 12, 120)]
    [InlineData(6, 24, 12)]
    [InlineData(6, 24, 24)]
    [InlineData(6, 24, 120)]
    [InlineData(100, 120, 120)]
    public void Installments_conserve_principal_and_payments_across_rates_and_horizons(int rate, int term, int period)
    {
        var profile = Profile(rate, term, period) with
        {
            LoanTerms = Terms(rate, term) with { SetupFeeSek = 123.456m, MonthlyFeeSek = 25.555m },
        };
        var result = Calculate(profile, 50_000m);
        var loan = result.Loan!;

        Assert.Equal(Math.Min(term, period), loan.Installments.Count);
        Near(50_000m, loan.PrincipalRepaidSek + loan.RemainingPrincipalSek);
        Near(loan.PrincipalRepaidSek, loan.Installments.Sum(row => row.PrincipalRepaidSek));
        Near(loan.PaymentsDuringPeriodSek, loan.PrincipalRepaidSek + loan.InterestPaidSek);
        Assert.Equal(25.555m * Math.Min(term, period), result.MonthlyFeesDuringPeriodSek);
        Assert.Equal(123.456m + result.MonthlyFeesDuringPeriodSek + loan.InterestPaidSek, result.FinancingCostDuringPeriodSek);
        Assert.All(loan.Installments, row =>
        {
            Assert.True(row.PrincipalRepaidSek >= 0m);
            Assert.True(row.InterestSek >= 0m);
            Assert.Equal(row.OpeningPrincipalSek - row.PrincipalRepaidSek, row.RemainingPrincipalSek);
            Assert.Equal(row.PrincipalRepaidSek + row.InterestSek, row.PaymentSek);
        });
        if (period >= term)
        {
            Assert.Equal(0m, loan.RemainingPrincipalSek);
            Assert.Equal(50_000m, loan.PrincipalRepaidSek);
        }
        else
        {
            Assert.True(loan.RemainingPrincipalSek > 0m);
        }
    }

    [Fact]
    public void Very_small_positive_rates_do_not_divide_by_a_cancelled_annuity_denominator()
    {
        var result = Calculate(Profile(0.00000000000000000001m, 12, 12), 12_000m);

        Assert.Equal(FinancingState.Complete, result.State);
        Assert.True(result.Loan!.InterestPaidSek > 0m);
        Near(1000m, result.Loan.MonthlyInstallmentSek);
        Assert.Equal(0m, result.Loan.RemainingPrincipalSek);
    }

    [Fact]
    public void Maximum_money_and_rate_remain_finite_with_unrounded_fee_totals()
    {
        var profile = Profile(100m, 120, 120) with
        {
            LoanTerms = Terms(100m, 120) with { SetupFeeSek = 100_000_000m, MonthlyFeeSek = 100_000_000m },
        };
        var result = Calculate(profile, 100_000_000m);

        Assert.Equal(FinancingState.Complete, result.State);
        Assert.Equal(100_000_000m, result.Loan!.PrincipalRepaidSek);
        Assert.Equal(0m, result.Loan.RemainingPrincipalSek);
        Assert.Equal(12_000_000_000m, result.MonthlyFeesDuringPeriodSek);
        Assert.True(result.FinancingCostDuringPeriodSek > 12_100_000_000m);
    }

    [Fact]
    public void Core_retains_fractional_money_for_later_cost_aggregation()
    {
        var profile = Profile(0m, 3, 3) with
        {
            LoanTerms = Terms(0m, 3) with { SetupFeeSek = 0.005m, MonthlyFeeSek = 0.005m },
        };
        var result = Calculate(profile, 100m);

        Assert.NotEqual(decimal.Round(result.Loan!.MonthlyInstallmentSek, 2), result.Loan.MonthlyInstallmentSek);
        Assert.Equal(100m, result.Loan.PrincipalRepaidSek);
        Assert.Equal(0.020m, result.FinancingCostDuringPeriodSek);
        Assert.Equal(100.020m, result.AcquisitionCashOutflowSek);
    }

    [Theory]
    [InlineData(SensitivityMode.Favorable, 2)]
    [InlineData(SensitivityMode.Baseline, 6)]
    [InlineData(SensitivityMode.Cautious, 10)]
    public void One_active_mode_applies_the_explicit_rate_to_every_candidate(SensitivityMode mode, int expectedRate)
    {
        var profile = Profile() with
        {
            ActiveSensitivityMode = mode,
            LoanTerms = Terms(0m, 12) with { AnnualNominalInterestRatePercent = SensitivityValue.Scenarios(2m, 6m, 10m) },
        };
        var preview = _calculator.Calculate(profile, [new("a", 20_000m), new("b", 40_000m)]);

        Assert.Equal(mode, preview.ActiveSensitivityMode);
        Assert.All(preview.Vehicles, row => Assert.Equal((decimal)expectedRate, row.Loan!.AnnualNominalInterestRatePercent));
        Near(preview.Vehicles[0].Loan!.MonthlyInstallmentSek * 2m, preview.Vehicles[1].Loan!.MonthlyInstallmentSek);
        Assert.Equal(6m, profile.LoanTerms.AnnualNominalInterestRatePercent.Baseline);
    }

    [Fact]
    public void Invalid_inactive_rate_is_reported_without_silently_discarding_it()
    {
        var profile = Profile() with
        {
            LoanTerms = Terms(0m, 12) with { AnnualNominalInterestRatePercent = SensitivityValue.Scenarios(0m, 5m, 101m) },
        };
        var result = Calculate(profile, 20_000m);

        Assert.Equal(FinancingState.Invalid, result.State);
        Assert.Null(result.Loan);
        Assert.Contains(result.Errors, error => error.Path.EndsWith("annualNominalInterestRatePercent.cautious", StringComparison.Ordinal));
        Assert.Equal(20_000m, result.Allocation!.PrincipalSek);
    }

    [Fact]
    public void Missing_price_or_cash_remains_missing_and_does_not_guess_a_loan()
    {
        var missingPrice = Calculate(Profile(), null);
        var missingCash = Calculate(Profile() with { PurchaseCashSek = null }, 20_000m);

        Assert.Equal(FinancingState.Unavailable, missingPrice.State);
        Assert.Equal(["vehicles[0].priceSek"], missingPrice.MissingComponents);
        Assert.Null(missingPrice.Allocation);
        Assert.Equal(FinancingState.Unavailable, missingCash.State);
        Assert.Equal(["profile.purchaseCashSek"], missingCash.MissingComponents);
        Assert.Null(missingCash.AcquisitionCashOutflowSek);
    }

    [Fact]
    public void Missing_loan_terms_preserve_known_allocation_and_list_required_components()
    {
        var result = Calculate(new HouseholdProfileInput { PurchaseCashSek = 10_000m }, 30_000m);

        Assert.Equal(FinancingState.Partial, result.State);
        Assert.Equal(new PurchaseCashAllocation(10_000m, 20_000m, 0m), result.Allocation);
        Assert.Null(result.Loan);
        Assert.Null(result.SetupFeeSek);
        Assert.Null(result.MonthlyFeesDuringPeriodSek);
        Assert.Null(result.FinancingCostDuringPeriodSek);
        Assert.Equal(5, result.MissingComponents.Count);
        Assert.Contains("profile.loanTerms.annualNominalInterestRatePercent", result.MissingComponents);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_fee_does_not_hide_loan_installments_but_prevents_a_complete_total()
    {
        var result = Calculate(Profile() with { LoanTerms = Terms(6m, 12) with { MonthlyFeeSek = null } }, 20_000m);

        Assert.Equal(FinancingState.Partial, result.State);
        Assert.NotNull(result.Loan);
        Assert.Equal(0m, result.SetupFeeSek);
        Assert.Null(result.MonthlyFeesDuringPeriodSek);
        Assert.Null(result.FinancingCostDuringPeriodSek);
        Assert.Null(result.AcquisitionCashOutflowSek);
        Assert.Equal(["profile.loanTerms.monthlyFeeSek"], result.MissingComponents);
    }

    [Fact]
    public void Unrelated_missing_or_invalid_profile_values_do_not_disable_financing()
    {
        var profile = Profile() with
        {
            AnnualDistanceKilometres = -1m,
            StartMonth = new CalendarMonth(0, 13),
            HomeChargingPricePerKilowattHourSek = SensitivityValue.Constant(-5m),
            StartupBudgetSek = null,
        };
        var result = _calculator.Calculate(profile, [new("a", 20_000m)]);

        Assert.Equal(3, result.ProfileErrors.Count);
        Assert.Equal(FinancingState.Complete, result.Vehicles[0].State);
        Assert.Empty(result.Vehicles[0].Errors);
    }

    [Fact]
    public void Invalid_price_affects_only_its_own_candidate()
    {
        var result = _calculator.Calculate(Profile(), [new("invalid", -1m), new("known", 20_000m), new("missing", null)]);

        Assert.Equal(FinancingState.Invalid, result.Vehicles[0].State);
        Assert.Equal("vehicles[0].priceSek", Assert.Single(result.Vehicles[0].Errors).Path);
        Assert.Equal(FinancingState.Complete, result.Vehicles[1].State);
        Assert.Equal(FinancingState.Unavailable, result.Vehicles[2].State);
    }

    [Fact]
    public void Duplicate_or_missing_keys_and_null_candidates_are_structural_errors()
    {
        var duplicate = Assert.Throws<HouseholdInputValidationException>(() =>
            _calculator.Calculate(Profile(), [new("same", 10m), new(" same ", 20m)]));
        Assert.Contains(duplicate.Errors, error => error.Code == "duplicateKey");
        Assert.Throws<HouseholdInputValidationException>(() => _calculator.Calculate(Profile(), [new(" ", 10m)]));
        Assert.Throws<HouseholdInputValidationException>(() => _calculator.Calculate(Profile(), [null!]));
        Assert.Throws<HouseholdInputValidationException>(() =>
            _calculator.Calculate(Profile() with { ActiveSensitivityMode = (SensitivityMode)99 }, []));
    }

    [Fact]
    public void Candidate_limit_is_enforced_and_an_empty_profile_preview_is_allowed()
    {
        var maximum = Enumerable.Range(1, 100).Select(index => new VehiclePurchaseInput(index.ToString(), 1m)).ToArray();
        Assert.Equal(100, _calculator.Calculate(Profile(), maximum).Vehicles.Count);
        Assert.Throws<HouseholdInputValidationException>(() =>
            _calculator.Calculate(Profile(), [.. maximum, new("extra", 1m)]));
        Assert.Empty(_calculator.Calculate(new HouseholdProfileInput(), []).Vehicles);
    }

    private PurchaseFinancingResult Calculate(HouseholdProfileInput profile, decimal? price) =>
        Assert.Single(_calculator.Calculate(profile, [new("car", price)]).Vehicles);

    private static HouseholdProfileInput Profile(decimal rate = 0m, int term = 12, int period = 12) => new()
    {
        PurchaseCashSek = 0m,
        PeriodMonths = period,
        LoanTerms = Terms(rate, term),
    };

    private static HouseholdLoanTerms Terms(decimal rate, int term) => new()
    {
        AnnualNominalInterestRatePercent = SensitivityValue.Constant(rate),
        TermMonths = term,
        SetupFeeSek = 0m,
        MonthlyFeeSek = 0m,
    };

    private static void Near(decimal expected, decimal actual) =>
        Assert.InRange(Math.Abs(expected - actual), 0m, 0.000000000000000001m);
}
