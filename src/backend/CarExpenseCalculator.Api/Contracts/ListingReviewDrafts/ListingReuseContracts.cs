using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.Comparisons;
using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Contracts.SavedListings;

namespace CarExpenseCalculator.Api.Contracts.ListingReviewDrafts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ListingReusePreviewRequest
{
    public Guid? VehicleId { get; init; }
    public long? ExpectedVehicleRevision { get; init; }
    public long? ExpectedListingVersion { get; init; }
    public Guid? ReviewDraftId { get; init; }
    public long? ExpectedReviewDraftRevision { get; init; }
    public ReviewedListingInput? UnsavedListing { get; init; }
    public required VehicleCostInput Target { get; init; }
    public VehicleFactEdits? FactEdits { get; init; }
}
public sealed record ListingReuseSuggestion<T>(string Key, T Value, ListingValueSource Source,
    bool RequiresReplacement, bool AlreadyApplied) where T : notnull;
public sealed record ListingFactReuseTarget(string Field, bool RequiresReplacement, bool AlreadyApplied);
public sealed record ListingReusePreviewResponse(
    ListingReuseSuggestion<decimal>? PurchasePrice,
    IReadOnlyList<ListingReuseSuggestion<HouseholdEnergySource>> EnergySources,
    ListingReuseSuggestion<HouseholdCostItem>? AnnualTax,
    ComparisonFactSet Facts,
    IReadOnlyList<ListingFactReuseTarget> FactTargets,
    IReadOnlyList<string> Warnings);
