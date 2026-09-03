using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Health;
using CarExpenseCalculator.Infrastructure.ListingExtraction;
using CarExpenseCalculator.Infrastructure.Persistence;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarExpenseCalculator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres must be configured.");
        }

        services.AddDbContext<CarExpenseDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.SetPostgresVersion(18, 0)));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ListingDraftProcessor>();
        services.AddScoped<ISavedCostScenarioStore, SavedCostScenarioStore>();

        var extractorAddress = configuration["CodexExtraction:BaseUrl"]
            ?? "http://codex-extractor:8080";
        services.AddHttpClient<IListingExtractionService, CodexListingExtractionService>(client =>
        {
            client.BaseAddress = new Uri(extractorAddress, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(65);
        });

        services.AddSingleton(new PostgresHealthCheck(connectionString));

        return services;
    }
}
