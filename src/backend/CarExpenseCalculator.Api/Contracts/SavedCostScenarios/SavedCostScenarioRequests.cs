using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.SavedCostScenarios;

public sealed record CreateSavedCostScenarioRequest
{
    public required string RegistrationNumber { get; init; }

    public required ManualCalculationRequest Scenario { get; init; }
}

public sealed record ReplaceSavedCostScenarioRequest
{
    [Range(typeof(long), "1", "9223372036854775807")]
    public required long ExpectedRevision { get; init; }

    public required ManualCalculationRequest Scenario { get; init; }
}
