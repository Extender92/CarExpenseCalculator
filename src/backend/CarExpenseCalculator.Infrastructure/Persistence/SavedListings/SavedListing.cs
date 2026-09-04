using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

public sealed class SavedListingInput
{
    public SavedListingInput(
        string submittedUrl,
        DateTimeOffset analyzedAtUtc,
        string? requestedModel,
        int? promptVersion,
        int? extractionSchemaVersion,
        IEnumerable<ListingUrl> sources,
        ListingDraft listing)
    {
        ArgumentNullException.ThrowIfNull(submittedUrl);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(listing);

        SubmittedUrl = submittedUrl;
        AnalyzedAtUtc = analyzedAtUtc;
        RequestedModel = requestedModel;
        PromptVersion = promptVersion;
        ExtractionSchemaVersion = extractionSchemaVersion;
        Sources = Array.AsReadOnly(sources.ToArray());
        Listing = listing;
    }

    public string SubmittedUrl { get; }

    public DateTimeOffset AnalyzedAtUtc { get; }

    public string? RequestedModel { get; }

    public int? PromptVersion { get; }

    public int? ExtractionSchemaVersion { get; }

    public IReadOnlyList<ListingUrl> Sources { get; }

    public ListingDraft Listing { get; }
}

public sealed record SavedListing(
    Guid VehicleId,
    RegistrationNumber RegistrationNumber,
    long Revision,
    long ListingVersion,
    int ListingSchemaVersion,
    string SubmittedUrl,
    ListingUrl NormalizedUrl,
    DateTimeOffset AnalyzedAtUtc,
    string? RequestedModel,
    int? PromptVersion,
    int? ExtractionSchemaVersion,
    ListingProcessingResult ProcessingResult,
    bool HasSavedCostScenario,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class SavedListingRegistrationConflictException : Exception
{
    public SavedListingRegistrationConflictException(
        RegistrationNumber registrationNumber,
        Guid? existingVehicleId,
        long? actualRevision,
        Exception? innerException = null)
        : base(
            $"A saved vehicle with registration number '{registrationNumber.Value}' already exists.",
            innerException)
    {
        RegistrationNumber = registrationNumber;
        ExistingVehicleId = existingVehicleId;
        ActualRevision = actualRevision;
    }

    public RegistrationNumber RegistrationNumber { get; }

    public Guid? ExistingVehicleId { get; }

    public long? ActualRevision { get; }
}

public sealed class SavedListingNotFoundException : Exception
{
    public SavedListingNotFoundException(Guid vehicleId)
        : base($"Saved vehicle '{vehicleId}' was not found.")
    {
        VehicleId = vehicleId;
    }

    public Guid VehicleId { get; }
}

public sealed class SavedListingConcurrencyException : Exception
{
    public SavedListingConcurrencyException(
        Guid vehicleId,
        long expectedRevision,
        long? actualRevision,
        Exception? innerException = null)
        : base(
            $"Saved vehicle '{vehicleId}' no longer has expected revision {expectedRevision}.",
            innerException)
    {
        VehicleId = vehicleId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public Guid VehicleId { get; }

    public long ExpectedRevision { get; }

    public long? ActualRevision { get; }
}

public sealed class UnsupportedSavedListingVersionException : Exception
{
    public UnsupportedSavedListingVersionException(
        Guid vehicleId,
        int listingSchemaVersion,
        int? promptVersion,
        int? extractionSchemaVersion)
        : base(
            $"Saved vehicle '{vehicleId}' uses unsupported listing schema version "
            + $"{listingSchemaVersion}, prompt version {promptVersion?.ToString() ?? "null"}, "
            + $"or extraction schema version {extractionSchemaVersion?.ToString() ?? "null"}.")
    {
        VehicleId = vehicleId;
        ListingSchemaVersion = listingSchemaVersion;
        PromptVersion = promptVersion;
        ExtractionSchemaVersion = extractionSchemaVersion;
    }

    public Guid VehicleId { get; }

    public int ListingSchemaVersion { get; }

    public int? PromptVersion { get; }

    public int? ExtractionSchemaVersion { get; }
}
