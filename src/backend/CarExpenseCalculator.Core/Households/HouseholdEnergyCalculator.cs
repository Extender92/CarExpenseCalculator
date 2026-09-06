using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

internal static class HouseholdEnergyCalculator
{
    public static (CostSection Cost, HouseholdEnergyResult Result) Calculate(
        IReadOnlyList<HouseholdEnergySource>? sources, string path, CostSection distance, HouseholdCostContext context,
        out IReadOnlyList<CostSection> rawSources)
    {
        var total = new CostSection(path);
        var rows = new List<HouseholdEnergySourceResult>();
        var costs = new List<CostSection>();
        if (distance.Complete == 0m)
        {
            total.Add(0m);
            costs.Add(total);
            foreach (var source in sources ?? [])
                rows.Add(new(source.Key.Trim(), source.Fuel, source.Unit, 0m, 0m, null, total.Result()));
        }
        else if (!context.Available(path, sources is { Count: > 0 }, total))
        {
            total.CopyProblems(distance);
            costs.Add(total);
        }
        else
        {
            for (var index = 0; index < sources!.Count; index++)
            {
                var (cost, row) = CalculateSource(sources, index, path, distance, context);
                total.Merge(cost);
                costs.Add(cost);
                rows.Add(row);
            }
        }

        rawSources = costs.AsReadOnly();
        return (total, new(total.Result(), rows.AsReadOnly()));
    }

    private static (CostSection Cost, HouseholdEnergySourceResult Result) CalculateSource(
        IReadOnlyList<HouseholdEnergySource> sources, int index, string path, CostSection distance, HouseholdCostContext context)
    {
        var source = sources[index];
        var sourcePath = $"{path}[{index}]";
        var quantity = new CostSection(sourcePath);
        var basisAvailable = context.Available($"{sourcePath}.consumptionBasis", source.ConsumptionBasis is not null, quantity);
        decimal? share = null;
        if (basisAvailable)
        {
            if (source.ConsumptionBasis == ConsumptionBasis.WholeDistance || sources.Count == 1) share = 100m;
            else
            {
                var fuelsAvailable = true;
                for (var i = 0; i < sources.Count; i++)
                    fuelsAvailable &= context.Available($"{path}[{i}].fuel", sources[i].Fuel is not null, quantity);
                var electricShare = context.Value(context.Profile.ElectricDrivingSharePercent, "profile.electricDrivingSharePercent", quantity);
                if (fuelsAvailable && electricShare is not null)
                    share = source.Fuel == FuelType.Electricity ? electricShare : 100m - electricShare;
            }
        }

        if (share == 0m)
        {
            // No driving in this mode needs neither consumption nor purchase prices.
            var zero = new CostSection(sourcePath);
            zero.CopyProblems(quantity);
            zero.Add(0m);
            return (zero, new(source.Key.Trim(), source.Fuel, source.Unit, 0m, 0m, null, zero.Result()));
        }

        quantity.CopyProblems(distance);
        var fuelAvailable = context.Available($"{sourcePath}.fuel", source.Fuel is not null, quantity);
        var unitAvailable = context.Available($"{sourcePath}.unit", source.Unit is not null, quantity);
        var consumption = context.Value(source.ConsumptionPer100Kilometres, $"{sourcePath}.consumptionPer100Kilometres", quantity);
        decimal? baseQuantity = null;
        if (unitAvailable && share is not null && consumption is not null && distance.Complete is { } kilometres)
        {
            baseQuantity = kilometres * share.Value / 100m * consumption.Value / 100m;
            if (baseQuantity == 0m)
            {
                quantity.Errors.Add(new(sourcePath, "calculationOutOfRange", "Positive energy use is below decimal precision."));
                baseQuantity = null;
            }
        }

        decimal? purchased = null;
        if (fuelAvailable)
        {
            if (source.Fuel != FuelType.Electricity) purchased = baseQuantity;
            else if (context.Available($"{sourcePath}.electricityBasis", source.ElectricityBasis is not null, quantity))
            {
                if (source.ElectricityBasis == ElectricityBasis.Metered) purchased = baseQuantity;
                else
                {
                    var loss = context.Value(context.Profile.ChargingLossPercent, "profile.chargingLossPercent", quantity);
                    if (baseQuantity is not null && loss is not null)
                        purchased = quantity.Arithmetic(() => baseQuantity.Value / (1m - loss.Value / 100m));
                }
            }
        }

        var price = CalculatePrice(source, sourcePath, context);
        var cost = new CostSection(sourcePath);
        cost.HasDetails = baseQuantity is not null || purchased is not null || price.HasKnown;
        cost.CopyProblems(quantity);
        cost.CopyProblems(price);
        if (purchased is not null) cost.AddTransformed(price, value => value * purchased.Value);
        return (cost, new(source.Key.Trim(), source.Fuel, source.Unit, CostSection.Quantity(baseQuantity),
            CostSection.Quantity(purchased), CostSection.Money(price.Complete), cost.Result()));
    }

    private static CostSection CalculatePrice(HouseholdEnergySource source, string path, HouseholdCostContext context)
    {
        var price = new CostSection(path);
        if (!context.Available($"{path}.fuel", source.Fuel is not null, price)) return price;
        if (source.Fuel == FuelType.Electricity)
        {
            var homeShare = context.Value(context.Profile.HomeChargingSharePercent, "profile.homeChargingSharePercent", price);
            if (homeShare is not null)
            {
                AddPrice(context.Profile.HomeChargingPricePerKilowattHourSek, "profile.homeChargingPricePerKilowattHourSek", homeShare.Value);
                AddPrice(context.Profile.PublicChargingPricePerKilowattHourSek, "profile.publicChargingPricePerKilowattHourSek", 100m - homeShare.Value);
            }
        }
        else if (context.Available($"{path}.unit", source.Unit is not null, price))
        {
            var index = -1;
            for (var i = 0; i < context.Profile.EnergyPrices.Count; i++)
                if (context.Profile.EnergyPrices[i].Fuel == source.Fuel && context.Profile.EnergyPrices[i].Unit == source.Unit) index = i;
            if (index < 0) price.Missing.Add("profile.energyPrices");
            else
            {
                var value = context.Value(context.Profile.EnergyPrices[index].PricePerUnitSek, $"profile.energyPrices[{index}].pricePerUnitSek", price);
                if (value is not null) price.Add(value.Value);
            }
        }

        return price;

        void AddPrice(SensitivityValue? input, string inputPath, decimal weight)
        {
            if (weight == 0m) return;
            var amount = context.Value(input, inputPath, price);
            if (amount is null) return;
            var contribution = amount.Value * weight / 100m;
            if (amount > 0m && contribution == 0m)
                price.Errors.Add(new(inputPath, "calculationOutOfRange", "Positive weighted price is below decimal precision."));
            else price.Add(contribution);
        }
    }
}
