using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

internal sealed class SavedCostScenarioEntity
{
    public Guid Id { get; set; }

    public Guid VehicleId { get; set; }

    public required VehicleEntity Vehicle { get; set; }

    public int CalculationPeriodMonths { get; set; }

    public decimal PurchasePriceSek { get; set; }

    public decimal? ExpectedResidualValueSek { get; set; }

    public decimal AnnualDistanceKilometres { get; set; }

    public decimal? FinancingDownPaymentSek { get; set; }

    public decimal? FinancingAnnualNominalInterestRatePercent { get; set; }

    public int? FinancingTermMonths { get; set; }

    public decimal? VehicleTaxAmountSek { get; set; }

    public RecurringCostCadence? VehicleTaxCadence { get; set; }

    public decimal? InsuranceAmountSek { get; set; }

    public RecurringCostCadence? InsuranceCadence { get; set; }

    public decimal? MaintenanceAndRepairsAmountSek { get; set; }

    public RecurringCostCadence? MaintenanceAndRepairsCadence { get; set; }

    public int CalculationVersion { get; set; }

    public int ResultSchemaVersion { get; set; }

    public required string ResultSnapshotJson { get; set; }

    public DateTimeOffset CalculatedAtUtc { get; set; }

    public List<ScenarioEnergySourceEntity> EnergySources { get; } = [];

    public List<ScenarioRecurringCostEntity> OtherRecurringCosts { get; } = [];

    public List<ScenarioOneTimeCostEntity> OtherOneTimeCosts { get; } = [];
}

internal sealed class ScenarioEnergySourceEntity
{
    public Guid Id { get; set; }

    public Guid ScenarioId { get; set; }

    public required SavedCostScenarioEntity Scenario { get; set; }

    public int Position { get; set; }

    public required string Label { get; set; }

    public EnergyUnit Unit { get; set; }

    public decimal ConsumptionPer100Kilometres { get; set; }

    public decimal PricePerUnitSek { get; set; }

    public decimal DistanceSharePercent { get; set; }
}

internal sealed class ScenarioRecurringCostEntity
{
    public Guid Id { get; set; }

    public Guid ScenarioId { get; set; }

    public required SavedCostScenarioEntity Scenario { get; set; }

    public int Position { get; set; }

    public required string Label { get; set; }

    public decimal AmountSek { get; set; }

    public RecurringCostCadence Cadence { get; set; }
}

internal sealed class ScenarioOneTimeCostEntity
{
    public Guid Id { get; set; }

    public Guid ScenarioId { get; set; }

    public required SavedCostScenarioEntity Scenario { get; set; }

    public int Position { get; set; }

    public required string Label { get; set; }

    public decimal AmountSek { get; set; }
}
