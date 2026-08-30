namespace CarExpenseCalculator.Core.CostScenarios;

public sealed class CostScenarioCalculator
{
    private const string Currency = "SEK";

    public CostCalculationResult Calculate(CostScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var validationErrors = CostScenarioValidator.Validate(scenario);
        if (validationErrors.Count > 0)
        {
            throw new CostScenarioValidationException(validationErrors);
        }

        var totalDistanceKilometres =
            scenario.AnnualDistanceKilometres * scenario.CalculationPeriodMonths / 12m;
        var energy = CalculateEnergy(scenario.EnergySources, totalDistanceKilometres);
        var vehicleTaxSek = CalculateRecurringCost(scenario.VehicleTax, scenario.CalculationPeriodMonths);
        var insuranceSek = CalculateRecurringCost(scenario.Insurance, scenario.CalculationPeriodMonths);
        var maintenanceAndRepairsSek =
            CalculateRecurringCost(scenario.MaintenanceAndRepairs, scenario.CalculationPeriodMonths);
        var recurringCosts = CalculateOtherRecurringCosts(
            scenario.OtherRecurringCosts,
            scenario.CalculationPeriodMonths);
        var oneTimeCosts = CalculateOneTimeCosts(scenario.OtherOneTimeCosts);
        var otherRecurringCostSek = recurringCosts.Sum(item => item.UnroundedCostDuringPeriodSek);
        var otherOneTimeCostSek = oneTimeCosts.Sum(item => item.UnroundedAmountSek);
        var knownOperatingCostSek =
            energy.UnroundedTotalCostSek
            + (vehicleTaxSek ?? 0m)
            + (insuranceSek ?? 0m)
            + (maintenanceAndRepairsSek ?? 0m)
            + otherRecurringCostSek
            + otherOneTimeCostSek;

        var financing = scenario.Financing is null
            ? null
            : CalculateFinancing(
                scenario.PurchasePriceSek,
                scenario.Financing,
                scenario.CalculationPeriodMonths);
        var loanPaymentsDuringPeriodSek = financing?.UnroundedLoanPaymentsDuringPeriodSek ?? 0m;
        var interestPaidSek = financing?.UnroundedInterestPaidSek ?? 0m;
        var remainingPrincipalSek = financing?.UnroundedRemainingPrincipalSek ?? 0m;
        var acquisitionCashPaidSek = scenario.Financing is null
            ? scenario.PurchasePriceSek
            : scenario.Financing.DownPaymentSek;
        var knownCashOutflowSek =
            acquisitionCashPaidSek + loanPaymentsDuringPeriodSek + knownOperatingCostSek;

        var completeness = CreateCompleteness(scenario);
        var cashFlow = new CashFlowResult(
            RoundMoney(acquisitionCashPaidSek),
            RoundMoney(loanPaymentsDuringPeriodSek),
            RoundMoney(energy.UnroundedTotalCostSek),
            RoundNullableMoney(vehicleTaxSek),
            RoundNullableMoney(insuranceSek),
            RoundNullableMoney(maintenanceAndRepairsSek),
            RoundMoney(otherRecurringCostSek),
            RoundMoney(otherOneTimeCostSek),
            RoundMoney(knownOperatingCostSek),
            RoundMoney(knownCashOutflowSek),
            RoundMoney(knownCashOutflowSek / scenario.CalculationPeriodMonths),
            RoundMoney(knownCashOutflowSek * 12m / scenario.CalculationPeriodMonths),
            completeness.IsCashFlowComplete);
        var netOwnershipCost = scenario.ExpectedResidualValueSek is { } residualValue
            ? CalculateNetOwnershipCost(
                scenario,
                residualValue,
                interestPaidSek,
                remainingPrincipalSek,
                knownOperatingCostSek,
                completeness.IsCashFlowComplete)
            : null;

        return new CostCalculationResult(
            Currency,
            scenario.CalculationPeriodMonths,
            RoundQuantity(totalDistanceKilometres),
            completeness,
            cashFlow,
            financing?.Result,
            energy.Result,
            Array.AsReadOnly(recurringCosts.Select(item => item.Result).ToArray()),
            Array.AsReadOnly(oneTimeCosts.Select(item => item.Result).ToArray()),
            netOwnershipCost);
    }

