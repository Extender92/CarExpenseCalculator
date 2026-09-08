using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

internal sealed record CriterionObservation(ComparisonObservedValue? Actual, IReadOnlyList<ComparisonEvidence> Evidence,
    IReadOnlyList<string> Reasons, IReadOnlyList<ComparisonInputError> Errors, bool IsEstimate = false, bool AuthoritativeBudget = false);

internal sealed class ComparisonCriteria(ComparisonCandidateInput candidate, HouseholdComparisonVehicle costs,
    IReadOnlyList<ComparisonInputError> errors, int index, DateOnly asOfDate)
{
    internal CriterionObservation Get(string key)
    {
        var f = candidate.Facts;
        return key switch
        {
            "purchasePriceSek" => Price(),
            "odometerKilometres" => Fact(key, f.OdometerKilometres, x => new(number: x)),
            "ownerCount" => Fact(key, f.OwnerCount, x => new(number: x)),
            "towBar" => Fact(key, f.TowBar, x => new(choice: new(Boolean: x))),
            "transmission" => Fact(key, f.Transmission, x => new(choice: new(Transmission: x))),
            "seats" => Fact(key, f.Seats, x => new(number: x)),
            "modelYear" => Fact(key, f.ModelYear, x => new(number: x)),
            "fuelTypes" => Fact(key, f.FuelTypes, x => new(fuels: x.Values)),
            "bodyType" => Fact(key, f.BodyType, x => new(choice: new(BodyType: x))),
            "drivetrain" => Fact(key, f.Drivetrain, x => new(choice: new(Drivetrain: x))),
            "locality" => Fact(key, f.Locality, x => new(choice: new(Text: x))),
            "county" => Fact(key, f.County, x => new(choice: new(Text: x))),
            "towingCapacityKilograms" => Fact(key, f.TowingCapacityKilograms, x => new(number: x)),
            "inspectionValidThrough" => Fact(key, f.InspectionValidThrough, x => new(number: x.DayNumber - asOfDate.DayNumber, date: x)),
            "serviceDocumentation" => Fact(key, f.ServiceDocumentation, x => new(choice: new(ServiceDocumentation: x))),
            "netCostSek" => Cost(costs.NetCostSek, costs.Result.Totals.OwnershipCost),
            "costPerMonthSek" => Cost(costs.CostPerMonthSek, costs.Result.Totals.MonthlyCost),
            "costPerMilSek" => Cost(costs.CostPerMilSek, costs.Result.Totals.CostPerMil),
            "startupBudget" => Budget(costs.Result.StartupBudget),
            "monthlyBudget" => Budget(costs.Result.MonthlyBudget),
            _ => throw new InvalidOperationException("Validated criterion is unsupported."),
        };
    }

    internal CriterionAssessment Assess(string key, EvidenceRequirement requirement)
    {
        var value = Get(key);
        var reasons = value.Reasons.ToList();
        var adequate = value.Actual is not null && (value.Errors.Count == 0 || value.AuthoritativeBudget) && reasons.Count == 0;
        var evidenceMatches = requirement switch
        {
            EvidenceRequirement.Advertised => value.IsEstimate || value.Evidence.Count == 1,
            EvidenceRequirement.UserConfirmed => value.Evidence.Count == 1 && value.Evidence[0].Verification == VerificationStatus.UserConfirmed,
            // Public normalization rejects all manual registry claims. A future permitted
            // provider must introduce its own authenticated boundary before this can pass.
            EvidenceRequirement.RegistryVerified => false,
            _ => false,
        };
        if (!evidenceMatches)
            reasons.Add(value.IsEstimate && requirement == EvidenceRequirement.RegistryVerified ? "estimatedValueNotRegistryVerifiable" : "insufficientEvidence");
        return new(key, value.Actual, value.Evidence, requirement, adequate && evidenceMatches, reasons, value.Errors);
    }

    private CriterionObservation Fact<T>(string key, VehicleFact<T>? fact, Func<T, ComparisonObservedValue> map) where T : notnull
    {
        var path = $"candidates[{index}].facts.{key}";
        var fieldErrors = errors.Where(x => x.Path == path || x.Path.StartsWith(path + ".", StringComparison.Ordinal)).ToArray();
        var reasons = new List<string>();
        if (fieldErrors.Length > 0) reasons.Add("invalidFact");
        if (fact is null || fact.State == VehicleFactState.Unknown) reasons.Add("unknownFact");
        else if (fact.State == VehicleFactState.NotApplicable) reasons.Add("notApplicable");
        else if (fact.State == VehicleFactState.Conflicting) reasons.Add("conflictingFacts");
        var actual = fact?.State == VehicleFactState.Known && fact.Observations.Count == 1 ? map(fact.Observations[0].Value) : null;
        return new(actual, Array.AsReadOnly(fact?.Observations.Where(x => x?.Evidence is not null).Select(x => x.Evidence).ToArray() ?? []),
            reasons.AsReadOnly(), Array.AsReadOnly(fieldErrors));
    }

    private CriterionObservation Price()
    {
        if (candidate.CostInput is not { AcquisitionType: AcquisitionType.Purchase } input)
            return Fact("purchasePriceSek", candidate.Facts.PurchasePriceSek, x => new(number: x));
        var fieldErrors = errors.Where(x => x.Path == $"candidates[{index}].costInput.priceSek").ToArray();
        if (input.PriceSek is not { } price) return new(null, [], ["unknownFact"], fieldErrors);
        if (candidate.CostConfirmation?.IsApplicableTo(input) == true)
            return new(new(number: price), ConfirmationEvidence(), fieldErrors.Length == 0 ? [] : ["invalidFact"], fieldErrors);
        var source = Fact("purchasePriceSek", candidate.Facts.PurchasePriceSek, x => new(number: x));
        if (source.Actual?.Number == price && source.Reasons.Count == 0 && fieldErrors.Length == 0) return source;
        return new(new(number: price), [], fieldErrors.Length == 0 ? ["effectivePriceNeedsConfirmation"] : ["invalidFact"], fieldErrors);
    }

    private IReadOnlyList<ComparisonEvidence> ConfirmationEvidence() => candidate.CostConfirmation?.IsApplicableTo(candidate.CostInput) == true
        ? Array.AsReadOnly(new[] { new ComparisonEvidence(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed,
            ConfirmedAt: candidate.CostConfirmation.ConfirmedAt) }) : [];

    private CriterionObservation Cost(decimal? value, CostSectionResult part) => new(
        value is null ? null : new(number: value), ConfirmationEvidence(),
        Array.AsReadOnly(part.MissingComponents.Concat(value is null ? new[] { "incompleteCost" } : []).Distinct().ToArray()),
        Array.AsReadOnly(part.Errors.Select(x => new ComparisonInputError(Path(x.Path, index), x.Code, x.Message)).ToArray()), true);

    private CriterionObservation Budget(HouseholdBudgetResult budget)
    {
        var known = budget.Status is HouseholdBudgetStatus.WithinLimit or HouseholdBudgetStatus.Exceeded;
        return new(known ? new(choice: new(Boolean: budget.Status == HouseholdBudgetStatus.WithinLimit)) : null,
            ConfirmationEvidence(), known ? [] : [budget.Status switch
            {
                HouseholdBudgetStatus.NotConfigured => "budgetNotConfigured",
                HouseholdBudgetStatus.Invalid => "invalidBudget",
                _ => "unknownBudget",
            }], Array.AsReadOnly(budget.FundingRequired.Errors.Select(x => new ComparisonInputError(Path(x.Path, index), x.Code, x.Message)).ToArray()), true, known);
    }

    internal static string Path(string path, int index) => path.StartsWith($"vehicles[{index}]", StringComparison.Ordinal)
        ? $"candidates[{index}].costInput" + path[$"vehicles[{index}]".Length..] : path;
}
