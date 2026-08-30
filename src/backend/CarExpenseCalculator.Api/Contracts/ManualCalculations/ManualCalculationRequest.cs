namespace CarExpenseCalculator.Api.Contracts.ManualCalculations;

public sealed record ManualCalculationRequest
{
    public string? VehicleLabel { get; init; }

    public required int CalculationPeriodMonths { get; init; }

    public required decimal PurchasePriceSek { get; init; }

    public decimal? ExpectedResidualValueSek { get; init; }

    public required decimal AnnualDistanceKilometres { get; init; }

    public FinancingInput? Financing { get; init; }

    public required IReadOnlyList<EnergySourceInput> EnergySources { get; init; }

    public required RecurringCostInput? VehicleTax { get; init; }

    public required RecurringCostInput? Insurance { get; init; }

    public required RecurringCostInput? MaintenanceAndRepairs { get; init; }

    public required IReadOnlyList<NamedRecurringCostInput> OtherRecurringCosts { get; init; }

    public required IReadOnlyList<OneTimeCostInput> OtherOneTimeCosts { get; init; }
}

public sealed record FinancingInput
{
    public required decimal DownPaymentSek { get; init; }

    public required decimal AnnualNominalInterestRatePercent { get; init; }

    public required int TermMonths { get; init; }
}

public sealed record EnergySourceInput
{
    public required string Label { get; init; }

    public required EnergyUnit Unit { get; init; }

    public required decimal ConsumptionPer100Kilometres { get; init; }

    public required decimal PricePerUnitSek { get; init; }

    public required decimal DistanceSharePercent { get; init; }
}

public record RecurringCostInput
{
    public required decimal AmountSek { get; init; }

    public required RecurringCostCadence Cadence { get; init; }
}

public sealed record NamedRecurringCostInput : RecurringCostInput
{
    public required string Label { get; init; }
}

public sealed record OneTimeCostInput
{
    public required string Label { get; init; }

    public required decimal AmountSek { get; init; }
}
