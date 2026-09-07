using System.Globalization;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class VehicleFactsProcessorTests
{
    private readonly VehicleFactsProcessor _processor = new();
    private static readonly DateTimeOffset ConfirmedAt = new(2026, 9, 7, 12, 30, 0, TimeSpan.FromHours(2));
    private static readonly ComparisonEvidence Manual = new(FieldOrigin.User, ExtractionMethod.Manual,
        VerificationStatus.UserConfirmed, ConfirmedAt: ConfirmedAt);

    [Theory]
    [InlineData("purchasePriceSek", "0", "100000000")]
    [InlineData("odometerKilometres", "0", "10000000")]
    [InlineData("ownerCount", "0", "10000")]
    [InlineData("seats", "1", "100")]
    [InlineData("modelYear", "1886", "2100")]
    [InlineData("towingCapacityKilograms", "0", "100000")]
    [InlineData("lastServiceOdometerKilometres", "0", "10000000")]
    public void Numeric_fields_accept_inclusive_bounds_and_reject_both_outside_values(string key, string minimum, string maximum)
    {
        var min = decimal.Parse(minimum, CultureInfo.InvariantCulture);
        var max = decimal.Parse(maximum, CultureInfo.InvariantCulture);
        _processor.Normalize(Numeric(key, min));
        _processor.Normalize(Numeric(key, max));

        foreach (var invalid in new[] { min - 1, max + 1 })
        {
            var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
                _processor.Normalize(Numeric(key, invalid))).Errors);
            Assert.Equal($"{key}.observations[0].value", error.Path);
            Assert.Equal("outOfRange", error.Code);
        }
    }

    [Fact]
    public void Numeric_errors_are_aggregated_without_turning_supplied_values_into_unknowns()
    {
        var input = new VehicleComparisonFacts
        {
            PurchasePriceSek = Known(-0.0000000000000000000000000001m),
            Seats = Known(0), ModelYear = Known(2101), OwnerCount = Known(-1),
        };
        var errors = Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(input)).Errors;
        Assert.Equal(4, errors.Count);
        Assert.All(errors, error => Assert.Equal("outOfRange", error.Code));
        Assert.Equal(-0.0000000000000000000000000001m, Value(input.PurchasePriceSek));
    }

    [Theory]
    [InlineData("transmission")]
    [InlineData("bodyType")]
    [InlineData("drivetrain")]
    [InlineData("serviceDocumentation")]
    public void Category_fields_accept_every_supported_value_and_reject_unknown_enum_members(string key)
    {
        var count = key switch
        {
            "transmission" => Enum.GetValues<Transmission>().Length,
            "bodyType" => Enum.GetValues<BodyType>().Length,
            "drivetrain" => Enum.GetValues<Drivetrain>().Length,
            _ => Enum.GetValues<ServiceDocumentationStatus>().Length,
        };
        for (var index = 0; index < count; index++)
        {
            _processor.Normalize(Category(key, index));
        }

        var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
            _processor.Normalize(Category(key, 999))).Errors);
        Assert.Equal($"{key}.observations[0].value", error.Path);
        Assert.Equal("invalidEnum", error.Code);
    }

    [Theory]
    [InlineData("locality", 100)]
    [InlineData("county", 100)]
    [InlineData("serviceNotes", 1000)]
    public void Text_fields_normalize_unicode_and_validate_their_own_bound(string key, int limit)
    {
        var normalized = _processor.Normalize(TextInput(key, "  A\u030A  "));
        Assert.Equal("Å", TextValue(normalized, key));
        Assert.Equal(new string('x', limit), TextValue(_processor.Normalize(TextInput(key, new string('x', limit))), key));
        foreach (var pair in new[] { (new string('x', limit + 1), "tooLong"), ("  ", "required"), ("\ud800", "invalidText") })
        {
            var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
                _processor.Normalize(TextInput(key, pair.Item1))).Errors);
            Assert.Equal($"{key}.observations[0].value", error.Path);
            Assert.Equal(pair.Item2, error.Code);
        }
    }

    [Fact]
    public void Missing_false_zero_empty_absent_and_not_applicable_remain_distinct()
    {
        var result = _processor.Normalize(new()
        {
            TowBar = Known(false), OwnerCount = Known(0), PurchasePriceSek = Known(0m),
            FuelTypes = Known(new FuelTypeSet([])), ServiceDocumentation = Known(ServiceDocumentationStatus.Absent),
            Transmission = VehicleFact<Transmission>.NotApplicable(),
        });
        Assert.False(Value(result.TowBar));
        Assert.Equal(0, Value(result.OwnerCount));
        Assert.Equal(0m, Value(result.PurchasePriceSek));
        Assert.Empty(Value(result.FuelTypes).Values);
        Assert.Equal(ServiceDocumentationStatus.Absent, Value(result.ServiceDocumentation));
        Assert.Equal(VehicleFactState.NotApplicable, result.Transmission!.State);
        Assert.Equal(VehicleFactState.Unknown, result.Seats!.State);
        Assert.Equal(VehicleFactState.Unknown, _processor.Normalize(new()).FuelTypes!.State);
    }

    [Fact]
    public void Lease_without_price_is_not_applicable_but_explicit_price_and_purchase_unknown_survive()
    {
        Assert.Equal(VehicleFactState.NotApplicable, _processor.Normalize(new(), AcquisitionType.Lease).PurchasePriceSek!.State);
        Assert.Equal(VehicleFactState.Unknown, _processor.Normalize(new()).PurchasePriceSek!.State);
        Assert.Equal(0m, Value(_processor.Normalize(new() { PurchasePriceSek = Known(0m) }, AcquisitionType.Lease).PurchasePriceSek));
        Assert.Equal("acquisitionType", Assert.Single(Assert.Throws<VehicleFactsValidationException>(() =>
            _processor.Normalize(new(), (AcquisitionType)42)).Errors).Path);
    }

    [Fact]
    public void All_fuels_are_supported_and_duplicates_and_invalid_values_have_indexed_errors()
    {
        var all = Enum.GetValues<FuelType>();
        Assert.Equal(all, Value(_processor.Normalize(new() { FuelTypes = Known(new FuelTypeSet(all)) }).FuelTypes).Values);
        var errors = Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new()
        {
            FuelTypes = Known(new FuelTypeSet([FuelType.Hydrogen, FuelType.Hydrogen, (FuelType)99])),
        })).Errors;
        Assert.Collection(errors,
            error => { Assert.Equal("fuelTypes.observations[0].value.values[1]", error.Path); Assert.Equal("duplicateValue", error.Code); },
            error => { Assert.Equal("fuelTypes.observations[0].value.values[2]", error.Path); Assert.Equal("invalidEnum", error.Code); });
    }

    [Fact]
    public void Dates_use_full_dateonly_range_and_service_support_does_not_infer_documentation()
    {
        foreach (var date in new[] { DateOnly.MinValue, new DateOnly(2020, 2, 29), DateOnly.MaxValue })
        {
            var result = _processor.Normalize(new()
            {
                InspectionValidThrough = Known(date), LastServiceDate = Known(date),
                LastServiceOdometerKilometres = Known(12345.678901234567890123456789m),
            });
            Assert.Equal(date, Value(result.InspectionValidThrough));
            Assert.Equal(date, Value(result.LastServiceDate));
            Assert.Equal(12345.678901234567890123456789m, Value(result.LastServiceOdometerKilometres));
            Assert.Equal(VehicleFactState.Unknown, result.ServiceDocumentation!.State);
        }
    }

    [Fact]
    public void Conflicts_keep_all_current_values_and_sources_until_explicit_manual_resolution()
    {
        var source = new ComparisonEvidence(FieldOrigin.Listing, ExtractionMethod.Ai, VerificationStatus.Unverified,
            ListingUrl.Parse("https://cars.example/item/1"), ObservedAt: ConfirmedAt.AddDays(-1));
        var conflict = VehicleFact<int>.Conflicting([new(4, source), new(5, Manual)]);
        var result = _processor.Normalize(new() { Seats = conflict });
        Assert.Equal(VehicleFactState.Conflicting, result.Seats!.State);
        Assert.Equal([4, 5], result.Seats.Observations.Select(observation => observation.Value));
        Assert.Equal(source, result.Seats.Observations[0].Evidence);

        var resolved = result.Seats.ResolveWithManual(6, ConfirmedAt.AddDays(1));
        Assert.Equal(6, Value(resolved));
        Assert.Equal(FieldOrigin.User, resolved.Observations[0].Evidence.Origin);
        Assert.Equal(ConfirmedAt.AddDays(1), resolved.Observations[0].Evidence.ConfirmedAt);
        Assert.Null(resolved.Observations[0].Evidence.SourceUrl);
        Assert.Equal(2, conflict.Observations.Count);
        Assert.Throws<VehicleFactsValidationException>(() => Known(4).ResolveWithManual(5, ConfirmedAt));
    }

    [Fact]
    public void Equivalent_normalized_locations_and_fuel_sets_do_not_form_a_conflict()
    {
        var errors = Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new()
        {
            Locality = VehicleFact<string>.Conflicting([new(" A\u030A ", Manual), new("å", Manual)]),
            FuelTypes = VehicleFact<FuelTypeSet>.Conflicting([
                new(new FuelTypeSet([FuelType.Petrol, FuelType.Electricity]), Manual),
                new(new FuelTypeSet([FuelType.Electricity, FuelType.Petrol]), Manual)]),
        })).Errors;
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Equal("invalidConflict", error.Code));
    }

    [Theory]
    [InlineData(VehicleFactState.Known, 0)]
    [InlineData(VehicleFactState.Known, 2)]
    [InlineData(VehicleFactState.Unknown, 1)]
    [InlineData(VehicleFactState.NotApplicable, 1)]
    [InlineData(VehicleFactState.Conflicting, 0)]
    [InlineData(VehicleFactState.Conflicting, 1)]
    [InlineData((VehicleFactState)999, 0)]
    public void Inconsistent_fact_states_are_rejected(VehicleFactState state, int observations)
    {
        var fact = new VehicleFact<int>(state, Enumerable.Range(1, observations).Select(value => new FactObservation<int>(value, Manual)));
        var error = Assert.Single(Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new() { Seats = fact })).Errors);
        Assert.Equal("seats.state", error.Path);
        Assert.Equal("invalidState", error.Code);
    }

    [Fact]
    public void Null_observations_values_and_evidence_are_errors_not_missing_facts()
    {
        var errors = Assert.Throws<VehicleFactsValidationException>(() => _processor.Normalize(new()
        {
            Seats = new(VehicleFactState.Known, [null!]), Locality = Known<string>(null!),
            TowBar = VehicleFact<bool>.Known(false, null!),
        })).Errors;
        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, error => error.Path == "seats.observations[0]");
        Assert.Contains(errors, error => error.Path == "locality.observations[0].value");
        Assert.Contains(errors, error => error.Path == "towBar.observations[0].evidence");
        Assert.All(errors, error => Assert.Equal("required", error.Code));
    }

    [Fact]
    public void Fact_observations_and_fuel_sets_snapshot_callers_collections()
    {
        var fuels = new[] { FuelType.Petrol };
        var set = new FuelTypeSet(fuels);
        var observations = new[] { new FactObservation<FuelTypeSet>(set, Manual) };
        var fact = new VehicleFact<FuelTypeSet>(VehicleFactState.Known, observations);
        fuels[0] = FuelType.Hydrogen;
        observations[0] = new(new FuelTypeSet([]), Manual);
        var result = _processor.Normalize(new() { FuelTypes = fact });
        Assert.Equal([FuelType.Petrol], Value(fact).Values);
        Assert.Equal([FuelType.Petrol], Value(result.FuelTypes).Values);
        Assert.Throws<NotSupportedException>(() => ((IList<FuelType>)set.Values)[0] = FuelType.Diesel);
        Assert.Throws<NotSupportedException>(() => ((IList<FactObservation<FuelTypeSet>>)fact.Observations).Clear());
    }

    [Fact]
    public void Catalogue_separates_source_facts_from_estimated_costs_and_budget_assessments()
    {
        var definitions = ComparisonCriterionCatalog.All;
        Assert.Equal(20, definitions.Count);
        Assert.Equal(20, definitions.Select(definition => definition.Key).Distinct().Count());
        Assert.Equal(15, definitions.Count(definition => definition.Source == ComparisonValueSource.VehicleFact));
        Assert.Equal(["netCostSek", "costPerMonthSek", "costPerMilSek"],
            definitions.Where(definition => definition.Source == ComparisonValueSource.HouseholdCost).Select(definition => definition.Key));
        Assert.Equal(["startupBudget", "monthlyBudget"],
            definitions.Where(definition => definition.Source == ComparisonValueSource.HouseholdBudget).Select(definition => definition.Key));
        Assert.All(definitions, definition => { Assert.NotEmpty(definition.Unit); Assert.NotEmpty(definition.Applicability); });
        Assert.Throws<NotSupportedException>(() => ((IList<ComparisonCriterionDefinition>)definitions).Clear());
    }

    private static VehicleFact<T> Known<T>(T value) where T : notnull => VehicleFact<T>.Known(value, Manual);
    private static T Value<T>(VehicleFact<T>? fact) where T : notnull
    {
        Assert.NotNull(fact);
        Assert.Equal(VehicleFactState.Known, fact.State);
        return Assert.Single(fact.Observations).Value;
    }

    private static VehicleComparisonFacts Numeric(string key, decimal value) => key switch
    {
        "purchasePriceSek" => new() { PurchasePriceSek = Known(value) },
        "odometerKilometres" => new() { OdometerKilometres = Known(value) },
        "ownerCount" => new() { OwnerCount = Known((int)value) },
        "seats" => new() { Seats = Known((int)value) },
        "modelYear" => new() { ModelYear = Known((int)value) },
        "towingCapacityKilograms" => new() { TowingCapacityKilograms = Known((int)value) },
        "lastServiceOdometerKilometres" => new() { LastServiceOdometerKilometres = Known(value) },
        _ => throw new ArgumentException(key),
    };
    private static VehicleComparisonFacts Category(string key, int value) => key switch
    {
        "transmission" => new() { Transmission = Known((Transmission)value) },
        "bodyType" => new() { BodyType = Known((BodyType)value) },
        "drivetrain" => new() { Drivetrain = Known((Drivetrain)value) },
        "serviceDocumentation" => new() { ServiceDocumentation = Known((ServiceDocumentationStatus)value) },
        _ => throw new ArgumentException(key),
    };
    private static VehicleComparisonFacts TextInput(string key, string value) => key switch
    {
        "locality" => new() { Locality = Known(value) },
        "county" => new() { County = Known(value) },
        "serviceNotes" => new() { ServiceNotes = Known(value) },
        _ => throw new ArgumentException(key),
    };
    private static string TextValue(VehicleComparisonFacts facts, string key) => key switch
    {
        "locality" => Value(facts.Locality), "county" => Value(facts.County), _ => Value(facts.ServiceNotes),
    };
}
