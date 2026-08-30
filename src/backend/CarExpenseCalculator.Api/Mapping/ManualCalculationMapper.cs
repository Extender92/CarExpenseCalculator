using ApiContracts = CarExpenseCalculator.Api.Contracts.ManualCalculations;
using CoreContracts = CarExpenseCalculator.Core.CostScenarios;

namespace CarExpenseCalculator.Api.Mapping;

internal static class ManualCalculationMapper
{
    public static CoreContracts.CostScenario ToCore(ApiContracts.ManualCalculationRequest request)
    {
        return new CoreContracts.CostScenario(
            request.VehicleLabel,
            request.CalculationPeriodMonths,
            request.PurchasePriceSek,
            request.ExpectedResidualValueSek,
            request.AnnualDistanceKilometres,
            MapFinancing(request.Financing),
            request.EnergySources.Select(source => source is null ? null! : MapEnergySource(source)),
            MapRecurringCost(request.VehicleTax),
            MapRecurringCost(request.Insurance),
            MapRecurringCost(request.MaintenanceAndRepairs),
            request.OtherRecurringCosts.Select(cost => cost is null ? null! : MapRecurringCost(cost)),
            request.OtherOneTimeCosts.Select(cost => cost is null ? null! : MapOneTimeCost(cost)));
    }

    public static ApiContracts.ManualCalculationResult ToApi(CoreContracts.CostCalculationResult result)
    {
        return new ApiContracts.ManualCalculationResult(
            result.Currency,
            result.CalculationPeriodMonths,
            result.TotalDistanceKilometres,
            new ApiContracts.CalculationCompleteness(
                result.Completeness.IsComplete,
                result.Completeness.IsCashFlowComplete,
                result.Completeness.IsNetOwnershipCostAvailable,
                result.Completeness.MissingCategories.Select(MapMissingCategory).ToArray()),
            MapCashFlow(result.CashFlow),
            result.Financing is null ? null : MapFinancing(result.Financing),
            new ApiContracts.EnergyBreakdownResult(
                result.Energy.Sources.Select(MapEnergySource).ToArray(),
                result.Energy.TotalCostSek),
            result.OtherRecurringCosts.Select(MapRecurringCost).ToArray(),
            result.OtherOneTimeCosts.Select(MapOneTimeCost).ToArray(),
            result.NetOwnershipCost is null ? null : MapNetOwnershipCost(result.NetOwnershipCost));
    }

    private static CoreContracts.FinancingTerms? MapFinancing(ApiContracts.FinancingInput? financing)
    {
        return financing is null
            ? null
            : new CoreContracts.FinancingTerms(
                financing.DownPaymentSek,
                financing.AnnualNominalInterestRatePercent,
                financing.TermMonths);
    }

    private static CoreContracts.EnergySource MapEnergySource(ApiContracts.EnergySourceInput source)
    {
        return new CoreContracts.EnergySource(
            source.Label,
            MapEnergyUnit(source.Unit),
            source.ConsumptionPer100Kilometres,
            source.PricePerUnitSek,
            source.DistanceSharePercent);
    }

    private static CoreContracts.RecurringCost? MapRecurringCost(ApiContracts.RecurringCostInput? cost)
    {
        return cost is null
            ? null
            : new CoreContracts.RecurringCost(cost.AmountSek, MapCadence(cost.Cadence));
    }

    private static CoreContracts.NamedRecurringCost MapRecurringCost(
        ApiContracts.NamedRecurringCostInput cost)
    {
        return new CoreContracts.NamedRecurringCost(
            cost.Label,
            cost.AmountSek,
            MapCadence(cost.Cadence));
    }

    private static CoreContracts.OneTimeCost MapOneTimeCost(ApiContracts.OneTimeCostInput cost)
    {
        return new CoreContracts.OneTimeCost(cost.Label, cost.AmountSek);
    }

    private static ApiContracts.CashFlowResult MapCashFlow(CoreContracts.CashFlowResult result)
    {
        return new ApiContracts.CashFlowResult(
            result.AcquisitionCashPaidSek,
            result.LoanPaymentsDuringPeriodSek,
            result.EnergyCostSek,
            result.VehicleTaxSek,
            result.InsuranceSek,
            result.MaintenanceAndRepairsSek,
            result.OtherRecurringCostSek,
            result.OtherOneTimeCostSek,
            result.KnownOperatingCostSek,
            result.KnownTotalSek,
            result.AveragePerMonthSek,
            result.AveragePerYearSek,
            result.IsComplete);
    }

