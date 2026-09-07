using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class VehicleFactsEvidenceTests
{
    private readonly VehicleFactsProcessor _processor = new();
    private static readonly ListingUrl Url = ListingUrl.Parse("https://cars.example/item/123");
    private static readonly FieldProvenance Listing = new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, Url);
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Reviewed_mapping_preserves_supported_facts_and_evidence_without_import_confirmation()
    {
        var input = new ListingDraft
        {
            RegistrationNumber = Source(RegistrationNumber.Parse("abc 123")),
            PriceSek = Source(12345.678901234567890123456789m), OdometerKilometres = Source(200000.123456789m),
            OwnerCount = Source(0), TowBar = Source(false), Transmission = Source(Transmission.Automatic),
            ModelYear = Source(2022), FuelTypes = new([FuelType.Petrol, FuelType.Electricity], Listing),
            BodyType = Source(BodyType.Wagon), Drivetrain = Source(Drivetrain.AllWheelDrive),
            Locality = Source("  A\u030Are  "), County = Source("  Jämtland  "),
            PublishedDate = Source(new DateOnly(2026, 8, 1)), UpdatedDate = Source(new DateOnly(2026, 9, 1)),
        };
        var result = _processor.FromReviewedListing(Url, [Url], input);
        Assert.Equal(12345.678901234567890123456789m, Observation(result.PurchasePriceSek).Value);
        Assert.Equal(200000.123456789m, Observation(result.OdometerKilometres).Value);
        Assert.Equal(0, Observation(result.OwnerCount).Value);
        Assert.False(Observation(result.TowBar).Value);
        Assert.Equal(Transmission.Automatic, Observation(result.Transmission).Value);
        Assert.Equal(2022, Observation(result.ModelYear).Value);
        Assert.Equal([FuelType.Petrol, FuelType.Electricity], Observation(result.FuelTypes).Value.Values);
        Assert.Equal(BodyType.Wagon, Observation(result.BodyType).Value);
        Assert.Equal(Drivetrain.AllWheelDrive, Observation(result.Drivetrain).Value);
        Assert.Equal("Åre", Observation(result.Locality).Value);
        Assert.Equal("Jämtland", Observation(result.County).Value);
        var evidence = Observation(result.PurchasePriceSek).Evidence;
        Assert.Equal(new ComparisonEvidence(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified, Url), evidence);
        Assert.Null(evidence.ObservedAt);
        Assert.Null(evidence.ConfirmedAt);
        Assert.Equal("ABC123", input.RegistrationNumber.Value.Value);
    }

    [Fact]
    public void Inspection_equipment_service_geography_and_registration_are_not_inferred()
    {
        var result = _processor.FromReviewedListing(Url, [Url], new()
        {
            Locality = Source("Uppsala"), FirstRegistrationDate = Source(new DateOnly(2019, 1, 1)),
            LastInspectionDate = Source(new DateOnly(2026, 1, 1)), NextInspectionDate = Source(new DateOnly(2027, 1, 1)),
            Equipment = new(["Dragkrok", "7 sittplatser", "Bromsad dragvikt 2000 kg"], Listing),
            ConditionNotes = new(["Full servicehistorik, senast servad 2026-01-01 vid 12000 mil"], Listing),
        });
        Assert.Equal(VehicleFactState.Unknown, result.County!.State);
        Assert.Equal(VehicleFactState.Unknown, result.ModelYear!.State);
        Assert.Equal(VehicleFactState.Unknown, result.InspectionValidThrough!.State);
        Assert.Equal(VehicleFactState.Unknown, result.TowBar!.State);
        Assert.Equal(VehicleFactState.Unknown, result.Seats!.State);
        Assert.Equal(VehicleFactState.Unknown, result.TowingCapacityKilograms!.State);
        Assert.Equal(VehicleFactState.Unknown, result.ServiceDocumentation!.State);
        Assert.Equal(VehicleFactState.Unknown, result.LastServiceDate!.State);
        Assert.Equal(VehicleFactState.Unknown, result.LastServiceOdometerKilometres!.State);
        Assert.Equal(VehicleFactState.Unknown, result.ServiceNotes!.State);
    }

    [Fact]
    public void Manual_mapping_retains_old_source_reference_and_unknown_confirmation_time()
    {
        var manual = new FieldProvenance(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, Url);
        var result = _processor.FromReviewedListing(Url, [], new() { TowBar = new(false, manual) });
        Assert.Equal(new ComparisonEvidence(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, Url),
            Observation(result.TowBar).Evidence);
    }

    [Fact]
    public void Manual_facts_need_no_listing_url_and_keep_independent_observation_and_confirmation_times()
    {
        var confirmed = ObservedAt.AddDays(3);
        var fact = VehicleFact<int>.Unknown().ReplaceWithManual(5, confirmed, ObservedAt);
        var result = _processor.Normalize(new() { Seats = fact });
        var evidence = Observation(result.Seats).Evidence;
        Assert.Null(evidence.SourceUrl);
        Assert.Equal(ObservedAt, evidence.ObservedAt);
        Assert.Equal(confirmed, evidence.ConfirmedAt);
        Assert.Equal(VerificationStatus.UserConfirmed, evidence.Verification);
    }

    [Fact]
    public void Manual_replacement_drops_previous_value_evidence_and_does_not_change_unrelated_facts()
    {
        var previous = _processor.Normalize(new()
        {
            Seats = VehicleFact<int>.Known(4, new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified,
                Url, ObservedAt)),
            OwnerCount = VehicleFact<int>.Unknown().ReplaceWithManual(2, ObservedAt),
        });
        var updated = _processor.Normalize(previous with { Seats = previous.Seats!.ReplaceWithManual(5, ObservedAt.AddDays(5)) });
        var evidence = Observation(updated.Seats).Evidence;
        Assert.Equal(4, Observation(previous.Seats).Value);
        Assert.Equal(5, Observation(updated.Seats).Value);
        Assert.Equal(FieldOrigin.User, evidence.Origin);
        Assert.Equal(ExtractionMethod.Manual, evidence.ExtractionMethod);
        Assert.Equal(VerificationStatus.UserConfirmed, evidence.Verification);
        Assert.Equal(ObservedAt.AddDays(5), evidence.ConfirmedAt);
        Assert.Null(evidence.ObservedAt);
        Assert.Null(evidence.SourceUrl);
        Assert.Equal(Observation(previous.OwnerCount).Evidence, Observation(updated.OwnerCount).Evidence);

        var anotherEdit = updated.Seats!.ReplaceWithManual(6, ObservedAt.AddDays(6));
        Assert.Equal(ObservedAt.AddDays(6), Observation(anotherEdit).Evidence.ConfirmedAt);
        Assert.Single(anotherEdit.Observations);
    }

    [Theory]
    [InlineData(FieldOrigin.Registry, ExtractionMethod.Manual, VerificationStatus.RegistryVerified)]
    [InlineData(FieldOrigin.Registry, ExtractionMethod.Ai, VerificationStatus.Unverified)]
    [InlineData(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.RegistryVerified)]
    [InlineData(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.RegistryVerified)]
    [InlineData(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.Unverified)]
    [InlineData(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.UserConfirmed)]
    [InlineData(FieldOrigin.User, ExtractionMethod.Ai, VerificationStatus.UserConfirmed)]
    [InlineData(FieldOrigin.Listing, ExtractionMethod.Manual, VerificationStatus.Unverified)]
    [InlineData((FieldOrigin)999, ExtractionMethod.Manual, VerificationStatus.UserConfirmed)]
    [InlineData(FieldOrigin.User, (ExtractionMethod)999, VerificationStatus.UserConfirmed)]
    [InlineData(FieldOrigin.User, ExtractionMethod.Manual, (VerificationStatus)999)]
    public void Unsupported_and_registry_claims_are_rejected_by_normalization_and_listing_mapping(
        FieldOrigin origin, ExtractionMethod method, VerificationStatus verification)
    {
        var evidence = new ComparisonEvidence(origin, method, verification, Url);
        Assert.Contains(Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new()
        {
            Seats = VehicleFact<int>.Known(5, evidence),
        })).Errors, error => error.Path == "seats.observations[0].evidence" && error.Code == "unsupportedEvidence");
        Assert.Contains(Assert.Throws<VehicleFactsValidationException>(() => _processor.FromReviewedListing(Url, [Url], new()
        {
            OwnerCount = new(2, new(origin, method, verification, Url)),
        })).Errors, error => error.Code == "unsupportedEvidence");
    }

    [Fact]
    public void Listing_evidence_requires_a_source_and_cannot_carry_a_confirmation_timestamp()
    {
        var errors = Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new()
        {
            Seats = VehicleFact<int>.Known(5, new(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified,
                ConfirmedAt: ObservedAt)),
        })).Errors;
        Assert.Collection(errors,
            error => { Assert.Equal("seats.observations[0].evidence.sourceUrl", error.Path); Assert.Equal("required", error.Code); },
            error => { Assert.Equal("seats.observations[0].evidence.confirmedAt", error.Path); Assert.Equal("invalidEvidence", error.Code); });
    }

    [Fact]
    public void Reviewed_mapping_reuses_source_matching_and_does_not_accept_a_different_advertisement()
    {
        var other = ListingUrl.Parse("https://cars.example/item/other");
        Assert.Equal(VehicleFactState.Unknown,
            _processor.FromReviewedListing(Url, [other], new() { OwnerCount = Source(2) }).OwnerCount!.State);
        Assert.Equal(VehicleFactState.Unknown,
            _processor.FromReviewedListing(Url, [Url], new()
            {
                OwnerCount = new(2, Listing with { SourceUrl = other }),
            }).OwnerCount!.State);
        var invalidManual = new FieldProvenance(FieldOrigin.User, ExtractionMethod.Manual, VerificationStatus.UserConfirmed, other);
        Assert.Contains(Assert.Throws<VehicleFactsValidationException>(() => _processor.FromReviewedListing(Url, [], new()
        {
            OwnerCount = new(2, invalidManual),
        })).Errors, error => error.Path == "ownerCount.provenance.sourceUrl" && error.Code == "invalidListing");
    }

    [Fact]
    public void Invalid_supplied_advertised_comparison_values_are_rejected_before_best_effort_listing_normalization()
    {
        var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() => _processor.FromReviewedListing(Url, [Url], new()
        {
            PriceSek = Source(-1m),
        })).Errors);
        Assert.Equal("purchasePriceSek.observations[0].value", error.Path);
        Assert.Equal("outOfRange", error.Code);
    }

    [Fact]
    public void Missing_listing_provenance_produces_a_typed_error()
    {
        var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() => _processor.FromReviewedListing(Url, [Url], new()
        {
            PriceSek = new(1m, null!),
        })).Errors);
        Assert.Equal("purchasePriceSek.observations[0].evidence", error.Path);
        Assert.Equal("required", error.Code);
    }

    private static SourcedValue<T> Source<T>(T value) where T : notnull => new(value, Listing);
    private static FactObservation<T> Observation<T>(VehicleFact<T>? fact) where T : notnull
    {
        Assert.NotNull(fact);
        Assert.Equal(VehicleFactState.Known, fact.State);
        return Assert.Single(fact.Observations);
    }
}
