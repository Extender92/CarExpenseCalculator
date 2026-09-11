using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

// Bounded reviewed listing facts only; no raw extraction output or credentials.
internal sealed record DraftListingPayload(string SubmittedUrl, DateTimeOffset AnalyzedAtUtc, string? RequestedModel,
    int? PromptVersion, int? ExtractionSchemaVersion, string[] Sources, DraftListingValues Values)
{
    public static DraftListingPayload FromInput(SavedListingInput x) => new(x.SubmittedUrl, x.AnalyzedAtUtc,
        x.RequestedModel, x.PromptVersion, x.ExtractionSchemaVersion, x.Sources.Select(s => s.Value).ToArray(),
        DraftListingValues.FromCore(x.Listing));
    public SavedListingInput ToInput() => new(SubmittedUrl, AnalyzedAtUtc, RequestedModel, PromptVersion,
        ExtractionSchemaVersion, Sources.Select(ListingUrl.Parse), Values.ToCore());
}

internal sealed record DraftProvenance(FieldOrigin Origin, ExtractionMethod ExtractionMethod, VerificationStatus Verification, string SourceUrl)
{
    public static DraftProvenance FromCore(FieldProvenance p) => new(p.Origin, p.ExtractionMethod, p.Verification, p.SourceUrl.Value);
    public FieldProvenance ToCore() => new(Origin, ExtractionMethod, Verification, ListingUrl.Parse(SourceUrl));
}
internal sealed record DraftFact<T>(T Value, DraftProvenance Provenance) where T : notnull
{
    public static DraftFact<T>? FromCore(SourcedValue<T>? x) => x is null ? null : new(x.Value, DraftProvenance.FromCore(x.Provenance));
    public SourcedValue<T> ToCore() => new(Value, Provenance.ToCore());
}
internal sealed record DraftCollection<T>(T[] Values, DraftProvenance Provenance) where T : notnull
{
    public static DraftCollection<T>? FromCore(SourcedCollection<T>? x) => x is null ? null : new(x.Values.ToArray(), DraftProvenance.FromCore(x.Provenance));
    public SourcedCollection<T> ToCore() => new(Values, Provenance.ToCore());
}

internal sealed record DraftListingValues
{
    public DraftListingDetails? Details { get; init; }

    public DraftFact<string>? RegistrationNumber { get; init; }
    public DraftFact<string>? Make { get; init; }
    public DraftFact<string>? Model { get; init; }
    public DraftFact<string>? Variant { get; init; }
    public DraftFact<int>? ModelYear { get; init; }
    public DraftFact<string>? Vin { get; init; }
    public DraftFact<string>? VehicleLabel { get; init; }
    public DraftFact<decimal>? PriceSek { get; init; }
    public DraftFact<decimal>? OdometerKilometres { get; init; }
    public DraftFact<SellerType>? SellerType { get; init; }
    public DraftFact<string>? Locality { get; init; }
    public DraftFact<string>? County { get; init; }
    public DraftFact<DateOnly>? PublishedDate { get; init; }
    public DraftFact<DateOnly>? UpdatedDate { get; init; }
    public DraftFact<int>? ImageCount { get; init; }
    public DraftCollection<FuelType>? FuelTypes { get; init; }
    public DraftFact<Transmission>? Transmission { get; init; }
    public DraftFact<Drivetrain>? Drivetrain { get; init; }
    public DraftFact<BodyType>? BodyType { get; init; }
    public DraftFact<string>? Colour { get; init; }
    public DraftFact<int>? Horsepower { get; init; }
    public DraftFact<decimal>? EngineDisplacementCubicCentimetres { get; init; }
    public DraftCollection<DraftEnergyConsumption>? EnergyConsumptions { get; init; }
    public DraftFact<decimal>? AnnualVehicleTaxSek { get; init; }
    public DraftFact<int>? OwnerCount { get; init; }
    public DraftFact<DateOnly>? FirstRegistrationDate { get; init; }
    public DraftFact<DateOnly>? LastInspectionDate { get; init; }
    public DraftFact<DateOnly>? NextInspectionDate { get; init; }
    public DraftFact<bool>? TowBar { get; init; }
    public DraftCollection<string>? Equipment { get; init; }
    public DraftCollection<string>? SellerClaims { get; init; }
    public DraftCollection<string>? ConditionNotes { get; init; }

