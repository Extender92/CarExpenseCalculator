using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ListingDetailsTests
{
    private static readonly ListingUrl Url = ListingUrl.Parse("https://cars.example/item/reference?ci=3");
    private static readonly FieldProvenance Ai = new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, Url);
    private static SourcedValue<T> V<T>(T value) where T : notnull => new(value, Ai);
    private static ListingProcessingResult Process(ListingDetails details) => new ListingDraftProcessor().ProcessExtraction(Url, [], new() { Details = details });

    [Fact]
    public void Identifier_alone_is_retained_but_is_not_usable_vehicle_content()
    {
        var result = Process(new() { ListingId = V("26434732") });
        Assert.Equal(ListingAnalysisStatus.Unavailable, result.Status);
        Assert.Equal("26434732", result.Listing.Details!.ListingId!.Value);
    }

    [Fact]
    public void Description_preserves_paragraphs_and_unconfirmed_fields_without_opened_page_metadata()
    {
        var result = Process(new() { Description = V("  Servicebok finns.\r\n\r\nTvå uppsättningar hjul.  "),
            Seats = V(5), WeightKilograms = V(1370m), WeightLabel = V("Vikt"), WeightCategory = V("unspecified"),
            UpdatedLocalDateTime = V("2026-09-08T16:35") });
        Assert.Equal(ListingAnalysisStatus.Partial, result.Status);
        Assert.Empty(result.Sources);
        var d = result.Listing.Details!;
        Assert.Equal("Servicebok finns.\n\nTvå uppsättningar hjul.", d.Description!.Value);
        Assert.Equal("2026-09-08T16:35:00", d.UpdatedLocalDateTime!.Value);
        Assert.Null(d.UpdatedTimeZone);
        Assert.Null(d.UpdatedUtcOffsetMinutes);
        Assert.Equal(VerificationStatus.Unverified, d.Description.Provenance.Verification);
        Assert.Equal("unspecified", d.WeightCategory!.Value);
    }

    [Fact]
    public void Negative_seller_answer_empty_collection_and_unknown_fields_remain_distinct()
    {
        var result = Process(new() { SellerAnswers = [V(new SellerAnswer("Har bilen några skulder?", "Nej"))],
            Specifications = [], Doors = V(0), LuggageLitres = V(0m) }).Listing.Details!;
        Assert.Equal("Nej", Assert.Single(result.SellerAnswers!).Value.Answer);
        Assert.Empty(result.Specifications!);
        Assert.Null(result.WeightKilograms);
        Assert.Equal(0, result.Doors!.Value);
        Assert.Equal(0m, result.LuggageLitres!.Value);
    }

    [Theory]
    [InlineData(32000, true)]
    [InlineData(32001, false)]
    public void Description_limit_is_explicit_and_never_truncates(int length, bool accepted)
    {
        var text = new string('å', length);
        if (accepted) Assert.Equal(text, Process(new() { Description = V(text) }).Listing.Details!.Description!.Value);
        else Assert.Contains(Assert.Throws<ListingValidationException>(() => Process(new() { Description = V(text) })).Errors,
            x => x.Path == "details.description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(101)]
    public void Supplemental_collections_have_no_silent_cutoff(int count)
    {
        var details = new ListingDetails { Specifications = Enumerable.Range(0, count).Select(i => V(new ListingSpecification($"Uppgift {i}", "Åäö"))).ToArray() };
        if (count <= 100) Assert.Equal(count, Process(details).Listing.Details!.Specifications!.Count);
        else Assert.Contains(Assert.Throws<ListingValidationException>(() => Process(details)).Errors, x => x.Path == "details.specifications");
    }

    [Fact]
    public void Caller_arrays_cannot_change_existing_observations()
    {
        var values = new[] { V(new SellerAnswer("Skulder?", "Nej")) };
        var details = new ListingDetails { SellerAnswers = values };
        values[0] = V(new SellerAnswer("Skulder?", "Ja"));
        Assert.Equal("Nej", details.SellerAnswers![0].Value.Answer);
    }

    [Theory]
    [InlineData("unspecified", false)]
    [InlineData("braked", true)]
    [InlineData("unbraked", false)]
    public void Only_explicit_braked_towing_maps_to_the_existing_criterion(string category, bool known)
    {
        var listing = new ListingDraft { Details = new() { Seats = V(5), TrailerWeightKilograms = V(1300m),
            TrailerWeightLabel = V("Max trailervikt"), TrailerWeightCategory = V(category) },
            NextInspectionDate = V(new DateOnly(2027, 9, 14)), SellerClaims = new(["Servicebok finns"], Ai) };
        var facts = new VehicleFactsProcessor().FromReviewedListing(Url, [], listing);
        Assert.Equal(5, facts.Seats!.Observations[0].Value);
        Assert.Equal(known ? VehicleFactState.Known : VehicleFactState.Unknown, facts.TowingCapacityKilograms!.State);
        Assert.Equal(VehicleFactState.Unknown, facts.InspectionValidThrough!.State);
        Assert.Equal(VehicleFactState.Unknown, facts.ServiceDocumentation!.State);
        Assert.Equal(VerificationStatus.Unverified, facts.Seats.Observations[0].Evidence.Verification);
    }

    [Fact]
    public void Numeric_precision_is_preserved_and_invalid_explicit_values_have_paths()
    {
        const decimal quantity = 1370.123456789012345678901234m;
        Assert.Equal(quantity, Process(new() { WeightKilograms = V(quantity) }).Listing.Details!.WeightKilograms!.Value);
        var errors = Assert.Throws<ListingValidationException>(() => Process(new() { Seats = V(101),
            WeightCategory = V("guessed"), UpdatedLocalDateTime = V("2026-09-08T16:35:00Z") })).Errors;
        Assert.Equal(["details.seats", "details.weightCategory", "details.updatedLocalDateTime"], errors.Select(x => x.Path));
    }
}
