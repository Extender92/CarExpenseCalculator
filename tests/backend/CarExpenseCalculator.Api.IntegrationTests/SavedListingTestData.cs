using ApiListing = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using ApiManual = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using ApiSaved = CarExpenseCalculator.Api.Contracts.SavedListings;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class SavedListingTestData
{
    public static readonly DateTimeOffset AnalyzedAtUtc =
        new(2026, 9, 4, 10, 30, 0, TimeSpan.Zero);

    public static ApiSaved.ReviewedListingInput Complete(
        string registrationNumber = " abc-12d ",
        string? vehicleLabel = "  Volvo V70  ")
    {
        const string submittedUrl = " https://EXAMPLE.com/listings/abc12d?campaign=Autumn#details ";
        var ai = Ai("https://example.com/listings/abc12d?campaign=Autumn");
        var manual = Manual("https://example.com/listings/abc12d?campaign=Autumn");

        return new ApiSaved.ReviewedListingInput
        {
            SubmittedUrl = submittedUrl,
            AnalyzedAtUtc = AnalyzedAtUtc,
            RequestedModel = "  gpt-5.6-luna  ",
            PromptVersion = 2,
            SchemaVersion = 2,
            Sources =
            [
                "https://manufacturer.example/vehicle",
                "https://example.com/listings/abc12d?source=search",
            ],
            Draft = EmptyDraft() with
            {
                RegistrationNumber = Value(registrationNumber, ai),
                Make = Value("  Volvo  ", ai),
                Model = Value("V70", ai),
                Variant = Value("D4 Momentum", ai),
                ModelYear = Value(2016, ai),
                Vin = Value(" yv1bw1234g1234567 ", ai),
                VehicleLabel = vehicleLabel is null ? null : Value(vehicleLabel, manual),
                PriceSek = Value(123_456.123456789012345m, ai),
                OdometerKilometres = Value(185_000.123456789012345m, ai),
                SellerType = Value(ApiListing.SellerType.Dealer, ai),
                Locality = Value("  Tenhult  ", ai),
                County = Value("Jo\u0308nko\u0308pings la\u0308n", ai),
                PublishedDate = Value(new DateOnly(2026, 8, 20), ai),
                UpdatedDate = Value(new DateOnly(2026, 9, 1), ai),
                ImageCount = Value(12, ai),
                FuelTypes = Collection(
                    [ApiListing.FuelType.Diesel, ApiListing.FuelType.Electricity],
                    ai),
                Transmission = Value(ApiListing.Transmission.Automatic, ai),
                Drivetrain = Value(ApiListing.Drivetrain.FrontWheelDrive, ai),
                BodyType = Value(ApiListing.BodyType.Wagon, ai),
                Colour = Value("  Bla\u030a  ", ai),
                Horsepower = Value(181, ai),
                EngineDisplacementCubicCentimetres = Value(1969.123456789012345m, ai),
                EnergyConsumptions = Collection(
                    [
                        new ApiSaved.EnergyConsumptionInput
                        {
                            Label = "  Diesel  ",
                            Unit = ApiManual.EnergyUnit.Litre,
                            ConsumptionPer100Kilometres = 5.25m,
                        },
                        new ApiSaved.EnergyConsumptionInput
                        {
                            Label = "El",
                            Unit = ApiManual.EnergyUnit.KilowattHour,
                            ConsumptionPer100Kilometres = 18.75m,
                        },
                    ],
                    ai),
                AnnualVehicleTaxSek = Value(2_400.123456789012345m, ai),
                OwnerCount = Value(3, ai),
                FirstRegistrationDate = Value(new DateOnly(2016, 4, 12), ai),
                LastInspectionDate = Value(new DateOnly(2026, 4, 10), ai),
                NextInspectionDate = Value(new DateOnly(2027, 6, 30), ai),
                TowBar = Value(false, ai),
                Equipment = Collection(["  Dragkrok  ", "Va\u0308rmare"], ai),
                SellerClaims = Collection(["  Full servicehistorik  ", "Inga kända skulder"], ai),
                ConditionNotes = Collection(["  Mindre bruksspår  "], ai),
            },
        };
    }

    public static ApiSaved.ReviewedListingInput ManualOnly(
        string? vehicleLabel = null,
        bool knownEmptyCollections = true)
    {
        const string url = "https://example.com/manual/abc123";
        var manual = Manual(url);
        return new ApiSaved.ReviewedListingInput
        {
            SubmittedUrl = url,
            AnalyzedAtUtc = AnalyzedAtUtc,
            RequestedModel = null,
            PromptVersion = null,
            SchemaVersion = null,
            Sources = [],
            Draft = EmptyDraft() with
            {
                VehicleLabel = vehicleLabel is null ? null : Value(vehicleLabel, manual),
                Make = Value("Saab", manual),
                PriceSek = Value(0m, manual),
                OdometerKilometres = Value(0m, manual),
                ImageCount = Value(0, manual),
                AnnualVehicleTaxSek = Value(0m, manual),
                OwnerCount = Value(0, manual),
                TowBar = Value(false, manual),
                FuelTypes = knownEmptyCollections
                    ? Collection<ApiListing.FuelType>([], manual)
                    : null,
                EnergyConsumptions = knownEmptyCollections
                    ? Collection<ApiSaved.EnergyConsumptionInput>([], manual)
                    : null,
                Equipment = knownEmptyCollections ? Collection<string>([], manual) : null,
                SellerClaims = knownEmptyCollections ? Collection<string>([], manual) : null,
                ConditionNotes = knownEmptyCollections ? Collection<string>([], manual) : null,
            },
        };
    }

    public static ApiSaved.ListingDraftInput EmptyDraft()
    {
        return new ApiSaved.ListingDraftInput
        {
            RegistrationNumber = null,
            Make = null,
            Model = null,
            Variant = null,
            ModelYear = null,
            Vin = null,
            VehicleLabel = null,
            PriceSek = null,
            OdometerKilometres = null,
            SellerType = null,
            Locality = null,
            County = null,
            PublishedDate = null,
            UpdatedDate = null,
            ImageCount = null,
            FuelTypes = null,
            Transmission = null,
            Drivetrain = null,
            BodyType = null,
            Colour = null,
            Horsepower = null,
            EngineDisplacementCubicCentimetres = null,
            EnergyConsumptions = null,
            AnnualVehicleTaxSek = null,
            OwnerCount = null,
            FirstRegistrationDate = null,
            LastInspectionDate = null,
            NextInspectionDate = null,
            TowBar = null,
            Equipment = null,
            SellerClaims = null,
            ConditionNotes = null,
        };
    }

    public static ApiSaved.SourcedValueInput<T> Value<T>(
        T value,
        ApiSaved.FieldProvenanceInput provenance)
        where T : notnull => new() { Value = value, Provenance = provenance };

    public static ApiSaved.SourcedCollectionInput<T> Collection<T>(
        IReadOnlyList<T> values,
        ApiSaved.FieldProvenanceInput provenance)
        where T : notnull => new() { Values = values, Provenance = provenance };

    public static ApiSaved.FieldProvenanceInput Ai(string sourceUrl) => new()
    {
        Origin = ApiListing.FieldOrigin.Listing,
        ExtractionMethod = ApiListing.ExtractionMethod.Ai,
        Verification = ApiListing.VerificationStatus.Unverified,
        SourceUrl = sourceUrl,
    };

    public static ApiSaved.FieldProvenanceInput Manual(string sourceUrl) => new()
    {
        Origin = ApiListing.FieldOrigin.User,
        ExtractionMethod = ApiListing.ExtractionMethod.Manual,
        Verification = ApiListing.VerificationStatus.UserConfirmed,
        SourceUrl = sourceUrl,
    };
}
