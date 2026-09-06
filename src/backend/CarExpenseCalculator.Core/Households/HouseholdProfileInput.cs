using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Households;

public readonly record struct CalendarMonth(int Year, int Month);

public sealed record HouseholdProfileInput
{
    public HouseholdProfileInput(IEnumerable<HouseholdEnergyPrice>? energyPrices = null)
    {
        EnergyPrices = Array.AsReadOnly(energyPrices?.ToArray() ?? []);
    }

    public CalendarMonth? StartMonth { get; init; }
    public int? PeriodMonths { get; init; }
    public decimal? AnnualDistanceKilometres { get; init; }
    public decimal? PurchaseCashSek { get; init; }
    public HouseholdLoanTerms? LoanTerms { get; init; }
    public IReadOnlyList<HouseholdEnergyPrice> EnergyPrices { get; }
    public SensitivityValue? ElectricDrivingSharePercent { get; init; }
    public SensitivityValue? HomeChargingSharePercent { get; init; }
    public SensitivityValue? HomeChargingPricePerKilowattHourSek { get; init; }
    public SensitivityValue? PublicChargingPricePerKilowattHourSek { get; init; }
    public SensitivityValue? ChargingLossPercent { get; init; }
    public decimal? StartupBudgetSek { get; init; }
    public decimal? MonthlyBudgetSek { get; init; }
    public SensitivityMode ActiveSensitivityMode { get; init; } = SensitivityMode.Baseline;
}

public sealed record HouseholdLoanTerms
{
    public SensitivityValue? AnnualNominalInterestRatePercent { get; init; }
    public int? TermMonths { get; init; }
    public decimal? SetupFeeSek { get; init; }
    public decimal? MonthlyFeeSek { get; init; }
}

public sealed record HouseholdEnergyPrice(FuelType Fuel, EnergyUnit Unit, SensitivityValue? PricePerUnitSek);

// Current purchase facts only. Household assumptions cannot be overridden here.
// Operating and lease input models are added by their respective work items.
public sealed record VehiclePurchaseInput(string CandidateKey, decimal? PriceSek);
