using CarExpenseCalculator.Api.Contracts.ManualCalculations;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.CostScenarios;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/manual-calculations")]
public sealed class ManualCalculationsController(CostScenarioCalculator calculator) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ManualCalculationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult<ManualCalculationResult> Calculate(ManualCalculationRequest request)
    {
        try
        {
            var scenario = ManualCalculationMapper.ToCore(request);
            var result = calculator.Calculate(scenario);

            return Ok(ManualCalculationMapper.ToApi(result));
        }
        catch (CostScenarioValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(error => error.Path, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Message).ToArray(),
                    StringComparer.Ordinal);

            return ValidationProblem(new ValidationProblemDetails(errors));
        }
    }
}