    private static EnergyCalculation CalculateEnergy(
        IReadOnlyList<EnergySource> sources,
        decimal totalDistanceKilometres)
    {
        var results = new List<EnergySourceResult>(sources.Count);
        var totalCostSek = 0m;

        foreach (var source in sources)
        {
            var allocatedDistanceKilometres =
                totalDistanceKilometres * source.DistanceSharePercent / 100m;
            var consumedQuantity =
                allocatedDistanceKilometres * source.ConsumptionPer100Kilometres / 100m;
            var costSek = consumedQuantity * source.PricePerUnitSek;
            totalCostSek += costSek;

            results.Add(new EnergySourceResult(
                source.Label.Trim(),
                source.Unit,
                RoundQuantity(source.DistanceSharePercent),
                RoundQuantity(allocatedDistanceKilometres),
                RoundQuantity(source.ConsumptionPer100Kilometres),
                RoundQuantity(consumedQuantity),
                RoundMoney(source.PricePerUnitSek),
                RoundMoney(costSek)));
        }

        return new EnergyCalculation(
            new EnergyBreakdownResult(
                Array.AsReadOnly(results.ToArray()),
                RoundMoney(totalCostSek)),
            totalCostSek);
    }

    private static FinancingCalculation CalculateFinancing(
        decimal purchasePriceSek,
        FinancingTerms financing,
        int calculationPeriodMonths)
    {
        var principalSek = purchasePriceSek - financing.DownPaymentSek;
        var monthlyInterestRate = financing.AnnualNominalInterestRatePercent / 100m / 12m;
        var monthlyPaymentSek = monthlyInterestRate == 0m
            ? principalSek / financing.TermMonths
            : CalculateAnnuityPayment(principalSek, monthlyInterestRate, financing.TermMonths);
        var paymentsMade = Math.Min(calculationPeriodMonths, financing.TermMonths);
        var loanPaymentsDuringPeriodSek = monthlyPaymentSek * paymentsMade;
        var remainingPrincipalSek = paymentsMade == financing.TermMonths
            ? 0m
            : CalculateRemainingPrincipal(
                principalSek,
                monthlyPaymentSek,
                monthlyInterestRate,
                paymentsMade);
        var principalRepaidSek = principalSek - remainingPrincipalSek;
        var interestPaidSek = loanPaymentsDuringPeriodSek - principalRepaidSek;

        return new FinancingCalculation(
            new FinancingResult(
                RoundMoney(financing.DownPaymentSek),
                RoundMoney(principalSek),
                RoundQuantity(financing.AnnualNominalInterestRatePercent),
                financing.TermMonths,
                RoundMoney(monthlyPaymentSek),
                paymentsMade,
                RoundMoney(loanPaymentsDuringPeriodSek),
                RoundMoney(principalRepaidSek),
                RoundMoney(interestPaidSek),
                RoundMoney(remainingPrincipalSek)),
            loanPaymentsDuringPeriodSek,
            interestPaidSek,
            remainingPrincipalSek);
    }

    private static decimal CalculateAnnuityPayment(
        decimal principalSek,
        decimal monthlyInterestRate,
        int termMonths)
    {
        var compoundFactor = Pow(1m + monthlyInterestRate, termMonths);
        return principalSek
            * monthlyInterestRate
            * compoundFactor
            / (compoundFactor - 1m);
    }

    private static decimal CalculateRemainingPrincipal(
        decimal principalSek,
        decimal monthlyPaymentSek,
        decimal monthlyInterestRate,
        int paymentsMade)
    {
        if (monthlyInterestRate == 0m)
        {
            return principalSek - (monthlyPaymentSek * paymentsMade);
        }

        var compoundFactor = Pow(1m + monthlyInterestRate, paymentsMade);
        return principalSek
            * compoundFactor
            - monthlyPaymentSek * ((compoundFactor - 1m) / monthlyInterestRate);
    }

    private static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        for (var index = 0; index < exponent; index++)
        {
            result *= value;
        }

