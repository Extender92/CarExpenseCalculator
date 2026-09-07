namespace CarExpenseCalculator.Core.Comparisons;

public enum ComparisonValueKind { Decimal, Integer, Boolean, Category, CategorySet, Text, Date, BudgetStatus }
public enum ComparisonValueSource { VehicleFact, HouseholdCost, HouseholdBudget }

public sealed record ComparisonCriterionDefinition(
    string Key, ComparisonValueKind Kind, string Unit, ComparisonValueSource Source,
    string Applicability, decimal? Minimum = null, decimal? Maximum = null, int? MaximumLength = null);

public static class VehicleFactLimits
{
    public const decimal MaximumPriceSek = 100_000_000m;
    public const decimal MaximumOdometerKilometres = 10_000_000m;
    public const int MaximumOwners = 10_000;
    public const int MinimumSeats = 1;
    public const int MaximumSeats = 100;
    public const int MinimumModelYear = 1886;
    public const int MaximumModelYear = 2100;
    public const int MaximumTowingCapacityKilograms = 100_000;
    public const int MaximumLocationLength = 100;
    public const int MaximumNotesLength = 1_000;
}

// Metadata only: no rule operators, scores, or user-populated calculated values.
// All source facts accept unknown/explicit not-applicable/conflicting states. None
// of those states is a measured value; later evaluation decides evidence sufficiency.
public static class ComparisonCriterionCatalog
{
    private const string Vehicle = "Purchase or lease; missing is unknown, not zero or not-applicable.";
    private const string Cost = "Complete comparable 3A estimate in the active mode; not registry-verifiable.";
    private const string Budget = "3A budget assessment; absent limits or unknown status cannot pass a required rule.";

    public static IReadOnlyList<ComparisonCriterionDefinition> All { get; } = Array.AsReadOnly<ComparisonCriterionDefinition>([
        new("purchasePriceSek", ComparisonValueKind.Decimal, "SEK", ComparisonValueSource.VehicleFact,
            "Purchase asking/input price; a lease without purchase price is not-applicable.", 0, VehicleFactLimits.MaximumPriceSek),
        new("odometerKilometres", ComparisonValueKind.Decimal, "km", ComparisonValueSource.VehicleFact, Vehicle, 0, VehicleFactLimits.MaximumOdometerKilometres),
        new("ownerCount", ComparisonValueKind.Integer, "count", ComparisonValueSource.VehicleFact, Vehicle, 0, VehicleFactLimits.MaximumOwners),
        new("towBar", ComparisonValueKind.Boolean, "boolean", ComparisonValueSource.VehicleFact, Vehicle),
        new("transmission", ComparisonValueKind.Category, "Transmission", ComparisonValueSource.VehicleFact, Vehicle),
        new("seats", ComparisonValueKind.Integer, "count", ComparisonValueSource.VehicleFact, Vehicle, VehicleFactLimits.MinimumSeats, VehicleFactLimits.MaximumSeats),
        new("modelYear", ComparisonValueKind.Integer, "year", ComparisonValueSource.VehicleFact, Vehicle, VehicleFactLimits.MinimumModelYear, VehicleFactLimits.MaximumModelYear),
        new("fuelTypes", ComparisonValueKind.CategorySet, "FuelType", ComparisonValueSource.VehicleFact, Vehicle),
        new("bodyType", ComparisonValueKind.Category, "BodyType", ComparisonValueSource.VehicleFact, Vehicle),
        new("drivetrain", ComparisonValueKind.Category, "Drivetrain", ComparisonValueSource.VehicleFact, Vehicle),
        new("locality", ComparisonValueKind.Text, "text", ComparisonValueSource.VehicleFact, Vehicle, MaximumLength: VehicleFactLimits.MaximumLocationLength),
        new("county", ComparisonValueKind.Text, "text", ComparisonValueSource.VehicleFact, Vehicle, MaximumLength: VehicleFactLimits.MaximumLocationLength),
        new("towingCapacityKilograms", ComparisonValueKind.Integer, "kg (braked trailer)", ComparisonValueSource.VehicleFact, Vehicle, 0, VehicleFactLimits.MaximumTowingCapacityKilograms),
        new("inspectionValidThrough", ComparisonValueKind.Date, "DateOnly (ISO date)", ComparisonValueSource.VehicleFact,
            "Explicit validity date, full DateOnly range; remaining days require an explicit asOfDate in the rule engine."),
        new("serviceDocumentation", ComparisonValueKind.Category, "ServiceDocumentationStatus", ComparisonValueSource.VehicleFact,
            "Documented, partial, or absent; supporting service facts do not infer this status."),
        new("netCostSek", ComparisonValueKind.Decimal, "SEK", ComparisonValueSource.HouseholdCost, Cost),
        new("costPerMonthSek", ComparisonValueKind.Decimal, "SEK/month", ComparisonValueSource.HouseholdCost, Cost),
        new("costPerMilSek", ComparisonValueKind.Decimal, "SEK/mil", ComparisonValueSource.HouseholdCost, Cost + " Unavailable at zero distance."),
        new("startupBudget", ComparisonValueKind.BudgetStatus, "budget status", ComparisonValueSource.HouseholdBudget, Budget),
        new("monthlyBudget", ComparisonValueKind.BudgetStatus, "budget status", ComparisonValueSource.HouseholdBudget, Budget),
    ]);
}
