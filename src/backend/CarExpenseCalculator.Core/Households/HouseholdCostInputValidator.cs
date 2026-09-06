using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

public static class HouseholdCostInputValidator
{
    public const int MaximumCostItems = 50;
    public const int MaximumEnergySources = 2;

    public static IReadOnlyList<HouseholdInputError> ValidateVehicle(VehicleCostInput vehicle, string path = "vehicle")
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        var errors = HouseholdInputValidator.ValidatePurchase(new(vehicle.CandidateKey, vehicle.PriceSek), path).ToList();
        if (vehicle.Residual is { } residual)
        {
            var maximum = residual.Mode == ResidualMode.AnnualPercentage ? 100m
                : vehicle.PriceSek is >= 0m and <= HouseholdInputValidator.MaximumMoneySek
                    ? vehicle.PriceSek.Value : HouseholdInputValidator.MaximumMoneySek;
            HouseholdInputValidator.Sensitivity(residual.Value, 0m, maximum, $"{path}.residual.value", errors);
            if (residual.Mode == ResidualMode.FixedAmount)
                HouseholdInputValidator.Range(residual.PeriodMonths, 1m, 120m, $"{path}.residual.periodMonths", errors);
        }

        HouseholdInputValidator.Sensitivity(vehicle.AdditionalRepairAllowancePerMonthSek, 0m,
            HouseholdInputValidator.MaximumMoneySek, $"{path}.additionalRepairAllowancePerMonthSek", errors);
        var costKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, category) in Categories(vehicle))
        {
            if (category is null) continue;
            var categoryPath = $"{path}.{name}";
            if (category.Items.Count > MaximumCostItems)
                errors.Add(new(categoryPath, "tooManyItems", "Each category accepts at most 50 items."));
            for (var index = 0; index < Math.Min(category.Items.Count, MaximumCostItems); index++)
            {
                var itemPath = $"{categoryPath}.items[{index}]";
                var item = category.Items[index];
                if (item is null)
                {
                    errors.Add(new(itemPath, "missingItem", "Cost item cannot be null."));
                    continue;
                }

                Key(item.Key, $"{itemPath}.key", costKeys, errors);
                if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 120)
                    errors.Add(new($"{itemPath}.label", "invalidLabel", "Label must contain 1-120 trimmed characters."));
                if (item.EvidenceNote?.Length > 1000)
                    errors.Add(new($"{itemPath}.evidenceNote", "invalidEvidence", "Evidence note cannot exceed 1000 characters."));
                if (item.SourceUrl is not null && !ListingUrl.TryParse(item.SourceUrl, out _))
                    errors.Add(new($"{itemPath}.sourceUrl", "invalidEvidence", "Evidence URL must pass existing public URL validation."));
                EnumValue(item.Cadence, $"{itemPath}.cadence", errors);
                if (name is "tax" or "insurance" && item.AmountSek is { Single: null })
                    errors.Add(new($"{itemPath}.amountSek", "invalidStructure", "Tax and insurance use single quoted amounts."));
                HouseholdInputValidator.Sensitivity(item.AmountSek, 0m, HouseholdInputValidator.MaximumMoneySek, $"{itemPath}.amountSek", errors);
                HouseholdInputValidator.Range(item.MonthOffset, 0m, 120m, $"{itemPath}.monthOffset", errors);
                HouseholdInputValidator.Range(item.DueMonthOfYear, 1m, 12m, $"{itemPath}.dueMonthOfYear", errors);
            }
        }

        ValidateEnergy(vehicle.EnergySources, $"{path}.energySources", errors);
        return errors.AsReadOnly();
    }

    internal static bool IsStructural(HouseholdInputError error) => error.Code is not
        ("outOfRange" or "energyBasisMismatch" or "unsupportedDrivingModes" or "invalidEnergyUnit");

    private static void ValidateEnergy(IReadOnlyList<HouseholdEnergySource>? sources, string path, List<HouseholdInputError> errors)
    {
        if (sources is null) return;
        if (sources.Count > MaximumEnergySources)
            errors.Add(new(path, "tooManyItems", "At most two energy sources are allowed."));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (source, index) in sources.Take(MaximumEnergySources).Select((source, index) => (source, index)))
        {
            var sourcePath = $"{path}[{index}]";
            if (source is null)
            {
                errors.Add(new(sourcePath, "missingItem", "Energy source cannot be null."));
                continue;
            }

            Key(source.Key, $"{sourcePath}.key", keys, errors);
            EnumValue(source.Fuel, $"{sourcePath}.fuel", errors);
            EnumValue(source.Unit, $"{sourcePath}.unit", errors);
            EnumValue(source.ConsumptionBasis, $"{sourcePath}.consumptionBasis", errors);
            EnumValue(source.ElectricityBasis, $"{sourcePath}.electricityBasis", errors);
            HouseholdInputValidator.Sensitivity(source.ConsumptionPer100Kilometres, 0.0000000000000000000000000001m,
                10_000m, $"{sourcePath}.consumptionPer100Kilometres", errors);
            if (source.Fuel == FuelType.Electricity && source.Unit is not null and not EnergyUnit.KilowattHour)
                errors.Add(new($"{sourcePath}.unit", "invalidEnergyUnit", "Electricity requires kilowatt hours."));
        }

        var bounded = sources.Take(MaximumEnergySources).Where(source => source is not null).ToArray();
        if (bounded.Where(source => source.ConsumptionBasis is not null).Select(source => source.ConsumptionBasis).Distinct().Count() > 1)
            errors.Add(new(path, "energyBasisMismatch", "Normalize mixed consumption bases before calculating this car."));
        if (bounded.Length == 2 && bounded.All(source => source.ConsumptionBasis == ConsumptionBasis.DrivingMode && source.Fuel is not null)
            && bounded.Count(source => source.Fuel == FuelType.Electricity) != 1)
            errors.Add(new(path, "unsupportedDrivingModes", "Two driving modes require electricity and one other fuel; otherwise supply whole-distance consumption."));
    }

    private static void EnumValue<T>(T? value, string path, List<HouseholdInputError> errors) where T : struct, Enum
    {
        if (value is { } supplied && !Enum.IsDefined(supplied))
            errors.Add(new(path, "unsupportedValue", "Value is not supported."));
    }

    private static void Key(string? key, string path, HashSet<string> keys, List<HouseholdInputError> errors)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 120)
            errors.Add(new(path, "invalidKey", "Key must contain 1-120 trimmed characters."));
        else if (!keys.Add(key.Trim()))
            errors.Add(new(path, "duplicateKey", "Item keys must be unique across their cost categories."));
    }

    internal static IEnumerable<(string Name, HouseholdCostCategoryInput? Category)> Categories(VehicleCostInput vehicle)
    {
        yield return ("tax", vehicle.Tax);
        yield return ("insurance", vehicle.Insurance);
        yield return ("service", vehicle.Service);
        yield return ("repairs", vehicle.Repairs);
        yield return ("customCosts", vehicle.CustomCosts);
    }
}
