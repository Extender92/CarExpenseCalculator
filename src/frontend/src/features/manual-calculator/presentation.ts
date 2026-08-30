import type { ValidationProblemDetails } from "@/api/client";
import type { ValidationErrors } from "./validation";

const moneyFormatter = new Intl.NumberFormat("sv-SE", {
  style: "currency",
  currency: "SEK",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const quantityFormatter = new Intl.NumberFormat("sv-SE", {
  minimumFractionDigits: 0,
  maximumFractionDigits: 3,
});

const integerFormatter = new Intl.NumberFormat("sv-SE", {
  maximumFractionDigits: 0,
});

const dateTimeFormatter = new Intl.DateTimeFormat("sv-SE", {
  dateStyle: "medium",
  timeStyle: "short",
});

const missingCategoryLabels = {
  vehicleTax: "fordonsskatt",
  insurance: "försäkring",
  maintenanceAndRepairs: "underhåll och reparationer",
  residualValue: "restvärde",
} as const;

const serverMessageTranslations: Record<string, string> = {
  "Residual value cannot exceed purchase price.": "Restvärdet får inte överstiga inköpspriset.",
  "A zero-price vehicle cannot be financed.": "En bil med priset 0 kr kan inte finansieras.",
  "Down payment must be less than purchase price.":
    "Kontantinsatsen måste vara lägre än inköpspriset.",
  "At most two energy sources are allowed.": "Högst två energikällor får anges.",
  "At least one energy source is required for positive distance.":
    "Lägg till minst en energikälla när körsträckan är större än noll.",
  "Energy source shares must total exactly 100 percent.":
    "Energikällornas andelar måste tillsammans vara exakt 100 procent.",
  "Label must contain at least one non-whitespace character.": "Ange ett namn.",
  "Label cannot exceed 120 characters after trimming.": "Namnet får vara högst 120 tecken.",
  "Value is not supported.": "Värdet stöds inte.",
};

export function formatSek(value: number) {
  return moneyFormatter.format(value);
}

export function formatQuantity(value: number) {
  return quantityFormatter.format(value);
}

export function formatInteger(value: number) {
  return integerFormatter.format(value);
}

export function formatDateTime(value: string) {
  return dateTimeFormatter.format(new Date(value));
}

export function formatDistance(kilometres: number) {
  return `${formatQuantity(kilometres)} km (${formatQuantity(kilometres / 10)} mil)`;
}

export function formatEnergyUnit(unit: "litre" | "kilowattHour" | "kilogram") {
  return unit === "litre" ? "liter" : unit === "kilowattHour" ? "kWh" : "kg";
}

export function formatCadence(cadence: "monthly" | "annual") {
  return cadence === "monthly" ? "per månad" : "per år";
}

export function formatMissingCategory(category: keyof typeof missingCategoryLabels) {
  return missingCategoryLabels[category];
}

export function fieldDomId(path: string) {
  return `manual-${path.replace(/[^a-zA-Z0-9]+/g, "-")}`;
}

export function fieldErrorId(path: string) {
  return `${fieldDomId(path)}-error`;
}

export function fieldLabel(path: string) {
  const normalized = path.replace(/^\$\.?/, "");
  const exactLabels: Record<string, string> = {
    registrationNumber: "Registreringsnummer",
    vehicleLabel: "Bilens namn",
    calculationPeriodMonths: "Beräkningsperiod",
    purchasePriceSek: "Inköpspris",
    expectedResidualValueSek: "Restvärde",
    annualDistanceKilometres: "Årlig körsträcka",
    financing: "Finansiering",
    "financing.downPaymentSek": "Kontantinsats",
    "financing.annualNominalInterestRatePercent": "Nominell årsränta",
    "financing.termMonths": "Lånets löptid",
    energySources: "Energikällor",
    vehicleTax: "Fordonsskatt",
    "vehicleTax.amountSek": "Fordonsskattens belopp",
    "vehicleTax.cadence": "Fordonsskattens intervall",
    insurance: "Försäkring",
    "insurance.amountSek": "Försäkringens belopp",
    "insurance.cadence": "Försäkringens intervall",
    maintenanceAndRepairs: "Underhåll och reparationer",
    "maintenanceAndRepairs.amountSek": "Underhållskostnad",
    "maintenanceAndRepairs.cadence": "Underhållskostnadens intervall",
    otherRecurringCosts: "Övriga återkommande kostnader",
    otherOneTimeCosts: "Övriga engångskostnader",
  };

  if (exactLabels[normalized]) {
    return exactLabels[normalized];
  }

  const energyMatch = normalized.match(/^energySources\[(\d+)]\.(.+)$/);
  if (energyMatch) {
    const labels: Record<string, string> = {
      label: "namn",
      unit: "enhet",
      consumptionPer100Kilometres: "förbrukning",
      pricePerUnitSek: "enhetspris",
      distanceSharePercent: "andel",
    };
    return `Energikälla ${Number(energyMatch[1]) + 1}: ${labels[energyMatch[2]] ?? "värde"}`;
  }

  const recurringMatch = normalized.match(/^otherRecurringCosts\[(\d+)]\.(.+)$/);
  if (recurringMatch) {
    const labels: Record<string, string> = {
      label: "namn",
      amountSek: "belopp",
      cadence: "intervall",
    };
    return `Återkommande kostnad ${Number(recurringMatch[1]) + 1}: ${labels[recurringMatch[2]] ?? "värde"}`;
  }

  const oneTimeMatch = normalized.match(/^otherOneTimeCosts\[(\d+)]\.(.+)$/);
  if (oneTimeMatch) {
    const labels: Record<string, string> = { label: "namn", amountSek: "belopp" };
    return `Engångskostnad ${Number(oneTimeMatch[1]) + 1}: ${labels[oneTimeMatch[2]] ?? "värde"}`;
  }

  return "Formuläret";
}

export function validationProblemToErrors(problem?: ValidationProblemDetails): ValidationErrors {
  const result: ValidationErrors = {};
  for (const [rawPath, messages] of Object.entries(problem?.errors ?? {})) {
    const path = normalizeServerPath(rawPath);
    result[path] = messages.map(translateServerMessage);
  }
  return result;
}

export function savedValidationProblemToErrors(
  problem?: ValidationProblemDetails,
): ValidationErrors {
  const result: ValidationErrors = {};
  for (const [rawPath, messages] of Object.entries(problem?.errors ?? {})) {
    const normalized = normalizeServerPath(rawPath);
    const path = normalized === "scenario"
      ? "form"
      : normalized.replace(/^scenario\./, "");
    result[path] = messages.map(translateServerMessage);
  }
  return result;
}

function normalizeServerPath(path: string) {
  if (path === "$" || !path) {
    return "form";
  }
  return path.replace(/^\$\.?/, "");
}

function translateServerMessage(message: string) {
  if (serverMessageTranslations[message]) {
    return serverMessageTranslations[message];
  }
  if (/^Value must be /.test(message)) {
    return "Värdet ligger utanför det tillåtna intervallet.";
  }
  return "Servern godkände inte värdet. Kontrollera det och försök igen.";
}
