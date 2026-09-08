using System.Data.Common;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class ComparisonConcurrencyTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    private static VehicleFactsStore Facts(CarExpenseDbContext db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Database_failure_after_vehicle_update_rolls_back_facts_confirmation_and_revision()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Costs(db).CreateAsync(Reg(), Write());
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_comparison_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected disposable test failure'; END $$;
            CREATE TRIGGER reject_comparison_write BEFORE INSERT ON vehicle_comparison_facts
            FOR EACH ROW EXECUTE FUNCTION reject_comparison_write();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Facts(db).SaveAsync(car.VehicleId, 1,
                new(new() { Seats = new(FactEditKind.Manual, new(5)) }, CostConfirmation: CostConfirmationAction.Confirm)));
            await using var fresh = Fixture.CreateDbContext();
            var saved = (await Facts(fresh).GetAsync(car.VehicleId))!;
            Assert.Equal(1, saved.Revision); Assert.Null(saved.Input); Assert.Null(saved.CostConfirmedAt);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_comparison_write ON vehicle_comparison_facts;
                DROP FUNCTION reject_comparison_write();
                """);
        }
    }

    [Fact]
    public void Caller_lists_cannot_change_fact_operations_after_construction()
    {
        var selections = new List<FactSelection<int>> { new(FactSelectionKind.Manual, Manual: new(5)), new(FactSelectionKind.Manual, Manual: new(7)) };
        var conflict = new FactEdit<int>(FactEditKind.Conflict, Observations: selections);
        var notes = new List<FactEdit<string>> { new(FactEditKind.Manual, new("Note")) };
        var edits = new VehicleFactEdits { Seats = conflict, ConditionNotes = notes };
        selections.Clear(); notes.Clear();
        Assert.Equal(2, edits.Seats.Observations!.Count); Assert.Single(edits.ConditionNotes!);
    }

    private static async Task<Exception?> Attempt(Func<Task> action)
    { try { await action(); return null; } catch (Exception e) { return e; } }

    [Fact]
    public async Task Competing_first_rule_saves_have_one_winner()
    {
        await Fixture.ResetDatabaseAsync();
        await using var a = Fixture.CreateDbContext(); await using var b = Fixture.CreateDbContext();
        var results = await Task.WhenAll(Attempt(() => new RuleProfileStore(a).SaveAsync(new(), 0)),
            Attempt(() => new RuleProfileStore(b).SaveAsync(new(preferences: [new("purchasePriceSek", 2, EvidenceRequirement.Advertised, 100000, 0)]), 0)));
        Assert.Single(results, x => x is null);
        Assert.Equal("ruleProfileRevisionConflict", Assert.IsType<ComparisonStoreException>(Assert.Single(results, x => x is not null)).Code);
        Assert.Equal(1, (await new RuleProfileStore(a).GetAsync()).Revision);
    }

    [Theory]
    [InlineData("facts")]
    [InlineData("cost")]
    [InlineData("listing")]
    [InlineData("delete")]
    public async Task Fact_save_and_competing_aggregate_change_never_overwrite_each_other(string other)
    {
        await Fixture.ResetDatabaseAsync();
        await using var a = Fixture.CreateDbContext(); await using var b = Fixture.CreateDbContext();
        var car = await Costs(a).CreateAsync(Reg(), Write());
        Func<Task> competitor = other switch
        {
            "facts" => () => Facts(b).SaveAsync(car.VehicleId, 1, new(new() { Seats = new(FactEditKind.Manual, new(7)) })),
            "cost" => () => Costs(b).ReplaceAsync(car.VehicleId, 1, Write(35000)),
            "listing" => () => Listings(b).ReplaceAsync(car.VehicleId, 1, ListingFactory.ManualOnly()),
            _ => () => Costs(b).DeleteAsync(car.VehicleId, 1),
        };
        var outcomes = await Task.WhenAll(Attempt(() => Facts(a).SaveAsync(car.VehicleId, 1,
            new(new() { Seats = new(FactEditKind.Manual, new(5)) }))), Attempt(competitor));
        Assert.Single(outcomes, x => x is null); Assert.Single(outcomes, x => x is not null);
        var saved = await Facts(a).GetAsync(car.VehicleId);
        if (saved is not null) Assert.Equal(2, saved.Revision);
        else Assert.Equal(0, await CountAsync("vehicle_comparison_facts"));
    }

    [Fact]
    public async Task Snapshot_keeps_profile_rules_vehicle_and_cost_consistent_across_a_writer()
    {
        await Fixture.ResetDatabaseAsync();
        await using var writer = Fixture.CreateDbContext();
        var car = await Costs(writer).CreateAsync(Reg(), Write(40000));
        var gate = new SnapshotGate();
        var options = new DbContextOptionsBuilder<CarExpenseDbContext>().UseNpgsql(Fixture.ConnectionString,
            x => x.SetPostgresVersion(18, 0)).AddInterceptors(gate).Options;
        await using var reader = new CarExpenseDbContext(options);
        var read = new ComparisonSnapshotStore(reader).ReadAsync([car.VehicleId]);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await new RuleProfileStore(writer).SaveAsync(new(), 0);
            await Costs(writer).ReplaceAsync(car.VehicleId, 1, Write(35000));
        }
        finally { gate.Release.TrySetResult(); }
        var result = await read;
        Assert.Equal(0, result.Rules.Revision); Assert.Equal(1, result.Vehicles[0].Facts.Revision);
        Assert.Equal(40000, result.Vehicles[0].Cost.Input!.PriceSek);
        var next = await new ComparisonSnapshotStore(writer).ReadAsync([car.VehicleId]);
        Assert.Equal(1, next.Rules.Revision); Assert.Equal(2, next.Vehicles[0].Facts.Revision);
    }

    [Fact]
    public async Task Cancelled_fact_save_waiting_for_lock_leaves_no_partial_state()
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        var car = await Costs(db).CreateAsync(Reg(), Write());
        await using var locker = new NpgsqlConnection(Fixture.ConnectionString);
        await locker.OpenAsync(); await using var tx = await locker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM household_state WHERE id=1 FOR UPDATE", locker, tx);
        await command.ExecuteScalarAsync();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Facts(db).SaveAsync(car.VehicleId, 1,
            new(new() { Seats = new(FactEditKind.Manual, new(5)) }, CostConfirmation: CostConfirmationAction.Confirm), cancel.Token));
        await tx.RollbackAsync();
        var saved = (await Facts(db).GetAsync(car.VehicleId))!;
        Assert.Equal(1, saved.Revision); Assert.Null(saved.Input); Assert.Null(saved.CostConfirmedAt);
    }

    private sealed class SnapshotGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("household_state", StringComparison.Ordinal) && Entered.TrySetResult())
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
