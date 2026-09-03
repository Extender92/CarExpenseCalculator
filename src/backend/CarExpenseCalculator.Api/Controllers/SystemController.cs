using System.Reflection;
using CarExpenseCalculator.Api.Contracts;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(
    HealthCheckService healthCheckService,
    IListingExtractionService extractionService) : ControllerBase
{
    private static readonly TimeSpan ExtractionStatusTimeout = TimeSpan.FromSeconds(2);

    [HttpGet("status")]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        var databaseHealthTask = healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);
        var extractionConfiguredTask = GetExtractionConfiguredAsync(cancellationToken);

        await Task.WhenAll(databaseHealthTask, extractionConfiguredTask);
        var databaseHealth = await databaseHealthTask;
        var extractionConfigured = await extractionConfiguredTask;

        var databaseAvailable = databaseHealth.Status == HealthStatus.Healthy;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";

        return Ok(new SystemStatusResponse(
            databaseAvailable ? "healthy" : "degraded",
            version,
            databaseAvailable ? "available" : "unavailable",
            new FeatureStatusResponse(
                RuleBasedSearch: false,
                UrlAnalysis: true,
                ManualCalculator: true,
                AiReview: false),
            new IntegrationStatusResponse(
                CodexListingExtractionConfigured: extractionConfigured)));
    }

    private async Task<bool> GetExtractionConfiguredAsync(CancellationToken requestCancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestCancellationToken);
        timeout.CancelAfter(ExtractionStatusTimeout);

        try
        {
            var status = await extractionService.GetStatusAsync(timeout.Token);
            return status.Configured;
        }
        catch (OperationCanceledException) when (!requestCancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
