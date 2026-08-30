using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

internal sealed record CostCalculationSnapshot(
    string Currency,
    int CalculationPeriodMonths,
    decimal TotalDistanceKilometres,
    CalculationCompletenessSnapshot Completeness,
    CashFlowSnapshot CashFlow,
    FinancingSnapshot? Financing,
    EnergyBreakdownSnapshot Energy,
    IReadOnlyList<RecurringCostSnapshot> OtherRecurringCosts,
    IReadOnlyList<OneTimeCostSnapshot> OtherOneTimeCosts,
    NetOwnershipCostSnapshot? NetOwnershipCost)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static string Serialize(CostCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(FromCore(result), SerializerOptions);
    }

    public static CostCalculationResult Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<CostCalculationSnapshot>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The saved calculation result snapshot is empty.");
        return snapshot.ToCore();
    }

    private static CostCalculationSnapshot FromCore(CostCalculationResult result)
    {
        return new CostCalculationSnapshot(
            result.Currency,
            result.CalculationPeriodMonths,
            result.TotalDistanceKilometres,
            new CalculationCompletenessSnapshot(
                result.Completeness.IsComplete,
                result.Completeness.IsCashFlowComplete,
                result.Completeness.IsNetOwnershipCostAvailable,
                result.Completeness.MissingCategories.ToArray()),
            CashFlowSnapshot.FromCore(result.CashFlow),
            result.Financing is null ? null : FinancingSnapshot.FromCore(result.Financing),
            EnergyBreakdownSnapshot.FromCore(result.Energy),
            result.OtherRecurringCosts.Select(RecurringCostSnapshot.FromCore).ToArray(),
            result.OtherOneTimeCosts.Select(OneTimeCostSnapshot.FromCore).ToArray(),
            result.NetOwnershipCost is null
                ? null
                : NetOwnershipCostSnapshot.FromCore(result.NetOwnershipCost));
    }

    private CostCalculationResult ToCore()
    {
        return new CostCalculationResult(
            Currency,
            CalculationPeriodMonths,
            TotalDistanceKilometres,
            new CalculationCompleteness(
                Completeness.IsComplete,
                Completeness.IsCashFlowComplete,
                Completeness.IsNetOwnershipCostAvailable,
                Array.AsReadOnly(Completeness.MissingCategories.ToArray())),
            CashFlow.ToCore(),
            Financing?.ToCore(),
            Energy.ToCore(),
            Array.AsReadOnly(OtherRecurringCosts.Select(cost => cost.ToCore()).ToArray()),
            Array.AsReadOnly(OtherOneTimeCosts.Select(cost => cost.ToCore()).ToArray()),
            NetOwnershipCost?.ToCore());
    }
}

internal sealed record CalculationCompletenessSnapshot(
    bool IsComplete,
    bool IsCashFlowComplete,
    bool IsNetOwnershipCostAvailable,
    IReadOnlyList<MissingCostCategory> MissingCategories);

internal sealed record CashFlowSnapshot(
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
    bool IsComplete)
{
    public static CashFlowSnapshot FromCore(CashFlowResult result) => new(
        result.AcquisitionCashPaidSek,
        result.LoanPaymentsDuringPeriodSek,
        result.EnergyCostSek,
        result.VehicleTaxSek,
        result.InsuranceSek,
        result.MaintenanceAndRepairsSek,
        result.OtherRecurringCostSek,
        result.OtherOneTimeCostSek,
        result.KnownOperatingCostSek,
        result.KnownTotalSek,
        result.AveragePerMonthSek,
        result.AveragePerYearSek,
        result.IsComplete);

    public CashFlowResult ToCore() => new(
        AcquisitionCashPaidSek,
        LoanPaymentsDuringPeriodSek,
        EnergyCostSek,
        VehicleTaxSek,
        InsuranceSek,
        MaintenanceAndRepairsSek,
        OtherRecurringCostSek,
        OtherOneTimeCostSek,
        KnownOperatingCostSek,
        KnownTotalSek,
        AveragePerMonthSek,
        AveragePerYearSek,
        IsComplete);
}

