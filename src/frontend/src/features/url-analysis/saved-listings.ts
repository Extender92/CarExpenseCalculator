import type {
  BodyType,
  CreateSavedListingRequest,
  Drivetrain,
  EnergyUnit,
  FieldProvenance,
  FuelType,
  ListingDraftInput,
  ReviewedListingInput,
  SavedListingResponse,
  SellerType,
  Transmission,
  ValidationProblemDetails,
} from "@/api/client";
import {
  listingResponseToDraft,
  manualProvenance,
  savedListingMetadata,
  savedListingToContext,
  scalarFieldNames,
  type CollectionDraft,
  type EnergyConsumptionDraft,
  type ListingReviewContext,
  type ListingReviewDraft,
  type OpenedSavedListing,
  type ScalarDraftField,
  type ScalarFieldName,
  type StringCollectionEntry,
} from "./review-model";
import { normalizeScalarInput, parseLocalizedNumber, validateReviewDraft } from "./validation";

export const collectionFieldNames = [
  "fuelTypes",
  "energyConsumptions",
  "equipment",
  "sellerClaims",
  "conditionNotes",
] as const;

export type CollectionFieldName = (typeof collectionFieldNames)[number];
export type ComparisonFieldName = Exclude<ScalarFieldName, "registrationNumber"> | CollectionFieldName;
export type ComparisonChoice = "existing" | "candidate";

export interface SavedListingReviewState {
  submittedUrl: string;
  normalizedUrl: string;
  phase: SavedListingResponse["status"];
  context: ListingReviewContext;
  draft: ListingReviewDraft;
  saved: OpenedSavedListing;
}

export interface SavedListingRequestBuildResult {
  registrationNumber?: string;
  request?: CreateSavedListingRequest;
  errors: Record<string, string>;
}

export interface ListingComparisonDifference {
  key: ComparisonFieldName;
  label: string;
  existingValue: string;
  candidateValue: string;
}

const integerFields = new Set<ScalarFieldName>([
  "modelYear",
  "imageCount",
  "horsepower",
  "ownerCount",
]);
const decimalFields = new Set<ScalarFieldName>([
  "priceSek",
  "odometerKilometres",
  "engineDisplacementCubicCentimetres",
  "annualVehicleTaxSek",
]);
const fieldLabels: Record<ComparisonFieldName, string> = {
  make: "Märke",
  model: "Modell",
  variant: "Variant",
  modelYear: "Modellår",
  vin: "Chassinummer",
  vehicleLabel: "Eget namn",
  priceSek: "Annonspris",
  odometerKilometres: "Mätarställning",
  sellerType: "Säljartyp",
  locality: "Ort eller stad",
  county: "Län",
  publishedDate: "Publicerad",
  updatedDate: "Uppdaterad",
  imageCount: "Antal bilder",
  transmission: "Växellåda",
  drivetrain: "Drivning",
  bodyType: "Karosstyp",
  colour: "Färg",
  horsepower: "Effekt",
  engineDisplacementCubicCentimetres: "Slagvolym",
  annualVehicleTaxSek: "Årlig fordonsskatt",
  ownerCount: "Antal ägare",
  firstRegistrationDate: "Första registrering",
  lastInspectionDate: "Senaste besiktning",
  nextInspectionDate: "Nästa besiktning",
  towBar: "Dragkrok",
  fuelTypes: "Bränsletyper",
  energyConsumptions: "Energiförbrukning",
  equipment: "Utrustning",
  sellerClaims: "Säljarens påståenden",
  conditionNotes: "Skicknoteringar",
};

export function createManualReviewContext(now = new Date()): ListingReviewContext {
  return {
    analyzedAtUtc: now.toISOString(),
    requestedModel: null,
    promptVersion: null,
    schemaVersion: null,
    sources: [],
  };
}

export function savedListingToReviewState(saved: SavedListingResponse): SavedListingReviewState {
  return {
    submittedUrl: saved.submittedUrl,
    normalizedUrl: saved.normalizedUrl,
    phase: saved.status,
    context: savedListingToContext(saved),
    draft: listingResponseToDraft(saved.listing),
    saved: savedListingMetadata(saved),
  };
}

