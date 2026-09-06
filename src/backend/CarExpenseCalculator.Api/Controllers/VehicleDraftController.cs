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
[Route("api/vehicle-draft")]
public sealed class VehicleDraftController(ISharedVehicleDraftStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<VehicleDraftResponse>(200)]
    public async Task<ActionResult<VehicleDraftResponse>> Get(CancellationToken ct) =>
        Ok(HouseholdStoreMapper.ToApi(await store.GetAsync(ct)));

    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<VehicleDraftResponse>(200)]
    public async Task<ActionResult<VehicleDraftResponse>> Save(SaveVehicleDraftRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision", allowZero: true);
        return Ok(HouseholdStoreMapper.ToApi(await store.SaveAsync(HouseholdStoreMapper.ToStore(request.Input),
            request.ExpectedRevision, request.ReplaceExisting, ct)));
    }

    [HttpDelete]
    [ProducesResponseType<VehicleDraftResponse>(200)]
    public async Task<ActionResult<VehicleDraftResponse>> Delete(
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired, Range(typeof(long), "0", "9223372036854775807")] long expectedRevision, CancellationToken ct) =>
        Ok(HouseholdStoreMapper.ToApi(await store.DeleteAsync(expectedRevision, ct)));

    [HttpPost("adopt")]
    [Consumes("application/json")]
    [ProducesResponseType<VehicleCostInputResponse>(200)]
    public async Task<ActionResult<VehicleCostInputResponse>> Adopt(AdoptVehicleDraftRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision", allowZero: true);
        return Ok(HouseholdStoreMapper.ToApi(await store.AdoptAsync(request.ExpectedRevision, ct)));
    }
}
