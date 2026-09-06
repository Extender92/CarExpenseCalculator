using CarExpenseCalculator.Core.CostScenarios;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

internal static partial class HouseholdJson
{
    internal sealed record CalendarMonthPayload(int Year, int Month)
    {
        public static CalendarMonthPayload? FromCore(CalendarMonth? x) => x is { } month ? new(month.Year, month.Month) : null;
        public CalendarMonth ToCore() => new(Year, Month);
    }

    internal sealed record LoanPayload(SensitivityValue? AnnualNominalInterestRatePercent, int? TermMonths,
        decimal? SetupFeeSek, decimal? MonthlyFeeSek)
    {
        public static LoanPayload? FromCore(HouseholdLoanTerms? x) => x is null ? null
            : new(x.AnnualNominalInterestRatePercent, x.TermMonths, x.SetupFeeSek, x.MonthlyFeeSek);
        public HouseholdLoanTerms ToCore() => new()
        { AnnualNominalInterestRatePercent = AnnualNominalInterestRatePercent, TermMonths = TermMonths, SetupFeeSek = SetupFeeSek, MonthlyFeeSek = MonthlyFeeSek };
    }

    internal sealed record EnergyPricePayload(FuelType Fuel, EnergyUnit Unit, SensitivityValue? PricePerUnitSek)
    {
        public static EnergyPricePayload FromCore(HouseholdEnergyPrice x) => new(x.Fuel, x.Unit, x.PricePerUnitSek);
        public HouseholdEnergyPrice ToCore() => new(Fuel, Unit, PricePerUnitSek);
    }

    internal sealed record EnergySourcePayload(string Key, FuelType? Fuel, EnergyUnit? Unit,
        SensitivityValue? ConsumptionPer100Kilometres, ConsumptionBasis? ConsumptionBasis, ElectricityBasis? ElectricityBasis)
    {
        public static EnergySourcePayload FromCore(HouseholdEnergySource x) => new(x.Key, x.Fuel, x.Unit,
            x.ConsumptionPer100Kilometres, x.ConsumptionBasis, x.ElectricityBasis);
        public HouseholdEnergySource ToCore() => new(Key, Fuel, Unit, ConsumptionPer100Kilometres, ConsumptionBasis, ElectricityBasis);
    }

    internal sealed record CostItemPayload(string Key, string Label, SensitivityValue? AmountSek,
        HouseholdCostCadence? Cadence, int? MonthOffset, int? DueMonthOfYear, string? EvidenceNote, string? SourceUrl)
    {
        public static CostItemPayload FromCore(HouseholdCostItem x) => new(x.Key, x.Label, x.AmountSek, x.Cadence,
            x.MonthOffset, x.DueMonthOfYear, x.EvidenceNote, x.SourceUrl);
        public HouseholdCostItem ToCore() => new(Key, Label, AmountSek, Cadence, MonthOffset, DueMonthOfYear, EvidenceNote, SourceUrl);
    }

    internal sealed record LeasePaymentPayload(int MonthOffset, decimal? AmountSek)
    {
        public static LeasePaymentPayload FromCore(HouseholdLeasePayment x) => new(x.MonthOffset, x.AmountSek);
        public HouseholdLeasePayment ToCore() => new(MonthOffset, AmountSek);
    }

    internal sealed record LeaseChargePayload(string Key, string Label, SensitivityValue? AmountSek,
        int? MonthOffset, string? EvidenceNote, string? SourceUrl)
    {
        public static LeaseChargePayload FromCore(HouseholdLeaseCharge x) => new(x.Key, x.Label, x.AmountSek, x.MonthOffset, x.EvidenceNote, x.SourceUrl);
        public HouseholdLeaseCharge ToCore() => new(Key, Label, AmountSek, MonthOffset, EvidenceNote, SourceUrl);
    }
}
