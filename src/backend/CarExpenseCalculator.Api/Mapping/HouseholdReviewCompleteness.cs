using CarExpenseCalculator.Api.Contracts.Households;

namespace CarExpenseCalculator.Api.Mapping;

// Presentation of outstanding source facts, not a second cost calculator.
internal static class HouseholdReviewCompleteness
{
    public static VehicleCostResult Apply(VehicleCostResult result, IReadOnlyList<LegacyReviewResponse> reviews)
    {
        if (reviews.Count == 0) return result;
        string[] Missing(string section) => reviews.Where(x => x.AffectedSections.Contains(section))
            .Select(x => $"legacyReview:{x.Input.Key}").ToArray();
        CostSectionResult Block(CostSectionResult part, string section)
        {
            var missing = Missing(section);
            if (missing.Length == 0) return part;
            return part with
            {
                State = part.State is CostSectionState.Invalid or CostSectionState.Unavailable
                    ? part.State : CostSectionState.Partial,
                CompleteTotalSek = null,
                MissingComponents = part.MissingComponents.Concat(missing).Distinct().ToArray(),
            };
        }
        HouseholdBudgetResult Budget(HouseholdBudgetResult budget, string section) => Missing(section).Length == 0 ? budget
            : budget with
            {
                FundingRequired = Block(budget.FundingRequired, section),
                // Exceeded was decided from unrounded known outflows in Core.
                Status = budget.Status is HouseholdBudgetStatus.WithinLimit ? HouseholdBudgetStatus.Unknown : budget.Status,
            };
        var payments = result.Payments;
        return result with
        {
            Energy = result.Energy with { Cost = Block(result.Energy.Cost, "energy") },
            Tax = result.Tax with { Cost = Block(result.Tax.Cost, "tax") },
            Insurance = result.Insurance with { Cost = Block(result.Insurance.Cost, "insurance") },
            Service = result.Service with { Cost = Block(result.Service.Cost, "service") },
            Repairs = result.Repairs with { Cost = Block(result.Repairs.Cost, "repairs") },
            CustomCosts = result.CustomCosts with { Cost = Block(result.CustomCosts.Cost, "customCosts") },
            Totals = result.Totals with
            {
                OwnershipCost = Block(result.Totals.OwnershipCost, "ownership"),
                MonthlyCost = Block(result.Totals.MonthlyCost, "ownership"),
                CostPerMil = Block(result.Totals.CostPerMil, "ownership"),
            },
            Payments = payments with
            {
                CalendarStatus = Block(payments.CalendarStatus, "payments"),
                ExternalOutflow = Block(payments.ExternalOutflow, "payments"),
                NetExternalCashFlow = Block(payments.NetExternalCashFlow, "payments"),
                // Dates for review items are unknown; preserve known month entries.
                Sources = payments.Sources.Concat(reviews.Select(x => new HouseholdPaymentSourceResult(
                    $"legacyReview:{x.Input.Key}", "legacyReview", x.Input.Label, HouseholdPaymentDirection.Outflow,
                    false, [], new(CostSectionState.Unavailable, 0m, null, [$"legacyReview:{x.Input.Key}"], []),
                    new(CostSectionState.Unavailable, 0m, null, [$"legacyReview:{x.Input.Key}"], [])))).ToArray(),
            },
            StartupBudget = Budget(result.StartupBudget, "startupBudget"),
            MonthlyBudget = Budget(result.MonthlyBudget, "monthlyBudget"),
            Reconciliation = result.Reconciliation with
            {
                AccruedOperatingCosts = Block(result.Reconciliation.AccruedOperatingCosts, "ownership"),
                PaidOperatingCosts = Block(result.Reconciliation.PaidOperatingCosts, "payments"),
                ReconciledOwnershipCost = Block(result.Reconciliation.ReconciledOwnershipCost, "ownership"),
            },
        };
    }
}
