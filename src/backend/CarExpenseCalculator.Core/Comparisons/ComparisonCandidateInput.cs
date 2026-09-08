using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Core.Comparisons;

public enum ComparisonReviewSection { Energy, Tax, Insurance, Service, Repairs, CustomCosts, Ownership, Payments, StartupBudget, MonthlyBudget }

// Application-owned projection of unresolved source review, not client-authenticated flags.
// The future storage/API adapter derives this impact from persisted/typed review kinds.
public sealed class ComparisonReviewItem(string key, string reason, IEnumerable<ComparisonReviewSection> affectedSections)
{
    public string Key { get; } = key;
    public string Reason { get; } = reason;
    public IReadOnlyList<ComparisonReviewSection> AffectedSections { get; } = Array.AsReadOnly(affectedSections.ToArray());
}

public sealed record ComparisonSourceRevisions(long? Vehicle = null, long? Listing = null,
    long? ReviewedListing = null, long? HouseholdProfile = null, long? RuleProfile = null);

public sealed class ComparisonCandidateInput(Guid vehicleId, RegistrationNumber registrationNumber,
    VehicleComparisonFacts? facts = null, VehicleCostInput? costInput = null,
    CostAssumptionConfirmation? costConfirmation = null, ComparisonSourceRevisions? sourceRevisions = null,
    IEnumerable<ComparisonReviewItem>? reviewItems = null, IEnumerable<VehicleFact<string>>? conditionNotes = null)
{
    public Guid VehicleId { get; } = vehicleId;
    public RegistrationNumber RegistrationNumber { get; } = registrationNumber;
    public VehicleComparisonFacts Facts { get; } = facts ?? new();
    public VehicleCostInput? CostInput { get; } = costInput;
    public CostAssumptionConfirmation? CostConfirmation { get; } = costConfirmation;
    public ComparisonSourceRevisions? SourceRevisions { get; } = sourceRevisions;
    public IReadOnlyList<ComparisonReviewItem> ReviewItems { get; } = Array.AsReadOnly(reviewItems?.ToArray() ?? []);
    public IReadOnlyList<VehicleFact<string>>? ConditionNotes { get; } = conditionNotes is null ? null : Array.AsReadOnly(conditionNotes.ToArray());
}

// Explicit adoption captures the actual immutable car assumptions, never just a boolean
// or revision claim. Reconstructed identical values remain applicable across reads.
public sealed class CostAssumptionConfirmation
{
    private CostAssumptionConfirmation(VehicleCostInput input, DateTimeOffset confirmedAt)
    {
        Input = input;
        ConfirmedAt = confirmedAt;
    }

    public VehicleCostInput Input { get; }
    public DateTimeOffset ConfirmedAt { get; }

    public static CostAssumptionConfirmation Confirm(VehicleCostInput input, DateTimeOffset confirmedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(input, confirmedAt);
    }

    internal bool Matches(VehicleCostInput? input) => input is not null && Equal(Input, input);

    private static bool Sequence<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);
    private static bool Category(HouseholdCostCategoryInput? a, HouseholdCostCategoryInput? b) =>
        a is null ? b is null : b is not null && a.IsIncluded == b.IsIncluded && Sequence(a.Items, b.Items);
    private static bool Lease(HouseholdLeaseInput? a, HouseholdLeaseInput? b) => a is null ? b is null : b is not null &&
        a.TermMonths == b.TermMonths && a.UpfrontNonRefundableSek == b.UpfrontNonRefundableSek &&
        a.RefundableDepositSek == b.RefundableDepositSek && a.DepositRefundSek == b.DepositRefundSek &&
        a.IncludedDistanceKilometres == b.IncludedDistanceKilometres && a.ExcessDistancePricePerKilometreSek == b.ExcessDistancePricePerKilometreSek &&
        a.PriceBasis == b.PriceBasis && a.EnergyIncluded == b.EnergyIncluded &&
        Sequence(a.MonthlyPayments, b.MonthlyPayments) && Sequence(a.EndFees, b.EndFees) && Sequence(a.OtherPayments, b.OtherPayments);
    private static bool Equal(VehicleCostInput a, VehicleCostInput b) =>
        RegistrationNumber.TryParse(a.CandidateKey, out var registrationA) &&
        RegistrationNumber.TryParse(b.CandidateKey, out var registrationB) && registrationA == registrationB &&
        a.AcquisitionType == b.AcquisitionType && a.PriceSek == b.PriceSek && a.Residual == b.Residual &&
        Sequence(a.EnergySources, b.EnergySources) && Category(a.Tax, b.Tax) && Category(a.Insurance, b.Insurance) &&
        Category(a.Service, b.Service) && Category(a.Repairs, b.Repairs) && Category(a.CustomCosts, b.CustomCosts) &&
        a.AdditionalRepairAllowancePerMonthSek == b.AdditionalRepairAllowancePerMonthSek && Lease(a.Lease, b.Lease);
}
