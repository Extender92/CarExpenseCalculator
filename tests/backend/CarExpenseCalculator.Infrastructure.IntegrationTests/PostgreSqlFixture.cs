using CarExpenseCalculator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL persistence";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("car_expense_calculator")
        .WithUsername("car_expense_app")
        .WithPassword("integration_test_password")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public CarExpenseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CarExpenseDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.SetPostgresVersion(18, 0))
            .Options;
        return new CarExpenseDbContext(options);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        await dbContext.Database.MigrateAsync();
    }
}
