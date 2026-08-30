using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Health;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Infrastructure;
using CarExpenseCalculator.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<CostScenarioCalculator>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

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
