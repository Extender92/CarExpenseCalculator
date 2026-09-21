using CarExpenseCalculator.Core.Households;

namespace CarExpenseCalculator.Core.Listings;

public static class ListingCostSources
{
    public static IEnumerable<(string Path, ListingValueSource Source)> Enumerate(VehicleCostInput input)
    {
        if (input.PriceSource is { } price) yield return ("priceSource", price);
        for (var index = 0; index < (input.EnergySources?.Count ?? 0); index++)
        {
            var item = input.EnergySources![index];
            if (item is null) continue;
            if (item.FuelSource is { } fuel) yield return ($"energySources[{index}].fuelSource", fuel);
            if (item.ConsumptionSource is { } consumption) yield return ($"energySources[{index}].consumptionSource", consumption);
        }
        foreach (var (name, category) in new[] { ("tax", input.Tax), ("insurance", input.Insurance),
            ("service", input.Service), ("repairs", input.Repairs), ("customCosts", input.CustomCosts) })
            for (var i = 0; i < (category?.Items.Count ?? 0); i++)
                if (category!.Items[i]?.ListingSource is { } source) yield return ($"{name}.items[{i}].listingSource", source);
    }

    public static IReadOnlyList<HouseholdInputError> ValidateClaims(VehicleCostInput input, VehicleCostInput? previous,
        ListingDraft? listing, ListingUrl? reference, long? listingVersion)
    {
        var errors = new List<HouseholdInputError>();
        bool Current(ListingValueSource source, ListingReuseField field) =>
            listing is not null && reference is not null && source.Field == field &&
            source.ListingVersion == listingVersion && ListingUrl.TryParse(source.ListingReference, out var url) &&
            url!.HasSamePageIdentity(reference);
        void Invalid(string path) => errors.Add(new(path, "invalidListingSource",
            "A new listing source must match the reviewed listing and the exact supplied value."));
        if (input.PriceSource is { } price &&
            !(previous?.PriceSource == price && previous.PriceSek == input.PriceSek) &&
            !(Current(price, ListingReuseField.PriceSek) && price.ItemIndex is null && price.OriginalLabel is null &&
              input.PriceSek is not null && input.PriceSek == listing?.PriceSek?.Value && input.AcquisitionType == AcquisitionType.Purchase))
            Invalid("priceSource");
        for (var index = 0; index < (input.EnergySources?.Count ?? 0); index++)
        {
            var item = input.EnergySources![index];
            var old = previous?.EnergySources?.FirstOrDefault(x => x.Key == item.Key);
            if (item.FuelSource is { } fuel &&
                !(old?.FuelSource == fuel && old.Fuel == item.Fuel) &&
                !(Current(fuel, ListingReuseField.FuelTypes) && fuel.OriginalLabel is null &&
                  fuel.ItemIndex is >= 0 && fuel.ItemIndex < listing?.FuelTypes?.Values.Count &&
                  item.Fuel == listing.FuelTypes.Values[fuel.ItemIndex.Value]))
                Invalid($"energySources[{index}].fuelSource");
            if (item.ConsumptionSource is { } consumption)
            {
                var observed = consumption.ItemIndex is >= 0 && consumption.ItemIndex < listing?.EnergyConsumptions?.Values.Count
                    ? listing.EnergyConsumptions.Values[consumption.ItemIndex.Value] : null;
                if (!(old?.ConsumptionSource == consumption && old.Unit == item.Unit &&
                    old.ConsumptionPer100Kilometres == item.ConsumptionPer100Kilometres && old.ConsumptionLabel == item.ConsumptionLabel) &&
                    !(Current(consumption, ListingReuseField.EnergyConsumptions) && observed is not null &&
                      item.Unit == observed.Unit && item.ConsumptionPer100Kilometres?.Single == observed.ConsumptionPer100Kilometres &&
                      item.ConsumptionLabel == observed.Label && consumption.OriginalLabel == observed.Label))
                    Invalid($"energySources[{index}].consumptionSource");
            }
        }
        foreach (var (name, category, oldCategory) in new[] { ("tax", input.Tax, previous?.Tax),
            ("insurance", input.Insurance, previous?.Insurance), ("service", input.Service, previous?.Service),
            ("repairs", input.Repairs, previous?.Repairs), ("customCosts", input.CustomCosts, previous?.CustomCosts) })
        {
            for (var index = 0; index < (category?.Items.Count ?? 0); index++)
            {
                var item = category!.Items[index];
                if (item.ListingSource is not { } source) continue;
                var old = oldCategory?.Items.FirstOrDefault(x => x.Key == item.Key);
                if (!(old?.ListingSource == source && old.AmountSek == item.AmountSek && old.Cadence == item.Cadence) &&
                    !(name == "tax" && Current(source, ListingReuseField.AnnualVehicleTaxSek) &&
                      source.ItemIndex is null && source.OriginalLabel is null && listing?.AnnualVehicleTaxSek is { } tax &&
                      item.AmountSek?.Single == tax.Value && item.Cadence == HouseholdCostCadence.Annual))
                    Invalid($"{name}.items[{index}].listingSource");
            }
        }
        return errors.AsReadOnly();
    }

    // Only called after validating the embedded listing and inside the adoption transaction.
    public static VehicleCostInput BindDraft(VehicleCostInput input, long version)
    {
        ListingValueSource? Bind(ListingValueSource? source) =>
            source is { ListingVersion: null } ? source with { ListingVersion = version } : source;
        HouseholdCostCategoryInput? Category(HouseholdCostCategoryInput? category) => category is null ? null
            : category.IsIncluded ? HouseholdCostCategoryInput.Included(category.Items.Select(Item))
            : HouseholdCostCategoryInput.FromItems(category.Items.Select(Item));
        HouseholdCostItem Item(HouseholdCostItem item) => item with { ListingSource = Bind(item.ListingSource) };
        return input with
        {
            PriceSource = Bind(input.PriceSource),
            EnergySources = input.EnergySources?.Select(item => item with
                { FuelSource = Bind(item.FuelSource), ConsumptionSource = Bind(item.ConsumptionSource) }).ToArray(),
            Tax = Category(input.Tax), Insurance = Category(input.Insurance), Service = Category(input.Service),
            Repairs = Category(input.Repairs), CustomCosts = Category(input.CustomCosts)
        };
    }
}
