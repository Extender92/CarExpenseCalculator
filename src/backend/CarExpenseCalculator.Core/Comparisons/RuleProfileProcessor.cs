using System.Text;

namespace CarExpenseCalculator.Core.Comparisons;

public sealed class RuleProfileProcessor
{
    public RuleProfileInput Normalize(RuleProfileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<ComparisonInputError>();
        var hard = new List<HardRuleInput>();
        var preferences = new List<PreferenceInput>();
        var hardKeys = new HashSet<string>(StringComparer.Ordinal);
        var preferenceKeys = new HashSet<string>(StringComparer.Ordinal);
        Limit(input.HardRules.Count, 50, "rules.hardRules", errors);
        Limit(input.Preferences.Count, 50, "rules.preferences", errors);
        for (var i = 0; i < Math.Min(input.HardRules.Count, 50); i++)
        {
            var rule = input.HardRules[i];
            var path = $"rules.hardRules[{i}]";
            if (rule is null) { Add(errors, path, "required", "A rule cannot be null."); continue; }
            var definition = Definition(rule.CriterionKey, hardKeys, path, errors);
            Evidence(rule.MinimumEvidence, path, errors);
            if (!Enum.IsDefined(rule.Operator)) Add(errors, path + ".operator", "invalidEnum", "Unsupported rule operator.");
            if (definition is null) continue;
            var expected = definition.Kind switch
            {
                ComparisonValueKind.Decimal or ComparisonValueKind.Integer => HardRuleOperator.InclusiveRange,
                ComparisonValueKind.Boolean => HardRuleOperator.Equals,
                ComparisonValueKind.CategorySet => HardRuleOperator.Intersects,
                ComparisonValueKind.Date => HardRuleOperator.MinimumRemainingDays,
                ComparisonValueKind.BudgetStatus => HardRuleOperator.WithinBudget,
                _ => HardRuleOperator.AllowedSet,
            };
            if (rule.Operator != expected) Add(errors, path + ".operator", "invalidOperator", "Operator does not match the criterion.");
            var numeric = IsNumeric(definition);
            if (numeric)
            {
                Bound(rule.Minimum, definition, path + ".minimum", errors);
                Bound(rule.Maximum, definition, path + ".maximum", errors);
                if (rule.Enabled && rule.Minimum is null && rule.Maximum is null) Add(errors, path, "required", "An active numeric rule requires a limit.");
                if (rule.Minimum > rule.Maximum) Add(errors, path, "invalidLimits", "Minimum cannot exceed maximum.");
                if (definition.Kind == ComparisonValueKind.Date && (rule.Maximum is not null || (rule.Enabled && rule.Minimum is null)))
                    Add(errors, path, "invalidLimits", "Inspection uses an inclusive minimum number of remaining days.");
                if (rule.AllowedValues is not null) Add(errors, path + ".allowedValues", "invalidState", "Numeric rules cannot contain categorical choices.");
            }
            else if (rule.Minimum is not null || rule.Maximum is not null) Add(errors, path, "invalidState", "This criterion does not use numeric limits.");
            var categorical = !numeric && definition.Kind != ComparisonValueKind.BudgetStatus;
            var choices = Choices(rule.AllowedValues, definition, path + ".allowedValues", rule.Enabled && categorical, errors);
            if (definition.Kind == ComparisonValueKind.BudgetStatus && choices is not null)
                Add(errors, path + ".allowedValues", "invalidState", "Budget rules use the calculated within-limit status.");
            if (definition.Kind == ComparisonValueKind.Boolean && choices is not null && choices.Count != 1)
                Add(errors, path + ".allowedValues", "invalidState", "Boolean equality requires exactly one choice.");
            hard.Add(new(rule.CriterionKey, rule.Operator, rule.MinimumEvidence, rule.Minimum, rule.Maximum, choices, rule.Enabled));
        }

        for (var i = 0; i < Math.Min(input.Preferences.Count, 50); i++)
        {
            var rule = input.Preferences[i];
            var path = $"rules.preferences[{i}]";
            if (rule is null) { Add(errors, path, "required", "A preference cannot be null."); continue; }
            var definition = Definition(rule.CriterionKey, preferenceKeys, path, errors);
            Evidence(rule.MinimumEvidence, path, errors);
            if (rule.Weight is < 0 or > 5) Add(errors, path + ".weight", "outOfRange", "Weight must be an integer from zero through five.");
            if (definition is null) continue;
            if (definition.Kind == ComparisonValueKind.BudgetStatus) Add(errors, path + ".criterionKey", "unsupportedPreference", "Budget criteria cannot earn preference scores.");
            var numeric = IsNumeric(definition);
            if (numeric)
            {
                Bound(rule.ZeroPoint, definition, path + ".zeroPoint", errors, anchor: true);
                Bound(rule.FullPoint, definition, path + ".fullPoint", errors, anchor: true);
                if (rule.Weight > 0 && (rule.ZeroPoint is null || rule.FullPoint is null)) Add(errors, path, "required", "Active numeric preferences require both anchors.");
                if (rule.ZeroPoint is not null && rule.ZeroPoint == rule.FullPoint) Add(errors, path, "invalidAnchors", "Zero and full anchors must differ.");
                if (rule.PreferredValues is not null) Add(errors, path + ".preferredValues", "invalidState", "Numeric preferences cannot contain categorical choices.");
            }
            else if (rule.ZeroPoint is not null || rule.FullPoint is not null) Add(errors, path, "invalidState", "Categorical preferences do not use numeric anchors.");
            var choices = Choices(rule.PreferredValues, definition, path + ".preferredValues", rule.Weight > 0 && !numeric, errors);
            preferences.Add(new(rule.CriterionKey, rule.Weight, rule.MinimumEvidence, rule.ZeroPoint, rule.FullPoint, choices));
        }

        Limit(input.Signals.Count, Enum.GetValues<ComparisonSignalKey>().Length, "rules.signals", errors);
        var signalKeys = new HashSet<ComparisonSignalKey>();
        for (var i = 0; i < input.Signals.Count; i++)
        {
            var signal = input.Signals[i];
            var path = $"rules.signals[{i}]";
            if (signal is null) { Add(errors, path, "required", "A signal cannot be null."); continue; }
            if (!Enum.IsDefined(signal.Key)) Add(errors, path + ".key", "invalidEnum", "Unsupported signal.");
            if (!signalKeys.Add(signal.Key)) Add(errors, path + ".key", "duplicateKey", "A signal may be selected once.");
            if (signal.Key == ComparisonSignalKey.InspectionValidity)
            {
                if (signal.ShortInspectionDays is null) Add(errors, path + ".shortInspectionDays", "required", "Inspection signals require an explicit day threshold.");
                else if (signal.ShortInspectionDays < 0 || signal.ShortInspectionDays > DateOnly.MaxValue.DayNumber)
                    Add(errors, path + ".shortInspectionDays", "outOfRange", "Threshold is outside the supported day range.");
            }
            else if (signal.ShortInspectionDays is not null) Add(errors, path + ".shortInspectionDays", "invalidState", "Only inspection signals use a day threshold.");
        }
        if (errors.Count > 0) throw new ComparisonInputValidationException(errors);
        return new(hard, preferences, input.Signals);
    }

