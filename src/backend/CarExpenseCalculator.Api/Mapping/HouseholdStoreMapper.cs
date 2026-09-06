using A = CarExpenseCalculator.Api.Contracts.Households;
using AM = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using AS = CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using C = CarExpenseCalculator.Core.Households;
using M = CarExpenseCalculator.Core.CostScenarios;
using S = CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

namespace CarExpenseCalculator.Api.Mapping;

internal static class HouseholdStoreMapper
{
    public static RegistrationNumber Registration(string value, string path) =>
        RegistrationNumber.TryParse(value, out var registration) ? registration!
            : throw HouseholdInputMapper.Error(path, "invalidRegistrationNumber", "Supply an ordinary Swedish registration number.");

    public static void Revision(long value, string path, bool allowZero = false)
    {
        if (value < (allowZero ? 0 : 1))
            throw HouseholdInputMapper.Error(path, "outOfRange", "Expected revision is outside its permitted range.");
    }

    public static S.VehicleCostWrite ToStore(A.VehicleCostWrite x, string path)
    {
        var input = HouseholdInputMapper.ToCore(x.Input, path + ".input");
        var errors = C.HouseholdCostInputValidator.ValidateVehicle(input, path + ".input");
        if (errors.Count > 0) throw new C.HouseholdInputValidationException(errors);
        return new(input, x.VehicleLabel, (SavedScenarioListingLinkMode)x.ListingLinkMode,
        x.LegacyDecisions?.Select((item, i) => item is null
            ? throw HouseholdInputMapper.Error($"{path}.legacyDecisions[{i}]", "missingItem", "A decision cannot be null.")
            : new S.LegacyItemDecision(item.Key, (S.LegacyItemDisposition)item.Disposition, item.TargetKey)).ToArray());
    }

    public static A.VehicleCostWrite ToApi(S.VehicleCostWrite x) => new()
    {
        Input = HouseholdInputMapper.ToApi(x.Input), VehicleLabel = x.VehicleLabel,
        ListingLinkMode = (AS.ListingLinkMode)x.ListingLinkMode,
        LegacyDecisions = x.LegacyDecisions?.Select(i => new A.LegacyItemDecision
            { Key = i.Key, Disposition = (A.LegacyItemDisposition)i.Disposition, TargetKey = i.TargetKey }).ToArray(),
    };

    public static S.VehicleDraftInput ToStore(A.VehicleDraftInput x) => new(
        Registration(x.RegistrationNumber, "input.registrationNumber"),
        x.Cost is null ? null : ToStore(x.Cost, "input.cost"),
        x.Listing is null ? null : SavedListingMapper.ToStoreInput(x.Listing), x.BaseVehicleId, x.BaseVehicleRevision);

    public static A.VehicleDraftResponse ToApi(S.SavedVehicleDraft x) => new(x.Revision,
        x.Input is not { } input ? null : new()
        {
            RegistrationNumber = input.RegistrationNumber.Value,
            Cost = input.Cost is null ? null : ToApi(input.Cost),
            Listing = input.Listing is null ? null : HouseholdDraftListingMapper.ToApi(input.Listing),
            BaseVehicleId = input.BaseVehicleId, BaseVehicleRevision = input.BaseVehicleRevision,
        });

    public static A.HouseholdProfileResponse ToApi(S.SavedHouseholdProfile x) => new(HouseholdInputMapper.ToApi(x.Input), x.Revision);
    public static A.HouseholdTransitionResponse ToApi(S.HouseholdTransition x) => new(x.Revision, ToApi(x.Profile), x.Vehicles.Select(ToApi).ToArray());
    public static A.VehicleCostInputResponse ToApi(S.SavedVehicleCostInput x) => new(
        x.VehicleId, x.RegistrationNumber.Value, x.VehicleLabel, x.Revision, (A.VehicleInputState)x.State,
        HouseholdInputMapper.ToApi(x.Input), x.Legacy is not { } legacy ? null : new(
            ManualCalculationMapper.ToApi(legacy.Input), legacy.CalculationVersion, legacy.ResultSchemaVersion,
            HouseholdInputMapper.ToApi(legacy.SuggestedInput), legacy.Items.Select(ToApi).ToArray()),
        x.UnresolvedLegacyItems.Select(ToApi).ToArray(), x.SourceListingVersion, x.CurrentListingVersion,
        x.NeedsListingReview, x.CreatedAtUtc, x.UpdatedAtUtc);
    public static A.VehicleCostInputSummary ToSummary(S.SavedVehicleCostInput x) => new(
        x.VehicleId, x.RegistrationNumber.Value, x.VehicleLabel, x.Revision, (A.VehicleInputState)x.State,
        (x.Legacy?.Items ?? x.UnresolvedLegacyItems).Select(ToApi).ToArray(), x.SourceListingVersion,
        x.CurrentListingVersion, x.NeedsListingReview, x.UpdatedAtUtc);

    public static A.LegacyReviewResponse ToApi(S.LegacyReviewItem x) => new(new()
    {
        Key = x.Key, Kind = (A.LegacyItemKind)x.Kind, Label = x.Label, AmountSek = x.AmountSek,
        Cadence = (A.LegacyRecurringCostCadence?)x.Cadence, EnergyUnit = (AM.EnergyUnit?)x.EnergyUnit,
        ConsumptionPer100Kilometres = x.ConsumptionPer100Kilometres,
    }, x.Reason, x.AffectedSections);

    public static IReadOnlyList<S.LegacyReviewItem> ReviewItems(IReadOnlyList<A.LegacyReviewInput> items, string path)
    {
        if (items.Count > 105) throw HouseholdInputMapper.Error(path, "tooManyItems", "At most 105 original facts may remain for review.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<S.LegacyReviewItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var x = items[i];
            var itemPath = $"{path}[{i}]";
            if (x is null) throw HouseholdInputMapper.Error(itemPath, "missingItem", "A review item cannot be null.");
            if (string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 120 || x.Label.Length > 200)
                throw HouseholdInputMapper.Error(itemPath, "invalidReviewItem", "Review keys and labels exceed their bounds.");
            if (!keys.Add(x.Key)) throw HouseholdInputMapper.Error(itemPath + ".key", "duplicateKey", "Review keys must be unique.");
            result.Add(new(x.Key, (S.LegacyItemKind)x.Kind, x.Label, x.AmountSek,
                (M.RecurringCostCadence?)x.Cadence, (M.EnergyUnit?)x.EnergyUnit, x.ConsumptionPer100Kilometres));
        }
        if (result.Count(x => x.Kind == S.LegacyItemKind.Recurring) > 50 ||
            result.Count(x => x.Kind == S.LegacyItemKind.OneTime) > 50 ||
            result.Count(x => x.Kind == S.LegacyItemKind.Energy) > 2 ||
            result.GroupBy(x => x.Kind).Any(g => g.Key is S.LegacyItemKind.Tax or S.LegacyItemKind.Insurance or S.LegacyItemKind.Maintenance && g.Count() > 1))
            throw HouseholdInputMapper.Error(path, "tooManyItems", "Review items exceed their original category bounds.");
        return result;
    }
}
