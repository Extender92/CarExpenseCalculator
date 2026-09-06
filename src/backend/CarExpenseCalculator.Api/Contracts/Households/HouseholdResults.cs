using CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.Households;

public sealed record CostSectionResult(
    CostSectionState State,
    decimal? KnownSubtotalSek,
    decimal? CompleteTotalSek,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<HouseholdInputError> Errors);

public sealed record VehicleCostResult(
    string CandidateKey,
    PurchaseFinancingResult? FinancingDetails,
    CostSectionResult Financing,
    HouseholdDepreciationResult Depreciation,
    HouseholdEnergyResult Energy,
    HouseholdCategoryResult Tax,
    HouseholdCategoryResult Insurance,
    HouseholdCategoryResult Service,
    HouseholdCategoryResult Repairs,
    CostSectionResult RepairAllowance,
    HouseholdCategoryResult CustomCosts,
    HouseholdCostTotals Totals,
    IReadOnlyList<HouseholdInputError> InputErrors,
    AcquisitionType AcquisitionType,
    HouseholdLeaseResult Lease,
    HouseholdPaymentCalendar Payments,
    HouseholdBudgetResult StartupBudget,
    HouseholdBudgetResult MonthlyBudget,
    HouseholdCashReconciliation Reconciliation);

public sealed record HouseholdDepreciationResult(
    CostSectionResult Cost,
    decimal? ResidualValueSek);

public sealed record HouseholdEnergyResult(
    CostSectionResult Cost,
    IReadOnlyList<HouseholdEnergySourceResult> Sources,
    bool IsIncluded);

public sealed record HouseholdEnergySourceResult(
    string Key,
    FuelType? Fuel,
    EnergyUnit? Unit,
    decimal? BaseQuantity,
    decimal? PurchasedQuantity,
    decimal? EffectivePricePerUnitSek,
    CostSectionResult Cost);

public sealed record HouseholdCategoryResult(
    bool IsIncluded,
    CostSectionResult Cost,
    IReadOnlyList<HouseholdCostItemResult> Items);

public sealed record HouseholdCostItemResult(
    string Key,
    string Label,
    CostSectionResult Cost);

public sealed record HouseholdCostTotals(
    decimal? DistanceKilometres,
    CostSectionResult OwnershipCost,
    CostSectionResult MonthlyCost,
    CostSectionResult CostPerMil,
    CostSectionResult EndEquity);

public sealed record HouseholdLeaseResult(
    CostSectionResult Cost,
    int? TermMonths,
    int? CoveredMonths,
    bool IsEstimate,
    decimal? ExcessDistanceKilometres,
    CostSectionResult DepositWithheld);

public sealed record HouseholdPaymentSourceResult(
    string Key,
    string Category,
    string Label,
    HouseholdPaymentDirection Direction,
    bool IsEstimate,
    IReadOnlyList<int> MonthOffsets,
    CostSectionResult Payments,
    CostSectionResult UnscheduledAmount);

public sealed record HouseholdPaymentCategoryResult(
    string Category,
    HouseholdPaymentDirection Direction,
    CostSectionResult Amount);

public sealed record HouseholdPaymentMonth(
    int MonthOffset,
    CalendarMonth? CalendarMonth,
    CostSectionResult Outflow,
    CostSectionResult Inflow,
    CostSectionResult InternalSaving,
    IReadOnlyList<HouseholdPaymentCategoryResult> Categories);

public sealed record HouseholdPaymentCalendar(
    int? RequestedMonths,
    int? CoveredMonths,
    CostSectionResult CalendarStatus,
    IReadOnlyList<HouseholdPaymentMonth> Months,
    IReadOnlyList<HouseholdPaymentSourceResult> Sources,
    CostSectionResult ExternalOutflow,
    CostSectionResult ExternalInflow,
    CostSectionResult NetExternalCashFlow,
    CostSectionResult InternalSaving);

public sealed record HouseholdBudgetResult(
    decimal? LimitSek,
    HouseholdBudgetStatus Status,
    CostSectionResult FundingRequired);

public sealed record HouseholdCashReconciliation(
    CostSectionResult PurchaseCash,
    CostSectionResult PrincipalRepaid,
    CostSectionResult Depreciation,
    CostSectionResult AccruedOperatingCosts,
    CostSectionResult PaidOperatingCosts,
    CostSectionResult DepositPaid,
    CostSectionResult DepositRefund,
    CostSectionResult DepositWithheld,
    CostSectionResult RepairAllowance,
    CostSectionResult ReconciledOwnershipCost);

public sealed record PurchaseFinancingResult(
    string CandidateKey,
    FinancingState State,
    PurchaseCashAllocation? Allocation,
    HouseholdLoanCalculation? Loan,
    decimal? SetupFeeSek,
    decimal? MonthlyFeesDuringPeriodSek,
    decimal? FinancingCostDuringPeriodSek,
    decimal? AcquisitionCashOutflowSek,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<HouseholdInputError> Errors);

public sealed record PurchaseCashAllocation(
    decimal CashAppliedSek,
    decimal PrincipalSek,
    decimal UnusedPurchaseCashSek);

public sealed record HouseholdLoanCalculation(
    int TermMonths,
    decimal AnnualNominalInterestRatePercent,
    decimal MonthlyInstallmentSek,
    decimal PaymentsDuringPeriodSek,
    decimal PrincipalRepaidSek,
    decimal InterestPaidSek,
    decimal RemainingPrincipalSek,
    IReadOnlyList<LoanInstallment> Installments);

public sealed record LoanInstallment(
    int MonthOffset,
    decimal OpeningPrincipalSek,
    decimal InterestSek,
    decimal PrincipalRepaidSek,
    decimal PaymentSek,
    decimal RemainingPrincipalSek);

public sealed record HouseholdInputError(
    string Path,
    string Code,
    string Message);

public sealed record HouseholdPreviewResponse(string RequestId, HouseholdProfileInput Profile,
    string Currency, int CalculationVersion, int ResultSchemaVersion, SensitivityMode ActiveSensitivityMode,
    IReadOnlyList<HouseholdInputError> ProfileErrors, IReadOnlyList<HouseholdVehiclePreview> Vehicles);

public sealed record HouseholdVehiclePreview(string CandidateKey, string? RegistrationNumber,
    VehicleCostInput Input, IReadOnlyList<LegacyReviewResponse> UnresolvedLegacyItems,
    bool IsCostComparable, VehicleCostResult Sections);
