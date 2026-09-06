namespace CarExpenseCalculator.Core.Households;

internal static class HouseholdPurchasePayments
{
    public static void Add(PurchaseFinancingResult financing, HouseholdCostContext context, HouseholdPaymentLedger ledger, string path)
    {
        var cash = new CostSection($"{path}.purchaseCash");
        if (financing.Allocation is { } allocation) cash.Add(allocation.CashAppliedSek);
        else CopyFinancingProblems(cash, input => input.EndsWith("priceSek", StringComparison.Ordinal) || input == "profile.purchaseCashSek");
        ledger.Add($"{path}.purchaseCash", "purchaseCash", "Purchase cash", cash, [0]);
        if (financing.Allocation?.PrincipalSek == 0m) return;

        var setup = HouseholdLeaseCalculator.Scalar(context.Profile.LoanTerms?.SetupFeeSek, "profile.loanTerms.setupFeeSek", context);
        var fee = HouseholdLeaseCalculator.Scalar(context.Profile.LoanTerms?.MonthlyFeeSek, "profile.loanTerms.monthlyFeeSek", context);
        var period = context.Period(fee);
        var termValid = context.Available("profile.loanTerms.termMonths", context.Profile.LoanTerms?.TermMonths is not null, fee);
        if (financing.Allocation is null)
        {
            setup.CopyProblems(cash);
            fee.CopyProblems(cash);
            // Without allocation, supplied loan fees are not known to apply.
            var unknownSetup = new CostSection($"{path}.setupFee");
            unknownSetup.CopyProblems(setup);
            setup = unknownSetup;
            var unknownFee = new CostSection($"{path}.monthlyFee");
            unknownFee.CopyProblems(fee);
            fee = unknownFee;
        }
        ledger.Add($"{path}.setupFee", "loanFees", "Loan setup fee", setup, [0]);
        ledger.Add($"{path}.monthlyFee", "loanFees", "Monthly loan fee", fee,
            period is not null && termValid ? Enumerable.Range(1, Math.Min(period.Value, context.Profile.LoanTerms!.TermMonths!.Value)).ToArray() : null);
        if (financing.Loan is { } loan)
        {
            foreach (var installment in loan.Installments)
            {
                var principal = CostSection.Zero($"{path}.principal");
                principal.Add(installment.PrincipalRepaidSek);
                var interest = CostSection.Zero($"{path}.interest");
                interest.Add(installment.InterestSek);
                ledger.Add($"{path}.principal.month{installment.MonthOffset}", "principal", "Loan principal", principal, [installment.MonthOffset]);
                ledger.Add($"{path}.interest.month{installment.MonthOffset}", "interest", "Loan interest", interest, [installment.MonthOffset]);
            }
        }
        else
        {
            var unknown = new CostSection($"{path}.loanPayments");
            CopyFinancingProblems(unknown, input => !input.Contains("FeeSek", StringComparison.Ordinal));
            ledger.Add($"{path}.loanPayments", "principal", "Loan installments", unknown,
                period is not null && termValid ? Enumerable.Range(1, Math.Min(period.Value, context.Profile.LoanTerms!.TermMonths!.Value)).ToArray() : null);
        }

        void CopyFinancingProblems(CostSection section, Func<string, bool> relevant)
        {
            section.Missing.AddRange(financing.MissingComponents.Where(relevant));
            section.Errors.AddRange(financing.Errors.Where(error => relevant(error.Path)));
        }
    }
}
