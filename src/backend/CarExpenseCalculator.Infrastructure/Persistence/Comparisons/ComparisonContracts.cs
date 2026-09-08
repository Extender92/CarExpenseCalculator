using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.Households;

namespace CarExpenseCalculator.Infrastructure.Persistence.Comparisons;

public enum FactEditKind { Preserve, Unknown, NotApplicable, Manual, Listing, Conflict, Resolve }
public enum FactSelectionKind { Current, Listing, Manual }
public enum CostConfirmationAction { Preserve, Confirm, Clear }
public sealed record ManualFactValue<T>(T Value, DateTimeOffset? ObservedAt = null) where T : notnull;
public sealed record FactSelection<T>(FactSelectionKind Kind, int? ObservationIndex = null, ManualFactValue<T>? Manual = null) where T : notnull;
public sealed record FactEdit<T>(FactEditKind Kind, ManualFactValue<T>? Manual = null,
    IReadOnlyList<FactSelection<T>>? Observations = null) where T : notnull
{
    public IReadOnlyList<FactSelection<T>>? Observations { get; } =
        Observations is null ? null : Array.AsReadOnly(Observations.ToArray());
}

public sealed record VehicleFactEdits
{
    public FactEdit<decimal>? PurchasePriceSek { get; init; }
    public FactEdit<decimal>? OdometerKilometres { get; init; }
    public FactEdit<int>? OwnerCount { get; init; }
    public FactEdit<bool>? TowBar { get; init; }
    public FactEdit<Transmission>? Transmission { get; init; }
    public FactEdit<int>? Seats { get; init; }
    public FactEdit<int>? ModelYear { get; init; }
    public FactEdit<FuelTypeSet>? FuelTypes { get; init; }
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
    // Null preserves the list; an empty list explicitly clears it. References address the same index in the old list.
    private readonly IReadOnlyList<FactEdit<string>>? _conditionNotes;
    public IReadOnlyList<FactEdit<string>>? ConditionNotes
    {
        get => _conditionNotes;
        init => _conditionNotes = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
}

public sealed class ComparisonFactSet(VehicleComparisonFacts facts,
    IReadOnlyDictionary<string, IReadOnlyList<long?>>? observationListingVersions = null,
    IEnumerable<VehicleFact<string>>? conditionNotes = null)
{
    public VehicleComparisonFacts Facts { get; } = facts;
    public IReadOnlyDictionary<string, IReadOnlyList<long?>> ObservationListingVersions { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<long?>>(
            (observationListingVersions ?? new Dictionary<string, IReadOnlyList<long?>>())
            .ToDictionary(x => x.Key, x => (IReadOnlyList<long?>)Array.AsReadOnly(x.Value.ToArray()), StringComparer.Ordinal));
    public IReadOnlyList<VehicleFact<string>>? ConditionNotes { get; } =
        conditionNotes is null ? null : Array.AsReadOnly(conditionNotes.ToArray());
}

public sealed record VehicleFactsWrite(VehicleFactEdits Edits, long? ExpectedListingVersion = null,
    bool ReviewCurrentListing = false, CostConfirmationAction CostConfirmation = CostConfirmationAction.Preserve);
public sealed record SavedRuleProfile(RuleProfileInput? Input, long Revision);
public sealed record SavedVehicleFacts(Guid VehicleId, RegistrationNumber RegistrationNumber, long Revision,
    ComparisonFactSet? Input, ComparisonFactSet? ListingProposal, long? CurrentListingVersion,
    long? FactsReviewedListingVersion, long? CostReviewedListingVersion, DateTimeOffset? CostConfirmedAt)
{
    public bool NeedsListingReview => CurrentListingVersion is not null && FactsReviewedListingVersion != CurrentListingVersion;
}
public sealed record ComparisonStoredVehicle(SavedVehicleFacts Facts, SavedVehicleCostInput Cost);
public sealed record ComparisonSnapshot(SavedHouseholdProfile Profile, SavedRuleProfile Rules,
    IReadOnlyList<ComparisonStoredVehicle> Vehicles);
public sealed record ComparisonBaseline(SavedHouseholdProfile Profile, SavedRuleProfile Rules,
    int CandidateCount, string BaselineToken);
public sealed record CompleteComparisonSnapshot(ComparisonBaseline Baseline, ComparisonSnapshot Snapshot);

public interface IRuleProfileStore
{
    Task<SavedRuleProfile> GetAsync(CancellationToken cancellationToken = default);
    Task<SavedRuleProfile> SaveAsync(RuleProfileInput input, long expectedRevision, CancellationToken cancellationToken = default);
}
public interface IVehicleFactsStore
{
    Task<SavedVehicleFacts?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<SavedVehicleFacts> SaveAsync(Guid vehicleId, long expectedRevision, VehicleFactsWrite write, CancellationToken cancellationToken = default);
}
public interface IComparisonSnapshotStore
{
    Task<ComparisonSnapshot> ReadAsync(IReadOnlyList<Guid> vehicleIds, CancellationToken cancellationToken = default);
    Task<ComparisonBaseline> ReadBaselineAsync(CancellationToken cancellationToken = default);
    Task<CompleteComparisonSnapshot> ReadAllAsync(string expectedBaselineToken, CancellationToken cancellationToken = default);
}
public sealed class ComparisonStoreException(string code, string message, Guid? vehicleId = null,
    long? expectedRevision = null, long? actualRevision = null, string? actualBaselineToken = null) : Exception(message)
{
    public string Code { get; } = code;
    public Guid? VehicleId { get; } = vehicleId;
    public long? ExpectedRevision { get; } = expectedRevision;
    public long? ActualRevision { get; } = actualRevision;
    public string? ActualBaselineToken { get; } = actualBaselineToken;
}
