import type { components } from "@/api/schema";
import type {
  EnergySourceDraft,
  ManualCalculationForm,
  OptionalRecurringCostDraft,
} from "./form-model";

export type ManualCalculationRequest = components["schemas"]["ManualCalculationRequest"];
export type ValidationErrors = Record<string, string[]>;

export interface ManualCalculationValidation {
  errors: ValidationErrors;
  request?: ManualCalculationRequest;
}

const maximumMoneySek = 100_000_000;
const maximumAnnualDistanceKilometres = 1_000_000;
const maximumConsumptionPer100Kilometres = 10_000;
const maximumPricePerUnitSek = 100_000;
const maximumLabelLength = 120;

const decimalPattern = /^\d+(?:[,.]\d+)?$/;
const integerPattern = /^\d+$/;

export function parseLocalizedDecimal(value: string): number | null {
  const normalized = value.trim();
  if (!decimalPattern.test(normalized)) {
    return null;
  }

  const parsed = Number(normalized.replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

export function parseUnsignedInteger(value: string): number | null {
  const normalized = value.trim();
  if (!integerPattern.test(normalized)) {
    return null;
  }

  const parsed = Number(normalized);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

export function convertMilToKilometres(value: string): number | null {
  const mil = parseLocalizedDecimal(value);
  return mil === null ? null : mil * 10;
}

export function validateManualCalculationForm(
  form: ManualCalculationForm,
): ManualCalculationValidation {
  const errors: ValidationErrors = {};
  const addError = (path: string, message: string) => {
    errors[path] = [...(errors[path] ?? []), message];
  };

  const vehicleLabel = form.vehicleLabel.trim();
  if (vehicleLabel.length > maximumLabelLength) {
    addError("vehicleLabel", "Namnet får vara högst 120 tecken.");
  }

  const calculationPeriodMonths = requireInteger(
    form.calculationPeriodMonths,
    "calculationPeriodMonths",
    "Ange en beräkningsperiod.",
    addError,
  );
  if (
    calculationPeriodMonths !== null &&
    (calculationPeriodMonths < 1 || calculationPeriodMonths > 120)
  ) {
    addError("calculationPeriodMonths", "Perioden måste vara mellan 1 och 120 månader.");
  }

  const purchasePriceSek = requireDecimal(
    form.purchasePriceSek,
    "purchasePriceSek",
    "Ange bilens inköpspris.",
    addError,
  );
  validateRange(
    purchasePriceSek,
    0,
    maximumMoneySek,
    "purchasePriceSek",
    "Priset måste vara mellan 0 och 100 000 000 kr.",
    addError,
  );

  const annualDistanceKilometres = requireConvertedDistance(form.annualDistanceMil, addError);
  validateRange(
    annualDistanceKilometres,
    0,
    maximumAnnualDistanceKilometres,
    "annualDistanceKilometres",
    "Körsträckan måste vara mellan 0 och 100 000 mil per år.",
    addError,
  );

  let expectedResidualValueSek: number | null = null;
  if (form.residualValueKnown) {
    expectedResidualValueSek = requireDecimal(
      form.expectedResidualValueSek,
      "expectedResidualValueSek",
      "Ange det förväntade restvärdet.",
      addError,
    );
    validateRange(
      expectedResidualValueSek,
      0,
      maximumMoneySek,
      "expectedResidualValueSek",
      "Restvärdet måste vara mellan 0 och 100 000 000 kr.",
      addError,
    );
    if (
      expectedResidualValueSek !== null &&
      purchasePriceSek !== null &&
      expectedResidualValueSek > purchasePriceSek
    ) {
      addError("expectedResidualValueSek", "Restvärdet får inte överstiga inköpspriset.");
    }
  }

  let financing: ManualCalculationRequest["financing"] = null;
  if (form.financing.enabled) {
    const downPaymentSek = requireDecimal(
      form.financing.downPaymentSek,
      "financing.downPaymentSek",
      "Ange kontantinsatsen.",
      addError,
    );
    const annualNominalInterestRatePercent = requireDecimal(
      form.financing.annualNominalInterestRatePercent,
      "financing.annualNominalInterestRatePercent",
      "Ange den nominella årsräntan.",
      addError,
    );
    const termMonths = requireInteger(
      form.financing.termMonths,
      "financing.termMonths",
      "Ange lånets löptid.",
      addError,
    );

    validateRange(
      downPaymentSek,
      0,
      maximumMoneySek,
      "financing.downPaymentSek",
      "Kontantinsatsen måste vara mellan 0 och 100 000 000 kr.",
      addError,
    );
    validateRange(
      annualNominalInterestRatePercent,
      0,
      100,
      "financing.annualNominalInterestRatePercent",
      "Räntan måste vara mellan 0 och 100 procent.",
      addError,
    );
    if (termMonths !== null && (termMonths < 1 || termMonths > 120)) {
      addError("financing.termMonths", "Löptiden måste vara mellan 1 och 120 månader.");
    }
    if (purchasePriceSek === 0) {
      addError("financing", "En bil med priset 0 kr kan inte finansieras.");
    }
    if (
      downPaymentSek !== null &&
      purchasePriceSek !== null &&
      downPaymentSek >= purchasePriceSek
    ) {
      addError("financing.downPaymentSek", "Kontantinsatsen måste vara lägre än inköpspriset.");
    }

    if (downPaymentSek !== null && annualNominalInterestRatePercent !== null && termMonths !== null) {
      financing = {
        downPaymentSek,
        annualNominalInterestRatePercent,
        termMonths,
      };
    }
  }

  if (form.energySources.length > 2) {
    addError("energySources", "Högst två energikällor får anges.");
  }
  if (
    annualDistanceKilometres !== null &&
    annualDistanceKilometres > 0 &&
    form.energySources.length === 0
  ) {
    addError("energySources", "Lägg till minst en energikälla när körsträckan är större än noll.");
  }

  const energySources = form.energySources.map((source, index) =>
    validateEnergySource(source, index, addError),
  );
  const shareTotal = form.energySources.reduce((total, source) => {
    const share = parseLocalizedDecimal(source.distanceSharePercent);
    return share !== null && share > 0 && share <= 100 ? total + share : total;
  }, 0);
  if (energySources.length > 0 && Math.abs(shareTotal - 100) > 1e-9) {
    addError("energySources", "Energikällornas andelar måste tillsammans vara exakt 100 procent.");
  }

  const vehicleTax = validateOptionalCost(form.vehicleTax, "vehicleTax", addError);
  const insurance = validateOptionalCost(form.insurance, "insurance", addError);
  const maintenanceAndRepairs = validateOptionalCost(
    form.maintenanceAndRepairs,
    "maintenanceAndRepairs",
    addError,
  );

  if (form.otherRecurringCosts.length > 50) {
    addError("otherRecurringCosts", "Högst 50 återkommande kostnader får anges.");
  }
  const otherRecurringCosts = form.otherRecurringCosts.map((cost, index) => {
    const path = `otherRecurringCosts[${index}]`;
    const label = validateRequiredLabel(cost.label, `${path}.label`, addError);
    const amountSek = requireDecimal(
      cost.amountSek,
      `${path}.amountSek`,
      "Ange kostnadens belopp.",
      addError,
    );
    validateRange(
      amountSek,
      0,
      maximumMoneySek,
      `${path}.amountSek`,
      "Beloppet måste vara mellan 0 och 100 000 000 kr.",
      addError,
    );
    if (!cost.cadence) {
      addError(`${path}.cadence`, "Välj om kostnaden är månadsvis eller årlig.");
    }
    return label && amountSek !== null && cost.cadence
      ? { label, amountSek, cadence: cost.cadence }
      : null;
  });

  if (form.otherOneTimeCosts.length > 50) {
    addError("otherOneTimeCosts", "Högst 50 engångskostnader får anges.");
  }
  const otherOneTimeCosts = form.otherOneTimeCosts.map((cost, index) => {
    const path = `otherOneTimeCosts[${index}]`;
    const label = validateRequiredLabel(cost.label, `${path}.label`, addError);
    const amountSek = requireDecimal(
      cost.amountSek,
      `${path}.amountSek`,
      "Ange kostnadens belopp.",
      addError,
    );
    validateRange(
      amountSek,
      0,
      maximumMoneySek,
      `${path}.amountSek`,
      "Beloppet måste vara mellan 0 och 100 000 000 kr.",
      addError,
    );
    return label && amountSek !== null ? { label, amountSek } : null;
  });

  if (Object.keys(errors).length > 0) {
    return { errors };
  }

  return {
    errors,
    request: {
      vehicleLabel: vehicleLabel || null,
      calculationPeriodMonths: calculationPeriodMonths!,
      purchasePriceSek: purchasePriceSek!,
      expectedResidualValueSek,
      annualDistanceKilometres: annualDistanceKilometres!,
      financing,
      energySources: energySources.filter(isPresent),
      vehicleTax,
      insurance,
      maintenanceAndRepairs,
      otherRecurringCosts: otherRecurringCosts.filter(isPresent),
      otherOneTimeCosts: otherOneTimeCosts.filter(isPresent),
    },
  };
}

type AddError = (path: string, message: string) => void;

function requireDecimal(value: string, path: string, emptyMessage: string, addError: AddError) {
  if (!value.trim()) {
    addError(path, emptyMessage);
    return null;
  }

  const parsed = parseLocalizedDecimal(value);
  if (parsed === null) {
    addError(path, "Ange ett giltigt icke-negativt tal utan tusentalsavgränsare eller enhet.");
  }
  return parsed;
}

function requireInteger(value: string, path: string, emptyMessage: string, addError: AddError) {
  if (!value.trim()) {
    addError(path, emptyMessage);
    return null;
  }

  const parsed = parseUnsignedInteger(value);
  if (parsed === null) {
    addError(path, "Ange ett heltal med endast siffror.");
  }
  return parsed;
}

function requireConvertedDistance(value: string, addError: AddError) {
  if (!value.trim()) {
    addError("annualDistanceKilometres", "Ange årlig körsträcka.");
    return null;
  }

  const parsed = convertMilToKilometres(value);
  if (parsed === null) {
    addError(
      "annualDistanceKilometres",
      "Ange körsträckan som ett giltigt antal mil utan enhet.",
    );
  }
  return parsed;
}

function validateRange(
  value: number | null,
  minimum: number,
  maximum: number,
  path: string,
  message: string,
  addError: AddError,
  minimumExclusive = false,
) {
  if (value === null) {
    return;
  }
  if ((minimumExclusive ? value <= minimum : value < minimum) || value > maximum) {
    addError(path, message);
  }
}

function validateRequiredLabel(value: string, path: string, addError: AddError) {
  const label = value.trim();
  if (!label) {
    addError(path, "Ange ett namn.");
    return null;
  }
  if (label.length > maximumLabelLength) {
    addError(path, "Namnet får vara högst 120 tecken.");
  }
  return label;
}

function validateEnergySource(source: EnergySourceDraft, index: number, addError: AddError) {
  const path = `energySources[${index}]`;
  const label = validateRequiredLabel(source.label, `${path}.label`, addError);
  if (!source.unit) {
    addError(`${path}.unit`, "Välj energienhet.");
  }
  const consumptionPer100Kilometres = requireDecimal(
    source.consumptionPer100Kilometres,
    `${path}.consumptionPer100Kilometres`,
    "Ange förbrukningen per 100 km.",
    addError,
  );
  validateRange(
    consumptionPer100Kilometres,
    0,
    maximumConsumptionPer100Kilometres,
    `${path}.consumptionPer100Kilometres`,
    "Förbrukningen måste vara större än 0 och högst 10 000 enheter per 100 km.",
    addError,
    true,
  );
  const pricePerUnitSek = requireDecimal(
    source.pricePerUnitSek,
    `${path}.pricePerUnitSek`,
    "Ange pris per enhet.",
    addError,
  );
  validateRange(
    pricePerUnitSek,
    0,
    maximumPricePerUnitSek,
    `${path}.pricePerUnitSek`,
    "Enhetspriset måste vara mellan 0 och 100 000 kr.",
    addError,
  );
  const distanceSharePercent = requireDecimal(
    source.distanceSharePercent,
    `${path}.distanceSharePercent`,
    "Ange energikällans andel.",
    addError,
  );
  validateRange(
    distanceSharePercent,
    0,
    100,
    `${path}.distanceSharePercent`,
    "Andelen måste vara större än 0 och högst 100 procent.",
    addError,
    true,
  );

  if (
    !label ||
    !source.unit ||
    consumptionPer100Kilometres === null ||
    pricePerUnitSek === null ||
    distanceSharePercent === null
  ) {
    return null;
  }
  return {
    label,
    unit: source.unit,
    consumptionPer100Kilometres,
    pricePerUnitSek,
    distanceSharePercent,
  };
}

function validateOptionalCost(
  cost: OptionalRecurringCostDraft,
  path: string,
  addError: AddError,
) {
  if (!cost.isKnown) {
    return null;
  }

  const amountSek = requireDecimal(
    cost.amountSek,
    `${path}.amountSek`,
    "Ange kostnadens belopp.",
    addError,
  );
  validateRange(
    amountSek,
    0,
    maximumMoneySek,
    `${path}.amountSek`,
    "Beloppet måste vara mellan 0 och 100 000 000 kr.",
    addError,
  );
  if (!cost.cadence) {
    addError(`${path}.cadence`, "Välj om kostnaden är månadsvis eller årlig.");
  }
  return amountSek !== null && cost.cadence
    ? { amountSek, cadence: cost.cadence }
    : null;
}

function isPresent<T>(value: T | null): value is T {
  return value !== null;
}
