using System.ComponentModel.DataAnnotations;
using CarExpenseCalculator.Api.Contracts.SavedCostScenarios;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/saved-cost-scenarios")]
public sealed class SavedCostScenariosController(ISavedCostScenarioStore store) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<SavedCostScenarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedCostScenarioResponse>> Create(
        CreateSavedCostScenarioRequest request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationNumber.TryParse(request.RegistrationNumber, out var registrationNumber))
        {
            return RegistrationNumberValidationProblem();
        }

        try
        {
            var scenario = ManualCalculationMapper.ToCore(request.Scenario);
            var savedScenario = await store.CreateAsync(
                registrationNumber!,
                scenario,
                cancellationToken);
            var response = SavedCostScenarioMapper.ToApi(savedScenario);

            return CreatedAtAction(
                nameof(GetById),
                new { vehicleId = savedScenario.VehicleId },
                response);
        }
        catch (CostScenarioValidationException exception)
        {
            return ScenarioValidationProblem(exception);
        }
        catch (RegistrationNumberConflictException exception)
        {
            return SavedProblem(
                StatusCodes.Status409Conflict,
                "A saved cost scenario already exists for this registration number.",
                "registrationNumberConflict",
                existingVehicleId: exception.ExistingVehicleId);
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SavedCostScenarioSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<SavedCostScenarioSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        try
        {
            var savedScenarios = await store.ListAsync(cancellationToken);
            return Ok(savedScenarios.Select(SavedCostScenarioMapper.ToSummaryApi).ToArray());
        }
        catch (UnsupportedSavedCostScenarioVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpGet("{vehicleId:guid}")]
    [ProducesResponseType<SavedCostScenarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedCostScenarioResponse>> GetById(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var savedScenario = await store.GetAsync(vehicleId, cancellationToken);
            return savedScenario is null
                ? SavedScenarioNotFoundProblem()
                : Ok(SavedCostScenarioMapper.ToApi(savedScenario));
        }
        catch (UnsupportedSavedCostScenarioVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpGet("by-registration/{registrationNumber}")]
    [ProducesResponseType<SavedCostScenarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedCostScenarioResponse>> GetByRegistrationNumber(
        string registrationNumber,
        CancellationToken cancellationToken)
    {
        if (!RegistrationNumber.TryParse(registrationNumber, out var normalizedRegistrationNumber))
        {
            return RegistrationNumberValidationProblem();
        }

        try
        {
            var savedScenario = await store.GetByRegistrationNumberAsync(
                normalizedRegistrationNumber!,
                cancellationToken);
            return savedScenario is null
                ? SavedScenarioNotFoundProblem()
                : Ok(SavedCostScenarioMapper.ToApi(savedScenario));
        }
        catch (UnsupportedSavedCostScenarioVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpPut("{vehicleId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<SavedCostScenarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SavedCostScenarioResponse>> Replace(
        Guid vehicleId,
        ReplaceSavedCostScenarioRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var scenario = ManualCalculationMapper.ToCore(request.Scenario);
            var savedScenario = await store.ReplaceAsync(
                vehicleId,
                request.ExpectedRevision,
                scenario,
                MapListingLinkMode(request.ListingLinkMode),
                cancellationToken);
            return Ok(SavedCostScenarioMapper.ToApi(savedScenario));
        }
        catch (CostScenarioValidationException exception)
        {
            return ScenarioValidationProblem(exception);
        }
        catch (SavedCostScenarioNotFoundException)
        {
            return SavedScenarioNotFoundProblem();
        }
        catch (SavedCostScenarioConcurrencyException exception)
        {
            return RevisionConflictProblem(exception);
        }
        catch (SavedScenarioListingLinkUnavailableException)
        {
            return SavedProblem(
                StatusCodes.Status409Conflict,
                "The saved vehicle does not contain a current listing to link.",
                "listingLinkUnavailable");
        }
        catch (UnsupportedSavedCostScenarioVersionException exception)
        {
            return UnsupportedVersionProblem(exception);
        }
    }

    [HttpDelete("{vehicleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(SavedCostScenarioProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid vehicleId,
        [FromQuery, BindRequired, Range(typeof(long), "1", "9223372036854775807")]
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteAsync(vehicleId, expectedRevision, cancellationToken);
            return NoContent();
        }
        catch (SavedCostScenarioNotFoundException)
        {
            return SavedScenarioNotFoundProblem();
        }
        catch (SavedCostScenarioConcurrencyException exception)
        {
            return RevisionConflictProblem(exception);
        }
    }

    private ActionResult RegistrationNumberValidationProblem()
    {
        return ValidationProblem(
            new ValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["registrationNumber"] =
                    ["Registration number must be a supported ordinary Swedish registration number."],
                }));
    }

    private ActionResult ScenarioValidationProblem(CostScenarioValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => $"scenario.{error.Path}", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);

        return ValidationProblem(new ValidationProblemDetails(errors));
    }

    private ObjectResult SavedScenarioNotFoundProblem()
    {
        return SavedProblem(
            StatusCodes.Status404NotFound,
            "The saved cost scenario was not found.",
            "savedCostScenarioNotFound");
    }

    private ObjectResult RevisionConflictProblem(SavedCostScenarioConcurrencyException exception)
    {
        return SavedProblem(
            StatusCodes.Status409Conflict,
            "The saved cost scenario has changed since it was loaded.",
            "revisionConflict",
            expectedRevision: exception.ExpectedRevision,
            actualRevision: exception.ActualRevision);
    }

    private ObjectResult UnsupportedVersionProblem(
        UnsupportedSavedCostScenarioVersionException exception)
    {
        return SavedProblem(
            StatusCodes.Status409Conflict,
            "The saved cost scenario uses an unsupported result version.",
            "unsupportedSavedScenarioVersion",
            calculationVersion: exception.CalculationVersion,
            resultSchemaVersion: exception.ResultSchemaVersion);
    }

    private ObjectResult SavedProblem(
        int statusCode,
        string title,
        string code,
        Guid? existingVehicleId = null,
        long? expectedRevision = null,
        long? actualRevision = null,
        int? calculationVersion = null,
        int? resultSchemaVersion = null)
    {
        var problem = new SavedCostScenarioProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode,
            Instance = HttpContext.Request.Path,
            Code = code,
            ExistingVehicleId = existingVehicleId,
            ExpectedRevision = expectedRevision,
            ActualRevision = actualRevision,
            CalculationVersion = calculationVersion,
            ResultSchemaVersion = resultSchemaVersion,
        };

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static SavedScenarioListingLinkMode MapListingLinkMode(ListingLinkMode value) =>
        value switch
        {
            ListingLinkMode.Preserve => SavedScenarioListingLinkMode.Preserve,
            ListingLinkMode.Current => SavedScenarioListingLinkMode.Current,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
}
