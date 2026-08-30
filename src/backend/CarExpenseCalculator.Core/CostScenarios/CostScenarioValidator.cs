namespace CarExpenseCalculator.Core.CostScenarios;

internal static class CostScenarioValidator
{
    private const decimal MaximumMoneySek = 100_000_000m;
    private const decimal MaximumAnnualDistanceKilometres = 1_000_000m;
    private const decimal MaximumConsumptionPer100Kilometres = 10_000m;
    private const decimal MaximumPricePerUnitSek = 100_000m;
    private const int MaximumCalculationMonths = 120;
    private const int MaximumLabelLength = 120;
    private const int MaximumCustomCostCount = 50;

    public static IReadOnlyList<CostScenarioValidationError> Validate(CostScenario scenario)
    {
        var errors = new List<CostScenarioValidationError>();

        ValidateOptionalLabel(scenario.VehicleLabel, "vehicleLabel", errors);
        ValidateIntegerRange(
            scenario.CalculationPeriodMonths,
            1,
            MaximumCalculationMonths,
            "calculationPeriodMonths",
            errors);
        ValidateDecimalRange(scenario.PurchasePriceSek, 0m, MaximumMoneySek, "purchasePriceSek", errors);
        ValidateResidualValue(scenario, errors);
        ValidateDecimalRange(
            scenario.AnnualDistanceKilometres,
            0m,
            MaximumAnnualDistanceKilometres,
            "annualDistanceKilometres",
            errors);
        ValidateFinancing(scenario, errors);
        ValidateEnergySources(scenario, errors);
        ValidateRecurringCost(scenario.VehicleTax, "vehicleTax", errors);
        ValidateRecurringCost(scenario.Insurance, "insurance", errors);
        ValidateRecurringCost(scenario.MaintenanceAndRepairs, "maintenanceAndRepairs", errors);
        ValidateNamedRecurringCosts(scenario.OtherRecurringCosts, errors);
        ValidateOneTimeCosts(scenario.OtherOneTimeCosts, errors);

        return Array.AsReadOnly(errors.ToArray());
    }

    private static void ValidateResidualValue(
        CostScenario scenario,
        ICollection<CostScenarioValidationError> errors)
    {
        if (scenario.ExpectedResidualValueSek is not { } residualValue)
        {
            return;
        }

        ValidateDecimalRange(
            residualValue,
            0m,
            MaximumMoneySek,
            "expectedResidualValueSek",
            errors);

        if (residualValue > scenario.PurchasePriceSek)
        {
            AddError(
                errors,
                "expectedResidualValueSek",
                "Residual value cannot exceed purchase price.");
        }
    }

    private static void ValidateFinancing(
        CostScenario scenario,
        ICollection<CostScenarioValidationError> errors)
    {
        if (scenario.Financing is not { } financing)
        {
            return;
        }

        if (scenario.PurchasePriceSek == 0m)
        {
            AddError(errors, "financing", "A zero-price vehicle cannot be financed.");
        }

        ValidateDecimalRange(
            financing.DownPaymentSek,
            0m,
            MaximumMoneySek,
            "financing.downPaymentSek",
            errors);

        if (financing.DownPaymentSek >= scenario.PurchasePriceSek)
        {
            AddError(
                errors,
                "financing.downPaymentSek",
                "Down payment must be less than purchase price.");
        }

        ValidateDecimalRange(
            financing.AnnualNominalInterestRatePercent,
            0m,
            100m,
            "financing.annualNominalInterestRatePercent",
            errors);
        ValidateIntegerRange(financing.TermMonths, 1, 120, "financing.termMonths", errors);
    }

    private static void ValidateEnergySources(
        CostScenario scenario,
        ICollection<CostScenarioValidationError> errors)
    {
        if (scenario.EnergySources.Count > 2)
        {
            AddError(errors, "energySources", "At most two energy sources are allowed.");
        }

        if (scenario.AnnualDistanceKilometres > 0m && scenario.EnergySources.Count == 0)
        {
            AddError(errors, "energySources", "At least one energy source is required for positive distance.");
        }

        var shareTotal = 0m;
        for (var index = 0; index < scenario.EnergySources.Count; index++)
        {
            var source = scenario.EnergySources[index];
            var path = $"energySources[{index}]";
            if (source is null)
            {
                AddError(errors, path, "Energy source cannot be null.");
                continue;
            }

            ValidateRequiredLabel(source.Label, $"{path}.label", errors);
            ValidateEnum(source.Unit, $"{path}.unit", errors);
            ValidateDecimalRange(
                source.ConsumptionPer100Kilometres,
                decimal.Zero,
                MaximumConsumptionPer100Kilometres,
                $"{path}.consumptionPer100Kilometres",
                errors,
                minimumIsExclusive: true);
            ValidateDecimalRange(
                source.PricePerUnitSek,
                0m,
                MaximumPricePerUnitSek,
                $"{path}.pricePerUnitSek",
                errors);
            ValidateDecimalRange(
                source.DistanceSharePercent,
                decimal.Zero,
                100m,
                $"{path}.distanceSharePercent",
                errors,
                minimumIsExclusive: true);

            if (source.DistanceSharePercent > 0m && source.DistanceSharePercent <= 100m)
            {
                shareTotal += source.DistanceSharePercent;
            }
        }

        if (scenario.EnergySources.Count > 0 && shareTotal != 100m)
        {
            AddError(errors, "energySources", "Energy source shares must total exactly 100 percent.");
        }
    }