internal sealed record FinancingSnapshot(
    decimal DownPaymentSek,
    decimal PrincipalSek,
    decimal AnnualNominalInterestRatePercent,
    int TermMonths,
    decimal MonthlyPaymentSek,
    int PaymentsMade,
    decimal LoanPaymentsDuringPeriodSek,
    decimal PrincipalRepaidSek,
    decimal InterestPaidSek,
    decimal RemainingPrincipalSek)
{
    public static FinancingSnapshot FromCore(FinancingResult result) => new(
        result.DownPaymentSek,
        result.PrincipalSek,
        result.AnnualNominalInterestRatePercent,
        result.TermMonths,
        result.MonthlyPaymentSek,
        result.PaymentsMade,
        result.LoanPaymentsDuringPeriodSek,
        result.PrincipalRepaidSek,
        result.InterestPaidSek,
        result.RemainingPrincipalSek);

    public FinancingResult ToCore() => new(
        DownPaymentSek,
        PrincipalSek,
        AnnualNominalInterestRatePercent,
        TermMonths,
        MonthlyPaymentSek,
        PaymentsMade,
        LoanPaymentsDuringPeriodSek,
        PrincipalRepaidSek,
        InterestPaidSek,
        RemainingPrincipalSek);
}

internal sealed record EnergyBreakdownSnapshot(
    IReadOnlyList<EnergySourceSnapshot> Sources,
    decimal TotalCostSek)
{
    public static EnergyBreakdownSnapshot FromCore(EnergyBreakdownResult result) => new(
        result.Sources.Select(EnergySourceSnapshot.FromCore).ToArray(),
        result.TotalCostSek);

    public EnergyBreakdownResult ToCore() => new(
        Array.AsReadOnly(Sources.Select(source => source.ToCore()).ToArray()),
        TotalCostSek);
}

internal sealed record EnergySourceSnapshot(
    string Label,
    EnergyUnit Unit,
    decimal DistanceSharePercent,
    decimal AllocatedDistanceKilometres,
    decimal ConsumptionPer100Kilometres,
    decimal ConsumedQuantity,
    decimal PricePerUnitSek,
    decimal CostSek)
{
    public static EnergySourceSnapshot FromCore(EnergySourceResult result) => new(
        result.Label,
        result.Unit,
        result.DistanceSharePercent,
        result.AllocatedDistanceKilometres,
        result.ConsumptionPer100Kilometres,
        result.ConsumedQuantity,
        result.PricePerUnitSek,
        result.CostSek);

    public EnergySourceResult ToCore() => new(
        Label,
        Unit,
        DistanceSharePercent,
        AllocatedDistanceKilometres,
        ConsumptionPer100Kilometres,
        ConsumedQuantity,
        PricePerUnitSek,
        CostSek);
}

internal sealed record RecurringCostSnapshot(
    string Label,
    decimal AmountSek,
    RecurringCostCadence Cadence,
    decimal CostDuringPeriodSek)
{
    public static RecurringCostSnapshot FromCore(RecurringCostResult result) => new(
        result.Label,
        result.AmountSek,
        result.Cadence,
        result.CostDuringPeriodSek);

    public RecurringCostResult ToCore() => new(Label, AmountSek, Cadence, CostDuringPeriodSek);
}

internal sealed record OneTimeCostSnapshot(string Label, decimal AmountSek)
{
    public static OneTimeCostSnapshot FromCore(OneTimeCostResult result) => new(
        result.Label,
        result.AmountSek);

    public OneTimeCostResult ToCore() => new(Label, AmountSek);
}

internal sealed record NetOwnershipCostSnapshot(
    decimal ResidualValueSek,
    decimal DepreciationSek,
    decimal InterestPaidSek,
    decimal KnownOperatingCostSek,
    decimal KnownTotalSek,
    decimal AveragePerMonthSek,
    decimal AveragePerYearSek,
    decimal EstimatedEquityAtPeriodEndSek,
    bool IsComplete)
{
    public static NetOwnershipCostSnapshot FromCore(NetOwnershipCostResult result) => new(
        result.ResidualValueSek,
        result.DepreciationSek,
        result.InterestPaidSek,
        result.KnownOperatingCostSek,
        result.KnownTotalSek,
        result.AveragePerMonthSek,
        result.AveragePerYearSek,
        result.EstimatedEquityAtPeriodEndSek,
        result.IsComplete);

    public NetOwnershipCostResult ToCore() => new(
        ResidualValueSek,
        DepreciationSek,
        InterestPaidSek,
        KnownOperatingCostSek,
        KnownTotalSek,
        AveragePerMonthSek,
        AveragePerYearSek,
        EstimatedEquityAtPeriodEndSek,
        IsComplete);
}
