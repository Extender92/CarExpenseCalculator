import type {
  FieldProvenance,
  ListingAnalysisResponse,
  ListingDraftResponse,
  ListingFieldCode,
} from "@/api/client";

export const scalarFieldNames = [
  "registrationNumber",
  "make",
  "model",
  "variant",
  "modelYear",
  "vin",
  "vehicleLabel",
  "priceSek",
  "odometerKilometres",
  "sellerType",
  "location",
  "publishedDate",
  "updatedDate",
  "imageCount",
  "transmission",
  "drivetrain",
  "bodyType",
  "colour",
  "horsepower",
  "engineDisplacementCubicCentimetres",
  "annualVehicleTaxSek",
  "ownerCount",
  "firstRegistrationDate",
  "lastInspectionDate",
  "nextInspectionDate",
  "towBar",
] as const;

export type ScalarFieldName = (typeof scalarFieldNames)[number];
export type CollectionMode = "unknown" | "empty" | "values";

export interface ScalarDraftField {
  input: string;
  provenance: FieldProvenance | null;
}

export interface StringCollectionEntry {
  id: string;
  value: string;
}

export interface EnergyConsumptionDraft {
  id: string;
  label: string;
  unit: "litre" | "kilowattHour" | "kilogram";
  consumptionPer100Kilometres: string;
}

export interface CollectionDraft<T> {
  mode: CollectionMode;
  values: T[];
  provenance: FieldProvenance | null;
}

export interface ListingReviewDraft {
  fields: Record<ScalarFieldName, ScalarDraftField>;
  fuelTypes: CollectionDraft<string>;
  energyConsumptions: CollectionDraft<EnergyConsumptionDraft>;
  equipment: CollectionDraft<StringCollectionEntry>;
  sellerClaims: CollectionDraft<StringCollectionEntry>;
  conditionNotes: CollectionDraft<StringCollectionEntry>;
}

export interface ListingWorkspaceItem {
  id: string;
  submittedUrl: string;
  normalizedUrl: string;
  phase: "queued" | "analyzing" | "retrying" | "complete" | "partial" | "unavailable" | "failed";
  analysis: ListingAnalysisResponse | null;
  draft: ListingReviewDraft;
  dirty: boolean;
  error: string | null;
  validationErrors: Record<string, string>;
  controller: AbortController | null;
}

const missingScalarFields: ReadonlyArray<readonly [ListingFieldCode, ScalarFieldName]> = [
  ["registrationNumber", "registrationNumber"],
  ["make", "make"],
  ["model", "model"],
  ["variant", "variant"],
  ["modelYear", "modelYear"],
  ["vin", "vin"],
  ["priceSek", "priceSek"],
  ["odometerKilometres", "odometerKilometres"],
  ["sellerType", "sellerType"],
  ["location", "location"],
  ["publishedDate", "publishedDate"],
  ["updatedDate", "updatedDate"],
  ["imageCount", "imageCount"],
  ["transmission", "transmission"],
  ["drivetrain", "drivetrain"],
  ["bodyType", "bodyType"],
  ["colour", "colour"],
  ["horsepower", "horsepower"],
  ["engineDisplacementCubicCentimetres", "engineDisplacementCubicCentimetres"],
  ["annualVehicleTaxSek", "annualVehicleTaxSek"],
  ["ownerCount", "ownerCount"],
  ["firstRegistrationDate", "firstRegistrationDate"],
  ["lastInspectionDate", "lastInspectionDate"],
  ["nextInspectionDate", "nextInspectionDate"],
  ["towBar", "towBar"],
];

let draftId = 0;

export function createEmptyReviewDraft(): ListingReviewDraft {
  const fields = Object.fromEntries(
    scalarFieldNames.map((name) => [name, { input: "", provenance: null }]),
  ) as Record<ScalarFieldName, ScalarDraftField>;

  return {
    fields,
    fuelTypes: emptyCollection(),
    energyConsumptions: emptyCollection(),
    equipment: emptyCollection(),
    sellerClaims: emptyCollection(),
    conditionNotes: emptyCollection(),
  };
}

export function analysisResponseToDraft(response: ListingAnalysisResponse): ListingReviewDraft {
  const listing = response.listing;
  return {
    fields: {
      registrationNumber: valueField(listing.registrationNumber),
      make: valueField(listing.make),
      model: valueField(listing.model),
      variant: valueField(listing.variant),
      modelYear: valueField(listing.modelYear),
      vin: valueField(listing.vin),
      vehicleLabel: valueField(listing.vehicleLabel),
      priceSek: valueField(listing.priceSek),
      odometerKilometres: valueField(listing.odometerKilometres, (value) => formatInputNumber(value / 10)),
      sellerType: valueField(listing.sellerType),
      location: valueField(listing.location),
      publishedDate: valueField(listing.publishedDate),
      updatedDate: valueField(listing.updatedDate),
      imageCount: valueField(listing.imageCount),
      transmission: valueField(listing.transmission),
      drivetrain: valueField(listing.drivetrain),
      bodyType: valueField(listing.bodyType),
      colour: valueField(listing.colour),
      horsepower: valueField(listing.horsepower),
      engineDisplacementCubicCentimetres: valueField(listing.engineDisplacementCubicCentimetres),
      annualVehicleTaxSek: valueField(listing.annualVehicleTaxSek),
      ownerCount: valueField(listing.ownerCount),
      firstRegistrationDate: valueField(listing.firstRegistrationDate),
      lastInspectionDate: valueField(listing.lastInspectionDate),
      nextInspectionDate: valueField(listing.nextInspectionDate),
      towBar: valueField(listing.towBar, (value) => String(value)),
    },
    fuelTypes: primitiveCollection(listing.fuelTypes),
    energyConsumptions: energyCollection(listing.energyConsumptions),
    equipment: stringCollection(listing.equipment),
    sellerClaims: stringCollection(listing.sellerClaims),
    conditionNotes: stringCollection(listing.conditionNotes),
  };
}

