namespace CarExpenseCalculator.Core.CostScenarios;

public sealed class CostScenario
{
    public CostScenario(
        string? vehicleLabel,
        int calculationPeriodMonths,
        decimal purchasePriceSek,
        decimal? expectedResidualValueSek,
        decimal annualDistanceKilometres,
        FinancingTerms? financing,
        IEnumerable<EnergySource> energySources,
        RecurringCost? vehicleTax,
        RecurringCost? insurance,
        RecurringCost? maintenanceAndRepairs,
        IEnumerable<NamedRecurringCost> otherRecurringCosts,
        IEnumerable<OneTimeCost> otherOneTimeCosts)
    {
        ArgumentNullException.ThrowIfNull(energySources);
        ArgumentNullException.ThrowIfNull(otherRecurringCosts);
        ArgumentNullException.ThrowIfNull(otherOneTimeCosts);

        VehicleLabel = vehicleLabel;
        CalculationPeriodMonths = calculationPeriodMonths;
        PurchasePriceSek = purchasePriceSek;
        ExpectedResidualValueSek = expectedResidualValueSek;
        AnnualDistanceKilometres = annualDistanceKilometres;
        Financing = financing;
        EnergySources = Array.AsReadOnly(energySources.ToArray());
        VehicleTax = vehicleTax;
        Insurance = insurance;
        MaintenanceAndRepairs = maintenanceAndRepairs;
        OtherRecurringCosts = Array.AsReadOnly(otherRecurringCosts.ToArray());
        OtherOneTimeCosts = Array.AsReadOnly(otherOneTimeCosts.ToArray());
    }

    public string? VehicleLabel { get; }

    public int CalculationPeriodMonths { get; }

    public decimal PurchasePriceSek { get; }

    public decimal? ExpectedResidualValueSek { get; }

    public decimal AnnualDistanceKilometres { get; }

    public FinancingTerms? Financing { get; }

    public IReadOnlyList<EnergySource> EnergySources { get; }

    public RecurringCost? VehicleTax { get; }

    public RecurringCost? Insurance { get; }

    public RecurringCost? MaintenanceAndRepairs { get; }

    public IReadOnlyList<NamedRecurringCost> OtherRecurringCosts { get; }

    public IReadOnlyList<OneTimeCost> OtherOneTimeCosts { get; }
}

public sealed record FinancingTerms(
    decimal DownPaymentSek,
    decimal AnnualNominalInterestRatePercent,
    int TermMonths);

public sealed record EnergySource(
    string Label,
    EnergyUnit Unit,
    decimal ConsumptionPer100Kilometres,
    decimal PricePerUnitSek,
    decimal DistanceSharePercent);

public sealed record RecurringCost(
    decimal AmountSek,
    RecurringCostCadence Cadence);

public sealed record NamedRecurringCost(
    string Label,
    decimal AmountSek,
    RecurringCostCadence Cadence);

public sealed record OneTimeCost(
    string Label,
    decimal AmountSek);

public enum EnergyUnit
{
    Litre,
    KilowattHour,
    Kilogram,
}

public enum RecurringCostCadence
{
    Monthly,
    Annual,
}

public enum MissingCostCategory
{
    VehicleTax,
    Insurance,
    MaintenanceAndRepairs,
    ResidualValue,
}
