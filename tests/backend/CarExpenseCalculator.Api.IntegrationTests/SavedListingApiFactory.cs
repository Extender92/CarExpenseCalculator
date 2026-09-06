using CarExpenseCalculator.Infrastructure.ListingExtraction;
using CarExpenseCalculator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class SavedListingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("car_expense_calculator")
        .WithUsername("car_expense_app")
        .WithPassword("integration_test_password")
        .Build();

    public FakeListingExtractionService ExtractionService { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarExpenseDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        ExtractionService.Reset();
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
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IListingExtractionService>();
            services.AddSingleton(ExtractionService);
            services.AddSingleton<IListingExtractionService>(ExtractionService);
        });
    }
}
