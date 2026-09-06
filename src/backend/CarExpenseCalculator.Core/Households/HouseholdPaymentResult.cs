namespace CarExpenseCalculator.Core.Households;

public enum HouseholdPaymentDirection { Outflow, Inflow, InternalSaving }
public enum HouseholdBudgetStatus { NotConfigured, WithinLimit, Exceeded, Unknown, Invalid }

public sealed record HouseholdLeaseResult(
    CostSectionResult Cost, int? TermMonths, int? CoveredMonths, bool IsEstimate,
    decimal? ExcessDistanceKilometres, CostSectionResult DepositWithheld);

public sealed record HouseholdPaymentSourceResult(
    string Key, string Category, string Label, HouseholdPaymentDirection Direction, bool IsEstimate,
    IReadOnlyList<int> MonthOffsets, CostSectionResult Payments, CostSectionResult UnscheduledAmount);

public sealed record HouseholdPaymentCategoryResult(
    string Category, HouseholdPaymentDirection Direction, CostSectionResult Amount);

public sealed record HouseholdPaymentMonth(
    int MonthOffset, CalendarMonth? CalendarMonth, CostSectionResult Outflow,
    CostSectionResult Inflow, CostSectionResult InternalSaving,
    IReadOnlyList<HouseholdPaymentCategoryResult> Categories);

public sealed record HouseholdPaymentCalendar(
    int? RequestedMonths, int? CoveredMonths, CostSectionResult CalendarStatus,
    IReadOnlyList<HouseholdPaymentMonth> Months, IReadOnlyList<HouseholdPaymentSourceResult> Sources,
    CostSectionResult ExternalOutflow, CostSectionResult ExternalInflow,
    CostSectionResult NetExternalCashFlow, CostSectionResult InternalSaving);

public sealed record HouseholdBudgetResult(
    decimal? LimitSek, HouseholdBudgetStatus Status, CostSectionResult FundingRequired);

public sealed record HouseholdCashReconciliation(
    CostSectionResult PurchaseCash, CostSectionResult PrincipalRepaid, CostSectionResult Depreciation,
    CostSectionResult AccruedOperatingCosts, CostSectionResult PaidOperatingCosts,
    CostSectionResult DepositPaid, CostSectionResult DepositRefund, CostSectionResult DepositWithheld,
    CostSectionResult RepairAllowance, CostSectionResult ReconciledOwnershipCost);
