using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence.ListingReviewDrafts;

public sealed class ListingReviewDraftStore(CarExpenseDbContext db, ListingDraftProcessor processor,
    TimeProvider clock) : IListingReviewDraftStore
{
    public async Task<IReadOnlyList<ListingReviewDraft>> ListAsync(CancellationToken ct = default) =>
        (await db.Set<ListingReviewDraftEntity>().AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Id).ToArrayAsync(ct)).Select(Read).ToArray();

    public async Task<ListingReviewDraft?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<ListingReviewDraftEntity>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } row
            ? Read(row) : null;

    public async Task<ListingReviewDraft> CreateAsync(SavedListingInput input, CancellationToken ct = default)
    {
        var normalized = Normalize(input);
        var reference = ListingUrl.Parse(normalized.SubmittedUrl);
        var json = HouseholdJson.Serialize(DraftListingPayload.FromInput(normalized));
        await using var session = await HouseholdWriteSession.BeginAsync(db, ct);
        // Query/fragment variants and supported host aliases identify the same advertisement.
        // Every writer holds the shared transaction lock, including simultaneous creates.
        await EnsurePageAvailable(reference, null, ct);
        var now = Now();
        var row = new ListingReviewDraftEntity { Id = Guid.CreateVersion7(now), Revision = 1,
            ListingReference = reference.Value, InputJson = json, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Add(row);
        await session.CommitAsync(ct);
        return Read(row);
    }

    public async Task<ListingReviewDraft> ReplaceAsync(Guid id, long expectedRevision, SavedListingInput input, CancellationToken ct = default)
    {
        var normalized = Normalize(input);
        var reference = ListingUrl.Parse(normalized.SubmittedUrl);
        var json = HouseholdJson.Serialize(DraftListingPayload.FromInput(normalized));
        await using var session = await HouseholdWriteSession.BeginAsync(db, ct);
        var row = await Require(id, expectedRevision, ct);
        if (!ListingUrl.Parse(row.ListingReference).HasSamePageIdentity(reference))
            throw new HouseholdStoreException("reviewDraftIdentityMismatch", "A review draft belongs to its original listing page.", reviewDraftId: id);
        await EnsurePageAvailable(reference, id, ct);
        row.InputJson = json;
        row.ListingReference = reference.Value;
        row.Revision = checked(row.Revision + 1);
        row.UpdatedAtUtc = Now();
        await session.CommitAsync(ct);
        return Read(row);
    }

    public async Task DeleteAsync(Guid id, long expectedRevision, CancellationToken ct = default)
    {
        await using var session = await HouseholdWriteSession.BeginAsync(db, ct);
        db.Remove(await Require(id, expectedRevision, ct));
        await session.CommitAsync(ct);
    }

    public async Task<SavedListing> AdoptAsync(Guid id, long expectedRevision, Guid? existingVehicleId = null,
        long? expectedVehicleRevision = null, CancellationToken ct = default)
    {
        if (existingVehicleId.HasValue != expectedVehicleRevision.HasValue || existingVehicleId == Guid.Empty || expectedVehicleRevision < 1)
            throw new HouseholdStoreException("invalidDraft", "Existing vehicle identity and revision must be supplied together.");
        await using var session = await HouseholdWriteSession.BeginAsync(db, ct);
        var row = await Require(id, expectedRevision, ct);
        var input = Read(row).Input;
        var registration = input.Listing.RegistrationNumber?.Value
            ?? throw new HouseholdStoreException("registrationRequiredForAdoption", "Supply registration before adding the vehicle.", reviewDraftId: id);
        var vehicle = await HouseholdStoreData.Vehicles(db).SingleOrDefaultAsync(x => x.RegistrationNumber == registration.Value, ct);
        var isNew = vehicle is null;
        if (vehicle is not null)
        {
            if (existingVehicleId != vehicle.Id)
                throw new HouseholdStoreException("registrationNumberConflict", "Review the existing vehicle before replacing its listing.",
                    vehicle.Id, actualRevision: vehicle.Revision, reviewDraftId: id);
            HouseholdStoreData.Revision(expectedVehicleRevision!.Value, vehicle.Revision, "vehicleRevisionConflict", vehicle.Id);
            if (vehicle.Scenario is not null) session.LegacyChanged();
        }
        else if (existingVehicleId is not null)
            throw new HouseholdStoreException("vehicleNotFound", "The reviewed existing vehicle no longer matches.", existingVehicleId);
        vehicle ??= HouseholdStoreData.CreateVehicle(registration, Now());
        await new SavedListingStore(db, processor, clock).ApplyDraftAsync(vehicle, input, ct);
        if (isNew) db.Add(vehicle);
        else vehicle.Revision = checked(vehicle.Revision + 1);
        vehicle.UpdatedAtUtc = Now();
        db.Remove(row);
        await session.CommitAsync(ct);
        return SavedListingStore.ToSavedListing(vehicle);
    }

    private SavedListingInput Normalize(SavedListingInput input) =>
        new SavedListingStore(db, processor, clock).NormalizeReviewDraft(input);

    private async Task<ListingReviewDraftEntity> Require(Guid id, long revision, CancellationToken ct)
    {
        var row = await db.Set<ListingReviewDraftEntity>().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new HouseholdStoreException("reviewDraftNotFound", "The listing review draft no longer exists.", reviewDraftId: id);
        if (revision != row.Revision)
            throw new HouseholdStoreException("reviewDraftRevisionConflict", "The draft changed in another operation.",
                expectedRevision: revision, actualRevision: row.Revision, reviewDraftId: id);
        return row;
    }

    private async Task EnsurePageAvailable(ListingUrl url, Guid? exceptId, CancellationToken ct)
    {
        var references = await db.Set<ListingReviewDraftEntity>().AsNoTracking().Where(x => x.Id != exceptId)
            .Select(x => new { x.Id, x.ListingReference, x.Revision }).ToArrayAsync(ct);
        var existing = references.FirstOrDefault(x => ListingUrl.Parse(x.ListingReference).HasSamePageIdentity(url));
        if (existing is not null)
            throw new HouseholdStoreException("reviewDraftAlreadyExists", "Open or explicitly replace the existing review draft.",
                actualRevision: existing.Revision, reviewDraftId: existing.Id);
    }

    private static ListingReviewDraft Read(ListingReviewDraftEntity row)
    {
        if (row.SchemaVersion != 1)
            throw new HouseholdStoreException("unsupportedReviewDraftVersion", "Stored review draft version is unsupported.", reviewDraftId: row.Id);
        return HouseholdJson.Decode(() => new ListingReviewDraft(row.Id, row.Revision, row.SchemaVersion, row.ListingReference,
            row.CreatedAtUtc, row.UpdatedAtUtc, HouseholdJson.Deserialize<DraftListingPayload>(row.InputJson).ToInput()));
    }

    private DateTimeOffset Now() => Comparisons.ComparisonFactOperations.OperationTime(clock);
}
