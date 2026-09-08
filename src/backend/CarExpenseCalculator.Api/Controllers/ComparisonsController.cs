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
[Route("api/comparisons")]
public sealed class ComparisonsController(TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("preview")]
    [Consumes("application/json")]
    [ProducesResponseType<ComparisonPreviewResponse>(200)]
    public async Task<ActionResult<ComparisonPreviewResponse>> Preview(ComparisonPreviewRequest request, CancellationToken ct) =>
        Ok(await new Comparisons.ComparisonPreviewService(timeProvider).PreviewAsync(request, HttpContext.RequestServices, ct));
}
