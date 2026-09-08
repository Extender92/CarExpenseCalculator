using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ComparisonEvaluatorTests
{
    [Fact]
    public void B1_fixed_targets_produce_75_and_100_weighted_to_85()
    {
        var result = Examples.One(Examples.B1(), Examples.Rules());
        Assert.Equal(new ScoreRange(85, 85), result.Score);
        Assert.Equal(100, result.CoveragePercent);
        Assert.Equal(new[] { new ScoreRange(75, 75), new ScoreRange(100, 100) }, result.Contributions.Select(x => x.Range));
        Assert.Equal(1, Examples.Preview(Examples.Rules(), Examples.B1()).RuleVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void B2_unknown_or_advertised_gearbox_retains_interval_and_common_denominator(bool advertised)
    {
        var facts = Examples.B1Facts() with { Transmission = advertised ? VehicleFact<Transmission>.Known(Transmission.Automatic, Examples.Advertisement) : null };
        var result = Examples.One(Examples.Candidate(facts: facts), Examples.Rules());
        Assert.Equal(new ScoreRange(45, 85), result.Score);
        Assert.Equal(60, result.CoveragePercent);
        Assert.False(result.Contributions[1].Assessment.HasAdequateEvidence);
        Assert.Equal(new ScoreRange(0, 100), result.Contributions[1].Range);
    }

    [Theory]
    [InlineData(3, 0, 75)]
    [InlineData(3, 2, 45)]
    [InlineData(1, 4, 15)]
    public void B3_B4_changing_only_weights_changes_scores(int priceWeight, int gearWeight, int expected)
    {
        var result = Examples.One(Examples.Candidate(facts: Examples.B1Facts() with { Transmission = Examples.Known(Transmission.Manual) }),
            Examples.Rules(priceWeight, gearWeight));
        Assert.Equal(new ScoreRange(expected, expected), result.Score);
        Assert.Equal(100, result.CoveragePercent);
        Assert.Equal(gearWeight == 0 ? 1 : 2, result.Contributions.Count);
    }

    [Fact]
    public void B3_all_zero_weights_have_no_score_coverage_or_preference_winner()
    {
        var result = Examples.Preview(Examples.Rules(0, 0), Examples.B1());
        var car = Assert.Single(result.Candidates);
        Assert.Null(car.Score); Assert.Null(car.CoveragePercent); Assert.Empty(car.Contributions);
        Assert.Equal("noActiveCriteria", car.ScoreUnavailableReason);
        Assert.False(car.IsDefinitePreferenceWinner);
    }

    [Fact]
    public void B5_overlapping_ranges_sort_by_lower_bound_without_a_certain_winner()
    {
        var rules = new RuleProfileInput(preferences: [Examples.PricePreference(), Examples.GearPreference(1),
            new("towBar", 1, EvidenceRequirement.UserConfirmed, preferredValues: [new(Boolean: true)])]);
        var x = Examples.Candidate(101, new() { PurchasePriceSek = Examples.Known(40_000m) });
        var y = Examples.Candidate(102, new() { PurchasePriceSek = Examples.Known(20_000m), Transmission = Examples.Known(Transmission.Manual) });
        var result = Examples.Preview(rules, x, y);
        Assert.Equal(new ScoreRange(45, 85), result.Candidates[0].Score);
        Assert.Equal(new ScoreRange(60, 80), result.Candidates[1].Score);
        Assert.Equal(new[] { y.VehicleId, x.VehicleId }, result.ScoreOrder);
        Assert.All(result.Candidates, c => Assert.False(c.IsDefinitePreferenceWinner));
        Assert.Equal("overlapOrTie", result.PreferenceRecommendationReason);
    }

    [Fact]
    public void B6_complete_costs_precede_partial_then_rejected_without_false_winners()
    {
        var a = Examples.Candidate(101, Examples.Tow(true), Examples.Cost(101, 30_000));
        var b = Examples.Candidate(102, Examples.Tow(true), Examples.Cost(102, 25_000));
        var c = Examples.Candidate(103, Examples.Tow(true), Examples.Cost(103, 10_000) with { Tax = null });
        var d = Examples.Candidate(104, Examples.Tow(false), Examples.Cost(104, 20_000));
        var result = Examples.Preview(new(hardRules: [Examples.TowRule()]), a, b, c, d);
        Assert.Equal(new[] { b.VehicleId, a.VehicleId, c.VehicleId, d.VehicleId }, result.CostOrder);
        Assert.Equal(b.VehicleId, Assert.Single(result.Candidates, x => x.IsCheapestEligibleComplete).EffectiveInput.VehicleId);
        Assert.Equal(10_000, result.Candidates[2].Cost.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Null(result.Candidates[2].Cost.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(BuyingEligibility.Rejected, result.Candidates[3].Eligibility);
    }

    [Fact]
    public void B7_inclusive_limits_unknown_owners_and_confirmed_false_tow_bar_are_independent()
    {
        var candidate = Examples.Candidate(facts: new()
        {
            PurchasePriceSek = Examples.Known(20_000m), OdometerKilometres = Examples.Known(200_000m), TowBar = Examples.Known(false),
        });
        var rules = new RuleProfileInput(hardRules: [
            new("purchasePriceSek", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, maximum: 20_000),
            new("odometerKilometres", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, maximum: SwedishMil.ToKilometres(20_000)),
            new("ownerCount", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, maximum: 6), Examples.TowRule()]);
        var result = Examples.One(candidate, rules);
        Assert.Equal(new[] { HardRuleState.Pass, HardRuleState.Pass, HardRuleState.NeedsVerification, HardRuleState.Fail }, result.HardRules.Select(x => x.State));
        Assert.Equal(BuyingEligibility.Rejected, result.Eligibility);
        Assert.Contains("bortvald", result.HardRules[3].Explanation);
    }

    [Fact]
    public void B8_adding_removing_and_reordering_candidates_never_normalizes_against_other_prices()
    {
        var original = Examples.B1();
        var cheap = Examples.Candidate(102, Examples.B1Facts() with { PurchasePriceSek = Examples.Known(1000m) });
        var expensive = Examples.Candidate(103, Examples.B1Facts() with { PurchasePriceSek = Examples.Known(10_000_000m) });
        foreach (var candidates in new[] { new[] { original }, [original, cheap], [expensive, cheap, original], [expensive, original] })
        {
            var item = Examples.Preview(Examples.Rules(), candidates).Candidates.Single(x => x.EffectiveInput.VehicleId == original.VehicleId);
            Assert.Equal(new ScoreRange(85, 85), item.Score);
            Assert.Equal(new ScoreRange(75, 75), item.Contributions[0].Range);
        }
    }

    [Theory]
    [InlineData(0, 0, 100, 0)]
    [InlineData(50, 0, 100, 50)]
    [InlineData(200, 0, 100, 100)]
    [InlineData(0, 100, 0, 100)]
    [InlineData(50, 100, 0, 50)]
    [InlineData(200, 100, 0, 0)]
    public void Numeric_directions_and_clamping_use_fixed_anchors(int value, int zero, int full, int expected)
    {
        var result = Examples.One(Examples.Candidate(facts: new() { PurchasePriceSek = Examples.Known((decimal)value) }),
            new(preferences: [new("purchasePriceSek", 1, EvidenceRequirement.UserConfirmed, zero, full)]));
        Assert.Equal(new ScoreRange(expected, expected), result.Score);
    }

    [Theory]
    [InlineData(VehicleFactState.Unknown)]
    [InlineData(VehicleFactState.NotApplicable)]
    [InlineData(VehicleFactState.Conflicting)]
    public void Unresolved_fact_states_never_shrink_the_denominator_or_pass_hard_rules(VehicleFactState state)
    {
        var fact = state == VehicleFactState.Conflicting ? VehicleFact<bool>.Conflicting([new(true, Examples.Manual), new(false, Examples.Manual)]) : new VehicleFact<bool>(state);
        var result = Examples.One(Examples.Candidate(facts: new() { TowBar = fact }), new(hardRules: [Examples.TowRule()],
            preferences: [new("towBar", 5, EvidenceRequirement.UserConfirmed, preferredValues: [new(Boolean: true)])]));
        Assert.Equal(new ScoreRange(0, 100), result.Score); Assert.Equal(0, result.CoveragePercent);
        Assert.Equal(HardRuleState.NeedsVerification, Assert.Single(result.HardRules).State);
    }

    [Fact]
    public void Invalid_field_does_not_erase_independent_hard_failure_or_other_candidate()
    {
        var first = Examples.Candidate(facts: new() { Seats = Examples.Known(0), TowBar = Examples.Known(false) });
        var second = Examples.Candidate(102, new() { Seats = Examples.Known(5), TowBar = Examples.Known(true) });
        var rules = new RuleProfileInput(hardRules: [new("seats", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, minimum: 4), Examples.TowRule()]);
        var preview = Examples.Preview(rules, first, second);
        Assert.Equal(BuyingEligibility.Rejected, preview.Candidates[0].Eligibility);
        Assert.Equal(HardRuleState.NeedsVerification, preview.Candidates[0].HardRules[0].State);
        Assert.Contains(preview.Candidates[0].Errors, x => x.Path == "candidates[0].facts.seats.observations[0].value" && x.Code == "outOfRange");
        Assert.Equal(BuyingEligibility.Eligible, preview.Candidates[1].Eligibility);
        Assert.Throws<VehicleFactsValidationException>(() => new VehicleFactsProcessor().Normalize(first.Facts));
    }

    [Theory]
    [InlineData(EvidenceRequirement.Advertised, HardRuleState.Fail)]
    [InlineData(EvidenceRequirement.UserConfirmed, HardRuleState.NeedsVerification)]
    [InlineData(EvidenceRequirement.RegistryVerified, HardRuleState.NeedsVerification)]
    public void Seller_claim_of_failure_is_separate_from_required_evidence(EvidenceRequirement minimum, HardRuleState expected)
    {
        var result = Examples.One(Examples.Candidate(facts: new() { TowBar = VehicleFact<bool>.Known(false, Examples.Advertisement) }),
            new(hardRules: [Examples.TowRule(minimum)]));
        var rule = Assert.Single(result.HardRules);
        Assert.Equal(expected, rule.State); Assert.False(rule.ObservedConditionSatisfied);
        Assert.Equal(Examples.Advertisement, Assert.Single(rule.Assessment.Evidence));
    }

    [Fact]
    public void Known_high_scores_cannot_override_failure_or_unverified_requirements()
    {
        var first = Examples.Candidate(101, Examples.B1Facts() with { TowBar = Examples.Known(false) }, Examples.Cost(101, 1) with { PriceSek = 40_000 });
        var second = Examples.Candidate(102, Examples.B1Facts(), Examples.Cost(102, 2) with { PriceSek = 40_000 });
        var result = Examples.Preview(new([Examples.TowRule()], Examples.Rules().Preferences), first, second);
        Assert.All(result.Candidates, x => { Assert.Equal(new ScoreRange(85, 85), x.Score); Assert.False(x.IsCheapestEligibleComplete); Assert.False(x.IsDefinitePreferenceWinner); });
        Assert.Equal(new[] { second.VehicleId, first.VehicleId }, result.ScoreOrder);
    }

    [Fact]
    public void Cost_and_score_order_use_precision_before_presentation_rounding()
    {
        var expensive = Examples.Candidate(101, cost: Examples.Cost(101, 100.002m));
        var cheap = Examples.Candidate(102, cost: Examples.Cost(102, 100.001m));
        var result = Examples.Preview(new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 200, 0)]), expensive, cheap);
        Assert.All(result.Candidates, x => Assert.Equal(100.00m, x.Cost.Totals.OwnershipCost.CompleteTotalSek));
        Assert.Equal(result.Candidates[0].Score, result.Candidates[1].Score);
        Assert.Equal(new[] { cheap.VehicleId, expensive.VehicleId }, result.CostOrder);
        Assert.Equal(result.CostOrder, result.ScoreOrder);
        Assert.True(result.Candidates[1].IsCheapestEligibleComplete);
        Assert.True(result.Candidates[1].IsDefinitePreferenceWinner);
    }

    [Fact]
    public void Exact_cost_ties_share_label_and_use_registration_order_regardless_of_input_order()
    {
        var a = Examples.Candidate(101, cost: Examples.Cost(101, 10));
        var b = Examples.Candidate(102, cost: Examples.Cost(102, 10));
        var result = Examples.Preview(new(preferences: [new("netCostSek", 1, EvidenceRequirement.Advertised, 100, 0)]), b, a);
        Assert.Equal(new[] { a.VehicleId, b.VehicleId }, result.CostOrder);
        Assert.Equal(b.VehicleId, result.Candidates[0].EffectiveInput.VehicleId);
        Assert.All(result.Candidates, x => { Assert.True(x.IsCheapestEligibleComplete); Assert.False(x.IsDefinitePreferenceWinner); });
    }

    [Fact]
    public void A_representable_tiny_score_is_not_lost_by_premature_division()
    {
        var zero = Examples.Candidate(101, new() { PurchasePriceSek = Examples.Known(0m) });
        var tiny = Examples.Candidate(102, new() { PurchasePriceSek = Examples.Known(0.0000000000000000000000000001m) });
        var result = Examples.Preview(new(preferences: [new("purchasePriceSek", 1, EvidenceRequirement.UserConfirmed, 0, 100)]), zero, tiny);
        Assert.Equal(result.Candidates[0].Score, result.Candidates[1].Score);
        Assert.Equal(new[] { tiny.VehicleId, zero.VehicleId }, result.ScoreOrder);
        Assert.True(result.Candidates[1].IsDefinitePreferenceWinner);
    }
}

internal static class Examples
{
    internal static readonly DateOnly Date = new(2026, 9, 8);
    internal static readonly DateTimeOffset ConfirmedAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
    internal static readonly ComparisonEvidence Manual = new(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, ConfirmedAt: ConfirmedAt);
    internal static readonly ComparisonEvidence Advertisement = new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, ListingUrl.Parse("https://example.com/car"));
    internal static VehicleFact<T> Known<T>(T value) where T : notnull => VehicleFact<T>.Known(value, Manual);
    internal static string Registration(int id) => $"ABC{id:000}";
    internal static ComparisonCandidateInput Candidate(int id = 101, VehicleComparisonFacts? facts = null, VehicleCostInput? cost = null,
        CostAssumptionConfirmation? confirmation = null, IEnumerable<ComparisonReviewItem>? reviews = null, IEnumerable<VehicleFact<string>>? notes = null) =>
        new(new Guid(id, 0, 0, new byte[8]), RegistrationNumber.Parse(Registration(id)), facts, cost, confirmation, reviewItems: reviews, conditionNotes: notes);
    internal static VehicleCostInput Cost(int id, decimal total) => CostExamples.Car(Registration(id)) with
    {
        CustomCosts = CostExamples.Category("expense", total, HouseholdCostCadence.Once, 1),
    };
    internal static HouseholdProfileInput Profile(int months = 12) => CostExamples.Profile(months) with { StartMonth = new(2026, 1) };
    internal static VehicleComparisonFacts B1Facts() => new() { PurchasePriceSek = Known(40_000m), Transmission = Known(Transmission.Automatic) };
    internal static VehicleComparisonFacts Tow(bool value) => new() { TowBar = Known(value) };
    internal static ComparisonCandidateInput B1() => Candidate(facts: B1Facts());
    internal static PreferenceInput PricePreference(int weight = 3) => new("purchasePriceSek", weight, EvidenceRequirement.UserConfirmed, 100_000, 20_000);
    internal static PreferenceInput GearPreference(int weight = 2) => new("transmission", weight, EvidenceRequirement.UserConfirmed, preferredValues: [new(Transmission: Transmission.Automatic)]);
    internal static RuleProfileInput Rules(int priceWeight = 3, int gearWeight = 2) => new(preferences: [PricePreference(priceWeight), GearPreference(gearWeight)]);
    internal static HardRuleInput TowRule(EvidenceRequirement evidence = EvidenceRequirement.UserConfirmed) => new("towBar", HardRuleOperator.Equals, evidence, allowedValues: [new(Boolean: true)]);
    internal static ComparisonPreview Preview(RuleProfileInput rules, params ComparisonCandidateInput[] candidates) => new ComparisonEvaluator().EvaluateComparison(Profile(), rules, Date, candidates);
    internal static CurrentEvaluation One(ComparisonCandidateInput candidate, RuleProfileInput rules) => Assert.Single(Preview(rules, candidate).Candidates);
}
