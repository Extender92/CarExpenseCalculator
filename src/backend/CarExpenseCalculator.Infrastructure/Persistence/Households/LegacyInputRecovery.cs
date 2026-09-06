using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

internal static class LegacyInputRecovery
{
    public static RecoveredLegacyInput Recover(VehicleEntity vehicle)
    {
        var entity = vehicle.Scenario ?? throw new InvalidOperationException("No legacy input.");
        var input = SavedCostScenarioStore.RecoverInput(vehicle);
        var items = new List<LegacyReviewItem>();
        AddRecurring("tax", LegacyItemKind.Tax, "Vehicle tax", input.VehicleTax);
        AddRecurring("insurance", LegacyItemKind.Insurance, "Insurance", input.Insurance);
        AddRecurring("maintenance", LegacyItemKind.Maintenance, "Maintenance and repairs", input.MaintenanceAndRepairs);
        items.AddRange(entity.EnergySources.OrderBy(x => x.Position).Select(x =>
            new LegacyReviewItem($"legacy-energy-{x.Id:N}", LegacyItemKind.Energy, x.Label,
                EnergyUnit: x.Unit, ConsumptionPer100Kilometres: x.ConsumptionPer100Kilometres)));
        items.AddRange(entity.OtherRecurringCosts.OrderBy(x => x.Position).Select(x =>
            new LegacyReviewItem($"legacy-recurring-{x.Id:N}", LegacyItemKind.Recurring, x.Label, x.AmountSek, x.Cadence)));
        items.AddRange(entity.OtherOneTimeCosts.OrderBy(x => x.Position).Select(x =>
            new LegacyReviewItem($"legacy-once-{x.Id:N}", LegacyItemKind.OneTime, x.Label, x.AmountSek)));

        // Suggestions contain only unambiguous car facts. Nothing is written until every
        // source item has an explicit disposition. Undated/combined costs remain outside totals.
        var suggestion = new VehicleCostInput(vehicle.RegistrationNumber, input.PurchasePriceSek,
            input.EnergySources.Count == 0 ? [] : null)
        {
            Residual = input.ExpectedResidualValueSek is { } residual
                ? HouseholdResidualInput.FixedAmount(SensitivityValue.Constant(residual), input.CalculationPeriodMonths) : null,
            Tax = Category(LegacyItemKind.Tax),
            Insurance = Category(LegacyItemKind.Insurance),
            CustomCosts = HouseholdCostCategoryInput.FromItems(items.Where(x => x.Kind == LegacyItemKind.Recurring).Select(ToCost)),
        };
        return new(input, entity.CalculationVersion, entity.ResultSchemaVersion, suggestion, items.AsReadOnly());

        void AddRecurring(string key, LegacyItemKind kind, string label, RecurringCost? cost)
        {
            if (cost is not null)
                items.Add(new($"legacy-{key}-{entity.Id:N}", kind, label, cost.AmountSek, cost.Cadence));
        }
        HouseholdCostCategoryInput? Category(LegacyItemKind kind) => items.Any(x => x.Kind == kind)
            ? HouseholdCostCategoryInput.FromItems(items.Where(x => x.Kind == kind).Select(ToCost)) : null;
    }

    private static HouseholdCostItem ToCost(LegacyReviewItem item) => new(item.Key, item.Label,
        SensitivityValue.Constant(item.AmountSek!.Value), item.Cadence == RecurringCostCadence.Monthly
            ? HouseholdCostCadence.Monthly : HouseholdCostCadence.Annual);

    public static IReadOnlyList<LegacyReviewItem> Resolve(IReadOnlyList<LegacyReviewItem> source,
        VehicleCostWrite write, bool requireDecisions)
    {
        var decisions = write.LegacyDecisions;
        if (decisions is null)
        {
            if (requireDecisions && source.Count > 0)
                throw new HouseholdStoreException("legacyDecisionsRequired", "Every legacy item needs an explicit disposition.");
            EnsureUnresolvedNotIncluded(source, write.Input);
            return source;
        }
        if (decisions.Count != source.Count || decisions.Any(x => x is null)
            || decisions.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != source.Count
            || decisions.Any(x => !source.Any(item => item.Key == x.Key)))
            throw new HouseholdStoreException("invalidLegacyDecisions", "Decisions must match the complete current review set.");

        var costKeys = CostKeys(write.Input).ToHashSet(StringComparer.Ordinal);
        var energyKeys = (write.Input.EnergySources ?? []).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var mappedTargets = new HashSet<string>(StringComparer.Ordinal);
        var remaining = new List<LegacyReviewItem>();
        foreach (var item in source)
        {
            var decision = decisions.Single(x => x.Key == item.Key);
            if (!Enum.IsDefined(decision.Disposition)
                || (decision.Disposition != LegacyItemDisposition.Map && decision.TargetKey is not null))
                throw new HouseholdStoreException("invalidLegacyDecisions", "Unsupported disposition or unexpected target.");
            switch (decision.Disposition)
            {
                case LegacyItemDisposition.KeepForReview:
                    remaining.Add(item);
                    break;
                case LegacyItemDisposition.Map:
                    if (decision.TargetKey is null
                        || !(item.Kind == LegacyItemKind.Energy ? energyKeys : costKeys).Contains(decision.TargetKey)
                        || !mappedTargets.Add(decision.TargetKey)
                        || (decision.TargetKey != item.Key && (costKeys.Contains(item.Key) || energyKeys.Contains(item.Key))))
                        throw new HouseholdStoreException("invalidLegacyTarget", "Each mapped source needs its own existing target item.");
                    break;
            }
        }
        EnsureUnresolvedNotIncluded(source.Where(item => decisions.Any(decision => decision.Key == item.Key
            && decision.Disposition == LegacyItemDisposition.Discard)), write.Input);
        EnsureUnresolvedNotIncluded(remaining, write.Input);
        return remaining.AsReadOnly();
    }

    private static void EnsureUnresolvedNotIncluded(IEnumerable<LegacyReviewItem> items, VehicleCostInput input)
    {
        var keys = CostKeys(input).Concat((input.EnergySources ?? []).Select(x => x.Key)).ToHashSet(StringComparer.Ordinal);
        if (items.Any(x => keys.Contains(x.Key)))
            throw new HouseholdStoreException("unresolvedLegacyItemIncluded", "An unresolved source cannot also contribute to calculations.");
    }

    private static IEnumerable<string> CostKeys(VehicleCostInput x) =>
        new[] { x.Tax, x.Insurance, x.Service, x.Repairs, x.CustomCosts }
            .SelectMany(category => category?.Items ?? []).Select(item => item.Key)
            .Concat((x.Lease?.EndFees ?? []).Concat(x.Lease?.OtherPayments ?? []).Select(item => item.Key));
}