    private static void ValidateRecurringCost(
        RecurringCost? cost,
        string path,
        ICollection<CostScenarioValidationError> errors)
    {
        if (cost is null)
        {
            return;
        }

        ValidateDecimalRange(cost.AmountSek, 0m, MaximumMoneySek, $"{path}.amountSek", errors);
        ValidateEnum(cost.Cadence, $"{path}.cadence", errors);
    }

    private static void ValidateNamedRecurringCosts(
        IReadOnlyList<NamedRecurringCost> costs,
        ICollection<CostScenarioValidationError> errors)
    {
        if (costs.Count > MaximumCustomCostCount)
        {
            AddError(errors, "otherRecurringCosts", "At most 50 recurring costs are allowed.");
        }

        for (var index = 0; index < costs.Count; index++)
        {
            var cost = costs[index];
            var path = $"otherRecurringCosts[{index}]";
            if (cost is null)
            {
                AddError(errors, path, "Recurring cost cannot be null.");
                continue;
            }

            ValidateRequiredLabel(cost.Label, $"{path}.label", errors);
            ValidateDecimalRange(cost.AmountSek, 0m, MaximumMoneySek, $"{path}.amountSek", errors);
            ValidateEnum(cost.Cadence, $"{path}.cadence", errors);
        }
    }

    private static void ValidateOneTimeCosts(
        IReadOnlyList<OneTimeCost> costs,
        ICollection<CostScenarioValidationError> errors)
    {
        if (costs.Count > MaximumCustomCostCount)
        {
            AddError(errors, "otherOneTimeCosts", "At most 50 one-time costs are allowed.");
        }

        for (var index = 0; index < costs.Count; index++)
        {
            var cost = costs[index];
            var path = $"otherOneTimeCosts[{index}]";
            if (cost is null)
            {
                AddError(errors, path, "One-time cost cannot be null.");
                continue;
            }

            ValidateRequiredLabel(cost.Label, $"{path}.label", errors);
            ValidateDecimalRange(cost.AmountSek, 0m, MaximumMoneySek, $"{path}.amountSek", errors);
        }
    }

    private static void ValidateOptionalLabel(
        string? value,
        string path,
        ICollection<CostScenarioValidationError> errors)
    {
        if (value is not null)
        {
            ValidateRequiredLabel(value, path, errors);
        }
    }

    private static void ValidateRequiredLabel(
        string? value,
        string path,
        ICollection<CostScenarioValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, path, "Label must contain at least one non-whitespace character.");
            return;
        }

        if (value.Trim().Length > MaximumLabelLength)
        {
            AddError(errors, path, "Label cannot exceed 120 characters after trimming.");
        }
    }

    private static void ValidateDecimalRange(
        decimal value,
        decimal minimum,
        decimal maximum,
        string path,
        ICollection<CostScenarioValidationError> errors,
        bool minimumIsExclusive = false)
    {
        var belowMinimum = minimumIsExclusive ? value <= minimum : value < minimum;
        if (belowMinimum || value > maximum)
        {
            var minimumDescription = minimumIsExclusive ? $"greater than {minimum}" : $"at least {minimum}";
            AddError(errors, path, $"Value must be {minimumDescription} and at most {maximum}.");
        }
    }

    private static void ValidateIntegerRange(
        int value,
        int minimum,
        int maximum,
        string path,
        ICollection<CostScenarioValidationError> errors)
    {
        if (value < minimum || value > maximum)
        {
            AddError(errors, path, $"Value must be between {minimum} and {maximum} inclusive.");
        }
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string path,
        ICollection<CostScenarioValidationError> errors)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            AddError(errors, path, "Value is not supported.");
        }
    }

    private static void AddError(
        ICollection<CostScenarioValidationError> errors,
        string path,
        string message)
    {
        errors.Add(new CostScenarioValidationError(path, message));
    }
}
