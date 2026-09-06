namespace CarExpenseCalculator.Core.Households;

public sealed class HouseholdFinancingCalculator
{
    public HouseholdFinancingPreview Calculate(HouseholdProfileInput profile, IReadOnlyList<VehiclePurchaseInput> vehicles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(vehicles);
        var profileErrors = HouseholdInputValidator.ValidateProfile(profile);
        var structureErrors = profileErrors.Where(error => error.Code != "outOfRange").ToList();
        if (vehicles.Count > HouseholdInputValidator.MaximumCandidates)
        {
            structureErrors.Add(new("vehicles", "tooManyItems", "At most 100 candidates are allowed in one calculation."));
        }

        var snapshot = vehicles.Take(HouseholdInputValidator.MaximumCandidates).ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.Length; index++)
        {
            var vehicle = snapshot[index];
            if (vehicle is null)
            {
                structureErrors.Add(new($"vehicles[{index}]", "missingItem", "Candidate cannot be null."));
                continue;
            }

            structureErrors.AddRange(HouseholdInputValidator.ValidatePurchase(vehicle, $"vehicles[{index}]")
                .Where(error => error.Code == "invalidKey"));
            if (!keys.Add(vehicle.CandidateKey?.Trim() ?? string.Empty))
            {
                structureErrors.Add(new($"vehicles[{index}].candidateKey", "duplicateKey", "Candidate keys must be unique."));
            }
        }

        if (structureErrors.Count > 0)
        {
            throw new HouseholdInputValidationException(structureErrors);
        }

        var results = snapshot.Select((vehicle, index) => CalculatePurchase(profile, vehicle, index, profileErrors)).ToArray();
        return new("SEK", profile.ActiveSensitivityMode, profileErrors, Array.AsReadOnly(results));
    }

    private static PurchaseFinancingResult CalculatePurchase(
        HouseholdProfileInput profile, VehiclePurchaseInput vehicle, int index,
        IReadOnlyList<HouseholdInputError> profileErrors)
    {
        var errors = HouseholdInputValidator.ValidatePurchase(vehicle, $"vehicles[{index}]").ToList();
        var missing = new List<string>();
        var pricePath = $"vehicles[{index}].priceSek";
        var cashPath = "profile.purchaseCashSek";
        errors.AddRange(profileErrors.Where(error => error.Path == cashPath));
        if (vehicle.PriceSek is null)
        {
            missing.Add(pricePath);
        }

        if (profile.PurchaseCashSek is null)
        {
            missing.Add(cashPath);
        }

        if (errors.Count > 0 || missing.Count > 0)
        {
            return Result(null, null, null, null, null, null);
        }

        var price = vehicle.PriceSek!.Value;
        var cash = profile.PurchaseCashSek!.Value;
        var allocation = new PurchaseCashAllocation(Math.Min(price, cash), Math.Max(price - cash, 0m), Math.Max(cash - price, 0m));
        if (allocation.PrincipalSek == 0m)
        {
            // A cash purchase needs neither a horizon nor any borrowing assumptions.
            return Result(allocation, null, 0m, 0m, 0m, allocation.CashAppliedSek);
        }

        var terms = profile.LoanTerms;
        const string ratePath = "profile.loanTerms.annualNominalInterestRatePercent";
        const string termPath = "profile.loanTerms.termMonths";
        const string periodPath = "profile.periodMonths";
        const string setupPath = "profile.loanTerms.setupFeeSek";
        const string monthlyFeePath = "profile.loanTerms.monthlyFeeSek";
        errors.AddRange(profileErrors.Where(error => error.Path == periodPath || error.Path.StartsWith("profile.loanTerms.", StringComparison.Ordinal)));
        if (profile.PeriodMonths is null)
        {
            missing.Add(periodPath);
        }

        if (terms?.TermMonths is null)
        {
            missing.Add(termPath);
        }

        if (terms?.AnnualNominalInterestRatePercent is null)
        {
            missing.Add(ratePath);
        }

        if (terms?.SetupFeeSek is null)
        {
            missing.Add(setupPath);
        }

        if (terms?.MonthlyFeeSek is null)
        {
            missing.Add(monthlyFeePath);
        }

        HouseholdLoanCalculation? loan = null;
        if (Available(periodPath) && Available(termPath) && Available(ratePath))
        {
            loan = CalculateLoan(allocation.PrincipalSek, terms!.AnnualNominalInterestRatePercent!.GetValue(profile.ActiveSensitivityMode),
                terms.TermMonths!.Value, profile.PeriodMonths!.Value);
        }

        var setup = Available(setupPath) ? terms!.SetupFeeSek : null;
        decimal? monthlyFees = Available(monthlyFeePath) && Available(periodPath) && Available(termPath)
            ? terms!.MonthlyFeeSek!.Value * Math.Min(profile.PeriodMonths!.Value, terms.TermMonths!.Value)
            : null;
        var financingCost = loan?.InterestPaidSek + setup + monthlyFees;
        var cashOutflow = allocation.CashAppliedSek + loan?.PaymentsDuringPeriodSek + setup + monthlyFees;
        return Result(allocation, loan, setup, monthlyFees, financingCost, cashOutflow);

        bool Available(string path) => !missing.Contains(path) && !errors.Any(error =>
            error.Path == path || error.Path.StartsWith(path + ".", StringComparison.Ordinal));

        PurchaseFinancingResult Result(PurchaseCashAllocation? applied, HouseholdLoanCalculation? calculatedLoan,
            decimal? setupFee, decimal? fees, decimal? cost, decimal? outflow)
        {
            var state = errors.Count > 0 ? FinancingState.Invalid
                : missing.Count == 0 ? FinancingState.Complete
                : applied is null ? FinancingState.Unavailable : FinancingState.Partial;
            return new(vehicle.CandidateKey.Trim(), state, applied, calculatedLoan, setupFee, fees, cost, outflow,
                missing.AsReadOnly(), errors.AsReadOnly());
        }
    }

    private static HouseholdLoanCalculation CalculateLoan(decimal principal, decimal annualRate, int term, int period)
    {
        var rate = annualRate / 1200m;
        // Equivalent to the annuity formula, without subtracting almost equal
        // powers at very small positive rates. Only bounded decimal operations.
        var discount = 1m;
        var discountedPayments = 0m;
        for (var month = 1; month <= term; month++)
        {
            discount /= 1m + rate;
            discountedPayments += discount;
        }

        var installment = principal / discountedPayments;
        var balance = principal;
        var rows = new List<LoanInstallment>();
        for (var month = 1; month <= Math.Min(period, term); month++)
        {
            var interest = balance * rate;
            var repaid = month == term ? balance : Math.Min(balance, installment - interest);
            var remaining = balance - repaid;
            rows.Add(new(month, balance, interest, repaid, repaid + interest, remaining));
            balance = remaining;
        }

        return new(term, annualRate, installment, rows.Sum(row => row.PaymentSek), principal - balance,
            rows.Sum(row => row.InterestSek), balance, rows.AsReadOnly());
    }
}
