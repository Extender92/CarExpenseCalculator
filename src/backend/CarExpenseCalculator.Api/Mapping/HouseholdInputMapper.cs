using System.Diagnostics.CodeAnalysis;
using A = CarExpenseCalculator.Api.Contracts.Households;
using C = CarExpenseCalculator.Core.Households;
using L = CarExpenseCalculator.Core.Listings;
using M = CarExpenseCalculator.Core.CostScenarios;
using AL = CarExpenseCalculator.Api.Contracts.ListingAnalyses;
using AM = CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Mapping;

internal static partial class HouseholdInputMapper
{
    [return: NotNullIfNotNull(nameof(x))]
    public static C.CalendarMonth? ToCore(A.CalendarMonth? x, string path)
    {
        if (x is null) return null;
        return new C.CalendarMonth(x.Year, x.Month);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.CalendarMonth? ToApi(C.CalendarMonth? x) => x is null ? null : new()
    {
        Year = x.Value.Year,
        Month = x.Value.Month,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.SensitivityValue? ToCore(A.SensitivityValue? x, string path)
    {
        if (x is null) return null;
        if (x.Single is { } single && x.Favorable is null && x.Baseline is null && x.Cautious is null)
            return C.SensitivityValue.Constant(single);
        if (x.Single is null && x.Favorable is { } favorable && x.Baseline is { } baseline && x.Cautious is { } cautious)
            return C.SensitivityValue.Scenarios(favorable, baseline, cautious);
        throw Error(path, "invalidSensitivity", "Supply a single value or all three scenario values.");
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.SensitivityValue? ToApi(C.SensitivityValue? x) => x is null ? null : new()
    {
        Single = x.Single,
        Favorable = x.Favorable,
        Baseline = x.Baseline,
        Cautious = x.Cautious,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdLoanTerms? ToCore(A.HouseholdLoanTerms? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdLoanTerms()
        {
            AnnualNominalInterestRatePercent = ToCore(x.AnnualNominalInterestRatePercent, $"{path}.annualNominalInterestRatePercent"),
            TermMonths = x.TermMonths,
            SetupFeeSek = x.SetupFeeSek,
            MonthlyFeeSek = x.MonthlyFeeSek,
        };
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLoanTerms? ToApi(C.HouseholdLoanTerms? x) => x is null ? null : new()
    {
        AnnualNominalInterestRatePercent = ToApi(x.AnnualNominalInterestRatePercent),
        TermMonths = x.TermMonths,
        SetupFeeSek = x.SetupFeeSek,
        MonthlyFeeSek = x.MonthlyFeeSek,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdEnergyPrice? ToCore(A.HouseholdEnergyPrice? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdEnergyPrice((L.FuelType)x.Fuel, (M.EnergyUnit)x.Unit, ToCore(x.PricePerUnitSek, $"{path}.pricePerUnitSek"));
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdEnergyPrice? ToApi(C.HouseholdEnergyPrice? x) => x is null ? null : new()
    {
        Fuel = (AL.FuelType)x.Fuel,
        Unit = (AM.EnergyUnit)x.Unit,
        PricePerUnitSek = ToApi(x.PricePerUnitSek),
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdProfileInput? ToCore(A.HouseholdProfileInput? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdProfileInput(MapItems(x.EnergyPrices, $"{path}.energyPrices", ToCore)!)
        {
            StartMonth = ToCore(x.StartMonth, $"{path}.startMonth"),
            PeriodMonths = x.PeriodMonths,
            AnnualDistanceKilometres = x.AnnualDistanceKilometres,
            PurchaseCashSek = x.PurchaseCashSek,
            LoanTerms = ToCore(x.LoanTerms, $"{path}.loanTerms"),
            ElectricDrivingSharePercent = ToCore(x.ElectricDrivingSharePercent, $"{path}.electricDrivingSharePercent"),
            HomeChargingSharePercent = ToCore(x.HomeChargingSharePercent, $"{path}.homeChargingSharePercent"),
            HomeChargingPricePerKilowattHourSek = ToCore(x.HomeChargingPricePerKilowattHourSek, $"{path}.homeChargingPricePerKilowattHourSek"),
            PublicChargingPricePerKilowattHourSek = ToCore(x.PublicChargingPricePerKilowattHourSek, $"{path}.publicChargingPricePerKilowattHourSek"),
            ChargingLossPercent = ToCore(x.ChargingLossPercent, $"{path}.chargingLossPercent"),
            StartupBudgetSek = x.StartupBudgetSek,
            MonthlyBudgetSek = x.MonthlyBudgetSek,
            ActiveSensitivityMode = (C.SensitivityMode)x.ActiveSensitivityMode,
        };
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdProfileInput? ToApi(C.HouseholdProfileInput? x) => x is null ? null : new()
    {
        StartMonth = ToApi(x.StartMonth),
        PeriodMonths = x.PeriodMonths,
        AnnualDistanceKilometres = x.AnnualDistanceKilometres,
        PurchaseCashSek = x.PurchaseCashSek,
        LoanTerms = ToApi(x.LoanTerms),
        EnergyPrices = x.EnergyPrices.Select(item => ToApi(item)!).ToArray(),
        ElectricDrivingSharePercent = ToApi(x.ElectricDrivingSharePercent),
        HomeChargingSharePercent = ToApi(x.HomeChargingSharePercent),
        HomeChargingPricePerKilowattHourSek = ToApi(x.HomeChargingPricePerKilowattHourSek),
        PublicChargingPricePerKilowattHourSek = ToApi(x.PublicChargingPricePerKilowattHourSek),
        ChargingLossPercent = ToApi(x.ChargingLossPercent),
        StartupBudgetSek = x.StartupBudgetSek,
        MonthlyBudgetSek = x.MonthlyBudgetSek,
        ActiveSensitivityMode = (A.SensitivityMode)x.ActiveSensitivityMode,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdResidualInput? ToCore(A.HouseholdResidualInput? x, string path)
    {
        if (x is null) return null;
        if (x.Mode == A.ResidualMode.AnnualPercentage && x.PeriodMonths is not null)
            throw Error(path + ".periodMonths", "invalidResidual", "An annual percentage has no fixed horizon.");
        return x.Mode == A.ResidualMode.FixedAmount
            ? C.HouseholdResidualInput.FixedAmount(ToCore(x.Value, path + ".value"), x.PeriodMonths)
            : C.HouseholdResidualInput.AnnualPercentage(ToCore(x.Value, path + ".value"));
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdResidualInput? ToApi(C.HouseholdResidualInput? x) => x is null ? null : new()
    {
        Mode = (A.ResidualMode)x.Mode,
        Value = ToApi(x.Value),
        PeriodMonths = x.PeriodMonths,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdEnergySource? ToCore(A.HouseholdEnergySource? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdEnergySource(x.Key, (L.FuelType?)x.Fuel, (M.EnergyUnit?)x.Unit, ToCore(x.ConsumptionPer100Kilometres, $"{path}.consumptionPer100Kilometres"), (C.ConsumptionBasis?)x.ConsumptionBasis, (C.ElectricityBasis?)x.ElectricityBasis);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdEnergySource? ToApi(C.HouseholdEnergySource? x) => x is null ? null : new()
    {
        Key = x.Key,
        Fuel = (AL.FuelType?)x.Fuel,
        Unit = (AM.EnergyUnit?)x.Unit,
        ConsumptionPer100Kilometres = ToApi(x.ConsumptionPer100Kilometres),
        ConsumptionBasis = (A.ConsumptionBasis?)x.ConsumptionBasis,
        ElectricityBasis = (A.ElectricityBasis?)x.ElectricityBasis,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdCostItem? ToCore(A.HouseholdCostItem? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdCostItem(x.Key, x.Label, ToCore(x.AmountSek, $"{path}.amountSek"), (C.HouseholdCostCadence?)x.Cadence, x.MonthOffset, x.DueMonthOfYear, x.EvidenceNote, x.SourceUrl);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCostItem? ToApi(C.HouseholdCostItem? x) => x is null ? null : new()
    {
        Key = x.Key,
        Label = x.Label,
        AmountSek = ToApi(x.AmountSek),
        Cadence = (A.HouseholdCostCadence?)x.Cadence,
        MonthOffset = x.MonthOffset,
        DueMonthOfYear = x.DueMonthOfYear,
        EvidenceNote = x.EvidenceNote,
        SourceUrl = x.SourceUrl,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdCostCategoryInput? ToCore(A.HouseholdCostCategoryInput? x, string path)
    {
        if (x is null) return null;
        var items = MapItems(x.Items, path + ".items", ToCore)!;
        return x.IsIncluded ? C.HouseholdCostCategoryInput.Included(items) : C.HouseholdCostCategoryInput.FromItems(items);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdCostCategoryInput? ToApi(C.HouseholdCostCategoryInput? x) => x is null ? null : new()
    {
        IsIncluded = x.IsIncluded,
        Items = x.Items.Select(item => ToApi(item)!).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdLeasePayment? ToCore(A.HouseholdLeasePayment? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdLeasePayment(x.MonthOffset, x.AmountSek);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLeasePayment? ToApi(C.HouseholdLeasePayment? x) => x is null ? null : new()
    {
        MonthOffset = x.MonthOffset,
        AmountSek = x.AmountSek,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdLeaseCharge? ToCore(A.HouseholdLeaseCharge? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdLeaseCharge(x.Key, x.Label, ToCore(x.AmountSek, $"{path}.amountSek"), x.MonthOffset, x.EvidenceNote, x.SourceUrl);
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLeaseCharge? ToApi(C.HouseholdLeaseCharge? x) => x is null ? null : new()
    {
        Key = x.Key,
        Label = x.Label,
        AmountSek = ToApi(x.AmountSek),
        MonthOffset = x.MonthOffset,
        EvidenceNote = x.EvidenceNote,
        SourceUrl = x.SourceUrl,
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.HouseholdLeaseInput? ToCore(A.HouseholdLeaseInput? x, string path)
    {
        if (x is null) return null;
        return new C.HouseholdLeaseInput(MapItems(x.MonthlyPayments, $"{path}.monthlyPayments", ToCore), MapItems(x.EndFees, $"{path}.endFees", ToCore), MapItems(x.OtherPayments, $"{path}.otherPayments", ToCore))
        {
            TermMonths = x.TermMonths,
            UpfrontNonRefundableSek = x.UpfrontNonRefundableSek,
            RefundableDepositSek = x.RefundableDepositSek,
            DepositRefundSek = ToCore(x.DepositRefundSek, $"{path}.depositRefundSek"),
            IncludedDistanceKilometres = x.IncludedDistanceKilometres,
            ExcessDistancePricePerKilometreSek = ToCore(x.ExcessDistancePricePerKilometreSek, $"{path}.excessDistancePricePerKilometreSek"),
            PriceBasis = (C.LeasePriceBasis?)x.PriceBasis,
            EnergyIncluded = x.EnergyIncluded,
        };
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.HouseholdLeaseInput? ToApi(C.HouseholdLeaseInput? x) => x is null ? null : new()
    {
        TermMonths = x.TermMonths,
        UpfrontNonRefundableSek = x.UpfrontNonRefundableSek,
        RefundableDepositSek = x.RefundableDepositSek,
        DepositRefundSek = ToApi(x.DepositRefundSek),
        IncludedDistanceKilometres = x.IncludedDistanceKilometres,
        ExcessDistancePricePerKilometreSek = ToApi(x.ExcessDistancePricePerKilometreSek),
        PriceBasis = (A.LeasePriceBasis?)x.PriceBasis,
        EnergyIncluded = x.EnergyIncluded,
        MonthlyPayments = x.MonthlyPayments?.Select(item => ToApi(item)!).ToArray(),
        EndFees = x.EndFees?.Select(item => ToApi(item)!).ToArray(),
        OtherPayments = x.OtherPayments?.Select(item => ToApi(item)!).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(x))]
    public static C.VehicleCostInput? ToCore(A.VehicleCostInput? x, string path)
    {
        if (x is null) return null;
        return new C.VehicleCostInput(x.CandidateKey.Trim(), x.PriceSek, MapItems(x.EnergySources, $"{path}.energySources", ToCore))
        {
            AcquisitionType = (C.AcquisitionType)x.AcquisitionType,
            Residual = ToCore(x.Residual, $"{path}.residual"),
            Lease = ToCore(x.Lease, $"{path}.lease"),
            Tax = ToCore(x.Tax, $"{path}.tax"),
            Insurance = ToCore(x.Insurance, $"{path}.insurance"),
            Service = ToCore(x.Service, $"{path}.service"),
            Repairs = ToCore(x.Repairs, $"{path}.repairs"),
            AdditionalRepairAllowancePerMonthSek = ToCore(x.AdditionalRepairAllowancePerMonthSek, $"{path}.additionalRepairAllowancePerMonthSek"),
            CustomCosts = ToCore(x.CustomCosts, $"{path}.customCosts"),
        };
    }

    [return: NotNullIfNotNull(nameof(x))]
    public static A.VehicleCostInput? ToApi(C.VehicleCostInput? x) => x is null ? null : new()
    {
        CandidateKey = x.CandidateKey.Trim(),
        AcquisitionType = (A.AcquisitionType)x.AcquisitionType,
        PriceSek = x.PriceSek,
        Residual = ToApi(x.Residual),
        Lease = ToApi(x.Lease),
        EnergySources = x.EnergySources?.Select(item => ToApi(item)!).ToArray(),
        Tax = ToApi(x.Tax),
        Insurance = ToApi(x.Insurance),
        Service = ToApi(x.Service),
        Repairs = ToApi(x.Repairs),
        AdditionalRepairAllowancePerMonthSek = ToApi(x.AdditionalRepairAllowancePerMonthSek),
        CustomCosts = ToApi(x.CustomCosts),
    };

    private static IReadOnlyList<TOut>? MapItems<TIn, TOut>(IReadOnlyList<TIn>? items, string path,
        Func<TIn, string, TOut?> map) where TIn : class where TOut : class => items?.Select((item, i) =>
            item is null ? throw Error($"{path}[{i}]", "missingItem", "An item cannot be null.")
                : map(item, $"{path}[{i}]")!).ToArray();

    internal static C.HouseholdInputValidationException Error(string path, string code, string message) =>
        new([new(path, code, message)]);
}
