using System.Net;
using System.Net.Http.Json;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using static CarExpenseCalculator.Api.IntegrationTests.HouseholdApiTestData;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class HouseholdIsolationEndpointTests
{
    [Fact]
    public async Task OpenApi_keeps_v1_cadence_nonnullable_and_documents_real_problem_media_types()
    {
        using var factory = new ManualCalculationApiFactory();
        using var client = factory.CreateClient();
        var document = await Json(await client.GetAsync("/api/openapi/v1.json"));
        var cadence = document["components"]!["schemas"]!["RecurringCostCadence"]!;
        Assert.Equal(["monthly", "annual"], cadence["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.DoesNotContain("null", cadence.ToJsonString());
        Assert.NotNull(document["components"]!["schemas"]!["LegacyRecurringCostCadence"]);
        var errors = document["paths"]!["/api/household-calculations/preview"]!["post"]!["responses"]!["400"]!["content"]!;
        Assert.NotNull(errors["application/problem+json"]);
        Assert.Null(errors["text/plain"]);
        var recovery = document["components"]!["schemas"]!["SavedCostScenarioProblemDetails"]!["properties"]!;
        Assert.NotNull(recovery["recoveryRoute"]);
        Assert.NotNull(recovery["vehicleId"]);
    }

    [Fact]
    public async Task Preview_does_not_resolve_any_store_or_call_extraction()
    {
        using var factory = new ManualCalculationApiFactory();
        var extractor = new FakeListingExtractionService
        {
            ExtractionHandler = (_, _) => throw new InvalidOperationException("Extraction must not run."),
            StatusHandler = _ => throw new InvalidOperationException("Extraction status must not run."),
        };
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHouseholdProfileStore>(); services.RemoveAll<IVehicleCostInputStore>();
            services.RemoveAll<ISharedVehicleDraftStore>(); services.RemoveAll<IHouseholdTransitionStore>();
            services.AddScoped<IHouseholdProfileStore>(_ => throw new InvalidOperationException("Store must not resolve."));
            services.AddScoped<IVehicleCostInputStore>(_ => throw new InvalidOperationException("Store must not resolve."));
            services.AddScoped<ISharedVehicleDraftStore>(_ => throw new InvalidOperationException("Store must not resolve."));
            services.AddScoped<IHouseholdTransitionStore>(_ => throw new InvalidOperationException("Store must not resolve."));
            services.RemoveAll<IListingExtractionService>(); services.AddSingleton<IListingExtractionService>(extractor);
        }));
        using var client = isolated.CreateClient();
        await Json(await Post(client, Preview()));
        Assert.Equal(0, extractor.ExtractionCallCount); Assert.Equal(0, extractor.StatusCallCount);
    }

    [Theory]
    [InlineData("/api/household-profile")]
    [InlineData("/api/vehicle-cost-inputs")]
    [InlineData("/api/household-transition")]
    [InlineData("/api/vehicle-draft")]
    public async Task Unreachable_storage_returns_sanitized_503_without_extraction(string route)
    {
        using var factory = new ListingAnalysisApiFactory();
        using var client = factory.CreateClient();
        var problem = await Json(await client.GetAsync(route), HttpStatusCode.ServiceUnavailable);
        Assert.Equal("householdStorageUnavailable", problem["code"]!.GetValue<string>());
        Assert.DoesNotContain("Password", problem.ToJsonString()); Assert.DoesNotContain("127.0.0.1", problem.ToJsonString());
        Assert.Equal(0, factory.ExtractionService.ExtractionCallCount); Assert.Equal(0, factory.ExtractionService.StatusCallCount);
    }

    [Fact]
    public async Task Failed_save_is_not_retried_or_reported_as_success()
    {
        var store = new FailingProfileStore();
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<IHouseholdProfileStore>(); services.AddSingleton<IHouseholdProfileStore>(store); }));
        using var client = isolated.CreateClient();
        await Json(await client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }), HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task Request_cancellation_reaches_the_store_without_retry()
    {
        var store = new WaitingProfileStore();
        using var factory = new ManualCalculationApiFactory();
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<IHouseholdProfileStore>(); services.AddSingleton<IHouseholdProfileStore>(store); }));
        using var client = isolated.CreateClient();
        using var cancellation = new CancellationTokenSource();
        var save = client.PutAsJsonAsync("/api/household-profile", new { expectedRevision = 0, input = Profile() }, cancellation.Token);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await save);
        await store.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, store.SaveCalls);
    }

    private sealed class FailingProfileStore : IHouseholdProfileStore
    {
        public int SaveCalls { get; private set; }
        public Task<SavedHouseholdProfile> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SavedHouseholdProfile> SaveAsync(HouseholdProfileInput input, long expectedRevision, CancellationToken cancellationToken = default)
        { SaveCalls++; throw new NpgsqlException("Synthetic database failure with details that must not leak."); }
    }
    private sealed class WaitingProfileStore : IHouseholdProfileStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SaveCalls { get; private set; }
        public Task<SavedHouseholdProfile> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<SavedHouseholdProfile> SaveAsync(HouseholdProfileInput input, long expectedRevision, CancellationToken cancellationToken = default)
        {
            SaveCalls++; Started.SetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled.SetResult(); throw; }
            throw new InvalidOperationException("Cancellation was not propagated.");
        }
    }
}
