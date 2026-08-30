using Microsoft.EntityFrameworkCore;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

namespace CarExpenseCalculator.Infrastructure.Persistence;

public sealed class CarExpenseDbContext(DbContextOptions<CarExpenseDbContext> options)
    : DbContext(options)
{
    internal DbSet<VehicleEntity> Vehicles => Set<VehicleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarExpenseDbContext).Assembly);
    }
}