        return result;
    }

    private static IReadOnlyList<RecurringCostCalculation> CalculateOtherRecurringCosts(
        IReadOnlyList<NamedRecurringCost> costs,
        int calculationPeriodMonths)
    {
        var results = new List<RecurringCostCalculation>(costs.Count);
        foreach (var cost in costs)
        {
            var costDuringPeriodSek = CalculateRecurringCost(
                new RecurringCost(cost.AmountSek, cost.Cadence),
                calculationPeriodMonths)!.Value;
            results.Add(new RecurringCostCalculation(
                new RecurringCostResult(
                    cost.Label.Trim(),
                    RoundMoney(cost.AmountSek),
                    cost.Cadence,
                    RoundMoney(costDuringPeriodSek)),
                costDuringPeriodSek));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    private static IReadOnlyList<OneTimeCostCalculation> CalculateOneTimeCosts(
        IReadOnlyList<OneTimeCost> costs)
    {
        return Array.AsReadOnly(costs
            .Select(cost => new OneTimeCostCalculation(
                new OneTimeCostResult(cost.Label.Trim(), RoundMoney(cost.AmountSek)),
                cost.AmountSek))
            .ToArray());
    }

    private static decimal? CalculateRecurringCost(RecurringCost? cost, int calculationPeriodMonths)
    {
        return cost?.Cadence switch
        {
            null => null,
            RecurringCostCadence.Monthly => cost.AmountSek * calculationPeriodMonths,
            RecurringCostCadence.Annual => cost.AmountSek * calculationPeriodMonths / 12m,
            _ => throw new InvalidOperationException("Unsupported recurring-cost cadence."),
        };
    }

    private static CalculationCompleteness CreateCompleteness(CostScenario scenario)
    {
        var missingCategories = new List<MissingCostCategory>(4);
        if (scenario.VehicleTax is null)
        {
            missingCategories.Add(MissingCostCategory.VehicleTax);
        }

        if (scenario.Insurance is null)
        {
            missingCategories.Add(MissingCostCategory.Insurance);
        }

        if (scenario.MaintenanceAndRepairs is null)
        {
            missingCategories.Add(MissingCostCategory.MaintenanceAndRepairs);
        }

        if (scenario.ExpectedResidualValueSek is null)
        {
            missingCategories.Add(MissingCostCategory.ResidualValue);
        }

        var isCashFlowComplete =
            scenario.VehicleTax is not null
            && scenario.Insurance is not null
            && scenario.MaintenanceAndRepairs is not null;

        return new CalculationCompleteness(
            missingCategories.Count == 0,
            isCashFlowComplete,
            scenario.ExpectedResidualValueSek is not null,
            Array.AsReadOnly(missingCategories.ToArray()));
    }

    private static NetOwnershipCostResult CalculateNetOwnershipCost(
        CostScenario scenario,
        decimal residualValueSek,
        decimal interestPaidSek,
        decimal remainingPrincipalSek,
        decimal knownOperatingCostSek,
        bool isCashFlowComplete)
    {
        var depreciationSek = scenario.PurchasePriceSek - residualValueSek;
        var knownTotalSek = depreciationSek + interestPaidSek + knownOperatingCostSek;
        var estimatedEquityAtPeriodEndSek = residualValueSek - remainingPrincipalSek;

        return new NetOwnershipCostResult(
            RoundMoney(residualValueSek),
            RoundMoney(depreciationSek),
            RoundMoney(interestPaidSek),
            RoundMoney(knownOperatingCostSek),
            RoundMoney(knownTotalSek),
            RoundMoney(knownTotalSek / scenario.CalculationPeriodMonths),
            RoundMoney(knownTotalSek * 12m / scenario.CalculationPeriodMonths),
            RoundMoney(estimatedEquityAtPeriodEndSek),
            isCashFlowComplete);
    }

    private static decimal? RoundNullableMoney(decimal? value)
    {
        return value is null ? null : RoundMoney(value.Value);
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundQuantity(decimal value)
    {
        return decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private sealed record EnergyCalculation(
        EnergyBreakdownResult Result,
        decimal UnroundedTotalCostSek);

    private sealed record FinancingCalculation(
        FinancingResult Result,
        decimal UnroundedLoanPaymentsDuringPeriodSek,
        decimal UnroundedInterestPaidSek,
        decimal UnroundedRemainingPrincipalSek);

    private sealed record RecurringCostCalculation(
        RecurringCostResult Result,
        decimal UnroundedCostDuringPeriodSek);

    private sealed record OneTimeCostCalculation(
        OneTimeCostResult Result,
        decimal UnroundedAmountSek);
}
