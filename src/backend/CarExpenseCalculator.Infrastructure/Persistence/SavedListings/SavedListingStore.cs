using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Extraction.Contracts;
using CarExpenseCalculator.Infrastructure.Persistence.Vehicles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

public sealed class SavedListingStore(
    CarExpenseDbContext dbContext,
    ListingDraftProcessor processor,
    TimeProvider timeProvider) : ISavedListingStore
{
    internal const int CurrentListingSchemaVersion = 1;

    public async Task<SavedListing> CreateAsync(
        RegistrationNumber registrationNumber,
        SavedListingInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);
        ArgumentNullException.ThrowIfNull(input);

        var prepared = Prepare(registrationNumber, input);
        var existing = await FindIdentityAsync(registrationNumber, cancellationToken);
        if (existing is not null)
        {
            throw new SavedListingRegistrationConflictException(
                registrationNumber,
                existing.Value.Id,
                existing.Value.Revision);
        }

        var now = timeProvider.GetUtcNow();
        var vehicle = new VehicleEntity
        {
            Id = Guid.CreateVersion7(now),
            RegistrationNumber = registrationNumber.Value,
            VehicleLabel = prepared.Result.Listing.VehicleLabel?.Value,
            Revision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var listing = CreateListing(vehicle, prepared, now);
        vehicle.Listing = listing;
        dbContext.Vehicles.Add(vehicle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsRegistrationNumberConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var conflict = await FindIdentityAsync(registrationNumber, cancellationToken);
            throw new SavedListingRegistrationConflictException(
                registrationNumber,
                conflict?.Id,
                conflict?.Revision,
                exception);
        }

        return ToSavedListing(vehicle);
    }

    public async Task<SavedListing?> GetAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await CompleteQuery(tracking: false)
            .Where(entity => entity.Listing != null)
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken);
        return vehicle is null ? null : ToSavedListing(vehicle);
    }

    public async Task<SavedListing?> GetByRegistrationNumberAsync(
        RegistrationNumber registrationNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);

        var vehicle = await CompleteQuery(tracking: false)
            .Where(entity => entity.Listing != null)
            .SingleOrDefaultAsync(
                entity => entity.RegistrationNumber == registrationNumber.Value,
                cancellationToken);
        return vehicle is null ? null : ToSavedListing(vehicle);
    }

    public async Task<IReadOnlyList<SavedListing>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var vehicles = await CompleteQuery(tracking: false)
            .Where(entity => entity.Listing != null)
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .ThenBy(entity => entity.Id)
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(vehicles.Select(ToSavedListing).ToArray());
    }

    public async Task<SavedListing> ReplaceAsync(
        Guid vehicleId,
        long expectedRevision,
        SavedListingInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var vehicle = await CompleteQuery(tracking: true)
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken)
            ?? throw new SavedListingNotFoundException(vehicleId);
        EnsureExpectedRevision(vehicle, expectedRevision);
        var registrationNumber = RegistrationNumber.Parse(vehicle.RegistrationNumber);
        var prepared = Prepare(registrationNumber, input);

        try
        {
            var now = timeProvider.GetUtcNow();
            if (vehicle.Listing is null)
            {
                vehicle.Listing = CreateListing(vehicle, prepared, now);
            }
            else
            {
                var listing = vehicle.Listing;
                dbContext.RemoveRange(listing.Sources);
                dbContext.RemoveRange(listing.Equipment);
                await dbContext.SaveChangesAsync(cancellationToken);
                listing.Sources.Clear();
                listing.Equipment.Clear();
                listing.ListingVersion++;
                listing.UpdatedAtUtc = now;
                ApplyListing(listing, prepared, now);
            }

            vehicle.VehicleLabel = prepared.Result.Listing.VehicleLabel?.Value;
            vehicle.Revision++;
            vehicle.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            var actualRevision = await FindActualRevisionAsync(vehicleId, cancellationToken);
            throw new SavedListingConcurrencyException(
                vehicleId,
                expectedRevision,
                actualRevision,
                exception);
        }

        return ToSavedListing(vehicle);
    }

    public async Task DeleteAsync(
        Guid vehicleId,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles
            .Where(entity => entity.Listing != null)
            .SingleOrDefaultAsync(entity => entity.Id == vehicleId, cancellationToken)
            ?? throw new SavedListingNotFoundException(vehicleId);
        EnsureExpectedRevision(vehicle, expectedRevision);
        dbContext.Vehicles.Remove(vehicle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var actualRevision = await FindActualRevisionAsync(vehicleId, cancellationToken);
            throw new SavedListingConcurrencyException(
                vehicleId,
                expectedRevision,
                actualRevision,
                exception);
        }
    }

    private PreparedListing Prepare(RegistrationNumber registrationNumber, SavedListingInput input)
    {
        var submittedUrl = ListingUrl.Parse(input.SubmittedUrl);
        var errors = new List<ListingValidationError>();
        var requestedModel = NormalizeMetadata(input, errors);
        var listing = input.Listing;

        if (listing.RegistrationNumber is null)
        {
            listing = listing with
            {
                RegistrationNumber = new SourcedValue<RegistrationNumber>(
                    registrationNumber,
                    ManualProvenance(submittedUrl)),
            };
        }
        else if (listing.RegistrationNumber.Value != registrationNumber)
        {
            errors.Add(new ListingValidationError(
                "registrationNumber.value",
                "Listing registration number must match the saved vehicle registration number."));
        }

        if (errors.Count > 0)
        {
            throw new ListingValidationException(errors);
        }

        var result = processor.ProcessReviewed(submittedUrl, input.Sources, listing);
        if (HasAiProvenance(result.Listing)
            && requestedModel is null)
        {
            throw new ListingValidationException(
            [
                new ListingValidationError(
                    "requestedModel",
                    "Extraction metadata is required when AI provenance is present."),
            ]);
        }

        return new PreparedListing(
            input.SubmittedUrl.Trim(),
            submittedUrl,
            input.AnalyzedAtUtc.ToUniversalTime(),
            requestedModel,
            input.PromptVersion,
            input.ExtractionSchemaVersion,
            result);
    }

    private static string? NormalizeMetadata(
        SavedListingInput input,
        ICollection<ListingValidationError> errors)
    {
        var hasModel = input.RequestedModel is not null;
        var hasPromptVersion = input.PromptVersion is not null;
        var hasSchemaVersion = input.ExtractionSchemaVersion is not null;
        if (!hasModel && !hasPromptVersion && !hasSchemaVersion)
        {
            return null;
        }

        if (!(hasModel && hasPromptVersion && hasSchemaVersion))
        {
            errors.Add(new ListingValidationError(
                "requestedModel",
                "Requested model, prompt version, and extraction schema version must all be supplied together."));
            return input.RequestedModel?.Trim();
        }

        var model = input.RequestedModel!.Trim();
        if (model.Length is < 1 or > 100)
        {
            errors.Add(new ListingValidationError(
                "requestedModel",
                "Requested model must contain 1 through 100 characters after trimming."));
        }

        if (input.PromptVersion != ListingExtractionContractVersions.Prompt)
        {
            errors.Add(new ListingValidationError(
                "promptVersion",
                $"Prompt version must be {ListingExtractionContractVersions.Prompt}."));
        }

        if (input.ExtractionSchemaVersion != ListingExtractionContractVersions.Schema)
        {
            errors.Add(new ListingValidationError(
                "schemaVersion",
                $"Extraction schema version must be {ListingExtractionContractVersions.Schema}."));
        }

        return model;
    }

    private IQueryable<VehicleEntity> CompleteQuery(bool tracking)
    {
        var query = dbContext.Vehicles
            .Include(entity => entity.Listing)
                .ThenInclude(entity => entity!.Sources)
            .Include(entity => entity.Listing)
                .ThenInclude(entity => entity!.Equipment)
            .Include(entity => entity.Scenario)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTrackingWithIdentityResolution();
    }

    private static VehicleListingEntity CreateListing(
        VehicleEntity vehicle,
        PreparedListing prepared,
        DateTimeOffset now)
    {
        var listing = new VehicleListingEntity
        {
            Id = Guid.CreateVersion7(now),
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            ListingVersion = 1,
            ListingSchemaVersion = CurrentListingSchemaVersion,
            SubmittedUrl = prepared.SubmittedUrl,
            NormalizedUrl = prepared.NormalizedUrl.Value,
            MissingFields = [],
            FieldProvenanceJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        ApplyListing(listing, prepared, now);
        return listing;
    }

    private static void ApplyListing(
        VehicleListingEntity entity,
        PreparedListing prepared,
        DateTimeOffset now)
    {
        var listing = prepared.Result.Listing;
        entity.SubmittedUrl = prepared.SubmittedUrl;
        entity.NormalizedUrl = prepared.NormalizedUrl.Value;
        entity.Status = prepared.Result.Status;
        entity.MissingFields = prepared.Result.MissingFields.Select(value => value.ToString()).ToArray();
        entity.RequestedModel = prepared.RequestedModel;
        entity.PromptVersion = prepared.PromptVersion;
        entity.ExtractionSchemaVersion = prepared.ExtractionSchemaVersion;
        entity.AnalyzedAtUtc = prepared.AnalyzedAtUtc;
        entity.Make = listing.Make?.Value;
        entity.Model = listing.Model?.Value;
        entity.Variant = listing.Variant?.Value;
        entity.ModelYear = listing.ModelYear?.Value;
        entity.Vin = listing.Vin?.Value;
        entity.PriceSek = listing.PriceSek?.Value;
        entity.OdometerKilometres = listing.OdometerKilometres?.Value;
        entity.SellerType = listing.SellerType?.Value;
        entity.Locality = listing.Locality?.Value;
        entity.County = listing.County?.Value;
        entity.PublishedDate = listing.PublishedDate?.Value;
        entity.AdvertisedUpdatedDate = listing.UpdatedDate?.Value;
        entity.ImageCount = listing.ImageCount?.Value;
        entity.FuelTypes = listing.FuelTypes?.Values.Select(value => value.ToString()).ToArray();
        entity.Transmission = listing.Transmission?.Value;
        entity.Drivetrain = listing.Drivetrain?.Value;
        entity.BodyType = listing.BodyType?.Value;
        entity.Colour = listing.Colour?.Value;
        entity.Horsepower = listing.Horsepower?.Value;
        entity.EngineDisplacementCubicCentimetres = listing.EngineDisplacementCubicCentimetres?.Value;
        entity.EnergyConsumptionsJson = SavedListingJson.SerializeEnergyConsumptions(listing.EnergyConsumptions);
        entity.AnnualVehicleTaxSek = listing.AnnualVehicleTaxSek?.Value;
        entity.OwnerCount = listing.OwnerCount?.Value;
        entity.FirstRegistrationDate = listing.FirstRegistrationDate?.Value;
        entity.LastInspectionDate = listing.LastInspectionDate?.Value;
        entity.NextInspectionDate = listing.NextInspectionDate?.Value;
        entity.TowBar = listing.TowBar?.Value;
        entity.EquipmentKnown = listing.Equipment is not null;
        entity.SellerClaimsJson = SavedListingJson.SerializeStrings(listing.SellerClaims);
        entity.ConditionNotesJson = SavedListingJson.SerializeStrings(listing.ConditionNotes);
        entity.FieldProvenanceJson = SavedListingJson.SerializeProvenance(listing);
        entity.UpdatedAtUtc = now;

        for (var position = 0; position < prepared.Result.Sources.Count; position++)
        {
            var source = prepared.Result.Sources[position];
            entity.Sources.Add(new ListingSourceEntity
            {
                Id = Guid.CreateVersion7(now),
                ListingId = entity.Id,
                Listing = entity,
                Position = position,
                Url = source.Url.Value,
                MatchesSubmittedUrl = source.MatchesSubmittedUrl,
            });
        }

        if (listing.Equipment is not null)
        {
            for (var position = 0; position < listing.Equipment.Values.Count; position++)
            {
                entity.Equipment.Add(new ListingEquipmentEntity
                {
                    Id = Guid.CreateVersion7(now),
                    ListingId = entity.Id,
                    Listing = entity,
                    Position = position,
                    Value = listing.Equipment.Values[position],
                });
            }
        }
    }

    private static SavedListing ToSavedListing(VehicleEntity vehicle)
    {
        var entity = vehicle.Listing
            ?? throw new InvalidOperationException("The loaded vehicle does not contain a listing.");
        var extractionMetadataIsSupported =
            entity.RequestedModel is null
                && entity.PromptVersion is null
                && entity.ExtractionSchemaVersion is null
            || entity.RequestedModel is not null
                && entity.PromptVersion == ListingExtractionContractVersions.Prompt
                && entity.ExtractionSchemaVersion == ListingExtractionContractVersions.Schema;
        if (entity.ListingSchemaVersion != CurrentListingSchemaVersion
            || !extractionMetadataIsSupported)
        {
            throw new UnsupportedSavedListingVersionException(
                vehicle.Id,
                entity.ListingSchemaVersion,
                entity.PromptVersion,
                entity.ExtractionSchemaVersion);
        }

        var normalizedUrl = ListingUrl.Parse(entity.NormalizedUrl);
        var provenance = SavedListingJson.DeserializeProvenance(
            entity.FieldProvenanceJson,
            normalizedUrl);
        FieldProvenance Provenance(string name)
        {
            if (provenance.TryGetValue(name, out var value))
            {
                return value;
            }

            if (name == "vehicleLabel")
            {
                return ManualProvenance(normalizedUrl);
            }

            throw new InvalidDataException($"Saved listing provenance for '{name}' is missing.");
        }

        var listing = new ListingDraft
        {
            RegistrationNumber = new SourcedValue<RegistrationNumber>(
                RegistrationNumber.Parse(vehicle.RegistrationNumber),
                Provenance("registrationNumber")),
            VehicleLabel = vehicle.VehicleLabel is null
                ? null
                : new SourcedValue<string>(vehicle.VehicleLabel, Provenance("vehicleLabel")),
            Make = Wrap(entity.Make, "make", Provenance),
            Model = Wrap(entity.Model, "model", Provenance),
            Variant = Wrap(entity.Variant, "variant", Provenance),
            ModelYear = Wrap(entity.ModelYear, "modelYear", Provenance),
            Vin = Wrap(entity.Vin, "vin", Provenance),
            PriceSek = Wrap(entity.PriceSek, "priceSek", Provenance),
            OdometerKilometres = Wrap(entity.OdometerKilometres, "odometerKilometres", Provenance),
            SellerType = Wrap(entity.SellerType, "sellerType", Provenance),
            Locality = Wrap(entity.Locality, "locality", Provenance),
            County = Wrap(entity.County, "county", Provenance),
            PublishedDate = Wrap(entity.PublishedDate, "publishedDate", Provenance),
            UpdatedDate = Wrap(entity.AdvertisedUpdatedDate, "updatedDate", Provenance),
            ImageCount = Wrap(entity.ImageCount, "imageCount", Provenance),
            FuelTypes = entity.FuelTypes is null
                ? null
                : new SourcedCollection<FuelType>(
                    entity.FuelTypes.Select(value => System.Enum.Parse<FuelType>(value)),
                    Provenance("fuelTypes")),
            Transmission = Wrap(entity.Transmission, "transmission", Provenance),
            Drivetrain = Wrap(entity.Drivetrain, "drivetrain", Provenance),
            BodyType = Wrap(entity.BodyType, "bodyType", Provenance),
            Colour = Wrap(entity.Colour, "colour", Provenance),
            Horsepower = Wrap(entity.Horsepower, "horsepower", Provenance),
            EngineDisplacementCubicCentimetres = Wrap(
                entity.EngineDisplacementCubicCentimetres,
                "engineDisplacementCubicCentimetres",
                Provenance),
            EnergyConsumptions = entity.EnergyConsumptionsJson is null
                ? null
                : new SourcedCollection<EnergyConsumption>(
                    SavedListingJson.DeserializeEnergyConsumptions(entity.EnergyConsumptionsJson),
                    Provenance("energyConsumptions")),
            AnnualVehicleTaxSek = Wrap(entity.AnnualVehicleTaxSek, "annualVehicleTaxSek", Provenance),
            OwnerCount = Wrap(entity.OwnerCount, "ownerCount", Provenance),
            FirstRegistrationDate = Wrap(entity.FirstRegistrationDate, "firstRegistrationDate", Provenance),
            LastInspectionDate = Wrap(entity.LastInspectionDate, "lastInspectionDate", Provenance),
            NextInspectionDate = Wrap(entity.NextInspectionDate, "nextInspectionDate", Provenance),
            TowBar = Wrap(entity.TowBar, "towBar", Provenance),
            Equipment = entity.EquipmentKnown
                ? new SourcedCollection<string>(
                    entity.Equipment.OrderBy(value => value.Position).Select(value => value.Value),
                    Provenance("equipment"))
                : null,
            SellerClaims = entity.SellerClaimsJson is null
                ? null
                : new SourcedCollection<string>(
                    SavedListingJson.DeserializeStrings(entity.SellerClaimsJson),
                    Provenance("sellerClaims")),
            ConditionNotes = entity.ConditionNotesJson is null
                ? null
                : new SourcedCollection<string>(
                    SavedListingJson.DeserializeStrings(entity.ConditionNotesJson),
                    Provenance("conditionNotes")),
        };
        var sources = Array.AsReadOnly(
            entity.Sources
                .OrderBy(source => source.Position)
                .Select(source => new ListingAnalysisSource(
                    ListingUrl.Parse(source.Url),
                    source.MatchesSubmittedUrl))
                .ToArray());
        var missingFields = Array.AsReadOnly(
            entity.MissingFields
                .Select(value => System.Enum.Parse<ListingFieldCode>(value))
                .ToArray());
        var result = new ListingProcessingResult(entity.Status, sources, listing, missingFields);

        return new SavedListing(
            vehicle.Id,
            RegistrationNumber.Parse(vehicle.RegistrationNumber),
            vehicle.Revision,
            entity.ListingVersion,
            entity.ListingSchemaVersion,
            entity.SubmittedUrl,
            normalizedUrl,
            entity.AnalyzedAtUtc,
            entity.RequestedModel,
            entity.PromptVersion,
            entity.ExtractionSchemaVersion,
            result,
            vehicle.Scenario is not null,
            vehicle.CreatedAtUtc,
            vehicle.UpdatedAtUtc);
    }

    private static SourcedValue<T>? Wrap<T>(
        T? value,
        string name,
        Func<string, FieldProvenance> provenance)
        where T : struct =>
        value is null ? null : new SourcedValue<T>(value.Value, provenance(name));

    private static SourcedValue<string>? Wrap(
        string? value,
        string name,
        Func<string, FieldProvenance> provenance) =>
        value is null ? null : new SourcedValue<string>(value, provenance(name));

    private static FieldProvenance ManualProvenance(ListingUrl sourceUrl) =>
        new(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, sourceUrl);

    private static bool HasAiProvenance(ListingDraft listing) =>
        EnumerateProvenance(listing).Any(value => value.ExtractionMethod == ExtractionMethod.Ai);

    private static IEnumerable<FieldProvenance> EnumerateProvenance(ListingDraft listing)
    {
        var values = new FieldProvenance?[]
        {
            listing.RegistrationNumber?.Provenance, listing.VehicleLabel?.Provenance,
            listing.Make?.Provenance, listing.Model?.Provenance, listing.Variant?.Provenance,
            listing.ModelYear?.Provenance, listing.Vin?.Provenance, listing.PriceSek?.Provenance,
            listing.OdometerKilometres?.Provenance, listing.SellerType?.Provenance,
            listing.Locality?.Provenance, listing.County?.Provenance,
            listing.PublishedDate?.Provenance, listing.UpdatedDate?.Provenance,
            listing.ImageCount?.Provenance, listing.FuelTypes?.Provenance,
            listing.Transmission?.Provenance, listing.Drivetrain?.Provenance,
            listing.BodyType?.Provenance, listing.Colour?.Provenance,
            listing.Horsepower?.Provenance, listing.EngineDisplacementCubicCentimetres?.Provenance,
            listing.EnergyConsumptions?.Provenance, listing.AnnualVehicleTaxSek?.Provenance,
            listing.OwnerCount?.Provenance, listing.FirstRegistrationDate?.Provenance,
            listing.LastInspectionDate?.Provenance, listing.NextInspectionDate?.Provenance,
            listing.TowBar?.Provenance, listing.Equipment?.Provenance,
            listing.SellerClaims?.Provenance, listing.ConditionNotes?.Provenance,
        };
        return values.OfType<FieldProvenance>();
    }

    private static void EnsureExpectedRevision(VehicleEntity vehicle, long expectedRevision)
    {
        if (vehicle.Revision != expectedRevision)
        {
            throw new SavedListingConcurrencyException(
                vehicle.Id,
                expectedRevision,
                vehicle.Revision);
        }
    }

    private async Task<(Guid Id, long Revision)?> FindIdentityAsync(
        RegistrationNumber registrationNumber,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.RegistrationNumber == registrationNumber.Value)
            .Select(vehicle => new { vehicle.Id, vehicle.Revision })
            .SingleOrDefaultAsync(cancellationToken);
        return identity is null ? null : (identity.Id, identity.Revision);
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

    private static bool IsRegistrationNumberConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_vehicles_registration_number",
        };

    private sealed record PreparedListing(
        string SubmittedUrl,
        ListingUrl NormalizedUrl,
        DateTimeOffset AnalyzedAtUtc,
        string? RequestedModel,
        int? PromptVersion,
        int? ExtractionSchemaVersion,
        ListingProcessingResult Result);
}
