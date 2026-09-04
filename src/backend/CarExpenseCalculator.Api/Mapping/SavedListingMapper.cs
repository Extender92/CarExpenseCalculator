using ApiContracts = CarExpenseCalculator.Api.Contracts.SavedListings;
using ApiListingContracts = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using ApiManualContracts = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using CoreContracts = CarExpenseCalculator.Core.Listings;
using CoreCostContracts = CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

namespace CarExpenseCalculator.Api.Mapping;

internal static class SavedListingMapper
{
    public static SavedListingInput ToStoreInput(ApiContracts.ReviewedListingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<CoreContracts.ListingValidationError>();
        _ = ParseUrl(input.SubmittedUrl, "submittedUrl", errors);
        var sources = input.Sources
            .Select((value, index) => ParseUrl(value, $"sources[{index}]", errors))
            .OfType<CoreContracts.ListingUrl>()
            .ToArray();
        var draft = MapDraft(input.Draft, errors);

        if (errors.Count > 0)
        {
            throw new SavedListingRequestMappingException(errors);
        }

        return new SavedListingInput(
            input.SubmittedUrl,
            input.AnalyzedAtUtc,
            input.RequestedModel,
            input.PromptVersion,
            input.SchemaVersion,
            sources,
            draft);
    }

    public static ApiContracts.SavedListingResponse ToApi(SavedListing savedListing)
    {
        ArgumentNullException.ThrowIfNull(savedListing);

        var result = savedListing.ProcessingResult;
        return new ApiContracts.SavedListingResponse(
            savedListing.VehicleId,
            savedListing.RegistrationNumber.Value,
            savedListing.Revision,
            savedListing.ListingVersion,
            savedListing.ListingSchemaVersion,
            savedListing.CreatedAtUtc,
            savedListing.UpdatedAtUtc,
            savedListing.AnalyzedAtUtc,
            savedListing.SubmittedUrl,
            savedListing.NormalizedUrl.Value,
            ListingAnalysisMapper.MapStatus(result.Status),
            savedListing.RequestedModel,
            savedListing.PromptVersion,
            savedListing.ExtractionSchemaVersion,
            result.Sources
                .Select(source => new ApiListingContracts.ListingAnalysisSourceResponse(
                    source.Url.Value,
                    source.MatchesSubmittedUrl))
                .ToArray(),
            ListingAnalysisMapper.MapListing(result.Listing),
            result.MissingFields.Select(ListingAnalysisMapper.MapFieldCode).ToArray(),
            savedListing.HasSavedCostScenario);
    }

    public static ApiContracts.SavedListingSummaryResponse ToSummaryApi(SavedListing savedListing)
    {
        ArgumentNullException.ThrowIfNull(savedListing);

        var listing = savedListing.ProcessingResult.Listing;
        return new ApiContracts.SavedListingSummaryResponse(
            savedListing.VehicleId,
            savedListing.RegistrationNumber.Value,
            listing.VehicleLabel?.Value,
            savedListing.Revision,
            savedListing.ListingVersion,
            savedListing.ListingSchemaVersion,
            listing.Make?.Value,
            listing.Model?.Value,
            listing.ModelYear?.Value,
            listing.PriceSek?.Value,
            listing.OdometerKilometres?.Value,
            ListingAnalysisMapper.MapStatus(savedListing.ProcessingResult.Status),
            savedListing.ProcessingResult.MissingFields.Count,
            savedListing.HasSavedCostScenario,
            savedListing.UpdatedAtUtc);
    }

