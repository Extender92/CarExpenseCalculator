namespace CarExpenseCalculator.Core.CostScenarios;

public sealed record CostCalculationResult(
    string Currency,
    int CalculationPeriodMonths,
    decimal TotalDistanceKilometres,
    CalculationCompleteness Completeness,
    CashFlowResult CashFlow,
    FinancingResult? Financing,
    EnergyBreakdownResult Energy,
    IReadOnlyList<RecurringCostResult> OtherRecurringCosts,
    IReadOnlyList<OneTimeCostResult> OtherOneTimeCosts,
    NetOwnershipCostResult? NetOwnershipCost);

public sealed record CalculationCompleteness(
    bool IsComplete,
    bool IsCashFlowComplete,
    bool IsNetOwnershipCostAvailable,
    IReadOnlyList<MissingCostCategory> MissingCategories);

public sealed record CashFlowResult(
    decimal AcquisitionCashPaidSek,
    decimal LoanPaymentsDuringPeriodSek,
    decimal EnergyCostSek,
    decimal? VehicleTaxSek,
    decimal? InsuranceSek,
    decimal? MaintenanceAndRepairsSek,
    decimal OtherRecurringCostSek,
    decimal OtherOneTimeCostSek,
    decimal KnownOperatingCostSek,
    decimal KnownTotalSek,
    decimal AveragePerMonthSek,
    decimal AveragePerYearSek,
    bool IsComplete);

public sealed record FinancingResult(
    decimal DownPaymentSek,
    decimal PrincipalSek,
    decimal AnnualNominalInterestRatePercent,
    int TermMonths,
    decimal MonthlyPaymentSek,
    int PaymentsMade,
    decimal LoanPaymentsDuringPeriodSek,
    decimal PrincipalRepaidSek,
    decimal InterestPaidSek,
    decimal RemainingPrincipalSek);

public sealed record EnergyBreakdownResult(
    IReadOnlyList<EnergySourceResult> Sources,
    decimal TotalCostSek);

public sealed record EnergySourceResult(
    string Label,
    EnergyUnit Unit,
    decimal DistanceSharePercent,
    decimal AllocatedDistanceKilometres,
    decimal ConsumptionPer100Kilometres,
    decimal ConsumedQuantity,
    decimal PricePerUnitSek,
    decimal CostSek);

public sealed record RecurringCostResult(
    string Label,
    decimal AmountSek,
    RecurringCostCadence Cadence,
    decimal CostDuringPeriodSek);

public sealed record OneTimeCostResult(
    string Label,
    decimal AmountSek);

public sealed record NetOwnershipCostResult(
    decimal ResidualValueSek,
    decimal DepreciationSek,
    decimal InterestPaidSek,
    decimal KnownOperatingCostSek,
    decimal KnownTotalSek,
    decimal AveragePerMonthSek,
    decimal AveragePerYearSek,
    decimal EstimatedEquityAtPeriodEndSek,
    bool IsComplete);
