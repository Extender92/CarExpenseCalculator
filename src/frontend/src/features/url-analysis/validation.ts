import type { ListingReviewDraft, ScalarFieldName } from "./review-model";

export interface ParsedNumber {
  value?: number;
  error?: string;
}

const decimalPattern = /^\d+(?:[.,]\d+)?$/;
const integerPattern = /^\d+$/;
const ordinaryLetters = "ABCDEFGHJKLMNOPRSTUWXYZ";
const finalLetters = "ABCDEFGHJKLMNPRSTUWXYZ";
const registrationPattern = new RegExp(`^[${ordinaryLetters}]{3}\\d{2}[\\d${finalLetters}]$`);

const generalTextFields: ScalarFieldName[] = [
  "make", "model", "variant", "vehicleLabel", "location", "colour",
];
const dateFields: ScalarFieldName[] = [
  "publishedDate", "updatedDate", "firstRegistrationDate", "lastInspectionDate", "nextInspectionDate",
];

export function parseLocalizedNumber(input: string, integer = false): ParsedNumber {
  const value = input.trim();
  if (!value) return { error: "Ange ett värde eller lämna fältet okänt." };
  if (!(integer ? integerPattern : decimalPattern).test(value)) {
    return {
      error: integer
        ? "Ange ett heltal med endast siffror."
        : "Ange ett tal med komma eller punkt som decimaltecken, utan enhet eller tusentalsavgränsare.",
    };
  }
  const parsed = Number(value.replace(",", "."));
  return Number.isFinite(parsed) ? { value: parsed } : { error: "Talet är för stort." };
}

export function validateReviewDraft(draft: ListingReviewDraft): Record<string, string> {
  const errors: Record<string, string> = {};

  for (const name of generalTextFields) validateOptionalText(draft.fields[name].input, name, 100, errors);
  validateOptionalText(draft.fields.vin.input, "vin", 50, errors);
  validateRegistration(draft.fields.registrationNumber.input, errors);
  validateOptionalNumber(draft.fields.modelYear.input, "modelYear", 1886, 2100, true, errors);
  validateOptionalNumber(draft.fields.priceSek.input, "priceSek", 0, 100_000_000, false, errors);
  validateOptionalNumber(draft.fields.odometerKilometres.input, "odometerKilometres", 0, 1_000_000, false, errors);
  validateOptionalNumber(draft.fields.imageCount.input, "imageCount", 0, 10_000, true, errors);
  validateOptionalNumber(draft.fields.horsepower.input, "horsepower", 1, 10_000, true, errors);
  validateOptionalNumber(
    draft.fields.engineDisplacementCubicCentimetres.input,
    "engineDisplacementCubicCentimetres",
    1,
    100_000,
    false,
    errors,
  );
  validateOptionalNumber(draft.fields.annualVehicleTaxSek.input, "annualVehicleTaxSek", 0, 100_000_000, false, errors);
  validateOptionalNumber(draft.fields.ownerCount.input, "ownerCount", 0, 10_000, true, errors);

  for (const name of dateFields) {
    const input = draft.fields[name].input;
    if (input && !isIsoDate(input)) errors[name] = "Ange ett giltigt datum.";
  }

  validateEnum(draft.fields.sellerType.input, "sellerType", ["private", "dealer"], errors);
  validateEnum(draft.fields.transmission.input, "transmission", ["manual", "automatic"], errors);
  validateEnum(draft.fields.drivetrain.input, "drivetrain", ["frontWheelDrive", "rearWheelDrive", "allWheelDrive"], errors);
  validateEnum(
    draft.fields.bodyType.input,
    "bodyType",
    ["sedan", "hatchback", "wagon", "suv", "coupe", "convertible", "minivan", "pickup", "van", "other"],
    errors,
  );
  validateEnum(draft.fields.towBar.input, "towBar", ["true", "false"], errors);

  validateEnumCollection(draft.fuelTypes, "fuelTypes", [
    "petrol", "diesel", "electricity", "ethanol", "biogas", "naturalGas",
    "liquefiedPetroleumGas", "hydrogen", "other",
  ], errors);
  validateStringCollection(draft.equipment, "equipment", 100, 100, errors);
  validateStringCollection(draft.sellerClaims, "sellerClaims", 20, 200, errors);
  validateStringCollection(draft.conditionNotes, "conditionNotes", 10, 300, errors);
  validateEnergy(draft, errors);

  return errors;
}

export function normalizeScalarInput(name: ScalarFieldName, input: string) {
  const normalized = input.trim().normalize("NFC");
  if (name === "registrationNumber") return normalized.replace(/[\s-]/g, "").toUpperCase();
  if (name === "vin") return normalized.toUpperCase();
  return normalized;
}

