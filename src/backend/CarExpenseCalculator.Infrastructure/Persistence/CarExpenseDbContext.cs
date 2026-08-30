using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Infrastructure.Persistence;

public sealed class CarExpenseDbContext(DbContextOptions<CarExpenseDbContext> options)
    : DbContext(options)
{
}
