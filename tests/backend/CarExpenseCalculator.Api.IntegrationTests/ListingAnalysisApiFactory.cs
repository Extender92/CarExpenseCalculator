using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ListingAnalysisApiFactory : WebApplicationFactory<Program>
{
    public FakeListingExtractionService ExtractionService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=unreachable;Password=unreachable;Timeout=1");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IListingExtractionService>();
            services.AddSingleton(ExtractionService);
            services.AddSingleton<IListingExtractionService>(ExtractionService);
        });
    }
}

public sealed class FakeListingExtractionService : IListingExtractionService
{
    private int _extractionCallCount;
    private int _statusCallCount;

    public Func<ListingUrl, CancellationToken, Task<ListingExtractionOutcome>> ExtractionHandler { get; set; } =
        (_, _) => Task.FromResult<ListingExtractionOutcome>(
            new ListingExtractionFailure(ListingExtractionFailureCode.NotConfigured));

    public Func<CancellationToken, Task<ListingExtractionConfigurationStatus>> StatusHandler { get; set; } =
        _ => Task.FromResult(new ListingExtractionConfigurationStatus(false, null, null, null));

    public int ExtractionCallCount => Volatile.Read(ref _extractionCallCount);

    public int StatusCallCount => Volatile.Read(ref _statusCallCount);

    public Task<ListingExtractionOutcome> ExtractAsync(
        ListingUrl listingUrl,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _extractionCallCount);
        return ExtractionHandler(listingUrl, cancellationToken);
    }

    public Task<ListingExtractionConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _statusCallCount);
        return StatusHandler(cancellationToken);
    }

    public void Reset()
    {
        Volatile.Write(ref _extractionCallCount, 0);
        Volatile.Write(ref _statusCallCount, 0);
        ExtractionHandler = (_, _) => Task.FromResult<ListingExtractionOutcome>(
            new ListingExtractionFailure(ListingExtractionFailureCode.NotConfigured));
        StatusHandler = _ => Task.FromResult(
            new ListingExtractionConfigurationStatus(false, null, null, null));
    }
}
