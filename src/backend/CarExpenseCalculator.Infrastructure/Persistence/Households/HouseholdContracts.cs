using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

public sealed record SavedHouseholdProfile(HouseholdProfileInput? Input, long Revision);
public enum VehicleInputState { ListingOnly, LegacyPending, Current }
public enum LegacyItemKind { Tax, Insurance, Maintenance, Energy, Recurring, OneTime }
public enum LegacyItemDisposition { KeepForReview, Map, Discard }

// Only unresolved car-specific facts survive confirmation, never old household overrides.
public sealed record LegacyReviewItem(string Key, LegacyItemKind Kind, string Label,
    decimal? AmountSek = null, RecurringCostCadence? Cadence = null,
    EnergyUnit? EnergyUnit = null, decimal? ConsumptionPer100Kilometres = null)
{
    public string Reason => Kind switch
    {
        LegacyItemKind.Energy => "energyIdentityAndConsumptionBasisRequireReview",
        LegacyItemKind.Maintenance => "combinedMaintenanceRequiresClassification",
        LegacyItemKind.OneTime => "oneTimePaymentMonthRequired",
        _ => "explicitCostMappingRequired",
    };
    public IReadOnlyList<string> AffectedSections => Kind switch
    {
        LegacyItemKind.Energy => ["energy", "ownership", "payments", "monthlyBudget"],
        LegacyItemKind.Tax => ["tax", "ownership", "payments", "monthlyBudget"],
        LegacyItemKind.Insurance => ["insurance", "ownership", "payments", "monthlyBudget"],
        LegacyItemKind.Maintenance => ["service", "repairs", "ownership", "payments", "startupBudget", "monthlyBudget"],
        LegacyItemKind.OneTime => ["customCosts", "ownership", "payments", "startupBudget", "monthlyBudget"],
        _ => ["customCosts", "ownership", "payments", "monthlyBudget"],
    };
}

public sealed record LegacyItemDecision(string Key, LegacyItemDisposition Disposition, string? TargetKey = null);
public sealed record VehicleCostWrite(VehicleCostInput Input, string? VehicleLabel = null,
    SavedScenarioListingLinkMode ListingLinkMode = SavedScenarioListingLinkMode.Preserve,
    IReadOnlyList<LegacyItemDecision>? LegacyDecisions = null);
public sealed record RecoveredLegacyInput(CostScenario Input, int CalculationVersion, int ResultSchemaVersion,
    VehicleCostInput SuggestedInput, IReadOnlyList<LegacyReviewItem> Items);
public sealed record SavedVehicleCostInput(Guid VehicleId, RegistrationNumber RegistrationNumber, string? VehicleLabel,
    long Revision, VehicleInputState State, VehicleCostInput? Input, RecoveredLegacyInput? Legacy,
    IReadOnlyList<LegacyReviewItem> UnresolvedLegacyItems, long? SourceListingVersion, long? CurrentListingVersion,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    public bool NeedsListingReview => SourceListingVersion is { } source && source != CurrentListingVersion;
}

public sealed record VehicleDraftInput(RegistrationNumber RegistrationNumber, VehicleCostWrite? Cost = null,
    SavedListingInput? Listing = null, Guid? BaseVehicleId = null, long? BaseVehicleRevision = null);
public sealed record SavedVehicleDraft(long Revision, VehicleDraftInput? Input);
public sealed record HouseholdTransition(long Revision, SavedHouseholdProfile Profile,
    IReadOnlyList<SavedVehicleCostInput> Vehicles);
public sealed record VehicleTransitionWrite(Guid VehicleId, long ExpectedRevision, VehicleCostWrite Cost);

public interface IHouseholdProfileStore
{
    Task<SavedHouseholdProfile> GetAsync(CancellationToken cancellationToken = default);
    Task<SavedHouseholdProfile> SaveAsync(HouseholdProfileInput input, long expectedRevision,
        CancellationToken cancellationToken = default);
}

public interface IVehicleCostInputStore
{
    Task<SavedVehicleCostInput> CreateAsync(RegistrationNumber registrationNumber, VehicleCostWrite input,
        CancellationToken cancellationToken = default);
    Task<SavedVehicleCostInput?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<SavedVehicleCostInput?> GetByRegistrationNumberAsync(RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedVehicleCostInput>> ListAsync(CancellationToken cancellationToken = default);
    Task<SavedVehicleCostInput> ReplaceAsync(Guid vehicleId, long expectedRevision, VehicleCostWrite input,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid vehicleId, long expectedRevision, CancellationToken cancellationToken = default);
}

public interface ISharedVehicleDraftStore
{
    Task<SavedVehicleDraft> GetAsync(CancellationToken cancellationToken = default);
    Task<SavedVehicleDraft> SaveAsync(VehicleDraftInput input, long expectedRevision, bool replaceExisting = false,
        CancellationToken cancellationToken = default);
    Task<SavedVehicleDraft> DeleteAsync(long expectedRevision, CancellationToken cancellationToken = default);
    Task<SavedVehicleCostInput> AdoptAsync(long expectedRevision, CancellationToken cancellationToken = default);
}

public interface IHouseholdTransitionStore
{
    Task<HouseholdTransition> GetAsync(CancellationToken cancellationToken = default);
    Task<HouseholdTransition> ConfirmAsync(HouseholdProfileInput profile, long expectedProfileRevision,
        long expectedTransitionRevision, IReadOnlyList<VehicleTransitionWrite> vehicles,
        CancellationToken cancellationToken = default);
}

public sealed class HouseholdStoreException(string code, string message, Guid? vehicleId = null,
    long? expectedRevision = null, long? actualRevision = null) : Exception(message)
{
    public string Code { get; } = code;
    public Guid? VehicleId { get; } = vehicleId;
    public long? ExpectedRevision { get; } = expectedRevision;
    public long? ActualRevision { get; } = actualRevision;
}
