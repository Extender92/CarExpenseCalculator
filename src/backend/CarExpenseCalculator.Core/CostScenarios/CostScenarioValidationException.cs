namespace CarExpenseCalculator.Core.CostScenarios;

public sealed record CostScenarioValidationError(
    string Path,
    string Message);

public sealed class CostScenarioValidationException : Exception
{
    public CostScenarioValidationException(IEnumerable<CostScenarioValidationError> errors)
        : base("The cost scenario is invalid.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<CostScenarioValidationError> Errors { get; }
}