export function buildSavedListingRequest(
  submittedUrl: string,
  normalizedUrl: string,
  context: ListingReviewContext,
  draft: ListingReviewDraft,
): SavedListingRequestBuildResult {
  const errors = validateReviewDraft(draft);
  const registrationNumber = normalizeRegistrationNumber(draft.fields.registrationNumber.input);
  if (!registrationNumber) {
    errors.registrationNumber = "Ange registreringsnummer för att spara bilen.";
  }

  if (Object.keys(errors).length > 0) return { errors };

  const listing: ReviewedListingInput = {
    submittedUrl,
    analyzedAtUtc: context.analyzedAtUtc,
    requestedModel: context.requestedModel,
    promptVersion: context.promptVersion,
    schemaVersion: context.schemaVersion,
    sources: context.sources.map((source) => source.url),
    draft: draftToInput(draft, normalizedUrl),
  };

  return {
    registrationNumber,
    request: { registrationNumber, listing },
    errors: {},
  };
}

export function normalizeRegistrationNumber(value: string) {
  return value.replace(/[\s-]/g, "").toUpperCase();
}

export function savedListingValidationErrors(
  problem: ValidationProblemDetails,
): Record<string, string> {
  const mapped: Record<string, string> = {};
  for (const [serverPath, messages] of Object.entries(problem.errors ?? {})) {
    const path = mapSavedListingValidationPath(serverPath);
    mapped[path] = messages.length > 0
      ? translateValidationMessage(path, messages[0])
      : "Servern kunde inte godkänna värdet.";
  }
  return mapped;
}

export function mapSavedListingValidationPath(path: string) {
  if (path === "registrationNumber") return "registrationNumber";
  if (path === "listing.submittedUrl") return "submittedUrl";
  if (path.startsWith("listing.sources[")) return "sources";
  if (path.startsWith("listing.draft.")) {
    return path
      .slice("listing.draft.".length)
      .replace(/\.value$/, "")
      .replace(/\.provenance(?:\..+)?$/, "");
  }
  if (path.startsWith("listing.")) return "submittedUrl";
  return path;
}

export function compareListingDrafts(
  existing: ListingReviewDraft,
  candidate: ListingReviewDraft,
): ListingComparisonDifference[] {
  const differences: ListingComparisonDifference[] = [];
  for (const key of scalarFieldNames) {
    if (key === "registrationNumber") continue;
    if (scalarComparable(existing.fields[key], key) !== scalarComparable(candidate.fields[key], key)) {
      differences.push({
        key,
        label: fieldLabels[key],
        existingValue: scalarDisplay(existing.fields[key], key),
        candidateValue: scalarDisplay(candidate.fields[key], key),
      });
    }
  }
  for (const key of collectionFieldNames) {
    if (collectionComparable(existing[key], key) !== collectionComparable(candidate[key], key)) {
      differences.push({
        key,
        label: fieldLabels[key],
        existingValue: collectionDisplay(existing[key], key),
        candidateValue: collectionDisplay(candidate[key], key),
      });
    }
  }
  return differences;
}

export function allComparisonChoicesSelected(
  differences: readonly ListingComparisonDifference[],
  choices: Partial<Record<ComparisonFieldName, ComparisonChoice>>,
) {
  return differences.every((difference) => choices[difference.key] !== undefined);
}

export function mergeListingComparison(
  existing: ListingReviewDraft,
  candidate: ListingReviewDraft,
  normalizedUrl: string,
  differences: readonly ListingComparisonDifference[],
  choices: Partial<Record<ComparisonFieldName, ComparisonChoice>>,
): ListingReviewDraft {
  if (!allComparisonChoicesSelected(differences, choices)) {
    throw new Error("Every listing difference must have an explicit choice.");
  }

  const merged = cloneDraft(candidate);
  for (const difference of differences) {
    if (choices[difference.key] !== "existing") continue;
    if (isCollectionField(difference.key)) {
      assignExistingCollection(merged, existing, difference.key, normalizedUrl);
    } else {
      const old = existing.fields[difference.key];
      merged.fields[difference.key] = old.input === ""
        ? { input: "", provenance: null }
        : { input: old.input, provenance: manualProvenance(normalizedUrl) };
    }
  }
  return merged;
}

