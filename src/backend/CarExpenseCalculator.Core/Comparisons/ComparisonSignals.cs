using CarExpenseCalculator.Core.Households;

namespace CarExpenseCalculator.Core.Comparisons;

internal static class ComparisonSignals
{
    internal static IReadOnlyList<ComparisonSignal> Create(ComparisonCandidateInput candidate, VehicleCostResult cost,
        IReadOnlyList<ComparisonSignalInput> selected, ComparisonCriteria criteria, IReadOnlyList<ComparisonInputError> errors, int index)
    {
        var result = new List<ComparisonSignal>();
        foreach (var signal in selected)
        {
            switch (signal.Key)
            {
                case ComparisonSignalKey.ConditionNotes:
                    if (candidate.ConditionNotes is null)
                        result.Add(new(signal.Key, ComparisonSignalKind.Information, "conditionNotesUnknown", "Skick- och reparationsuppgifter saknas."));
                    else for (var i = 0; i < candidate.ConditionNotes.Count; i++)
                    {
                        var path = $"candidates[{index}].conditionNotes[{i}]";
                        if (errors.Any(x => x.Path.StartsWith(path, StringComparison.Ordinal)))
                            result.Add(new(signal.Key, ComparisonSignalKind.Warning, "invalidConditionNote", "En skickuppgift har ogiltigt underlag och behöver granskas."));
                        else foreach (var observation in candidate.ConditionNotes[i].Observations)
                            result.Add(new(signal.Key, ComparisonSignalKind.Information, "reportedConditionNote",
                                $"{ComparisonText.Source(observation.Evidence)}: {observation.Value}", observation.Evidence));
                    }
                    break;
                case ComparisonSignalKey.ServiceDocumentation:
                {
                    var value = criteria.Get("serviceDocumentation");
                    var known = value.Errors.Count == 0 && value.Reasons.Count == 0;
                    var positive = known && value.Actual?.Choice?.ServiceDocumentation == ServiceDocumentationStatus.Documented;
                    result.Add(new(signal.Key, positive ? ComparisonSignalKind.Positive : ComparisonSignalKind.Warning,
                        positive ? "documentedService" : "unclearServiceDocumentation",
                        $"{ComparisonText.Source(value.Evidence.FirstOrDefault())}: serviceunderlag {ComparisonText.Actual(value.Actual)}." +
                        (known ? "" : " Underlaget behöver granskas."), value.Evidence.FirstOrDefault()));
                    break;
                }
                case ComparisonSignalKey.InspectionValidity:
                {
                    var value = criteria.Get("inspectionValidThrough");
                    var days = value.Errors.Count == 0 && value.Reasons.Count == 0 ? value.Actual?.Number : null;
                    var code = days is null ? "inspectionUnknown" : days < 0 ? "inspectionExpired"
                        : days < signal.ShortInspectionDays ? "inspectionShort" : "inspectionMeetsThreshold";
                    var text = code switch
                    {
                        "inspectionUnknown" => "Besiktningsgiltigheten behöver verifieras.",
                        "inspectionExpired" => "Angiven besiktningsgiltighet har gått ut.",
                        "inspectionShort" => $"Angiven besiktningsgiltighet är kortare än din gräns på {signal.ShortInspectionDays} dagar.",
                        _ => $"Angiven besiktningsgiltighet når din gräns på {signal.ShortInspectionDays} dagar.",
                    };
                    result.Add(new(signal.Key, code == "inspectionMeetsThreshold" ? ComparisonSignalKind.Positive : ComparisonSignalKind.Warning,
                        code, $"{ComparisonText.Source(value.Evidence.FirstOrDefault())}: {text}", value.Evidence.FirstOrDefault()));
                    break;
                }
                case ComparisonSignalKey.CostCompleteness:
                    var complete = cost.Totals.OwnershipCost.State == CostSectionState.Complete;
                    result.Add(new(signal.Key, complete ? ComparisonSignalKind.Positive : ComparisonSignalKind.Warning,
                        complete ? "completeCostEstimate" : "incompleteCostEstimate", complete
                            ? "Den uppskattade ägandekostnaden har komplett beräkningsunderlag."
                            : "Den uppskattade ägandekostnaden är ofullständig. Kända delkostnader är inte en komplett total."));
                    break;
                case ComparisonSignalKey.StartupBudget:
                case ComparisonSignalKey.MonthlyBudget:
                    var budget = signal.Key == ComparisonSignalKey.StartupBudget ? cost.StartupBudget : cost.MonthlyBudget;
                    var label = signal.Key == ComparisonSignalKey.StartupBudget ? "Startbudgeten" : "Månadsbudgeten";
                    result.Add(new(signal.Key, budget.Status == HouseholdBudgetStatus.WithinLimit ? ComparisonSignalKind.Positive : ComparisonSignalKind.Warning,
                        "budget" + budget.Status, label + (budget.Status switch
                        {
                            HouseholdBudgetStatus.WithinLimit => " ryms enligt aktuella kalkylantaganden.",
                            HouseholdBudgetStatus.Exceeded => " överskrids enligt aktuella kalkylantaganden.",
                            HouseholdBudgetStatus.NotConfigured => " saknar angiven gräns.",
                            HouseholdBudgetStatus.Invalid => " kan inte bedömas på grund av ogiltiga uppgifter.",
                            _ => " kan inte bedömas fullständigt eftersom uppgifter saknas.",
                        })));
                    break;
            }
        }
        return result.AsReadOnly();
    }
}
