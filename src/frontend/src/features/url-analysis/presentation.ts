import type { FieldProvenance, ListingFieldCode } from "@/api/client";
import type { ScalarFieldName } from "./review-model";

export interface SelectOption {
  value: string;
  label: string;
}

export interface ScalarFieldDefinition {
  name: ScalarFieldName;
  label: string;
  kind: "text" | "decimal" | "integer" | "date" | "select" | "boolean";
  suffix?: string;
  options?: SelectOption[];
}

export const identityFields: ScalarFieldDefinition[] = [
  { name: "registrationNumber", label: "Registreringsnummer", kind: "text" },
  { name: "make", label: "Märke", kind: "text" },
  { name: "model", label: "Modell", kind: "text" },
  { name: "variant", label: "Variant", kind: "text" },
  { name: "modelYear", label: "Modellår", kind: "integer" },
  { name: "vin", label: "Chassinummer (VIN)", kind: "text" },
  { name: "vehicleLabel", label: "Eget namn eller smeknamn", kind: "text" },
];

export const advertisementFields: ScalarFieldDefinition[] = [
  { name: "priceSek", label: "Annonspris", kind: "decimal", suffix: "kr" },
  { name: "odometerKilometres", label: "Mätarställning", kind: "decimal", suffix: "mil" },
  { name: "sellerType", label: "Säljartyp", kind: "select", options: options({ private: "Privat", dealer: "Handlare" }) },
  { name: "location", label: "Ort eller område", kind: "text" },
  { name: "publishedDate", label: "Publicerad", kind: "date" },
  { name: "updatedDate", label: "Uppdaterad", kind: "date" },
  { name: "imageCount", label: "Antal bilder", kind: "integer" },
];

export const technicalFields: ScalarFieldDefinition[] = [
  { name: "transmission", label: "Växellåda", kind: "select", options: options({ manual: "Manuell", automatic: "Automatisk" }) },
  { name: "drivetrain", label: "Drivning", kind: "select", options: options({ frontWheelDrive: "Framhjulsdrift", rearWheelDrive: "Bakhjulsdrift", allWheelDrive: "Fyrhjulsdrift" }) },
  { name: "bodyType", label: "Karosstyp", kind: "select", options: options({ sedan: "Sedan", hatchback: "Halvkombi", wagon: "Kombi", suv: "SUV", coupe: "Coupé", convertible: "Cabriolet", minivan: "Minibuss/MPV", pickup: "Pickup", van: "Skåpbil", other: "Annan" }) },
  { name: "colour", label: "Färg", kind: "text" },
  { name: "horsepower", label: "Effekt", kind: "integer", suffix: "hk" },
  { name: "engineDisplacementCubicCentimetres", label: "Slagvolym", kind: "decimal", suffix: "cm³" },
  { name: "annualVehicleTaxSek", label: "Årlig fordonsskatt", kind: "decimal", suffix: "kr/år" },
];

export const historyFields: ScalarFieldDefinition[] = [
  { name: "ownerCount", label: "Antal ägare", kind: "integer" },
  { name: "firstRegistrationDate", label: "Första registrering", kind: "date" },
  { name: "lastInspectionDate", label: "Senaste besiktning", kind: "date" },
  { name: "nextInspectionDate", label: "Nästa besiktning", kind: "date" },
  { name: "towBar", label: "Dragkrok", kind: "boolean" },
];

export const fuelOptions = options({
  petrol: "Bensin",
  diesel: "Diesel",
  electricity: "El",
  ethanol: "Etanol",
  biogas: "Biogas",
  naturalGas: "Naturgas",
  liquefiedPetroleumGas: "Gasol",
  hydrogen: "Vätgas",
  other: "Annat",
});

export const energyUnitOptions = options({
  litre: "liter",
  kilowattHour: "kWh",
  kilogram: "kg",
});

export const missingFieldLabels: Record<ListingFieldCode, string> = {
  registrationNumber: "registreringsnummer",
  make: "märke",
  model: "modell",
  variant: "variant",
  modelYear: "modellår",
  vin: "chassinummer",
  priceSek: "pris",
  odometerKilometres: "mätarställning",
  sellerType: "säljartyp",
  location: "ort eller område",
  publishedDate: "publiceringsdatum",
  updatedDate: "uppdateringsdatum",
  imageCount: "antal bilder",
  fuelTypes: "bränsletyp",
  transmission: "växellåda",
  drivetrain: "drivning",
  bodyType: "karosstyp",
  colour: "färg",
  horsepower: "effekt",
  engineDisplacementCubicCentimetres: "slagvolym",
  energyConsumptions: "energiförbrukning",
  annualVehicleTaxSek: "fordonsskatt",
  ownerCount: "antal ägare",
  firstRegistrationDate: "första registrering",
  lastInspectionDate: "senaste besiktning",
  nextInspectionDate: "nästa besiktning",
  towBar: "dragkrok",
  equipment: "utrustning",
  sellerClaims: "säljaruppgifter",
  conditionNotes: "skicknoteringar",
};

export const inputClassName = "mt-2 h-11 w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 text-sm text-white outline-none transition focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20 disabled:opacity-60";
export const textareaClassName = "mt-2 min-h-24 w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-white outline-none transition focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20 disabled:opacity-60";

export function provenanceLabel(provenance: FieldProvenance | null) {
  if (!provenance) return "Okänt";
  if (provenance.origin === "user") return "Användare · Manuell · Bekräftad";
  if (provenance.verification === "registryVerified") return "Register · Verifierad";
  return "Annons · AI · Inte verifierad";
}

export function formatMoneyInput(input: string) {
  const value = Number(input.replace(",", "."));
  return Number.isFinite(value)
    ? new Intl.NumberFormat("sv-SE", { style: "currency", currency: "SEK", minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value)
    : "Okänt";
}

export function formatDateTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? value
    : new Intl.DateTimeFormat("sv-SE", { dateStyle: "medium", timeStyle: "short" }).format(date);
}

function options(values: Record<string, string>): SelectOption[] {
  return Object.entries(values).map(([value, label]) => ({ value, label }));
}
