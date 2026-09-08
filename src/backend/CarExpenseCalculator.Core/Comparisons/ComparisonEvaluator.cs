using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Core.Comparisons;

public sealed class ComparisonEvaluator
{
    public ComparisonPreview EvaluateComparison(HouseholdProfileInput profile, RuleProfileInput rules,
        DateOnly asOfDate, IReadOnlyList<ComparisonCandidateInput> candidates)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count > 100) throw new ComparisonInputValidationException([
            new("candidates", "tooManyItems", "At most 100 candidates are allowed.")]);
        var normalizedRules = new RuleProfileProcessor().Normalize(rules);
        var snapshot = candidates.ToArray();
        ValidateCandidates(snapshot);
        var inputs = snapshot.Select(x => x.CostInput is { } cost
            ? cost with { CandidateKey = x.RegistrationNumber.Value } : new VehicleCostInput(x.RegistrationNumber.Value, null)).ToArray();
        var calculation = new HouseholdCostCalculator().CalculateForComparison(profile, inputs);
        var work = snapshot.Select((x, index) => Evaluate(x, normalizedRules, asOfDate,
            ComparisonReviewCompleteness.Apply(calculation.Vehicles[index], x.ReviewItems), index)).ToArray();
        var costOrder = work.OrderBy(x => x.Result.Eligibility == BuyingEligibility.Rejected)
            .ThenBy(x => x.Cost is null).ThenBy(x => x.Cost)
            .ThenBy(x => x.Result.EffectiveInput.RegistrationNumber.Value, StringComparer.Ordinal).ToArray();
        var scoreOrder = work.OrderBy(x => x.Result.Eligibility == BuyingEligibility.Rejected)
            .ThenBy(x => x.Lower is null).ThenByDescending(x => x.Lower)
            .ThenBy(x => x.Result.EffectiveInput.RegistrationNumber.Value, StringComparer.Ordinal).ToArray();
        var cheapest = work.Where(x => x.Result.Eligibility == BuyingEligibility.Eligible && x.Cost is not null)
            .Select(x => x.Cost).DefaultIfEmpty(null).Min();
        var winner = work.FirstOrDefault(x => x.Result.Eligibility == BuyingEligibility.Eligible && x.Lower is not null &&
            work.Where(other => other != x && other.Result.Eligibility != BuyingEligibility.Rejected)
                .All(other => other.Upper is not null && x.Lower > other.Upper));
        var reason = work.All(x => x.Lower is null) ? "noActiveCriteria" : winner is not null ? "definiteWinner"
            : work.All(x => x.Result.Eligibility != BuyingEligibility.Eligible) ? "noEligibleCandidate" : "overlapOrTie";
        var results = work.Select(x => x.Result with
        {
            IsCheapestEligibleComplete = cheapest is not null && x.Cost == cheapest && x.Result.Eligibility == BuyingEligibility.Eligible,
            IsDefinitePreferenceWinner = ReferenceEquals(x, winner),
        });
        return new(profile, normalizedRules, asOfDate, calculation.Preview.ProfileErrors, results,
            costOrder.Select(x => x.Result.EffectiveInput.VehicleId), scoreOrder.Select(x => x.Result.EffectiveInput.VehicleId), reason);
    }

    private static EvaluationWork Evaluate(ComparisonCandidateInput input, RuleProfileInput rules, DateOnly date,
        HouseholdComparisonVehicle costs, int index)
    {
        var processor = new VehicleFactsProcessor();
        var (facts, factErrors) = processor.NormalizeForEvaluation(input.Facts, input.CostInput?.AcquisitionType ?? AcquisitionType.Purchase);
        var errors = factErrors.Select(x => new ComparisonInputError($"candidates[{index}].facts.{x.Path}", x.Code, x.Message))
            .Concat(costs.Result.InputErrors.Select(x => new ComparisonInputError(ComparisonCriteria.Path(x.Path, index), x.Code, x.Message))).ToList();
        var notes = input.ConditionNotes?.Select((note, noteIndex) =>
        {
            var (value, problems) = processor.NormalizeForEvaluation(new() { ServiceNotes = note });
            errors.AddRange(problems.Select(x => new ComparisonInputError(
                $"candidates[{index}].conditionNotes[{noteIndex}]" + x.Path["serviceNotes".Length..], x.Code, x.Message)));
            if (note is null) errors.Add(new($"candidates[{index}].conditionNotes[{noteIndex}]", "required", "A condition note cannot be null."));
            foreach (var observation in value.ServiceNotes!.Observations)
                if (observation.Value.Length > 300) errors.Add(new($"candidates[{index}].conditionNotes[{noteIndex}]", "tooLong", "A reviewed condition note cannot exceed 300 characters."));
            return value.ServiceNotes!;
        }).ToArray();
        var normalized = new ComparisonCandidateInput(input.VehicleId, input.RegistrationNumber, facts,
            input.CostInput is { } costInput ? costInput with { CandidateKey = input.RegistrationNumber.Value } : null,
            input.CostConfirmation?.Matches(input.CostInput) == true ? input.CostConfirmation : null,
            input.SourceRevisions, input.ReviewItems, notes);
        var criteria = new ComparisonCriteria(normalized, costs, errors, index, date);
        var hard = rules.HardRules.Where(x => x.Enabled).Select(rule =>
        {
            var assessment = criteria.Assess(rule.CriterionKey, rule.MinimumEvidence);
            var observed = Satisfies(rule, assessment.Actual);
            var state = !assessment.HasAdequateEvidence ? HardRuleState.NeedsVerification
                : observed == true ? HardRuleState.Pass : HardRuleState.Fail;
            var reason = state switch { HardRuleState.Pass => "requirementMet", HardRuleState.Fail => "requirementNotMet", _ => assessment.Reasons.FirstOrDefault() ?? "unavailable" };
            return new HardRuleEvaluation(rule, state, assessment, observed, reason, ComparisonText.Hard(rule, assessment, state, observed));
        }).ToArray();
        var eligibility = hard.Any(x => x.State == HardRuleState.Fail) ? BuyingEligibility.Rejected
            : hard.Any(x => x.State == HardRuleState.NeedsVerification) ? BuyingEligibility.NeedsVerification : BuyingEligibility.Eligible;
        var weight = rules.Preferences.Sum(x => x.Weight);
        decimal known = 0, unknownWeight = 0, knownWeight = 0;
        var contributions = new List<PreferenceContribution>();
        foreach (var preference in rules.Preferences.Where(x => x.Weight > 0))
        {
            var assessment = criteria.Assess(preference.CriterionKey, preference.MinimumEvidence);
            decimal? score = null;
            if (assessment.HasAdequateEvidence)
            {
                try { score = Score(preference, assessment.Actual!); }
                catch (OverflowException)
                {
                    var error = new ComparisonInputError($"candidates[{index}].scores.{preference.CriterionKey}", "calculationOutOfRange", "Score arithmetic exceeds decimal capacity.");
                    errors.Add(error);
                    assessment = new(assessment.CriterionKey, assessment.Actual, assessment.Evidence, assessment.MinimumEvidence, false,
                        assessment.Reasons.Append(error.Code), assessment.Errors.Append(error));
                }
            }
            if (score is { } s) { known += preference.Weight * s; knownWeight += preference.Weight; }
            else unknownWeight += preference.Weight;
            var range = new ScoreRange(score ?? 0, score ?? 100);
            contributions.Add(new(preference, assessment, Round(range),
                Round(new ScoreRange(range.Lower * preference.Weight / weight, range.Upper * preference.Weight / weight)),
                ComparisonText.Preference(preference, assessment, range)));
        }
        decimal? lower = weight == 0 ? null : known / weight;
        decimal? upper = weight == 0 ? null : (known + 100 * unknownWeight) / weight;
        var signals = ComparisonSignals.Create(normalized, costs.Result, rules.Signals, criteria, errors, index);
        var result = new CurrentEvaluation(normalized, costs.Result, errors, hard, eligibility, contributions,
            lower is null ? null : Round(new ScoreRange(lower.Value, upper!.Value)), weight == 0 ? null : Round(100 * knownWeight / weight), signals);
        return new(result, costs.NetCostSek, lower, upper);
    }

    private static bool? Satisfies(HardRuleInput rule, ComparisonObservedValue? value)
    {
        if (value is null) return null;
        return rule.Operator switch
        {
            HardRuleOperator.InclusiveRange or HardRuleOperator.MinimumRemainingDays =>
                value.Number is { } number ? (rule.Minimum is null || number >= rule.Minimum) && (rule.Maximum is null || number <= rule.Maximum) : null,
            HardRuleOperator.WithinBudget => value.Choice?.Boolean,
            _ => Matches(rule.AllowedValues!, value),
        };
    }
    private static decimal Score(PreferenceInput preference, ComparisonObservedValue value)
    {
        if (value.Number is not { } number) return Matches(preference.PreferredValues!, value) ? 100 : 0;
        var zero = preference.ZeroPoint!.Value;
        var full = preference.FullPoint!.Value;
        if (full > zero)
        {
            if (number <= zero) return 0;
            if (number >= full) return 100;
        }
        else
        {
            if (number >= zero) return 0;
            if (number <= full) return 100;
        }
        var delta = number - zero;
        var span = full - zero;
        // Preserve representable tiny scores; only divide first when multiplying the
        // numerator would overflow. Neither path uses presentation-rounded values.
        decimal numerator;
        try { numerator = delta * 100m; }
        catch (OverflowException) { return delta / span * 100m; }
        return numerator / span;
    }
    private static bool Matches(IReadOnlyList<ComparisonChoice> choices, ComparisonObservedValue value) => value.Fuels is { } fuels
        ? choices.Any(x => x.FuelType is { } fuel && fuels.Contains(fuel))
        : value.Choice is { } choice && choices.Any(x => RuleProfileProcessor.ChoiceEquals(x, choice));
    internal static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static ScoreRange Round(ScoreRange range) => new(Round(range.Lower), Round(range.Upper));
    private sealed record EvaluationWork(CurrentEvaluation Result, decimal? Cost, decimal? Lower, decimal? Upper);

    private static void ValidateCandidates(IReadOnlyList<ComparisonCandidateInput> candidates)
    {
        var errors = new List<ComparisonInputError>();
        RuleProfileProcessor.Limit(candidates.Count, 100, "candidates", errors);
        var ids = new HashSet<Guid>();
        var registrations = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Math.Min(candidates.Count, 100); i++)
        {
            var item = candidates[i];
            var path = $"candidates[{i}]";
            if (item is null) { errors.Add(new(path, "required", "A candidate cannot be null.")); continue; }
            if (item.VehicleId == Guid.Empty) errors.Add(new(path + ".vehicleId", "required", "An existing vehicle UUID is required."));
            if (!ids.Add(item.VehicleId)) errors.Add(new(path + ".vehicleId", "duplicateKey", "Vehicle UUID is duplicated."));
            if (item.RegistrationNumber is null) errors.Add(new(path + ".registrationNumber", "required", "Registration is required."));
            else
            {
                if (!registrations.Add(item.RegistrationNumber.Value)) errors.Add(new(path + ".registrationNumber", "duplicateKey", "Registration is duplicated."));
                if (item.CostInput is { } input && (!RegistrationNumber.TryParse(input.CandidateKey, out var reg) || reg != item.RegistrationNumber))
                    errors.Add(new(path + ".costInput.candidateKey", "identityMismatch", "Cost input must belong to the same registration."));
            }
            RuleProfileProcessor.Limit(item.ConditionNotes?.Count ?? 0, 10, path + ".conditionNotes", errors);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < item.ReviewItems.Count; j++)
            {
                var review = item.ReviewItems[j];
                var reviewPath = $"{path}.reviewItems[{j}]";
                if (review is null) { errors.Add(new(reviewPath, "required", "Review item is required.")); continue; }
                if (string.IsNullOrWhiteSpace(review.Key) || review.Key.Length > 120 || review.Key != review.Key.Trim())
                    errors.Add(new(reviewPath + ".key", "invalidKey", "Review keys must contain 1-120 trimmed characters."));
                if (!keys.Add(review.Key)) errors.Add(new(reviewPath + ".key", "duplicateKey", "Review key is duplicated."));
                if (string.IsNullOrWhiteSpace(review.Reason) || review.Reason.Length > 1000)
                    errors.Add(new(reviewPath + ".reason", "invalidText", "Review reason must contain 1-1000 characters."));
                if (review.AffectedSections.Count == 0 || review.AffectedSections.Any(x => !Enum.IsDefined(x)) ||
                    review.AffectedSections.Distinct().Count() != review.AffectedSections.Count)
                    errors.Add(new(reviewPath + ".affectedSections", "invalidState", "Review requires distinct supported affected sections."));
            }
        }
        if (errors.Count > 0) throw new ComparisonInputValidationException(errors);
    }
}
