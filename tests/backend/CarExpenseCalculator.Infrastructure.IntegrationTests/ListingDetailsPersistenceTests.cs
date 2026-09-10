using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ListingDetailsPersistenceTests(PostgreSqlFixture fixture)
{
    private static readonly ListingUrl Url = ListingUrl.Parse("https://cars.example/item/details?ci=3");
    private static readonly FieldProvenance Ai = new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, Url);
    private static ListingDetails Details() => new()
    {
        Description = new("Full beskrivning.\n\nServicebok finns, men historiken är inte verifierad.", Ai),
        Title = new("Fiktiv testbil", Ai), Subtitle = new("Sedan 1.8 Manuell", Ai), ListingId = new("reference", Ai),
        Seats = new(5, Ai), Doors = new(4, Ai), LuggageLitres = new(440m, Ai),
        WeightKilograms = new(1370.123456789012345678901234m, Ai), WeightLabel = new("Vikt", Ai), WeightCategory = new("unspecified", Ai),
        TrailerWeightKilograms = new(1300m, Ai), TrailerWeightLabel = new("Max trailervikt", Ai), TrailerWeightCategory = new("unspecified", Ai),
        PostalCode = new("13738", Ai), Country = new("Sverige", Ai), FeeClass = new("Personbil", Ai), SaleForm = new("Begagnad bil till salu", Ai),
        UpdatedLocalDateTime = new("2026-09-08T16:35:00", Ai),
        Specifications = [], SellerAnswers = [new(new SellerAnswer("Har bilen några skulder?", "Nej"), Ai)],
    };
    private static SavedListingInput Input(ListingDetails? details, int version = 3) => new(Url.Value,
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), "gpt-5.6-luna", version, version, [],
        new() { PriceSek = new(28888m, Ai), OwnerCount = new(4, Ai), Details = details });

    [Fact]
    public async Task Extension_round_trips_without_sources_and_is_in_the_same_comparison_snapshot()
    {
        await fixture.ResetDatabaseAsync();
        Guid id;
        await using (var writer = fixture.CreateDbContext())
        {
            var saved = await new SavedListingStore(writer, new(), TimeProvider.System)
                .CreateAsync(RegistrationNumber.Parse("TST123"), Input(Details()));
            id = saved.VehicleId;
            Assert.Equal(2, saved.ListingSchemaVersion);
            Assert.Empty(saved.ProcessingResult.Sources);
        }
        await using var reader = fixture.CreateDbContext();
        var snapshotStore = new ComparisonSnapshotStore(reader);
        var baseline = await snapshotStore.ReadBaselineAsync();
        var snapshot = await snapshotStore.ReadAllAsync(baseline.BaselineToken);
        var vehicle = Assert.Single(snapshot.Snapshot.Vehicles);
        Assert.Equal(id, vehicle.Listing!.VehicleId);
        Assert.Equivalent(Details(), vehicle.Listing.ProcessingResult.Listing.Details, strict: true);
        Assert.Equal(5, vehicle.Facts.ListingProposal!.Facts.Seats!.Observations[0].Value);
        Assert.Null(vehicle.Listing.ProcessingResult.Listing.Details!.UpdatedTimeZone);
        Assert.False(reader.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Draft_open_and_adoption_preserve_the_full_extension_without_confirmation()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var drafts = new SharedVehicleDraftStore(db, new(), TimeProvider.System);
        var slot = await drafts.SaveAsync(new(RegistrationNumber.Parse("TST124"), Listing: Input(Details())), 0);
        var opened = await drafts.GetAsync();
        Assert.Equal(slot.Revision, opened.Revision);
        Assert.Equivalent(Details(), opened.Input!.Listing!.Listing.Details, strict: true);
        await drafts.AdoptAsync(slot.Revision);
        await using var read = fixture.CreateDbContext();
        var saved = await new SavedListingStore(read, new(), TimeProvider.System).GetByRegistrationNumberAsync(RegistrationNumber.Parse("TST124"));
        Assert.Equivalent(Details(), saved!.ProcessingResult.Listing.Details, strict: true);
        Assert.Equal(VerificationStatus.Unverified, saved.ProcessingResult.Listing.Details!.Description!.Provenance.Verification);
        Assert.Null((await drafts.GetAsync()).Input);
    }

    [Fact]
    public async Task Older_extraction_metadata_remains_readable_and_replacement_does_not_retain_old_extension()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var store = new SavedListingStore(db, new(), TimeProvider.System);
        var saved = await store.CreateAsync(RegistrationNumber.Parse("TST125"), Input(Details()));
        var replacement = await store.ReplaceAsync(saved.VehicleId, saved.Revision, Input(null, 2));
        Assert.Null(replacement.ProcessingResult.Listing.Details);
        Assert.Equal(2, replacement.ExtractionSchemaVersion);
        await using var reader = fixture.CreateDbContext();
        Assert.Null((await new SavedListingStore(reader, new(), TimeProvider.System).GetAsync(saved.VehicleId))!.ProcessingResult.Listing.Details);
    }

    [Fact]
    public async Task Rollback_rejects_new_data_atomically_and_leaves_it_readable()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var saved = await new SavedListingStore(db, new(), TimeProvider.System).CreateAsync(RegistrationNumber.Parse("TST126"), Input(Details()));
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.GetService<IMigrator>()
            .MigrateAsync("20260908103211_AddComparisonPersistence"));
        Assert.Contains("cannot be downgraded", error.MessageText);
        await using var reader = fixture.CreateDbContext();
        Assert.Equivalent(Details(), (await new SavedListingStore(reader, new(), TimeProvider.System).GetAsync(saved.VehicleId))!.ProcessingResult.Listing.Details, strict: true);
    }

    [Fact]
    public async Task Compatible_v1_listing_survives_rollback_and_reapplication_without_relabeling_extraction()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var saved = await new SavedListingStore(db, new(), TimeProvider.System).CreateAsync(RegistrationNumber.Parse("TST127"), Input(null,2));
        await MigrationTests.SeedLegacyListingFormatAsync(db);
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260908103211_AddComparisonPersistence");
        await migrator.MigrateAsync();
        await using var reader = fixture.CreateDbContext();
        var legacy = (await new SavedListingStore(reader, new(), TimeProvider.System).GetAsync(saved.VehicleId))!;
        Assert.Equal(1, legacy.ListingSchemaVersion);
        Assert.Equal(2, legacy.PromptVersion);
        Assert.Equal(2, legacy.ExtractionSchemaVersion);
        Assert.Null(legacy.ProcessingResult.Listing.Details);
        Assert.Equal(saved.Revision, legacy.Revision);
        Assert.Equal(saved.AnalyzedAtUtc, legacy.AnalyzedAtUtc);
        Assert.Equal(saved.ProcessingResult.Listing.OwnerCount, legacy.ProcessingResult.Listing.OwnerCount);
        Assert.False(reader.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Rollback_also_protects_unadopted_draft_content()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var drafts = new SharedVehicleDraftStore(db, new(), TimeProvider.System);
        var saved = await drafts.SaveAsync(new(RegistrationNumber.Parse("TST128"), Listing: Input(Details())),0);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.GetService<IMigrator>().MigrateAsync("20260908103211_AddComparisonPersistence"));
        var current = await drafts.GetAsync();
        Assert.Equal(saved.Revision,current.Revision);
        Assert.Equivalent(Details(),current.Input!.Listing!.Listing.Details,strict:true);
    }
}
