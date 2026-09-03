using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using Xunit;
using ApiListing = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using ApiManual = CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.IntegrationTests;

public sealed class ListingAnalysisMapperTests
{
    [Fact]
    public void Complete_result_maps_every_listing_field_without_losing_precision_or_order()
    {
        var submittedUrl = ListingUrl.Parse(" HTTP://EXAMPLE.COM:80/item/1?CI=2#details ");

        var result = ListingAnalysisMapper.ToApi(ListingAnalysisTestData.Complete(submittedUrl));

        Assert.Equal("HTTP://EXAMPLE.COM:80/item/1?CI=2#details", result.SubmittedUrl);
        Assert.Equal("http://example.com/item/1?CI=2", result.NormalizedUrl);
        Assert.Equal("gpt-5.6-luna", result.RequestedModel);
        Assert.Equal(ListingAnalysisTestData.AnalyzedAtUtc, result.AnalyzedAtUtc);
        Assert.Equal(["https://manufacturer.example/vehicle", "https://example.com/item/1"],
            result.Sources.Select(source => source.Url));
        Assert.Equal([false, true], result.Sources.Select(source => source.MatchesSubmittedUrl));

        var listing = result.Listing;
        Assert.Equal("ABC12D", listing.RegistrationNumber!.Value);
        Assert.Equal("Volvo", listing.Make!.Value);
        Assert.Equal("V70", listing.Model!.Value);
        Assert.Equal("D4 Momentum", listing.Variant!.Value);
        Assert.Equal(2015, listing.ModelYear!.Value);
        Assert.Equal("YV1TEST123", listing.Vin!.Value);
        Assert.Null(listing.VehicleLabel);
        Assert.Equal(89_900.50m, listing.PriceSek!.Value);
        Assert.Equal(198_765.432m, listing.OdometerKilometres!.Value);
        Assert.Equal(ApiListing.SellerType.Dealer, listing.SellerType!.Value);
        Assert.Equal("Göteborg", listing.Location!.Value);
        Assert.Equal(new DateOnly(2026, 8, 30), listing.PublishedDate!.Value);
        Assert.Equal(new DateOnly(2026, 9, 2), listing.UpdatedDate!.Value);
        Assert.Equal(12, listing.ImageCount!.Value);
        Assert.Equal(9, listing.FuelTypes!.Values.Count);
        Assert.Equal(ApiListing.Transmission.Automatic, listing.Transmission!.Value);
        Assert.Equal(ApiListing.Drivetrain.FrontWheelDrive, listing.Drivetrain!.Value);
        Assert.Equal(ApiListing.BodyType.Wagon, listing.BodyType!.Value);
        Assert.Equal("Blå", listing.Colour!.Value);
        Assert.Equal(181, listing.Horsepower!.Value);
        Assert.Equal(1_969m, listing.EngineDisplacementCubicCentimetres!.Value);
        Assert.Equal(["Diesel", "El"], listing.EnergyConsumptions!.Values.Select(value => value.Label));
        Assert.Equal(
            [ApiManual.EnergyUnit.Litre,
             ApiManual.EnergyUnit.KilowattHour],
            listing.EnergyConsumptions.Values.Select(value => value.Unit));
        Assert.Equal([5.2m, 18.75m],
            listing.EnergyConsumptions.Values.Select(value => value.ConsumptionPer100Kilometres));
        Assert.Equal(2_400m, listing.AnnualVehicleTaxSek!.Value);
        Assert.Equal(3, listing.OwnerCount!.Value);
        Assert.Equal(new DateOnly(2015, 2, 3), listing.FirstRegistrationDate!.Value);
        Assert.Equal(new DateOnly(2026, 2, 3), listing.LastInspectionDate!.Value);
        Assert.Equal(new DateOnly(2027, 4, 30), listing.NextInspectionDate!.Value);
        Assert.False(listing.TowBar!.Value);
        Assert.Equal(["Dragkrok", "Stolsvärme"], listing.Equipment!.Values);
        Assert.Equal(["Servad enligt plan"], listing.SellerClaims!.Values);
        Assert.Equal(["Normalt bruksslitage"], listing.ConditionNotes!.Values);
        Assert.Equal(ApiListing.FieldOrigin.Listing, listing.Make.Provenance.Origin);
        Assert.Equal(ApiListing.ExtractionMethod.Ai,
            listing.Make.Provenance.ExtractionMethod);
        Assert.Equal(ApiListing.VerificationStatus.Unverified,
            listing.Make.Provenance.Verification);
        Assert.Equal(submittedUrl.Value, listing.Make.Provenance.SourceUrl);
    }