function draftToInput(draft: ListingReviewDraft, normalizedUrl: string): ListingDraftInput {
  return {
    registrationNumber: scalarInput(draft.fields.registrationNumber, normalizedUrl, normalizeRegistrationNumber),
    make: scalarInput(draft.fields.make, normalizedUrl, normalizeText),
    model: scalarInput(draft.fields.model, normalizedUrl, normalizeText),
    variant: scalarInput(draft.fields.variant, normalizedUrl, normalizeText),
    modelYear: scalarInput(draft.fields.modelYear, normalizedUrl, parseNumber),
    vin: scalarInput(draft.fields.vin, normalizedUrl, (value) => normalizeText(value).toUpperCase()),
    vehicleLabel: scalarInput(draft.fields.vehicleLabel, normalizedUrl, normalizeText),
    priceSek: scalarInput(draft.fields.priceSek, normalizedUrl, parseNumber),
    odometerKilometres: scalarInput(
      draft.fields.odometerKilometres,
      normalizedUrl,
      (value) => parseNumber(value) * 10,
    ),
    sellerType: scalarInput(draft.fields.sellerType, normalizedUrl, (value) => value as SellerType),
    locality: scalarInput(draft.fields.locality, normalizedUrl, normalizeText),
    county: scalarInput(draft.fields.county, normalizedUrl, normalizeText),
    publishedDate: scalarInput(draft.fields.publishedDate, normalizedUrl, normalizeText),
    updatedDate: scalarInput(draft.fields.updatedDate, normalizedUrl, normalizeText),
    imageCount: scalarInput(draft.fields.imageCount, normalizedUrl, parseNumber),
    fuelTypes: collectionInput(draft.fuelTypes, normalizedUrl, (value) => value as FuelType),
    transmission: scalarInput(draft.fields.transmission, normalizedUrl, (value) => value as Transmission),
    drivetrain: scalarInput(draft.fields.drivetrain, normalizedUrl, (value) => value as Drivetrain),
    bodyType: scalarInput(draft.fields.bodyType, normalizedUrl, (value) => value as BodyType),
    colour: scalarInput(draft.fields.colour, normalizedUrl, normalizeText),
    horsepower: scalarInput(draft.fields.horsepower, normalizedUrl, parseNumber),
    engineDisplacementCubicCentimetres: scalarInput(
      draft.fields.engineDisplacementCubicCentimetres,
      normalizedUrl,
      parseNumber,
    ),
    energyConsumptions: collectionInput(
      draft.energyConsumptions,
      normalizedUrl,
      (value) => ({
        label: normalizeText(value.label),
        unit: value.unit as EnergyUnit,
        consumptionPer100Kilometres: parseNumber(value.consumptionPer100Kilometres),
      }),
    ),
    annualVehicleTaxSek: scalarInput(draft.fields.annualVehicleTaxSek, normalizedUrl, parseNumber),
    ownerCount: scalarInput(draft.fields.ownerCount, normalizedUrl, parseNumber),
    firstRegistrationDate: scalarInput(draft.fields.firstRegistrationDate, normalizedUrl, normalizeText),
    lastInspectionDate: scalarInput(draft.fields.lastInspectionDate, normalizedUrl, normalizeText),
    nextInspectionDate: scalarInput(draft.fields.nextInspectionDate, normalizedUrl, normalizeText),
    towBar: scalarInput(draft.fields.towBar, normalizedUrl, (value) => value === "true"),
    equipment: collectionInput(draft.equipment, normalizedUrl, (value) => normalizeText(value.value)),
    sellerClaims: collectionInput(draft.sellerClaims, normalizedUrl, (value) => normalizeText(value.value)),
    conditionNotes: collectionInput(draft.conditionNotes, normalizedUrl, (value) => normalizeText(value.value)),
  };
}

function scalarInput<T>(
  field: ScalarDraftField,
  normalizedUrl: string,
  convert: (value: string) => T,
): { value: T; provenance: FieldProvenance } | null {
  if (field.input === "") return null;
  return {
    value: convert(normalizeScalarInputValue(field.input)),
    provenance: field.provenance ? { ...field.provenance } : manualProvenance(normalizedUrl),
  };
}

function collectionInput<TSource, TTarget>(
  collection: CollectionDraft<TSource>,
  normalizedUrl: string,
  convert: (value: TSource) => TTarget,
): { values: TTarget[]; provenance: FieldProvenance } | null {
  if (collection.mode === "unknown") return null;
  return {
    values: collection.mode === "empty" ? [] : collection.values.map(convert),
    provenance: collection.provenance
      ? { ...collection.provenance }
      : manualProvenance(normalizedUrl),
  };
}

function normalizeScalarInputValue(value: string) {
  return value.trim().normalize("NFC");
}

function normalizeText(value: string) {
  return value.trim().normalize("NFC");
}

function parseNumber(value: string) {
  return parseLocalizedNumber(value).value!;
}

function translateValidationMessage(path: string, message: string) {
  if (path === "registrationNumber") {
    return "Ange ett vanligt svenskt registreringsnummer, till exempel ABC123 eller ABC12D.";
  }
  if (path === "submittedUrl" || path === "sources") {
    return "Annonsens URL eller källor kunde inte godkännas.";
  }
  if (/required/i.test(message)) return "Värdet måste anges.";
  return "Servern kunde inte godkänna värdet.";
}

