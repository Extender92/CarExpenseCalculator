using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HouseholdMigrationTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    private const string PreviousMigration = "20260904132333_LinkSavedScenariosToListings";

    [Fact]
    public async Task Upgrade_keeps_seeded_legacy_data_and_does_not_select_a_profile()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        await Listings(db).ReplaceAsync(car.VehicleId, 1, ListingFactory.Complete());
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var before = await ScalarAsync<string>("SELECT result_snapshot::text FROM saved_cost_scenarios");
        await migrator.MigrateAsync();
        Assert.Equal(before, await ScalarAsync<string>("SELECT result_snapshot::text FROM saved_cost_scenarios"));
        var review = await Transition(db).GetAsync();
        Assert.Equal(0, review.Revision);
        Assert.Equal(new SavedHouseholdProfile(null, 0), review.Profile);
        Assert.Equal(car.VehicleId, Assert.Single(review.Vehicles).VehicleId);
        Assert.Equal(2, review.Vehicles[0].Revision);
        Assert.Equal(ScenarioFactory.Complete().Financing, review.Vehicles[0].Legacy!.Input.Financing);
        Assert.Equal(new SavedVehicleDraft(0, null), await Drafts(db).GetAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Explicit_rollback_discards_household_data_and_orphans_preserving_listings_and_unconverted_cars()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var convertedOnly = await Legacy(db).CreateAsync(Reg(), ScenarioFactory.Complete());
        var combined = await Legacy(db).CreateAsync(Reg("DEF456"), ScenarioFactory.Complete());
        await Listings(db).ReplaceAsync(combined.VehicleId, 1, ListingFactory.ManualOnly());
        var review = await Transition(db).GetAsync();
        await Transition(db).ConfirmAsync(Profile(), 0, review.Revision, KeepAll(review));
        await Costs(db).CreateAsync(Reg("JKL789"), Write());
        await Listings(db).CreateAsync(Reg("MNP123"), ListingFactory.ManualOnly());
        await Legacy(db).CreateAsync(Reg("RST456"), ScenarioFactory.Replacement());
        await Drafts(db).SaveAsync(new(Reg(), Write(), BaseVehicleId: convertedOnly.VehicleId, BaseVehicleRevision: 2), 0);
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(3, await CountAsync("vehicles"));
        Assert.Equal(2, await CountAsync("vehicle_listings"));
        Assert.Equal(1, await CountAsync("saved_cost_scenarios"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM vehicles WHERE registration_number IN ('ABC123', 'JKL789')"));
        foreach (var table in new[] { "household_state", "vehicle_cost_inputs", "vehicle_draft" })
            Assert.False(await ScalarAsync<bool>($"SELECT to_regclass('public.{table}') IS NOT NULL"));
        await migrator.MigrateAsync();
        Assert.Equal(new SavedHouseholdProfile(null, 0), await new HouseholdProfileStore(db).GetAsync());
        Assert.Equal(new SavedVehicleDraft(0, null), await Drafts(db).GetAsync());
        Assert.Equal(0, await CountAsync("vehicle_cost_inputs"));
        Assert.Equal(3, (await Costs(db).ListAsync()).Count);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
