using System.Net;
using System.Net.Http.Json;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ComparisonCancellationEndpointTests
{
    [Fact]
    public async Task Cancelled_rule_save_propagates_cancellation_without_retry_or_success()
    {
        var store = new ControlledRuleStore(false);
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IRuleProfileStore>(); s.AddSingleton<IRuleProfileStore>(store); }));
        using var client = isolated.CreateClient(); using var cancel = new CancellationTokenSource();
        var pending = client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = new { } }, cancel.Token);
        try { await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancel.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        await store.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task Failed_rule_save_is_sanitized_and_not_retried()
    {
        var store = new ControlledRuleStore(true);
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IRuleProfileStore>(); s.AddSingleton<IRuleProfileStore>(store); }));
        using var client = isolated.CreateClient();
        var error = await Json(await client.PutAsJsonAsync("/api/rule-profile", new { expectedRevision = 0, input = new { } }), HttpStatusCode.ServiceUnavailable);
        Assert.DoesNotContain("private-detail", error.ToJsonString()); Assert.Equal(1, store.SaveCalls);
    }

    private sealed class ControlledRuleStore(bool fail) : IRuleProfileStore
    {
        public int SaveCalls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SavedRuleProfile> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SavedRuleProfile(null, 0));
        public async Task<SavedRuleProfile> SaveAsync(RuleProfileInput input, long expectedRevision, CancellationToken cancellationToken = default)
        {
            SaveCalls++; Started.TrySetResult();
            if (fail) throw new NpgsqlException("private-detail");
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            throw new InvalidOperationException("A cancelled operation cannot return success.");
        }
    }
}
