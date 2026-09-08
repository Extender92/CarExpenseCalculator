using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using H = CarExpenseCalculator.Api.Contracts.Households;

namespace CarExpenseCalculator.Api.Contracts.Comparisons;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManualFactValue<T> where T : notnull
{
    public required T Value { get; init; }
    public DateTimeOffset? ObservedAt { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactSelection<T> where T : notnull
{
    public required FactSelectionKind Kind { get; init; }
    public int? ObservationIndex { get; init; }
    public ManualFactValue<T>? Manual { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactEdit<T> where T : notnull
{
    public required FactEditKind Kind { get; init; }
    public ManualFactValue<T>? Manual { get; init; }
    public IReadOnlyList<FactSelection<T>>? Observations { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleFactEdits
{
    public FactEdit<decimal>? PurchasePriceSek { get; init; }
    public FactEdit<decimal>? OdometerKilometres { get; init; }
    public FactEdit<int>? OwnerCount { get; init; }
    public FactEdit<bool>? TowBar { get; init; }
    public FactEdit<Transmission>? Transmission { get; init; }
    public FactEdit<int>? Seats { get; init; }
    public FactEdit<int>? ModelYear { get; init; }
    public FactEdit<IReadOnlyList<FuelType>>? FuelTypes { get; init; }
    public FactEdit<BodyType>? BodyType { get; init; }
    public FactEdit<Drivetrain>? Drivetrain { get; init; }
    public FactEdit<string>? Locality { get; init; }
    public FactEdit<string>? County { get; init; }
    public FactEdit<int>? TowingCapacityKilograms { get; init; }
    public FactEdit<DateOnly>? InspectionValidThrough { get; init; }
    public FactEdit<ServiceDocumentationStatus>? ServiceDocumentation { get; init; }
    public FactEdit<DateOnly>? LastServiceDate { get; init; }
    public FactEdit<decimal>? LastServiceOdometerKilometres { get; init; }
    public FactEdit<string>? ServiceNotes { get; init; }
    public IReadOnlyList<FactEdit<string>>? ConditionNotes { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleFactsWrite
{
    public VehicleFactEdits Edits { get; init; } = new();
    public long? ExpectedListingVersion { get; init; }
    public bool ReviewCurrentListing { get; init; }
    public CostConfirmationAction CostConfirmation { get; init; } = CostConfirmationAction.Preserve;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveVehicleFactsRequest
{
    public required long ExpectedRevision { get; init; }
    public required VehicleFactsWrite Input { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonChoice
{
    public bool? Boolean { get; init; }
    public Transmission? Transmission { get; init; }
    public FuelType? FuelType { get; init; }
    public BodyType? BodyType { get; init; }
    public Drivetrain? Drivetrain { get; init; }
    public ServiceDocumentationStatus? ServiceDocumentation { get; init; }
    public string? Text { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HardRuleInput
{
    public required string CriterionKey { get; init; }
    public required HardRuleOperator Operator { get; init; }
    public required EvidenceRequirement MinimumEvidence { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public IReadOnlyList<ComparisonChoice>? AllowedValues { get; init; }
    public bool Enabled { get; init; } = true;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreferenceInput
{
    public required string CriterionKey { get; init; }
    public required int Weight { get; init; }
    public required EvidenceRequirement MinimumEvidence { get; init; }
    public decimal? ZeroPoint { get; init; }
    public decimal? FullPoint { get; init; }
    public IReadOnlyList<ComparisonChoice>? PreferredValues { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonSignalInput
{
    public required ComparisonSignalKey Key { get; init; }
    public int? ShortInspectionDays { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleProfileInput
{
    public IReadOnlyList<HardRuleInput> HardRules { get; init; } = [];
    public IReadOnlyList<PreferenceInput> Preferences { get; init; } = [];
    public IReadOnlyList<ComparisonSignalInput> Signals { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveRuleProfileRequest
{
    public required long ExpectedRevision { get; init; }
    public required RuleProfileInput Input { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonStoredBase
{
    public required long HouseholdProfileRevision { get; init; }
    public required long RuleProfileRevision { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonVehicleBase
{
    public required long VehicleRevision { get; init; }
    // Required even when null: absence is not permission to skip the listing version check.
    [Required(AllowEmptyStrings = true)]
    public required ListingVersionInput Listing { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ListingVersionInput
{
    public required long? Version { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonCandidateRequest
{
    public required Guid VehicleId { get; init; }
    public required string RegistrationNumber { get; init; }
    public ComparisonVehicleBase? StoredBase { get; init; }
    public VehicleFactsWrite Facts { get; init; } = new();
    public H.VehicleCostInput? CostInput { get; init; }
    public IReadOnlyList<H.LegacyItemDecision>? LegacyDecisions { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComparisonPreviewRequest
{
    public required ComparisonPreviewMode Mode { get; init; }
    public required string RequestId { get; init; }
    public required H.HouseholdProfileInput Profile { get; init; }
    public required RuleProfileInput Rules { get; init; }
    public required DateOnly AsOfDate { get; init; }
    public ComparisonStoredBase? StoredBase { get; init; }
    public required IReadOnlyList<ComparisonCandidateRequest> Candidates { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompleteComparisonStoredBase
{
    public required string BaselineToken { get; init; }
    public required long HouseholdProfileRevision { get; init; }
    public required long RuleProfileRevision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompleteComparisonRequest
{
    public required ComparisonPreviewMode Mode { get; init; }
    public required string RequestId { get; init; }
    public required H.HouseholdProfileInput Profile { get; init; }
    public required RuleProfileInput Rules { get; init; }
    public required DateOnly AsOfDate { get; init; }
    public CompleteComparisonStoredBase? StoredBase { get; init; }
    public IReadOnlyList<ComparisonCandidateRequest>? Overrides { get; init; }
    public IReadOnlyList<ComparisonCandidateRequest>? Candidates { get; init; }
}
