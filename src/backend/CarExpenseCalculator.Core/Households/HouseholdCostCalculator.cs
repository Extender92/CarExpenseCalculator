namespace CarExpenseCalculator.Core.Households;

public sealed class HouseholdCostCalculator
{
    public HouseholdCostPreview Calculate(HouseholdProfileInput profile, IReadOnlyList<VehicleCostInput> vehicles)
        => CalculateForComparison(profile, vehicles).Preview;

    // The same calculation supplies presentation and ordering; no rounded re-composition.
    internal HouseholdComparisonCalculation CalculateForComparison(HouseholdProfileInput profile, IReadOnlyList<VehicleCostInput> vehicles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(vehicles);
        if (vehicles.Count > HouseholdInputValidator.MaximumCandidates)
            throw new HouseholdInputValidationException([new("vehicles", "tooManyItems", "At most 100 candidates are allowed.")]);
        var snapshot = vehicles.ToArray();
        var nullErrors = snapshot.Select((car, index) => (car, index)).Where(pair => pair.car is null)
            .Select(pair => new HouseholdInputError($"vehicles[{pair.index}]", "missingItem", "Candidate cannot be null.")).ToArray();
        if (nullErrors.Length > 0) throw new HouseholdInputValidationException(nullErrors);

        var financing = new HouseholdFinancingCalculator().Calculate(profile,
            snapshot.Select(car => new VehiclePurchaseInput(car.CandidateKey, car.PriceSek)).ToArray());
        var validations = snapshot.Select((car, index) => HouseholdCostInputValidator.ValidateVehicle(car, $"vehicles[{index}]")).ToArray();
        var structural = validations.SelectMany(errors => errors).Where(HouseholdCostInputValidator.IsStructural).ToArray();
        if (structural.Length > 0) throw new HouseholdInputValidationException(structural);

        var results = snapshot.Select((car, index) => CalculateVehicle(car, index, financing.Vehicles[index],
            new HouseholdCostContext(profile, financing.ProfileErrors, validations[index]), validations[index])).ToArray();
        return new(new("SEK", HouseholdCalculationVersions.Calculation, HouseholdCalculationVersions.ResultSchema,
            profile.ActiveSensitivityMode, financing.ProfileErrors, Array.AsReadOnly(results.Select(x => x.Result).ToArray())),
            Array.AsReadOnly(results));
    }

