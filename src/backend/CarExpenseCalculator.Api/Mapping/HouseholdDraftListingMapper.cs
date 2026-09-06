using A = CarExpenseCalculator.Api.Contracts.SavedListings;
using AL = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using AM = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using C = CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

namespace CarExpenseCalculator.Api.Mapping;

internal static class HouseholdDraftListingMapper
{
    public static A.ReviewedListingInput ToApi(SavedListingInput input) => new()
    {
        SubmittedUrl = input.SubmittedUrl, AnalyzedAtUtc = input.AnalyzedAtUtc,
        RequestedModel = input.RequestedModel, PromptVersion = input.PromptVersion,
        SchemaVersion = input.ExtractionSchemaVersion, Sources = input.Sources.Select(x => x.Value).ToArray(),
        Draft = Draft(input.Listing),
    };

    private static A.ListingDraftInput Draft(C.ListingDraft x) => new()
    {
        RegistrationNumber = Value(x.RegistrationNumber, value => value.Value),
        Make = Value(x.Make, value => value),
        Model = Value(x.Model, value => value),
        Variant = Value(x.Variant, value => value),
        ModelYear = Value(x.ModelYear, value => value),
        Vin = Value(x.Vin, value => value),
        VehicleLabel = Value(x.VehicleLabel, value => value),
        PriceSek = Value(x.PriceSek, value => value),
        OdometerKilometres = Value(x.OdometerKilometres, value => value),
        SellerType = Value(x.SellerType, value => (AL.SellerType)value),
        Locality = Value(x.Locality, value => value),
        County = Value(x.County, value => value),
        PublishedDate = Value(x.PublishedDate, value => value),
        UpdatedDate = Value(x.UpdatedDate, value => value),
        ImageCount = Value(x.ImageCount, value => value),
        Transmission = Value(x.Transmission, value => (AL.Transmission)value),
        Drivetrain = Value(x.Drivetrain, value => (AL.Drivetrain)value),
        BodyType = Value(x.BodyType, value => (AL.BodyType)value),
        Colour = Value(x.Colour, value => value),
        Horsepower = Value(x.Horsepower, value => value),
        EngineDisplacementCubicCentimetres = Value(x.EngineDisplacementCubicCentimetres, value => value),
        AnnualVehicleTaxSek = Value(x.AnnualVehicleTaxSek, value => value),
        OwnerCount = Value(x.OwnerCount, value => value),
        FirstRegistrationDate = Value(x.FirstRegistrationDate, value => value),
        LastInspectionDate = Value(x.LastInspectionDate, value => value),
        NextInspectionDate = Value(x.NextInspectionDate, value => value),
        TowBar = Value(x.TowBar, value => value),
        FuelTypes = Collection(x.FuelTypes, value => (AL.FuelType)value),
        EnergyConsumptions = Collection(x.EnergyConsumptions, value => new A.EnergyConsumptionInput
        { Label = value.Label, Unit = (AM.EnergyUnit)value.Unit, ConsumptionPer100Kilometres = value.ConsumptionPer100Kilometres }),
        Equipment = Collection(x.Equipment, value => value),
        SellerClaims = Collection(x.SellerClaims, value => value),
        ConditionNotes = Collection(x.ConditionNotes, value => value),
    };

    private static A.FieldProvenanceInput Provenance(C.FieldProvenance x) => new()
    {
        Origin = (AL.FieldOrigin)x.Origin, ExtractionMethod = (AL.ExtractionMethod)x.ExtractionMethod,
        Verification = (AL.VerificationStatus)x.Verification, SourceUrl = x.SourceUrl.Value,
    };
    private static A.SourcedValueInput<TOut>? Value<TIn, TOut>(C.SourcedValue<TIn>? x, Func<TIn, TOut> map)
        where TIn : notnull where TOut : notnull => x is null ? null
            : new() { Value = map(x.Value), Provenance = Provenance(x.Provenance) };
    private static A.SourcedCollectionInput<TOut>? Collection<TIn, TOut>(C.SourcedCollection<TIn>? x, Func<TIn, TOut> map)
        where TIn : notnull where TOut : notnull => x is null ? null
            : new() { Values = x.Values.Select(map).ToArray(), Provenance = Provenance(x.Provenance) };
}
