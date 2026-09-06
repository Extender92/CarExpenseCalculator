using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class HouseholdEnergyCalculatorTests
{
    [Fact]
    public void A5_applies_driving_share_charging_loss_and_kwh_price_mix_once()
    {
        var sources = new[]
        {
            CostExamples.Electricity() with { ConsumptionBasis = ConsumptionBasis.DrivingMode },
            CostExamples.Petrol() with { ConsumptionBasis = ConsumptionBasis.DrivingMode },
        };
        var result = CostExamples.Calculate(CostExamples.Car(sources: sources), ChargingProfile());
        Assert.Equal(1296m, result.Energy.Sources[0].BaseQuantity);
        Assert.Equal(1440m, result.Energy.Sources[0].PurchasedQuantity);
        Assert.Equal(3744m, result.Energy.Sources[0].Cost.CompleteTotalSek);
        Assert.Equal(288m, result.Energy.Sources[1].PurchasedQuantity);
        Assert.Equal(5760m, result.Energy.Sources[1].Cost.CompleteTotalSek);
        Assert.Equal(9504m, result.Energy.Cost.CompleteTotalSek);
        Assert.Equal(7.92m, result.Totals.CostPerMil.CompleteTotalSek);
        Assert.Equal(792m, result.Totals.MonthlyCost.CompleteTotalSek);
    }

    [Fact]
    public void A6_whole_distance_and_metered_consumption_ignore_driving_share_and_losses()
    {
        var sources = new[]
        {
            CostExamples.Electricity(ElectricityBasis.Metered) with { ConsumptionPer100Kilometres = CostExamples.Value(10) },
            CostExamples.Petrol() with { ConsumptionPer100Kilometres = CostExamples.Value(3) },
        };
        var profile = ChargingProfile() with { ElectricDrivingSharePercent = CostExamples.Value(-1), ChargingLossPercent = CostExamples.Value(100) };
        var preview = new HouseholdCostCalculator().Calculate(profile, [CostExamples.Car(sources: sources)]);
        var energy = Assert.Single(preview.Vehicles).Energy;
        Assert.Equal(1200m, energy.Sources[0].PurchasedQuantity);
        Assert.Equal(360m, energy.Sources[1].PurchasedQuantity);
        Assert.Equal(10_320m, energy.Cost.CompleteTotalSek);
        Assert.Equal(2, preview.ProfileErrors.Count);
        Assert.Empty(energy.Cost.Errors);
    }

    [Theory]
    [InlineData(0, 14400)]
    [InlineData(100, 6240)]
    public void Zero_share_needs_no_consumption_or_price_for_the_unused_mode(int electricShare, int expected)
    {
        var electric = CostExamples.Electricity() with { ConsumptionBasis = ConsumptionBasis.DrivingMode };
        var petrol = CostExamples.Petrol() with { ConsumptionBasis = ConsumptionBasis.DrivingMode };
        var profile = ChargingProfile() with { ElectricDrivingSharePercent = CostExamples.Value(electricShare) };
        if (electricShare == 0)
        {
            electric = electric with { ConsumptionPer100Kilometres = null, ElectricityBasis = null };
            profile = profile with { HomeChargingSharePercent = null, HomeChargingPricePerKilowattHourSek = null, PublicChargingPricePerKilowattHourSek = null, ChargingLossPercent = null };
        }
        else
        {
            petrol = petrol with { ConsumptionPer100Kilometres = null };
            profile = new HouseholdProfileInput() with
            {
                PeriodMonths = 12, AnnualDistanceKilometres = 12_000, PurchaseCashSek = 100_000_000,
                ElectricDrivingSharePercent = CostExamples.Value(100), HomeChargingSharePercent = CostExamples.Value(80),
                HomeChargingPricePerKilowattHourSek = CostExamples.Value(2), PublicChargingPricePerKilowattHourSek = CostExamples.Value(5),
                ChargingLossPercent = CostExamples.Value(10),
            };
        }

        var energy = CostExamples.Calculate(CostExamples.Car(sources: [electric, petrol]), profile).Energy;
        Assert.Equal((decimal)expected, energy.Cost.CompleteTotalSek);
        Assert.Equal(0m, energy.Sources[electricShare == 0 ? 0 : 1].Cost.CompleteTotalSek);
    }

    [Theory]
    [InlineData(0, 10800)]
    [InlineData(100, 4320)]
    public void Zero_weight_charging_location_does_not_require_a_price(int homeShare, int expected)
    {
        var profile = ChargingProfile() with
        {
            HomeChargingSharePercent = CostExamples.Value(homeShare),
            HomeChargingPricePerKilowattHourSek = homeShare == 0 ? null : CostExamples.Value(2),
            PublicChargingPricePerKilowattHourSek = homeShare == 100 ? null : CostExamples.Value(5),
            ChargingLossPercent = null,
        };
        var result = CostExamples.Calculate(CostExamples.Car(sources: [CostExamples.Electricity(ElectricityBasis.Metered)]), profile);
        Assert.Equal((decimal)expected, result.Energy.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Missing_price_preserves_quantity_and_the_other_fuel_cost()
    {
        var profile = ChargingProfile() with { PublicChargingPricePerKilowattHourSek = null };
        var result = CostExamples.Calculate(CostExamples.Car(sources: [CostExamples.Electricity(), CostExamples.Petrol()]), profile).Energy;
        Assert.Equal(2400m, result.Sources[0].PurchasedQuantity);
        Assert.Equal(3840m, result.Sources[0].Cost.KnownSubtotalSek);
        Assert.Null(result.Sources[0].Cost.CompleteTotalSek);
        Assert.Equal(18_240m, result.Cost.KnownSubtotalSek);
        Assert.Equal(CostSectionState.Partial, result.Cost.State);
        Assert.Contains("profile.publicChargingPricePerKilowattHourSek", result.Cost.MissingComponents);
    }

    [Fact]
    public void Fuel_price_matches_both_fuel_and_unit_without_conversion_or_fallback()
    {
        var source = new HouseholdEnergySource("gas", FuelType.Biogas, EnergyUnit.Kilogram, CostExamples.Value(4), ConsumptionBasis.WholeDistance);
        var profile = CostExamples.Profile(12, 12_000, [new(FuelType.Biogas, EnergyUnit.Litre, CostExamples.Value(30))]);
        var result = CostExamples.Calculate(CostExamples.Car(sources: [source]), profile).Energy;
        Assert.Equal(480m, result.Sources[0].PurchasedQuantity);
        Assert.Equal(CostSectionState.Partial, result.Cost.State);
        Assert.Null(result.Cost.CompleteTotalSek);
        Assert.Contains("profile.energyPrices", result.Cost.MissingComponents);
    }

    [Theory]
    [InlineData(FuelType.Petrol, EnergyUnit.Litre)]
    [InlineData(FuelType.Diesel, EnergyUnit.Litre)]
    [InlineData(FuelType.Ethanol, EnergyUnit.Litre)]
    [InlineData(FuelType.Biogas, EnergyUnit.Kilogram)]
    [InlineData(FuelType.NaturalGas, EnergyUnit.Kilogram)]
    [InlineData(FuelType.LiquefiedPetroleumGas, EnergyUnit.Litre)]
    [InlineData(FuelType.Hydrogen, EnergyUnit.Kilogram)]
    [InlineData(FuelType.Other, EnergyUnit.Kilogram)]
    public void Supported_non_electric_fuels_use_explicit_units_and_single_source_distance(FuelType fuel, EnergyUnit unit)
    {
        var source = new HouseholdEnergySource("fuel", fuel, unit, CostExamples.Value(4), ConsumptionBasis.DrivingMode);
        var profile = CostExamples.Profile(12, 12_000, [new(fuel, unit, CostExamples.Value(30))]) with { ElectricDrivingSharePercent = CostExamples.Value(60) };
        var energy = CostExamples.Calculate(CostExamples.Car(sources: [source]), profile).Energy;
        Assert.Single(energy.Sources);
        Assert.Equal(480m, energy.Sources[0].PurchasedQuantity);
        Assert.Equal(14_400m, energy.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Two_non_electric_sources_use_whole_distance_without_invented_mode_shares()
    {
        var sources = new[]
        {
            CostExamples.Petrol() with { ConsumptionPer100Kilometres = CostExamples.Value(3) },
            new HouseholdEnergySource("gas", FuelType.Biogas, EnergyUnit.Kilogram, CostExamples.Value(2), ConsumptionBasis.WholeDistance),
        };
        var prices = new[] { new HouseholdEnergyPrice(FuelType.Petrol, EnergyUnit.Litre, CostExamples.Value(20)), new(FuelType.Biogas, EnergyUnit.Kilogram, CostExamples.Value(30)) };
        var profile = CostExamples.Profile(12, 12_000, prices);
        Assert.Equal(14_400m, CostExamples.Calculate(CostExamples.Car(sources: sources), profile).Energy.Cost.CompleteTotalSek);
        var modeSources = sources.Select(source => source with { ConsumptionBasis = ConsumptionBasis.DrivingMode });
        Assert.Contains(CostExamples.Calculate(CostExamples.Car(sources: modeSources), profile).Energy.Cost.Errors,
            error => error.Code == "unsupportedDrivingModes");
    }

    [Fact]
    public void Mixed_bases_invalidate_only_that_cars_energy_section()
    {
        var badCar = CostExamples.Car(sources: [CostExamples.Petrol(), CostExamples.Electricity() with { ConsumptionBasis = ConsumptionBasis.DrivingMode }]);
        var preview = new HouseholdCostCalculator().Calculate(ChargingProfile(), [badCar, CostExamples.Car("other", sources: [CostExamples.Petrol()])]);
        Assert.Contains(preview.Vehicles[0].Energy.Cost.Errors, error => error.Code == "energyBasisMismatch");
        Assert.Null(preview.Vehicles[0].Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(0m, preview.Vehicles[0].Depreciation.Cost.CompleteTotalSek);
        Assert.Equal(14_400m, preview.Vehicles[1].Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Zero_distance_needs_no_energy_assumptions_and_only_disables_the_per_mil_measure()
    {
        var car = CostExamples.Car(sources: [new("unknown", null, null, null, null)]) with
        {
            Tax = CostExamples.Category("tax", 1200, HouseholdCostCadence.Annual),
        };
        var result = CostExamples.Calculate(car);
        Assert.Equal(0m, result.Energy.Cost.CompleteTotalSek);
        Assert.Equal(1200m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(100m, result.Totals.MonthlyCost.CompleteTotalSek);
        Assert.Null(result.Totals.CostPerMil.CompleteTotalSek);
        Assert.Contains("zeroDistance", result.Totals.CostPerMil.MissingComponents);
    }

    [Fact]
    public void Missing_electricity_basis_preserves_base_quantity_but_not_purchased_energy()
    {
        var car = CostExamples.Car(sources: [CostExamples.Electricity() with { ElectricityBasis = null }]);
        var source = Assert.Single(CostExamples.Calculate(car, ChargingProfile()).Energy.Sources);
        Assert.Equal(2160m, source.BaseQuantity);
        Assert.Null(source.PurchasedQuantity);
        Assert.Null(source.Cost.CompleteTotalSek);
        Assert.Contains("vehicles[0].energySources[0].electricityBasis", source.Cost.MissingComponents);
    }

    [Fact]
    public void Missing_consumption_basis_preserves_price_without_guessing_quantities()
    {
        var car = CostExamples.Car(sources: [CostExamples.Petrol() with { ConsumptionBasis = null }]);
        var source = Assert.Single(CostExamples.Calculate(car, ChargingProfile()).Energy.Sources);
        Assert.Null(source.BaseQuantity);
        Assert.Equal(20m, source.EffectivePricePerUnitSek);
        Assert.Null(source.Cost.CompleteTotalSek);
        Assert.Contains("vehicles[0].energySources[0].consumptionBasis", source.Cost.MissingComponents);
    }

    [Fact]
    public void Rounded_distance_and_energy_quantities_are_not_used_to_compute_cost_per_mil()
    {
        var result = CostExamples.Calculate(CostExamples.Car(sources: [CostExamples.Petrol()]), CostExamples.Profile(1, 1));
        Assert.Equal(0.083m, result.Totals.DistanceKilometres);
        Assert.Equal(0.1m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.Equal(12m, result.Totals.CostPerMil.CompleteTotalSek);
    }

    [Fact]
    public void An_inactive_invalid_fuel_price_affects_only_candidates_using_that_price()
    {
        var profile = CostExamples.Profile(12, 12_000,
            [new(FuelType.Petrol, EnergyUnit.Litre, SensitivityValue.Scenarios(10, 20, -1)),
             new(FuelType.Electricity, EnergyUnit.KilowattHour, CostExamples.Value(1000))]) with
        {
            HomeChargingSharePercent = CostExamples.Value(100), HomeChargingPricePerKilowattHourSek = CostExamples.Value(2),
        };
        var result = new HouseholdCostCalculator().Calculate(profile,
            [CostExamples.Car(sources: [CostExamples.Petrol()]), CostExamples.Car("electric", sources: [CostExamples.Electricity(ElectricityBasis.Metered)])]);
        Assert.Single(result.ProfileErrors);
        Assert.Equal(CostSectionState.Invalid, result.Vehicles[0].Energy.Cost.State);
        Assert.Equal(4320m, result.Vehicles[1].Energy.Cost.CompleteTotalSek);
    }

    [Fact]
    public void Extreme_loss_overflow_preserves_base_quantity_other_source_and_other_candidate()
    {
        var profile = ChargingProfile() with
        {
            AnnualDistanceKilometres = 1_000_000, PeriodMonths = 120,
            ChargingLossPercent = CostExamples.Value(99.99999999999999999999999999m),
        };
        var car = CostExamples.Car(sources: [CostExamples.Electricity() with { ConsumptionPer100Kilometres = CostExamples.Value(10_000) }, CostExamples.Petrol()]);
        var result = new HouseholdCostCalculator().Calculate(profile, [car, CostExamples.Car("other", sources: [CostExamples.Petrol()])]);
        var energy = result.Vehicles[0].Energy;
        Assert.Equal(1_000_000_000m, energy.Sources[0].BaseQuantity);
        Assert.Null(energy.Sources[0].PurchasedQuantity);
        Assert.Contains(energy.Cost.Errors, error => error.Code == "calculationOutOfRange");
        Assert.Equal(12_000_000m, energy.Cost.KnownSubtotalSek);
        Assert.Equal(12_000_000m, result.Vehicles[1].Totals.OwnershipCost.CompleteTotalSek);
    }

    [Fact]
    public void Aggregate_overflow_has_no_fake_subtotal_and_preserves_representable_source_rows()
    {
        var profile = ChargingProfile() with
        {
            AnnualDistanceKilometres = 1_000_000, PeriodMonths = 120,
            ChargingLossPercent = CostExamples.Value(99.9999999999999m),
            HomeChargingSharePercent = CostExamples.Value(100), HomeChargingPricePerKilowattHourSek = CostExamples.Value(100_000),
        };
        var source = CostExamples.Electricity() with { ConsumptionPer100Kilometres = CostExamples.Value(6000) };
        var result = CostExamples.Calculate(CostExamples.Car(sources: [source, source with { Key = "second" }]), profile);
        Assert.All(result.Energy.Sources, row => Assert.Equal(60_000_000_000_000_000_000_000_000_000m, row.Cost.CompleteTotalSek));
        Assert.Null(result.Energy.Cost.KnownSubtotalSek);
        Assert.Null(result.Totals.OwnershipCost.KnownSubtotalSek);
        Assert.Equal(CostSectionState.Invalid, result.Energy.Cost.State);
    }

    [Fact]
    public void Per_mil_overflow_does_not_hide_a_representable_total_or_monthly_cost()
    {
        var profile = ChargingProfile() with
        {
            AnnualDistanceKilometres = 0.00000000000000000001m,
            ChargingLossPercent = CostExamples.Value(99.99999999999999999999999999m),
            HomeChargingSharePercent = CostExamples.Value(100), HomeChargingPricePerKilowattHourSek = CostExamples.Value(100_000),
        };
        var source = CostExamples.Electricity() with { ConsumptionPer100Kilometres = CostExamples.Value(1) };
        var result = CostExamples.Calculate(CostExamples.Car(sources: [source]), profile);
        Assert.Equal(100_000_000_000m, result.Totals.OwnershipCost.CompleteTotalSek);
        Assert.NotNull(result.Totals.MonthlyCost.CompleteTotalSek);
        Assert.Null(result.Totals.CostPerMil.KnownSubtotalSek);
        Assert.Contains(result.Totals.CostPerMil.Errors, error => error.Code == "calculationOutOfRange");
    }

    private static HouseholdProfileInput ChargingProfile() => CostExamples.Profile(12, 12_000) with
    {
        ElectricDrivingSharePercent = CostExamples.Value(60), ChargingLossPercent = CostExamples.Value(10),
        HomeChargingSharePercent = CostExamples.Value(80), HomeChargingPricePerKilowattHourSek = CostExamples.Value(2),
        PublicChargingPricePerKilowattHourSek = CostExamples.Value(5),
    };
}
