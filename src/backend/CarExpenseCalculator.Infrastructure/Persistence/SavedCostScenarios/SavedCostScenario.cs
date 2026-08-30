using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

public sealed record SavedCostScenario(
    Guid VehicleId,
    RegistrationNumber RegistrationNumber,
    CostScenario Scenario,
    CostCalculationResult Result,
    int CalculationVersion,
    int ResultSchemaVersion,
    long Revision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset CalculatedAtUtc);

public sealed class RegistrationNumberConflictException : Exception
{
    public RegistrationNumberConflictException(
        RegistrationNumber registrationNumber,
        Guid? existingVehicleId = null,
        Exception? innerException = null)
        : base(
            $"A saved vehicle with registration number '{registrationNumber.Value}' already exists.",
            innerException)
    {
        RegistrationNumber = registrationNumber;
        ExistingVehicleId = existingVehicleId;
    }

    public RegistrationNumber RegistrationNumber { get; }

    public Guid? ExistingVehicleId { get; }
}

public sealed class SavedCostScenarioNotFoundException : Exception
{
    public SavedCostScenarioNotFoundException(Guid vehicleId)
        : base($"Saved vehicle '{vehicleId}' was not found.")
    {
        VehicleId = vehicleId;
    }

    public Guid VehicleId { get; }
}

public sealed class SavedCostScenarioConcurrencyException : Exception
{
    public SavedCostScenarioConcurrencyException(
        Guid vehicleId,
        long expectedRevision,
        long? actualRevision,
        Exception? innerException = null)
        : base(
            $"Saved vehicle '{vehicleId}' no longer has expected revision {expectedRevision}.",
            innerException)
    {
        VehicleId = vehicleId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public Guid VehicleId { get; }

    public long ExpectedRevision { get; }

    public long? ActualRevision { get; }
}

public sealed class UnsupportedSavedCostScenarioVersionException : Exception
{
    public UnsupportedSavedCostScenarioVersionException(
        Guid vehicleId,
        int calculationVersion,
        int resultSchemaVersion)
        : base(
            $"Saved vehicle '{vehicleId}' uses unsupported calculation version "
            + $"{calculationVersion} or result schema version {resultSchemaVersion}.")
    {
        VehicleId = vehicleId;
        CalculationVersion = calculationVersion;
        ResultSchemaVersion = resultSchemaVersion;
    }

    public Guid VehicleId { get; }

    public int CalculationVersion { get; }

    public int ResultSchemaVersion { get; }
}
