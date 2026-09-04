using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MigrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task All_migrations_apply_and_roll_back_all_product_tables()
    {
        await fixture.ResetDatabaseAsync();

        foreach (var table in ProductTables)
        {
            Assert.True(await TableExistsAsync(table));
        }

        await using (var dbContext = fixture.CreateDbContext())
        {
            var applied = await dbContext.Database.GetAppliedMigrationsAsync();
            Assert.Equal(3, applied.Count());
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(Migration.InitialDatabase);
        }

        foreach (var table in ProductTables)
        {
            Assert.False(await TableExistsAsync(table));
        }
    }

    [Fact]
    public async Task Listing_migration_upgrades_the_existing_schema()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        await migrator.MigrateAsync(InitialScenarioMigration);

        foreach (var table in ScenarioTables)
        {
            Assert.True(await TableExistsAsync(table));
        }

        foreach (var table in ListingTables)
        {
            Assert.False(await TableExistsAsync(table));
        }

        await migrator.MigrateAsync();
        foreach (var table in ProductTables)
        {
            Assert.True(await TableExistsAsync(table));
        }
    }

    [Fact]
    public async Task Listing_rollback_removes_listing_only_roots_and_preserves_combined_scenarios()
    {
        await fixture.ResetDatabaseAsync();
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        await using (var context = fixture.CreateDbContext())
        {
            var listingStore = new SavedListingStore(context, new ListingDraftProcessor(), time);
            await listingStore.CreateAsync(
                RegistrationNumber.Parse("ABC123"),
                ListingFactory.ManualOnly("Listing only"));
        }

        SavedCostScenario combined;
        await using (var context = fixture.CreateDbContext())
        {
            var scenarioStore = new SavedCostScenarioStore(context, new CostScenarioCalculator(), time);
            combined = await scenarioStore.CreateAsync(
                RegistrationNumber.Parse("DEF456"),
                ScenarioFactory.Complete("Combined"));
            await scenarioStore.CreateAsync(
                RegistrationNumber.Parse("JKL789"),
                ScenarioFactory.Complete("Scenario only"));
        }

        await using (var context = fixture.CreateDbContext())
        {
            var listingStore = new SavedListingStore(context, new ListingDraftProcessor(), time);
            await listingStore.ReplaceAsync(
                combined.VehicleId,
                combined.Revision,
                ListingFactory.Complete("DEF456", "Combined"));
        }

        await using (var context = fixture.CreateDbContext())
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(InitialScenarioMigration);
        }

        foreach (var table in ListingTables)
        {
            Assert.False(await TableExistsAsync(table));
        }

        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM vehicles"));
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM saved_cost_scenarios"));
        Assert.Equal(
                2L,
                await ScalarAsync<long>(
                "SELECT count(*) FROM vehicles WHERE registration_number IN ('DEF456', 'JKL789')"));
    }

    [Fact]
    public async Task Linking_migration_preserves_existing_scenarios_and_rolls_back_only_link_metadata()
    {
        await fixture.ResetDatabaseAsync();
        SavedCostScenario scenario;
        await using (var context = fixture.CreateDbContext())
        {
            scenario = await new SavedCostScenarioStore(
                context,
                new CostScenarioCalculator(),
                TimeProvider.System).CreateAsync(
                    RegistrationNumber.Parse("ABC123"),
                    ScenarioFactory.Complete());
        }

        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT count(*) FROM saved_cost_scenarios WHERE source_listing_version IS NOT NULL"));
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE saved_cost_scenarios SET source_listing_version = 1";
            await command.ExecuteNonQueryAsync();
        }
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT source_listing_version FROM saved_cost_scenarios"));

        await using (var context = fixture.CreateDbContext())
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(CurrentListingMigration);
        }

        Assert.True(await TableExistsAsync("saved_cost_scenarios"));
        Assert.Equal(
            scenario.VehicleId,
            await ScalarAsync<Guid>("SELECT vehicle_id FROM saved_cost_scenarios"));
        Assert.False(await ColumnExistsAsync("saved_cost_scenarios", "source_listing_version"));
    }

    [Fact]
    public async Task Current_model_has_no_pending_changes()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Explicit_migration_runner_applies_a_target_and_reports_invalid_targets()
    {
        await fixture.ResetDatabaseAsync();
        await using (var dbContext = fixture.CreateDbContext())
        {
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(Migration.InitialDatabase);
        }

        await using var services = CreateMigrationServices();
        await DatabaseMigrationRunner.RunAsync(services);
        Assert.True(await TableExistsAsync("vehicles"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseMigrationRunner.RunAsync(services, "not-a-real-migration"));
        Assert.True(await TableExistsAsync("vehicles"));

        await DatabaseMigrationRunner.RunAsync(services, Migration.InitialDatabase);
        Assert.False(await TableExistsAsync("vehicles"));
    }

    private const string InitialScenarioMigration = "20260830181537_InitialSavedCostScenarios";
    private const string CurrentListingMigration = "20260904100409_AddCurrentVehicleListings";

    private static readonly string[] ScenarioTables =
    [
        "vehicles",
        "saved_cost_scenarios",
        "scenario_energy_sources",
        "scenario_recurring_costs",
        "scenario_one_time_costs",
    ];

    private static readonly string[] ListingTables =
    [
        "vehicle_listings",
        "listing_sources",
        "listing_equipment",
    ];

    private static readonly string[] ProductTables = [.. ScenarioTables, .. ListingTables];

    private ServiceProvider CreateMigrationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CarExpenseDbContext>(options =>
            options.UseNpgsql(
                fixture.ConnectionString,
                npgsql => npgsql.SetPostgresVersion(18, 0)));
        return services.BuildServiceProvider();
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@table_name) IS NOT NULL";
        command.Parameters.AddWithValue("table_name", $"public.{tableName}");
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @table_name
                  AND column_name = @column_name)
            """;
        command.Parameters.AddWithValue("table_name", tableName);
        command.Parameters.AddWithValue("column_name", columnName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
