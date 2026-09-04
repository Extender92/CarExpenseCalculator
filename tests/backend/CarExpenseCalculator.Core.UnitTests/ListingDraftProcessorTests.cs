using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Core.Vehicles;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ListingDraftProcessorTests
{
    private readonly ListingDraftProcessor _processor = new();
    private readonly ListingUrl _submittedUrl = ListingUrl.Parse("https://cars.example/item/123?ci=2");

    [Fact]
    public void Process_extraction_returns_complete_normalized_listing_and_ordered_sources()
    {
        var provenance = AiProvenance(ListingUrl.Parse("https://cars.example/item/123/"));
        var draft = CompleteDraft(provenance) with
        {
            Make = Value("  Vo\u006Cvo  ", provenance),
            Vin = Value(" vf3wb5fwc33999177 ", provenance),
            VehicleLabel = Value("AI label", provenance),
        };
        var sources = new[]
        {
            ListingUrl.Parse("https://cars.example/other"),
            ListingUrl.Parse("https://cars.example/item/123/?provider=source"),
        };

        var result = _processor.ProcessExtraction(_submittedUrl, sources, draft);

        Assert.Equal(ListingAnalysisStatus.Complete, result.Status);
        Assert.Empty(result.MissingFields);
        Assert.Equal([false, true], result.Sources.Select(source => source.MatchesSubmittedUrl));
        Assert.Equal(sources, result.Sources.Select(source => source.Url));
        Assert.Equal("Volvo", result.Listing.Make!.Value);
        Assert.Equal("VF3WB5FWC33999177", result.Listing.Vin!.Value);
        Assert.Null(result.Listing.VehicleLabel);
        Assert.Equal(_submittedUrl, result.Listing.Make.Provenance.SourceUrl);
        Assert.Equal(FieldOrigin.Listing, result.Listing.Make.Provenance.Origin);
    }

    [Fact]
    public void Process_extraction_discards_invalid_values_and_later_duplicates_individually()
    {
        var provenance = AiProvenance();
        var draft = new ListingDraft
        {
            Make = Value("  Volvo  ", provenance),
            PriceSek = Value(-1m, provenance),
            Equipment = Collection(
                [" AC ", "ac", new string('x', 101), "Dragkrok"],
                provenance),
            FuelTypes = Collection(
                [FuelType.Petrol, FuelType.Petrol, (FuelType)999],
                provenance),
            EnergyConsumptions = Collection(
                [
                    new EnergyConsumption("invalid", EnergyUnit.Litre, 0m),
                    new EnergyConsumption(" Bensin ", EnergyUnit.Litre, 6.5m),
                ],
                provenance),
            ConditionNotes = Collection(["  "], provenance),
        };

        var result = _processor.ProcessExtraction(_submittedUrl, [_submittedUrl], draft);

        Assert.Equal(ListingAnalysisStatus.Partial, result.Status);
        Assert.Equal("Volvo", result.Listing.Make!.Value);
        Assert.Null(result.Listing.PriceSek);
        Assert.Equal(["AC", "Dragkrok"], result.Listing.Equipment!.Values);
        Assert.Equal([FuelType.Petrol], result.Listing.FuelTypes!.Values);
        var consumption = Assert.Single(result.Listing.EnergyConsumptions!.Values);
        Assert.Equal("Bensin", consumption.Label);
        Assert.Null(result.Listing.ConditionNotes);
        Assert.Contains(ListingFieldCode.PriceSek, result.MissingFields);
        Assert.Contains(ListingFieldCode.ConditionNotes, result.MissingFields);
    }

    [Fact]
    public void Process_extraction_normalizes_locality_and_discards_invalid_county_independently()
    {
        var provenance = AiProvenance();
        var draft = new ListingDraft
        {
            Locality = Value("  Te\u006Ehult  ", provenance),
            County = Value(new string('x', 101), provenance),
        };

        var result = _processor.ProcessExtraction(_submittedUrl, [_submittedUrl], draft);

        Assert.Equal(ListingAnalysisStatus.Partial, result.Status);
        Assert.Equal("Tenhult", result.Listing.Locality!.Value);
        Assert.Equal(_submittedUrl, result.Listing.Locality.Provenance.SourceUrl);
        Assert.Null(result.Listing.County);
        Assert.DoesNotContain(ListingFieldCode.Locality, result.MissingFields);
        Assert.Contains(ListingFieldCode.County, result.MissingFields);
    }

    [Fact]
    public void Process_reviewed_validates_locality_and_county_independently()
    {
        var provenance = ManualProvenance();
        var draft = new ListingDraft
        {
            Locality = Value(" ", provenance),
            County = Value(new string('x', 101), provenance),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [], draft));

        Assert.Equal(
            ["locality.value", "county.value"],
            exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Process_extraction_treats_county_without_locality_as_a_usable_fact()
    {
        var provenance = AiProvenance();

        var result = _processor.ProcessExtraction(
            _submittedUrl,
            [_submittedUrl],
            new ListingDraft { County = Value("Jönköpings län", provenance) });

        Assert.Equal(ListingAnalysisStatus.Partial, result.Status);
        Assert.Null(result.Listing.Locality);
        Assert.Equal("Jönköpings län", result.Listing.County!.Value);
        Assert.Contains(ListingFieldCode.Locality, result.MissingFields);
        Assert.DoesNotContain(ListingFieldCode.County, result.MissingFields);
    }

    [Fact]
    public void Process_extraction_preserves_originally_known_empty_collection()
    {
        var provenance = AiProvenance();
        var result = _processor.ProcessExtraction(
            _submittedUrl,
            [_submittedUrl],
            new ListingDraft
            {
                Equipment = Collection<string>([], provenance),
                ImageCount = Value(0, provenance),
                OwnerCount = Value(0, provenance),
                TowBar = Value(false, provenance),
            });

        Assert.Equal(ListingAnalysisStatus.Partial, result.Status);
        Assert.Empty(result.Listing.Equipment!.Values);
        Assert.Equal(0, result.Listing.ImageCount!.Value);
        Assert.Equal(0, result.Listing.OwnerCount!.Value);
        Assert.False(result.Listing.TowBar!.Value);
        Assert.DoesNotContain(ListingFieldCode.Equipment, result.MissingFields);
        Assert.DoesNotContain(ListingFieldCode.ImageCount, result.MissingFields);
        Assert.DoesNotContain(ListingFieldCode.OwnerCount, result.MissingFields);
        Assert.DoesNotContain(ListingFieldCode.TowBar, result.MissingFields);
    }

    [Fact]
    public void Process_extraction_truncates_overlong_collections_in_input_order()
    {
        var provenance = AiProvenance();
        var equipment = Enumerable.Range(0, 101).Select(index => $"Equipment {index}").ToArray();
        var result = _processor.ProcessExtraction(
            _submittedUrl,
            [_submittedUrl],
            new ListingDraft
            {
                Equipment = Collection(equipment, provenance),
                EnergyConsumptions = Collection(
                    [
                        new EnergyConsumption("First", EnergyUnit.Litre, 1m),
                        new EnergyConsumption("Second", EnergyUnit.KilowattHour, 2m),
                        new EnergyConsumption("Third", EnergyUnit.Kilogram, 3m),
                    ],
                    provenance),
            });

        Assert.Equal(100, result.Listing.Equipment!.Values.Count);
        Assert.Equal("Equipment 0", result.Listing.Equipment.Values[0]);
        Assert.Equal("Equipment 99", result.Listing.Equipment.Values[^1]);
        Assert.Equal(["First", "Second"], result.Listing.EnergyConsumptions!.Values.Select(value => value.Label));
    }

    [Fact]
    public void Process_extraction_without_matching_source_discards_all_ai_values()
    {
        var result = _processor.ProcessExtraction(
            _submittedUrl,
            [ListingUrl.Parse("https://cars.example/item/other")],
            CompleteDraft(AiProvenance()));

        Assert.Equal(ListingAnalysisStatus.Unavailable, result.Status);
        Assert.Equal(31, result.MissingFields.Count);
        Assert.Null(result.Listing.Make);
        Assert.Null(result.Listing.Equipment);
    }

    [Fact]
    public void Processing_enforces_active_provenance_by_mode()
    {
        var extraction = _processor.ProcessExtraction(
            _submittedUrl,
            [_submittedUrl],
            new ListingDraft { Make = Value("Manual", ManualProvenance()) });
        Assert.Null(extraction.Listing.Make);

        var reviewed = _processor.ProcessReviewed(
            _submittedUrl,
            [],
            new ListingDraft
            {
                Make = Value("AI", AiProvenance()),
                Model = Value("Manual", ManualProvenance()),
            });
        Assert.Null(reviewed.Listing.Make);
        Assert.Equal("Manual", reviewed.Listing.Model!.Value);
    }

    [Fact]
    public void Process_reviewed_without_matching_source_retains_manual_values_but_stays_unavailable()
    {
        var provenance = ManualProvenance(ListingUrl.Parse("http://cars.example/item/123/"));
        var result = _processor.ProcessReviewed(
            _submittedUrl,
            [],
            new ListingDraft
            {
                Make = Value(" Volvo ", provenance),
                VehicleLabel = Value(" Sommarbil ", provenance),
                PriceSek = Value(0m, provenance),
            });

        Assert.Equal(ListingAnalysisStatus.Unavailable, result.Status);
        Assert.Equal("Volvo", result.Listing.Make!.Value);
        Assert.Equal("Sommarbil", result.Listing.VehicleLabel!.Value);
        Assert.Equal(0m, result.Listing.PriceSek!.Value);
        Assert.Equal(_submittedUrl, result.Listing.Make.Provenance.SourceUrl);
    }

    [Fact]
    public void Process_reviewed_can_complete_a_matched_ai_draft_with_manual_values()
    {
        var ai = AiProvenance();
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            RegistrationNumber = Value(RegistrationNumber.Parse("ABC123"), ai),
            Make = Value("Volvo", ai),
            Model = Value("V70", manual),
            ModelYear = Value(2008, manual),
            PriceSek = Value(20_000m, ai),
            OdometerKilometres = Value(200_000m, manual),
        };

        var result = _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft);

        Assert.Equal(ListingAnalysisStatus.Complete, result.Status);
        Assert.Equal(FieldOrigin.Listing, result.Listing.Make!.Provenance.Origin);
        Assert.Equal(FieldOrigin.User, result.Listing.Model!.Provenance.Origin);
    }

    [Fact]
    public void Process_reviewed_accumulates_validation_errors_with_stable_paths()
    {
        var manual = ManualProvenance();
        var registry = new FieldProvenance(
            FieldOrigin.Registry,
            ExtractionMethod.Manual,
            VerificationStatus.RegistryVerified,
            _submittedUrl);
        var draft = new ListingDraft
        {
            Make = Value(" ", manual),
            PriceSek = Value(-1m, manual),
            SellerType = Value((SellerType)99, manual),
            VehicleLabel = Value("AI label", AiProvenance()),
            Vin = Value("VIN", registry),
            Equipment = Collection(["AC", " ac ", ""], manual),
            EnergyConsumptions = Collection(
                [
                    new EnergyConsumption("", (EnergyUnit)99, 0m),
                    new EnergyConsumption("Bensin", EnergyUnit.Litre, 6m),
                    new EnergyConsumption("El", EnergyUnit.KilowattHour, 20m),
                ],
                manual),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft));

        Assert.Equal(
            [
                "make.value",
                "vin.provenance",
                "priceSek.value",
                "sellerType.value",
                "energyConsumptions.values",
                "energyConsumptions.values[0].label",
                "energyConsumptions.values[0].unit",
                "energyConsumptions.values[0].consumptionPer100Kilometres",
                "equipment.values[1]",
                "equipment.values[2]",
            ],
            exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Process_reviewed_discards_invalid_ai_values_without_rejecting_valid_manual_values()
    {
        var ai = AiProvenance();
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            Make = Value(" ", ai),
            VehicleLabel = Value("AI label", ai),
            PriceSek = Value(-1m, ai),
            Model = Value(" V70 ", manual),
            Equipment = Collection(["AC", " ac "], ai),
            EnergyConsumptions = Collection(
                [
                    new EnergyConsumption(" Bensin ", EnergyUnit.Litre, 6m),
                    new EnergyConsumption("bensin", EnergyUnit.KilowattHour, 20m),
                ],
                ai),
        };

        var result = _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft);

        Assert.Null(result.Listing.Make);
        Assert.Null(result.Listing.VehicleLabel);
        Assert.Null(result.Listing.PriceSek);
        Assert.Equal("V70", result.Listing.Model!.Value);
        Assert.Equal(["AC"], result.Listing.Equipment!.Values);
        Assert.Equal("Bensin", Assert.Single(result.Listing.EnergyConsumptions!.Values).Label);
    }

    [Fact]
    public void Process_reviewed_rejects_duplicate_manual_energy_labels_case_insensitively()
    {
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            EnergyConsumptions = Collection(
                [
                    new EnergyConsumption(" Bensin ", EnergyUnit.Litre, 6m),
                    new EnergyConsumption("bensin", EnergyUnit.KilowattHour, 20m),
                ],
                manual),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft));

        Assert.Equal("energyConsumptions.values[1].label", Assert.Single(exception.Errors).Path);
    }

    [Fact]
    public void Process_reviewed_rejects_provenance_from_another_page()
    {
        var provenance = ManualProvenance(ListingUrl.Parse("https://cars.example/item/other"));

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(
                _submittedUrl,
                [],
                new ListingDraft { Make = Value("Volvo", provenance) }));

        Assert.Equal("make.provenance.sourceUrl", Assert.Single(exception.Errors).Path);
    }

    [Fact]
    public void Process_reviewed_accepts_all_documented_numeric_boundaries()
    {
        var manual = ManualProvenance();
        var draft = CompleteDraft(manual) with
        {
            ModelYear = Value(1886, manual),
            PriceSek = Value(100_000_000m, manual),
            OdometerKilometres = Value(10_000_000m, manual),
            ImageCount = Value(0, manual),
            Horsepower = Value(10_000, manual),
            EngineDisplacementCubicCentimetres = Value(100_000m, manual),
            AnnualVehicleTaxSek = Value(0m, manual),
            OwnerCount = Value(10_000, manual),
            EnergyConsumptions = Collection(
                [new EnergyConsumption("El", EnergyUnit.KilowattHour, 10_000m)],
                manual),
        };

        var result = _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft);

        Assert.Equal(ListingAnalysisStatus.Complete, result.Status);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    public void Process_reviewed_accepts_opposite_numeric_and_collection_boundaries()
    {
        var manual = ManualProvenance();
        var draft = CompleteDraft(manual) with
        {
            Make = Value(new string('m', 100), manual),
            Vin = Value(new string('v', 50), manual),
            ModelYear = Value(2100, manual),
            PriceSek = Value(0m, manual),
            OdometerKilometres = Value(0m, manual),
            ImageCount = Value(10_000, manual),
            Horsepower = Value(1, manual),
            EngineDisplacementCubicCentimetres = Value(1m, manual),
            AnnualVehicleTaxSek = Value(100_000_000m, manual),
            OwnerCount = Value(0, manual),
            Equipment = Collection(Enumerable.Range(0, 100).Select(index => $"E{index}"), manual),
            SellerClaims = Collection(Enumerable.Range(0, 20).Select(index => $"C{index}"), manual),
            ConditionNotes = Collection(Enumerable.Range(0, 10).Select(index => $"N{index}"), manual),
            EnergyConsumptions = Collection(
                [
                    new EnergyConsumption("First", EnergyUnit.Litre, 0.0001m),
                    new EnergyConsumption("Second", EnergyUnit.KilowattHour, 10_000m),
                ],
                manual),
        };

        var result = _processor.ProcessReviewed(_submittedUrl, [_submittedUrl], draft);

        Assert.Equal(ListingAnalysisStatus.Complete, result.Status);
        Assert.Equal(100, result.Listing.Equipment!.Values.Count);
        Assert.Equal(20, result.Listing.SellerClaims!.Values.Count);
        Assert.Equal(10, result.Listing.ConditionNotes!.Values.Count);
        Assert.Equal(2, result.Listing.EnergyConsumptions!.Values.Count);
    }

    [Fact]
    public void Process_reviewed_rejects_all_documented_numeric_ranges()
    {
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            ModelYear = Value(1885, manual),
            PriceSek = Value(100_000_001m, manual),
            OdometerKilometres = Value(-1m, manual),
            ImageCount = Value(10_001, manual),
            Horsepower = Value(0, manual),
            EngineDisplacementCubicCentimetres = Value(0m, manual),
            AnnualVehicleTaxSek = Value(-1m, manual),
            OwnerCount = Value(10_001, manual),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [], draft));

        Assert.Equal(
            [
                "modelYear.value",
                "priceSek.value",
                "odometerKilometres.value",
                "imageCount.value",
                "horsepower.value",
                "engineDisplacementCubicCentimetres.value",
                "annualVehicleTaxSek.value",
                "ownerCount.value",
            ],
            exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Process_reviewed_validates_collection_limits_and_string_lengths()
    {
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            Make = Value(new string('m', 101), manual),
            Vin = Value(new string('v', 51), manual),
            Equipment = Collection(Enumerable.Range(0, 101).Select(index => $"E{index}"), manual),
            SellerClaims = Collection(Enumerable.Range(0, 21).Select(index => $"C{index}"), manual),
            ConditionNotes = Collection(Enumerable.Range(0, 11).Select(index => $"N{index}"), manual),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [], draft));

        Assert.Equal(
            [
                "make.value",
                "vin.value",
                "equipment.values",
                "sellerClaims.values",
                "conditionNotes.values",
            ],
            exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Process_reviewed_validates_each_collection_string_limit()
    {
        var manual = ManualProvenance();
        var draft = new ListingDraft
        {
            Equipment = Collection([new string('e', 101)], manual),
            SellerClaims = Collection([new string('c', 201)], manual),
            ConditionNotes = Collection([new string('n', 301)], manual),
        };

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessReviewed(_submittedUrl, [], draft));

        Assert.Equal(
            ["equipment.values[0]", "sellerClaims.values[0]", "conditionNotes.values[0]"],
            exception.Errors.Select(error => error.Path));
    }

    [Fact]
    public void Process_reviewed_unicode_normalizes_strings_uppercases_vin_and_keeps_date_facts()
    {
        var manual = ManualProvenance();
        var decomposed = "A\u030A";
        var draft = new ListingDraft
        {
            Make = Value($" {decomposed}koda ", manual),
            Vin = Value(" abc123å ", manual),
            LastInspectionDate = Value(new DateOnly(2030, 1, 1), manual),
            NextInspectionDate = Value(new DateOnly(2029, 1, 1), manual),
        };

        var result = _processor.ProcessReviewed(_submittedUrl, [], draft);

        Assert.Equal("Åkoda", result.Listing.Make!.Value);
        Assert.Equal("ABC123Å", result.Listing.Vin!.Value);
        Assert.Equal(new DateOnly(2030, 1, 1), result.Listing.LastInspectionDate!.Value);
        Assert.Equal(new DateOnly(2029, 1, 1), result.Listing.NextInspectionDate!.Value);
    }

    [Fact]
    public void Processing_defensively_copies_and_preserves_collection_order()
    {
        var values = new[] { "First", "Second" };
        var sourceCollection = Collection(values, ManualProvenance());
        values[0] = "Changed";
        var result = _processor.ProcessReviewed(
            _submittedUrl,
            [],
            new ListingDraft { Equipment = sourceCollection });

        Assert.Equal(["First", "Second"], result.Listing.Equipment!.Values);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Listing.Equipment.Values);
    }

    [Fact]
    public void Missing_fields_are_returned_in_exact_stable_order_and_exclude_vehicle_label()
    {
        var result = _processor.ProcessReviewed(
            _submittedUrl,
            [],
            new ListingDraft { VehicleLabel = Value("Only label", ManualProvenance()) });

        Assert.Equal(Enum.GetValues<ListingFieldCode>(), result.MissingFields);
        Assert.Equal(31, result.MissingFields.Count);
    }

    [Fact]
    public void Closed_enums_have_exact_documented_values()
    {
        Assert.Equal(
            [FieldOrigin.Listing, FieldOrigin.User, FieldOrigin.Registry],
            Enum.GetValues<FieldOrigin>());
        Assert.Equal([ExtractionMethod.Ai, ExtractionMethod.Manual], Enum.GetValues<ExtractionMethod>());
        Assert.Equal(
            [VerificationStatus.Unverified, VerificationStatus.UserConfirmed, VerificationStatus.RegistryVerified],
            Enum.GetValues<VerificationStatus>());
        Assert.Equal([SellerType.Private, SellerType.Dealer], Enum.GetValues<SellerType>());
        Assert.Equal(9, Enum.GetValues<FuelType>().Length);
        Assert.Equal([Transmission.Manual, Transmission.Automatic], Enum.GetValues<Transmission>());
        Assert.Equal(3, Enum.GetValues<Drivetrain>().Length);
        Assert.Equal(10, Enum.GetValues<BodyType>().Length);
        Assert.Equal(31, Enum.GetValues<ListingFieldCode>().Length);
    }

    [Fact]
    public void Process_rejects_null_programmer_inputs_and_null_sources()
    {
        Assert.Throws<ArgumentNullException>(
            () => _processor.ProcessExtraction(null!, [], new ListingDraft()));
        Assert.Throws<ArgumentNullException>(
            () => _processor.ProcessExtraction(_submittedUrl, null!, new ListingDraft()));
        Assert.Throws<ArgumentNullException>(
            () => _processor.ProcessExtraction(_submittedUrl, [], null!));

        var exception = Assert.Throws<ListingValidationException>(
            () => _processor.ProcessExtraction(_submittedUrl, [null!], new ListingDraft()));
        Assert.Equal("sources[0]", Assert.Single(exception.Errors).Path);
    }

    private ListingDraft CompleteDraft(FieldProvenance provenance)
    {
        return new ListingDraft
        {
            RegistrationNumber = Value(RegistrationNumber.Parse("ABC123"), provenance),
            Make = Value("Volvo", provenance),
            Model = Value("V70", provenance),
            Variant = Value("2.4", provenance),
            ModelYear = Value(2008, provenance),
            Vin = Value("YV1ABC123", provenance),
            PriceSek = Value(20_000m, provenance),
            OdometerKilometres = Value(200_000m, provenance),
            SellerType = Value(SellerType.Private, provenance),
            Locality = Value("Tenhult", provenance),
            County = Value("Jönköpings län", provenance),
            PublishedDate = Value(new DateOnly(2026, 8, 1), provenance),
            UpdatedDate = Value(new DateOnly(2026, 8, 2), provenance),
            ImageCount = Value(8, provenance),
            FuelTypes = Collection([FuelType.Petrol], provenance),
            Transmission = Value(Transmission.Manual, provenance),
            Drivetrain = Value(Drivetrain.FrontWheelDrive, provenance),
            BodyType = Value(BodyType.Wagon, provenance),
            Colour = Value("Röd", provenance),
            Horsepower = Value(140, provenance),
            EngineDisplacementCubicCentimetres = Value(2_400m, provenance),
            EnergyConsumptions = Collection(
                [new EnergyConsumption("Bensin", EnergyUnit.Litre, 8m)],
                provenance),
            AnnualVehicleTaxSek = Value(2_400m, provenance),
            OwnerCount = Value(3, provenance),
            FirstRegistrationDate = Value(new DateOnly(2008, 1, 1), provenance),
            LastInspectionDate = Value(new DateOnly(2026, 8, 1), provenance),
            NextInspectionDate = Value(new DateOnly(2027, 8, 1), provenance),
            TowBar = Value(true, provenance),
            Equipment = Collection(["AC", "Dragkrok"], provenance),
            SellerClaims = Collection(["Motor går bra"], provenance),
            ConditionNotes = Collection(["Normalt bruksslitage"], provenance),
        };
    }

    private FieldProvenance AiProvenance(ListingUrl? sourceUrl = null)
    {
        return new FieldProvenance(
            FieldOrigin.Listing,
            ExtractionMethod.Ai,
            VerificationStatus.Unverified,
            sourceUrl ?? _submittedUrl);
    }

    private FieldProvenance ManualProvenance(ListingUrl? sourceUrl = null)
    {
        return new FieldProvenance(
            FieldOrigin.User,
            ExtractionMethod.Manual,
            VerificationStatus.UserConfirmed,
            sourceUrl ?? _submittedUrl);
    }

    private static SourcedValue<T> Value<T>(T value, FieldProvenance provenance)
        where T : notnull
    {
        return new SourcedValue<T>(value, provenance);
    }

    private static SourcedCollection<T> Collection<T>(
        IEnumerable<T> values,
        FieldProvenance provenance)
        where T : notnull
    {
        return new SourcedCollection<T>(values, provenance);
    }
}
