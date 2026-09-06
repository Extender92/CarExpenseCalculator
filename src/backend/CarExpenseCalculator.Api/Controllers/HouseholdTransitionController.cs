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
[Route("api/household-transition")]
public sealed class HouseholdTransitionController(IHouseholdTransitionStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HouseholdTransitionResponse>(200)]
    public async Task<ActionResult<HouseholdTransitionResponse>> Get(CancellationToken ct) =>
        Ok(HouseholdStoreMapper.ToApi(await store.GetAsync(ct)));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<HouseholdTransitionResponse>(200)]
    public async Task<ActionResult<HouseholdTransitionResponse>> Confirm(ConfirmHouseholdTransitionRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedProfileRevision, "expectedProfileRevision", allowZero: true);
        HouseholdStoreMapper.Revision(request.ExpectedTransitionRevision, "expectedTransitionRevision", allowZero: true);
        var vehicles = request.Vehicles.Select((x, i) =>
        {
            if (x is null) throw HouseholdInputMapper.Error($"vehicles[{i}]", "missingItem", "A transition vehicle cannot be null.");
            HouseholdStoreMapper.Revision(x.ExpectedRevision, $"vehicles[{i}].expectedRevision");
            return new CarExpenseCalculator.Infrastructure.Persistence.Households.VehicleTransitionWrite(
                x.VehicleId, x.ExpectedRevision, HouseholdStoreMapper.ToStore(x.Cost, $"vehicles[{i}].cost"));
        }).ToArray();
        return Ok(HouseholdStoreMapper.ToApi(await store.ConfirmAsync(HouseholdInputMapper.ToCore(request.Profile, "profile"),
            request.ExpectedProfileRevision, request.ExpectedTransitionRevision, vehicles, ct)));
    }
}
