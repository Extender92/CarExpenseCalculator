using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

public interface ISavedCostScenarioStore
{
    Task<SavedCostScenario> CreateAsync(
        RegistrationNumber registrationNumber,
        CostScenario scenario,
        CancellationToken cancellationToken = default);

    Task<SavedCostScenario?> GetAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    Task<SavedCostScenario?> GetByRegistrationNumberAsync(
        RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedCostScenario>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<SavedCostScenario> ReplaceAsync(
        Guid vehicleId,
        long expectedRevision,
        CostScenario scenario,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid vehicleId,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}
