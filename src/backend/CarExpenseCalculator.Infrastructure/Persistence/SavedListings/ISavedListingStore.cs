using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

public interface ISavedListingStore
{
    Task<SavedListing> CreateAsync(
        RegistrationNumber registrationNumber,
        SavedListingInput input,
        CancellationToken cancellationToken = default);

    Task<SavedListing?> GetAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    Task<SavedListing?> GetByRegistrationNumberAsync(
        RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedListing>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<SavedListing> ReplaceAsync(
        Guid vehicleId,
        long expectedRevision,
        SavedListingInput input,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid vehicleId,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}
