using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using Microsoft.AspNetCore.Mvc;
using H = CarExpenseCalculator.Api.Contracts.Households;

namespace CarExpenseCalculator.Api.Contracts.Comparisons;

public sealed record ComparisonEvidence(FieldOrigin Origin, ExtractionMethod ExtractionMethod, VerificationStatus Verification,
    string? SourceUrl, DateTimeOffset? ObservedAt, DateTimeOffset? ConfirmedAt);
public sealed record FactObservation<T>(T Value, ComparisonEvidence Evidence, long? SourceListingVersion);
public sealed record VehicleFact<T>(VehicleFactState State, IReadOnlyList<FactObservation<T>> Observations);
public sealed record VehicleComparisonFacts
{
    public required VehicleFact<decimal> PurchasePriceSek { get; init; }
    public required VehicleFact<decimal> OdometerKilometres { get; init; }
    public required VehicleFact<int> OwnerCount { get; init; }
    public required VehicleFact<bool> TowBar { get; init; }
    public required VehicleFact<Transmission> Transmission { get; init; }
    public required VehicleFact<int> Seats { get; init; }
    public required VehicleFact<int> ModelYear { get; init; }
    public required VehicleFact<IReadOnlyList<FuelType>> FuelTypes { get; init; }
    public required VehicleFact<BodyType> BodyType { get; init; }
    public required VehicleFact<Drivetrain> Drivetrain { get; init; }
    public required VehicleFact<string> Locality { get; init; }
    public required VehicleFact<string> County { get; init; }
    public required VehicleFact<int> TowingCapacityKilograms { get; init; }
    public required VehicleFact<DateOnly> InspectionValidThrough { get; init; }
    public required VehicleFact<ServiceDocumentationStatus> ServiceDocumentation { get; init; }
    public required VehicleFact<DateOnly> LastServiceDate { get; init; }
    public required VehicleFact<decimal> LastServiceOdometerKilometres { get; init; }
    public required VehicleFact<string> ServiceNotes { get; init; }
}
public sealed record ComparisonFactSet(VehicleComparisonFacts Facts, IReadOnlyList<VehicleFact<string>>? ConditionNotes);
public sealed record RuleProfileResponse(RuleProfileInput? Input, long Revision, int StorageVersion = 1);
public sealed record VehicleFactsResponse(Guid VehicleId, string RegistrationNumber, long Revision,
    ComparisonFactSet? Input, ComparisonFactSet? ListingProposal, long? CurrentListingVersion,
    long? FactsReviewedListingVersion, long? CostReviewedListingVersion, bool NeedsListingReview,
    DateTimeOffset? CostConfirmedAt, int StorageVersion = 1);
public sealed record ComparisonFieldError(string Path, string Code, string Message);
public sealed record ScoreRange(decimal Lower, decimal Upper);
public sealed record ComparisonObservedValue(decimal? Number, ComparisonChoice? Choice, DateOnly? Date, IReadOnlyList<FuelType>? Fuels);
public sealed record CriterionAssessment(string CriterionKey, ComparisonObservedValue? Actual,
    IReadOnlyList<ComparisonEvidence> Evidence, EvidenceRequirement MinimumEvidence, bool HasAdequateEvidence,
    IReadOnlyList<string> Reasons, IReadOnlyList<ComparisonFieldError> Errors);
public sealed record HardRuleEvaluation(HardRuleInput Rule, HardRuleState State, CriterionAssessment Assessment,
    bool? ObservedConditionSatisfied, string ReasonCode, string Explanation);
public sealed record PreferenceContribution(PreferenceInput Preference, CriterionAssessment Assessment,
    ScoreRange Range, ScoreRange WeightedContribution, string Explanation);
public sealed record ComparisonSignal(ComparisonSignalKey Key, ComparisonSignalKind Kind, string ReasonCode,
    string Explanation, ComparisonEvidence? Evidence);
public sealed record ComparisonSourceRevisions(long? HouseholdProfile, long? RuleProfile, long? Vehicle,
    long? Listing, long? FactsReviewedListing, long? CostReviewedListing);
public sealed record ComparisonDirtyState(bool Profile, bool Rules, bool Facts, bool Costs,
    bool ReviewDecisions, bool CostConfirmation, bool ListingReview);
public sealed record ComparisonCandidateResult(Guid VehicleId, string RegistrationNumber,
    ComparisonFactSet EffectiveFacts, H.VehicleCostInput? EffectiveCostInput, DateTimeOffset? CostConfirmedAt,
    IReadOnlyList<H.LegacyReviewResponse> UnresolvedLegacyItems, ComparisonSourceRevisions SourceRevisions,
    bool NeedsListingReview, H.VehicleInputState? StoredInputState, ComparisonDirtyState Unsaved,
    H.VehicleCostResult Cost, IReadOnlyList<ComparisonFieldError> Errors, IReadOnlyList<HardRuleEvaluation> HardRules,
    BuyingEligibility Eligibility, IReadOnlyList<PreferenceContribution> Contributions, ScoreRange? Score,
    decimal? CoveragePercent, string? ScoreUnavailableReason, IReadOnlyList<ComparisonSignal> Signals,
    bool IsCheapestEligibleComplete, bool IsDefinitePreferenceWinner);
public sealed record ComparisonPreviewResponse(string RequestId, ComparisonPreviewMode Mode, bool StorageChecked,
    H.HouseholdProfileInput Profile, RuleProfileInput Rules, DateOnly AsOfDate, int RuleVersion, int ResultSchemaVersion,
    int CalculationVersion, int HouseholdResultSchemaVersion, IReadOnlyList<ComparisonFieldError> ProfileErrors,
    IReadOnlyList<ComparisonCandidateResult> Candidates, IReadOnlyList<Guid> CostOrder,
    IReadOnlyList<Guid> ScoreOrder, string PreferenceRecommendationReason);
public sealed class ComparisonProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
    public Guid? VehicleId { get; init; }
    public long? ExpectedRevision { get; init; }
    public long? ActualRevision { get; init; }
}
public sealed class ComparisonValidationProblemDetails : ValidationProblemDetails
{
    public required string Code { get; init; }
    public required IReadOnlyList<ComparisonFieldError> FieldErrors { get; init; }
}