    private static ApiContracts.FinancingResult MapFinancing(CoreContracts.FinancingResult result)
    {
        return new ApiContracts.FinancingResult(
            result.DownPaymentSek,
            result.PrincipalSek,
            result.AnnualNominalInterestRatePercent,
            result.TermMonths,
            result.MonthlyPaymentSek,
            result.PaymentsMade,
            result.LoanPaymentsDuringPeriodSek,
            result.PrincipalRepaidSek,
            result.InterestPaidSek,
            result.RemainingPrincipalSek);
    }

    private static ApiContracts.EnergySourceResult MapEnergySource(CoreContracts.EnergySourceResult result)
    {
        return new ApiContracts.EnergySourceResult(
            result.Label,
            MapEnergyUnit(result.Unit),
            result.DistanceSharePercent,
            result.AllocatedDistanceKilometres,
            result.ConsumptionPer100Kilometres,
            result.ConsumedQuantity,
            result.PricePerUnitSek,
            result.CostSek);
    }

    private static ApiContracts.RecurringCostResult MapRecurringCost(
        CoreContracts.RecurringCostResult result)
    {
        return new ApiContracts.RecurringCostResult(
            result.Label,
            result.AmountSek,
            MapCadence(result.Cadence),
            result.CostDuringPeriodSek);
    }

    private static ApiContracts.OneTimeCostResult MapOneTimeCost(CoreContracts.OneTimeCostResult result)
    {
        return new ApiContracts.OneTimeCostResult(result.Label, result.AmountSek);
    }

    private static ApiContracts.NetOwnershipCostResult MapNetOwnershipCost(
        CoreContracts.NetOwnershipCostResult result)
    {
        return new ApiContracts.NetOwnershipCostResult(
            result.ResidualValueSek,
            result.DepreciationSek,
            result.InterestPaidSek,
            result.KnownOperatingCostSek,
            result.KnownTotalSek,
            result.AveragePerMonthSek,
            result.AveragePerYearSek,
            result.EstimatedEquityAtPeriodEndSek,
            result.IsComplete);
    }

    private static CoreContracts.EnergyUnit MapEnergyUnit(ApiContracts.EnergyUnit unit)
    {
        return unit switch
        {
            ApiContracts.EnergyUnit.Litre => CoreContracts.EnergyUnit.Litre,
            ApiContracts.EnergyUnit.KilowattHour => CoreContracts.EnergyUnit.KilowattHour,
            ApiContracts.EnergyUnit.Kilogram => CoreContracts.EnergyUnit.Kilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported energy unit."),
        };
    }

    private static ApiContracts.EnergyUnit MapEnergyUnit(CoreContracts.EnergyUnit unit)
    {
        return unit switch
        {
            CoreContracts.EnergyUnit.Litre => ApiContracts.EnergyUnit.Litre,
            CoreContracts.EnergyUnit.KilowattHour => ApiContracts.EnergyUnit.KilowattHour,
            CoreContracts.EnergyUnit.Kilogram => ApiContracts.EnergyUnit.Kilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported energy unit."),
        };
    }

    private static CoreContracts.RecurringCostCadence MapCadence(
        ApiContracts.RecurringCostCadence cadence)
    {
        return cadence switch
        {
            ApiContracts.RecurringCostCadence.Monthly => CoreContracts.RecurringCostCadence.Monthly,
            ApiContracts.RecurringCostCadence.Annual => CoreContracts.RecurringCostCadence.Annual,
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, "Unsupported cadence."),
        };
    }

    private static ApiContracts.RecurringCostCadence MapCadence(
        CoreContracts.RecurringCostCadence cadence)
    {
        return cadence switch
        {
            CoreContracts.RecurringCostCadence.Monthly => ApiContracts.RecurringCostCadence.Monthly,
            CoreContracts.RecurringCostCadence.Annual => ApiContracts.RecurringCostCadence.Annual,
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, "Unsupported cadence."),
        };
    }

    private static ApiContracts.MissingCategory MapMissingCategory(
        CoreContracts.MissingCostCategory category)
    {
        return category switch
        {
            CoreContracts.MissingCostCategory.VehicleTax => ApiContracts.MissingCategory.VehicleTax,
            CoreContracts.MissingCostCategory.Insurance => ApiContracts.MissingCategory.Insurance,
            CoreContracts.MissingCostCategory.MaintenanceAndRepairs =>
                ApiContracts.MissingCategory.MaintenanceAndRepairs,
            CoreContracts.MissingCostCategory.ResidualValue => ApiContracts.MissingCategory.ResidualValue,
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Unsupported missing category."),
        };
    }
}
