using CarExpenseCalculator.Infrastructure.ListingExtraction;
using ApiContracts = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using ApiManualContracts = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using CoreContracts = CarExpenseCalculator.Core.Listings;
using CoreCostContracts = CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Api.Mapping;

internal static class ListingAnalysisMapper
{
    public static ApiContracts.ListingAnalysisResponse ToApi(ListingExtractionSuccess extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);

        return new ApiContracts.ListingAnalysisResponse(
            extraction.SubmittedUrl.SubmittedValue,
            extraction.SubmittedUrl.Value,
            MapStatus(extraction.ProcessingResult.Status),
            extraction.AnalyzedAtUtc,
            extraction.RequestedModel,
            extraction.PromptVersion,
            extraction.SchemaVersion,
            extraction.ProcessingResult.Sources.Select(source =>
                new ApiContracts.ListingAnalysisSourceResponse(
                    source.Url.Value,
                    source.MatchesSubmittedUrl)).ToArray(),
            MapListing(extraction.ProcessingResult.Listing),
            extraction.ProcessingResult.MissingFields.Select(MapFieldCode).ToArray());
    }

    private static ApiContracts.ListingDraftResponse MapListing(CoreContracts.ListingDraft listing)
    {
        return new ApiContracts.ListingDraftResponse(
            MapValue(listing.RegistrationNumber, value => value.Value),
            MapValue(listing.Make),
            MapValue(listing.Model),
            MapValue(listing.Variant),
            MapValue(listing.ModelYear),
            MapValue(listing.Vin),
            MapValue(listing.VehicleLabel),
            MapValue(listing.PriceSek),
            MapValue(listing.OdometerKilometres),
            MapValue(listing.SellerType, MapSellerType),
            MapValue(listing.Locality),
            MapValue(listing.County),
            MapValue(listing.PublishedDate),
            MapValue(listing.UpdatedDate),
            MapValue(listing.ImageCount),
            MapCollection(listing.FuelTypes, MapFuelType),
            MapValue(listing.Transmission, MapTransmission),
            MapValue(listing.Drivetrain, MapDrivetrain),
            MapValue(listing.BodyType, MapBodyType),
            MapValue(listing.Colour),
            MapValue(listing.Horsepower),
            MapValue(listing.EngineDisplacementCubicCentimetres),
            MapCollection(listing.EnergyConsumptions, MapEnergyConsumption),
            MapValue(listing.AnnualVehicleTaxSek),
            MapValue(listing.OwnerCount),
            MapValue(listing.FirstRegistrationDate),
            MapValue(listing.LastInspectionDate),
            MapValue(listing.NextInspectionDate),
            MapValue(listing.TowBar),
            MapCollection(listing.Equipment),
            MapCollection(listing.SellerClaims),
            MapCollection(listing.ConditionNotes));
    }

    private static ApiContracts.SourcedValueResponse<T>? MapValue<T>(
        CoreContracts.SourcedValue<T>? source)
        where T : notnull
    {
        return source is null
            ? null
            : new ApiContracts.SourcedValueResponse<T>(
                source.Value,
                MapProvenance(source.Provenance));
    }

    private static ApiContracts.SourcedValueResponse<TTarget>? MapValue<TSource, TTarget>(
        CoreContracts.SourcedValue<TSource>? source,
        Func<TSource, TTarget> mapValue)
        where TSource : notnull
        where TTarget : notnull
    {
        return source is null
            ? null
            : new ApiContracts.SourcedValueResponse<TTarget>(
                mapValue(source.Value),
                MapProvenance(source.Provenance));
    }

    private static ApiContracts.SourcedCollectionResponse<T>? MapCollection<T>(
        CoreContracts.SourcedCollection<T>? source)
        where T : notnull
    {
        return source is null
            ? null
            : new ApiContracts.SourcedCollectionResponse<T>(
                source.Values.ToArray(),
                MapProvenance(source.Provenance));
    }

    private static ApiContracts.SourcedCollectionResponse<TTarget>? MapCollection<TSource, TTarget>(
        CoreContracts.SourcedCollection<TSource>? source,
        Func<TSource, TTarget> mapValue)
        where TSource : notnull
        where TTarget : notnull
    {
        return source is null
            ? null
            : new ApiContracts.SourcedCollectionResponse<TTarget>(
                source.Values.Select(mapValue).ToArray(),
                MapProvenance(source.Provenance));
    }

    private static ApiContracts.FieldProvenanceResponse MapProvenance(
        CoreContracts.FieldProvenance provenance)
    {
        return new ApiContracts.FieldProvenanceResponse(
            MapOrigin(provenance.Origin),
            MapExtractionMethod(provenance.ExtractionMethod),
            MapVerification(provenance.Verification),
            provenance.SourceUrl.Value);
    }

    private static ApiContracts.EnergyConsumptionResponse MapEnergyConsumption(
        CoreContracts.EnergyConsumption value)
    {
        return new ApiContracts.EnergyConsumptionResponse(
            value.Label,
            MapEnergyUnit(value.Unit),
            value.ConsumptionPer100Kilometres);
    }

    private static ApiContracts.ListingAnalysisStatus MapStatus(CoreContracts.ListingAnalysisStatus status)
    {
        return status switch
        {
            CoreContracts.ListingAnalysisStatus.Complete => ApiContracts.ListingAnalysisStatus.Complete,
            CoreContracts.ListingAnalysisStatus.Partial => ApiContracts.ListingAnalysisStatus.Partial,
            CoreContracts.ListingAnalysisStatus.Unavailable => ApiContracts.ListingAnalysisStatus.Unavailable,
            _ => throw Unsupported(status),
        };
    }

    private static ApiContracts.FieldOrigin MapOrigin(CoreContracts.FieldOrigin origin)
    {
        return origin switch
        {
            CoreContracts.FieldOrigin.Listing => ApiContracts.FieldOrigin.Listing,
            CoreContracts.FieldOrigin.User => ApiContracts.FieldOrigin.User,
            CoreContracts.FieldOrigin.Registry => ApiContracts.FieldOrigin.Registry,
            _ => throw Unsupported(origin),
        };
    }

    private static ApiContracts.ExtractionMethod MapExtractionMethod(
        CoreContracts.ExtractionMethod method)
    {
        return method switch
        {
            CoreContracts.ExtractionMethod.Ai => ApiContracts.ExtractionMethod.Ai,
            CoreContracts.ExtractionMethod.Manual => ApiContracts.ExtractionMethod.Manual,
            _ => throw Unsupported(method),
        };
    }

    private static ApiContracts.VerificationStatus MapVerification(
        CoreContracts.VerificationStatus verification)
    {
        return verification switch
        {
            CoreContracts.VerificationStatus.Unverified => ApiContracts.VerificationStatus.Unverified,
            CoreContracts.VerificationStatus.UserConfirmed => ApiContracts.VerificationStatus.UserConfirmed,
            CoreContracts.VerificationStatus.RegistryVerified => ApiContracts.VerificationStatus.RegistryVerified,
            _ => throw Unsupported(verification),
        };
    }

    private static ApiContracts.SellerType MapSellerType(CoreContracts.SellerType sellerType)
    {
        return sellerType switch
        {
            CoreContracts.SellerType.Private => ApiContracts.SellerType.Private,
            CoreContracts.SellerType.Dealer => ApiContracts.SellerType.Dealer,
            _ => throw Unsupported(sellerType),
        };
    }

    private static ApiContracts.FuelType MapFuelType(CoreContracts.FuelType fuelType)
    {
        return fuelType switch
        {
            CoreContracts.FuelType.Petrol => ApiContracts.FuelType.Petrol,
            CoreContracts.FuelType.Diesel => ApiContracts.FuelType.Diesel,
            CoreContracts.FuelType.Electricity => ApiContracts.FuelType.Electricity,
            CoreContracts.FuelType.Ethanol => ApiContracts.FuelType.Ethanol,
            CoreContracts.FuelType.Biogas => ApiContracts.FuelType.Biogas,
            CoreContracts.FuelType.NaturalGas => ApiContracts.FuelType.NaturalGas,
            CoreContracts.FuelType.LiquefiedPetroleumGas => ApiContracts.FuelType.LiquefiedPetroleumGas,
            CoreContracts.FuelType.Hydrogen => ApiContracts.FuelType.Hydrogen,
            CoreContracts.FuelType.Other => ApiContracts.FuelType.Other,
            _ => throw Unsupported(fuelType),
        };
    }

    private static ApiContracts.Transmission MapTransmission(CoreContracts.Transmission transmission)
    {
        return transmission switch
        {
            CoreContracts.Transmission.Manual => ApiContracts.Transmission.Manual,
            CoreContracts.Transmission.Automatic => ApiContracts.Transmission.Automatic,
            _ => throw Unsupported(transmission),
        };
    }

    private static ApiContracts.Drivetrain MapDrivetrain(CoreContracts.Drivetrain drivetrain)
    {
        return drivetrain switch
        {
            CoreContracts.Drivetrain.FrontWheelDrive => ApiContracts.Drivetrain.FrontWheelDrive,
            CoreContracts.Drivetrain.RearWheelDrive => ApiContracts.Drivetrain.RearWheelDrive,
            CoreContracts.Drivetrain.AllWheelDrive => ApiContracts.Drivetrain.AllWheelDrive,
            _ => throw Unsupported(drivetrain),
        };
    }

    private static ApiContracts.BodyType MapBodyType(CoreContracts.BodyType bodyType)
    {
        return bodyType switch
        {
            CoreContracts.BodyType.Sedan => ApiContracts.BodyType.Sedan,
            CoreContracts.BodyType.Hatchback => ApiContracts.BodyType.Hatchback,
            CoreContracts.BodyType.Wagon => ApiContracts.BodyType.Wagon,
            CoreContracts.BodyType.Suv => ApiContracts.BodyType.Suv,
            CoreContracts.BodyType.Coupe => ApiContracts.BodyType.Coupe,
            CoreContracts.BodyType.Convertible => ApiContracts.BodyType.Convertible,
            CoreContracts.BodyType.Minivan => ApiContracts.BodyType.Minivan,
            CoreContracts.BodyType.Pickup => ApiContracts.BodyType.Pickup,
            CoreContracts.BodyType.Van => ApiContracts.BodyType.Van,
            CoreContracts.BodyType.Other => ApiContracts.BodyType.Other,
            _ => throw Unsupported(bodyType),
        };
    }

    private static ApiManualContracts.EnergyUnit MapEnergyUnit(CoreCostContracts.EnergyUnit unit)
    {
        return unit switch
        {
            CoreCostContracts.EnergyUnit.Litre => ApiManualContracts.EnergyUnit.Litre,
            CoreCostContracts.EnergyUnit.KilowattHour => ApiManualContracts.EnergyUnit.KilowattHour,
            CoreCostContracts.EnergyUnit.Kilogram => ApiManualContracts.EnergyUnit.Kilogram,
            _ => throw Unsupported(unit),
        };
    }

    private static ApiContracts.ListingFieldCode MapFieldCode(CoreContracts.ListingFieldCode code)
    {
        return code switch
        {
            CoreContracts.ListingFieldCode.RegistrationNumber => ApiContracts.ListingFieldCode.RegistrationNumber,
            CoreContracts.ListingFieldCode.Make => ApiContracts.ListingFieldCode.Make,
            CoreContracts.ListingFieldCode.Model => ApiContracts.ListingFieldCode.Model,
            CoreContracts.ListingFieldCode.Variant => ApiContracts.ListingFieldCode.Variant,
            CoreContracts.ListingFieldCode.ModelYear => ApiContracts.ListingFieldCode.ModelYear,
            CoreContracts.ListingFieldCode.Vin => ApiContracts.ListingFieldCode.Vin,
            CoreContracts.ListingFieldCode.PriceSek => ApiContracts.ListingFieldCode.PriceSek,
            CoreContracts.ListingFieldCode.OdometerKilometres => ApiContracts.ListingFieldCode.OdometerKilometres,
            CoreContracts.ListingFieldCode.SellerType => ApiContracts.ListingFieldCode.SellerType,
            CoreContracts.ListingFieldCode.Locality => ApiContracts.ListingFieldCode.Locality,
            CoreContracts.ListingFieldCode.County => ApiContracts.ListingFieldCode.County,
            CoreContracts.ListingFieldCode.PublishedDate => ApiContracts.ListingFieldCode.PublishedDate,
            CoreContracts.ListingFieldCode.UpdatedDate => ApiContracts.ListingFieldCode.UpdatedDate,
            CoreContracts.ListingFieldCode.ImageCount => ApiContracts.ListingFieldCode.ImageCount,
            CoreContracts.ListingFieldCode.FuelTypes => ApiContracts.ListingFieldCode.FuelTypes,
            CoreContracts.ListingFieldCode.Transmission => ApiContracts.ListingFieldCode.Transmission,
            CoreContracts.ListingFieldCode.Drivetrain => ApiContracts.ListingFieldCode.Drivetrain,
            CoreContracts.ListingFieldCode.BodyType => ApiContracts.ListingFieldCode.BodyType,
            CoreContracts.ListingFieldCode.Colour => ApiContracts.ListingFieldCode.Colour,
            CoreContracts.ListingFieldCode.Horsepower => ApiContracts.ListingFieldCode.Horsepower,
            CoreContracts.ListingFieldCode.EngineDisplacementCubicCentimetres =>
                ApiContracts.ListingFieldCode.EngineDisplacementCubicCentimetres,
            CoreContracts.ListingFieldCode.EnergyConsumptions => ApiContracts.ListingFieldCode.EnergyConsumptions,
            CoreContracts.ListingFieldCode.AnnualVehicleTaxSek => ApiContracts.ListingFieldCode.AnnualVehicleTaxSek,
            CoreContracts.ListingFieldCode.OwnerCount => ApiContracts.ListingFieldCode.OwnerCount,
            CoreContracts.ListingFieldCode.FirstRegistrationDate => ApiContracts.ListingFieldCode.FirstRegistrationDate,
            CoreContracts.ListingFieldCode.LastInspectionDate => ApiContracts.ListingFieldCode.LastInspectionDate,
            CoreContracts.ListingFieldCode.NextInspectionDate => ApiContracts.ListingFieldCode.NextInspectionDate,
            CoreContracts.ListingFieldCode.TowBar => ApiContracts.ListingFieldCode.TowBar,
            CoreContracts.ListingFieldCode.Equipment => ApiContracts.ListingFieldCode.Equipment,
            CoreContracts.ListingFieldCode.SellerClaims => ApiContracts.ListingFieldCode.SellerClaims,
            CoreContracts.ListingFieldCode.ConditionNotes => ApiContracts.ListingFieldCode.ConditionNotes,
            _ => throw Unsupported(code),
        };
    }

    private static ArgumentOutOfRangeException Unsupported<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return new ArgumentOutOfRangeException(nameof(value), value, "Unsupported listing enum value.");
    }
}