function scalarComparable(field: ScalarDraftField, name: ScalarFieldName) {
  if (field.input === "") return "null";
  if (integerFields.has(name) || decimalFields.has(name)) {
    const parsed = parseLocalizedNumber(field.input, integerFields.has(name));
    return parsed.error ? normalizeText(field.input) : String(parsed.value);
  }
  if (name === "registrationNumber") return normalizeRegistrationNumber(field.input);
  if (name === "vin") return normalizeText(field.input).toUpperCase();
  return normalizeText(field.input);
}

function scalarDisplay(field: ScalarDraftField, name: ScalarFieldName) {
  if (field.input === "") return "Okänt";
  if (name === "towBar") return field.input === "true" ? "Ja" : "Nej";
  return normalizeScalarInput(name, field.input);
}

function collectionComparable(
  collection: ListingReviewDraft[CollectionFieldName],
  name: CollectionFieldName,
) {
  if (collection.mode === "unknown") return "null";
  if (collection.mode === "empty") return "[]";
  if (name === "energyConsumptions") {
    return JSON.stringify((collection as CollectionDraft<EnergyConsumptionDraft>).values.map((value) => ({
      label: normalizeText(value.label),
      unit: value.unit,
      consumption: parseLocalizedNumber(value.consumptionPer100Kilometres).value,
    })));
  }
  if (name === "fuelTypes") return JSON.stringify(collection.values);
  return JSON.stringify((collection as CollectionDraft<StringCollectionEntry>).values.map((value) => normalizeText(value.value)));
}

function collectionDisplay(
  collection: ListingReviewDraft[CollectionFieldName],
  name: CollectionFieldName,
) {
  if (collection.mode === "unknown") return "Okänt";
  if (collection.mode === "empty") return "Inga";
  if (name === "energyConsumptions") {
    return (collection as CollectionDraft<EnergyConsumptionDraft>).values
      .map((value) => `${value.label}: ${value.consumptionPer100Kilometres} ${value.unit}/100 km`)
      .join(", ");
  }
  if (name === "fuelTypes") return (collection as CollectionDraft<string>).values.join(", ");
  return (collection as CollectionDraft<StringCollectionEntry>).values.map((value) => value.value).join(", ");
}

function cloneDraft(draft: ListingReviewDraft): ListingReviewDraft {
  return {
    fields: Object.fromEntries(
      scalarFieldNames.map((name) => [name, {
        input: draft.fields[name].input,
        provenance: draft.fields[name].provenance ? { ...draft.fields[name].provenance } : null,
      }]),
    ) as ListingReviewDraft["fields"],
    fuelTypes: cloneCollection(draft.fuelTypes),
    energyConsumptions: cloneCollection(draft.energyConsumptions),
    equipment: cloneCollection(draft.equipment),
    sellerClaims: cloneCollection(draft.sellerClaims),
    conditionNotes: cloneCollection(draft.conditionNotes),
  };
}

function cloneCollection<T>(collection: CollectionDraft<T>): CollectionDraft<T> {
  return {
    mode: collection.mode,
    values: collection.values.map((value) => typeof value === "object" && value !== null
      ? { ...value }
      : value),
    provenance: collection.provenance ? { ...collection.provenance } : null,
  };
}

function isCollectionField(value: ComparisonFieldName): value is CollectionFieldName {
  return (collectionFieldNames as readonly string[]).includes(value);
}

function assignExistingCollection(
  merged: ListingReviewDraft,
  existing: ListingReviewDraft,
  key: CollectionFieldName,
  normalizedUrl: string,
) {
  if (key === "fuelTypes") merged.fuelTypes = manualCollection(existing.fuelTypes, normalizedUrl);
  else if (key === "energyConsumptions") merged.energyConsumptions = manualCollection(existing.energyConsumptions, normalizedUrl);
  else if (key === "equipment") merged.equipment = manualCollection(existing.equipment, normalizedUrl);
  else if (key === "sellerClaims") merged.sellerClaims = manualCollection(existing.sellerClaims, normalizedUrl);
  else merged.conditionNotes = manualCollection(existing.conditionNotes, normalizedUrl);
}

function manualCollection<T>(collection: CollectionDraft<T>, normalizedUrl: string) {
  const replacement = cloneCollection(collection);
  replacement.provenance = replacement.mode === "unknown" ? null : manualProvenance(normalizedUrl);
  return replacement;
}
