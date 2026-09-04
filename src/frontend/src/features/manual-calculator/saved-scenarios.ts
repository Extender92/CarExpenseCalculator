import type {
  ManualCalculationRequest,
  SavedCostScenarioResponse,
} from "@/api/client";
import {
  createEnergySource,
  createInitialManualCalculationForm,
  createNamedRecurringCost,
  createOneTimeCost,
  type ManualCalculationForm,
  type OptionalRecurringCostDraft,
} from "./form-model";

const ordinaryLetters = "ABCDEFGHJKLMNOPRSTUWXYZ";
const finalLetters = "ABCDEFGHJKLMNPRSTUWXYZ";
const ordinaryRegistrationPattern = new RegExp(
  `^[${ordinaryLetters}]{3}\\d{2}[\\d${finalLetters}]$`,
);

export interface RegistrationNumberValidation {
  normalized?: string;
  error?: string;
}

export interface OpenedSavedScenario {
  vehicleId: string;
  registrationNumber: string;
  revision: number;
  sourceListingVersion: number | null;
  currentListingVersion: number | null;
  isListingOutdated: boolean;
  hasSavedListing: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export function normalizeRegistrationNumber(value: string) {
  return value.replace(/[\s-]/g, "").toUpperCase();
}

export function validateRegistrationNumber(value: string): RegistrationNumberValidation {
  if (!value.trim()) {
    return { error: "Ange registreringsnummer för att spara bilen." };
  }

  const normalized = normalizeRegistrationNumber(value);
  if (!ordinaryRegistrationPattern.test(normalized)) {
    return {
      error: "Ange ett vanligt svenskt registreringsnummer, till exempel ABC123 eller ABC12D.",
    };
  }

  return { normalized };
}

export function savedScenarioToForm(
  saved: Pick<SavedCostScenarioResponse, "scenario">,
): ManualCalculationForm {
  const scenario = saved.scenario;
  const form = createInitialManualCalculationForm();

  form.vehicleLabel = scenario.vehicleLabel ?? "";
  form.calculationPeriodMonths = toDraftNumber(scenario.calculationPeriodMonths);
  form.purchasePriceSek = toDraftNumber(scenario.purchasePriceSek);
  form.annualDistanceMil = toDraftNumber(scenario.annualDistanceKilometres / 10);
  form.residualValueKnown = scenario.expectedResidualValueSek !== null
    && scenario.expectedResidualValueSek !== undefined;
  form.expectedResidualValueSek = form.residualValueKnown
    ? toDraftNumber(scenario.expectedResidualValueSek!)
    : "";

  if (scenario.financing) {
    form.financing = {
      enabled: true,
      downPaymentSek: toDraftNumber(scenario.financing.downPaymentSek),
      annualNominalInterestRatePercent: toDraftNumber(
        scenario.financing.annualNominalInterestRatePercent,
      ),
      termMonths: toDraftNumber(scenario.financing.termMonths),
    };
  }

  form.energySources = scenario.energySources.map((source) => ({
    ...createEnergySource(),
    label: source.label,
    unit: source.unit,
    consumptionPer100Kilometres: toDraftNumber(source.consumptionPer100Kilometres),
    pricePerUnitSek: toDraftNumber(source.pricePerUnitSek),
    distanceSharePercent: toDraftNumber(source.distanceSharePercent),
  }));
  form.vehicleTax = recurringCostToDraft(scenario.vehicleTax);
  form.insurance = recurringCostToDraft(scenario.insurance);
  form.maintenanceAndRepairs = recurringCostToDraft(scenario.maintenanceAndRepairs);
  form.otherRecurringCosts = scenario.otherRecurringCosts.map((cost) => ({
    ...createNamedRecurringCost(),
    label: cost.label,
    amountSek: toDraftNumber(cost.amountSek),
    cadence: cost.cadence,
  }));
  form.otherOneTimeCosts = scenario.otherOneTimeCosts.map((cost) => ({
    ...createOneTimeCost(),
    label: cost.label,
    amountSek: toDraftNumber(cost.amountSek),
  }));

  return form;
}

export function savedScenarioMetadata(saved: SavedCostScenarioResponse): OpenedSavedScenario {
  return {
    vehicleId: saved.vehicleId,
    registrationNumber: saved.registrationNumber,
    revision: saved.revision,
    sourceListingVersion: saved.sourceListingVersion,
    currentListingVersion: saved.currentListingVersion,
    isListingOutdated: saved.isListingOutdated,
    hasSavedListing: saved.hasSavedListing,
    createdAtUtc: saved.createdAtUtc,
    updatedAtUtc: saved.updatedAtUtc,
  };
}

function recurringCostToDraft(
  cost: ManualCalculationRequest["vehicleTax"],
): OptionalRecurringCostDraft {
  return cost
    ? { isKnown: true, amountSek: toDraftNumber(cost.amountSek), cadence: cost.cadence }
    : { isKnown: false, amountSek: "", cadence: "" };
}

function toDraftNumber(value: number) {
  return String(value);
}
