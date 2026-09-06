using CarExpenseCalculator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class SavedCostScenarioApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("car_expense_calculator")
        .WithUsername("car_expense_app")
        .WithPassword("integration_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarExpenseDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarExpenseDbContext>();
        // TRUNCATE CASCADE also removes the draft singleton through its optional vehicle FK.
        await dbContext.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE vehicles, household_state CASCADE;
            INSERT INTO household_state (id, profile_revision, transition_revision, schema_version)
                VALUES (1, 0, 0, 1);
            INSERT INTO vehicle_draft (id, revision, schema_version) VALUES (1, 0, 1);
            """);
    }

    public async Task ExecuteDatabaseCommandAsync(string sql)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarExpenseDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    public new Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
    }
}