    private static HouseholdComparisonVehicle CalculateVehicle(VehicleCostInput car, int index, PurchaseFinancingResult financing,
        HouseholdCostContext context, IReadOnlyList<HouseholdInputError> inputErrors)
    {
        var path = $"vehicles[{index}]";
        var isLease = car.AcquisitionType == AcquisitionType.Lease;
        if (isLease) context.Coverage = HouseholdLeaseCalculator.Coverage(car.Lease, $"{path}.lease", context);
        var ledger = new HouseholdPaymentLedger(context, $"{path}.payments");
        var finance = isLease ? CostSection.Zero($"{path}.financing") : new CostSection($"{path}.financing");
        if (!isLease)
        {
            finance.HasDetails = financing.Allocation is not null;
            finance.Missing.AddRange(financing.MissingComponents);
            finance.Errors.AddRange(financing.Errors);
            if (financing.Loan is { } loan) finance.Add(loan.InterestPaidSek);
            if (financing.SetupFeeSek is { } setup) finance.Add(setup);
            if (financing.MonthlyFeesDuringPeriodSek is { } fees) finance.Add(fees);
            HouseholdPurchasePayments.Add(financing, context, ledger, path);
        }

        var distance = CalculateDistance(context, true);
        var (depreciation, residual) = isLease ? (CostSection.Zero($"{path}.residual"), (decimal?)null) : CalculateDepreciation(car, path, context);
        var (energy, energyResult) = HouseholdEnergyCalculator.Calculate(car.EnergySources, $"{path}.energySources",
            isLease ? CalculateDistance(context) : distance, context, out var rawEnergySources);
        if (isLease && car.Lease?.EnergyIncluded == true)
        {
            energy = CostSection.Zero($"{path}.energySources");
            energyResult = new(energy.Result(), Array.AsReadOnly(energyResult.Sources.Select(source => source with { Cost = energy.Result() }).ToArray()), true);
            rawEnergySources = [energy];
        }
        for (var sourceIndex = 0; sourceIndex < rawEnergySources.Count; sourceIndex++)
        {
            var amount = new CostSection($"{path}.energySources[{sourceIndex}]");
            amount.Merge(rawEnergySources[sourceIndex]);
            var months = context.Period(amount);
            ledger.Add($"{path}.energySources[{sourceIndex}]", "energy", "Estimated monthly energy", amount,
                months is not null ? Enumerable.Range(1, months.Value).ToArray() : null, true, distribute: true);
        }
        var categories = HouseholdCostInputValidator.Categories(car).Select(pair =>
            CalculateCategory(pair.Category, $"{path}.{pair.Name}", pair.Name, context, ledger)).ToArray();
        var allowance = new CostSection($"{path}.additionalRepairAllowancePerMonthSek");
        var allowanceAmount = context.Value(car.AdditionalRepairAllowancePerMonthSek, $"{path}.additionalRepairAllowancePerMonthSek", allowance);
        var allowancePeriod = context.Period(allowance);
        if (allowanceAmount is not null && allowancePeriod is not null) allowance.Add(allowanceAmount.Value * allowancePeriod.Value);
        var saving = HouseholdLeaseCalculator.Scalar(car.AdditionalRepairAllowancePerMonthSek, $"{path}.additionalRepairAllowancePerMonthSek", context);
        var savingPeriod = context.Period(saving);
        ledger.Add($"{path}.additionalRepairAllowancePerMonthSek", "repairAllowance", "Additional repair saving", saving,
            savingPeriod is not null ? Enumerable.Range(1, savingPeriod.Value).ToArray() : null, true, HouseholdPaymentDirection.InternalSaving);

        var lease = isLease ? HouseholdLeaseCalculator.Calculate(car.Lease, $"{path}.lease", context, ledger)
            : (Cost: CostSection.Zero($"{path}.lease"), Withheld: CostSection.Zero($"{path}.lease.depositWithheld"),
                Result: new HouseholdLeaseResult(CostSection.NotApplicable(), null, null, false, null, CostSection.NotApplicable()));

        var total = new CostSection($"{path}.totals.ownershipCost");
        foreach (var section in new[] { finance, depreciation, energy, allowance, lease.Cost }.Concat(categories.Select(pair => pair.Section)))
            total.Merge(section);
        var monthly = new CostSection($"{path}.totals.monthlyCost");
        monthly.CopyProblems(total);
        var period = context.RequestedPeriod(monthly);
        if (period is not null) monthly.AddTransformed(total, value => value / period.Value);

        var perMil = new CostSection($"{path}.totals.costPerMil");
        perMil.CopyProblems(total);
        perMil.CopyProblems(distance);
        if (distance.Complete == 0m) perMil.Missing.Add("zeroDistance");
        else if (distance.Complete is { } kilometres) perMil.AddTransformed(total, value => value / kilometres * 10m);

        var equity = isLease ? CostSection.Zero($"{path}.totals.endEquity") : CalculateEquity(residual, depreciation, financing, path);
        var operating = CostSection.Zero($"{path}.operatingCosts");
        operating.Merge(energy);
        foreach (var category in categories) operating.Merge(category.Section);
        var payments = ledger.Finish(operating, depreciation, allowance, lease.Withheld, total, isLease);
        var result = new VehicleCostResult(car.CandidateKey.Trim(), isLease ? null : financing, isLease ? CostSection.NotApplicable() : finance.Result(),
            new(isLease ? CostSection.NotApplicable() : depreciation.Result(), CostSection.Money(residual)),
            energyResult, categories[0].Result, categories[1].Result, categories[2].Result, categories[3].Result,
            allowance.Result(), categories[4].Result,
            new(CostSection.Quantity(distance.Complete), total.Result(), monthly.Result(), perMil.Result(), isLease ? CostSection.NotApplicable() : equity.Result()),
            inputErrors, car.AcquisitionType, lease.Result, payments.Calendar, payments.Startup, payments.Monthly, payments.Reconciliation);
        return new(result, total.Complete, monthly.Complete, perMil.Complete);
    }

    private static CostSection CalculateDistance(HouseholdCostContext context, bool requested = false)
    {
        var result = new CostSection("profile.annualDistanceKilometres");
        var annual = context.Value(context.Profile.AnnualDistanceKilometres, "profile.annualDistanceKilometres", result);
        if (annual == 0m) result.Add(0m);
        else
        {
            var period = requested ? context.RequestedPeriod(result) : context.Period(result);
            if (annual is not null && period is not null)
            {
                var distance = annual.Value * period.Value / 12m;
                if (distance == 0m)
                    result.Errors.Add(new("profile.annualDistanceKilometres", "calculationOutOfRange", "Positive distance is below decimal precision."));
                else result.Add(distance);
            }
        }

        return result;
    }

