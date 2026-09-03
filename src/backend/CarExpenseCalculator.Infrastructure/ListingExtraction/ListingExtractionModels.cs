using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Infrastructure.ListingExtraction;

public enum ListingExtractionFailureCode
{
    NotConfigured,
    RateLimited,
    TimedOut,
    ProviderUnavailable,
    InvalidProviderResponse,
}

public abstract record ListingExtractionOutcome;

public sealed record ListingExtractionSuccess(
    ListingUrl SubmittedUrl,
    string RequestedModel,
    int PromptVersion,
    int SchemaVersion,
    DateTimeOffset AnalyzedAtUtc,
    ListingProcessingResult ProcessingResult)
    : ListingExtractionOutcome;

public sealed record ListingExtractionFailure(ListingExtractionFailureCode Code)
    : ListingExtractionOutcome;

public sealed record ListingExtractionConfigurationStatus(
    bool Configured,
    string? RequestedModel,
    int? PromptVersion,
    int? SchemaVersion);

public interface IListingExtractionService
{
    Task<ListingExtractionOutcome> ExtractAsync(
        ListingUrl listingUrl,
        CancellationToken cancellationToken = default);

    Task<ListingExtractionConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);
}
