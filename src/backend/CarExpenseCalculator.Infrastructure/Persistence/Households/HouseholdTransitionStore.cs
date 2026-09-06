using CarExpenseCalculator.Core.Households;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

public sealed class HouseholdTransitionStore(CarExpenseDbContext dbContext, TimeProvider timeProvider) : IHouseholdTransitionStore
{
    public Task<HouseholdTransition> GetAsync(CancellationToken cancellationToken = default) =>
        HouseholdStoreData.ReadAsync(dbContext, async () =>
        {
            var state = await dbContext.Set<HouseholdStateEntity>().AsNoTracking().SingleAsync(cancellationToken);
            var vehicles = await HouseholdStoreData.Vehicles(dbContext, false).Where(x => x.Scenario != null)
                .OrderBy(x => x.RegistrationNumber).ToListAsync(cancellationToken);
            return new HouseholdTransition(state.TransitionRevision, HouseholdStoreData.Profile(state),
                Array.AsReadOnly(vehicles.Select(HouseholdStoreData.Vehicle).ToArray()));
        }, cancellationToken);

    public async Task<HouseholdTransition> ConfirmAsync(HouseholdProfileInput profile, long expectedProfileRevision,
        long expectedTransitionRevision, IReadOnlyList<VehicleTransitionWrite> vehicles, CancellationToken cancellationToken = default)
    {
        var profileJson = HouseholdStoreData.ValidateProfile(profile);
        ArgumentNullException.ThrowIfNull(vehicles);
        // The calculator's 100-candidate limit does not truncate the set of saved cars.
        if (vehicles.Any(x => x is null) || vehicles.Select(x => x.VehicleId).Distinct().Count() != vehicles.Count)
            throw new HouseholdStoreException("invalidTransitionSet", "Each pending vehicle must appear exactly once.");
        var writes = vehicles.Select(x => x with { Cost = HouseholdStoreData.Snapshot(x.Cost) }).ToArray();
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        HouseholdStoreData.Revision(expectedTransitionRevision, session.State.TransitionRevision, "transitionRevisionConflict");
        HouseholdStoreData.Revision(expectedProfileRevision, session.State.ProfileRevision, "profileRevisionConflict");
        var pending = await HouseholdStoreData.Vehicles(dbContext).Where(x => x.Scenario != null).ToListAsync(cancellationToken);
        if (pending.Count == 0 || pending.Count != writes.Length || pending.Any(x => !writes.Any(write => write.VehicleId == x.Id)))
            throw new HouseholdStoreException("transitionSetConflict", "Confirmation must include every pending legacy vehicle.",
                expectedRevision: expectedTransitionRevision, actualRevision: session.State.TransitionRevision);

        foreach (var vehicle in pending)
        {
            var write = writes.Single(x => x.VehicleId == vehicle.Id);
            HouseholdStoreData.Revision(write.ExpectedRevision, vehicle.Revision, "vehicleRevisionConflict", vehicle.Id);
            HouseholdStoreData.ApplyCost(vehicle, write.Cost, transitioning: true);
            dbContext.Remove(vehicle.Scenario!);
            vehicle.Scenario = null;
            vehicle.Revision = checked(vehicle.Revision + 1);
            vehicle.UpdatedAtUtc = timeProvider.GetUtcNow();
        }
        session.State.ProfileJson = profileJson;
        session.State.SchemaVersion = HouseholdJson.SchemaVersion;
        session.State.ProfileRevision = checked(session.State.ProfileRevision + 1);
        session.LegacyChanged();
        var result = new HouseholdTransition(session.State.TransitionRevision, HouseholdStoreData.Profile(session.State), []);
        await session.CommitAsync(cancellationToken);
        return result;
    }
}
