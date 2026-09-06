namespace CarExpenseCalculator.Core.Households;

public enum LeasePriceBasis { Quoted, Estimated, Unresolved }

public sealed record HouseholdLeasePayment(int MonthOffset, decimal? AmountSek);

public sealed record HouseholdLeaseCharge(
    string Key, string Label, SensitivityValue? AmountSek, int? MonthOffset = null,
    string? EvidenceNote = null, string? SourceUrl = null);

public sealed record HouseholdLeaseInput
{
    public HouseholdLeaseInput(IEnumerable<HouseholdLeasePayment>? monthlyPayments = null,
        IEnumerable<HouseholdLeaseCharge>? endFees = null, IEnumerable<HouseholdLeaseCharge>? otherPayments = null)
    {
        MonthlyPayments = monthlyPayments is null ? null : Array.AsReadOnly(monthlyPayments.ToArray());
        EndFees = endFees is null ? null : Array.AsReadOnly(endFees.ToArray());
        OtherPayments = otherPayments is null ? null : Array.AsReadOnly(otherPayments.ToArray());
    }

    public int? TermMonths { get; init; }
    public decimal? UpfrontNonRefundableSek { get; init; }
    public decimal? RefundableDepositSek { get; init; }
    public SensitivityValue? DepositRefundSek { get; init; }
    public decimal? IncludedDistanceKilometres { get; init; }
    public SensitivityValue? ExcessDistancePricePerKilometreSek { get; init; }
    public LeasePriceBasis? PriceBasis { get; init; }
    public bool EnergyIncluded { get; init; }
    public IReadOnlyList<HouseholdLeasePayment>? MonthlyPayments { get; }
    // End fees always fall in the final contract month; only OtherPayments use an offset.
    public IReadOnlyList<HouseholdLeaseCharge>? EndFees { get; }
    public IReadOnlyList<HouseholdLeaseCharge>? OtherPayments { get; }
}
