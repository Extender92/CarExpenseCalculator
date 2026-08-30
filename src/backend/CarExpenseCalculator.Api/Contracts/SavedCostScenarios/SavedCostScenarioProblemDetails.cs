using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Contracts.SavedCostScenarios;

public sealed class SavedCostScenarioProblemDetails : ProblemDetails
{
    public required string Code { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ExistingVehicleId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExpectedRevision { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ActualRevision { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CalculationVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ResultSchemaVersion { get; init; }
}
