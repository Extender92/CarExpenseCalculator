using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.ListingExtraction;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class ListingAnalysisTestData
{
    public static readonly DateTimeOffset AnalyzedAtUtc =
        new(2026, 9, 3, 12, 30, 0, TimeSpan.Zero);

    public static ListingExtractionSuccess Complete(ListingUrl submittedUrl)
    {
        var provenance = ListingProvenance(submittedUrl);
        var listing = new ListingDraft
        {
            RegistrationNumber = Value(RegistrationNumber.Parse("ABC12D"), provenance),
            Make = Value("Volvo", provenance),
            Model = Value("V70", provenance),
            Variant = Value("D4 Momentum", provenance),
            ModelYear = Value(2015, provenance),
            Vin = Value("YV1TEST123", provenance),
            VehicleLabel = null,
            PriceSek = Value(89_900.50m, provenance),
            OdometerKilometres = Value(198_765.432m, provenance),
            SellerType = Value(SellerType.Dealer, provenance),
            Location = Value("Göteborg", provenance),
            PublishedDate = Value(new DateOnly(2026, 8, 30), provenance),
            UpdatedDate = Value(new DateOnly(2026, 9, 2), provenance),
            ImageCount = Value(12, provenance),
            FuelTypes = Collection(
                Enum.GetValues<FuelType>(),
                provenance),
            Transmission = Value(Transmission.Automatic, provenance),
            Drivetrain = Value(Drivetrain.FrontWheelDrive, provenance),
            BodyType = Value(BodyType.Wagon, provenance),
            Colour = Value("Blå", provenance),
            Horsepower = Value(181, provenance),
            EngineDisplacementCubicCentimetres = Value(1_969m, provenance),
            EnergyConsumptions = Collection(
                new[]
                {
                    new EnergyConsumption("Diesel", EnergyUnit.Litre, 5.2m),
                    new EnergyConsumption("El", EnergyUnit.KilowattHour, 18.75m),
                },
                provenance),
            AnnualVehicleTaxSek = Value(2_400m, provenance),
            OwnerCount = Value(3, provenance),
            FirstRegistrationDate = Value(new DateOnly(2015, 2, 3), provenance),
            LastInspectionDate = Value(new DateOnly(2026, 2, 3), provenance),
            NextInspectionDate = Value(new DateOnly(2027, 4, 30), provenance),
            TowBar = Value(false, provenance),
            Equipment = Collection(new[] { "Dragkrok", "Stolsvärme" }, provenance),
            SellerClaims = Collection(new[] { "Servad enligt plan" }, provenance),
            ConditionNotes = Collection(new[] { "Normalt bruksslitage" }, provenance),
        };

        return Success(
            submittedUrl,
            ListingAnalysisStatus.Complete,
            listing,
            [],
            [
                new ListingAnalysisSource(ListingUrl.Parse("https://manufacturer.example/vehicle"), false),
                new ListingAnalysisSource(ListingUrl.Parse("https://example.com/item/1"), true),
            ]);
    }

    public static ListingExtractionSuccess Success(
        ListingUrl submittedUrl,
        ListingAnalysisStatus status,
        ListingDraft? listing = null,
        IReadOnlyList<ListingFieldCode>? missingFields = null,
        IReadOnlyList<ListingAnalysisSource>? sources = null)
    {
        return new ListingExtractionSuccess(
            submittedUrl,
            "gpt-5.6-luna",
            1,
            1,
            AnalyzedAtUtc,
            new ListingProcessingResult(
                status,
                sources ?? [new ListingAnalysisSource(submittedUrl, true)],
                listing ?? new ListingDraft(),
                missingFields ?? Enum.GetValues<ListingFieldCode>()));
    }

    public static FieldProvenance ListingProvenance(ListingUrl sourceUrl) =>
        new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, sourceUrl);

    public static SourcedValue<T> Value<T>(T value, FieldProvenance provenance)
        where T : notnull => new(value, provenance);

    public static SourcedCollection<T> Collection<T>(
        IEnumerable<T> values,
        FieldProvenance provenance)
        where T : notnull => new(values, provenance);
}
