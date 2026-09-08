using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.Comparisons;

[JsonConverter(typeof(StrictStringEnumConverter<ComparisonPreviewMode>))]
public enum ComparisonPreviewMode { Stored, Manual }

[JsonConverter(typeof(StrictStringEnumConverter<FactEditKind>))]
public enum FactEditKind { Preserve, Unknown, NotApplicable, Manual, Listing, Conflict, Resolve }

[JsonConverter(typeof(StrictStringEnumConverter<FactSelectionKind>))]
public enum FactSelectionKind { Current, Listing, Manual }

[JsonConverter(typeof(StrictStringEnumConverter<CostConfirmationAction>))]
public enum CostConfirmationAction { Preserve, Confirm, Clear }

[JsonConverter(typeof(StrictStringEnumConverter<VehicleFactState>))]
public enum VehicleFactState { Known, Unknown, NotApplicable, Conflicting }

[JsonConverter(typeof(StrictStringEnumConverter<ServiceDocumentationStatus>))]
public enum ServiceDocumentationStatus { Documented, Partial, Absent }

[JsonConverter(typeof(StrictStringEnumConverter<EvidenceRequirement>))]
public enum EvidenceRequirement { Advertised, UserConfirmed, RegistryVerified }

[JsonConverter(typeof(StrictStringEnumConverter<HardRuleOperator>))]
public enum HardRuleOperator { InclusiveRange, Equals, AllowedSet, Intersects, MinimumRemainingDays, WithinBudget }

[JsonConverter(typeof(StrictStringEnumConverter<ComparisonSignalKey>))]
public enum ComparisonSignalKey { ConditionNotes, ServiceDocumentation, InspectionValidity, CostCompleteness, StartupBudget, MonthlyBudget }

[JsonConverter(typeof(StrictStringEnumConverter<HardRuleState>))]
public enum HardRuleState { Pass, Fail, NeedsVerification }

[JsonConverter(typeof(StrictStringEnumConverter<BuyingEligibility>))]
public enum BuyingEligibility { Eligible, NeedsVerification, Rejected }

[JsonConverter(typeof(StrictStringEnumConverter<ComparisonSignalKind>))]
public enum ComparisonSignalKind { Information, Warning, Positive }
