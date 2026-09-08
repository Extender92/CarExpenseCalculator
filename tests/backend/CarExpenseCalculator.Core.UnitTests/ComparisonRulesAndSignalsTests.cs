using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ComparisonRulesAndSignalsTests
{
    public static IEnumerable<object[]> NumericFacts()
    {
        yield return ["purchasePriceSek", 40_000m, new VehicleComparisonFacts { PurchasePriceSek = Examples.Known(40_000m) }];
        yield return ["odometerKilometres", 123_456.789m, new VehicleComparisonFacts { OdometerKilometres = Examples.Known(123_456.789m) }];
        yield return ["ownerCount", 6m, new VehicleComparisonFacts { OwnerCount = Examples.Known(6) }];
        yield return ["seats", 5m, new VehicleComparisonFacts { Seats = Examples.Known(5) }];
        yield return ["modelYear", 2020m, new VehicleComparisonFacts { ModelYear = Examples.Known(2020) }];
        yield return ["towingCapacityKilograms", 1500m, new VehicleComparisonFacts { TowingCapacityKilograms = Examples.Known(1500) }];
    }

    [Theory]
    [MemberData(nameof(NumericFacts))]
    public void Every_numeric_source_field_supports_inclusive_hard_limits_and_preference_scoring(string key, decimal value, VehicleComparisonFacts facts)
    {
        var definition = ComparisonCriterionCatalog.All.Single(x => x.Key == key);
        var result = Examples.One(Examples.Candidate(facts: facts), new(
            [new(key, HardRuleOperator.InclusiveRange, EvidenceRequirement.UserConfirmed, value, value)],
            [new(key, 1, EvidenceRequirement.UserConfirmed, definition.Minimum, value)]));
        Assert.Equal(HardRuleState.Pass, result.HardRules[0].State);
        Assert.Equal(new ScoreRange(100, 100), result.Score);
        Assert.Equal(value, result.Contributions[0].Assessment.Actual!.Number);
    }

    public static IEnumerable<object[]> Categories()
    {
        yield return ["towBar", new ComparisonChoice(Boolean: false), new VehicleComparisonFacts { TowBar = Examples.Known(false) }];
        foreach (var value in Enum.GetValues<Transmission>())
            yield return ["transmission", new ComparisonChoice(Transmission: value), new VehicleComparisonFacts { Transmission = Examples.Known(value) }];
        foreach (var value in Enum.GetValues<BodyType>())
            yield return ["bodyType", new ComparisonChoice(BodyType: value), new VehicleComparisonFacts { BodyType = Examples.Known(value) }];
        foreach (var value in Enum.GetValues<Drivetrain>())
            yield return ["drivetrain", new ComparisonChoice(Drivetrain: value), new VehicleComparisonFacts { Drivetrain = Examples.Known(value) }];
        foreach (var value in Enum.GetValues<ServiceDocumentationStatus>())
            yield return ["serviceDocumentation", new ComparisonChoice(ServiceDocumentation: value), new VehicleComparisonFacts { ServiceDocumentation = Examples.Known(value) }];
        foreach (var value in Enum.GetValues<FuelType>())
            yield return ["fuelTypes", new ComparisonChoice(FuelType: value), new VehicleComparisonFacts { FuelTypes = Examples.Known(new FuelTypeSet([value])) }];
        yield return ["locality", new ComparisonChoice(Text: "  O\u0308REBRO "), new VehicleComparisonFacts { Locality = Examples.Known("Örebro") }];
        yield return ["county", new ComparisonChoice(Text: "  ÖREBRO LÄN "), new VehicleComparisonFacts { County = Examples.Known("Örebro län") }];
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void Every_supported_category_and_fuel_has_equal_matching_semantics(string key, ComparisonChoice choice, VehicleComparisonFacts facts)
    {
        var op = key == "towBar" ? HardRuleOperator.Equals : key == "fuelTypes" ? HardRuleOperator.Intersects : HardRuleOperator.AllowedSet;
        var result = Examples.One(Examples.Candidate(facts: facts), new([new(key, op, EvidenceRequirement.UserConfirmed, allowedValues: [choice])],
            [new(key, 1, EvidenceRequirement.UserConfirmed, preferredValues: [choice])]));
        Assert.Equal(HardRuleState.Pass, result.HardRules[0].State);
        Assert.Equal(new ScoreRange(100, 100), result.Score);
    }

    [Fact]
    public void Known_empty_fuels_score_zero_with_full_coverage_while_unknown_fuels_have_an_interval()
    {
        var rules = new RuleProfileInput(preferences: [new("fuelTypes", 1, EvidenceRequirement.UserConfirmed, preferredValues: [new(FuelType: FuelType.Electricity)])]);
        var empty = Examples.One(Examples.Candidate(facts: new() { FuelTypes = Examples.Known(new FuelTypeSet([])) }), rules);
        var unknown = Examples.One(Examples.Candidate(), rules);
        Assert.Equal(new ScoreRange(0, 0), empty.Score); Assert.Equal(100, empty.CoveragePercent);
        Assert.Equal(new ScoreRange(0, 100), unknown.Score); Assert.Equal(0, unknown.CoveragePercent);
    }

    [Theory]
    [InlineData(-1, HardRuleState.Fail, "inspectionExpired")]
    [InlineData(29, HardRuleState.Fail, "inspectionShort")]
    [InlineData(30, HardRuleState.Pass, "inspectionMeetsThreshold")]
    [InlineData(31, HardRuleState.Pass, "inspectionMeetsThreshold")]
    public void Inspection_uses_explicit_date_and_inclusive_day_threshold(int days, HardRuleState expected, string code)
    {
        var candidate = Examples.Candidate(facts: new() { InspectionValidThrough = Examples.Known(Examples.Date.AddDays(days)) });
        var rules = new RuleProfileInput([new("inspectionValidThrough", HardRuleOperator.MinimumRemainingDays, EvidenceRequirement.UserConfirmed, minimum: 30)],
            [new("inspectionValidThrough", 1, EvidenceRequirement.UserConfirmed, 0, 100)], [new(ComparisonSignalKey.InspectionValidity, 30)]);
        var result = Examples.One(candidate, rules);
        Assert.Equal(expected, result.HardRules[0].State);
        Assert.Equal(code, Assert.Single(result.Signals).ReasonCode);
        Assert.Equal(days, result.HardRules[0].Assessment.Actual!.Number);
        var later = Assert.Single(new ComparisonEvaluator().EvaluateComparison(Examples.Profile(), rules, Examples.Date.AddDays(1), [candidate]).Candidates);
        Assert.Equal(days - 1, later.HardRules[0].Assessment.Actual!.Number);
    }

    [Theory]
    [InlineData(ServiceDocumentationStatus.Documented, ComparisonSignalKind.Positive)]
    [InlineData(ServiceDocumentationStatus.Partial, ComparisonSignalKind.Warning)]
    [InlineData(ServiceDocumentationStatus.Absent, ComparisonSignalKind.Warning)]
    public void Optional_service_and_condition_signals_do_not_change_score_or_hard_outcomes(ServiceDocumentationStatus service, ComparisonSignalKind kind)
    {
        var notes = new[] { VehicleFact<string>.Known("Säljaren uppger rost vid bakluckan", Examples.Advertisement) };
        var candidate = Examples.Candidate(facts: Examples.B1Facts() with { ServiceDocumentation = Examples.Known(service) }, notes: notes);
        var ordinary = Examples.One(candidate, Examples.Rules());
        var withSignals = Examples.One(candidate, new(preferences: Examples.Rules().Preferences,
            signals: [new(ComparisonSignalKey.ConditionNotes), new(ComparisonSignalKey.ServiceDocumentation), new(ComparisonSignalKey.CostCompleteness), new(ComparisonSignalKey.MonthlyBudget)]));
        Assert.Empty(ordinary.Signals);
        Assert.Equal(ordinary.Score, withSignals.Score); Assert.Equal(ordinary.Eligibility, withSignals.Eligibility);
        Assert.Equal(kind, withSignals.Signals.Single(x => x.Key == ComparisonSignalKey.ServiceDocumentation).Kind);
        var note = withSignals.Signals[0];
        Assert.Equal(ComparisonSignalKind.Information, note.Kind);
        Assert.Contains("Annonsuppgift", note.Explanation);
        Assert.Contains("rost vid bakluckan", note.Explanation);
        Assert.Equal(Examples.Advertisement, note.Evidence);
    }

    [Fact]
    public void Registry_claims_invalid_notes_and_conflicting_service_cannot_create_positive_verification()
    {
        var registry = Examples.Manual with { Verification = VerificationStatus.RegistryVerified, Origin = FieldOrigin.Registry };
        var facts = new VehicleComparisonFacts { TowBar = VehicleFact<bool>.Known(true, registry), ServiceDocumentation = VehicleFact<ServiceDocumentationStatus>.Conflicting(
            [new(ServiceDocumentationStatus.Documented, Examples.Manual), new(ServiceDocumentationStatus.Absent, Examples.Advertisement)]) };
        var result = Examples.One(Examples.Candidate(facts: facts, notes: [Examples.Known(new string('x', 301))]),
            new([Examples.TowRule(EvidenceRequirement.RegistryVerified)], signals: [new(ComparisonSignalKey.ConditionNotes), new(ComparisonSignalKey.ServiceDocumentation)]));
        Assert.Equal(HardRuleState.NeedsVerification, result.HardRules[0].State);
        Assert.Contains(result.Errors, x => x.Code == "unsupportedEvidence");
        Assert.Contains(result.Errors, x => x.Code == "tooLong");
        Assert.All(result.Signals, x => Assert.Equal(ComparisonSignalKind.Warning, x.Kind));
    }

    [Fact]
    public void Comparison_normalizes_effective_identity_and_echoes_revisions_without_reinterpreting_them()
    {
        var cost = Examples.Cost(101, 25) with { CandidateKey = "abc-101" };
        var revisions = new ComparisonSourceRevisions(long.MaxValue, 7, 6, 5, 4);
        var candidate = new ComparisonCandidateInput(Examples.Candidate().VehicleId, Examples.Candidate().RegistrationNumber,
            costInput: cost, costConfirmation: CostAssumptionConfirmation.Confirm(cost, Examples.ConfirmedAt), sourceRevisions: revisions);
        var result = Examples.One(candidate, new(preferences: [new("netCostSek", 1, EvidenceRequirement.UserConfirmed, 100, 0)]));
        Assert.Equal("ABC101", result.Cost.CandidateKey);
        Assert.Equal("ABC101", result.EffectiveInput.CostInput!.CandidateKey);
        Assert.Equal(revisions, result.EffectiveInput.SourceRevisions);
        Assert.Equal(new ScoreRange(75, 75), result.Score);
    }
}
