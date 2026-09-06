using System.Data.Common;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class HouseholdConcurrencyTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    [Theory]
    [InlineData("save")]
    [InlineData("replace")]
    [InlineData("delete")]
    [InlineData("adopt")]
    public async Task Competing_draft_mutations_have_one_winner_and_no_partial_writes(string competingOperation)
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        await Drafts(first).SaveAsync(new(Reg(), Write(10)), 0);
        Func<Task> competing = competingOperation switch
        {
            "save" => () => Drafts(second).SaveAsync(new(Reg(), Write(20)), 1),
            "replace" => () => Drafts(second).SaveAsync(new(Reg("DEF456"), Write(20)), 1, true),
            "delete" => () => Drafts(second).DeleteAsync(1),
            _ => () => Drafts(second).AdoptAsync(1),
        };
        var outcomes = await Task.WhenAll(Attempt(() => Drafts(first).AdoptAsync(1)), Attempt(competing));
        Assert.Single(outcomes, x => x is null);
        Assert.Equal("draftRevisionConflict", Assert.Single(outcomes.OfType<HouseholdStoreException>()).Code);
        var slot = await Drafts(first).GetAsync();
        Assert.Equal(2, slot.Revision);
        var cars = await Costs(first).ListAsync();
        Assert.InRange(cars.Count, 0, 1);
        if (cars.Count == 1) { Assert.Null(slot.Input); Assert.Equal(10, cars[0].Input!.PriceSek); }
        else if (competingOperation != "delete") Assert.NotNull(slot.Input);
    }

    [Fact]
    public async Task Adoption_and_vehicle_deletion_cannot_resurrect_a_stale_vehicle()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var car = await Costs(first).CreateAsync(Reg(), Write());
        await Drafts(first).SaveAsync(new(Reg(), Write(100), BaseVehicleId: car.VehicleId, BaseVehicleRevision: 1), 0);
        var outcomes = await Task.WhenAll(Attempt(() => Drafts(first).AdoptAsync(1)), Attempt(() => Costs(second).DeleteAsync(car.VehicleId, 1)));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes.OfType<HouseholdStoreException>());
        var remaining = await Costs(first).GetAsync(car.VehicleId);
        if (remaining is not null) { Assert.Equal(2, remaining.Revision); Assert.Equal(100, remaining.Input!.PriceSek); }
        Assert.Equal(new SavedVehicleDraft(2, null), await Drafts(first).GetAsync());
    }

    [Fact]
    public async Task Concurrent_registration_creation_uses_one_shared_identity_across_old_and_new_stores()
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var outcomes = await Task.WhenAll(Attempt(() => Costs(first).CreateAsync(Reg(" abc-12d "), Write())),
            Attempt(() => Legacy(second).CreateAsync(Reg("ABC12D"), ScenarioFactory.Complete())));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is not null);
        Assert.Contains(outcomes, x => x is HouseholdStoreException { Code: "registrationNumberConflict" }
            or RegistrationNumberConflictException);
        Assert.Equal(1, await CountAsync("vehicles"));
        Assert.Equal(1, await CountAsync("vehicle_cost_inputs") + await CountAsync("saved_cost_scenarios"));
    }

    [Theory]
    [InlineData("transition")]
    [InlineData("legacy")]
    [InlineData("listing")]
    [InlineData("profile")]
    public async Task Transition_races_recheck_all_revisions_after_the_lock(string competingOperation)
    {
        await Fixture.ResetDatabaseAsync();
        await using var first = Fixture.CreateDbContext();
        await using var second = Fixture.CreateDbContext();
        var car = await Legacy(first).CreateAsync(Reg(), ScenarioFactory.Complete());
        var snapshot = await Transition(first).GetAsync();
        Func<Task> competing = competingOperation switch
        {
            "transition" => () => Transition(second).ConfirmAsync(new(), 0, snapshot.Revision, KeepAll(snapshot)),
            "legacy" => () => Legacy(second).ReplaceAsync(car.VehicleId, 1, ScenarioFactory.Replacement(), SavedScenarioListingLinkMode.Preserve),
            "listing" => () => Listings(second).ReplaceAsync(car.VehicleId, 1, ListingFactory.ManualOnly()),
            _ => () => new HouseholdProfileStore(second).SaveAsync(new(), 0),
        };
        var outcomes = await Task.WhenAll(Attempt(() => Transition(first).ConfirmAsync(Profile(), 0, snapshot.Revision, KeepAll(snapshot))), Attempt(competing));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is not null);
        var current = (await Costs(first).GetAsync(car.VehicleId))!;
        var profile = await new HouseholdProfileStore(first).GetAsync();
        Assert.Equal(2, current.Revision);
        if (current.State == VehicleInputState.Current)
        { Assert.Equal(1, profile.Revision); Assert.Equal(0, await CountAsync("saved_cost_scenarios")); }
        else
        { Assert.Equal(0, profile.Revision); Assert.Equal(0, await CountAsync("vehicle_cost_inputs")); }
    }

    [Fact]
    public async Task Cancelled_lock_wait_leaves_all_state_unchanged_and_context_reusable()
    {
        await Fixture.ResetDatabaseAsync();
        await using var locker = new NpgsqlConnection(Fixture.ConnectionString);
        await locker.OpenAsync();
        await using var transaction = await locker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM household_state WHERE id = 1 FOR UPDATE", locker, transaction);
        await command.ExecuteScalarAsync();
        await using var db = Fixture.CreateDbContext();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HouseholdProfileStore(db).SaveAsync(Profile(), 0, cancellation.Token));
        await transaction.RollbackAsync();
        Assert.Equal(new SavedHouseholdProfile(null, 0), await new HouseholdProfileStore(db).GetAsync());
        Assert.Equal(1, (await new HouseholdProfileStore(db).SaveAsync(Profile(), 0)).Revision);
    }

    [Fact]
    public async Task Transition_read_uses_one_snapshot_even_when_legacy_set_changes_between_queries()
    {
        await Fixture.ResetDatabaseAsync();
        await using var writer = Fixture.CreateDbContext();
        await Legacy(writer).CreateAsync(Reg(), ScenarioFactory.Complete());
        var gate = new SnapshotGate();
        var options = new DbContextOptionsBuilder<CarExpenseDbContext>().UseNpgsql(Fixture.ConnectionString,
            x => x.SetPostgresVersion(18, 0)).AddInterceptors(gate).Options;
        await using var reader = new CarExpenseDbContext(options);
        var read = Transition(reader).GetAsync();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Legacy(writer).CreateAsync(Reg("DEF456"), ScenarioFactory.Replacement());
        }
        finally { gate.Release.TrySetResult(); }
        var snapshot = await read;
        Assert.Equal(1, snapshot.Revision);
        Assert.Single(snapshot.Vehicles);
        Assert.Equal(2, (await Transition(writer).GetAsync()).Vehicles.Count);
        await ErrorAsync("transitionRevisionConflict", () => Transition(reader).ConfirmAsync(new(), 0, snapshot.Revision, KeepAll(snapshot)));
    }

    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception exception) { return exception; }
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
