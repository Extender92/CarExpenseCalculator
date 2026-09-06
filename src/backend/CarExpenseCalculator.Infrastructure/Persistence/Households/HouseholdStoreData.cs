using System.Data;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

internal static class HouseholdStoreData
{
    public static IQueryable<VehicleEntity> Vehicles(CarExpenseDbContext db, bool tracking = true)
    {
        var query = db.Vehicles.Include(x => x.HouseholdCostInput)
            .Include(x => x.Scenario).ThenInclude(x => x!.EnergySources)
            .Include(x => x.Scenario).ThenInclude(x => x!.OtherRecurringCosts)
            .Include(x => x.Scenario).ThenInclude(x => x!.OtherOneTimeCosts)
            .Include(x => x.Listing).ThenInclude(x => x!.Sources)
            .Include(x => x.Listing).ThenInclude(x => x!.Equipment)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTrackingWithIdentityResolution();
    }

    public static async Task<T> ReadAsync<T>(CarExpenseDbContext db, Func<Task<T>> read, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var result = await read();
        await transaction.CommitAsync(ct);
        return result;
    }

    public static SavedHouseholdProfile Profile(HouseholdStateEntity state) => HouseholdJson.Decode(() => new SavedHouseholdProfile(state.ProfileJson is null ? null
        : HouseholdJson.Deserialize<HouseholdJson.ProfilePayload>(state.ProfileJson, state.SchemaVersion).ToCore(), state.ProfileRevision));

    public static SavedVehicleCostInput Vehicle(VehicleEntity vehicle) => HouseholdJson.Decode(() => ReadVehicle(vehicle));

    private static SavedVehicleCostInput ReadVehicle(VehicleEntity vehicle)
    {
        var entity = vehicle.HouseholdCostInput;
        var stored = entity is null ? null : HouseholdJson.Deserialize<HouseholdJson.StoredCostPayload>(entity.InputJson, entity.SchemaVersion);
        var legacy = vehicle.Scenario is null ? null : LegacyInputRecovery.Recover(vehicle);
        return new(vehicle.Id, RegistrationNumber.Parse(vehicle.RegistrationNumber), vehicle.VehicleLabel, vehicle.Revision,
            stored is not null ? VehicleInputState.Current : legacy is not null ? VehicleInputState.LegacyPending : VehicleInputState.ListingOnly,
            stored?.Input.ToCore(), legacy, Array.AsReadOnly(stored?.UnresolvedItems.Select(x => x.ToItem()).ToArray() ?? []),
            entity?.SourceListingVersion ?? vehicle.Scenario?.SourceListingVersion,
            vehicle.Listing?.ListingVersion, vehicle.CreatedAtUtc, vehicle.UpdatedAtUtc);
    }

    public static string ValidateProfile(HouseholdProfileInput input)
    {
        var errors = HouseholdInputValidator.ValidateProfile(input);
        if (errors.Count > 0) throw new HouseholdInputValidationException(errors);
        return HouseholdJson.Serialize(HouseholdJson.ProfilePayload.FromCore(input));
    }

    public static VehicleCostWrite Snapshot(VehicleCostWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        var errors = HouseholdCostInputValidator.ValidateVehicle(write.Input);
        if (errors.Count > 0) throw new HouseholdInputValidationException(errors);
        if (write.VehicleLabel?.Trim().Length > 120 || !Enum.IsDefined(write.ListingLinkMode)
            || write.LegacyDecisions?.Count > 105)
            throw new HouseholdStoreException("invalidVehicleWrite", "Label, link mode or review decisions exceed their bounds.");
        if (write.LegacyDecisions?.Any(x => x is null || !Enum.IsDefined(x.Disposition)
            || string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 120
            || x.TargetKey?.Length > 120) == true)
            throw new HouseholdStoreException("invalidLegacyDecisions", "Review decisions require valid source keys and dispositions.");
        return HouseholdJson.Deserialize<HouseholdJson.CostWritePayload>(
            HouseholdJson.Serialize(HouseholdJson.CostWritePayload.FromInput(write))).ToInput();
    }

    public static void Revision(long expected, long actual, string code, Guid? vehicleId = null)
    {
        if (expected != actual)
            throw new HouseholdStoreException(code, "Input was changed by another operation.", vehicleId, expected, actual);
    }

    public static void RequireCurrent(VehicleEntity vehicle)
    {
        if (vehicle.Scenario is not null)
            throw new HouseholdStoreException("householdTransitionRequired", "Confirm the complete legacy transition first.", vehicle.Id,
                actualRevision: vehicle.Revision);
    }

    public static VehicleEntity CreateVehicle(RegistrationNumber registrationNumber, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now), RegistrationNumber = registrationNumber.Value, Revision = 1,
        CreatedAtUtc = now, UpdatedAtUtc = now,
    };

    public static async Task EnsureAvailableAsync(CarExpenseDbContext db, RegistrationNumber registrationNumber, CancellationToken ct)
    {
        var existing = await db.Vehicles.SingleOrDefaultAsync(x => x.RegistrationNumber == registrationNumber.Value, ct);
        if (existing is not null)
            throw new HouseholdStoreException("registrationNumberConflict", "The registration number already identifies a vehicle.",
                existing.Id, actualRevision: existing.Revision);
    }

    public static void ApplyCost(VehicleEntity vehicle, VehicleCostWrite write, bool transitioning = false)
    {
        ValidateListingLabel(vehicle, write);
        var current = vehicle.HouseholdCostInput;
        var previousItems = ReviewItems(vehicle);
        var remaining = LegacyInputRecovery.Resolve(previousItems, write, transitioning);
        var source = current?.SourceListingVersion ?? vehicle.Scenario?.SourceListingVersion;
        if (write.ListingLinkMode == SavedScenarioListingLinkMode.Current)
            source = vehicle.Listing?.ListingVersion
                ?? throw new HouseholdStoreException("listingRequired", "There is no listing version to confirm.", vehicle.Id);
        var payload = new HouseholdJson.StoredCostPayload(HouseholdJson.CostPayload.FromCore(write.Input) with
        { CandidateKey = vehicle.RegistrationNumber }, remaining.Select(HouseholdJson.LegacyReviewPayload.FromItem).ToArray());
        var json = HouseholdJson.Serialize(payload);
        if (current is null)
            vehicle.HouseholdCostInput = new() { VehicleId = vehicle.Id, Vehicle = vehicle, InputJson = json, SourceListingVersion = source };
        else
        {
            current.InputJson = json;
            current.SourceListingVersion = source;
            current.SchemaVersion = HouseholdJson.SchemaVersion;
        }
        // Listing readers use the aggregate label together with listing-owned provenance.
        // A cost-only write must not change that reviewed listing fact or its version.
        if (vehicle.Listing is null) vehicle.VehicleLabel = write.VehicleLabel;
    }

    public static void ValidateListingLabel(VehicleEntity vehicle, VehicleCostWrite write)
    {
        if (vehicle.Listing is not null && write.VehicleLabel is { } label && label != vehicle.VehicleLabel)
            throw new HouseholdStoreException("listingLabelRequiresReview", "Change a listed vehicle's label through its reviewed listing.", vehicle.Id);
    }

    public static IReadOnlyList<LegacyReviewItem> ReviewItems(VehicleEntity vehicle) =>
        vehicle.Scenario is not null ? LegacyInputRecovery.Recover(vehicle).Items
        : vehicle.HouseholdCostInput is not { } current ? []
        : HouseholdJson.Deserialize<HouseholdJson.StoredCostPayload>(current.InputJson, current.SchemaVersion)
            .UnresolvedItems.Select(x => x.ToItem()).ToArray();
}
