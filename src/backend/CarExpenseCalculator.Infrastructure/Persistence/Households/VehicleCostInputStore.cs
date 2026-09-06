using CarExpenseCalculator.Core.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

public sealed class VehicleCostInputStore(CarExpenseDbContext dbContext, TimeProvider timeProvider) : IVehicleCostInputStore
{
    public Task<SavedVehicleCostInput?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        ReadOneAsync(x => x.Id == vehicleId, cancellationToken);

    public Task<SavedVehicleCostInput?> GetByRegistrationNumberAsync(RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);
        return ReadOneAsync(x => x.RegistrationNumber == registrationNumber.Value, cancellationToken);
    }

    private Task<SavedVehicleCostInput?> ReadOneAsync(System.Linq.Expressions.Expression<Func<Vehicles.VehicleEntity, bool>> predicate,
        CancellationToken ct) => HouseholdStoreData.ReadAsync(dbContext, async () =>
    {
        var vehicle = await HouseholdStoreData.Vehicles(dbContext, false).SingleOrDefaultAsync(predicate, ct);
        return vehicle is null ? null : HouseholdStoreData.Vehicle(vehicle);
    }, ct);

    public Task<IReadOnlyList<SavedVehicleCostInput>> ListAsync(CancellationToken cancellationToken = default) =>
        HouseholdStoreData.ReadAsync<IReadOnlyList<SavedVehicleCostInput>>(dbContext, async () =>
            Array.AsReadOnly((await HouseholdStoreData.Vehicles(dbContext, false).OrderBy(x => x.RegistrationNumber)
                .ToListAsync(cancellationToken)).Select(HouseholdStoreData.Vehicle).ToArray()), cancellationToken);

    public async Task<SavedVehicleCostInput> CreateAsync(RegistrationNumber registrationNumber, VehicleCostWrite input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);
        var write = HouseholdStoreData.Snapshot(input);
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        await HouseholdStoreData.EnsureAvailableAsync(dbContext, registrationNumber, cancellationToken);
        var vehicle = HouseholdStoreData.CreateVehicle(registrationNumber, timeProvider.GetUtcNow());
        HouseholdStoreData.ApplyCost(vehicle, write);
        dbContext.Vehicles.Add(vehicle);
        var result = HouseholdStoreData.Vehicle(vehicle);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<SavedVehicleCostInput> ReplaceAsync(Guid vehicleId, long expectedRevision, VehicleCostWrite input,
        CancellationToken cancellationToken = default)
    {
        var write = HouseholdStoreData.Snapshot(input);
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        var vehicle = await HouseholdStoreData.Vehicles(dbContext).SingleOrDefaultAsync(x => x.Id == vehicleId, cancellationToken)
            ?? throw new HouseholdStoreException("vehicleNotFound", "Vehicle no longer exists.", vehicleId, expectedRevision);
        HouseholdStoreData.Revision(expectedRevision, vehicle.Revision, "vehicleRevisionConflict", vehicle.Id);
        HouseholdStoreData.RequireCurrent(vehicle);
        HouseholdStoreData.ApplyCost(vehicle, write);
        vehicle.Revision = checked(vehicle.Revision + 1);
        vehicle.UpdatedAtUtc = timeProvider.GetUtcNow();
        var result = HouseholdStoreData.Vehicle(vehicle);
        await session.CommitAsync(cancellationToken);
        return result;
    }

    public async Task DeleteAsync(Guid vehicleId, long expectedRevision, CancellationToken cancellationToken = default)
    {
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        var vehicle = await dbContext.Vehicles.Include(x => x.Scenario).SingleOrDefaultAsync(x => x.Id == vehicleId, cancellationToken)
            ?? throw new HouseholdStoreException("vehicleNotFound", "Vehicle no longer exists.", vehicleId, expectedRevision);
        HouseholdStoreData.Revision(expectedRevision, vehicle.Revision, "vehicleRevisionConflict", vehicle.Id);
        if (vehicle.Scenario is not null) session.LegacyChanged();
        await session.ClearMatchingDraftAsync(vehicle, cancellationToken);
        dbContext.Vehicles.Remove(vehicle);
        await session.CommitAsync(cancellationToken);
    }
}
