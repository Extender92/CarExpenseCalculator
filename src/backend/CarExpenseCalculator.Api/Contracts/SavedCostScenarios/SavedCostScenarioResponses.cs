using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.SavedCostScenarios;

public sealed record SavedCostScenarioResponse(
    Guid VehicleId,
    string RegistrationNumber,
    long Revision,
    int CalculationVersion,
    int ResultSchemaVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset CalculatedAtUtc,
    ManualCalculationRequest Scenario,
    ManualCalculationResult Result);

public sealed record SavedCostScenarioSummaryResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string? VehicleLabel,
    long Revision,
    decimal PurchasePriceSek,
    int CalculationPeriodMonths,
    decimal CashFlowKnownTotalSek,
    decimal? NetOwnershipCostKnownTotalSek,
    CalculationCompleteness Completeness,
    DateTimeOffset UpdatedAtUtc);
