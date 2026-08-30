import type { components } from "@/api/schema";

export type EnergyUnit = components["schemas"]["EnergyUnit"];
export type RecurringCostCadence = components["schemas"]["RecurringCostCadence"];

export interface FinancingDraft {
  enabled: boolean;
  downPaymentSek: string;
  annualNominalInterestRatePercent: string;
  termMonths: string;
}

export interface EnergySourceDraft {
  id: string;
  label: string;
  unit: EnergyUnit | "";
  consumptionPer100Kilometres: string;
  pricePerUnitSek: string;
  distanceSharePercent: string;
}

export interface OptionalRecurringCostDraft {
  isKnown: boolean;
  amountSek: string;
  cadence: RecurringCostCadence | "";
}

export interface NamedRecurringCostDraft {
  id: string;
  label: string;
  amountSek: string;
  cadence: RecurringCostCadence | "";
}

export interface OneTimeCostDraft {
  id: string;
  label: string;
  amountSek: string;
}

export interface ManualCalculationForm {
  vehicleLabel: string;
  calculationPeriodMonths: string;
  purchasePriceSek: string;
  annualDistanceMil: string;
  residualValueKnown: boolean;
  expectedResidualValueSek: string;
  financing: FinancingDraft;
  energySources: EnergySourceDraft[];
  vehicleTax: OptionalRecurringCostDraft;
  insurance: OptionalRecurringCostDraft;
  maintenanceAndRepairs: OptionalRecurringCostDraft;
  otherRecurringCosts: NamedRecurringCostDraft[];
  otherOneTimeCosts: OneTimeCostDraft[];
}

let nextDraftId = 0;

function createDraftId(prefix: string) {
  nextDraftId += 1;
  return `${prefix}-${nextDraftId}`;
}

export function createInitialManualCalculationForm(): ManualCalculationForm {
  return {
    vehicleLabel: "",
    calculationPeriodMonths: "12",
    purchasePriceSek: "",
    annualDistanceMil: "",
    residualValueKnown: false,
    expectedResidualValueSek: "",
    financing: {
      enabled: false,
      downPaymentSek: "",
      annualNominalInterestRatePercent: "",
      termMonths: "",
    },
    energySources: [],
    vehicleTax: createOptionalRecurringCost(),
    insurance: createOptionalRecurringCost(),
    maintenanceAndRepairs: createOptionalRecurringCost(),
    otherRecurringCosts: [],
    otherOneTimeCosts: [],
  };
}

export function createEnergySource(distanceSharePercent = ""): EnergySourceDraft {
  return {
    id: createDraftId("energy"),
    label: "",
    unit: "",
    consumptionPer100Kilometres: "",
    pricePerUnitSek: "",
    distanceSharePercent,
  };
}

export function createOptionalRecurringCost(): OptionalRecurringCostDraft {
  return {
    isKnown: false,
    amountSek: "",
    cadence: "",
  };
}

export function createNamedRecurringCost(): NamedRecurringCostDraft {
  return {
    id: createDraftId("recurring"),
    label: "",
    amountSek: "",
    cadence: "",
  };
}

export function createOneTimeCost(): OneTimeCostDraft {
  return {
    id: createDraftId("one-time"),
    label: "",
    amountSek: "",
  };
}