    [Fact]
    public void Mapper_preserves_all_closed_enum_values_and_missing_field_order()
    {
        var submittedUrl = ListingUrl.Parse("https://example.com/item/1");
        var listingProvenance = ListingAnalysisTestData.ListingProvenance(submittedUrl);

        foreach (var status in Enum.GetValues<ListingAnalysisStatus>())
        {
            var response = ListingAnalysisMapper.ToApi(
                ListingAnalysisTestData.Success(submittedUrl, status));
            Assert.Equal(status.ToString(), response.Status.ToString());
        }

        foreach (var sellerType in Enum.GetValues<SellerType>())
        {
            var listing = new ListingDraft
            {
                SellerType = ListingAnalysisTestData.Value(sellerType, listingProvenance),
            };
            var response = ListingAnalysisMapper.ToApi(
                ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, listing));
            Assert.Equal(sellerType.ToString(), response.Listing.SellerType!.Value.ToString());
        }

        foreach (var transmission in Enum.GetValues<Transmission>())
        {
            var listing = new ListingDraft
            {
                Transmission = ListingAnalysisTestData.Value(transmission, listingProvenance),
            };
            var response = ListingAnalysisMapper.ToApi(
                ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, listing));
            Assert.Equal(transmission.ToString(), response.Listing.Transmission!.Value.ToString());
        }

        foreach (var drivetrain in Enum.GetValues<Drivetrain>())
        {
            var listing = new ListingDraft
            {
                Drivetrain = ListingAnalysisTestData.Value(drivetrain, listingProvenance),
            };
            var response = ListingAnalysisMapper.ToApi(
                ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, listing));
            Assert.Equal(drivetrain.ToString(), response.Listing.Drivetrain!.Value.ToString());
        }

        foreach (var bodyType in Enum.GetValues<BodyType>())
        {
            var listing = new ListingDraft
            {
                BodyType = ListingAnalysisTestData.Value(bodyType, listingProvenance),
            };
            var response = ListingAnalysisMapper.ToApi(
                ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, listing));
            Assert.Equal(bodyType.ToString(), response.Listing.BodyType!.Value.ToString());
        }

        var fuelListing = new ListingDraft
        {
            FuelTypes = ListingAnalysisTestData.Collection(Enum.GetValues<FuelType>(), listingProvenance),
            EnergyConsumptions = ListingAnalysisTestData.Collection(
                Enum.GetValues<EnergyUnit>().Select(unit => new EnergyConsumption(unit.ToString(), unit, 1m)),
                listingProvenance),
        };
        var fuelResponse = ListingAnalysisMapper.ToApi(
            ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, fuelListing));
        Assert.Equal(Enum.GetNames<FuelType>(), fuelResponse.Listing.FuelTypes!.Values.Select(value => value.ToString()));
        Assert.Equal(Enum.GetNames<EnergyUnit>(),
            fuelResponse.Listing.EnergyConsumptions!.Values.Select(value => value.Unit.ToString()));

        var allMissing = Enum.GetValues<ListingFieldCode>();
        var missingResponse = ListingAnalysisMapper.ToApi(
            ListingAnalysisTestData.Success(
                submittedUrl,
                ListingAnalysisStatus.Unavailable,
                missingFields: allMissing));
        Assert.Equal(Enum.GetNames<ListingFieldCode>(),
            missingResponse.MissingFields.Select(value => value.ToString()));
    }

    [Fact]
    public void Mapper_preserves_reserved_provenance_enum_values()
    {
        var submittedUrl = ListingUrl.Parse("https://example.com/item/1");
        var provenance = new FieldProvenance(
            FieldOrigin.Registry,
            ExtractionMethod.Manual,
            VerificationStatus.RegistryVerified,
            submittedUrl);
        var userProvenance = new FieldProvenance(
            FieldOrigin.User,
            ExtractionMethod.Manual,
            VerificationStatus.UserConfirmed,
            submittedUrl);
        var listing = new ListingDraft
        {
            Make = ListingAnalysisTestData.Value("Volvo", provenance),
            Model = ListingAnalysisTestData.Value("V70", userProvenance),
        };

        var response = ListingAnalysisMapper.ToApi(
            ListingAnalysisTestData.Success(submittedUrl, ListingAnalysisStatus.Partial, listing));

        Assert.Equal(ApiListing.FieldOrigin.Registry,
            response.Listing.Make!.Provenance.Origin);
        Assert.Equal(ApiListing.ExtractionMethod.Manual,
            response.Listing.Make.Provenance.ExtractionMethod);
        Assert.Equal(ApiListing.VerificationStatus.RegistryVerified,
            response.Listing.Make.Provenance.Verification);
        Assert.Equal(ApiListing.FieldOrigin.User, response.Listing.Model!.Provenance.Origin);
        Assert.Equal(ApiListing.ExtractionMethod.Manual,
            response.Listing.Model.Provenance.ExtractionMethod);
        Assert.Equal(ApiListing.VerificationStatus.UserConfirmed,
            response.Listing.Model.Provenance.Verification);
    }
}
