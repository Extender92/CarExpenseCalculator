using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

public static class HouseholdCalculationVersions
{
    public const int Calculation = 1;
    public const int ResultSchema = 1;
}

public enum CostSectionState { Complete, Partial, Unavailable, Invalid, NotApplicable }

// A null known subtotal means arithmetic overflow, not zero or an unknown item.
public sealed record CostSectionResult(
    CostSectionState State,
    decimal? KnownSubtotalSek,
    decimal? CompleteTotalSek,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<HouseholdInputError> Errors);

public sealed record HouseholdCostPreview(
    string Currency,
    int CalculationVersion,
    int ResultSchemaVersion,
    SensitivityMode ActiveSensitivityMode,
    IReadOnlyList<HouseholdInputError> ProfileErrors,
    IReadOnlyList<VehicleCostResult> Vehicles);

public sealed record VehicleCostResult(
    string CandidateKey,
    PurchaseFinancingResult FinancingDetails,
    CostSectionResult Financing,
    HouseholdDepreciationResult Depreciation,
    HouseholdEnergyResult Energy,
    HouseholdCategoryResult Tax,
    HouseholdCategoryResult Insurance,
    HouseholdCategoryResult Service,
    HouseholdCategoryResult Repairs,
    CostSectionResult RepairAllowance,
    HouseholdCategoryResult CustomCosts,
    HouseholdCostTotals Totals,
    IReadOnlyList<HouseholdInputError> InputErrors);

public sealed record HouseholdDepreciationResult(CostSectionResult Cost, decimal? ResidualValueSek);
public sealed record HouseholdEnergyResult(CostSectionResult Cost, IReadOnlyList<HouseholdEnergySourceResult> Sources);
public sealed record HouseholdEnergySourceResult(
    string Key,
    FuelType? Fuel,
    EnergyUnit? Unit,
    decimal? BaseQuantity,
    decimal? PurchasedQuantity,
    decimal? EffectivePricePerUnitSek,
    CostSectionResult Cost);

public sealed record HouseholdCategoryResult(bool IsIncluded, CostSectionResult Cost, IReadOnlyList<HouseholdCostItemResult> Items);
public sealed record HouseholdCostItemResult(string Key, string Label, CostSectionResult Cost);
public sealed record HouseholdCostTotals(
    decimal? DistanceKilometres,
    CostSectionResult OwnershipCost,
    CostSectionResult MonthlyCost,
    CostSectionResult CostPerMil,
    CostSectionResult EndEquity);
