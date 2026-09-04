using Microsoft.EntityFrameworkCore;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence;

public sealed class CarExpenseDbContext(DbContextOptions<CarExpenseDbContext> options)
    : DbContext(options)
{
    internal DbSet<VehicleEntity> Vehicles => Set<VehicleEntity>();

    internal DbSet<VehicleListingEntity> VehicleListings => Set<VehicleListingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarExpenseDbContext).Assembly);
    }
}
