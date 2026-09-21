using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.ListingReviewDrafts;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ListingReviewDraftStoreTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    private static SavedListingInput Input(string path = "one", bool registered = false)
    {
        var original = ListingFactory.Complete();
        var url = ListingUrl.Parse("https://www.blocket.se/mobility/item/" + path);
        var provenance = new FieldProvenance(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.Unverified, url);
        return new(url.Value, original.AnalyzedAtUtc, null, null, null, [], new()
        {
            RegistrationNumber = registered ? new(Reg(), provenance) : null,
            Make = new("Testbil", provenance), PriceSek = new(0m, provenance), TowBar = new(false, provenance),
            OwnerCount = new(0, provenance), Equipment = new([], provenance),
            Details = new() { Description = new("Återgiven text\n\nAndra stycket.", provenance) }
        });
    }

    [Fact]
    public async Task Multiple_unregistered_drafts_round_trip_without_creating_vehicles_and_delete_independently()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var store = new ListingReviewDraftStore(db, new(), TimeProvider.System);
        var first = await store.CreateAsync(Input());
        var second = await store.CreateAsync(Input("two"));
        Assert.Equal(0, await CountAsync("vehicles"));
        Assert.Equal(2, (await store.ListAsync()).Count);
        await using var other = Fixture.CreateDbContext();
        var reopened = (await new ListingReviewDraftStore(other, new(), TimeProvider.System).GetAsync(first.Id))!;
        Assert.Null(reopened.Input.Listing.RegistrationNumber);
        Assert.Equal(0, reopened.Input.Listing.PriceSek!.Value);
        Assert.False(reopened.Input.Listing.TowBar!.Value);
        Assert.Empty(reopened.Input.Listing.Equipment!.Values);
        Assert.Equal(VerificationStatus.Unverified, reopened.Input.Listing.OwnerCount!.Provenance.Verification);
        Assert.Equal("Återgiven text\n\nAndra stycket.", reopened.Input.Listing.Details!.Description!.Value);
        await store.DeleteAsync(first.Id, first.Revision);
        Assert.Null(await store.GetAsync(first.Id));
        Assert.Equal(second.Id, Assert.Single(await store.ListAsync()).Id);
    }

    [Fact]
    public async Task Page_variants_conflict_and_replacements_require_revision_and_original_page()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var store = new ListingReviewDraftStore(db, new(), TimeProvider.System);
        var first = await store.CreateAsync(Input());
        var input = Input();
        var variant = new SavedListingInput("https://blocket.se/mobility/item/one/?ci=7", input.AnalyzedAtUtc,
            null, null, null, [], input.Listing);
        var duplicate = await ErrorAsync("reviewDraftAlreadyExists", () => store.CreateAsync(variant));
        Assert.Equal(first.Id, duplicate.ReviewDraftId);
        var changed = await store.ReplaceAsync(first.Id, 1, Input(registered: true));
        Assert.Equal(2, changed.Revision);
        await ErrorAsync("reviewDraftRevisionConflict", () => store.ReplaceAsync(first.Id, 1, Input()));
        await ErrorAsync("reviewDraftRevisionConflict", () => store.DeleteAsync(first.Id, 1));
        await ErrorAsync("reviewDraftIdentityMismatch", () => store.ReplaceAsync(first.Id, 2, Input("different")));
        Assert.NotNull((await store.GetAsync(first.Id))!.Input.Listing.RegistrationNumber);
    }

    [Fact]
    public async Task Adoption_requires_registration_and_consumes_only_the_successfully_adopted_draft()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var store = new ListingReviewDraftStore(db, new(), TimeProvider.System);
        var first = await store.CreateAsync(Input());
        await ErrorAsync("registrationRequiredForAdoption", () => store.AdoptAsync(first.Id, 1));
        Assert.NotNull(await store.GetAsync(first.Id));
        await store.ReplaceAsync(first.Id, 1, Input(registered: true));
        var saved = await store.AdoptAsync(first.Id, 2);
        Assert.Equal("ABC123", saved.RegistrationNumber.Value);
        Assert.Null(await store.GetAsync(first.Id));
        Assert.Equal(3, saved.ListingSchemaVersion);
        Assert.Equal(VerificationStatus.Unverified, saved.ProcessingResult.Listing.Make!.Provenance.Verification);
    }

    [Fact]
    public async Task Existing_vehicle_adoption_requires_both_revisions_and_preserves_costs()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var vehicle = await Costs(db).CreateAsync(Reg(), Write(12345));
        var store = new ListingReviewDraftStore(db, new(), TimeProvider.System);
        var draft = await store.CreateAsync(Input(registered: true));
        await ErrorAsync("registrationNumberConflict", () => store.AdoptAsync(draft.Id, 1));
        await ErrorAsync("vehicleRevisionConflict", () => store.AdoptAsync(draft.Id, 1, vehicle.VehicleId, 99));
        Assert.NotNull(await store.GetAsync(draft.Id));
        var saved = await store.AdoptAsync(draft.Id, 1, vehicle.VehicleId, vehicle.Revision);
        Assert.Equal(vehicle.VehicleId, saved.VehicleId);
        Assert.Equal(vehicle.Revision + 1, saved.Revision);
        Assert.Equal(12345m, (await Costs(db).GetAsync(vehicle.VehicleId))!.Input!.PriceSek);
        Assert.Null(await store.GetAsync(draft.Id));
        await Listings(db).DeleteAsync(saved.VehicleId, saved.Revision);
        Assert.Equal(0, await CountAsync("vehicle_listings"));
        Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
    }

    [Fact]
    public async Task Migration_down_refuses_to_discard_review_drafts()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var store = new ListingReviewDraftStore(db, new(), TimeProvider.System);
        var draft = await store.CreateAsync(Input());
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260910214541_AllowHtmlListingExtraction"));
        Assert.NotNull(await store.GetAsync(draft.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_down_preserves_new_cost_and_shared_draft_formats(bool sharedDraft)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        Guid? vehicleId = null;
        if (sharedDraft) await Drafts(db).SaveAsync(new(Reg(), Cost: Write(12345)), 0);
        else vehicleId = (await Costs(db).CreateAsync(Reg(), Write(12345))).VehicleId;
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>()
            .MigrateAsync("20260910214541_AllowHtmlListingExtraction"));
        Assert.Contains("Cannot downgrade listing review workflow", error.MessageText);
        if (sharedDraft) Assert.Equal(12345m, (await Drafts(db).GetAsync()).Input!.Cost!.Input.PriceSek);
        else Assert.Equal(12345m, (await Costs(db).GetAsync(vehicleId!.Value))!.Input!.PriceSek);
    }
}
