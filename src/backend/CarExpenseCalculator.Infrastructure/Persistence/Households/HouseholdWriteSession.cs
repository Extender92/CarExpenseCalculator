using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

// All application writers enter through this row lock, before loading revision-owned entities.
internal sealed class HouseholdWriteSession(CarExpenseDbContext db, IDbContextTransaction transaction, HouseholdStateEntity state) : IAsyncDisposable
{
    public HouseholdStateEntity State { get; } = state;

    public static async Task<HouseholdWriteSession> BeginAsync(CarExpenseDbContext db, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // DbContext may have served a previous read; revision checks must use fresh database values.
            db.ChangeTracker.Clear();
            var state = await db.Set<HouseholdStateEntity>()
                .FromSqlRaw("SELECT * FROM household_state WHERE id = 1 FOR UPDATE")
                .SingleAsync(cancellationToken);
            return new(db, transaction, state);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public void LegacyChanged() => State.TransitionRevision = checked(State.TransitionRevision + 1);

    public async Task ClearMatchingDraftAsync(VehicleEntity vehicle, CancellationToken cancellationToken)
    {
        var draft = await db.Set<VehicleDraftEntity>().SingleAsync(cancellationToken);
        if (draft.BaseVehicleId == vehicle.Id || draft.RegistrationNumber == vehicle.RegistrationNumber)
            draft.Clear();
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        db.ChangeTracker.Clear();
    }
}
