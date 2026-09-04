using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;

namespace CarExpenseCalculator.Infrastructure.IntegrationTests;

internal static class ListingFactory
{
    public const string SubmittedUrl = " https://EXAMPLE.com/listings/abc123?campaign=Autumn#details ";

    public static SavedListingInput Complete(
        string registrationNumber = "ABC123",
        string? vehicleLabel = "  Volvo V70  ")
    {
        var url = ListingUrl.Parse(SubmittedUrl);
        var ai = Ai(url);
        var manual = Manual(url);

        return new SavedListingInput(
            SubmittedUrl,
            new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero),
            "  gpt-5.6-luna  ",
            2,
            2,
            [
                ListingUrl.Parse("https://example.com/listings/abc123?source=search"),
                ListingUrl.Parse("https://example.com/dealer"),
            ],
            new ListingDraft
            {
                RegistrationNumber = new(
                    RegistrationNumber.Parse(registrationNumber),
                    ai),
                VehicleLabel = vehicleLabel is null ? null : new(vehicleLabel, manual),
                Make = new("  Volvo  ", ai),
                Model = new("V70", ai),
                Variant = new("D4 Momentum", ai),
                ModelYear = new(2016, ai),
                Vin = new(" yv1bw1234g1234567 ", ai),
                PriceSek = new(123_456.123456789012345m, ai),
                OdometerKilometres = new(185_000.123456789012345m, ai),
                SellerType = new(SellerType.Dealer, ai),
                Locality = new("  Tenhult  ", ai),
                County = new("Jo\u0308nko\u0308pings la\u0308n", ai),
                PublishedDate = new(new DateOnly(2026, 8, 20), ai),
                UpdatedDate = new(new DateOnly(2026, 9, 1), ai),
                ImageCount = new(12, ai),
                FuelTypes = new([FuelType.Diesel, FuelType.Electricity], ai),
                Transmission = new(Transmission.Automatic, ai),
                Drivetrain = new(Drivetrain.FrontWheelDrive, ai),
                BodyType = new(BodyType.Wagon, ai),
                Colour = new("  Bla\u030a  ", ai),
                Horsepower = new(181, ai),
                EngineDisplacementCubicCentimetres = new(1969.123456789012345m, ai),
                EnergyConsumptions = new(
                    [
                        new EnergyConsumption("  Diesel  ", EnergyUnit.Litre, 5.25m),
                        new EnergyConsumption("El", EnergyUnit.KilowattHour, 18.75m),
                    ],
                    ai),
                AnnualVehicleTaxSek = new(2_400.123456789012345m, ai),
                OwnerCount = new(3, ai),
                FirstRegistrationDate = new(new DateOnly(2016, 4, 12), ai),
                LastInspectionDate = new(new DateOnly(2026, 4, 10), ai),
                NextInspectionDate = new(new DateOnly(2027, 6, 30), ai),
                TowBar = new(false, ai),
                Equipment = new(["  Dragkrok  ", "Va\u0308rmare"], ai),
                SellerClaims = new(["  Full servicehistorik  ", "Inga kända skulder"], ai),
                ConditionNotes = new(["  Mindre bruksspår  "], ai),
            });
    }

    public static SavedListingInput ManualOnly(
        string? vehicleLabel = null,
        bool knownEmptyCollections = true)
    {
        var url = ListingUrl.Parse("https://example.com/manual/abc123");
        var manual = Manual(url);
        return new SavedListingInput(
            url.Value,
            new DateTimeOffset(2026, 9, 4, 9, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            [],
            new ListingDraft
            {
                VehicleLabel = vehicleLabel is null ? null : new(vehicleLabel, manual),
                Make = new("Saab", manual),
                PriceSek = new(0m, manual),
                OdometerKilometres = new(0m, manual),
                ImageCount = new(0, manual),
                AnnualVehicleTaxSek = new(0m, manual),
                OwnerCount = new(0, manual),
                TowBar = new(false, manual),
                FuelTypes = knownEmptyCollections ? new([], manual) : null,
                EnergyConsumptions = knownEmptyCollections ? new([], manual) : null,
                Equipment = knownEmptyCollections ? new([], manual) : null,
                SellerClaims = knownEmptyCollections ? new([], manual) : null,
                ConditionNotes = knownEmptyCollections ? new([], manual) : null,
            });
    }

    public static FieldProvenance Ai(ListingUrl url) =>
        new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, url);

    public static FieldProvenance Manual(ListingUrl url) =>
        new(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, url);
}
