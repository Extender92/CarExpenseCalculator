using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var schemaPath = Path.Combine(
            builder.Environment.ContentRootPath,
            "Schemas",
            "listing-extraction-v2.schema.json");
        var options = new CodexExtractorOptions
        {
            Model = builder.Configuration["CODEX_MODEL"] ?? CodexExtractorOptions.RequiredModel,
            ReasoningEffort = builder.Configuration["CODEX_REASONING_EFFORT"]
                ?? CodexExtractorOptions.RequiredReasoningEffort,
            CodexHome = builder.Configuration["CODEX_HOME"],
            CodexExecutable = builder.Configuration["CODEX_EXECUTABLE"] ?? "codex",
            WorkRoot = builder.Configuration["CODEX_WORK_ROOT"]
                ?? Path.Combine(Path.GetTempPath(), "car-expense-codex-work"),
            SchemaPath = schemaPath,
        };

        builder.Services.ConfigureHttpJsonOptions(jsonOptions =>
        {
            jsonOptions.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            jsonOptions.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            jsonOptions.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IExtractionOutputValidator, ExtractionOutputValidator>();
        builder.Services.AddSingleton<CodexJsonlParser>();
        builder.Services.AddSingleton<ICodexProcessRunner, CodexProcessRunner>();
        builder.Services.AddSingleton<CodexExtractionOrchestrator>();

        var app = builder.Build();

        app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
        app.MapGet(
            "/internal/status",
            async (CodexExtractionOrchestrator orchestrator, CancellationToken cancellationToken) =>
                Results.Ok(await orchestrator.GetStatusAsync(cancellationToken)));
        app.MapPost(
            "/internal/listing-extractions",
            async (
                ListingExtractionRequest request,
                CodexExtractionOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
            {
                var execution = await orchestrator.ExecuteAsync(request, cancellationToken);
                return execution switch
                {
                    CodexExtractionSucceeded success => Results.Ok(success.Response),
                    CodexExtractionFailed failure => MapFailure(failure.Failure),
                    _ => throw new UnreachableException(),
                };
            });

        await app.RunAsync();
    }

    private static IResult MapFailure(CodexExecutionFailure failure)
    {
        var (statusCode, code, title) = failure switch
        {
            CodexExecutionFailure.InvalidRequest => (
                StatusCodes.Status400BadRequest,
                ListingExtractorProblemCodes.InvalidRequest,
                "The listing extraction request is invalid."),
            CodexExecutionFailure.UnsupportedVersion => (
                StatusCodes.Status409Conflict,
                ListingExtractorProblemCodes.UnsupportedVersion,
                "The requested extraction contract version is unsupported."),
            CodexExecutionFailure.NotConfigured => (
                StatusCodes.Status503ServiceUnavailable,
                ListingExtractorProblemCodes.NotConfigured,
                "Codex listing extraction is not configured."),
            CodexExecutionFailure.RateLimited => (
                StatusCodes.Status429TooManyRequests,
                ListingExtractorProblemCodes.RateLimited,
                "Codex listing extraction is rate limited."),
            CodexExecutionFailure.TimedOut => (
                StatusCodes.Status503ServiceUnavailable,
                ListingExtractorProblemCodes.TimedOut,
                "Codex listing extraction timed out."),
            CodexExecutionFailure.ProviderUnavailable => (
                StatusCodes.Status503ServiceUnavailable,
                ListingExtractorProblemCodes.ProviderUnavailable,
                "Codex listing extraction is unavailable."),
            CodexExecutionFailure.InvalidOutput => (
                StatusCodes.Status503ServiceUnavailable,
                ListingExtractorProblemCodes.InvalidOutput,
                "Codex returned an unusable extraction result."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
