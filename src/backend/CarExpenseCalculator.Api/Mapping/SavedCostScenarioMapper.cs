using CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

namespace CarExpenseCalculator.Api.Mapping;

internal static class SavedCostScenarioMapper
{
    public static SavedCostScenarioResponse ToApi(SavedCostScenario savedScenario)
    {
        return new SavedCostScenarioResponse(
            savedScenario.VehicleId,
            savedScenario.RegistrationNumber.Value,
            savedScenario.Revision,
            savedScenario.CalculationVersion,
            savedScenario.ResultSchemaVersion,
            savedScenario.CreatedAtUtc,
            savedScenario.UpdatedAtUtc,
            savedScenario.CalculatedAtUtc,
            ManualCalculationMapper.ToApi(savedScenario.Scenario),
            ManualCalculationMapper.ToApi(savedScenario.Result));
    }

    public static SavedCostScenarioSummaryResponse ToSummaryApi(SavedCostScenario savedScenario)
    {
        var result = savedScenario.Result;
        return new SavedCostScenarioSummaryResponse(
            savedScenario.VehicleId,
            savedScenario.RegistrationNumber.Value,
            savedScenario.Scenario.VehicleLabel,
            savedScenario.Revision,
            savedScenario.Scenario.PurchasePriceSek,
            savedScenario.Scenario.CalculationPeriodMonths,
            result.CashFlow.KnownTotalSek,
            result.NetOwnershipCost?.KnownTotalSek,
            ManualCalculationMapper.ToApi(result.Completeness),
            savedScenario.UpdatedAtUtc);
    }
}
