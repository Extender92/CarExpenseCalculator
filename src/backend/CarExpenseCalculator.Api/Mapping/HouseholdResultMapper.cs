using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using A = CarExpenseCalculator.Api.Contracts.Households;
using C = CarExpenseCalculator.Core.Households;
using AL = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using AM = CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Mapping;

internal static class HouseholdResultMapper
{
    [return: NotNullIfNotNull(nameof(x))]
    public static A.CostSectionResult? ToApi(C.CostSectionResult? x) => x is null ? null : new(
        (A.CostSectionState)x.State,
        Round(x.KnownSubtotalSek, 2),
        Round(x.CompleteTotalSek, 2),
        x.MissingComponents.Select(InputPath).ToArray(),
        x.Errors.Select(item => ToApi(item)!).ToArray());

    [return: NotNullIfNotNull(nameof(x))]
    public static A.VehicleCostResult? ToApi(C.VehicleCostResult? x) => x is null ? null : new(
        x.CandidateKey,
        ToApi(x.FinancingDetails),
        ToApi(x.Financing),
        ToApi(x.Depreciation),
        ToApi(x.Energy),
        ToApi(x.Tax),
        ToApi(x.Insurance),
        ToApi(x.Service),
        ToApi(x.Repairs),
        ToApi(x.RepairAllowance),
        ToApi(x.CustomCosts),
        ToApi(x.Totals),
        x.InputErrors.Select(item => ToApi(item)!).ToArray(),
        (A.AcquisitionType)x.AcquisitionType,
        ToApi(x.Lease),
        ToApi(x.Payments),
        ToApi(x.StartupBudget),
        ToApi(x.MonthlyBudget),
        ToApi(x.Reconciliation));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdDepreciationResult? ToApi(C.HouseholdDepreciationResult? x) => x is null ? null : new(
        ToApi(x.Cost),
        Round(x.ResidualValueSek, 2));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdEnergyResult? ToApi(C.HouseholdEnergyResult? x) => x is null ? null : new(
        ToApi(x.Cost),
        x.Sources.Select(item => ToApi(item)!).ToArray(),
        x.IsIncluded);

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdEnergySourceResult? ToApi(C.HouseholdEnergySourceResult? x) => x is null ? null : new(
        x.Key,
        (AL.FuelType?)x.Fuel,
        (AM.EnergyUnit?)x.Unit,
        Round(x.BaseQuantity, 3),
        Round(x.PurchasedQuantity, 3),
        Round(x.EffectivePricePerUnitSek, 2),
        ToApi(x.Cost));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCategoryResult? ToApi(C.HouseholdCategoryResult? x) => x is null ? null : new(
        x.IsIncluded,
        ToApi(x.Cost),
        x.Items.Select(item => ToApi(item)!).ToArray());

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCostItemResult? ToApi(C.HouseholdCostItemResult? x) => x is null ? null : new(
        x.Key,
        x.Label,
        ToApi(x.Cost));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCostTotals? ToApi(C.HouseholdCostTotals? x) => x is null ? null : new(
        Round(x.DistanceKilometres, 3),
        ToApi(x.OwnershipCost),
        ToApi(x.MonthlyCost),
        ToApi(x.CostPerMil),
        ToApi(x.EndEquity));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLeaseResult? ToApi(C.HouseholdLeaseResult? x) => x is null ? null : new(
        ToApi(x.Cost),
        x.TermMonths,
        x.CoveredMonths,
        x.IsEstimate,
        Round(x.ExcessDistanceKilometres, 3),
        ToApi(x.DepositWithheld));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdPaymentSourceResult? ToApi(C.HouseholdPaymentSourceResult? x) => x is null ? null : new(
        x.Key,
        x.Category,
        x.Label,
        (A.HouseholdPaymentDirection)x.Direction,
        x.IsEstimate,
        x.MonthOffsets,
        ToApi(x.Payments),
        ToApi(x.UnscheduledAmount));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdPaymentCategoryResult? ToApi(C.HouseholdPaymentCategoryResult? x) => x is null ? null : new(
        x.Category,
        (A.HouseholdPaymentDirection)x.Direction,
        ToApi(x.Amount));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdPaymentMonth? ToApi(C.HouseholdPaymentMonth? x) => x is null ? null : new(
        x.MonthOffset,
        HouseholdInputMapper.ToApi(x.CalendarMonth),
        ToApi(x.Outflow),
        ToApi(x.Inflow),
        ToApi(x.InternalSaving),
        x.Categories.Select(item => ToApi(item)!).ToArray());

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdPaymentCalendar? ToApi(C.HouseholdPaymentCalendar? x) => x is null ? null : new(
        x.RequestedMonths,
        x.CoveredMonths,
        ToApi(x.CalendarStatus),
        x.Months.Select(item => ToApi(item)!).ToArray(),
        x.Sources.Select(item => ToApi(item)!).ToArray(),
        ToApi(x.ExternalOutflow),
        ToApi(x.ExternalInflow),
        ToApi(x.NetExternalCashFlow),
        ToApi(x.InternalSaving));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdBudgetResult? ToApi(C.HouseholdBudgetResult? x) => x is null ? null : new(
        Round(x.LimitSek, 2),
        (A.HouseholdBudgetStatus)x.Status,
        ToApi(x.FundingRequired));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCashReconciliation? ToApi(C.HouseholdCashReconciliation? x) => x is null ? null : new(
        ToApi(x.PurchaseCash),
        ToApi(x.PrincipalRepaid),
        ToApi(x.Depreciation),
        ToApi(x.AccruedOperatingCosts),
        ToApi(x.PaidOperatingCosts),
        ToApi(x.DepositPaid),
        ToApi(x.DepositRefund),
        ToApi(x.DepositWithheld),
        ToApi(x.RepairAllowance),
        ToApi(x.ReconciledOwnershipCost));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.PurchaseFinancingResult? ToApi(C.PurchaseFinancingResult? x) => x is null ? null : new(
        x.CandidateKey,
        (A.FinancingState)x.State,
        ToApi(x.Allocation),
        ToApi(x.Loan),
        Round(x.SetupFeeSek, 2),
        Round(x.MonthlyFeesDuringPeriodSek, 2),
        Round(x.FinancingCostDuringPeriodSek, 2),
        Round(x.AcquisitionCashOutflowSek, 2),
        x.MissingComponents.Select(InputPath).ToArray(),
        x.Errors.Select(item => ToApi(item)!).ToArray());

