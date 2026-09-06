using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

public sealed class SharedVehicleDraftStore(CarExpenseDbContext dbContext, ListingDraftProcessor processor,
    TimeProvider timeProvider) : ISharedVehicleDraftStore
{
    public async Task<SavedVehicleDraft> GetAsync(CancellationToken cancellationToken = default) =>
        ToSaved(await dbContext.Set<VehicleDraftEntity>().AsNoTracking().SingleAsync(cancellationToken));

    public async Task<SavedVehicleDraft> SaveAsync(VehicleDraftInput input, long expectedRevision, bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.RegistrationNumber);
        if ((input.Cost is null && input.Listing is null)
            || input.BaseVehicleId.HasValue != input.BaseVehicleRevision.HasValue
            || input.BaseVehicleId == Guid.Empty || input.BaseVehicleRevision < 1)
            throw new HouseholdStoreException("invalidDraft", "A registered draft needs content and a complete, valid original identity when editing.");
        var listingStore = new SavedListingStore(dbContext, processor, timeProvider);
        var cost = input.Cost is null ? null : HouseholdStoreData.Snapshot(input.Cost);
        var listing = input.Listing is null ? null : listingStore.NormalizeDraft(input.RegistrationNumber, input.Listing);
        var json = HouseholdJson.Serialize(new DraftPayload(cost is null ? null : HouseholdJson.CostWritePayload.FromInput(cost),
            listing is null ? null : DraftListingPayload.FromInput(listing)));
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        var slot = await dbContext.Set<VehicleDraftEntity>().SingleAsync(cancellationToken);
        HouseholdStoreData.Revision(expectedRevision, slot.Revision, "draftRevisionConflict");
        if (slot.RegistrationNumber is { } previous && previous != input.RegistrationNumber.Value && !replaceExisting)
            throw new HouseholdStoreException("draftReplacementRequired", "Replacing another vehicle's draft requires an explicit choice.",
                expectedRevision: expectedRevision, actualRevision: slot.Revision);
        var original = await ResolveBaseAsync(input, cancellationToken);
        if (cost is not null)
        {
            if (listing is not null && cost.VehicleLabel is { } label && label != listing.Listing.VehicleLabel?.Value)
                throw new HouseholdStoreException("listingLabelRequiresReview", "The draft label must agree with its reviewed listing.", original?.Id);
            if (listing is null && original is not null) HouseholdStoreData.ValidateListingLabel(original, cost);
            LegacyInputRecovery.Resolve(original is null ? [] : HouseholdStoreData.ReviewItems(original), cost, requireDecisions: false);
            if (cost.ListingLinkMode == SavedCostScenarios.SavedScenarioListingLinkMode.Current
                && listing is null && original?.Listing is null)
                throw new HouseholdStoreException("listingRequired", "There is no listing version to confirm.", original?.Id);
        }
        slot.Revision = checked(slot.Revision + 1);
        slot.SchemaVersion = HouseholdJson.SchemaVersion;
        slot.RegistrationNumber = input.RegistrationNumber.Value;
        slot.BaseVehicleId = input.BaseVehicleId;
        slot.BaseVehicleRevision = input.BaseVehicleRevision;
        slot.InputJson = json;
        var result = ToSaved(slot);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<SavedVehicleDraft> DeleteAsync(long expectedRevision, CancellationToken cancellationToken = default)
    {
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        var slot = await dbContext.Set<VehicleDraftEntity>().SingleAsync(cancellationToken);
        HouseholdStoreData.Revision(expectedRevision, slot.Revision, "draftRevisionConflict");
        slot.Clear();
        var result = ToSaved(slot);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<SavedVehicleCostInput> AdoptAsync(long expectedRevision, CancellationToken cancellationToken = default)
    {
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        var slot = await dbContext.Set<VehicleDraftEntity>().SingleAsync(cancellationToken);
        HouseholdStoreData.Revision(expectedRevision, slot.Revision, "draftRevisionConflict");
        var input = ToSaved(slot).Input ?? throw new HouseholdStoreException("draftEmpty", "There is no saved draft.", actualRevision: slot.Revision);
        var vehicle = await ResolveBaseAsync(input, cancellationToken);
        var isNew = vehicle is null;
        vehicle ??= HouseholdStoreData.CreateVehicle(input.RegistrationNumber, timeProvider.GetUtcNow());
        if (input.Cost is not null) HouseholdStoreData.RequireCurrent(vehicle);
        if (vehicle.Scenario is not null && input.Listing is not null) session.LegacyChanged();
        if (input.Listing is not null)
            await new SavedListingStore(dbContext, processor, timeProvider).ApplyDraftAsync(vehicle, input.Listing, cancellationToken);
        if (input.Cost is not null) HouseholdStoreData.ApplyCost(vehicle, input.Cost);
        if (isNew) dbContext.Vehicles.Add(vehicle);
        else vehicle.Revision = checked(vehicle.Revision + 1);
        vehicle.UpdatedAtUtc = timeProvider.GetUtcNow();
        slot.Clear();
        var result = HouseholdStoreData.Vehicle(vehicle);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<VehicleEntity?> ResolveBaseAsync(VehicleDraftInput input, CancellationToken ct)
    {
        if (input.BaseVehicleId is not { } id)
        {
            await HouseholdStoreData.EnsureAvailableAsync(dbContext, input.RegistrationNumber, ct);
            return null;
        }
        var vehicle = await HouseholdStoreData.Vehicles(dbContext).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new HouseholdStoreException("vehicleNotFound", "The original vehicle no longer exists.", id, input.BaseVehicleRevision);
        if (vehicle.RegistrationNumber != input.RegistrationNumber.Value)
            throw new HouseholdStoreException("draftIdentityMismatch", "The original vehicle has a different registration number.", id,
                input.BaseVehicleRevision, vehicle.Revision);
        HouseholdStoreData.Revision(input.BaseVehicleRevision!.Value, vehicle.Revision, "vehicleRevisionConflict", id);
        return vehicle;
    }

    private static SavedVehicleDraft ToSaved(VehicleDraftEntity slot) => HouseholdJson.Decode(() => ReadSaved(slot));

    private static SavedVehicleDraft ReadSaved(VehicleDraftEntity slot)
    {
        if (slot.InputJson is null) return new(slot.Revision, null);
        var payload = HouseholdJson.Deserialize<DraftPayload>(slot.InputJson, slot.SchemaVersion);
        return new(slot.Revision, new(RegistrationNumber.Parse(slot.RegistrationNumber!), payload.Cost?.ToInput(),
            payload.Listing?.ToInput(), slot.BaseVehicleId, slot.BaseVehicleRevision));
    }

    private sealed record DraftPayload(HouseholdJson.CostWritePayload? Cost, DraftListingPayload? Listing);
}
