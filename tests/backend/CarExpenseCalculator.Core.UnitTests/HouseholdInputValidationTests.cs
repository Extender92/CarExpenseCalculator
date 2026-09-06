using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdInputValidationTests
{
    [Fact]
    public void Missing_numeric_assumptions_are_allowed_and_explicit_zero_is_preserved()
    {
        var profile = new HouseholdProfileInput { PurchaseCashSek = 0m, StartupBudgetSek = 0m, MonthlyBudgetSek = null };
        Assert.Empty(HouseholdInputValidator.ValidateProfile(profile));
        Assert.Equal(0m, profile.StartupBudgetSek);
        Assert.Null(profile.MonthlyBudgetSek);
        Assert.Empty(HouseholdInputValidator.ValidatePurchase(new("registered-or-unsaved-key", null)));
    }

    [Theory]
    [InlineData(1, 0, 0, 1900, 1)]
    [InlineData(120, 100000000, 1000000, 9989, 12)]
    public void Inclusive_profile_boundaries_are_accepted(int months, int money, int kilometres, int year, int month)
    {
        var profile = new HouseholdProfileInput
        {
            StartMonth = new(year, month),
            PeriodMonths = months,
            AnnualDistanceKilometres = kilometres,
            PurchaseCashSek = money,
            StartupBudgetSek = money,
            MonthlyBudgetSek = money,
            LoanTerms = new() { TermMonths = months, SetupFeeSek = money, MonthlyFeeSek = money },
        };
        Assert.Empty(HouseholdInputValidator.ValidateProfile(profile));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Invalid_period_and_term_report_separate_field_errors(int months)
    {
        var errors = HouseholdInputValidator.ValidateProfile(new()
        {
            PeriodMonths = months,
            LoanTerms = new() { TermMonths = months },
        });
        Assert.Equal(["profile.periodMonths", "profile.loanTerms.termMonths"], errors.Select(error => error.Path));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000001)]
    public void All_shared_money_inputs_use_the_same_bounds(int invalid)
    {
        var errors = HouseholdInputValidator.ValidateProfile(new()
        {
            PurchaseCashSek = invalid,
            StartupBudgetSek = invalid,
            MonthlyBudgetSek = invalid,
            LoanTerms = new() { SetupFeeSek = invalid, MonthlyFeeSek = invalid },
        });
        Assert.Equal(5, errors.Count);
        Assert.All(errors, error => Assert.Equal("outOfRange", error.Code));
        Assert.Equal("vehicle.priceSek", Assert.Single(HouseholdInputValidator.ValidatePurchase(new("car", invalid))).Path);
    }

    [Theory]
    [InlineData(1899, 12)]
    [InlineData(9990, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Invalid_calendar_month_is_rejected_without_reading_a_clock(int year, int month)
    {
        var errors = HouseholdInputValidator.ValidateProfile(new() { StartMonth = new(year, month) });
        Assert.Equal("profile.startMonth", Assert.Single(errors).Path);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void Annual_distance_stays_in_kilometres_and_within_the_v1_bound(int distance)
    {
        var errors = HouseholdInputValidator.ValidateProfile(new() { AnnualDistanceKilometres = distance });
        Assert.Equal("profile.annualDistanceKilometres", Assert.Single(errors).Path);
    }

    [Theory]
    [InlineData(SensitivityMode.Favorable, 30)]
    [InlineData(SensitivityMode.Baseline, 20)]
    [InlineData(SensitivityMode.Cautious, 10)]
    public void Sensitivity_keeps_explicit_values_without_reordering_or_invented_fallback(SensitivityMode mode, int expected)
    {
        Assert.Equal((decimal)expected, SensitivityValue.Scenarios(30m, 20m, 10m).GetValue(mode));
        Assert.Equal(7m, SensitivityValue.Constant(7m).GetValue(mode));
        Assert.Throws<ArgumentOutOfRangeException>(() => SensitivityValue.Constant(7m).GetValue((SensitivityMode)99));
    }

    [Fact]
    public void Every_value_in_a_sensitivity_trio_is_validated_including_inactive_modes()
    {
        var errors = HouseholdInputValidator.ValidateProfile(new()
        {
            LoanTerms = new() { AnnualNominalInterestRatePercent = SensitivityValue.Scenarios(-1m, 5m, 101m) },
            ElectricDrivingSharePercent = SensitivityValue.Scenarios(0m, 50m, 101m),
            HomeChargingSharePercent = SensitivityValue.Scenarios(-1m, 50m, 100m),
            ChargingLossPercent = SensitivityValue.Scenarios(0m, 10m, 100m),
        });

        Assert.Equal(5, errors.Count);
        Assert.Contains(errors, error => error.Path == "profile.loanTerms.annualNominalInterestRatePercent.favorable");
        Assert.Contains(errors, error => error.Path == "profile.loanTerms.annualNominalInterestRatePercent.cautious");
        Assert.Contains(errors, error => error.Path == "profile.chargingLossPercent.cautious");
    }

    [Fact]
    public void Rates_and_shares_allow_endpoints_but_a_total_charging_loss_does_not()
    {
        var profile = new HouseholdProfileInput
        {
            LoanTerms = new() { AnnualNominalInterestRatePercent = SensitivityValue.Scenarios(0m, 50m, 100m) },
            ElectricDrivingSharePercent = SensitivityValue.Scenarios(0m, 50m, 100m),
            HomeChargingSharePercent = SensitivityValue.Scenarios(0m, 50m, 100m),
            ChargingLossPercent = SensitivityValue.Constant(99.999999m),
        };
        Assert.Empty(HouseholdInputValidator.ValidateProfile(profile));
        Assert.Single(HouseholdInputValidator.ValidateProfile(profile with { ChargingLossPercent = SensitivityValue.Constant(100m) }));
    }

    [Fact]
    public void Energy_and_charging_prices_are_validated_for_each_supplied_mode()
    {
        var profile = new HouseholdProfileInput(
            [new(FuelType.Petrol, EnergyUnit.Litre, SensitivityValue.Scenarios(-1m, 20m, 100_001m))])
        {
            HomeChargingPricePerKilowattHourSek = SensitivityValue.Scenarios(1m, 2m, 100_001m),
            PublicChargingPricePerKilowattHourSek = SensitivityValue.Constant(-1m),
        };
        var errors = HouseholdInputValidator.ValidateProfile(profile);

        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, error => error.Path == "profile.energyPrices[0].pricePerUnitSek.favorable");
        Assert.Contains(errors, error => error.Path == "profile.energyPrices[0].pricePerUnitSek.cautious");
    }

    [Fact]
    public void All_supported_fuels_and_units_can_have_common_prices_without_per_car_overrides()
    {
        var prices = Enum.GetValues<FuelType>().SelectMany(fuel => Enum.GetValues<EnergyUnit>()
            .Select(unit => new HouseholdEnergyPrice(fuel, unit, SensitivityValue.Scenarios(0m, 10m, 100_000m)))).ToArray();
        Assert.Empty(HouseholdInputValidator.ValidateProfile(new(prices)));

        var tooMany = HouseholdInputValidator.ValidateProfile(new([.. prices, prices[0]]));
        Assert.Contains(tooMany, error => error.Code == "tooManyItems");
    }

    [Fact]
    public void Price_keys_are_unique_and_unknown_enums_or_null_entries_are_rejected()
    {
        HouseholdEnergyPrice price = new(FuelType.Petrol, EnergyUnit.Litre, null);
        var errors = HouseholdInputValidator.ValidateProfile(new(
            [price, price, new((FuelType)999, (EnergyUnit)999, null), null!]));

        Assert.Contains(errors, error => error.Code == "duplicateKey");
        Assert.Equal(2, errors.Count(error => error.Code == "unsupportedValue"));
        Assert.Contains(errors, error => error.Code == "missingItem");
    }

    [Fact]
    public void Profile_snapshots_the_price_collection_so_caller_edits_cannot_change_shared_assumptions()
    {
        var prices = new List<HouseholdEnergyPrice> { new(FuelType.Petrol, EnergyUnit.Litre, SensitivityValue.Constant(20m)) };
        var profile = new HouseholdProfileInput(prices);
        prices.Clear();

        Assert.Single(profile.EnergyPrices);
        Assert.Throws<NotSupportedException>(() => ((IList<HouseholdEnergyPrice>)profile.EnergyPrices).Clear());
    }

    [Fact]
    public void Candidate_key_bounds_are_validated_independently_from_missing_price()
    {
        Assert.Empty(HouseholdInputValidator.ValidatePurchase(new(new string('x', 120), null)));
        Assert.Equal("invalidKey", Assert.Single(HouseholdInputValidator.ValidatePurchase(new(new string('x', 121), null))).Code);
        Assert.Equal("invalidKey", Assert.Single(HouseholdInputValidator.ValidatePurchase(new(null!, null))).Code);
    }
}
