using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[ProducesResponseType(typeof(HouseholdValidationProblemDetails), 400, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 404, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 409, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 413, "application/problem+json")]
[ProducesResponseType(typeof(HouseholdProblemDetails), 503, "application/problem+json")]
[Route("api/vehicle-cost-inputs")]
public sealed class VehicleCostInputsController(IVehicleCostInputStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleCostInputSummary>>(200)]
    public async Task<ActionResult<IReadOnlyList<VehicleCostInputSummary>>> List(CancellationToken ct) =>
        Ok((await store.ListAsync(ct)).Select(HouseholdStoreMapper.ToSummary).ToArray());

    [HttpGet("{vehicleId:guid}")]
    [ProducesResponseType<VehicleCostInputResponse>(200)]
    public async Task<ActionResult<VehicleCostInputResponse>> Get(Guid vehicleId, CancellationToken ct)
    {
        var result = await store.GetAsync(vehicleId, ct)
            ?? throw new CarExpenseCalculator.Infrastructure.Persistence.Households.HouseholdStoreException("vehicleNotFound", "Vehicle no longer exists.", vehicleId);
        return Ok(HouseholdStoreMapper.ToApi(result));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<VehicleCostInputResponse>(201)]
    public async Task<ActionResult<VehicleCostInputResponse>> Create(CreateVehicleCostInputRequest request, CancellationToken ct)
    {
        var result = await store.CreateAsync(HouseholdStoreMapper.Registration(request.RegistrationNumber, "registrationNumber"),
            HouseholdStoreMapper.ToStore(request.Cost, "cost"), ct);
        return CreatedAtAction(nameof(Get), new { vehicleId = result.VehicleId }, HouseholdStoreMapper.ToApi(result));
    }

    [HttpPut("{vehicleId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<VehicleCostInputResponse>(200)]
    public async Task<ActionResult<VehicleCostInputResponse>> Replace(Guid vehicleId, ReplaceVehicleCostInputRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision");
        return Ok(HouseholdStoreMapper.ToApi(await store.ReplaceAsync(vehicleId, request.ExpectedRevision,
            HouseholdStoreMapper.ToStore(request.Cost, "cost"), ct)));
    }

    [HttpDelete("{vehicleId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid vehicleId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired, Range(typeof(long), "1", "9223372036854775807")] long expectedRevision, CancellationToken ct)
    {
        await store.DeleteAsync(vehicleId, expectedRevision, ct);
        return NoContent();
    }
}
