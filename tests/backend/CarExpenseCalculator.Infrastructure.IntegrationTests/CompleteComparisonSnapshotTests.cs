using System.Data.Common;
using System.Globalization;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class CompleteComparisonSnapshotTests(PostgreSqlFixture fixture) : HouseholdTestSupport(fixture)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(250)]
    public async Task All_input_states_are_read_once_without_consuming_a_draft(int count)
    {
        await Fixture.ResetDatabaseAsync();
        await using var db = Fixture.CreateDbContext();
        foreach (var i in Enumerable.Range(100, count).Reverse())
        {
            var registration = Reg($"ABC{i:000}");
            switch (i % 4)
            {
                case 0: await Listings(db).CreateAsync(registration, ListingFactory.ManualOnly()); break;
                case 1: await Legacy(db).CreateAsync(registration, ScenarioFactory.Complete()); break;
                case 2: await Costs(db).CreateAsync(registration, Write()); break;
                default: await Costs(db).CreateAsync(registration, new(VehicleCostInput.ForLease("x", null))); break;
            }
        }
        var store = new ComparisonSnapshotStore(db);
        var baseline = await store.ReadBaselineAsync();
        await Drafts(db).SaveAsync(new(Reg("ZZZ999"), Write(100)), 0);
        var result = await store.ReadAllAsync(baseline.BaselineToken);
        Assert.Equal(count, result.Baseline.CandidateCount);
        Assert.Equal(count, result.Snapshot.Vehicles.Select(x => x.Facts.VehicleId).Distinct().Count());
        Assert.Equal(Enumerable.Range(100, count).Select(i => $"ABC{i:000}"), result.Snapshot.Vehicles.Select(x => x.Facts.RegistrationNumber.Value));
        Assert.Null(result.Baseline.Profile.Input); Assert.Null(result.Baseline.Rules.Input);
        Assert.Equal(baseline.BaselineToken, (await store.ReadBaselineAsync()).BaselineToken);
        Assert.NotNull((await Drafts(db).GetAsync()).Input);
        if (count >= 4) Assert.Equal(3, result.Snapshot.Vehicles.Select(x => x.Cost.State).Distinct().Count());
    }

    [Fact]
    public async Task Baseline_is_culture_independent_and_changes_even_for_equivalent_saved_values()
    {
        await Fixture.ResetDatabaseAsync(); await using var db = Fixture.CreateDbContext();
        var vehicle = await Costs(db).CreateAsync(Reg(), Write(40000));
        var store = new ComparisonSnapshotStore(db);
        var baseline = await store.ReadBaselineAsync();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(baseline.BaselineToken, (await store.ReadBaselineAsync()).BaselineToken);
        }
        finally { CultureInfo.CurrentCulture = original; }
        await Costs(db).ReplaceAsync(vehicle.VehicleId, vehicle.Revision, Write(40000.00m));
        var conflict = await Assert.ThrowsAsync<ComparisonStoreException>(() => store.ReadAllAsync(baseline.BaselineToken));
        Assert.Equal("comparisonBaselineConflict", conflict.Code);
        Assert.Equal((await store.ReadBaselineAsync()).BaselineToken, conflict.ActualBaselineToken);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("cost")]
    [InlineData("facts")]
    [InlineData("listing")]
    [InlineData("profile")]
    [InlineData("rules")]
    public async Task Changes_between_groups_cannot_mix_membership_or_source_revisions(string change)
    {
        await Fixture.ResetDatabaseAsync(); await using var writer = Fixture.CreateDbContext();
        SavedVehicleCostInput? last = null;
        for (var i = 100; i <= 200; i++) last = await Costs(writer).CreateAsync(Reg($"ABC{i}"), Write(40000));
        var baseline = await new ComparisonSnapshotStore(writer).ReadBaselineAsync();
        var gate = new BatchGate();
        await using var reader = Reader(gate);
        var pending = new ComparisonSnapshotStore(reader).ReadAllAsync(baseline.BaselineToken);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            switch (change)
            {
                case "add": await Costs(writer).CreateAsync(Reg("ABC201"), Write(100)); break;
                case "delete": await Costs(writer).DeleteAsync(last!.VehicleId, 1); break;
                case "cost": await Costs(writer).ReplaceAsync(last!.VehicleId, 1, Write(35000)); break;
                case "facts": await new VehicleFactsStore(writer, TimeProvider.System).SaveAsync(last!.VehicleId, 1, new(new() { Seats = new(FactEditKind.Manual, new(5)) })); break;
                case "listing": await Listings(writer).ReplaceAsync(last!.VehicleId, 1, ListingFactory.ManualOnly()); break;
                case "profile": await new HouseholdProfileStore(writer).SaveAsync(Profile(), 0); break;
                case "rules": await new RuleProfileStore(writer).SaveAsync(new(), 0); break;
            }
        }
        finally { gate.Release.TrySetResult(); }
        var result = await pending;
        Assert.Equal(101, result.Snapshot.Vehicles.Count);
        Assert.Equal(40000, result.Snapshot.Vehicles[^1].Cost.Input!.PriceSek);
        Assert.Equal(1, result.Snapshot.Vehicles[^1].Facts.Revision);
        Assert.Null(result.Snapshot.Vehicles[^1].Facts.Input);
        Assert.Null(result.Snapshot.Vehicles[^1].Facts.CurrentListingVersion);
        Assert.Equal(0, result.Baseline.Profile.Revision); Assert.Equal(0, result.Baseline.Rules.Revision);
        Assert.Equal(baseline.BaselineToken, result.Baseline.BaselineToken);
        await using var fresh = Fixture.CreateDbContext();
        var next = new ComparisonSnapshotStore(fresh);
        Assert.NotEqual(baseline.BaselineToken, (await next.ReadBaselineAsync()).BaselineToken);
        Assert.Equal("comparisonBaselineConflict", (await Assert.ThrowsAsync<ComparisonStoreException>(() => next.ReadAllAsync(baseline.BaselineToken))).Code);
    }

    [Fact]
    public async Task Cancellation_during_a_group_releases_the_read_transaction()
    {
        await Fixture.ResetDatabaseAsync(); await using var writer = Fixture.CreateDbContext();
        await Costs(writer).CreateAsync(Reg(), Write());
        var baseline = await new ComparisonSnapshotStore(writer).ReadBaselineAsync();
        var gate = new BatchGate(); await using var reader = Reader(gate);
        using var cancel = new CancellationTokenSource();
        var pending = new ComparisonSnapshotStore(reader).ReadAllAsync(baseline.BaselineToken, cancel.Token);
        try { await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancel.Cancel(); gate.Release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(reader.Database.CurrentTransaction);
        Assert.Single((await new ComparisonSnapshotStore(reader).ReadAllAsync(baseline.BaselineToken)).Snapshot.Vehicles);
    }

    private CarExpenseDbContext Reader(BatchGate gate) => new(new DbContextOptionsBuilder<CarExpenseDbContext>()
        .UseNpgsql(Fixture.ConnectionString, x => x.SetPostgresVersion(18, 0)).AddInterceptors(gate).Options);

    private sealed class BatchGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("ANY", StringComparison.Ordinal) && Entered.TrySetResult())
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