    private static CoreContracts.ListingDraft MapDraft(
        ApiContracts.ListingDraftInput input,
        ICollection<CoreContracts.ListingValidationError> errors)
    {
        return new CoreContracts.ListingDraft
        {
            RegistrationNumber = MapRegistrationNumber(input.RegistrationNumber, errors),
            Make = MapValue(input.Make, "draft.make", errors),
            Model = MapValue(input.Model, "draft.model", errors),
            Variant = MapValue(input.Variant, "draft.variant", errors),
            ModelYear = MapValue(input.ModelYear, "draft.modelYear", errors),
            Vin = MapValue(input.Vin, "draft.vin", errors),
            VehicleLabel = MapValue(input.VehicleLabel, "draft.vehicleLabel", errors),
            PriceSek = MapValue(input.PriceSek, "draft.priceSek", errors),
            OdometerKilometres = MapValue(
                input.OdometerKilometres,
                "draft.odometerKilometres",
                errors),
            SellerType = MapValue(
                input.SellerType,
                "draft.sellerType",
                errors,
                MapSellerType),
            Locality = MapValue(input.Locality, "draft.locality", errors),
            County = MapValue(input.County, "draft.county", errors),
            PublishedDate = MapValue(input.PublishedDate, "draft.publishedDate", errors),
            UpdatedDate = MapValue(input.UpdatedDate, "draft.updatedDate", errors),
            ImageCount = MapValue(input.ImageCount, "draft.imageCount", errors),
            FuelTypes = MapCollection(
                input.FuelTypes,
                "draft.fuelTypes",
                errors,
                MapFuelType),
            Transmission = MapValue(
                input.Transmission,
                "draft.transmission",
                errors,
                MapTransmission),
            Drivetrain = MapValue(
                input.Drivetrain,
                "draft.drivetrain",
                errors,
                MapDrivetrain),
            BodyType = MapValue(
                input.BodyType,
                "draft.bodyType",
                errors,
                MapBodyType),
            Colour = MapValue(input.Colour, "draft.colour", errors),
            Horsepower = MapValue(input.Horsepower, "draft.horsepower", errors),
            EngineDisplacementCubicCentimetres = MapValue(
                input.EngineDisplacementCubicCentimetres,
                "draft.engineDisplacementCubicCentimetres",
                errors),
            EnergyConsumptions = MapCollection(
                input.EnergyConsumptions,
                "draft.energyConsumptions",
                errors,
                MapEnergyConsumption),
            AnnualVehicleTaxSek = MapValue(
                input.AnnualVehicleTaxSek,
                "draft.annualVehicleTaxSek",
                errors),
            OwnerCount = MapValue(input.OwnerCount, "draft.ownerCount", errors),
            FirstRegistrationDate = MapValue(
                input.FirstRegistrationDate,
                "draft.firstRegistrationDate",
                errors),
            LastInspectionDate = MapValue(
                input.LastInspectionDate,
                "draft.lastInspectionDate",
                errors),
            NextInspectionDate = MapValue(
                input.NextInspectionDate,
                "draft.nextInspectionDate",
                errors),
            TowBar = MapValue(input.TowBar, "draft.towBar", errors),
            Equipment = MapCollection(input.Equipment, "draft.equipment", errors),
            SellerClaims = MapCollection(input.SellerClaims, "draft.sellerClaims", errors),
            ConditionNotes = MapCollection(input.ConditionNotes, "draft.conditionNotes", errors),
        };
    }

    private static CoreContracts.SourcedValue<RegistrationNumber>? MapRegistrationNumber(
        ApiContracts.SourcedValueInput<string>? input,
        ICollection<CoreContracts.ListingValidationError> errors)
    {
        if (input is null)
        {
            return null;
        }

        if (input.Provenance is null)
        {
            errors.Add(new CoreContracts.ListingValidationError(
                "draft.registrationNumber.provenance",
                "Provenance is required."));
            return null;
        }

        var provenance = MapProvenance(input.Provenance, "draft.registrationNumber", errors);
        if (!RegistrationNumber.TryParse(input.Value, out var registrationNumber))
        {
            errors.Add(new CoreContracts.ListingValidationError(
                "draft.registrationNumber.value",
                "Registration number must be a supported ordinary Swedish registration number."));
        }

        return provenance is null || registrationNumber is null
            ? null
            : new CoreContracts.SourcedValue<RegistrationNumber>(registrationNumber, provenance);
    }

    private static CoreContracts.SourcedValue<T>? MapValue<T>(
        ApiContracts.SourcedValueInput<T>? input,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors)
        where T : notnull
    {
        return MapValue(input, path, errors, value => value);
    }

    private static CoreContracts.SourcedValue<TTarget>? MapValue<TSource, TTarget>(
        ApiContracts.SourcedValueInput<TSource>? input,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors,
        Func<TSource, TTarget> map)
        where TSource : notnull
        where TTarget : notnull
    {
        if (input is null)
        {
            return null;
        }

        if (input.Provenance is null)
        {
            errors.Add(new CoreContracts.ListingValidationError(
                $"{path}.provenance",
                "Provenance is required."));
            return null;
        }

        var provenance = MapProvenance(input.Provenance, path, errors);
        return provenance is null
            ? null
            : new CoreContracts.SourcedValue<TTarget>(map(input.Value), provenance);
    }