export function editScalarField(
  draft: ListingReviewDraft,
  name: ScalarFieldName,
  input: string,
  normalizedUrl: string,
): ListingReviewDraft {
  return {
    ...draft,
    fields: {
      ...draft.fields,
      [name]: {
        input,
        provenance: input === "" ? null : manualProvenance(normalizedUrl),
      },
    },
  };
}

export function editCollection<T>(
  draft: CollectionDraft<T>,
  mode: CollectionMode,
  values: T[],
  normalizedUrl: string,
): CollectionDraft<T> {
  return {
    mode,
    values,
    provenance: mode === "unknown" ? null : manualProvenance(normalizedUrl),
  };
}

export function createStringEntry(value = ""): StringCollectionEntry {
  draftId += 1;
  return { id: `listing-entry-${draftId}`, value };
}

export function createEnergyEntry(): EnergyConsumptionDraft {
  draftId += 1;
  return {
    id: `listing-energy-${draftId}`,
    label: "",
    unit: "litre",
    consumptionPer100Kilometres: "",
  };
}

export function deriveMissingFields(draft: ListingReviewDraft): ListingFieldCode[] {
  const missing = new Set<ListingFieldCode>(missingScalarFields
    .filter(([, name]) => draft.fields[name].input === "")
    .map(([code]) => code));

  if (draft.fuelTypes.mode === "unknown") missing.add("fuelTypes");
  if (draft.energyConsumptions.mode === "unknown") missing.add("energyConsumptions");
  if (draft.equipment.mode === "unknown") missing.add("equipment");
  if (draft.sellerClaims.mode === "unknown") missing.add("sellerClaims");
  if (draft.conditionNotes.mode === "unknown") missing.add("conditionNotes");

  return allMissingFieldCodes.filter((code) => missing.has(code));
}

export function manualProvenance(normalizedUrl: string): FieldProvenance {
  return {
    origin: "user",
    extractionMethod: "manual",
    verification: "userConfirmed",
    sourceUrl: normalizedUrl,
  };
}

export const allMissingFieldCodes: ListingFieldCode[] = [
  "registrationNumber", "make", "model", "variant", "modelYear", "vin", "priceSek",
  "odometerKilometres", "sellerType", "location", "publishedDate", "updatedDate", "imageCount",
  "fuelTypes", "transmission", "drivetrain", "bodyType", "colour", "horsepower",
  "engineDisplacementCubicCentimetres", "energyConsumptions", "annualVehicleTaxSek", "ownerCount",
  "firstRegistrationDate", "lastInspectionDate", "nextInspectionDate", "towBar", "equipment",
  "sellerClaims", "conditionNotes",
];

function valueField<T extends string | number | boolean>(
  value: { value: T; provenance: FieldProvenance } | null,
  transform: (value: T) => string = (input) => String(input),
): ScalarDraftField {
  return value
    ? { input: transform(value.value), provenance: { ...value.provenance } }
    : { input: "", provenance: null };
}

function primitiveCollection<T extends string>(
  source: { values: T[]; provenance: FieldProvenance } | null,
): CollectionDraft<string> {
  if (!source) return emptyCollection();
  return {
    mode: source.values.length === 0 ? "empty" : "values",
    values: [...source.values],
    provenance: { ...source.provenance },
  };
}

function stringCollection(
  source: { values: string[]; provenance: FieldProvenance } | null,
): CollectionDraft<StringCollectionEntry> {
  if (!source) return emptyCollection();
  return {
    mode: source.values.length === 0 ? "empty" : "values",
    values: source.values.map((value) => createStringEntry(value)),
    provenance: { ...source.provenance },
  };
}

function energyCollection(
  source: ListingDraftResponse["energyConsumptions"],
): CollectionDraft<EnergyConsumptionDraft> {
  if (!source) return emptyCollection();
  return {
    mode: source.values.length === 0 ? "empty" : "values",
    values: source.values.map((value) => ({
      id: createEnergyEntry().id,
      label: value.label,
      unit: value.unit,
      consumptionPer100Kilometres: formatInputNumber(value.consumptionPer100Kilometres),
    })),
    provenance: { ...source.provenance },
  };
}

function emptyCollection<T>(): CollectionDraft<T> {
  return { mode: "unknown", values: [], provenance: null };
}

function formatInputNumber(value: number) {
  return String(value);
}
