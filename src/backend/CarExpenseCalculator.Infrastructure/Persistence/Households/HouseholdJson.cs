using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedCostScenarios;

namespace CarExpenseCalculator.Infrastructure.Persistence.Households;

// Persistence-owned payloads, independent of HTTP DTOs and derived result versions.
internal static partial class HouseholdJson
{
    public const int SchemaVersion = 1;
    private const int MaximumPayloadBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false), new SensitivityConverter() },
    };

    public static string Serialize<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumPayloadBytes)
            throw new HouseholdStoreException("payloadTooLarge", "Stored input exceeds 2 MiB.");
        return json;
    }

    public static T Deserialize<T>(string json, int version = SchemaVersion)
    {
        if (version != SchemaVersion)
            throw new HouseholdStoreException("unsupportedHouseholdInputVersion", "Stored input version is unsupported.");
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Stored input is missing.");
    }

    internal sealed record ProfilePayload(CalendarMonthPayload? StartMonth, int? PeriodMonths, decimal? AnnualDistanceKilometres,
        decimal? PurchaseCashSek, LoanPayload? LoanTerms, EnergyPricePayload[] EnergyPrices,
        SensitivityValue? ElectricDrivingSharePercent, SensitivityValue? HomeChargingSharePercent,
        SensitivityValue? HomeChargingPricePerKilowattHourSek, SensitivityValue? PublicChargingPricePerKilowattHourSek,
        SensitivityValue? ChargingLossPercent, decimal? StartupBudgetSek, decimal? MonthlyBudgetSek, SensitivityMode ActiveSensitivityMode)
    {
        public static ProfilePayload FromCore(HouseholdProfileInput x) => new(CalendarMonthPayload.FromCore(x.StartMonth), x.PeriodMonths,
            x.AnnualDistanceKilometres, x.PurchaseCashSek, LoanPayload.FromCore(x.LoanTerms), x.EnergyPrices.Select(EnergyPricePayload.FromCore).ToArray(), x.ElectricDrivingSharePercent,
            x.HomeChargingSharePercent, x.HomeChargingPricePerKilowattHourSek, x.PublicChargingPricePerKilowattHourSek,
            x.ChargingLossPercent, x.StartupBudgetSek, x.MonthlyBudgetSek, x.ActiveSensitivityMode);
        public HouseholdProfileInput ToCore() => new(EnergyPrices.Select(x => x.ToCore()))
        {
            StartMonth = StartMonth?.ToCore(), PeriodMonths = PeriodMonths, AnnualDistanceKilometres = AnnualDistanceKilometres,
            PurchaseCashSek = PurchaseCashSek, LoanTerms = LoanTerms?.ToCore(), ElectricDrivingSharePercent = ElectricDrivingSharePercent,
            HomeChargingSharePercent = HomeChargingSharePercent, HomeChargingPricePerKilowattHourSek = HomeChargingPricePerKilowattHourSek,
            PublicChargingPricePerKilowattHourSek = PublicChargingPricePerKilowattHourSek, ChargingLossPercent = ChargingLossPercent,
            StartupBudgetSek = StartupBudgetSek, MonthlyBudgetSek = MonthlyBudgetSek, ActiveSensitivityMode = ActiveSensitivityMode,
        };
    }

    internal sealed record CategoryPayload(bool Included, CostItemPayload[] Items)
    {
        public static CategoryPayload? FromCore(HouseholdCostCategoryInput? x) => x is null ? null : new(x.IsIncluded, x.Items.Select(CostItemPayload.FromCore).ToArray());
        public HouseholdCostCategoryInput ToCore() => Included ? HouseholdCostCategoryInput.Included(Items.Select(x => x.ToCore())) : HouseholdCostCategoryInput.FromItems(Items.Select(x => x.ToCore()));
    }

    internal sealed record ResidualPayload(ResidualMode Mode, SensitivityValue? Value, int? PeriodMonths)
    {
        public static ResidualPayload? FromCore(HouseholdResidualInput? x) => x is null ? null : new(x.Mode, x.Value, x.PeriodMonths);
        public HouseholdResidualInput ToCore() => Mode switch
        {
            ResidualMode.FixedAmount => HouseholdResidualInput.FixedAmount(Value, PeriodMonths),
            ResidualMode.AnnualPercentage => HouseholdResidualInput.AnnualPercentage(Value),
            _ => throw new JsonException("Unsupported residual mode."),
        };
    }

    internal sealed record LeasePayload(int? TermMonths, decimal? UpfrontNonRefundableSek, decimal? RefundableDepositSek,
        SensitivityValue? DepositRefundSek, decimal? IncludedDistanceKilometres, SensitivityValue? ExcessDistancePricePerKilometreSek,
        LeasePriceBasis? PriceBasis, bool EnergyIncluded, LeasePaymentPayload[]? MonthlyPayments,
        LeaseChargePayload[]? EndFees, LeaseChargePayload[]? OtherPayments)
    {
        public static LeasePayload? FromCore(HouseholdLeaseInput? x) => x is null ? null : new(x.TermMonths,
            x.UpfrontNonRefundableSek, x.RefundableDepositSek, x.DepositRefundSek, x.IncludedDistanceKilometres,
            x.ExcessDistancePricePerKilometreSek, x.PriceBasis, x.EnergyIncluded, x.MonthlyPayments?.Select(LeasePaymentPayload.FromCore).ToArray(),
            x.EndFees?.Select(LeaseChargePayload.FromCore).ToArray(), x.OtherPayments?.Select(LeaseChargePayload.FromCore).ToArray());
        public HouseholdLeaseInput ToCore() => new(MonthlyPayments?.Select(x => x.ToCore()), EndFees?.Select(x => x.ToCore()), OtherPayments?.Select(x => x.ToCore()))
        {
            TermMonths = TermMonths, UpfrontNonRefundableSek = UpfrontNonRefundableSek, RefundableDepositSek = RefundableDepositSek,
            DepositRefundSek = DepositRefundSek, IncludedDistanceKilometres = IncludedDistanceKilometres,
            ExcessDistancePricePerKilometreSek = ExcessDistancePricePerKilometreSek, PriceBasis = PriceBasis, EnergyIncluded = EnergyIncluded,
        };
    }

    internal sealed record CostPayload(string CandidateKey, AcquisitionType AcquisitionType, decimal? PriceSek,
        ResidualPayload? Residual, LeasePayload? Lease, EnergySourcePayload[]? EnergySources,
        CategoryPayload? Tax, CategoryPayload? Insurance, CategoryPayload? Service, CategoryPayload? Repairs,
        SensitivityValue? AdditionalRepairAllowancePerMonthSek, CategoryPayload? CustomCosts)
    {
        public static CostPayload FromCore(VehicleCostInput x) => new(x.CandidateKey.Trim(), x.AcquisitionType, x.PriceSek,
            ResidualPayload.FromCore(x.Residual), LeasePayload.FromCore(x.Lease), x.EnergySources?.Select(EnergySourcePayload.FromCore).ToArray(),
            CategoryPayload.FromCore(x.Tax), CategoryPayload.FromCore(x.Insurance), CategoryPayload.FromCore(x.Service),
            CategoryPayload.FromCore(x.Repairs), x.AdditionalRepairAllowancePerMonthSek, CategoryPayload.FromCore(x.CustomCosts));
        public VehicleCostInput ToCore() => new(CandidateKey, PriceSek, EnergySources?.Select(x => x.ToCore()))
        {
            AcquisitionType = AcquisitionType, Residual = Residual?.ToCore(), Lease = Lease?.ToCore(),
            Tax = Tax?.ToCore(), Insurance = Insurance?.ToCore(), Service = Service?.ToCore(), Repairs = Repairs?.ToCore(),
            AdditionalRepairAllowancePerMonthSek = AdditionalRepairAllowancePerMonthSek, CustomCosts = CustomCosts?.ToCore(),
        };
    }

    internal sealed record StoredCostPayload(CostPayload Input, LegacyReviewPayload[] UnresolvedItems);
    internal sealed record LegacyReviewPayload(string Key, LegacyItemKind Kind, string Label, decimal? AmountSek,
        Core.CostScenarios.RecurringCostCadence? Cadence, Core.CostScenarios.EnergyUnit? EnergyUnit, decimal? ConsumptionPer100Kilometres)
    {
        public static LegacyReviewPayload FromItem(LegacyReviewItem x) => new(x.Key, x.Kind, x.Label, x.AmountSek, x.Cadence, x.EnergyUnit, x.ConsumptionPer100Kilometres);
        public LegacyReviewItem ToItem() => new(Key, Kind, Label, AmountSek, Cadence, EnergyUnit, ConsumptionPer100Kilometres);
    }

    internal sealed record CostWritePayload(CostPayload Input, string? VehicleLabel, SavedScenarioListingLinkMode ListingLinkMode,
        LegacyItemDecision[]? LegacyDecisions)
    {
        public static CostWritePayload FromInput(VehicleCostWrite x) => new(CostPayload.FromCore(x.Input), x.VehicleLabel?.Trim(), x.ListingLinkMode, x.LegacyDecisions?.ToArray());
        public VehicleCostWrite ToInput() => new(Input.ToCore(), VehicleLabel, ListingLinkMode,
            LegacyDecisions is null ? null : Array.AsReadOnly(LegacyDecisions));
    }

    private sealed record SensitivityPayload(decimal? Single, decimal? Favorable, decimal? Baseline, decimal? Cautious);
    private sealed class SensitivityConverter : JsonConverter<SensitivityValue>
    {
        public override SensitivityValue Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<SensitivityPayload>(ref reader, options) ?? throw new JsonException("Missing sensitivity value.");
            if (value.Single is { } single && value.Favorable is null && value.Baseline is null && value.Cautious is null)
                return SensitivityValue.Constant(single);
            if (value.Single is null && value.Favorable is { } favorable && value.Baseline is { } baseline && value.Cautious is { } cautious)
                return SensitivityValue.Scenarios(favorable, baseline, cautious);
            throw new JsonException("A sensitivity value requires a single value or all three modes.");
        }
        public override void Write(Utf8JsonWriter writer, SensitivityValue value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new SensitivityPayload(value.Single, value.Favorable, value.Baseline, value.Cautious), options);
    }
}
