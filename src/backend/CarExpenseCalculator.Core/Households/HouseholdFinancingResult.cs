namespace CarExpenseCalculator.Core.Households;

public enum FinancingState
{
    Complete,
    Partial,
    Unavailable,
    Invalid,
}

// These Core amounts remain unrounded for composition with later cost sections.
public sealed record HouseholdFinancingPreview(
    string Currency,
    SensitivityMode ActiveSensitivityMode,
    IReadOnlyList<HouseholdInputError> ProfileErrors,
    IReadOnlyList<PurchaseFinancingResult> Vehicles);

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

public sealed record PurchaseCashAllocation(decimal CashAppliedSek, decimal PrincipalSek, decimal UnusedPurchaseCashSek);

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
