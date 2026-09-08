using CarExpenseCalculator.Core.Households;

namespace CarExpenseCalculator.Core.Comparisons;

internal static class ComparisonReviewCompleteness
{
    internal static HouseholdComparisonVehicle Apply(HouseholdComparisonVehicle calculated, IReadOnlyList<ComparisonReviewItem> reviews)
    {
        if (reviews.Count == 0) return calculated;
        bool Affects(ComparisonReviewItem item, ComparisonReviewSection section) => item.AffectedSections.Contains(section) ||
            section == ComparisonReviewSection.Ownership && item.AffectedSections.Any(x => x is ComparisonReviewSection.Energy
                or ComparisonReviewSection.Tax or ComparisonReviewSection.Insurance or ComparisonReviewSection.Service
                or ComparisonReviewSection.Repairs or ComparisonReviewSection.CustomCosts);
        string[] Missing(ComparisonReviewSection section) => reviews.Where(x => Affects(x, section)).Select(x => $"legacyReview:{x.Key}").ToArray();
        CostSectionResult Block(CostSectionResult part, ComparisonReviewSection section)
        {
            var missing = Missing(section);
            return missing.Length == 0 ? part : part with
            {
                State = part.State is CostSectionState.Invalid or CostSectionState.Unavailable ? part.State : CostSectionState.Partial,
                CompleteTotalSek = null,
                MissingComponents = Array.AsReadOnly(part.MissingComponents.Concat(missing).Distinct().ToArray()),
            };
        }
        HouseholdBudgetResult Budget(HouseholdBudgetResult part, ComparisonReviewSection section) => Missing(section).Length == 0 ? part : part with
        {
            FundingRequired = Block(part.FundingRequired, section),
            Status = part.Status == HouseholdBudgetStatus.WithinLimit ? HouseholdBudgetStatus.Unknown : part.Status,
        };
        var r = calculated.Result;
        var result = r with
        {
            Energy = r.Energy with { Cost = Block(r.Energy.Cost, ComparisonReviewSection.Energy) },
            Tax = r.Tax with { Cost = Block(r.Tax.Cost, ComparisonReviewSection.Tax) },
            Insurance = r.Insurance with { Cost = Block(r.Insurance.Cost, ComparisonReviewSection.Insurance) },
            Service = r.Service with { Cost = Block(r.Service.Cost, ComparisonReviewSection.Service) },
            Repairs = r.Repairs with { Cost = Block(r.Repairs.Cost, ComparisonReviewSection.Repairs) },
            CustomCosts = r.CustomCosts with { Cost = Block(r.CustomCosts.Cost, ComparisonReviewSection.CustomCosts) },
            Totals = r.Totals with
            {
                OwnershipCost = Block(r.Totals.OwnershipCost, ComparisonReviewSection.Ownership),
                MonthlyCost = Block(r.Totals.MonthlyCost, ComparisonReviewSection.Ownership),
                CostPerMil = Block(r.Totals.CostPerMil, ComparisonReviewSection.Ownership),
            },
            Payments = r.Payments with
            {
                CalendarStatus = Block(r.Payments.CalendarStatus, ComparisonReviewSection.Payments),
                ExternalOutflow = Block(r.Payments.ExternalOutflow, ComparisonReviewSection.Payments),
                NetExternalCashFlow = Block(r.Payments.NetExternalCashFlow, ComparisonReviewSection.Payments),
            },
            StartupBudget = Budget(r.StartupBudget, ComparisonReviewSection.StartupBudget),
            MonthlyBudget = Budget(r.MonthlyBudget, ComparisonReviewSection.MonthlyBudget),
            Reconciliation = r.Reconciliation with
            {
                AccruedOperatingCosts = Block(r.Reconciliation.AccruedOperatingCosts, ComparisonReviewSection.Ownership),
                PaidOperatingCosts = Block(r.Reconciliation.PaidOperatingCosts, ComparisonReviewSection.Payments),
                ReconciledOwnershipCost = Block(r.Reconciliation.ReconciledOwnershipCost, ComparisonReviewSection.Ownership),
            },
        };
        return new(result, Missing(ComparisonReviewSection.Ownership).Length == 0 ? calculated.NetCostSek : null,
            Missing(ComparisonReviewSection.Ownership).Length == 0 ? calculated.CostPerMonthSek : null,
            Missing(ComparisonReviewSection.Ownership).Length == 0 ? calculated.CostPerMilSek : null);
    }
}