    private static CoreContracts.SourcedCollection<T>? MapCollection<T>(
        ApiContracts.SourcedCollectionInput<T>? input,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors)
        where T : notnull
    {
        return MapCollection(input, path, errors, value => value);
    }

    private static CoreContracts.SourcedCollection<TTarget>? MapCollection<TSource, TTarget>(
        ApiContracts.SourcedCollectionInput<TSource>? input,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors,
        Func<TSource, TTarget> map)
        where TSource : notnull
        where TTarget : notnull
    {
        if (input is null)
        {
            return null;
        }

        if (input.Provenance is null)
        {
            errors.Add(new CoreContracts.ListingValidationError(
                $"{path}.provenance",
                "Provenance is required."));
            return null;
        }

        if (input.Values is null)
        {
            errors.Add(new CoreContracts.ListingValidationError(
                $"{path}.values",
                "Values are required."));
            return null;
        }

        var provenance = MapProvenance(input.Provenance, path, errors);
        var values = new List<TTarget>();
        for (var index = 0; index < input.Values.Count; index++)
        {
            var value = input.Values[index];
            if (value is null)
            {
                errors.Add(new CoreContracts.ListingValidationError(
                    $"{path}.values[{index}]",
                    "Value cannot be null."));
            }
            else
            {
                values.Add(map(value));
            }
        }

        return provenance is null
            ? null
            : new CoreContracts.SourcedCollection<TTarget>(values, provenance);
    }

    private static CoreContracts.FieldProvenance? MapProvenance(
        ApiContracts.FieldProvenanceInput input,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors)
    {
        var sourceUrl = ParseUrl(input.SourceUrl, $"{path}.provenance.sourceUrl", errors);
        return sourceUrl is null
            ? null
            : new CoreContracts.FieldProvenance(
                MapOrigin(input.Origin),
                MapExtractionMethod(input.ExtractionMethod),
                MapVerification(input.Verification),
                sourceUrl);
    }

    private static CoreContracts.ListingUrl? ParseUrl(
        string? value,
        string path,
        ICollection<CoreContracts.ListingValidationError> errors)
    {
        if (value is null)
        {
            errors.Add(new CoreContracts.ListingValidationError(path, "URL is required."));
            return null;
        }

        try
        {
            return CoreContracts.ListingUrl.Parse(value);
        }
        catch (CoreContracts.ListingUrlValidationException exception)
        {
            errors.Add(new CoreContracts.ListingValidationError(
                path,
                ListingUrlValidationMessages.Get(exception.Code)));
            return null;
        }
    }

    private static CoreContracts.EnergyConsumption MapEnergyConsumption(
        ApiContracts.EnergyConsumptionInput value)
    {
        return new CoreContracts.EnergyConsumption(
            value.Label,
            MapEnergyUnit(value.Unit),
            value.ConsumptionPer100Kilometres);
    }