    internal static bool IsNumeric(ComparisonCriterionDefinition definition) => definition.Kind is
        ComparisonValueKind.Decimal or ComparisonValueKind.Integer or ComparisonValueKind.Date;
    internal static ComparisonCriterionDefinition? Find(string? key) => ComparisonCriterionCatalog.All.FirstOrDefault(x => x.Key == key);

    private static ComparisonCriterionDefinition? Definition(string key, HashSet<string> seen, string path, List<ComparisonInputError> errors)
    {
        var definition = Find(key);
        if (definition is null) Add(errors, path + ".criterionKey", "unsupportedCriterion", "Criterion key is not supported.");
        if (!seen.Add(key)) Add(errors, path + ".criterionKey", "duplicateKey", "Only one entry per criterion is allowed.");
        return definition;
    }
    private static void Evidence(EvidenceRequirement evidence, string path, List<ComparisonInputError> errors)
    {
        if (!Enum.IsDefined(evidence)) Add(errors, path + ".minimumEvidence", "invalidEnum", "Unsupported evidence requirement.");
    }
    private static void Bound(decimal? value, ComparisonCriterionDefinition definition, string path, List<ComparisonInputError> errors, bool anchor = false)
    {
        if (value is null) return;
        var min = definition.Kind == ComparisonValueKind.Date ? -DateOnly.MaxValue.DayNumber : definition.Minimum;
        var max = definition.Kind == ComparisonValueKind.Date ? DateOnly.MaxValue.DayNumber : definition.Maximum;
        if (value < min || value > max) Add(errors, path, "outOfRange", "Value is outside the criterion domain.");
        if (!anchor && definition.Kind is ComparisonValueKind.Integer or ComparisonValueKind.Date && decimal.Truncate(value.Value) != value)
            Add(errors, path, "invalidInteger", "This rule requires a whole-number limit.");
    }
    private static IReadOnlyList<ComparisonChoice>? Choices(IReadOnlyList<ComparisonChoice>? values,
        ComparisonCriterionDefinition definition, string path, bool required, List<ComparisonInputError> errors)
    {
        if (required && (values is null || values.Count == 0)) Add(errors, path, "required", "An active categorical rule requires choices.");
        if (values is null) return null;
        Limit(values.Count, 50, path, errors);
        var normalized = new List<ComparisonChoice>();
        for (var i = 0; i < Math.Min(values.Count, 50); i++)
        {
            var value = values[i];
            var itemPath = $"{path}[{i}]";
            if (value is null) { Add(errors, itemPath, "required", "A choice cannot be null."); continue; }
            var count = new object?[] { value.Boolean, value.Transmission, value.FuelType, value.BodyType,
                value.Drivetrain, value.ServiceDocumentation, value.Text }.Count(x => x is not null);
            var matches = definition.Key switch
            {
                "towBar" => value.Boolean is not null,
                "transmission" => value.Transmission is not null && Enum.IsDefined(value.Transmission.Value),
                "fuelTypes" => value.FuelType is not null && Enum.IsDefined(value.FuelType.Value),
                "bodyType" => value.BodyType is not null && Enum.IsDefined(value.BodyType.Value),
                "drivetrain" => value.Drivetrain is not null && Enum.IsDefined(value.Drivetrain.Value),
                "serviceDocumentation" => value.ServiceDocumentation is not null && Enum.IsDefined(value.ServiceDocumentation.Value),
                "locality" or "county" => value.Text is not null,
                _ => false,
            };
            if (count != 1 || !matches) Add(errors, itemPath, "invalidChoice", "Choice must contain exactly one supported value matching the criterion.");
            if (value.Text is not null)
            {
                try { value = value with { Text = value.Text.Trim().Normalize(NormalizationForm.FormC) }; }
                catch (ArgumentException) { Add(errors, itemPath + ".text", "invalidText", "Text must contain valid Unicode."); }
                if (value.Text!.Length is 0 or > VehicleFactLimits.MaximumLocationLength) Add(errors, itemPath + ".text", "outOfRange", "Text must contain 1-100 characters.");
            }
            if (normalized.Any(x => ChoiceEquals(x, value))) Add(errors, itemPath, "duplicateValue", "Duplicate choices are not allowed.");
            normalized.Add(value);
        }
        return normalized.AsReadOnly();
    }
    internal static bool ChoiceEquals(ComparisonChoice a, ComparisonChoice b) => a.Text is not null && b.Text is not null
        ? string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase) : a == b;
    internal static void Limit(int count, int maximum, string path, List<ComparisonInputError> errors)
    {
        if (count > maximum) Add(errors, path, "tooManyItems", $"At most {maximum} items are allowed.");
    }
    internal static void Add(List<ComparisonInputError> errors, string path, string code, string message) => errors.Add(new(path, code, message));
}
