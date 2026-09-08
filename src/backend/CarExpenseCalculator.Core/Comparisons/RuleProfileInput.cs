using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

public enum EvidenceRequirement { Advertised, UserConfirmed, RegistryVerified }
public enum HardRuleOperator { InclusiveRange, Equals, AllowedSet, Intersects, MinimumRemainingDays, WithinBudget }
public enum ComparisonSignalKey { ConditionNotes, ServiceDocumentation, InspectionValidity, CostCompleteness, StartupBudget, MonthlyBudget }

// Exactly one supported, criterion-appropriate member is required by validation.
public sealed record ComparisonChoice(bool? Boolean = null, Transmission? Transmission = null,
    FuelType? FuelType = null, BodyType? BodyType = null, Drivetrain? Drivetrain = null,
    ServiceDocumentationStatus? ServiceDocumentation = null, string? Text = null);

public sealed class HardRuleInput(string criterionKey, HardRuleOperator @operator, EvidenceRequirement minimumEvidence,
    decimal? minimum = null, decimal? maximum = null, IEnumerable<ComparisonChoice>? allowedValues = null, bool enabled = true)
{
    public string CriterionKey { get; } = criterionKey;
    public HardRuleOperator Operator { get; } = @operator;
    public EvidenceRequirement MinimumEvidence { get; } = minimumEvidence;
    public decimal? Minimum { get; } = minimum;
    public decimal? Maximum { get; } = maximum;
    public IReadOnlyList<ComparisonChoice>? AllowedValues { get; } = allowedValues is null ? null : Array.AsReadOnly(allowedValues.ToArray());
    public bool Enabled { get; } = enabled;
}

public sealed class PreferenceInput(string criterionKey, int weight, EvidenceRequirement minimumEvidence,
    decimal? zeroPoint = null, decimal? fullPoint = null, IEnumerable<ComparisonChoice>? preferredValues = null)
{
    public string CriterionKey { get; } = criterionKey;
    public int Weight { get; } = weight;
    public EvidenceRequirement MinimumEvidence { get; } = minimumEvidence;
    public decimal? ZeroPoint { get; } = zeroPoint;
    public decimal? FullPoint { get; } = fullPoint;
    public IReadOnlyList<ComparisonChoice>? PreferredValues { get; } = preferredValues is null ? null : Array.AsReadOnly(preferredValues.ToArray());
}

public sealed record ComparisonSignalInput(ComparisonSignalKey Key, int? ShortInspectionDays = null);

public sealed class RuleProfileInput(IEnumerable<HardRuleInput>? hardRules = null,
    IEnumerable<PreferenceInput>? preferences = null, IEnumerable<ComparisonSignalInput>? signals = null)
{
    public IReadOnlyList<HardRuleInput> HardRules { get; } = Array.AsReadOnly(hardRules?.ToArray() ?? []);
    public IReadOnlyList<PreferenceInput> Preferences { get; } = Array.AsReadOnly(preferences?.ToArray() ?? []);
    public IReadOnlyList<ComparisonSignalInput> Signals { get; } = Array.AsReadOnly(signals?.ToArray() ?? []);
}

public sealed record ComparisonInputError(string Path, string Code, string Message);
public sealed class ComparisonInputValidationException(IEnumerable<ComparisonInputError> errors)
    : ArgumentException("Comparison input structure or rule profile is invalid.")
{
    public IReadOnlyList<ComparisonInputError> Errors { get; } = Array.AsReadOnly(errors.ToArray());
}
