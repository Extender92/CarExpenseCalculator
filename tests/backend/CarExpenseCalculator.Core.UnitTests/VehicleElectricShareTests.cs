using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using Xunit;

namespace CarExpenseCalculator.Core.UnitTests;

public sealed class VehicleElectricShareTests
{
    private static VehicleCostInput Hybrid(string key, VehicleElectricShare? share = null) =>
        CostExamples.Car(key, sources: [
            CostExamples.Electricity(ElectricityBasis.Metered) with { ConsumptionBasis = ConsumptionBasis.DrivingMode },
            CostExamples.Petrol() with { ConsumptionBasis = ConsumptionBasis.DrivingMode }
        ]) with { ElectricDrivingShare = share ?? VehicleElectricShare.Inherit };

    private static HouseholdProfileInput Profile(decimal shared = 50) => CostExamples.Profile(12, 12000) with
    {
        ElectricDrivingSharePercent = SensitivityValue.Constant(shared),
        HomeChargingSharePercent = SensitivityValue.Constant(100),
        HomeChargingPricePerKilowattHourSek = SensitivityValue.Constant(2),
    };

    [Fact]
    public void Two_cars_keep_independent_shares_and_inheritance_follows_household_changes()
    {
        var cars = new[] { Hybrid("first", new(ElectricShareMode.Override, SensitivityValue.Constant(30))),
            Hybrid("second", new(ElectricShareMode.Override, SensitivityValue.Constant(70))), Hybrid("inherited") };
        var calculator = new HouseholdCostCalculator();
        var first = calculator.Calculate(Profile(), cars).Vehicles;
        var changed = calculator.Calculate(Profile(80), cars).Vehicles;
        Assert.Equal(648m, first[0].Energy.Sources[0].BaseQuantity);
        Assert.Equal(1512m, first[1].Energy.Sources[0].BaseQuantity);
        Assert.Equal(1080m, first[2].Energy.Sources[0].BaseQuantity);
        Assert.Equal(1728m, changed[2].Energy.Sources[0].BaseQuantity);
        Assert.Equal(first[0].Energy.Sources[0].BaseQuantity, changed[0].Energy.Sources[0].BaseQuantity);
        Assert.Equal(first[1].Energy.Sources[0].BaseQuantity, changed[1].Energy.Sources[0].BaseQuantity);
        Assert.Equal(new(ElectricShareOrigin.Vehicle, 30m), first[0].Energy.ElectricDrivingShare);
        Assert.Equal(new(ElectricShareOrigin.Household, 80m), changed[2].Energy.ElectricDrivingShare);
    }

    [Fact]
    public void Explicit_unknown_override_never_falls_back_and_returning_to_inherit_works()
    {
        var car = Hybrid("car", new(ElectricShareMode.Override));
        var result = CostExamples.Calculate(car, Profile()).Energy;
        Assert.Null(result.Sources[0].BaseQuantity);
        Assert.Null(result.Cost.CompleteTotalSek);
        Assert.Contains("vehicles[0].electricDrivingShare.value", result.Cost.MissingComponents);
        Assert.DoesNotContain("profile.electricDrivingSharePercent", result.Cost.MissingComponents);
        Assert.Equal(new(ElectricShareOrigin.Vehicle, null), result.ElectricDrivingShare);
        Assert.Equal(1080m, CostExamples.Calculate(car with { ElectricDrivingShare = VehicleElectricShare.Inherit }, Profile()).Energy.Sources[0].BaseQuantity);
    }

    [Theory]
    [InlineData(SensitivityMode.Favorable, 100)]
    [InlineData(SensitivityMode.Baseline, 30)]
    [InlineData(SensitivityMode.Cautious, 0)]
    public void Complete_scenarios_select_the_exact_car_value(SensitivityMode mode, int expected)
    {
        var car = Hybrid("car", new(ElectricShareMode.Override, SensitivityValue.Scenarios(100, 30, 0)));
        var result = CostExamples.Calculate(car, Profile() with { ActiveSensitivityMode = mode }).Energy;
        Assert.Equal((decimal)expected, result.ElectricDrivingShare!.Percent);
        Assert.Equal(2160m * expected / 100m, result.Sources[0].BaseQuantity);
    }

    [Fact]
    public void Exact_override_is_preserved_and_invalid_values_are_reported()
    {
        const decimal exact = 30.123456789012345678901234567m;
        var car = Hybrid("car", new(ElectricShareMode.Override, SensitivityValue.Constant(exact)));
        Assert.Empty(HouseholdCostInputValidator.ValidateVehicle(car));
        Assert.Equal(exact, CostExamples.Calculate(car, Profile()).Energy.ElectricDrivingShare!.Percent);
        foreach (var invalid in new[] { -0.00001m, 100.00001m })
            Assert.Contains(HouseholdCostInputValidator.ValidateVehicle(car with { ElectricDrivingShare = new(ElectricShareMode.Override, SensitivityValue.Constant(invalid)) }),
                x => x.Path == "vehicle.electricDrivingShare.value.single" && x.Code == "outOfRange");
        Assert.Contains(HouseholdCostInputValidator.ValidateVehicle(car with { ElectricDrivingShare = new(ElectricShareMode.Inherit, SensitivityValue.Constant(0)) }),
            x => x.Code == "invalidStructure");
    }

    [Fact]
    public void Whole_distance_and_single_source_ignore_unknown_override()
    {
        foreach (var sources in new[] { new[] { CostExamples.Petrol() }, new[] { CostExamples.Electricity(ElectricityBasis.Metered) },
            new[] { CostExamples.Electricity(ElectricityBasis.Metered), CostExamples.Petrol() } })
        {
            var car = CostExamples.Car(sources: sources);
            var expected = CostExamples.Calculate(car, Profile()).Energy;
            var actual = CostExamples.Calculate(car with { ElectricDrivingShare = new(ElectricShareMode.Override) }, Profile()).Energy;
            Assert.Equal(expected.Cost, actual.Cost);
            Assert.Equal(expected.Sources.Select(x => x.BaseQuantity), actual.Sources.Select(x => x.BaseQuantity));
            Assert.Null(actual.ElectricDrivingShare);
        }
    }

    [Fact]
    public void Changing_override_choice_or_value_invalidates_cost_confirmation()
    {
        var car = Hybrid("ABC123");
        var confirmation = CostAssumptionConfirmation.Confirm(car, DateTimeOffset.UtcNow);
        Assert.True(confirmation.IsApplicableTo(car));
        Assert.False(confirmation.IsApplicableTo(car with { ElectricDrivingShare = new(ElectricShareMode.Override) }));
        Assert.False(confirmation.IsApplicableTo(car with { ElectricDrivingShare = new(ElectricShareMode.Override, SensitivityValue.Constant(50)) }));
    }
}
