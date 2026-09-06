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
[Route("api/household-profile")]
public sealed class HouseholdProfileController(IHouseholdProfileStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HouseholdProfileResponse>(200)]
    public async Task<ActionResult<HouseholdProfileResponse>> Get(CancellationToken ct)
    {
        var result = await store.GetAsync(ct);
        if (result.Input is null) throw new HouseholdStoreException("profileNotFound", "The household profile has not been initialized.", actualRevision: result.Revision);
        return Ok(HouseholdStoreMapper.ToApi(result));
    }

    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<HouseholdProfileResponse>(200)]
    public async Task<ActionResult<HouseholdProfileResponse>> Save(SaveHouseholdProfileRequest request, CancellationToken ct)
    {
        HouseholdStoreMapper.Revision(request.ExpectedRevision, "expectedRevision", allowZero: true);
        var input = HouseholdInputMapper.ToCore(request.Input, "input");
        var errors = CarExpenseCalculator.Core.Households.HouseholdInputValidator.ValidateProfile(input);
        if (errors.Count > 0) throw new CarExpenseCalculator.Core.Households.HouseholdInputValidationException(
            errors.Select(x => x with { Path = "input" + x.Path["profile".Length..] }).ToArray());
        return Ok(HouseholdStoreMapper.ToApi(await store.SaveAsync(input, request.ExpectedRevision, ct)));
    }
}
