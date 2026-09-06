using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

public sealed record VehicleCostInput
{
    public VehicleCostInput(string candidateKey, decimal? priceSek, IEnumerable<HouseholdEnergySource>? energySources = null)
    {
        CandidateKey = candidateKey;
        PriceSek = priceSek;
        EnergySources = energySources is null ? null : Array.AsReadOnly(energySources.ToArray());
    }

    public string CandidateKey { get; init; }
    public AcquisitionType AcquisitionType { get; init; }
    public HouseholdLeaseInput? Lease { get; init; }
    public decimal? PriceSek { get; init; }
    public HouseholdResidualInput? Residual { get; init; }
    public IReadOnlyList<HouseholdEnergySource>? EnergySources { get; }
    public HouseholdCostCategoryInput? Tax { get; init; }
    public HouseholdCostCategoryInput? Insurance { get; init; }
    public HouseholdCostCategoryInput? Service { get; init; }
    public HouseholdCostCategoryInput? Repairs { get; init; }
    public SensitivityValue? AdditionalRepairAllowancePerMonthSek { get; init; }
    public HouseholdCostCategoryInput? CustomCosts { get; init; }

    public static VehicleCostInput ForLease(string candidateKey, HouseholdLeaseInput? lease,
        IEnumerable<HouseholdEnergySource>? energySources = null) =>
        new(candidateKey, null, energySources) { AcquisitionType = AcquisitionType.Lease, Lease = lease };
}

public enum AcquisitionType { Purchase, Lease }

public enum ResidualMode { FixedAmount, AnnualPercentage }

public sealed record HouseholdResidualInput
{
    private HouseholdResidualInput(ResidualMode mode, SensitivityValue? value, int? periodMonths)
    {
        Mode = mode;
        Value = value;
        PeriodMonths = periodMonths;
    }

    public ResidualMode Mode { get; }
    public SensitivityValue? Value { get; }
    public int? PeriodMonths { get; }

    public static HouseholdResidualInput FixedAmount(SensitivityValue? amountSek, int? periodMonths) =>
        new(ResidualMode.FixedAmount, amountSek, periodMonths);

    public static HouseholdResidualInput AnnualPercentage(SensitivityValue? annualRatePercent) =>
        new(ResidualMode.AnnualPercentage, annualRatePercent, null);
}

public enum ConsumptionBasis { WholeDistance, DrivingMode }
public enum ElectricityBasis { Battery, Metered }

public sealed record HouseholdEnergySource(
    string Key,
    FuelType? Fuel,
    EnergyUnit? Unit,
    SensitivityValue? ConsumptionPer100Kilometres,
    ConsumptionBasis? ConsumptionBasis,
    ElectricityBasis? ElectricityBasis = null);

public enum HouseholdCostCadence { Monthly, Annual, Once }

public sealed record HouseholdCostItem(
    string Key,
    string Label,
    SensitivityValue? AmountSek,
    HouseholdCostCadence? Cadence,
    int? MonthOffset = null,
    int? DueMonthOfYear = null,
    string? EvidenceNote = null,
    string? SourceUrl = null);

public sealed record HouseholdCostCategoryInput
{
    private HouseholdCostCategoryInput(bool included, IEnumerable<HouseholdCostItem> items)
    {
        IsIncluded = included;
        Items = Array.AsReadOnly(items.ToArray());
    }

    public bool IsIncluded { get; }
    public IReadOnlyList<HouseholdCostItem> Items { get; }

    public static HouseholdCostCategoryInput Included() => new(true, []);
    // Every supplied item is explicitly outside the included base service.
    public static HouseholdCostCategoryInput Included(IEnumerable<HouseholdCostItem> extras)
    {
        ArgumentNullException.ThrowIfNull(extras);
        return new(true, extras);
    }
    public static HouseholdCostCategoryInput KnownZero() => new(false, []);
    public static HouseholdCostCategoryInput FromItems(IEnumerable<HouseholdCostItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new(false, items);
    }
}
