using CarExpenseCalculator.Core.Households;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

public sealed class HouseholdProfileStore(CarExpenseDbContext dbContext) : IHouseholdProfileStore
{
    public async Task<SavedHouseholdProfile> GetAsync(CancellationToken cancellationToken = default) =>
        HouseholdStoreData.Profile(await dbContext.Set<HouseholdStateEntity>().AsNoTracking().SingleAsync(cancellationToken));

    public async Task<SavedHouseholdProfile> SaveAsync(HouseholdProfileInput input, long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var json = HouseholdStoreData.ValidateProfile(input);
        await using var session = await HouseholdWriteSession.BeginAsync(dbContext, cancellationToken);
        HouseholdStoreData.Revision(expectedRevision, session.State.ProfileRevision, "profileRevisionConflict");
        if (await dbContext.Vehicles.AnyAsync(x => x.Scenario != null, cancellationToken))
            throw new HouseholdStoreException("householdTransitionRequired", "Confirm pending legacy inputs before saving the shared profile.");
        session.State.ProfileJson = json;
        session.State.SchemaVersion = HouseholdJson.SchemaVersion;
        session.State.ProfileRevision = checked(session.State.ProfileRevision + 1);
        var result = HouseholdStoreData.Profile(session.State);
        await session.CommitAsync(cancellationToken);
        return result;
    }
}