    public static DraftListingValues FromCore(ListingDraft x) => new()
    {
        Details = DraftListingDetails.FromCore(x.Details),
        RegistrationNumber = x.RegistrationNumber is null ? null : new(x.RegistrationNumber.Value.Value, DraftProvenance.FromCore(x.RegistrationNumber.Provenance)),
        Make = DraftFact<string>.FromCore(x.Make),
        Model = DraftFact<string>.FromCore(x.Model),
        Variant = DraftFact<string>.FromCore(x.Variant),
        ModelYear = DraftFact<int>.FromCore(x.ModelYear),
        Vin = DraftFact<string>.FromCore(x.Vin),
        VehicleLabel = DraftFact<string>.FromCore(x.VehicleLabel),
        PriceSek = DraftFact<decimal>.FromCore(x.PriceSek),
        OdometerKilometres = DraftFact<decimal>.FromCore(x.OdometerKilometres),
        SellerType = DraftFact<SellerType>.FromCore(x.SellerType),
        Locality = DraftFact<string>.FromCore(x.Locality),
        County = DraftFact<string>.FromCore(x.County),
        PublishedDate = DraftFact<DateOnly>.FromCore(x.PublishedDate),
        UpdatedDate = DraftFact<DateOnly>.FromCore(x.UpdatedDate),
        ImageCount = DraftFact<int>.FromCore(x.ImageCount),
        FuelTypes = DraftCollection<FuelType>.FromCore(x.FuelTypes),
        Transmission = DraftFact<Transmission>.FromCore(x.Transmission),
        Drivetrain = DraftFact<Drivetrain>.FromCore(x.Drivetrain),
        BodyType = DraftFact<BodyType>.FromCore(x.BodyType),
        Colour = DraftFact<string>.FromCore(x.Colour),
        Horsepower = DraftFact<int>.FromCore(x.Horsepower),
        EngineDisplacementCubicCentimetres = DraftFact<decimal>.FromCore(x.EngineDisplacementCubicCentimetres),
        EnergyConsumptions = x.EnergyConsumptions is null ? null : new(x.EnergyConsumptions.Values.Select(DraftEnergyConsumption.FromCore).ToArray(), DraftProvenance.FromCore(x.EnergyConsumptions.Provenance)),
        AnnualVehicleTaxSek = DraftFact<decimal>.FromCore(x.AnnualVehicleTaxSek),
        OwnerCount = DraftFact<int>.FromCore(x.OwnerCount),
        FirstRegistrationDate = DraftFact<DateOnly>.FromCore(x.FirstRegistrationDate),
        LastInspectionDate = DraftFact<DateOnly>.FromCore(x.LastInspectionDate),
        NextInspectionDate = DraftFact<DateOnly>.FromCore(x.NextInspectionDate),
        TowBar = DraftFact<bool>.FromCore(x.TowBar),
        Equipment = DraftCollection<string>.FromCore(x.Equipment),
        SellerClaims = DraftCollection<string>.FromCore(x.SellerClaims),
        ConditionNotes = DraftCollection<string>.FromCore(x.ConditionNotes),
    };

    public ListingDraft ToCore() => new()
    {
        Details = Details?.ToCore(),
        RegistrationNumber = RegistrationNumber is null ? null : new(Core.Vehicles.RegistrationNumber.Parse(RegistrationNumber.Value), RegistrationNumber.Provenance.ToCore()),
        Make = Make?.ToCore(),
        Model = Model?.ToCore(),
        Variant = Variant?.ToCore(),
        ModelYear = ModelYear?.ToCore(),
        Vin = Vin?.ToCore(),
        VehicleLabel = VehicleLabel?.ToCore(),
        PriceSek = PriceSek?.ToCore(),
        OdometerKilometres = OdometerKilometres?.ToCore(),
        SellerType = SellerType?.ToCore(),
        Locality = Locality?.ToCore(),
        County = County?.ToCore(),
        PublishedDate = PublishedDate?.ToCore(),
        UpdatedDate = UpdatedDate?.ToCore(),
        ImageCount = ImageCount?.ToCore(),
        FuelTypes = FuelTypes?.ToCore(),
        Transmission = Transmission?.ToCore(),
        Drivetrain = Drivetrain?.ToCore(),
        BodyType = BodyType?.ToCore(),
        Colour = Colour?.ToCore(),
        Horsepower = Horsepower?.ToCore(),
        EngineDisplacementCubicCentimetres = EngineDisplacementCubicCentimetres?.ToCore(),
        EnergyConsumptions = EnergyConsumptions is null ? null : new(EnergyConsumptions.Values.Select(x => x.ToCore()), EnergyConsumptions.Provenance.ToCore()),
        AnnualVehicleTaxSek = AnnualVehicleTaxSek?.ToCore(),
        OwnerCount = OwnerCount?.ToCore(),
        FirstRegistrationDate = FirstRegistrationDate?.ToCore(),
        LastInspectionDate = LastInspectionDate?.ToCore(),
        NextInspectionDate = NextInspectionDate?.ToCore(),
        TowBar = TowBar?.ToCore(),
        Equipment = Equipment?.ToCore(),
        SellerClaims = SellerClaims?.ToCore(),
        ConditionNotes = ConditionNotes?.ToCore(),
    };
}
