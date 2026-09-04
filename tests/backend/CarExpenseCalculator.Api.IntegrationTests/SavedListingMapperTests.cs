using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Xunit;
using ApiListing = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using ApiManual = CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class SavedListingMapperTests
{
    [Fact]
    public void Request_mapper_preserves_every_field_enum_provenance_and_collection_order()
    {
        var mapped = SavedListingMapper.ToStoreInput(SavedListingTestData.Complete());

        Assert.Equal(" https://EXAMPLE.com/listings/abc12d?campaign=Autumn#details ", mapped.SubmittedUrl);
        Assert.Equal(SavedListingTestData.AnalyzedAtUtc, mapped.AnalyzedAtUtc);
        Assert.Equal("  gpt-5.6-luna  ", mapped.RequestedModel);
        Assert.Equal(2, mapped.PromptVersion);
        Assert.Equal(2, mapped.ExtractionSchemaVersion);
        Assert.Equal(
            ["https://manufacturer.example/vehicle", "https://example.com/listings/abc12d?source=search"],
            mapped.Sources.Select(source => source.Value));

        var listing = mapped.Listing;
        Assert.Equal("ABC12D", listing.RegistrationNumber!.Value.Value);
        Assert.Equal("  Volvo  ", listing.Make!.Value);
        Assert.Equal("V70", listing.Model!.Value);
        Assert.Equal("D4 Momentum", listing.Variant!.Value);
        Assert.Equal(2016, listing.ModelYear!.Value);
        Assert.Equal(" yv1bw1234g1234567 ", listing.Vin!.Value);
        Assert.Equal("  Volvo V70  ", listing.VehicleLabel!.Value);
        Assert.Equal(123_456.123456789012345m, listing.PriceSek!.Value);
        Assert.Equal(185_000.123456789012345m, listing.OdometerKilometres!.Value);
        Assert.Equal(SellerType.Dealer, listing.SellerType!.Value);
        Assert.Equal("  Tenhult  ", listing.Locality!.Value);
        Assert.Equal("Jo\u0308nko\u0308pings la\u0308n", listing.County!.Value);
        Assert.Equal(new DateOnly(2026, 8, 20), listing.PublishedDate!.Value);
        Assert.Equal(new DateOnly(2026, 9, 1), listing.UpdatedDate!.Value);
        Assert.Equal(12, listing.ImageCount!.Value);
        Assert.Equal([FuelType.Diesel, FuelType.Electricity], listing.FuelTypes!.Values);
        Assert.Equal(Transmission.Automatic, listing.Transmission!.Value);
        Assert.Equal(Drivetrain.FrontWheelDrive, listing.Drivetrain!.Value);
        Assert.Equal(BodyType.Wagon, listing.BodyType!.Value);
        Assert.Equal("  Bla\u030a  ", listing.Colour!.Value);
        Assert.Equal(181, listing.Horsepower!.Value);
        Assert.Equal(1969.123456789012345m, listing.EngineDisplacementCubicCentimetres!.Value);
        Assert.Equal(["  Diesel  ", "El"], listing.EnergyConsumptions!.Values.Select(value => value.Label));
        Assert.Equal([5.25m, 18.75m],
            listing.EnergyConsumptions.Values.Select(value => value.ConsumptionPer100Kilometres));
        Assert.Equal(2_400.123456789012345m, listing.AnnualVehicleTaxSek!.Value);
        Assert.Equal(3, listing.OwnerCount!.Value);
        Assert.Equal(new DateOnly(2016, 4, 12), listing.FirstRegistrationDate!.Value);
        Assert.Equal(new DateOnly(2026, 4, 10), listing.LastInspectionDate!.Value);
        Assert.Equal(new DateOnly(2027, 6, 30), listing.NextInspectionDate!.Value);
        Assert.False(listing.TowBar!.Value);
        Assert.Equal(["  Dragkrok  ", "Va\u0308rmare"], listing.Equipment!.Values);
        Assert.Equal(["  Full servicehistorik  ", "Inga kända skulder"], listing.SellerClaims!.Values);
        Assert.Equal(["  Mindre bruksspår  "], listing.ConditionNotes!.Values);
        Assert.Equal(FieldOrigin.Listing, listing.Make.Provenance.Origin);
        Assert.Equal(ExtractionMethod.Ai, listing.Make.Provenance.ExtractionMethod);
        Assert.Equal(VerificationStatus.Unverified, listing.Make.Provenance.Verification);
        Assert.Equal("https://example.com/listings/abc12d?campaign=Autumn",
            listing.Make.Provenance.SourceUrl.Value);
        Assert.Equal(FieldOrigin.User, listing.VehicleLabel.Provenance.Origin);
    }

    [Fact]
    public void Response_mapper_returns_normalized_authoritative_resource_and_summary()
    {
        var input = SavedListingMapper.ToStoreInput(SavedListingTestData.Complete());
        var normalizedUrl = ListingUrl.Parse(input.SubmittedUrl);
        var result = new ListingDraftProcessor().ProcessReviewed(
            normalizedUrl,
            input.Sources,
            input.Listing);
        var createdAt = new DateTimeOffset(2026, 9, 4, 10, 31, 0, TimeSpan.Zero);
        var saved = new SavedListing(
            Guid.CreateVersion7(),
            RegistrationNumber.Parse("ABC12D"),
            3,
            2,
            1,
            input.SubmittedUrl.Trim(),
            normalizedUrl,
            input.AnalyzedAtUtc,
            "gpt-5.6-luna",
            2,
            2,
            result,
            true,
            1,
            true,
            createdAt,
            createdAt.AddMinutes(1));

        var response = SavedListingMapper.ToApi(saved);
        var summary = SavedListingMapper.ToSummaryApi(saved);

        Assert.Equal(saved.VehicleId, response.VehicleId);
        Assert.Equal("ABC12D", response.RegistrationNumber);
        Assert.Equal(3, response.Revision);
        Assert.Equal(2, response.ListingVersion);
        Assert.Equal(1, response.ListingSchemaVersion);
        Assert.Equal("https://example.com/listings/abc12d?campaign=Autumn", response.NormalizedUrl);
        Assert.Equal(ApiListing.ListingAnalysisStatus.Complete, response.Status);
        Assert.Equal([false, true], response.Sources.Select(source => source.MatchesSubmittedUrl));
        Assert.Equal("Volvo", response.Listing.Make!.Value);
        Assert.Equal("Jönköpings län", response.Listing.County!.Value);
        Assert.False(response.Listing.TowBar!.Value);
        Assert.Equal(["Dragkrok", "Värmare"], response.Listing.Equipment!.Values);
        Assert.Empty(response.MissingFields);
        Assert.True(response.HasSavedCostScenario);
        Assert.Equal(1, response.SavedCostScenarioSourceListingVersion);
        Assert.True(response.SavedCostScenarioOutdated);

        Assert.Equal(response.VehicleId, summary.VehicleId);
        Assert.Equal("Volvo V70", summary.VehicleLabel);
        Assert.Equal("Volvo", summary.Make);
        Assert.Equal("V70", summary.Model);
        Assert.Equal(2016, summary.ModelYear);
        Assert.Equal(123_456.123456789012345m, summary.PriceSek);
        Assert.Equal(185_000.123456789012345m, summary.OdometerKilometres);
        Assert.Equal(ApiListing.ListingAnalysisStatus.Complete, summary.Status);
        Assert.Equal(0, summary.MissingFieldCount);
        Assert.True(summary.HasSavedCostScenario);
    }

    [Fact]
    public void Request_mapper_preserves_null_zero_false_and_known_empty_collections()
    {
        var mapped = SavedListingMapper.ToStoreInput(SavedListingTestData.ManualOnly());
        var listing = mapped.Listing;

        Assert.Null(mapped.RequestedModel);
        Assert.Null(mapped.PromptVersion);
        Assert.Null(mapped.ExtractionSchemaVersion);
        Assert.Empty(mapped.Sources);
        Assert.Null(listing.Model);
        Assert.Equal(0m, listing.PriceSek!.Value);
        Assert.Equal(0m, listing.OdometerKilometres!.Value);
        Assert.Equal(0, listing.OwnerCount!.Value);
        Assert.False(listing.TowBar!.Value);
        Assert.Empty(listing.FuelTypes!.Values);
        Assert.Empty(listing.EnergyConsumptions!.Values);
        Assert.Empty(listing.Equipment!.Values);
        Assert.Empty(listing.SellerClaims!.Values);
        Assert.Empty(listing.ConditionNotes!.Values);
    }

    [Fact]
    public void Request_mapper_accumulates_safe_paths_for_invalid_urls()
    {
        var invalid = SavedListingTestData.ManualOnly() with
        {
            SubmittedUrl = "http://127.0.0.1/private",
            Sources = ["file:///tmp/listing", "https://localhost/listing"],
            Draft = SavedListingTestData.EmptyDraft() with
            {
                Make = SavedListingTestData.Value(
                    "Volvo",
                    SavedListingTestData.Manual("https://192.168.1.2/listing")),
            },
        };

        var exception = Assert.Throws<SavedListingRequestMappingException>(
            () => SavedListingMapper.ToStoreInput(invalid));

        Assert.Equal(
            [
                "submittedUrl",
                "sources[0]",
                "sources[1]",
                "draft.make.provenance.sourceUrl",
            ],
            exception.Errors.Select(error => error.Path));
        Assert.All(exception.Errors, error => Assert.DoesNotContain("127.0.0.1", error.Message));
    }

    [Fact]
    public void Request_mapper_supports_every_closed_input_enum_value()
    {
        const string url = "https://example.com/listing";
        var manual = SavedListingTestData.Manual(url);

        foreach (var sellerType in Enum.GetValues<ApiListing.SellerType>())
        {
            var mapped = MapDraft(SavedListingTestData.EmptyDraft() with
            {
                SellerType = SavedListingTestData.Value(sellerType, manual),
            });
            Assert.Equal(sellerType.ToString(), mapped.SellerType!.Value.ToString());
        }

        foreach (var transmission in Enum.GetValues<ApiListing.Transmission>())
        {
            var mapped = MapDraft(SavedListingTestData.EmptyDraft() with
            {
                Transmission = SavedListingTestData.Value(transmission, manual),
            });
            Assert.Equal(transmission.ToString(), mapped.Transmission!.Value.ToString());
        }

        foreach (var drivetrain in Enum.GetValues<ApiListing.Drivetrain>())
        {
            var mapped = MapDraft(SavedListingTestData.EmptyDraft() with
            {
                Drivetrain = SavedListingTestData.Value(drivetrain, manual),
            });
            Assert.Equal(drivetrain.ToString(), mapped.Drivetrain!.Value.ToString());
        }

        foreach (var bodyType in Enum.GetValues<ApiListing.BodyType>())
        {
            var mapped = MapDraft(SavedListingTestData.EmptyDraft() with
            {
                BodyType = SavedListingTestData.Value(bodyType, manual),
            });
            Assert.Equal(bodyType.ToString(), mapped.BodyType!.Value.ToString());
        }

        var fuelMapped = MapDraft(SavedListingTestData.EmptyDraft() with
        {
            FuelTypes = SavedListingTestData.Collection(
                Enum.GetValues<ApiListing.FuelType>(),
                manual),
            EnergyConsumptions = SavedListingTestData.Collection(
                Enum.GetValues<ApiManual.EnergyUnit>()
                    .Select(unit => new CarExpenseCalculator.Api.Contracts.SavedListings.EnergyConsumptionInput
                    {
                        Label = unit.ToString(),
                        Unit = unit,
                        ConsumptionPer100Kilometres = 1m,
                    })
                    .ToArray(),
                manual),
        });
        Assert.Equal(Enum.GetNames<ApiListing.FuelType>(),
            fuelMapped.FuelTypes!.Values.Select(value => value.ToString()));
        Assert.Equal(Enum.GetNames<ApiManual.EnergyUnit>(),
            fuelMapped.EnergyConsumptions!.Values.Select(value => value.Unit.ToString()));
    }

    private static ListingDraft MapDraft(
        CarExpenseCalculator.Api.Contracts.SavedListings.ListingDraftInput draft)
    {
        var request = SavedListingTestData.ManualOnly() with { Draft = draft };
        return SavedListingMapper.ToStoreInput(request).Listing;
    }
}
