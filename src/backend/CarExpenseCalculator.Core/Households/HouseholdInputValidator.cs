using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

public sealed record HouseholdInputError(string Path, string Code, string Message);

public sealed class HouseholdInputValidationException(IReadOnlyList<HouseholdInputError> errors)
    : ArgumentException("Household input structure is invalid.")
{
    public IReadOnlyList<HouseholdInputError> Errors { get; } = Array.AsReadOnly(errors.ToArray());
}

public static class HouseholdInputValidator
{
    public const decimal MaximumMoneySek = 100_000_000m;
    public const int MaximumPeriodMonths = 120;
    public const int MaximumCandidates = 100;

    public static IReadOnlyList<HouseholdInputError> ValidateProfile(HouseholdProfileInput profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = new List<HouseholdInputError>();
        if (profile.StartMonth is { } start && (start.Year is < 1900 or > 9989 || start.Month is < 1 or > 12))
        {
            errors.Add(new("profile.startMonth", "outOfRange", "Start month must be a calendar month in years 1900-9989."));
        }

        Range(profile.PeriodMonths, 1m, MaximumPeriodMonths, "profile.periodMonths", errors);
        Range(profile.AnnualDistanceKilometres, 0m, 1_000_000m, "profile.annualDistanceKilometres", errors);
        Range(profile.PurchaseCashSek, 0m, MaximumMoneySek, "profile.purchaseCashSek", errors);
        Range(profile.StartupBudgetSek, 0m, MaximumMoneySek, "profile.startupBudgetSek", errors);
        Range(profile.MonthlyBudgetSek, 0m, MaximumMoneySek, "profile.monthlyBudgetSek", errors);
        if (!Enum.IsDefined(profile.ActiveSensitivityMode))
        {
            errors.Add(new("profile.activeSensitivityMode", "unsupportedValue", "Sensitivity mode is not supported."));
        }

        if (profile.LoanTerms is { } terms)
        {
            Sensitivity(terms.AnnualNominalInterestRatePercent, 0m, 100m, "profile.loanTerms.annualNominalInterestRatePercent", errors);
            Range(terms.TermMonths, 1m, MaximumPeriodMonths, "profile.loanTerms.termMonths", errors);
            Range(terms.SetupFeeSek, 0m, MaximumMoneySek, "profile.loanTerms.setupFeeSek", errors);
            Range(terms.MonthlyFeeSek, 0m, MaximumMoneySek, "profile.loanTerms.monthlyFeeSek", errors);
        }

        Sensitivity(profile.ElectricDrivingSharePercent, 0m, 100m, "profile.electricDrivingSharePercent", errors);
        Sensitivity(profile.HomeChargingSharePercent, 0m, 100m, "profile.homeChargingSharePercent", errors);
        Sensitivity(profile.ChargingLossPercent, 0m, 100m, "profile.chargingLossPercent", errors, maximumExclusive: true);
        Sensitivity(profile.HomeChargingPricePerKilowattHourSek, 0m, 100_000m, "profile.homeChargingPricePerKilowattHourSek", errors);
        Sensitivity(profile.PublicChargingPricePerKilowattHourSek, 0m, 100_000m, "profile.publicChargingPricePerKilowattHourSek", errors);

        // Fuel and unit are finite enums; a unique entry per pair bounds this collection.
        var maximumPrices = Enum.GetValues<FuelType>().Length * Enum.GetValues<EnergyUnit>().Length;
        if (profile.EnergyPrices.Count > maximumPrices)
        {
            errors.Add(new("profile.energyPrices", "tooManyItems", "Energy prices exceed the number of supported fuel/unit pairs."));
        }

        var keys = new HashSet<(FuelType, EnergyUnit)>();
        for (var index = 0; index < Math.Min(profile.EnergyPrices.Count, maximumPrices); index++)
        {
            var entry = profile.EnergyPrices[index];
            var path = $"profile.energyPrices[{index}]";
            if (entry is null)
            {
                errors.Add(new(path, "missingItem", "An energy price entry cannot be null."));
                continue;
            }

            if (!Enum.IsDefined(entry.Fuel))
            {
                errors.Add(new($"{path}.fuel", "unsupportedValue", "Fuel is not supported."));
            }

            if (!Enum.IsDefined(entry.Unit))
            {
                errors.Add(new($"{path}.unit", "unsupportedValue", "Energy unit is not supported."));
            }

            if (!keys.Add((entry.Fuel, entry.Unit)))
            {
                errors.Add(new(path, "duplicateKey", "Each fuel/unit pair can have only one shared price."));
            }

            Sensitivity(entry.PricePerUnitSek, 0m, 100_000m, $"{path}.pricePerUnitSek", errors);
        }

        return errors.AsReadOnly();
    }

    public static IReadOnlyList<HouseholdInputError> ValidatePurchase(VehiclePurchaseInput vehicle, string path = "vehicle")
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        var errors = new List<HouseholdInputError>();
        if (string.IsNullOrWhiteSpace(vehicle.CandidateKey) || vehicle.CandidateKey.Trim().Length > 120)
        {
            errors.Add(new($"{path}.candidateKey", "invalidKey", "Candidate key must contain 1-120 trimmed characters."));
        }

        Range(vehicle.PriceSek, 0m, MaximumMoneySek, $"{path}.priceSek", errors);
        return errors.AsReadOnly();
    }

    internal static void Sensitivity(
        SensitivityValue? value, decimal minimum, decimal maximum, string path,
        ICollection<HouseholdInputError> errors, bool maximumExclusive = false)
    {
        if (value is null)
        {
            return;
        }

        if (value.Single is { } single)
        {
            Range(single, minimum, maximum, $"{path}.single", errors, maximumExclusive);
            return;
        }

        Range(value.Favorable, minimum, maximum, $"{path}.favorable", errors, maximumExclusive);
        Range(value.Baseline, minimum, maximum, $"{path}.baseline", errors, maximumExclusive);
        Range(value.Cautious, minimum, maximum, $"{path}.cautious", errors, maximumExclusive);
    }

    internal static void Range(
        decimal? value, decimal minimum, decimal maximum, string path,
        ICollection<HouseholdInputError> errors, bool maximumExclusive = false)
    {
        if (value is { } number && (number < minimum || (maximumExclusive ? number >= maximum : number > maximum)))
        {
            errors.Add(new(path, "outOfRange", "Value is outside the supported range."));
        }
    }
}
