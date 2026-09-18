using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Core.Listings;

// Provenance of an applied cost value, not a confirmation of its truth.
// A null version is only valid while bound to a supplied, reviewed draft.
public enum ListingReuseField { PriceSek, AnnualVehicleTaxSek, FuelTypes, EnergyConsumptions }
public sealed record ListingValueSource(string ListingReference, ListingReuseField Field,
    string? OriginalLabel, long? ListingVersion, int? ItemIndex = null);

public sealed record ListingReuseSuggestion<T>(string Key, T Value, ListingValueSource Source,
    bool RequiresReplacement, bool AlreadyApplied) where T : notnull;
public sealed record ListingFactReuseTarget(string Field, bool RequiresReplacement, bool AlreadyApplied);
public sealed record ListingReusePreview(
    ListingReuseSuggestion<decimal>? PurchasePrice,
    IReadOnlyList<ListingReuseSuggestion<Households.HouseholdEnergySource>> EnergySources,
    ListingReuseSuggestion<Households.HouseholdCostItem>? AnnualTax,
    VehicleComparisonFacts Facts,
    IReadOnlyList<ListingFactReuseTarget> FactTargets,
    IReadOnlyList<VehicleFact<string>>? ConditionNotes,
    IReadOnlyList<string> Warnings);

public static class ListingReuse
{
    public static ListingReusePreview Preview(ListingUrl reference, IEnumerable<ListingUrl> observedSources,
        ListingDraft listing, long? listingVersion, Households.VehicleCostInput target,
        VehicleComparisonFacts? currentFacts = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (listingVersion is <= 0) throw new ArgumentOutOfRangeException(nameof(listingVersion));
        var reviewed = new ListingDraftProcessor().ProcessReviewed(reference, observedSources, listing).Listing;
        var facts = new VehicleFactsProcessor().FromReviewedListing(reference, observedSources, reviewed, target.AcquisitionType);
        var warnings = new List<string>();
        ListingValueSource Source(ListingReuseField field, string? label = null, int? index = null) =>
            new(reference.Value, field, label, listingVersion, index);
        ListingReuseSuggestion<decimal>? price = null;
        if (target.AcquisitionType == Households.AcquisitionType.Purchase && reviewed.PriceSek is { } amount)
        {
            var source = Source(ListingReuseField.PriceSek);
            price = new("purchasePriceSek", amount.Value, source, target.PriceSek is not null,
                target.PriceSek == amount.Value && target.PriceSource == source);
        }
        ListingReuseSuggestion<Households.HouseholdCostItem>? tax = null;
        if (reviewed.AnnualVehicleTaxSek is { } annual)
        {
            var source = Source(ListingReuseField.AnnualVehicleTaxSek);
            var old = target.Tax?.Items.FirstOrDefault(x => x.ListingSource?.Field == ListingReuseField.AnnualVehicleTaxSek);
            var item = new Households.HouseholdCostItem(old?.Key ?? "listing-annual-tax", "Fordonsskatt",
                Households.SensitivityValue.Constant(annual.Value), Households.HouseholdCostCadence.Annual,
                SourceUrl: reference.Value, ListingSource: source);
            tax = new("annualVehicleTaxSek", item, source, target.Tax is not null,
                old is not null && old.AmountSek == item.AmountSek && old.Cadence == item.Cadence && old.ListingSource == source);
        }
        var energy = new List<ListingReuseSuggestion<Households.HouseholdEnergySource>>();
        var fuels = reviewed.FuelTypes?.Values;
        foreach (var (fuel, index) in (fuels ?? []).Select((value, index) => (value, index)))
        {
            var existing = target.EnergySources?.FirstOrDefault(x => x.Fuel == fuel);
            var key = existing?.Key ?? "listing-fuel-" + fuel.ToString().ToLowerInvariant();
            var fuelSource = Source(ListingReuseField.FuelTypes, index: index);
            var candidate = new Households.HouseholdEnergySource(key, fuel, null, null, null,
                FuelSource: fuelSource);
            // More than one fuel leaves the pairing/basis unresolved, even if a label looks plausible.
            if (fuels?.Count == 1 && reviewed.EnergyConsumptions?.Values is { Count: 1 } consumptions)
            {
                var consumption = consumptions[0];
                if (fuel != FuelType.Electricity || consumption.Unit == EnergyUnit.KilowattHour)
                    candidate = candidate with
                    {
                        Unit = consumption.Unit,
                        ConsumptionPer100Kilometres = Households.SensitivityValue.Constant(consumption.ConsumptionPer100Kilometres),
                        ConsumptionBasis = Households.ConsumptionBasis.WholeDistance,
                        ConsumptionLabel = consumption.Label,
                        ConsumptionSource = Source(ListingReuseField.EnergyConsumptions, consumption.Label, 0)
                    };
                if (fuel == FuelType.Electricity) warnings.Add("electricityBasisRequired");
            }
            else if (reviewed.EnergyConsumptions is { Values.Count: > 0 }) warnings.Add("consumptionPairingRequired");
            energy.Add(new(key, candidate, fuelSource, target.EnergySources is not null,
                existing is not null && existing.FuelSource == fuelSource &&
                existing.ConsumptionPer100Kilometres == candidate.ConsumptionPer100Kilometres &&
                existing.ConsumptionSource == candidate.ConsumptionSource));
        }
        var targets = new List<ListingFactReuseTarget>();
        void Fact<T>(string field, VehicleFact<T>? proposed, VehicleFact<T>? current) where T : notnull
        {
            if (proposed?.State != VehicleFactState.Known) return;
            var same = current?.State == VehicleFactState.Known &&
                EqualityComparer<T>.Default.Equals(proposed.Observations[0].Value, current.Observations[0].Value) &&
                proposed.Observations[0].Evidence == current.Observations[0].Evidence;
            targets.Add(new(field, current is not null && current.State != VehicleFactState.Unknown, same));
        }
        if (target.AcquisitionType == Households.AcquisitionType.Purchase)
            Fact("purchasePriceSek", facts.PurchasePriceSek, currentFacts?.PurchasePriceSek);
        Fact("odometerKilometres", facts.OdometerKilometres, currentFacts?.OdometerKilometres);
        Fact("ownerCount", facts.OwnerCount, currentFacts?.OwnerCount);
        Fact("towBar", facts.TowBar, currentFacts?.TowBar);
        Fact("transmission", facts.Transmission, currentFacts?.Transmission);
        Fact("seats", facts.Seats, currentFacts?.Seats);
        Fact("modelYear", facts.ModelYear, currentFacts?.ModelYear);
        Fact("fuelTypes", facts.FuelTypes, currentFacts?.FuelTypes);
        Fact("bodyType", facts.BodyType, currentFacts?.BodyType);
        Fact("drivetrain", facts.Drivetrain, currentFacts?.Drivetrain);
        Fact("locality", facts.Locality, currentFacts?.Locality);
        Fact("county", facts.County, currentFacts?.County);
        Fact("towingCapacityKilograms", facts.TowingCapacityKilograms, currentFacts?.TowingCapacityKilograms);
        var notes = reviewed.ConditionNotes is not { } conditions ? null : conditions.Values.Select(value =>
            VehicleFact<string>.Known(value, new(conditions.Provenance.Origin, conditions.Provenance.ExtractionMethod,
                conditions.Provenance.Verification, conditions.Provenance.SourceUrl))).ToArray();
        return new(price, energy.AsReadOnly(), tax, facts, targets.AsReadOnly(), notes, warnings.Distinct().ToArray());
    }
}
