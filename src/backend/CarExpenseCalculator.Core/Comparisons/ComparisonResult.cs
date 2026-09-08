using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

public static class ComparisonVersions { public const int Rules = 1; public const int ResultSchema = 1; }
public enum HardRuleState { Pass, Fail, NeedsVerification }
public enum BuyingEligibility { Eligible, NeedsVerification, Rejected }
public enum ComparisonSignalKind { Information, Warning, Positive }

public sealed record ScoreRange(decimal Lower, decimal Upper);

public sealed class ComparisonObservedValue(decimal? number = null, ComparisonChoice? choice = null,
    DateOnly? date = null, IEnumerable<FuelType>? fuels = null)
{
    public decimal? Number { get; } = number;
    public ComparisonChoice? Choice { get; } = choice;
    public DateOnly? Date { get; } = date;
    public IReadOnlyList<FuelType>? Fuels { get; } = fuels is null ? null : Array.AsReadOnly(fuels.ToArray());
}

public sealed class CriterionAssessment
{
    internal CriterionAssessment(string key, ComparisonObservedValue? actual, IEnumerable<ComparisonEvidence> evidence,
        EvidenceRequirement requirement, bool adequate, IEnumerable<string> reasons, IEnumerable<ComparisonInputError> errors)
    {
        CriterionKey = key; Actual = actual; Evidence = Array.AsReadOnly(evidence.ToArray());
        MinimumEvidence = requirement; HasAdequateEvidence = adequate;
        Reasons = Array.AsReadOnly(reasons.Distinct().ToArray()); Errors = Array.AsReadOnly(errors.ToArray());
    }
    public string CriterionKey { get; }
    public ComparisonObservedValue? Actual { get; }
    public IReadOnlyList<ComparisonEvidence> Evidence { get; }
    public EvidenceRequirement MinimumEvidence { get; }
    public bool HasAdequateEvidence { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<ComparisonInputError> Errors { get; }
}

public sealed record HardRuleEvaluation(HardRuleInput Rule, HardRuleState State,
    CriterionAssessment Assessment, bool? ObservedConditionSatisfied, string ReasonCode, string Explanation);
public sealed record PreferenceContribution(PreferenceInput Preference, CriterionAssessment Assessment,
    ScoreRange Range, ScoreRange WeightedContribution, string Explanation);
public sealed record ComparisonSignal(ComparisonSignalKey Key, ComparisonSignalKind Kind, string ReasonCode,
    string Explanation, ComparisonEvidence? Evidence = null);

public sealed record CurrentEvaluation
{
    internal CurrentEvaluation(ComparisonCandidateInput input, VehicleCostResult cost,
        IEnumerable<ComparisonInputError> errors, IEnumerable<HardRuleEvaluation> hardRules,
        BuyingEligibility eligibility, IEnumerable<PreferenceContribution> contributions,
        ScoreRange? score, decimal? coverage, IEnumerable<ComparisonSignal> signals)
    {
        EffectiveInput = input; Cost = cost; Errors = Array.AsReadOnly(errors.ToArray());
        HardRules = Array.AsReadOnly(hardRules.ToArray()); Eligibility = eligibility;
        Contributions = Array.AsReadOnly(contributions.ToArray()); Score = score; CoveragePercent = coverage;
        Signals = Array.AsReadOnly(signals.ToArray());
    }
    public ComparisonCandidateInput EffectiveInput { get; }
    public VehicleCostResult Cost { get; }
    public IReadOnlyList<ComparisonInputError> Errors { get; }
    public IReadOnlyList<HardRuleEvaluation> HardRules { get; }
    public BuyingEligibility Eligibility { get; }
    public IReadOnlyList<PreferenceContribution> Contributions { get; }
    public ScoreRange? Score { get; }
    public decimal? CoveragePercent { get; }
    public string? ScoreUnavailableReason => Score is null ? "noActiveCriteria" : null;
    public IReadOnlyList<ComparisonSignal> Signals { get; }
    public bool IsCheapestEligibleComplete { get; internal init; }
    public bool IsDefinitePreferenceWinner { get; internal init; }
}

public sealed class ComparisonPreview
{
    internal ComparisonPreview(HouseholdProfileInput profile, RuleProfileInput rules, DateOnly asOfDate,
        IEnumerable<HouseholdInputError> profileErrors, IEnumerable<CurrentEvaluation> candidates,
        IEnumerable<Guid> costOrder, IEnumerable<Guid> scoreOrder, string preferenceRecommendationReason)
    {
        Profile = profile; Rules = rules; AsOfDate = asOfDate;
        ProfileErrors = Array.AsReadOnly(profileErrors.ToArray()); Candidates = Array.AsReadOnly(candidates.ToArray());
        CostOrder = Array.AsReadOnly(costOrder.ToArray()); ScoreOrder = Array.AsReadOnly(scoreOrder.ToArray());
        PreferenceRecommendationReason = preferenceRecommendationReason;
    }
    public int RuleVersion => ComparisonVersions.Rules;
    public int ResultSchemaVersion => ComparisonVersions.ResultSchema;
    public int CalculationVersion => HouseholdCalculationVersions.Calculation;
    public int HouseholdResultSchemaVersion => HouseholdCalculationVersions.ResultSchema;
    public HouseholdProfileInput Profile { get; }
    public RuleProfileInput Rules { get; }
    public DateOnly AsOfDate { get; }
    public IReadOnlyList<HouseholdInputError> ProfileErrors { get; }
    public IReadOnlyList<CurrentEvaluation> Candidates { get; }
    public IReadOnlyList<Guid> CostOrder { get; }
    public IReadOnlyList<Guid> ScoreOrder { get; }
    public string PreferenceRecommendationReason { get; }
}
