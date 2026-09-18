using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class ListingReuseTests
{
    private static readonly ListingUrl Url = ListingUrl.Parse("https://www.blocket.se/mobility/item/123?ci=3");
    private static readonly FieldProvenance Source = new(FieldOrigin.Listing, ExtractionMethod.Html, VerificationStatus.Unverified, Url);
    private static SourcedValue<T> Field<T>(T value) where T : notnull => new(value, Source);
    private static ListingDraft Listing() => new()
    {
        PriceSek = Field(12345.123456789012345678901234m), AnnualVehicleTaxSek = Field(0m),
        FuelTypes = new([FuelType.Petrol], Source), EnergyConsumptions = new([new("NEDC", EnergyUnit.Litre, 8.123456789012345678901234567m)], Source),
        OwnerCount = Field(0), TowBar = Field(false), ConditionNotes = new([], Source),
        NextInspectionDate = Field(new DateOnly(2027, 1, 1)), Equipment = new(["Servicebok finns"], Source),
    };
    private static ListingReusePreview Preview(ListingDraft? listing = null, VehicleCostInput? target = null,
        VehicleComparisonFacts? facts = null, long? version = 2) =>
        ListingReuse.Preview(Url, [], listing ?? Listing(), version, target ?? new("car", null), facts);

    [Fact]
    public void Exact_values_original_label_false_zero_and_empty_are_proposed_without_confirmation()
    {
        var actual = Preview();
        Assert.Equal(12345.123456789012345678901234m, actual.PurchasePrice!.Value);
        Assert.Equal(0m, actual.AnnualTax!.Value.AmountSek!.Single);
        var energy = Assert.Single(actual.EnergySources).Value;
        Assert.Equal(8.123456789012345678901234567m, energy.ConsumptionPer100Kilometres!.Single);
        Assert.Equal("NEDC", energy.ConsumptionLabel);
        Assert.Equal("NEDC", energy.ConsumptionSource!.OriginalLabel);
        Assert.Equal(2, energy.ConsumptionSource.ListingVersion);
        Assert.False(actual.Facts.TowBar!.Observations[0].Value);
        Assert.Equal(0, actual.Facts.OwnerCount!.Observations[0].Value);
        Assert.Equal(VerificationStatus.Unverified, actual.Facts.OwnerCount.Observations[0].Evidence.Verification);
        Assert.Empty(actual.ConditionNotes!);
        Assert.Equal(VehicleFactState.Unknown, actual.Facts.InspectionValidThrough!.State);
        Assert.Equal(VehicleFactState.Unknown, actual.Facts.ServiceDocumentation!.State);
    }

    [Fact]
    public void Missing_costs_are_not_proposed_or_defaulted()
    {
        var result = Preview(new());
        Assert.Null(result.PurchasePrice);
        Assert.Null(result.AnnualTax);
        Assert.Empty(result.EnergySources);
        Assert.Empty(result.FactTargets);
        Assert.Null(result.ConditionNotes);
    }

    [Fact]
    public void Existing_zero_false_and_known_empty_targets_require_explicit_replacement()
    {
        var current = new VehicleFactsProcessor().FromReviewedListing(Url, [], Listing() with { FuelTypes = new([], Source) });
        var result = Preview(target: new("car", 0, []) { Tax = HouseholdCostCategoryInput.KnownZero() }, facts: current);
        Assert.True(result.PurchasePrice!.RequiresReplacement);
        Assert.True(result.AnnualTax!.RequiresReplacement);
        Assert.True(Assert.Single(result.EnergySources).RequiresReplacement);
        Assert.True(result.FactTargets.Single(x => x.Field == "towBar").RequiresReplacement);
        Assert.True(result.FactTargets.Single(x => x.Field == "fuelTypes").RequiresReplacement);
    }

    [Fact]
    public void Repeated_preview_recognizes_exact_applied_values_and_keeps_stable_keys()
    {
        var first = Preview();
        var input = new VehicleCostInput("car", first.PurchasePrice!.Value, first.EnergySources.Select(x => x.Value))
        { PriceSource = first.PurchasePrice.Source, Tax = HouseholdCostCategoryInput.FromItems([first.AnnualTax!.Value]) };
        var again = Preview(target: input, facts: first.Facts);
        Assert.True(again.PurchasePrice!.AlreadyApplied);
        Assert.True(again.AnnualTax!.AlreadyApplied);
        Assert.True(Assert.Single(again.EnergySources).AlreadyApplied);
        Assert.All(again.FactTargets, fact => Assert.True(fact.AlreadyApplied));
        Assert.Equal(first.EnergySources[0].Key, again.EnergySources[0].Key);
        Assert.Equal(first.AnnualTax.Value.Key, again.AnnualTax.Value.Key);
        Assert.Empty(ListingCostSources.ValidateClaims(input, null, Listing(), Url, 2));
    }

    [Fact]
    public void Hybrid_consumption_is_not_paired_or_given_a_driving_basis()
    {
        var result = Preview(Listing() with { FuelTypes = new([FuelType.Petrol, FuelType.Electricity], Source) });
        Assert.Equal(2, result.EnergySources.Count);
        Assert.All(result.EnergySources, row => { Assert.Null(row.Value.ConsumptionPer100Kilometres); Assert.Null(row.Value.ConsumptionBasis); });
        Assert.Equal(["consumptionPairingRequired"], result.Warnings);
    }

    [Fact]
    public void Electric_consumption_keeps_unknown_measurement_basis()
    {
        var result = Preview(Listing() with { FuelTypes = new([FuelType.Electricity], Source),
            EnergyConsumptions = new([new("WLTP", EnergyUnit.KilowattHour, 18m)], Source) });
        Assert.Equal(18m, result.EnergySources[0].Value.ConsumptionPer100Kilometres!.Single);
        Assert.Null(result.EnergySources[0].Value.ElectricityBasis);
        Assert.Contains("electricityBasisRequired", result.Warnings);
    }

    [Fact]
    public void Lease_never_proposes_purchase_price()
    {
        var result = Preview(target: VehicleCostInput.ForLease("lease", null));
        Assert.Null(result.PurchasePrice);
        Assert.DoesNotContain(result.FactTargets, x => x.Field == "purchasePriceSek");
    }

    [Fact]
    public void Claims_reject_changed_values_wrong_sources_and_unknown_versions()
    {
        var p = Preview();
        var input = new VehicleCostInput("car", p.PurchasePrice!.Value) { PriceSource = p.PurchasePrice.Source };
        Assert.NotEmpty(ListingCostSources.ValidateClaims(input with { PriceSek = 0 }, null, Listing(), Url, 2));
        Assert.NotEmpty(ListingCostSources.ValidateClaims(input, null, Listing(), Url, 3));
        Assert.NotEmpty(ListingCostSources.ValidateClaims(input, null, Listing(), ListingUrl.Parse("https://www.blocket.se/mobility/item/999"), 2));
        Assert.NotEmpty(ListingCostSources.ValidateClaims(input, null, null, null, null));
        Assert.Empty(ListingCostSources.ValidateClaims(input, input, null, null, null));
    }

    [Fact]
    public void Cost_claims_follow_keys_when_rows_reorder_and_reject_edited_claimed_values()
    {
        var p = Preview();
        var source = p.EnergySources[0].Value;
        var input = new VehicleCostInput("car", null, [source]);
        var added = new HouseholdEnergySource("manual", FuelType.Diesel, EnergyUnit.Litre, null, null);
        var reordered = input with { EnergySources = [added, source] };
        Assert.Empty(ListingCostSources.ValidateClaims(reordered, input, null, null, null));
        var changed = reordered with { EnergySources = [added, source with { ConsumptionPer100Kilometres = SensitivityValue.Constant(1) }] };
        Assert.Contains(ListingCostSources.ValidateClaims(changed, input, Listing(), Url, 2), x => x.Path == "energySources[1].consumptionSource");
    }

    [Fact]
    public void Embedded_draft_claims_bind_only_unassigned_versions_and_do_not_modify_input()
    {
        var p = Preview(version: null);
        var input = new VehicleCostInput("car", p.PurchasePrice!.Value, p.EnergySources.Select(x => x.Value))
        { PriceSource = p.PurchasePrice.Source, Tax = HouseholdCostCategoryInput.FromItems([p.AnnualTax!.Value]) };
        Assert.Empty(ListingCostSources.ValidateClaims(input, null, Listing(), Url, null));
        var adopted = ListingCostSources.BindDraft(input, 7);
        Assert.All(ListingCostSources.Enumerate(adopted), x => Assert.Equal(7, x.Source.ListingVersion));
        Assert.All(ListingCostSources.Enumerate(input), x => Assert.Null(x.Source.ListingVersion));
        Assert.Equal(input.PriceSek, adopted.PriceSek);
        Assert.Empty(ListingCostSources.ValidateClaims(adopted, null, Listing(), Url, 7));
    }
}
