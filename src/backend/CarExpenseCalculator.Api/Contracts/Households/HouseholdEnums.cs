using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.Households;

[JsonConverter(typeof(StrictStringEnumConverter<SensitivityMode>))]
public enum SensitivityMode { Baseline, Favorable, Cautious }

[JsonConverter(typeof(StrictStringEnumConverter<AcquisitionType>))]
public enum AcquisitionType { Purchase, Lease }

[JsonConverter(typeof(StrictStringEnumConverter<ResidualMode>))]
public enum ResidualMode { FixedAmount, AnnualPercentage }

[JsonConverter(typeof(StrictStringEnumConverter<ConsumptionBasis>))]
public enum ConsumptionBasis { WholeDistance, DrivingMode }

[JsonConverter(typeof(StrictStringEnumConverter<ElectricityBasis>))]
public enum ElectricityBasis { Battery, Metered }

[JsonConverter(typeof(StrictStringEnumConverter<HouseholdCostCadence>))]
public enum HouseholdCostCadence { Monthly, Annual, Once }

[JsonConverter(typeof(StrictStringEnumConverter<LeasePriceBasis>))]
public enum LeasePriceBasis { Quoted, Estimated, Unresolved }

[JsonConverter(typeof(StrictStringEnumConverter<CostSectionState>))]
public enum CostSectionState { Complete, Partial, Unavailable, Invalid, NotApplicable }

[JsonConverter(typeof(StrictStringEnumConverter<FinancingState>))]
public enum FinancingState { Complete, Partial, Unavailable, Invalid }

[JsonConverter(typeof(StrictStringEnumConverter<HouseholdPaymentDirection>))]
public enum HouseholdPaymentDirection { Outflow, Inflow, InternalSaving }

[JsonConverter(typeof(StrictStringEnumConverter<HouseholdBudgetStatus>))]
public enum HouseholdBudgetStatus { NotConfigured, WithinLimit, Exceeded, Unknown, Invalid }

[JsonConverter(typeof(StrictStringEnumConverter<VehicleInputState>))]
public enum VehicleInputState { ListingOnly, LegacyPending, Current }

[JsonConverter(typeof(StrictStringEnumConverter<LegacyItemKind>))]
public enum LegacyItemKind { Tax, Insurance, Maintenance, Energy, Recurring, OneTime }

[JsonConverter(typeof(StrictStringEnumConverter<LegacyItemDisposition>))]
public enum LegacyItemDisposition { KeepForReview, Map, Discard }

[JsonConverter(typeof(StrictStringEnumConverter<LegacyRecurringCostCadence>))]
public enum LegacyRecurringCostCadence { Monthly, Annual }
