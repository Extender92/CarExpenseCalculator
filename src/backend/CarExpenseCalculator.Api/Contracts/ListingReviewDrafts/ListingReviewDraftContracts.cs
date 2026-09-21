using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.SavedListings;

namespace CarExpenseCalculator.Api.Contracts.ListingReviewDrafts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateListingReviewDraftRequest
{
    public required ReviewedListingInput Input { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceListingReviewDraftRequest
{
    public required long ExpectedRevision { get; init; }
    public required ReviewedListingInput Input { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdoptListingReviewDraftRequest
{
    public required long ExpectedRevision { get; init; }
    public Guid? ExistingVehicleId { get; init; }
    public long? ExpectedVehicleRevision { get; init; }
}
public sealed record ListingReviewDraftResponse(Guid Id, long Revision, int SchemaVersion, string ListingReference,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, ReviewedListingInput Input);
