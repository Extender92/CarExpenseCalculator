using CarExpenseCalculator.Api.Contracts.ManualCalculations;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Contracts.Households;

public sealed record HouseholdProfileResponse(HouseholdProfileInput? Input, long Revision);
public sealed record LegacyReviewResponse(LegacyReviewInput Input, string Reason, IReadOnlyList<string> AffectedSections);
public sealed record RecoveredLegacyResponse(ManualCalculationRequest Input, int CalculationVersion,
    int ResultSchemaVersion, VehicleCostInput SuggestedInput, IReadOnlyList<LegacyReviewResponse> Items);
public sealed record VehicleCostInputResponse(Guid VehicleId, string RegistrationNumber, string? VehicleLabel,
    long Revision, VehicleInputState State, VehicleCostInput? Input, RecoveredLegacyResponse? Legacy,
    IReadOnlyList<LegacyReviewResponse> UnresolvedLegacyItems, long? SourceListingVersion,
    long? CurrentListingVersion, bool NeedsListingReview, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record VehicleCostInputSummary(Guid VehicleId, string RegistrationNumber, string? VehicleLabel,
    long Revision, VehicleInputState State, IReadOnlyList<LegacyReviewResponse> ReviewItems,
    long? SourceListingVersion, long? CurrentListingVersion, bool NeedsListingReview, DateTimeOffset UpdatedAtUtc);
public sealed record VehicleDraftResponse(long Revision, VehicleDraftInput? Input);
public sealed record HouseholdTransitionResponse(long Revision, HouseholdProfileResponse Profile,
    IReadOnlyList<VehicleCostInputResponse> Vehicles);

public sealed class HouseholdProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
    public Guid? VehicleId { get; init; }
    public long? ExpectedRevision { get; init; }
    public long? ActualRevision { get; init; }
    public string? RecoveryRoute { get; init; }
}

public sealed class HouseholdValidationProblemDetails : ValidationProblemDetails
{
    public required string Code { get; init; }
    public required IReadOnlyList<HouseholdInputError> FieldErrors { get; init; }
}
