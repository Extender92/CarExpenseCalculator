using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarExpenseCalculator.Infrastructure.Persistence;

public static class DatabaseMigrationRunner
{
    public static async Task RunAsync(
        IServiceProvider services,
        string? targetMigration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarExpenseDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseMigrationRunner));

        if (string.IsNullOrWhiteSpace(targetMigration))
        {
            logger.LogInformation("Applying all pending database migrations.");
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Migrating database to explicit target {TargetMigration}.",
                targetMigration);
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(targetMigration, cancellationToken);
        }

        logger.LogInformation("Database migration completed successfully.");
    }
}
