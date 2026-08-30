using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.IntegrationTests;

internal static class SavedCostScenarioTestData
{
    public static ManualCalculationRequest Complete(string? vehicleLabel = " Example car ")
    {
        return new ManualCalculationRequest
        {
            VehicleLabel = vehicleLabel,
            CalculationPeriodMonths = 12,
            PurchasePriceSek = 20_000m,
            ExpectedResidualValueSek = 15_000m,
            AnnualDistanceKilometres = 15_000m,
            Financing = new FinancingInput
            {
                DownPaymentSek = 5_000m,
                AnnualNominalInterestRatePercent = 0m,
                TermMonths = 12,
            },
            EnergySources =
            [
                new EnergySourceInput
                {
                    Label = " Petrol ",
                    Unit = EnergyUnit.Litre,
                    ConsumptionPer100Kilometres = 8m,
                    PricePerUnitSek = 20m,
                    DistanceSharePercent = 100m,
                },
            ],
            VehicleTax = new RecurringCostInput
            {
                AmountSek = 2_400m,
                Cadence = RecurringCostCadence.Annual,
            },
            Insurance = new RecurringCostInput
            {
                AmountSek = 500m,
                Cadence = RecurringCostCadence.Monthly,
            },
            MaintenanceAndRepairs = new RecurringCostInput
            {
                AmountSek = 6_000m,
                Cadence = RecurringCostCadence.Annual,
            },
            OtherRecurringCosts =
            [
                new NamedRecurringCostInput
                {
                    Label = " Parking ",
                    AmountSek = 300m,
                    Cadence = RecurringCostCadence.Monthly,
                },
            ],
            OtherOneTimeCosts =
            [
                new OneTimeCostInput
                {
                    Label = " Initial repair ",
                    AmountSek = 2_000m,
                },
            ],
        };
    }

    public static ManualCalculationRequest Incomplete(string? vehicleLabel = null)
    {
        return new ManualCalculationRequest
        {
            VehicleLabel = vehicleLabel,
            CalculationPeriodMonths = 12,
            PurchasePriceSek = 10_000m,
            ExpectedResidualValueSek = null,
            AnnualDistanceKilometres = 0m,
            Financing = null,
            EnergySources = [],
            VehicleTax = null,
            Insurance = null,
            MaintenanceAndRepairs = null,
            OtherRecurringCosts = [],
            OtherOneTimeCosts = [],
        };
    }

    public static ManualCalculationRequest Replacement()
    {
        return new ManualCalculationRequest
        {
            VehicleLabel = " Replacement car ",
            CalculationPeriodMonths = 6,
            PurchasePriceSek = 12_000m,
            ExpectedResidualValueSek = 8_000m,
            AnnualDistanceKilometres = 0m,
            Financing = null,
            EnergySources = [],
            VehicleTax = new RecurringCostInput
            {
                AmountSek = 0m,
                Cadence = RecurringCostCadence.Annual,
            },
            Insurance = new RecurringCostInput
            {
                AmountSek = 0m,
                Cadence = RecurringCostCadence.Monthly,
            },
            MaintenanceAndRepairs = new RecurringCostInput
            {
                AmountSek = 0m,
                Cadence = RecurringCostCadence.Annual,
            },
            OtherRecurringCosts =
            [
                new NamedRecurringCostInput
                {
                    Label = " Storage ",
                    AmountSek = 100m,
                    Cadence = RecurringCostCadence.Monthly,
                },
            ],
            OtherOneTimeCosts =
            [
                new OneTimeCostInput
                {
                    Label = " Inspection ",
                    AmountSek = 500m,
                },
            ],
        };
    }
}
