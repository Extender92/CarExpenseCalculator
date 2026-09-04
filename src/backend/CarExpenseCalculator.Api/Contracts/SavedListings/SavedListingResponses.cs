using CarExpenseCalculator.Api.Contracts.ListingAnalyses;

namespace CarExpenseCalculator.Api.Contracts.SavedListings;

public sealed record SavedListingResponse(
    Guid VehicleId,
    string RegistrationNumber,
    long Revision,
    long ListingVersion,
    int ListingSchemaVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset AnalyzedAtUtc,
    string SubmittedUrl,
    string NormalizedUrl,
    ListingAnalysisStatus Status,
    string? RequestedModel,
    int? PromptVersion,
    int? SchemaVersion,
    IReadOnlyList<ListingAnalysisSourceResponse> Sources,
    ListingDraftResponse Listing,
    IReadOnlyList<ListingFieldCode> MissingFields,
    bool HasSavedCostScenario);

public sealed record SavedListingSummaryResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string? VehicleLabel,
    long Revision,
    long ListingVersion,
    int ListingSchemaVersion,
    string? Make,
    string? Model,
    int? ModelYear,
    decimal? PriceSek,
    decimal? OdometerKilometres,
    ListingAnalysisStatus Status,
    int MissingFieldCount,
    bool HasSavedCostScenario,
    DateTimeOffset UpdatedAtUtc);