    private static (CostSection Cost, decimal? Residual) CalculateDepreciation(VehicleCostInput car, string path, HouseholdCostContext context)
    {
        var result = new CostSection($"{path}.residual");
        var price = context.Value(car.PriceSek, $"{path}.priceSek", result);
        var period = context.Period(result);
        if (car.Residual is not { } input)
        {
            result.Missing.Add($"{path}.residual");
            return (result, null);
        }

        var value = context.Value(input.Value, $"{path}.residual.value", result);
        if (input.Mode == ResidualMode.FixedAmount)
        {
            var fixedPeriodAvailable = context.Available($"{path}.residual.periodMonths", input.PeriodMonths is not null, result);
            if (fixedPeriodAvailable && period is not null && input.PeriodMonths != period)
                result.Errors.Add(new($"{path}.residual.periodMonths", "residualHorizonMismatch", "Fixed residual applies only to its entered horizon."));
        }

        if (price is null || value is null || period is null || result.Errors.Count > 0 || result.Missing.Count > 0) return (result, null);
        var residual = input.Mode == ResidualMode.FixedAmount ? value.Value : HouseholdDecimalMath.Residual(price.Value, value.Value, period.Value);
        result.Add(price.Value - residual);
        return (result, residual);
    }

    private static CostSection CalculateEquity(decimal? residual, CostSection depreciation, PurchaseFinancingResult financing, string path)
    {
        var equity = new CostSection($"{path}.totals.endEquity");
        if (residual is null) equity.CopyProblems(depreciation);
        var balance = financing.Allocation?.PrincipalSek == 0m ? 0m : financing.Loan?.RemainingPrincipalSek;
        if (balance is null)
        {
            // Fees affect ownership cost, but not the remaining principal.
            equity.Missing.AddRange(financing.MissingComponents.Where(IsBalanceInput));
            equity.Errors.AddRange(financing.Errors.Where(error => IsBalanceInput(error.Path)));
        }

        if (residual is not null && balance is not null) equity.Add(residual.Value - balance.Value);
        return equity;

        static bool IsBalanceInput(string inputPath) => !inputPath.Contains("setupFeeSek", StringComparison.Ordinal)
            && !inputPath.Contains("monthlyFeeSek", StringComparison.Ordinal);
    }

    private static (CostSection Section, HouseholdCategoryResult Result) CalculateCategory(
        HouseholdCostCategoryInput? category, string path, string categoryName, HouseholdCostContext context, HouseholdPaymentLedger ledger)
    {
        var result = new CostSection(path);
        var items = new List<HouseholdCostItemResult>();
        if (category is null)
        {
            result.Missing.Add(path);
            ledger.AddMissingCategory(categoryName, path);
        }
        else if (category.Items.Count == 0) result.Add(0m);
        else
        {
            for (var index = 0; index < category.Items.Count; index++)
            {
                var item = category.Items[index];
                var itemPath = $"{path}.items[{index}]";
                var cost = CalculateItem(item, itemPath, context);
                ledger.AddCostItem(item, itemPath, categoryName);
                result.Merge(cost);
                items.Add(new(item.Key.Trim(), item.Label.Trim(), cost.Result()));
            }
        }

        return (result, new(category?.IsIncluded ?? false, result.Result(), items.AsReadOnly()));
    }

    private static CostSection CalculateItem(HouseholdCostItem item, string path, HouseholdCostContext context)
    {
        var result = new CostSection(path);
        var cadenceAvailable = context.Available($"{path}.cadence", item.Cadence is not null, result);
        var period = context.Period(result);
        var timingAvailable = true;
        if (item.Cadence == HouseholdCostCadence.Once)
        {
            timingAvailable = context.Available($"{path}.monthOffset", item.MonthOffset is not null, result);
            if (timingAvailable && period is not null && item.MonthOffset > period)
            {
                result.Add(0m);
                return result;
            }
        }

        var amount = context.Value(item.AmountSek, $"{path}.amountSek", result);
        if (cadenceAvailable && timingAvailable && amount is not null && period is not null)
            result.Add(item.Cadence switch
            {
                HouseholdCostCadence.Monthly => amount.Value * period.Value,
                HouseholdCostCadence.Annual => amount.Value * period.Value / 12m,
                HouseholdCostCadence.Once => amount.Value,
                _ => throw new InvalidOperationException("Unsupported cost cadence."),
            });
        return result;
    }
}

internal sealed record HouseholdComparisonCalculation(HouseholdCostPreview Preview, IReadOnlyList<HouseholdComparisonVehicle> Vehicles);
internal sealed record HouseholdComparisonVehicle(VehicleCostResult Result, decimal? NetCostSek, decimal? CostPerMonthSek, decimal? CostPerMilSek);
