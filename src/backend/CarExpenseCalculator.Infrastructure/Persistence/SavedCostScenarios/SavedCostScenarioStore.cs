using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

public sealed class SavedCostScenarioStore(
    CarExpenseDbContext dbContext,
    CostScenarioCalculator calculator,
    TimeProvider timeProvider) : ISavedCostScenarioStore
{
    internal const int CurrentCalculationVersion = 1;
    internal const int CurrentResultSchemaVersion = 1;

    public async Task<SavedCostScenario> CreateAsync(
        RegistrationNumber registrationNumber,
        CostScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);
        ArgumentNullException.ThrowIfNull(scenario);

        var result = calculator.Calculate(scenario);
        var existingId = await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.RegistrationNumber == registrationNumber.Value)
            .Select(vehicle => (Guid?)vehicle.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            throw new RegistrationNumberConflictException(registrationNumber, existingId);
        }

        var now = timeProvider.GetUtcNow();
        var vehicle = CreateVehicleEntity(registrationNumber, scenario, result, now);
        dbContext.Vehicles.Add(vehicle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsRegistrationNumberConflict(exception))
        {
            throw new RegistrationNumberConflictException(registrationNumber, innerException: exception);
        }

        return ToSavedScenario(vehicle);
    }

    public async Task<SavedCostScenario?> GetAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await CompleteQuery(tracking: false)
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken);
        return vehicle is null ? null : ToSavedScenario(vehicle);
    }

    public async Task<SavedCostScenario?> GetByRegistrationNumberAsync(
        RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);

        var vehicle = await CompleteQuery(tracking: false)
            .SingleOrDefaultAsync(
                entity => entity.RegistrationNumber == registrationNumber.Value,
                cancellationToken);
        return vehicle is null ? null : ToSavedScenario(vehicle);
    }

    public async Task<IReadOnlyList<SavedCostScenario>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var vehicles = await CompleteQuery(tracking: false)
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .ThenBy(entity => entity.Id)
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(vehicles.Select(ToSavedScenario).ToArray());
    }

    public async Task<SavedCostScenario> ReplaceAsync(
        Guid vehicleId,
        long expectedRevision,
        CostScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var result = calculator.Calculate(scenario);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vehicle = await CompleteQuery(tracking: true)
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken)
            ?? throw new SavedCostScenarioNotFoundException(vehicleId);
        EnsureExpectedRevision(vehicle, expectedRevision);

        try
        {
            var savedScenario = vehicle.Scenario;
            dbContext.RemoveRange(savedScenario.EnergySources);
            dbContext.RemoveRange(savedScenario.OtherRecurringCosts);
            dbContext.RemoveRange(savedScenario.OtherOneTimeCosts);
            await dbContext.SaveChangesAsync(cancellationToken);
            savedScenario.EnergySources.Clear();
            savedScenario.OtherRecurringCosts.Clear();
            savedScenario.OtherOneTimeCosts.Clear();

            var now = timeProvider.GetUtcNow();
            vehicle.VehicleLabel = NormalizeOptionalLabel(scenario.VehicleLabel);
            vehicle.Revision++;
            vehicle.UpdatedAtUtc = now;
            ApplyScenario(savedScenario, scenario, result, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            var actualRevision = await FindActualRevisionAsync(vehicleId, cancellationToken);
            throw new SavedCostScenarioConcurrencyException(
                vehicleId,
                expectedRevision,
                actualRevision,
                exception);
        }

        return ToSavedScenario(vehicle);
    }

    public async Task DeleteAsync(
        Guid vehicleId,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken)
            ?? throw new SavedCostScenarioNotFoundException(vehicleId);
        EnsureExpectedRevision(vehicle, expectedRevision);
        dbContext.Vehicles.Remove(vehicle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var actualRevision = await FindActualRevisionAsync(vehicleId, cancellationToken);
            throw new SavedCostScenarioConcurrencyException(
                vehicleId,
                expectedRevision,
                actualRevision,
                exception);
        }
    }

    private IQueryable<VehicleEntity> CompleteQuery(bool tracking)
    {
        var query = dbContext.Vehicles
            .Include(entity => entity.Scenario)
                .ThenInclude(entity => entity.EnergySources)
            .Include(entity => entity.Scenario)
                .ThenInclude(entity => entity.OtherRecurringCosts)
            .Include(entity => entity.Scenario)
                .ThenInclude(entity => entity.OtherOneTimeCosts)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTrackingWithIdentityResolution();
    }

    private static VehicleEntity CreateVehicleEntity(
        RegistrationNumber registrationNumber,
        CostScenario scenario,
        CostCalculationResult result,
        DateTimeOffset now)
    {
        var vehicle = new VehicleEntity
        {
            Id = Guid.CreateVersion7(now),
            RegistrationNumber = registrationNumber.Value,
            VehicleLabel = NormalizeOptionalLabel(scenario.VehicleLabel),
            Revision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Scenario = null!,
        };
        var savedScenario = new SavedCostScenarioEntity
        {
            Id = Guid.CreateVersion7(now),
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            ResultSnapshotJson = string.Empty,
        };
        vehicle.Scenario = savedScenario;
        ApplyScenario(savedScenario, scenario, result, now);
        return vehicle;
    }

    private static void ApplyScenario(
        SavedCostScenarioEntity entity,
        CostScenario scenario,
        CostCalculationResult result,
        DateTimeOffset calculatedAtUtc)
    {
        entity.CalculationPeriodMonths = scenario.CalculationPeriodMonths;
        entity.PurchasePriceSek = scenario.PurchasePriceSek;
        entity.ExpectedResidualValueSek = scenario.ExpectedResidualValueSek;
        entity.AnnualDistanceKilometres = scenario.AnnualDistanceKilometres;
        entity.FinancingDownPaymentSek = scenario.Financing?.DownPaymentSek;
        entity.FinancingAnnualNominalInterestRatePercent =
            scenario.Financing?.AnnualNominalInterestRatePercent;
        entity.FinancingTermMonths = scenario.Financing?.TermMonths;
        ApplyRecurringCost(
            scenario.VehicleTax,
            (amount, cadence) =>
            {
                entity.VehicleTaxAmountSek = amount;
                entity.VehicleTaxCadence = cadence;
            });
        ApplyRecurringCost(
            scenario.Insurance,
            (amount, cadence) =>
            {
                entity.InsuranceAmountSek = amount;
                entity.InsuranceCadence = cadence;
            });
        ApplyRecurringCost(
            scenario.MaintenanceAndRepairs,
            (amount, cadence) =>
            {
                entity.MaintenanceAndRepairsAmountSek = amount;
                entity.MaintenanceAndRepairsCadence = cadence;
            });
        entity.CalculationVersion = CurrentCalculationVersion;
        entity.ResultSchemaVersion = CurrentResultSchemaVersion;
        entity.ResultSnapshotJson = CostCalculationSnapshot.Serialize(result);
        entity.CalculatedAtUtc = calculatedAtUtc;

        for (var position = 0; position < scenario.EnergySources.Count; position++)
        {
            var source = scenario.EnergySources[position];
            entity.EnergySources.Add(new ScenarioEnergySourceEntity
            {
                Id = Guid.CreateVersion7(calculatedAtUtc),
                ScenarioId = entity.Id,
                Scenario = entity,
                Position = position,
                Label = source.Label.Trim(),
                Unit = source.Unit,
                ConsumptionPer100Kilometres = source.ConsumptionPer100Kilometres,
                PricePerUnitSek = source.PricePerUnitSek,
                DistanceSharePercent = source.DistanceSharePercent,
            });
        }

        for (var position = 0; position < scenario.OtherRecurringCosts.Count; position++)
        {
            var cost = scenario.OtherRecurringCosts[position];
            entity.OtherRecurringCosts.Add(new ScenarioRecurringCostEntity
            {
                Id = Guid.CreateVersion7(calculatedAtUtc),
                ScenarioId = entity.Id,
                Scenario = entity,
                Position = position,
                Label = cost.Label.Trim(),
                AmountSek = cost.AmountSek,
                Cadence = cost.Cadence,
            });
        }

        for (var position = 0; position < scenario.OtherOneTimeCosts.Count; position++)
        {
            var cost = scenario.OtherOneTimeCosts[position];
            entity.OtherOneTimeCosts.Add(new ScenarioOneTimeCostEntity
            {
                Id = Guid.CreateVersion7(calculatedAtUtc),
                ScenarioId = entity.Id,
                Scenario = entity,
                Position = position,
                Label = cost.Label.Trim(),
                AmountSek = cost.AmountSek,
            });
        }
    }

    private static void ApplyRecurringCost(
        RecurringCost? cost,
        Action<decimal?, RecurringCostCadence?> assign)
    {
        assign(cost?.AmountSek, cost?.Cadence);
    }

    private static SavedCostScenario ToSavedScenario(VehicleEntity vehicle)
    {
        var entity = vehicle.Scenario;
        if (entity.CalculationVersion != CurrentCalculationVersion
            || entity.ResultSchemaVersion != CurrentResultSchemaVersion)
        {
            throw new UnsupportedSavedCostScenarioVersionException(
                vehicle.Id,
                entity.CalculationVersion,
                entity.ResultSchemaVersion);
        }

        var financing = entity.FinancingDownPaymentSek is null
            ? null
            : new FinancingTerms(
                entity.FinancingDownPaymentSek.Value,
                entity.FinancingAnnualNominalInterestRatePercent!.Value,
                entity.FinancingTermMonths!.Value);
        var scenario = new CostScenario(
            vehicle.VehicleLabel,
            entity.CalculationPeriodMonths,
            entity.PurchasePriceSek,
            entity.ExpectedResidualValueSek,
            entity.AnnualDistanceKilometres,
            financing,
            entity.EnergySources
                .OrderBy(source => source.Position)
                .Select(source => new EnergySource(
                    source.Label,
                    source.Unit,
                    source.ConsumptionPer100Kilometres,
                    source.PricePerUnitSek,
                    source.DistanceSharePercent)),
            ToRecurringCost(entity.VehicleTaxAmountSek, entity.VehicleTaxCadence),
            ToRecurringCost(entity.InsuranceAmountSek, entity.InsuranceCadence),
            ToRecurringCost(
                entity.MaintenanceAndRepairsAmountSek,
                entity.MaintenanceAndRepairsCadence),
            entity.OtherRecurringCosts
                .OrderBy(cost => cost.Position)
                .Select(cost => new NamedRecurringCost(cost.Label, cost.AmountSek, cost.Cadence)),
            entity.OtherOneTimeCosts
                .OrderBy(cost => cost.Position)
                .Select(cost => new OneTimeCost(cost.Label, cost.AmountSek)));

        return new SavedCostScenario(
            vehicle.Id,
            RegistrationNumber.Parse(vehicle.RegistrationNumber),
            scenario,
            CostCalculationSnapshot.Deserialize(entity.ResultSnapshotJson),
            entity.CalculationVersion,
            entity.ResultSchemaVersion,
            vehicle.Revision,
            vehicle.CreatedAtUtc,
            vehicle.UpdatedAtUtc,
            entity.CalculatedAtUtc);
    }

    private static RecurringCost? ToRecurringCost(
        decimal? amount,
        RecurringCostCadence? cadence)
    {
        return amount is null ? null : new RecurringCost(amount.Value, cadence!.Value);
    }

    private static string? NormalizeOptionalLabel(string? label) => label?.Trim();

    private static void EnsureExpectedRevision(VehicleEntity vehicle, long expectedRevision)
    {
        if (vehicle.Revision != expectedRevision)
        {
            throw new SavedCostScenarioConcurrencyException(
                vehicle.Id,
                expectedRevision,
                vehicle.Revision);
        }
    }

    private async Task<long?> FindActualRevisionAsync(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        return await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => (long?)vehicle.Revision)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool IsRegistrationNumberConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_vehicles_registration_number",
        };
    }
}
