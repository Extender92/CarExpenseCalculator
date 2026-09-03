import type {
  FieldProvenance,
  ListingAnalysisResponse,
  ListingDraftResponse,
} from "@/api/client";

const sourceUrl = "https://cars.example/item/1";
const provenance: FieldProvenance = {
  origin: "listing",
  extractionMethod: "ai",
  verification: "unverified",
  sourceUrl,
};

const value = <T,>(item: T) => ({ value: item, provenance });
const collection = <T,>(values: T[]) => ({ values, provenance });

export const completeListingDraft: ListingDraftResponse = {
  registrationNumber: value("ABC123"),
  make: value("Volvo"),
  model: value("V70"),
  variant: value("2.4"),
  modelYear: value(2008),
  vin: value("YV1TEST123"),
  vehicleLabel: null,
  priceSek: value(20_000),
  odometerKilometres: value(167_100),
  sellerType: value("private"),
  location: value("Tenhult"),
  publishedDate: value("2026-08-20"),
  updatedDate: value("2026-08-27"),
  imageCount: value(8),
  fuelTypes: collection(["petrol"]),
  transmission: value("manual"),
  drivetrain: value("frontWheelDrive"),
  bodyType: value("wagon"),
  colour: value("Röd"),
  horsepower: value(140),
  engineDisplacementCubicCentimetres: value(2_435),
  energyConsumptions: collection([
    { label: "Bensin", unit: "litre", consumptionPer100Kilometres: 8 },
  ]),
  annualVehicleTaxSek: value(2_400),
  ownerCount: value(4),
  firstRegistrationDate: value("2008-01-15"),
  lastInspectionDate: value("2026-08-24"),
  nextInspectionDate: value("2027-10-31"),
  towBar: value(true),
  equipment: collection(["AC", "Isofix"]),
  sellerClaims: collection(["Motor och växellåda fungerar bra"]),
  conditionNotes: collection([]),
};

export const completeListingAnalysisResponse: ListingAnalysisResponse = {
  submittedUrl: sourceUrl,
  normalizedUrl: sourceUrl,
  status: "complete",
  analyzedAtUtc: "2026-09-03T08:00:00Z",
  requestedModel: "gpt-5.6-luna",
  promptVersion: 1,
  schemaVersion: 1,
  sources: [
    { url: sourceUrl, matchesSubmittedUrl: true },
    { url: "https://manufacturer.example/model", matchesSubmittedUrl: false },
  ],
  listing: completeListingDraft,
  missingFields: [],
};
