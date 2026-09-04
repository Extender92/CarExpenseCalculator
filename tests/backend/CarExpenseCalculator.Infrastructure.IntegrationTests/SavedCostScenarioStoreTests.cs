using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class SavedCostScenarioStoreTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_and_read_round_trip_the_complete_current_aggregate()
    {
        await fixture.ResetDatabaseAsync();
        var timeProvider = new MutableTimeProvider(InitialTime);
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext, timeProvider);
        var scenario = ScenarioFactory.Complete();

        var created = await store.CreateAsync(
            RegistrationNumber.Parse(" abc-12d "),
            scenario);
        var byId = await store.GetAsync(created.VehicleId);
        var byRegistration = await store.GetByRegistrationNumberAsync(
            RegistrationNumber.Parse("ABC12D"));

        Assert.Equal(7, created.VehicleId.Version);
        Assert.Equal("ABC12D", created.RegistrationNumber.Value);
        Assert.Equal(1, created.Revision);
        Assert.Equal(1, created.CalculationVersion);
        Assert.Equal(1, created.ResultSchemaVersion);
        Assert.Equal(InitialTime, created.CreatedAtUtc);
        Assert.Equal(InitialTime, created.UpdatedAtUtc);
        Assert.Equal(InitialTime, created.CalculatedAtUtc);
        Assert.NotNull(byId);
        Assert.NotNull(byRegistration);
        AssertSavedScenario(created, byId);
        AssertSavedScenario(created, byRegistration);

        Assert.Equal("Volvo V70", created.Scenario.VehicleLabel);
        Assert.Equal(scenario.PurchasePriceSek, created.Scenario.PurchasePriceSek);
        Assert.Equal(
            scenario.ExpectedResidualValueSek,
            created.Scenario.ExpectedResidualValueSek);
        Assert.Equal(
            scenario.AnnualDistanceKilometres,
            created.Scenario.AnnualDistanceKilometres);
        Assert.Equal(scenario.Financing, created.Scenario.Financing);
        Assert.Collection(
            created.Scenario.EnergySources,
            source =>
            {
                Assert.Equal("Bensin", source.Label);
                Assert.Equal(EnergyUnit.Litre, source.Unit);
                Assert.Equal(65m, source.DistanceSharePercent);
            },
            source =>
            {
                Assert.Equal("El", source.Label);
                Assert.Equal(EnergyUnit.KilowattHour, source.Unit);
                Assert.Equal(35m, source.DistanceSharePercent);
            });
        Assert.Collection(
            created.Scenario.OtherRecurringCosts,
            cost => Assert.Equal("Parkering", cost.Label),
            cost => Assert.Equal("Däckhotell", cost.Label));
        Assert.Collection(
            created.Scenario.OtherOneTimeCosts,
            cost => Assert.Equal("Besiktning", cost.Label),
            cost => Assert.Equal("Tillbehör", cost.Label));
        Assert.Equivalent(
            new CostScenarioCalculator().Calculate(scenario),
            created.Result,
            strict: true);

        var jsonType = await ScalarAsync<string>(
            dbContext,
            "SELECT jsonb_typeof(result_snapshot) FROM saved_cost_scenarios");
        Assert.Equal("object", jsonType);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_normalized_registration_number()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        var first = await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete());

        var exception = await Assert.ThrowsAsync<RegistrationNumberConflictException>(
            () => store.CreateAsync(
                RegistrationNumber.Parse("abc-123"),
                ScenarioFactory.Replacement()));

        Assert.Equal(first.VehicleId, exception.ExistingVehicleId);
        Assert.Equal("ABC123", exception.RegistrationNumber.Value);
        Assert.Equal(1L, await ScalarAsync<long>(dbContext, "SELECT count(*) FROM vehicles"));
    }

    [Fact]
    public async Task List_orders_current_aggregates_by_update_time_then_id()
    {
        await fixture.ResetDatabaseAsync();
        var timeProvider = new MutableTimeProvider(InitialTime);
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext, timeProvider);
        var first = await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete("First"));
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var second = await store.CreateAsync(
            RegistrationNumber.Parse("DEF456"),
            ScenarioFactory.Complete("Second"));

        var saved = await store.ListAsync();

        Assert.Equal([second.VehicleId, first.VehicleId], saved.Select(item => item.VehicleId));
    }

    [Fact]
    public async Task Replace_atomically_removes_old_children_and_result_then_increments_revision()
    {
        await fixture.ResetDatabaseAsync();
        var timeProvider = new MutableTimeProvider(InitialTime);
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext, timeProvider);
        var created = await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete());
        var oldChildIds = await ChildIdsAsync(dbContext);
        timeProvider.Advance(TimeSpan.FromHours(2));

        var replaced = await store.ReplaceAsync(
            created.VehicleId,
            created.Revision,
            ScenarioFactory.Replacement(),
            SavedScenarioListingLinkMode.Preserve);

        Assert.Equal(created.VehicleId, replaced.VehicleId);
        Assert.Equal(created.RegistrationNumber, replaced.RegistrationNumber);
        Assert.Equal(created.CreatedAtUtc, replaced.CreatedAtUtc);
        Assert.Equal(2, replaced.Revision);
        Assert.Equal(InitialTime.AddHours(2), replaced.UpdatedAtUtc);
        Assert.Equal(InitialTime.AddHours(2), replaced.CalculatedAtUtc);
        Assert.Equal("Saab 9-5", replaced.Scenario.VehicleLabel);
        Assert.Empty(replaced.Scenario.EnergySources);
        Assert.Single(replaced.Scenario.OtherRecurringCosts);
        Assert.Single(replaced.Scenario.OtherOneTimeCosts);
        Assert.Null(replaced.Scenario.VehicleTax);
        Assert.Equal(0m, replaced.Scenario.Insurance!.AmountSek);
        Assert.Null(replaced.Result.NetOwnershipCost);

        var newChildIds = await ChildIdsAsync(dbContext);
        Assert.Empty(oldChildIds.Intersect(newChildIds));
        Assert.Equal(0L, await ScalarAsync<long>(dbContext, "SELECT count(*) FROM scenario_energy_sources"));
        Assert.Equal(1L, await ScalarAsync<long>(dbContext, "SELECT count(*) FROM scenario_recurring_costs"));
        Assert.Equal(1L, await ScalarAsync<long>(dbContext, "SELECT count(*) FROM scenario_one_time_costs"));
        var snapshot = await ScalarAsync<string>(
            dbContext,
            "SELECT result_snapshot::text FROM saved_cost_scenarios");
        Assert.DoesNotContain("Bensin", snapshot, StringComparison.Ordinal);
        Assert.Contains("residualValue", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_updates_and_deletes_are_rejected_without_changing_current_data()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        var created = await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete());
        var replaced = await store.ReplaceAsync(
            created.VehicleId,
            created.Revision,
            ScenarioFactory.Replacement(),
            SavedScenarioListingLinkMode.Preserve);

        var updateException = await Assert.ThrowsAsync<SavedCostScenarioConcurrencyException>(
            () => store.ReplaceAsync(
                created.VehicleId,
                created.Revision,
                ScenarioFactory.Complete("Stale"),
                SavedScenarioListingLinkMode.Preserve));
        var deleteException = await Assert.ThrowsAsync<SavedCostScenarioConcurrencyException>(
            () => store.DeleteAsync(created.VehicleId, created.Revision));

        Assert.Equal(replaced.Revision, updateException.ActualRevision);
        Assert.Equal(replaced.Revision, deleteException.ActualRevision);
        var current = await store.GetAsync(created.VehicleId);
        Assert.NotNull(current);
        Assert.Equal("Saab 9-5", current.Scenario.VehicleLabel);
        Assert.Equal(2, current.Revision);
    }

    [Fact]
    public async Task Missing_aggregates_produce_typed_outcomes()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        var missingId = Guid.CreateVersion7();

        Assert.Null(await store.GetAsync(missingId));
        await Assert.ThrowsAsync<SavedCostScenarioNotFoundException>(
            () => store.ReplaceAsync(
                missingId,
                1,
                ScenarioFactory.Replacement(),
                SavedScenarioListingLinkMode.Preserve));
        await Assert.ThrowsAsync<SavedCostScenarioNotFoundException>(
            () => store.DeleteAsync(missingId, 1));
    }

    [Fact]
    public async Task Delete_physically_removes_the_complete_aggregate_through_cascades()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        var created = await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete());

        await store.DeleteAsync(created.VehicleId, created.Revision);

        Assert.Null(await store.GetAsync(created.VehicleId));
        foreach (var table in new[]
                 {
                     "vehicles",
                     "saved_cost_scenarios",
                     "scenario_energy_sources",
                     "scenario_recurring_costs",
                     "scenario_one_time_costs",
                 })
        {
            Assert.Equal(0L, await ScalarAsync<long>(dbContext, $"SELECT count(*) FROM {table}"));
        }
    }

    [Fact]
    public async Task Database_constraints_reject_invalid_ranges_and_orphaned_children()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        await store.CreateAsync(
            RegistrationNumber.Parse("ABC123"),
            ScenarioFactory.Complete());

        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteNonQueryAsync(
                fixture.ConnectionString,
                "UPDATE saved_cost_scenarios SET calculation_period_months = 0"));
        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteNonQueryAsync(
                fixture.ConnectionString,
                "UPDATE saved_cost_scenarios SET source_listing_version = 0"));
        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteNonQueryAsync(
                fixture.ConnectionString,
                "UPDATE vehicles SET registration_number = 'ABI123'"));
        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteNonQueryAsync(
                fixture.ConnectionString,
                """
                INSERT INTO scenario_one_time_costs
                    (id, scenario_id, position, label, amount_sek)
                VALUES
                    ('00000000-0000-7000-8000-000000000001',
                     '00000000-0000-7000-8000-000000000002', 0, 'Orphan', 1)
                """));
    }

    private static SavedCostScenarioStore CreateStore(
        CarExpenseDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        return new SavedCostScenarioStore(
            dbContext,
            new CostScenarioCalculator(),
            timeProvider ?? new MutableTimeProvider(InitialTime));
    }

    private static void AssertSavedScenario(
        SavedCostScenario expected,
        SavedCostScenario? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.VehicleId, actual.VehicleId);
        Assert.Equal(expected.RegistrationNumber, actual.RegistrationNumber);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(expected.SourceListingVersion, actual.SourceListingVersion);
        Assert.Equal(expected.CurrentListingVersion, actual.CurrentListingVersion);
        Assert.Equal(expected.HasSavedListing, actual.HasSavedListing);
        Assert.Equivalent(expected.Scenario, actual.Scenario, strict: true);
        Assert.Equivalent(expected.Result, actual.Result, strict: true);
    }

    private static async Task<IReadOnlyList<Guid>> ChildIdsAsync(CarExpenseDbContext dbContext)
    {
        var ids = new List<Guid>();
        await using var connection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id FROM scenario_energy_sources
            UNION ALL SELECT id FROM scenario_recurring_costs
            UNION ALL SELECT id FROM scenario_one_time_costs
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task<T> ScalarAsync<T>(
        CarExpenseDbContext dbContext,
        string sql)
    {
        await using var connection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return (T)result!;
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