    private static CoreContracts.FieldOrigin MapOrigin(ApiListingContracts.FieldOrigin value) =>
        value switch
        {
            ApiListingContracts.FieldOrigin.Listing => CoreContracts.FieldOrigin.Listing,
            ApiListingContracts.FieldOrigin.User => CoreContracts.FieldOrigin.User,
            ApiListingContracts.FieldOrigin.Registry => CoreContracts.FieldOrigin.Registry,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.ExtractionMethod MapExtractionMethod(
        ApiListingContracts.ExtractionMethod value) => value switch
        {
            ApiListingContracts.ExtractionMethod.Ai => CoreContracts.ExtractionMethod.Ai,
            ApiListingContracts.ExtractionMethod.Manual => CoreContracts.ExtractionMethod.Manual,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.VerificationStatus MapVerification(
        ApiListingContracts.VerificationStatus value) => value switch
        {
            ApiListingContracts.VerificationStatus.Unverified => CoreContracts.VerificationStatus.Unverified,
            ApiListingContracts.VerificationStatus.UserConfirmed => CoreContracts.VerificationStatus.UserConfirmed,
            ApiListingContracts.VerificationStatus.RegistryVerified => CoreContracts.VerificationStatus.RegistryVerified,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.SellerType MapSellerType(ApiListingContracts.SellerType value) =>
        value switch
        {
            ApiListingContracts.SellerType.Private => CoreContracts.SellerType.Private,
            ApiListingContracts.SellerType.Dealer => CoreContracts.SellerType.Dealer,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.FuelType MapFuelType(ApiListingContracts.FuelType value) =>
        value switch
        {
            ApiListingContracts.FuelType.Petrol => CoreContracts.FuelType.Petrol,
            ApiListingContracts.FuelType.Diesel => CoreContracts.FuelType.Diesel,
            ApiListingContracts.FuelType.Electricity => CoreContracts.FuelType.Electricity,
            ApiListingContracts.FuelType.Ethanol => CoreContracts.FuelType.Ethanol,
            ApiListingContracts.FuelType.Biogas => CoreContracts.FuelType.Biogas,
            ApiListingContracts.FuelType.NaturalGas => CoreContracts.FuelType.NaturalGas,
            ApiListingContracts.FuelType.LiquefiedPetroleumGas => CoreContracts.FuelType.LiquefiedPetroleumGas,
            ApiListingContracts.FuelType.Hydrogen => CoreContracts.FuelType.Hydrogen,
            ApiListingContracts.FuelType.Other => CoreContracts.FuelType.Other,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.Transmission MapTransmission(ApiListingContracts.Transmission value) =>
        value switch
        {
            ApiListingContracts.Transmission.Manual => CoreContracts.Transmission.Manual,
            ApiListingContracts.Transmission.Automatic => CoreContracts.Transmission.Automatic,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.Drivetrain MapDrivetrain(ApiListingContracts.Drivetrain value) =>
        value switch
        {
            ApiListingContracts.Drivetrain.FrontWheelDrive => CoreContracts.Drivetrain.FrontWheelDrive,
            ApiListingContracts.Drivetrain.RearWheelDrive => CoreContracts.Drivetrain.RearWheelDrive,
            ApiListingContracts.Drivetrain.AllWheelDrive => CoreContracts.Drivetrain.AllWheelDrive,
            _ => throw Unsupported(value),
        };

    private static CoreContracts.BodyType MapBodyType(ApiListingContracts.BodyType value) =>
        value switch
        {
            ApiListingContracts.BodyType.Sedan => CoreContracts.BodyType.Sedan,
            ApiListingContracts.BodyType.Hatchback => CoreContracts.BodyType.Hatchback,
            ApiListingContracts.BodyType.Wagon => CoreContracts.BodyType.Wagon,
            ApiListingContracts.BodyType.Suv => CoreContracts.BodyType.Suv,
            ApiListingContracts.BodyType.Coupe => CoreContracts.BodyType.Coupe,
            ApiListingContracts.BodyType.Convertible => CoreContracts.BodyType.Convertible,
            ApiListingContracts.BodyType.Minivan => CoreContracts.BodyType.Minivan,
            ApiListingContracts.BodyType.Pickup => CoreContracts.BodyType.Pickup,
            ApiListingContracts.BodyType.Van => CoreContracts.BodyType.Van,
            ApiListingContracts.BodyType.Other => CoreContracts.BodyType.Other,
            _ => throw Unsupported(value),
        };

    private static CoreCostContracts.EnergyUnit MapEnergyUnit(ApiManualContracts.EnergyUnit value) =>
        value switch
        {
            ApiManualContracts.EnergyUnit.Litre => CoreCostContracts.EnergyUnit.Litre,
            ApiManualContracts.EnergyUnit.KilowattHour => CoreCostContracts.EnergyUnit.KilowattHour,
            ApiManualContracts.EnergyUnit.Kilogram => CoreCostContracts.EnergyUnit.Kilogram,
            _ => throw Unsupported(value),
        };

    private static ArgumentOutOfRangeException Unsupported<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        new(nameof(value), value, "Unsupported listing enum value.");
}

internal sealed class SavedListingRequestMappingException(
    IEnumerable<CoreContracts.ListingValidationError> errors)
    : Exception("The saved listing request is invalid.")
{
    public IReadOnlyList<CoreContracts.ListingValidationError> Errors { get; } =
        Array.AsReadOnly(errors.ToArray());
}
