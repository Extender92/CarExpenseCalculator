using CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Core.Listings;

public sealed record FieldProvenance(
    FieldOrigin Origin,
    ExtractionMethod ExtractionMethod,
    VerificationStatus Verification,
    ListingUrl SourceUrl);

public sealed record SourcedValue<T>(
    T Value,
    FieldProvenance Provenance)
    where T : notnull;

public sealed class SourcedCollection<T>
    where T : notnull
{
    public SourcedCollection(IEnumerable<T> values, FieldProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(provenance);

        Values = Array.AsReadOnly(values.ToArray());
        Provenance = provenance;
    }

    public IReadOnlyList<T> Values { get; }

    public FieldProvenance Provenance { get; }
}

public sealed record EnergyConsumption(
    string Label,
    EnergyUnit Unit,
    decimal ConsumptionPer100Kilometres);

public sealed record ListingAnalysisSource(
    ListingUrl Url,
    bool MatchesSubmittedUrl);

public sealed record ListingProcessingResult(
    ListingAnalysisStatus Status,
    IReadOnlyList<ListingAnalysisSource> Sources,
    ListingDraft Listing,
    IReadOnlyList<ListingFieldCode> MissingFields);
