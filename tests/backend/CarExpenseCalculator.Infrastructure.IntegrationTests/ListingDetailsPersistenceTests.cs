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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Html_source_and_prompt_four_survive_drafts_adoption_and_guarded_rollback(bool adopt)
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var html = Ai with { ExtractionMethod = ExtractionMethod.Html };
        var seller = new SourcedValue<SellerType>(adopt ? SellerType.Dealer : SellerType.Private, html);
        var details = Details() with { Description = new("Originaltext.\n\nOförändrat andra stycke.", html),
            SellerAnswers = [new(new SellerAnswer("Skulder?", "Nej"), html)] };
        var input = new SavedListingInput(Url.Value, new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero),
            "gpt-5.6-luna", 4, 3, [], new() { PriceSek = new(28888m, Ai), SellerType = seller, Details = details });
        var drafts = new SharedVehicleDraftStore(db, new(), TimeProvider.System);
        var saved = await drafts.SaveAsync(new(RegistrationNumber.Parse("TST129"), Listing: input), 0);
        if (adopt) await drafts.AdoptAsync(saved.Revision);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.GetService<IMigrator>()
            .MigrateAsync("20260910132449_AddListingDetails"));
        Assert.Contains("HTML listing content cannot be downgraded", error.MessageText);
        await using var reader = fixture.CreateDbContext();
        if (adopt)
        {
            var listing = (await new SavedListingStore(reader, new(), TimeProvider.System)
                .GetByRegistrationNumberAsync(RegistrationNumber.Parse("TST129")))!;
            Assert.Equal(4, listing.PromptVersion);
            Assert.Equal(3, listing.ExtractionSchemaVersion);
            Assert.Equivalent(details, listing.ProcessingResult.Listing.Details, strict: true);
            Assert.Equal(seller, listing.ProcessingResult.Listing.SellerType);
            var snapshot = await new ComparisonSnapshotStore(reader).ReadAsync([listing.VehicleId]);
            Assert.Equivalent(details, Assert.Single(snapshot.Vehicles).Listing!.ProcessingResult.Listing.Details, strict: true);
            Assert.Equal(seller, Assert.Single(snapshot.Vehicles).Listing!.ProcessingResult.Listing.SellerType);
        }
        else
        {
            var reopened = await new SharedVehicleDraftStore(reader, new(), TimeProvider.System).GetAsync();
            Assert.Equal(saved.Revision, reopened.Revision);
            Assert.Equal(4, reopened.Input!.Listing!.PromptVersion);
            Assert.Equivalent(details, reopened.Input.Listing.Listing.Details, strict: true);
            Assert.Equal(seller, reopened.Input.Listing.Listing.SellerType);
        }
    }

    [Fact]
    public async Task Prompt_three_survives_the_followup_migration_down_and_up()
    {
        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var listing = await new SavedListingStore(db, new(), TimeProvider.System)
            .CreateAsync(RegistrationNumber.Parse("TST130"), Input(Details()));
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260910132449_AddListingDetails");
        await migrator.MigrateAsync();
        await using var reader = fixture.CreateDbContext();
        var read = (await new SavedListingStore(reader, new(), TimeProvider.System).GetAsync(listing.VehicleId))!;
        Assert.Equal(3, read.PromptVersion);
        Assert.Equal(listing.Revision, read.Revision);
        Assert.Equivalent(Details(), read.ProcessingResult.Listing.Details, strict: true);
        Assert.False(reader.Database.HasPendingModelChanges());
    }
}
