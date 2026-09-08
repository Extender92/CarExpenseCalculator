using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ComparisonCostEvidenceTests
{
    [Fact]
    public void Effective_price_uses_35000_without_inheriting_40000_confirmation()
    {
        var old = Examples.Cost(101, 0) with { PriceSek = 40_000 };
        var changed = old with { PriceSek = 35_000 };
        var candidate = Examples.Candidate(facts: Examples.B1Facts(), cost: changed,
            confirmation: CostAssumptionConfirmation.Confirm(old, Examples.ConfirmedAt));
        var rule = new RuleProfileInput([new("purchasePriceSek", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, maximum: 36_000)], [Examples.PricePreference()]);
        var result = Examples.One(candidate, rule);
        Assert.Equal(35_000, result.HardRules[0].Assessment.Actual!.Number);
        Assert.True(result.HardRules[0].ObservedConditionSatisfied);
        Assert.Equal(HardRuleState.NeedsVerification, result.HardRules[0].State);
        Assert.Equal(new ScoreRange(0, 100), result.Score);
        Assert.Contains("effectivePriceNeedsConfirmation", result.Contributions[0].Assessment.Reasons);
        Assert.Equal(40_000, result.EffectiveInput.Facts.PurchasePriceSek!.Observations[0].Value);
        var confirmed = Examples.One(Examples.Candidate(facts: Examples.B1Facts(), cost: changed,
            confirmation: CostAssumptionConfirmation.Confirm(changed, Examples.ConfirmedAt.AddMinutes(1))), rule);
        Assert.Equal(HardRuleState.Pass, confirmed.HardRules[0].State);
        Assert.Equal(new ScoreRange(81.25m, 81.25m), confirmed.Score);
    }

    [Fact]
    public void Missing_effective_price_does_not_fall_back_to_known_advertisement()
    {
        var result = Examples.One(Examples.Candidate(facts: Examples.B1Facts(), cost: Examples.Cost(101, 0) with { PriceSek = null }), Examples.Rules());
        Assert.Null(result.Contributions[0].Assessment.Actual);
        Assert.Equal(new ScoreRange(40, 100), result.Score);
        Assert.Null(result.Cost.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Equal_reconstructed_inputs_keep_confirmation_but_changed_nested_values_do_not()
    {
        var original = Examples.Cost(101, 25);
        var confirmation = CostAssumptionConfirmation.Confirm(original, Examples.ConfirmedAt);
        var preference = new RuleProfileInput(preferences: [new("netCostSek", 1, EvidenceRequirement.UserConfirmed, 100, 0)]);
        var reconstructed = Examples.One(Examples.Candidate(cost: Examples.Cost(101, 25), confirmation: confirmation), preference);
        Assert.Equal(new ScoreRange(75, 75), reconstructed.Score);
        var changed = Examples.One(Examples.Candidate(cost: Examples.Cost(101, 50), confirmation: confirmation), preference);
        Assert.Equal(new ScoreRange(0, 100), changed.Score);
        Assert.Null(changed.EffectiveInput.CostConfirmation);
        Assert.Equal(50, changed.Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(Examples.ConfirmedAt, Assert.Single(reconstructed.Contributions[0].Assessment.Evidence).ConfirmedAt);
    }

    [Theory]
    [InlineData(EvidenceRequirement.Advertised, 75)]
    [InlineData(EvidenceRequirement.UserConfirmed, 0)]
    [InlineData(EvidenceRequirement.RegistryVerified, 0)]
    public void Calculated_estimates_do_not_automatically_become_confirmed_or_registry_facts(EvidenceRequirement evidence, int lower)
    {
        var result = Examples.One(Examples.Candidate(cost: Examples.Cost(101, 25)),
            new(preferences: [new("netCostSek", 1, evidence, 100, 0)]));
        Assert.Equal(lower, result.Score!.Lower);
        Assert.Equal(evidence == EvidenceRequirement.Advertised ? 75 : 100, result.Score.Upper);
    }

    [Fact]
    public void Profile_edits_recalculate_confirmed_car_assumptions_without_claiming_registry_evidence()
    {
        var cost = Examples.Cost(101, 0) with { CustomCosts = CostExamples.Category("monthly", 10, HouseholdCostCadence.Monthly) };
        var candidate = Examples.Candidate(cost: cost, confirmation: CostAssumptionConfirmation.Confirm(cost, Examples.ConfirmedAt));
        var rules = new RuleProfileInput(preferences: [new("netCostSek", 1, EvidenceRequirement.UserConfirmed, 1000, 0)]);
        var first = new ComparisonEvaluator().EvaluateComparison(Examples.Profile(12), rules, Examples.Date, [candidate]);
        var second = new ComparisonEvaluator().EvaluateComparison(Examples.Profile(24), rules, Examples.Date, [candidate]);
        Assert.Equal(120, first.Candidates[0].Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(240, second.Candidates[0].Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(100, second.Candidates[0].CoveragePercent);
        Assert.Equal(VerificationStatus.UserConfirmed, Assert.Single(second.Candidates[0].Contributions[0].Assessment.Evidence).Verification);
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(6, false)]
    [InlineData(24, false)]
    public void Lease_horizon_controls_cost_scoring_and_purchase_price_is_not_applicable(int months, bool complete)
    {
        var lease = new HouseholdLeaseInput(Enumerable.Range(1, 12).Select(x => new HouseholdLeasePayment(x, 100)), [], [])
        {
            TermMonths = 12, PriceBasis = LeasePriceBasis.Quoted, UpfrontNonRefundableSek = 0,
            RefundableDepositSek = 0, DepositRefundSek = CostExamples.Value(0), IncludedDistanceKilometres = 0,
            EnergyIncluded = true,
        };
        var cost = Examples.Cost(101, 0) with { AcquisitionType = AcquisitionType.Lease, PriceSek = null, Residual = null, Lease = lease };
        var rules = new RuleProfileInput(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 2400, 0), Examples.PricePreference(1)]);
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile(months), rules, Examples.Date, [Examples.Candidate(cost: cost)]).Candidates);
        Assert.Equal(complete, result.Contributions[0].Assessment.HasAdequateEvidence);
        Assert.Contains("notApplicable", result.Contributions[1].Assessment.Reasons);
        Assert.Equal(complete ? 50 : 0, result.CoveragePercent);
        Assert.Equal(complete, result.IsCheapestEligibleComplete);
    }

    [Fact]
    public void Fixed_residual_mismatch_zero_distance_and_invalid_profile_only_block_dependent_criteria()
    {
        var cost = Examples.Cost(101, 100) with { Residual = HouseholdResidualInput.FixedAmount(CostExamples.Value(90_000), 12) };
        var candidate = Examples.Candidate(facts: Examples.Tow(true), cost: cost);
        var rules = new RuleProfileInput([Examples.TowRule()], [new("netCostSek", 1, EvidenceRequirement.Advertised, 20_000, 0), new("costPerMilSek", 1, EvidenceRequirement.Advertised, 100, 0)]);
        var valid = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile(), rules, Examples.Date, [candidate]).Candidates);
        Assert.True(valid.Contributions[0].Assessment.HasAdequateEvidence);
        Assert.Contains("zeroDistance", valid.Contributions[1].Assessment.Reasons);
        var mismatched = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile(24), rules, Examples.Date, [candidate]).Candidates);
        Assert.Equal(BuyingEligibility.Eligible, mismatched.Eligibility);
        Assert.False(mismatched.Contributions[0].Assessment.HasAdequateEvidence);
        Assert.Equal(100, mismatched.Cost.CustomCosts.Cost.CompleteTotalSek);
        var invalid = new ComparisonEvaluator().EvaluateComparison(Examples.Profile() with { PeriodMonths = 0 }, rules, Examples.Date, [candidate]);
        Assert.NotEmpty(invalid.ProfileErrors);
        Assert.Equal(BuyingEligibility.Eligible, invalid.Candidates[0].Eligibility);
    }

    [Theory]
    [InlineData(SensitivityMode.Favorable, 10)]
    [InlineData(SensitivityMode.Baseline, 20)]
    [InlineData(SensitivityMode.Cautious, 30)]
    public void All_candidates_use_one_active_mode(SensitivityMode mode, int amount)
    {
        VehicleCostInput Cost(int id) => Examples.Cost(id, 0) with { AdditionalRepairAllowancePerMonthSek = SensitivityValue.Scenarios(10, 20, 30) };
        var result = new ComparisonEvaluator().EvaluateComparison(Examples.Profile() with { ActiveSensitivityMode = mode },
            new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 1200, 0)]), Examples.Date,
            [Examples.Candidate(101, cost: Cost(101)), Examples.Candidate(102, cost: Cost(102))]);
        foreach (var c in result.Candidates) Assert.Equal(amount * 12, c.Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(result.Candidates[0].Score, result.Candidates[1].Score);
    }

    [Theory]
    [InlineData(null, HouseholdBudgetStatus.NotConfigured, HardRuleState.NeedsVerification)]
    [InlineData(-1, HouseholdBudgetStatus.Invalid, HardRuleState.NeedsVerification)]
    [InlineData(10, HouseholdBudgetStatus.WithinLimit, HardRuleState.Pass)]
    [InlineData(9, HouseholdBudgetStatus.Exceeded, HardRuleState.Fail)]
    [InlineData(0, HouseholdBudgetStatus.Exceeded, HardRuleState.Fail)]
    public void Budget_rules_keep_all_authoritative_statuses(int? limit, HouseholdBudgetStatus status, HardRuleState ruleState)
    {
        var rules = new RuleProfileInput([new("monthlyBudget", HardRuleOperator.WithinBudget, EvidenceRequirement.Advertised)]);
        var profile = Examples.Profile() with { MonthlyBudgetSek = limit };
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(profile, rules, Examples.Date, [Examples.Candidate(cost: Examples.Cost(101, 120))]).Candidates);
        Assert.Equal(status, result.Cost.MonthlyBudget.Status);
        Assert.Equal(ruleState, result.HardRules[0].State);
    }

    [Theory]
    [InlineData(2400, 200, HouseholdBudgetStatus.Unknown)]
    [InlineData(2400, 199, HouseholdBudgetStatus.Exceeded)]
    [InlineData(2400, null, HouseholdBudgetStatus.NotConfigured)]
    [InlineData(2400, -1, HouseholdBudgetStatus.Invalid)]
    public void Legacy_review_blocks_complete_costs_and_budget_passes_but_preserves_known_exceedance(int amount, int? limit, HouseholdBudgetStatus status)
    {
        var review = new ComparisonReviewItem("legacy-maintenance", "combinedMaintenanceRequiresClassification",
            [ComparisonReviewSection.Service, ComparisonReviewSection.Payments, ComparisonReviewSection.StartupBudget, ComparisonReviewSection.MonthlyBudget]);
        var candidate = Examples.Candidate(cost: Examples.Cost(101, amount), reviews: [review]);
        var rules = new RuleProfileInput([new("monthlyBudget", HardRuleOperator.WithinBudget, EvidenceRequirement.Advertised)], [new("netCostSek", 1, EvidenceRequirement.Advertised, 3000, 0)]);
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile() with { MonthlyBudgetSek = limit }, rules, Examples.Date, [candidate]).Candidates);
        Assert.Equal(amount, result.Cost.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Null(result.Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Null(result.Cost.Reconciliation.ReconciledOwnershipCost.CompleteTotalSek);
        Assert.Contains("legacyReview:legacy-maintenance", result.Cost.Totals.OwnershipCost.MissingComponents);
        Assert.Equal(new ScoreRange(0, 100), result.Score);
        Assert.Equal(status, result.Cost.MonthlyBudget.Status);
        Assert.Equal(status == HouseholdBudgetStatus.Exceeded ? HardRuleState.Fail : HardRuleState.NeedsVerification, result.HardRules[0].State);
        Assert.False(result.IsCheapestEligibleComplete);
    }

    [Fact]
    public void Safe_budget_exceedance_survives_an_independent_invalid_cost_item_and_display_rounding()
    {
        var cost = Examples.Cost(101, 120.0001m) with { Tax = CostExamples.Category("invalid", -1, HouseholdCostCadence.Monthly) };
        var rules = new RuleProfileInput([new("monthlyBudget", HardRuleOperator.WithinBudget, EvidenceRequirement.Advertised)]);
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile() with { MonthlyBudgetSek = 10 }, rules, Examples.Date, [Examples.Candidate(cost: cost)]).Candidates);
        Assert.Equal(10, result.Cost.MonthlyBudget.FundingRequired.KnownSubtotalSek);
        Assert.Equal(HouseholdBudgetStatus.Exceeded, result.Cost.MonthlyBudget.Status);
        Assert.Equal(HardRuleState.Fail, result.HardRules[0].State);
        Assert.NotEmpty(result.HardRules[0].Assessment.Errors);
    }

    [Fact]
    public void Existing_residual_bounds_prevent_fabricating_a_negative_complete_cost()
    {
        var cost = Examples.Cost(101, 0) with { Residual = HouseholdResidualInput.FixedAmount(CostExamples.Value(100_100), 12) };
        var result = Examples.One(Examples.Candidate(cost: cost), new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 0, -200)]));
        Assert.Null(result.Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(new ScoreRange(0, 100), result.Score);
        Assert.Contains(result.Cost.Depreciation.Cost.Errors, x => x.Code == "outOfRange");
        Assert.False(result.IsCheapestEligibleComplete);
        var zero = Examples.One(Examples.Candidate(cost: Examples.Cost(101, 0)),
            new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, -100, 100)]));
        Assert.Equal(new ScoreRange(50, 50), zero.Score);
    }

    [Fact]
    public void Overflow_in_score_arithmetic_is_local_and_preserves_other_contributions()
    {
        var result = Examples.One(Examples.Candidate(facts: Examples.Tow(true), cost: Examples.Cost(101, 0)),
            new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, decimal.MinValue, decimal.MaxValue),
                new("towBar", 1, EvidenceRequirement.UserConfirmed, preferredValues: [new(Boolean: true)])]));
        Assert.Contains(result.Errors, x => x.Code == "calculationOutOfRange");
        Assert.Equal(new ScoreRange(50, 100), result.Score);
        Assert.Equal(50, result.CoveragePercent);
        Assert.Equal(0, result.Cost.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData("netCostSek", 240, 120)]
    [InlineData("costPerMonthSek", 20, 10)]
    [InlineData("costPerMilSek", 24, 12)]
    public void Each_derived_cost_criterion_uses_the_corresponding_complete_engine_measure(string key, int zero, int expected)
    {
        // 100 km * 6 litres/100 km * 20 SEK/litre = 120 SEK.
        var cost = CostExamples.Car(Examples.Registration(101), sources: [CostExamples.Petrol()]);
        var profile = Examples.Profile() with { AnnualDistanceKilometres = 100 };
        var rules = new RuleProfileInput(preferences: [new(key, 1, EvidenceRequirement.Advertised, zero, 0)]);
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(profile, rules, Examples.Date, [Examples.Candidate(cost: cost)]).Candidates);
        Assert.Equal(expected, result.Contributions[0].Assessment.Actual!.Number);
        Assert.Equal(new ScoreRange(50, 50), result.Score);
    }

    [Theory]
    [InlineData(1, HardRuleState.Pass)]
    [InlineData(0, HardRuleState.Fail)]
    public void Startup_budget_uses_its_separate_limit_and_excludes_purchase_cash(int limit, HardRuleState expected)
    {
        var cost = Examples.Cost(101, 0) with { CustomCosts = CostExamples.Category("startup", 1, HouseholdCostCadence.Once, 0) };
        var rules = new RuleProfileInput([new("startupBudget", HardRuleOperator.WithinBudget, EvidenceRequirement.Advertised)]);
        var result = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile() with { StartupBudgetSek = limit }, rules, Examples.Date, [Examples.Candidate(cost: cost)]).Candidates);
        Assert.Equal(1, result.Cost.StartupBudget.FundingRequired.CompleteTotalSek);
        Assert.Equal(expected, result.HardRules[0].State);
        Assert.Equal(0, result.Cost.MonthlyBudget.FundingRequired.CompleteTotalSek);
    }

    [Fact]
    public void Cost_per_mil_overflow_preserves_total_and_another_candidates_complete_score()
    {
        var cost = CostExamples.Car(Examples.Registration(101), sources: [CostExamples.Petrol() with { ConsumptionPer100Kilometres = CostExamples.Value(100) }]) with
        { CustomCosts = CostExamples.Category("expense", 1, HouseholdCostCadence.Once, 1) };
        var zero = CostExamples.Car(Examples.Registration(102), sources: [CostExamples.Petrol() with { ConsumptionPer100Kilometres = CostExamples.Value(100) }]);
        var rules = new RuleProfileInput(preferences: [new("costPerMilSek", 1, EvidenceRequirement.Advertised, 100, 0)]);
        var profile = CostExamples.Profile(12, 0.0000000000000000000000000001m, [new(FuelType.Petrol, EnergyUnit.Litre, CostExamples.Value(0))]) with { StartMonth = new(2026, 1) };
        var result = new ComparisonEvaluator().EvaluateComparison(profile, rules, Examples.Date,
            [Examples.Candidate(101, cost: cost), Examples.Candidate(102, cost: zero)]);
        Assert.Equal(1, result.Candidates[0].Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Contains(result.Candidates[0].Contributions[0].Assessment.Errors, x => x.Code == "calculationOutOfRange");
        Assert.Equal(new ScoreRange(0, 100), result.Candidates[0].Score);
        Assert.Equal(new ScoreRange(100, 100), result.Candidates[1].Score);
    }
}
