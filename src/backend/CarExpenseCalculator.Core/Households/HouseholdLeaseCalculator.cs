namespace CarExpenseCalculator.Core.Households;

internal static class HouseholdLeaseCalculator
{
    public static CostSection Coverage(HouseholdLeaseInput? lease, string path, HouseholdCostContext context)
    {
        var coverage = new CostSection(path);
        var requested = context.RequestedPeriod(coverage);
        var validTerm = context.Available($"{path}.termMonths", lease?.TermMonths is not null, coverage);
        if (requested is not null && validTerm) coverage.Add(Math.Min(requested.Value, lease!.TermMonths!.Value));
        return coverage;
    }

    public static (CostSection Cost, CostSection Withheld, HouseholdLeaseResult Result) Calculate(
        HouseholdLeaseInput? lease, string path, HouseholdCostContext context, HouseholdPaymentLedger ledger)
    {
        var cost = CostSection.Zero(path);
        var withheld = CostSection.Zero($"{path}.depositWithheld");
        var covered = context.Period(cost);
        var requested = context.RequestedPeriod(cost);
        if (lease is null)
        {
            cost.Missing.Add(path);
            ledger.AddMissingCategory("lease", path);
            withheld.Missing.Add(path);
            return (cost, withheld, new(cost.Result(), null, null, false, null, withheld.Result()));
        }

        var estimate = lease.PriceBasis == LeasePriceBasis.Estimated;
        var pricing = new CostSection($"{path}.priceBasis");
        context.Available($"{path}.priceBasis", lease.PriceBasis is not null, pricing);
        if (lease.PriceBasis == LeasePriceBasis.Unresolved) pricing.Missing.Add($"{path}.priceBasis");
        if (!pricing.IsComplete)
        {
            cost.CopyProblems(pricing);
            ledger.Add($"{path}.priceBasis", "lease", "Unresolved lease pricing", pricing, null, mayAffectStartup: true);
        }

        var upfront = Scalar(lease.UpfrontNonRefundableSek, $"{path}.upfrontNonRefundableSek", context);
        cost.Merge(upfront);
        ledger.Add($"{path}.upfrontNonRefundableSek", "leaseUpfront", "Upfront lease fee", upfront, [0], estimate);
        var deposit = Scalar(lease.RefundableDepositSek, $"{path}.refundableDepositSek", context);
        ledger.Add($"{path}.refundableDepositSek", "deposit", "Refundable deposit", deposit, [0]);

        if (covered is not null)
        {
            for (var month = 1; month <= covered; month++)
            {
                var index = -1;
                for (var i = 0; i < (lease.MonthlyPayments?.Count ?? 0); i++)
                    if (lease.MonthlyPayments![i].MonthOffset == month) index = i;
                var paymentPath = index >= 0 ? $"{path}.monthlyPayments[{index}]" : $"{path}.monthlyPayments.month{month}";
                var amount = Scalar(index >= 0 ? lease.MonthlyPayments![index].AmountSek : null, $"{paymentPath}.amountSek", context);
                cost.Merge(amount);
                ledger.Add(paymentPath, "leasePayments", $"Lease month {month}", amount, [month], estimate);
            }
        }

        foreach (var (payment, index) in (lease.MonthlyPayments ?? []).Select((payment, index) => (payment, index)))
        {
            var paymentPath = $"{path}.monthlyPayments[{index}]";
            var timing = new CostSection(paymentPath);
            if (!context.Available($"{paymentPath}.monthOffset", true, timing))
            {
                var value = context.Value(payment.AmountSek, $"{paymentPath}.amountSek", timing);
                if (value is not null) timing.Add(value.Value);
                cost.CopyProblems(timing);
                ledger.Add(paymentPath, "leasePayments", "Invalid payment month", timing, null);
            }
        }
        if (covered is null)
        {
            var unknown = new CostSection($"{path}.monthlyPayments");
            context.Period(unknown);
            ledger.Add($"{path}.monthlyPayments", "leasePayments", "Lease payments", unknown, null);
        }

        decimal? excessKm = null;
        var excess = new CostSection($"{path}.excessDistance");
        var termValid = context.Available($"{path}.termMonths", lease.TermMonths is not null, excess);
        var annual = context.Value(context.Profile.AnnualDistanceKilometres, "profile.annualDistanceKilometres", excess);
        var included = context.Value(lease.IncludedDistanceKilometres, $"{path}.includedDistanceKilometres", excess);
        if (termValid && annual is not null && included is not null)
        {
            var distance = annual.Value * lease.TermMonths!.Value / 12m;
            if (annual > 0m && distance == 0m)
                excess.Errors.Add(new($"{path}.excessDistance", "calculationOutOfRange", "Positive contract distance is below decimal precision."));
            else excessKm = Math.Max(distance - included.Value, 0m);
        }
        if (excessKm == 0m) excess.Add(0m);
        else
        {
            var rate = context.Value(lease.ExcessDistancePricePerKilometreSek, $"{path}.excessDistancePricePerKilometreSek", excess);
            if (excessKm is not null && rate is not null)
            {
                var charge = excessKm.Value * rate.Value;
                if (excessKm > 0m && rate > 0m && charge == 0m)
                    excess.Errors.Add(new($"{path}.excessDistance", "calculationOutOfRange", "Positive excess charge is below decimal precision."));
                else excess.Add(charge);
            }
        }

        var endWithin = covered is not null && termValid && covered == lease.TermMonths;
        var endOutside = covered is not null && termValid && covered < lease.TermMonths;
        var endMonths = endWithin ? new[] { covered!.Value } : endOutside ? [] : null;
        if (!endOutside)
        {
            var refund = deposit.Complete == 0m && lease.DepositRefundSek is null ? CostSection.Zero($"{path}.depositRefundSek")
                : Scalar(lease.DepositRefundSek, $"{path}.depositRefundSek", context);
            withheld.CopyProblems(deposit);
            withheld.CopyProblems(refund);
            if (endWithin && deposit.Complete is { } paid && refund.Complete is { } returned) withheld.Add(paid - returned);
            if (!endWithin)
            {
                context.Period(withheld);
                context.Available($"{path}.termMonths", lease.TermMonths is not null, withheld);
                context.Period(refund);
                context.Period(excess);
            }
            cost.Merge(withheld);
            if (endWithin) cost.Merge(excess);
            else cost.CopyProblems(excess);
            ledger.Add($"{path}.depositRefundSek", "depositRefund", "Deposit refund", refund, endMonths,
                lease.DepositRefundSek?.Single is null, HouseholdPaymentDirection.Inflow);
            ledger.Add($"{path}.excessDistance", "leaseExcess", "Excess distance", excess, endMonths, true);
        }
        AddCharges(lease.EndFees, "endFees", true);
        AddCharges(lease.OtherPayments, "otherPayments", false);

        if (requested is not null && termValid && requested != lease.TermMonths)
            cost.Errors.Add(new($"{path}.termMonths", "leaseHorizonMismatch", "Comparable lease cost requires the contract term as the comparison period."));
        return (cost, withheld, new(cost.Result(), termValid ? lease.TermMonths : null, covered, estimate,
            CostSection.Quantity(excessKm), withheld.Result()));

        void AddCharges(IReadOnlyList<HouseholdLeaseCharge>? charges, string name, bool atEnd)
        {
            if (atEnd && endOutside) return;
            var categoryPath = $"{path}.{name}";
            if (charges is null)
            {
                var unknown = new CostSection(categoryPath);
                unknown.Missing.Add(categoryPath);
                cost.CopyProblems(unknown);
                ledger.Add(categoryPath, "leaseExtras", name, unknown, atEnd ? endMonths : null,
                    mayAffectStartup: !atEnd);
                return;
            }
            for (var index = 0; index < charges.Count; index++)
            {
                var charge = charges[index];
                var chargePath = $"{categoryPath}.items[{index}]";
                var amount = new CostSection(chargePath);
                var months = atEnd ? endMonths : ledger.ItemMonths(HouseholdCostCadence.Once, charge.MonthOffset, null, chargePath, amount);
                if (months is not { Count: 0 })
                {
                    var value = context.Value(charge.AmountSek, $"{chargePath}.amountSek", amount);
                    if (value is not null) amount.Add(value.Value);
                    if (months is not null) cost.Merge(amount);
                    else
                    {
                        if (atEnd) context.Period(amount);
                        cost.CopyProblems(amount);
                    }
                }
                ledger.Add(chargePath, "leaseExtras", charge.Label.Trim(), amount, months, true,
                    mayAffectStartup: !atEnd, mayAffectOngoing: atEnd || charge.MonthOffset != 0);
            }
        }
    }

    internal static CostSection Scalar(decimal? input, string path, HouseholdCostContext context)
    {
        var result = new CostSection(path);
        var value = context.Value(input, path, result);
        if (value is not null) result.Add(value.Value);
        return result;
    }

    internal static CostSection Scalar(SensitivityValue? input, string path, HouseholdCostContext context)
    {
        var result = new CostSection(path);
        var value = context.Value(input, path, result);
        if (value is not null) result.Add(value.Value);
        return result;
    }
}
