using CarExpenseCalculator.Api.Contracts.Comparisons;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using Microsoft.AspNetCore.Mvc;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[ProducesResponseType(typeof(ComparisonValidationProblemDetails), 400, "application/problem+json")]
[ProducesResponseType(typeof(ComparisonProblemDetails), 404, "application/problem+json")]
[ProducesResponseType(typeof(ComparisonProblemDetails), 409, "application/problem+json")]
[ProducesResponseType(typeof(ComparisonProblemDetails), 413, "application/problem+json")]
[ProducesResponseType(typeof(ComparisonProblemDetails), 503, "application/problem+json")]
[Route("api/vehicle-facts")]
public sealed class VehicleFactsController(IVehicleFactsStore store) : ControllerBase
{
    [HttpGet("{vehicleId:guid}")]
    [ProducesResponseType<VehicleFactsResponse>(200)]
    public async Task<ActionResult<VehicleFactsResponse>> Get(Guid vehicleId, CancellationToken ct) =>
        Ok(ComparisonInputMapper.ToApi(await store.GetAsync(vehicleId, ct)
            ?? throw new ComparisonStoreException("vehicleNotFound", "Vehicle no longer exists.", vehicleId)));
    [HttpPut("{vehicleId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<VehicleFactsResponse>(200)]
    public async Task<ActionResult<VehicleFactsResponse>> Save(Guid vehicleId, SaveVehicleFactsRequest request, CancellationToken ct)
    {
        try { return Ok(ComparisonInputMapper.ToApi(await store.SaveAsync(vehicleId, request.ExpectedRevision, ComparisonInputMapper.ToStore(request.Input), ct))); }
        catch (Core.Comparisons.ComparisonInputValidationException e)
        {
            throw new Core.Comparisons.ComparisonInputValidationException(e.Errors.Select(x => new Core.Comparisons.ComparisonInputError(
                x.Path == "expectedRevision" ? x.Path : "input." + ComparisonInputMapper.FactWritePath(x.Path), x.Code, x.Message)));
        }
    }
}
