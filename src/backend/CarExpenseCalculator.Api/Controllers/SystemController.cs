using System.Reflection;
using CarExpenseCalculator.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(HealthCheckService healthCheckService) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        var databaseHealth = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);

        var databaseAvailable = databaseHealth.Status == HealthStatus.Healthy;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";

        return Ok(new SystemStatusResponse(
            databaseAvailable ? "healthy" : "degraded",
            version,
            databaseAvailable ? "available" : "unavailable",
            new FeatureStatusResponse(
                RuleBasedSearch: false,
                UrlAnalysis: false,
                ManualCalculator: false,
                AiReview: false)));
    }
}
