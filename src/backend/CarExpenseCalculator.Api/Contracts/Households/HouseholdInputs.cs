using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;
using CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using CarExpenseCalculator.Api.Contracts.SavedListings;

namespace CarExpenseCalculator.Api.Contracts.Households;

// These input DTOs deliberately have no numeric Range attributes: preview reports
// parsed numeric errors per dependent section, whereas stores reject invalid saves.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarMonth
{
    public required int Year { get; init; }
    public required int Month { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SensitivityValue
{
    public decimal? Single { get; init; }
    public decimal? Favorable { get; init; }
    public decimal? Baseline { get; init; }
    public decimal? Cautious { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdLoanTerms
{
    public SensitivityValue? AnnualNominalInterestRatePercent { get; init; }
    public int? TermMonths { get; init; }
    public decimal? SetupFeeSek { get; init; }
    public decimal? MonthlyFeeSek { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdEnergyPrice
{
    public required FuelType Fuel { get; init; }
    public required EnergyUnit Unit { get; init; }
    public SensitivityValue? PricePerUnitSek { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdProfileInput
{
    public CalendarMonth? StartMonth { get; init; }
    public int? PeriodMonths { get; init; }
    public decimal? AnnualDistanceKilometres { get; init; }
    public decimal? PurchaseCashSek { get; init; }
    public HouseholdLoanTerms? LoanTerms { get; init; }
    [Required(AllowEmptyStrings = true)]
    public IReadOnlyList<HouseholdEnergyPrice> EnergyPrices { get; init; } = [];
    public SensitivityValue? ElectricDrivingSharePercent { get; init; }
    public SensitivityValue? HomeChargingSharePercent { get; init; }
    public SensitivityValue? HomeChargingPricePerKilowattHourSek { get; init; }
    public SensitivityValue? PublicChargingPricePerKilowattHourSek { get; init; }
    public SensitivityValue? ChargingLossPercent { get; init; }
    public decimal? StartupBudgetSek { get; init; }
    public decimal? MonthlyBudgetSek { get; init; }
    public SensitivityMode ActiveSensitivityMode { get; init; } = SensitivityMode.Baseline;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdResidualInput
{
    public required ResidualMode Mode { get; init; }
    public SensitivityValue? Value { get; init; }
    public int? PeriodMonths { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdEnergySource
{
    [Required(AllowEmptyStrings = true)]
    public required string Key { get; init; }
    public FuelType? Fuel { get; init; }
    public EnergyUnit? Unit { get; init; }
    public SensitivityValue? ConsumptionPer100Kilometres { get; init; }
    public ConsumptionBasis? ConsumptionBasis { get; init; }
    public ElectricityBasis? ElectricityBasis { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdCostItem
{
    [Required(AllowEmptyStrings = true)]
    public required string Key { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required string Label { get; init; }
    public SensitivityValue? AmountSek { get; init; }
    public HouseholdCostCadence? Cadence { get; init; }
    public int? MonthOffset { get; init; }
    public int? DueMonthOfYear { get; init; }
    public string? EvidenceNote { get; init; }
    public string? SourceUrl { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdCostCategoryInput
{
    public required bool IsIncluded { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required IReadOnlyList<HouseholdCostItem> Items { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdLeasePayment
{
    public required int MonthOffset { get; init; }
    public decimal? AmountSek { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdLeaseCharge
{
    [Required(AllowEmptyStrings = true)]
    public required string Key { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required string Label { get; init; }
    public SensitivityValue? AmountSek { get; init; }
    public int? MonthOffset { get; init; }
    public string? EvidenceNote { get; init; }
    public string? SourceUrl { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdLeaseInput
{
    public int? TermMonths { get; init; }
    public decimal? UpfrontNonRefundableSek { get; init; }
    public decimal? RefundableDepositSek { get; init; }
    public SensitivityValue? DepositRefundSek { get; init; }
    public decimal? IncludedDistanceKilometres { get; init; }
    public SensitivityValue? ExcessDistancePricePerKilometreSek { get; init; }
    public LeasePriceBasis? PriceBasis { get; init; }
    public bool EnergyIncluded { get; init; }
    public IReadOnlyList<HouseholdLeasePayment>? MonthlyPayments { get; init; }
    public IReadOnlyList<HouseholdLeaseCharge>? EndFees { get; init; }
    public IReadOnlyList<HouseholdLeaseCharge>? OtherPayments { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleCostInput
{
    [Required(AllowEmptyStrings = true)]
    public required string CandidateKey { get; init; }
    public AcquisitionType AcquisitionType { get; init; }
    public decimal? PriceSek { get; init; }
    public HouseholdResidualInput? Residual { get; init; }
    public HouseholdLeaseInput? Lease { get; init; }
    public IReadOnlyList<HouseholdEnergySource>? EnergySources { get; init; }
    public HouseholdCostCategoryInput? Tax { get; init; }
    public HouseholdCostCategoryInput? Insurance { get; init; }
    public HouseholdCostCategoryInput? Service { get; init; }
    public HouseholdCostCategoryInput? Repairs { get; init; }
    public SensitivityValue? AdditionalRepairAllowancePerMonthSek { get; init; }
    public HouseholdCostCategoryInput? CustomCosts { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegacyReviewInput
{
    [Required(AllowEmptyStrings = true)]
    public required string Key { get; init; }
    public required LegacyItemKind Kind { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required string Label { get; init; }
    public decimal? AmountSek { get; init; }
    public LegacyRecurringCostCadence? Cadence { get; init; }
    public EnergyUnit? EnergyUnit { get; init; }
    public decimal? ConsumptionPer100Kilometres { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdPreviewCandidate
{
    public required VehicleCostInput Input { get; init; }
    public string? RegistrationNumber { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required IReadOnlyList<LegacyReviewInput> UnresolvedLegacyItems { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HouseholdPreviewRequest
{
    [Required(AllowEmptyStrings = true)]
    public required string RequestId { get; init; }
    public required HouseholdProfileInput Profile { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required IReadOnlyList<HouseholdPreviewCandidate> Vehicles { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveHouseholdProfileRequest
{
    public required long ExpectedRevision { get; init; }
    public required HouseholdProfileInput Input { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegacyItemDecision
{
    [Required(AllowEmptyStrings = true)]
    public required string Key { get; init; }
    public required LegacyItemDisposition Disposition { get; init; }
    public string? TargetKey { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleCostWrite
{
    public required VehicleCostInput Input { get; init; }
    public string? VehicleLabel { get; init; }
    public ListingLinkMode ListingLinkMode { get; init; }
    public IReadOnlyList<LegacyItemDecision>? LegacyDecisions { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVehicleCostInputRequest
{
    [Required(AllowEmptyStrings = true)]
    public required string RegistrationNumber { get; init; }
    public required VehicleCostWrite Cost { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceVehicleCostInputRequest
{
    public required long ExpectedRevision { get; init; }
    public required VehicleCostWrite Cost { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleDraftInput
{
    [Required(AllowEmptyStrings = true)]
    public required string RegistrationNumber { get; init; }
    public VehicleCostWrite? Cost { get; init; }
    public ReviewedListingInput? Listing { get; init; }
    public Guid? BaseVehicleId { get; init; }
    public long? BaseVehicleRevision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveVehicleDraftRequest
{
    public required long ExpectedRevision { get; init; }
    public required VehicleDraftInput Input { get; init; }
    public bool ReplaceExisting { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdoptVehicleDraftRequest
{
    public required long ExpectedRevision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleTransitionWrite
{
    public required Guid VehicleId { get; init; }
    public required long ExpectedRevision { get; init; }
    public required VehicleCostWrite Cost { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmHouseholdTransitionRequest
{
    public required HouseholdProfileInput Profile { get; init; }
    public required long ExpectedProfileRevision { get; init; }
    public required long ExpectedTransitionRevision { get; init; }
    [Required(AllowEmptyStrings = true)]
    public required IReadOnlyList<VehicleTransitionWrite> Vehicles { get; init; }
}
