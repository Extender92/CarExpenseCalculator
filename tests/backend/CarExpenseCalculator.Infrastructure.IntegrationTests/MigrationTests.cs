using CarExpenseCalculator.Infrastructure.Persistence;
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
    public async Task Initial_migration_applies_and_rolls_back_all_product_tables()
    {
        await fixture.ResetDatabaseAsync();

        foreach (var table in ProductTables)
        {
            Assert.True(await TableExistsAsync(table));
        }

        await using (var dbContext = fixture.CreateDbContext())
        {
            var applied = await dbContext.Database.GetAppliedMigrationsAsync();
            Assert.Single(applied);
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(Migration.InitialDatabase);
        }

        foreach (var table in ProductTables)
        {
            Assert.False(await TableExistsAsync(table));
        }
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

    private static readonly string[] ProductTables =
    [
        "vehicles",
        "saved_cost_scenarios",
        "scenario_energy_sources",
        "scenario_recurring_costs",
        "scenario_one_time_costs",
    ];

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
}
