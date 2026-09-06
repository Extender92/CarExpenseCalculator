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
        if (!Enum.IsDefined(vehicle.AcquisitionType))
            errors.Add(new($"{path}.acquisitionType", "unsupportedValue", "Acquisition type is not supported."));
        if ((vehicle.AcquisitionType == AcquisitionType.Purchase && vehicle.Lease is not null)
            || (vehicle.AcquisitionType == AcquisitionType.Lease && (vehicle.PriceSek is not null || vehicle.Residual is not null)))
            errors.Add(new(path, "invalidStructure", "Purchase and lease inputs are mutually exclusive."));
        ValidateLease(vehicle.Lease, $"{path}.lease", errors);
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
        foreach (var (name, category) in Categories(vehicle).Concat(LeaseCategories(vehicle.Lease)))
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

    private static void ValidateLease(HouseholdLeaseInput? lease, string path, List<HouseholdInputError> errors)
    {
        if (lease is null) return;
        EnumValue(lease.PriceBasis, $"{path}.priceBasis", errors);
        HouseholdInputValidator.Range(lease.TermMonths, 1, 120, $"{path}.termMonths", errors);
        HouseholdInputValidator.Range(lease.UpfrontNonRefundableSek, 0, HouseholdInputValidator.MaximumMoneySek, $"{path}.upfrontNonRefundableSek", errors);
        HouseholdInputValidator.Range(lease.RefundableDepositSek, 0, HouseholdInputValidator.MaximumMoneySek, $"{path}.refundableDepositSek", errors);
        var refundMaximum = lease.RefundableDepositSek is >= 0 and <= HouseholdInputValidator.MaximumMoneySek
            ? lease.RefundableDepositSek.Value : HouseholdInputValidator.MaximumMoneySek;
        HouseholdInputValidator.Sensitivity(lease.DepositRefundSek, 0, refundMaximum, $"{path}.depositRefundSek", errors);
        HouseholdInputValidator.Range(lease.IncludedDistanceKilometres, 0, 10_000_000, $"{path}.includedDistanceKilometres", errors);
        HouseholdInputValidator.Sensitivity(lease.ExcessDistancePricePerKilometreSek, 0, 100_000, $"{path}.excessDistancePricePerKilometreSek", errors);
        if (lease.MonthlyPayments?.Count > 120)
            errors.Add(new($"{path}.monthlyPayments", "tooManyItems", "At most 120 monthly payments are allowed."));
        var months = new HashSet<int>();
        foreach (var (payment, index) in (lease.MonthlyPayments ?? []).Take(120).Select((payment, index) => (payment, index)))
        {
            var itemPath = $"{path}.monthlyPayments[{index}]";
            if (payment is null)
            {
                errors.Add(new(itemPath, "missingItem", "Payment cannot be null."));
                continue;
            }
            if (!months.Add(payment.MonthOffset))
                errors.Add(new($"{itemPath}.monthOffset", "duplicateKey", "Each contract month accepts one quoted payment."));
            HouseholdInputValidator.Range(payment.MonthOffset, 1, lease.TermMonths is >= 1 and <= 120 ? lease.TermMonths.Value : 120,
                $"{itemPath}.monthOffset", errors);
            HouseholdInputValidator.Range(payment.AmountSek, 0, HouseholdInputValidator.MaximumMoneySek, $"{itemPath}.amountSek", errors);
        }
        foreach (var (charge, index) in (lease.EndFees ?? []).Take(50).Select((charge, index) => (charge, index)))
            if (charge?.MonthOffset is not null)
                errors.Add(new($"{path}.endFees.items[{index}].monthOffset", "invalidStructure", "End fees use the contract end month."));
        foreach (var (charge, index) in (lease.OtherPayments ?? []).Take(50).Select((charge, index) => (charge, index)))
            if (charge is not null && lease.TermMonths is >= 1 and <= 120)
                HouseholdInputValidator.Range(charge.MonthOffset, 0, lease.TermMonths.Value, $"{path}.otherPayments.items[{index}].monthOffset", errors);
    }

    private static IEnumerable<(string Name, HouseholdCostCategoryInput? Category)> LeaseCategories(HouseholdLeaseInput? lease)
    {
        if (lease is null) yield break;
        yield return ("lease.endFees", Adapt(lease.EndFees));
        yield return ("lease.otherPayments", Adapt(lease.OtherPayments));

        static HouseholdCostCategoryInput? Adapt(IReadOnlyList<HouseholdLeaseCharge>? charges) => charges is null ? null
            : HouseholdCostCategoryInput.FromItems(charges.Select(charge => charge is null ? null! : new HouseholdCostItem(
                charge.Key, charge.Label, charge.AmountSek, HouseholdCostCadence.Once, charge.MonthOffset,
                EvidenceNote: charge.EvidenceNote, SourceUrl: charge.SourceUrl)));
    }

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
