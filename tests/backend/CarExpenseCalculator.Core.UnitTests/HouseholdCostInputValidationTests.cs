using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdCostInputValidationTests
{
    [Fact]
    public void Unknown_known_zero_and_included_categories_remain_distinct()
    {
        var result = CostExamples.Calculate(CostExamples.Car() with
        {
            Tax = null, Insurance = HouseholdCostCategoryInput.Included(), Service = HouseholdCostCategoryInput.KnownZero(),
        });
        Assert.Equal(CostSectionState.Unavailable, result.Tax.Cost.State);
        Assert.Null(result.Tax.Cost.CompleteTotalSek);
        Assert.True(result.Insurance.IsIncluded);
        Assert.False(result.Service.IsIncluded);
        Assert.Equal(0m, result.Insurance.Cost.CompleteTotalSek);
        Assert.Equal(0m, result.Service.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Category_and_energy_collections_are_snapshots_of_caller_inputs()
    {
        var items = new List<HouseholdCostItem> { new("item", "Item", CostExamples.Value(100), HouseholdCostCadence.Monthly) };
        var sources = new List<HouseholdEnergySource> { CostExamples.Petrol() };
        var car = CostExamples.Car(sources: sources) with { Service = HouseholdCostCategoryInput.FromItems(items) };
        items.Clear();
        sources.Clear();
        Assert.Single(car.Service.Items);
        Assert.Single(car.EnergySources!);
        Assert.Throws<NotSupportedException>(() => ((IList<HouseholdCostItem>)car.Service.Items).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HouseholdEnergySource>)car.EnergySources!).Clear());
    }

    [Fact]
    public void Duplicate_cost_keys_across_categories_are_structural_but_equal_amounts_are_not_duplicates()
    {
        var service = CostExamples.Category("service", 100, HouseholdCostCadence.Monthly);
        var car = CostExamples.Car() with { Service = service, CustomCosts = service };
        var error = Assert.Throws<HouseholdInputValidationException>(() => CostExamples.Calculate(car));
        Assert.Contains(error.Errors, item => item.Code == "duplicateKey");
        Assert.Equal(2400m, CostExamples.Calculate(car with
        {
            CustomCosts = CostExamples.Category("custom", 100, HouseholdCostCadence.Monthly),
        }).Totals.OwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void Cost_collection_limit_is_enforced(int count, bool rejected)
    {
        var items = Enumerable.Range(0, count).Select(i => new HouseholdCostItem($"item-{i}", "Item", CostExamples.Value(1), HouseholdCostCadence.Monthly));
        var car = CostExamples.Car() with { Service = HouseholdCostCategoryInput.FromItems(items) };
        if (rejected)
            Assert.Contains(Assert.Throws<HouseholdInputValidationException>(() => CostExamples.Calculate(car)).Errors, error => error.Code == "tooManyItems");
        else Assert.Equal(600m, CostExamples.Calculate(car).Service.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Candidate_limits_nulls_and_duplicate_keys_remain_structural_at_the_composition_boundary()
    {
        var calculator = new HouseholdCostCalculator();
        Assert.Empty(calculator.Calculate(CostExamples.Profile(), []).Vehicles);
        Assert.Equal(100, calculator.Calculate(CostExamples.Profile(), Enumerable.Range(0, 100).Select(i => CostExamples.Car($"car-{i}")).ToArray()).Vehicles.Count);
        Assert.Throws<HouseholdInputValidationException>(() => calculator.Calculate(CostExamples.Profile(), Enumerable.Range(0, 101).Select(i => CostExamples.Car($"car-{i}")).ToArray()));
        Assert.Throws<HouseholdInputValidationException>(() => calculator.Calculate(CostExamples.Profile(), [null!]));
        Assert.Throws<HouseholdInputValidationException>(() => calculator.Calculate(CostExamples.Profile(), [CostExamples.Car("car"), CostExamples.Car(" car ")]));
    }

    [Fact]
    public void Energy_limits_null_entries_duplicate_keys_and_unsupported_enums_are_structural()
    {
        var source = CostExamples.Petrol();
        HouseholdEnergySource[][] malformed =
        [
            [source, source with { Key = "second" }, source with { Key = "third" }],
            [null!], [source, source], [source with { Fuel = (FuelType)999 }],
            [source with { Unit = (EnergyUnit)999 }], [source with { ConsumptionBasis = (ConsumptionBasis)999 }],
            [source with { ElectricityBasis = (ElectricityBasis)999 }],
        ];
        foreach (var sources in malformed)
            Assert.Throws<HouseholdInputValidationException>(() => CostExamples.Calculate(CostExamples.Car(sources: sources)));
    }

    [Fact]
    public void Invalid_money_is_local_to_its_item_and_candidate_and_other_known_items_survive()
    {
        var car = CostExamples.Car() with
        {
            Service = HouseholdCostCategoryInput.FromItems([
                new("known", "Known", CostExamples.Value(100), HouseholdCostCadence.Monthly),
                new("invalid", "Invalid", CostExamples.Value(-1), HouseholdCostCadence.Monthly),
            ]),
        };
        var preview = new HouseholdCostCalculator().Calculate(CostExamples.Profile(), [car, CostExamples.Car("other")]);
        Assert.Equal(CostSectionState.Invalid, preview.Vehicles[0].Service.Cost.State);
        Assert.Equal(1200m, preview.Vehicles[0].Service.Cost.KnownSubtotalSek);
        Assert.Equal(0m, preview.Vehicles[0].Financing.CompleteTotalSek);
        Assert.Equal(0m, preview.Vehicles[1].Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Invalid_price_preserves_tax_and_does_not_invalidate_other_candidates()
    {
        var car = CostExamples.Car(price: -1) with { Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual) };
        var preview = new HouseholdCostCalculator().Calculate(CostExamples.Profile(), [car, CostExamples.Car("other")]);
        Assert.Equal(1200m, preview.Vehicles[0].Tax.Cost.CompleteTotalSek);
        Assert.Equal(1200m, preview.Vehicles[0].Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Null(preview.Vehicles[0].Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(0m, preview.Vehicles[1].Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Missing_price_cash_and_period_do_not_become_defaults_or_requirements_for_unrelated_sections()
    {
        var profile = CostExamples.Profile() with { PurchaseCashSek = null };
        var car = CostExamples.Car() with { PriceSek = null, Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual) };
        var result = CostExamples.Calculate(car, profile);
        Assert.Equal(1200m, result.Tax.Cost.CompleteTotalSek);
        Assert.Contains("vehicles[0].priceSek", result.Financing.MissingComponents);
        Assert.Contains("profile.purchaseCashSek", result.Financing.MissingComponents);
        var noPeriod = CostExamples.Calculate(CostExamples.Car(), CostExamples.Profile() with { PeriodMonths = null });
        Assert.Equal(0m, noPeriod.Financing.CompleteTotalSek);
        Assert.Contains("profile.periodMonths", noPeriod.Depreciation.Cost.MissingComponents);
        Assert.Null(noPeriod.Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Missing_in_period_amount_and_cadence_preserve_other_known_cost_items()
    {
        var car = CostExamples.Car() with
        {
            Repairs = HouseholdCostCategoryInput.FromItems([
                new("known", "Known", CostExamples.Value(500), HouseholdCostCadence.Once, 2),
                new("unknown-amount", "Amount missing", null, HouseholdCostCadence.Once, 3),
                new("unknown-cadence", "Cadence missing", CostExamples.Value(200), null),
            ]),
        };
        var result = CostExamples.Calculate(car).Repairs.Cost;
        Assert.Equal(500m, result.KnownSubtotalSek);
        Assert.Null(result.CompleteTotalSek);
        Assert.Contains("vehicles[0].repairs.items[1].amountSek", result.MissingComponents);
        Assert.Contains("vehicles[0].repairs.items[2].cadence", result.MissingComponents);
    }

    [Fact]
    public void Inactive_sensitivity_values_are_validated_without_reordering_or_partial_fallback()
    {
        var car = CostExamples.Car() with
        {
            Residual = HouseholdResidualInput.FixedAmount(SensitivityValue.Scenarios(0, 50_000, 100_001), 12),
            Service = HouseholdCostCategoryInput.FromItems([new("service", "Service", SensitivityValue.Scenarios(1, 2, -1), HouseholdCostCadence.Monthly)]),
            AdditionalRepairAllowancePerMonthSek = SensitivityValue.Scenarios(1, 2, 100_000_001),
        };
        var result = CostExamples.Calculate(car);
        Assert.Equal(3, result.InputErrors.Count);
        Assert.All(result.InputErrors, error => Assert.EndsWith(".cautious", error.Path));
        Assert.Null(result.Depreciation.ResidualValueSek);
        Assert.Equal(CostSectionState.Invalid, result.Service.Cost.State);
        Assert.Equal(CostSectionState.Invalid, result.RepairAllowance.State);
    }

    [Fact]
    public void Unused_calendar_and_budget_errors_remain_visible_without_hiding_accrued_costs()
    {
        var profile = CostExamples.Profile() with { StartMonth = new(2026, 13), MonthlyBudgetSek = -1 };
        var car = CostExamples.Car() with
        {
            Tax = HouseholdCostCategoryInput.FromItems([new("tax", "Tax", CostExamples.Value(1200), HouseholdCostCadence.Annual, DueMonthOfYear: 13)]),
        };
        var preview = new HouseholdCostCalculator().Calculate(profile, [car]);
        Assert.Equal(2, preview.ProfileErrors.Count);
        Assert.Single(preview.Vehicles[0].InputErrors);
        Assert.Equal(1200m, preview.Vehicles[0].Totals.OwnershipCost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void Invalid_event_month_affects_its_cost_but_retains_other_sections(int month)
    {
        var result = CostExamples.Calculate(CostExamples.Car() with { Repairs = CostExamples.Category("repair", 100, HouseholdCostCadence.Once, month) });
        Assert.Equal(CostSectionState.Invalid, result.Repairs.Cost.State);
        Assert.Equal(0m, result.Depreciation.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Labels_evidence_and_known_cadences_use_bounded_existing_validation()
    {
        var valid = new HouseholdCostItem(" key ", new string('a', 120), CostExamples.Value(100_000_000), HouseholdCostCadence.Annual,
            EvidenceNote: new string('a', 1000), SourceUrl: "https://example.com/quote");
        var car = CostExamples.Car() with { Service = HouseholdCostCategoryInput.FromItems([valid]) };
        Assert.Empty(HouseholdCostInputValidator.ValidateVehicle(car));
        HouseholdCostItem[] invalid =
        [
            valid with { Key = " " }, valid with { Key = new string('a', 121) }, valid with { Label = " " },
            valid with { Label = new string('a', 121) }, valid with { EvidenceNote = new string('a', 1001) },
            valid with { SourceUrl = "http://127.0.0.1/quote" }, valid with { SourceUrl = "https://example.com/" + new string('a', 2048) },
            valid with { Cadence = (HouseholdCostCadence)999 }, null!,
        ];
        foreach (var item in invalid)
            Assert.Throws<HouseholdInputValidationException>(() => CostExamples.Calculate(car with { Service = HouseholdCostCategoryInput.FromItems([item]) }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void Invalid_consumption_is_not_guessed_and_does_not_block_cash_financing(int consumption)
    {
        var car = CostExamples.Car(sources: [CostExamples.Petrol() with { ConsumptionPer100Kilometres = CostExamples.Value(consumption) }]);
        var result = CostExamples.Calculate(car, CostExamples.Profile(12, 12_000));
        Assert.Equal(CostSectionState.Invalid, result.Energy.Cost.State);
        Assert.Equal(0m, result.Financing.CompleteTotalSek);
    }

    [Fact]
    public void Electricity_in_a_non_kwh_unit_invalidates_only_its_energy_calculation()
    {
        var result = CostExamples.Calculate(CostExamples.Car(sources: [CostExamples.Electricity() with { Unit = EnergyUnit.Litre }]), CostExamples.Profile(12, 12_000));
        Assert.Contains(result.Energy.Cost.Errors, error => error.Code == "invalidEnergyUnit");
        Assert.Equal(0m, result.Financing.CompleteTotalSek);
    }
}
