using CarExpenseCalculator.Infrastructure.Health;
using CarExpenseCalculator.Infrastructure.Persistence;
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

        services.AddSingleton(new PostgresHealthCheck(connectionString));

        return services;
    }
}