    [return: NotNullIfNotNull(nameof(x))]
    public static A.PurchaseCashAllocation? ToApi(C.PurchaseCashAllocation? x) => x is null ? null : new(
        Round(x.CashAppliedSek, 2),
        Round(x.PrincipalSek, 2),
        Round(x.UnusedPurchaseCashSek, 2));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLoanCalculation? ToApi(C.HouseholdLoanCalculation? x) => x is null ? null : new(
        x.TermMonths,
        Round(x.AnnualNominalInterestRatePercent, 3),
        Round(x.MonthlyInstallmentSek, 2),
        Round(x.PaymentsDuringPeriodSek, 2),
        Round(x.PrincipalRepaidSek, 2),
        Round(x.InterestPaidSek, 2),
        Round(x.RemainingPrincipalSek, 2),
        x.Installments.Select(item => ToApi(item)!).ToArray());

    [return: NotNullIfNotNull(nameof(x))]
    public static A.LoanInstallment? ToApi(C.LoanInstallment? x) => x is null ? null : new(
        x.MonthOffset,
        Round(x.OpeningPrincipalSek, 2),
        Round(x.InterestSek, 2),
        Round(x.PrincipalRepaidSek, 2),
        Round(x.PaymentSek, 2),
        Round(x.RemainingPrincipalSek, 2));

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdInputError? ToApi(C.HouseholdInputError? x) => x is null ? null : new(
        InputPath(x.Path),
        x.Code,
        x.Message);

    private static decimal? Round(decimal? value, int digits) => value is null ? null
        : decimal.Round(value.Value, digits, MidpointRounding.AwayFromZero);
    private static decimal Round(decimal value, int digits) => decimal.Round(value, digits, MidpointRounding.AwayFromZero);

    // Core's vehicle shape is nested under each HTTP candidate's input property.
    internal static string InputPath(string path) => Regex.Replace(path, @"^vehicles\[(\d+)\](?!\.input)", "vehicles[$1].input");
}
