using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ComparisonInputValidationTests
{
    [Fact]
    public void Malformed_rules_aggregate_paths_even_when_disabled()
    {
        var rules = new RuleProfileInput([
            new("towBar", HardRuleOperator.AllowedSet, EvidenceRequirement.UserConfirmed, allowedValues: []),
            new("ownerCount", HardRuleOperator.InclusiveRange, (EvidenceRequirement)99, minimum: 7, maximum: 6),
            new("ownerCount", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, minimum: -1, enabled: false),
            new("unknown", HardRuleOperator.Equals, EvidenceRequirement.UserConfirmed)],
            [new("purchasePriceSek", 0, EvidenceRequirement.UserConfirmed, 10, 10), new("seats", 6, EvidenceRequirement.UserConfirmed, 0, 5),
                new("monthlyBudget", 1, EvidenceRequirement.Advertised)],
            [new(ComparisonSignalKey.InspectionValidity), new(ComparisonSignalKey.InspectionValidity, -1), new((ComparisonSignalKey)99)]);
        var errors = Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(rules)).Errors;
        Assert.Contains(errors, x => x.Path == "rules.hardRules[0].operator" && x.Code == "invalidOperator");
        Assert.Contains(errors, x => x.Path == "rules.hardRules[1].minimumEvidence" && x.Code == "invalidEnum");
        Assert.Contains(errors, x => x.Path == "rules.hardRules[2].criterionKey" && x.Code == "duplicateKey");
        Assert.Contains(errors, x => x.Path == "rules.hardRules[2].minimum" && x.Code == "outOfRange");
        Assert.Contains(errors, x => x.Path == "rules.preferences[0]" && x.Code == "invalidAnchors");
        Assert.Contains(errors, x => x.Code == "unsupportedPreference");
        Assert.Contains(errors, x => x.Path == "rules.signals[0].shortInspectionDays" && x.Code == "required");
    }

    [Fact]
    public void Categorical_normalization_rejects_duplicate_and_wrong_typed_values()
    {
        var rules = new RuleProfileInput(preferences: [
            new("locality", 1, EvidenceRequirement.UserConfirmed, preferredValues: [new(Text: "Örebro"), new(Text: " O\u0308REBRO ")]),
            new("transmission", 0, EvidenceRequirement.UserConfirmed, preferredValues: [new(Transmission: (Transmission)99), new(Boolean: true)]),
            new("towBar", 1, EvidenceRequirement.UserConfirmed, preferredValues: [new(Boolean: false, Text: "no")])]);
        var errors = Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(rules)).Errors;
        Assert.Contains(errors, x => x.Path == "rules.preferences[0].preferredValues[1]" && x.Code == "duplicateValue");
        Assert.Contains(errors, x => x.Path == "rules.preferences[1].preferredValues[0]" && x.Code == "invalidChoice");
        Assert.Contains(errors, x => x.Path == "rules.preferences[1].preferredValues[1]" && x.Code == "invalidChoice");
        Assert.Contains(errors, x => x.Path == "rules.preferences[2].preferredValues[0]" && x.Code == "invalidChoice");
    }

    [Fact]
    public void Disabled_incomplete_rules_need_no_targets_but_active_ones_do()
    {
        var disabled = new RuleProfileInput([new("ownerCount", HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, enabled: false)],
            [new("purchasePriceSek", 0, EvidenceRequirement.UserConfirmed), new("transmission", 0, EvidenceRequirement.UserConfirmed)]);
        var result = Examples.One(Examples.Candidate(), disabled);
        Assert.Equal(BuyingEligibility.Eligible, result.Eligibility);
        Assert.Null(result.Score);
        Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(new(preferences: [new("transmission", 1, EvidenceRequirement.UserConfirmed)])));
        Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(new(preferences: [new("purchasePriceSek", 1, EvidenceRequirement.UserConfirmed)])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Empty_and_maximum_candidate_sets_are_supported_without_defaults(int count)
    {
        var cars = Enumerable.Range(100, count).Select(x => Examples.Candidate(x)).ToArray();
        var preview = Examples.Preview(new(), cars);
        Assert.Equal(count, preview.Candidates.Count);
        Assert.All(preview.Candidates, x => { Assert.Null(x.Score); Assert.Empty(x.HardRules); Assert.Empty(x.Signals); });
    }

    [Fact]
    public void Excess_candidates_duplicate_identities_and_cross_vehicle_costs_are_structural_errors()
    {
        var many = Enumerable.Range(100, 101).Select(x => Examples.Candidate(x)).ToArray();
        Assert.Contains(Assert.Throws<ComparisonInputValidationException>(() => Examples.Preview(new(), many)).Errors, x => x.Code == "tooManyItems");
        var duplicate = Assert.Throws<ComparisonInputValidationException>(() => Examples.Preview(new(), Examples.Candidate(), Examples.Candidate()));
        Assert.Contains(duplicate.Errors, x => x.Path == "candidates[1].vehicleId");
        Assert.Contains(duplicate.Errors, x => x.Path == "candidates[1].registrationNumber");
        var mismatch = Assert.Throws<ComparisonInputValidationException>(() => Examples.One(Examples.Candidate(cost: Examples.Cost(102, 1)), new()));
        Assert.Contains(mismatch.Errors, x => x.Code == "identityMismatch");
    }

    [Fact]
    public void Collection_limits_and_null_entries_are_rejected_without_truncation()
    {
        var choices = Enumerable.Range(0, 51).Select(x => new ComparisonChoice(Text: "Ort " + x)).ToArray();
        var rules = new RuleProfileInput(Enumerable.Repeat(Examples.TowRule(), 51),
            [new("locality", 1, EvidenceRequirement.UserConfirmed, preferredValues: choices)]);
        var errors = Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(rules)).Errors;
        Assert.Contains(errors, x => x.Path == "rules.hardRules" && x.Code == "tooManyItems");
        Assert.Contains(errors, x => x.Path == "rules.preferences[0].preferredValues" && x.Code == "tooManyItems");
        Assert.Throws<ComparisonInputValidationException>(() => Examples.Preview(new(), [null!]));
        Assert.Throws<ArgumentNullException>(() => Examples.Preview(new(), null!));
        Assert.Throws<ComparisonInputValidationException>(() => new RuleProfileProcessor().Normalize(new([null!], [null!], [null!])));
    }

    [Fact]
    public void Input_collections_and_results_are_immutable_snapshots()
    {
        var choices = new[] { new ComparisonChoice(Transmission: Transmission.Automatic) };
        var preferences = new[] { new PreferenceInput("transmission", 1, EvidenceRequirement.UserConfirmed, preferredValues: choices) };
        var signals = new[] { new ComparisonSignalInput(ComparisonSignalKey.ServiceDocumentation) };
        var profile = new RuleProfileInput(preferences: preferences, signals: signals);
        var affected = new[] { ComparisonReviewSection.Tax };
        var reviews = new[] { new ComparisonReviewItem("legacy-tax", "unmapped", affected) };
        var candidate = Examples.Candidate(facts: Examples.B1Facts(), reviews: reviews);
        choices[0] = new(Transmission: Transmission.Manual); preferences[0] = Examples.PricePreference();
        signals[0] = new(ComparisonSignalKey.MonthlyBudget); affected[0] = ComparisonReviewSection.Energy;
        reviews[0] = new("other", "other", [ComparisonReviewSection.Payments]);
        var result = Examples.One(candidate, profile);
        Assert.Equal(new ScoreRange(100, 100), result.Score);
        Assert.Equal(ComparisonSignalKey.ServiceDocumentation, Assert.Single(result.Signals).Key);
        Assert.Equal("legacy-tax", result.EffectiveInput.ReviewItems[0].Key);
        Assert.Equal(ComparisonReviewSection.Tax, result.EffectiveInput.ReviewItems[0].AffectedSections[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<PreferenceContribution>)result.Contributions).Clear());
    }
}