function validateRegistration(input: string, errors: Record<string, string>) {
  if (!input) return;
  const normalized = input.replace(/[\s-]/g, "").toUpperCase();
  if (!registrationPattern.test(normalized)) {
    errors.registrationNumber = "Ange ett vanligt svenskt registreringsnummer, till exempel ABC123 eller ABC12D.";
  }
}

function validateOptionalText(
  input: string,
  path: string,
  maximumLength: number,
  errors: Record<string, string>,
) {
  if (!input) return;
  const normalized = input.trim().normalize("NFC");
  if (!normalized) errors[path] = "Värdet måste innehålla minst ett synligt tecken.";
  else if (normalized.length > maximumLength) errors[path] = `Ange högst ${maximumLength} tecken.`;
}

function validateOptionalNumber(
  input: string,
  path: string,
  minimum: number,
  maximum: number,
  integer: boolean,
  errors: Record<string, string>,
) {
  if (!input) return;
  const parsed = parseLocalizedNumber(input, integer);
  if (parsed.error) errors[path] = parsed.error;
  else if (parsed.value! < minimum || parsed.value! > maximum) {
    errors[path] = `Värdet måste vara mellan ${minimum.toLocaleString("sv-SE")} och ${maximum.toLocaleString("sv-SE")}.`;
  }
}

function validateEnum(
  input: string,
  path: string,
  allowed: readonly string[],
  errors: Record<string, string>,
) {
  if (input && !allowed.includes(input)) errors[path] = "Välj ett giltigt alternativ.";
}

function validateEnumCollection(
  collection: ListingReviewDraft["fuelTypes"],
  path: string,
  allowed: readonly string[],
  errors: Record<string, string>,
) {
  if (collection.mode !== "values") return;
  if (collection.values.length === 0) errors[path] = "Välj minst ett värde eller ange Inga.";
  const seen = new Set<string>();
  collection.values.forEach((value, index) => {
    if (!allowed.includes(value)) errors[`${path}.values[${index}]`] = "Välj ett giltigt alternativ.";
    const normalized = value.toLocaleLowerCase("en-US");
    if (seen.has(normalized)) errors[`${path}.values[${index}]`] = "Samma värde får inte anges flera gånger.";
    seen.add(normalized);
  });
}

function validateStringCollection(
  collection: ListingReviewDraft["equipment"],
  path: string,
  maximumCount: number,
  maximumLength: number,
  errors: Record<string, string>,
) {
  if (collection.mode !== "values") return;
  if (collection.values.length === 0) errors[path] = "Lägg till minst ett värde eller ange Inga.";
  if (collection.values.length > maximumCount) errors[path] = `Högst ${maximumCount} värden tillåts.`;
  const seen = new Set<string>();
  collection.values.forEach((entry, index) => {
    const normalized = entry.value.trim().normalize("NFC");
    if (!normalized) errors[`${path}.values[${index}]`] = "Värdet får inte vara tomt.";
    else if (normalized.length > maximumLength) errors[`${path}.values[${index}]`] = `Ange högst ${maximumLength} tecken.`;
    const key = normalized.toLocaleLowerCase("en-US");
    if (normalized && seen.has(key)) errors[`${path}.values[${index}]`] = "Samma värde får inte anges flera gånger.";
    seen.add(key);
  });
}

function validateEnergy(draft: ListingReviewDraft, errors: Record<string, string>) {
  const collection = draft.energyConsumptions;
  if (collection.mode !== "values") return;
  if (collection.values.length === 0) errors.energyConsumptions = "Lägg till minst en förbrukning eller ange Inga.";
  if (collection.values.length > 2) errors.energyConsumptions = "Högst två förbrukningsvärden tillåts.";
  const seen = new Set<string>();
  collection.values.forEach((entry, index) => {
    const path = `energyConsumptions.values[${index}]`;
    const label = entry.label.trim().normalize("NFC");
    if (!label) errors[`${path}.label`] = "Ange en etikett.";
    else if (label.length > 100) errors[`${path}.label`] = "Ange högst 100 tecken.";
    const key = label.toLocaleLowerCase("en-US");
    if (label && seen.has(key)) errors[`${path}.label`] = "Etiketten måste vara unik.";
    seen.add(key);

    if (!["litre", "kilowattHour", "kilogram"].includes(entry.unit)) {
      errors[`${path}.unit`] = "Välj en giltig enhet.";
    }
    const consumption = parseLocalizedNumber(entry.consumptionPer100Kilometres);
    if (consumption.error) errors[`${path}.consumptionPer100Kilometres`] = consumption.error;
    else if (consumption.value! <= 0 || consumption.value! > 10_000) {
      errors[`${path}.consumptionPer100Kilometres`] = "Värdet måste vara större än 0 och högst 10 000.";
    }
  });
}

function isIsoDate(input: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(input)) return false;
  const [year, month, day] = input.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}
