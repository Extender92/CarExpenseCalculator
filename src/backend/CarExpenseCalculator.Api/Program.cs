using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Health;
using CarExpenseCalculator.Api.Households;
using CarExpenseCalculator.Core.Households;
using Microsoft.AspNetCore.Mvc;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Infrastructure;
using CarExpenseCalculator.Infrastructure.Health;
using CarExpenseCalculator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi(options => options.CreateSchemaReferenceId = type =>
    // Nullable<TEnum> must not share a component with required TEnum values.
    // Otherwise one optional comparison choice changes existing listing contracts.
    Nullable.GetUnderlyingType(type.Type) is { IsEnum: true } underlying &&
        underlying.Name is "Transmission" or "Drivetrain" or "BodyType" or "VehicleInputState" or "ServiceDocumentationStatus"
        ? "Nullable" + underlying.Name
        : Microsoft.AspNetCore.OpenApi.OpenApiOptions.CreateDefaultSchemaReferenceId(type));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<CostScenarioCalculator>();
builder.Services.AddSingleton<HouseholdCostCalculator>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var original = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        if (CarExpenseCalculator.Api.Comparisons.ComparisonHttpBoundary.IsComparison(context.HttpContext.Request.Path))
        {
            var comparisonErrors = context.ModelState.SelectMany(pair => pair.Value!.Errors.Select(error =>
                new CarExpenseCalculator.Api.Contracts.Comparisons.ComparisonFieldError(pair.Key, "invalidInput",
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Input cannot be read." : error.ErrorMessage))).ToArray();
            return new BadRequestObjectResult(CarExpenseCalculator.Api.Comparisons.ComparisonHttpBoundary.Validation(context.HttpContext, comparisonErrors))
                { ContentTypes = { "application/problem+json" } };
        }
        if (!HouseholdHttpBoundary.IsHousehold(context.HttpContext.Request.Path)) return original(context);
        var errors = context.ModelState.SelectMany(pair => pair.Value!.Errors.Select(error =>
            new CarExpenseCalculator.Api.Contracts.Households.HouseholdInputError(pair.Key, "invalidInput",
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Input cannot be read." : error.ErrorMessage))).ToArray();
        return new BadRequestObjectResult(HouseholdHttpBoundary.Validation(context.HttpContext, errors))
            { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();

if (args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length > 2)
    {
        throw new ArgumentException("Usage: migrate [target-migration]");
    }

    await DatabaseMigrationRunner.RunAsync(app.Services, args.ElementAtOrDefault(1));
    return;
}

app.UseExceptionHandler();
app.UseMiddleware<HouseholdHttpBoundary>();
app.UseMiddleware<CarExpenseCalculator.Api.Comparisons.ComparisonHttpBoundary>();

app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapControllers();

app.MapHealthChecks(
    "/api/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthResponseWriter.WriteAsync,
    });

app.MapHealthChecks(
    "/api/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthResponseWriter.WriteAsync,
    });

app.Run();

public partial class Program;
