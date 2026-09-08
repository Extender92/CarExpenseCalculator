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
[Route("api/rule-profile")]
public sealed class RuleProfileController(IRuleProfileStore store) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RuleProfileResponse>(200)]
    public async Task<ActionResult<RuleProfileResponse>> Get(CancellationToken ct)
    {
        var result = await store.GetAsync(ct);
        if (result.Input is null) throw new ComparisonStoreException("ruleProfileNotFound", "No rule profile has been saved.", actualRevision: result.Revision);
        return Ok(new RuleProfileResponse(ComparisonInputMapper.ToApi(result.Input), result.Revision));
    }
    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<RuleProfileResponse>(200)]
    public async Task<ActionResult<RuleProfileResponse>> Save(SaveRuleProfileRequest request, CancellationToken ct)
    {
        var result = await store.SaveAsync(ComparisonInputMapper.NormalizeRules(request.Input, "input"), request.ExpectedRevision, ct);
        return Ok(new RuleProfileResponse(ComparisonInputMapper.ToApi(result.Input!), result.Revision));
    }
}
