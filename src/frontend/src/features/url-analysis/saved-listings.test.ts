import { describe, expect, it } from "vitest";
import { completeListingAnalysisResponse, savedListingResponse } from "@/test/listing-analysis";
import { analysisResponseToDraft, editCollection, editScalarField } from "./review-model";
import {
  allComparisonChoicesSelected,
  buildSavedListingRequest,
  compareListingDrafts,
  createManualReviewContext,
  mapSavedListingValidationPath,
  mergeListingComparison,
  normalizeRegistrationNumber,
  savedListingToReviewState,
  savedListingValidationErrors,
} from "./saved-listings";

describe("saved listing review mapping", () => {
  it("restores saved identity, context, provenance, ordered collections, and kilometres as mil", () => {
    const state = savedListingToReviewState(savedListingResponse);

    expect(state.saved).toMatchObject({
      vehicleId: savedListingResponse.vehicleId,
      registrationNumber: "ABC123",
      revision: 3,
      listingVersion: 2,
    });
    expect(state.draft.fields.odometerKilometres.input).toBe("16710");
    expect(state.draft.fields.towBar.input).toBe("true");
    expect(state.draft.energyConsumptions.values).toEqual([
      expect.objectContaining({ label: "Bensin", unit: "litre", consumptionPer100Kilometres: "8" }),
    ]);
    expect(state.draft.equipment.values.map((entry) => entry.value)).toEqual(["AC", "Isofix"]);
    expect(state.draft.conditionNotes).toMatchObject({ mode: "empty", values: [] });
    expect(state.context.sources.map((source) => source.url)).toEqual(
      savedListingResponse.sources.map((source) => source.url),
    );
  });

  it("builds the complete reviewed input with normalized registration and exact mil conversion", () => {
    let draft = analysisResponseToDraft(completeListingAnalysisResponse);
    draft = editScalarField(draft, "registrationNumber", "abc-12d", completeListingAnalysisResponse.normalizedUrl);
    draft = editScalarField(draft, "odometerKilometres", "12,345", completeListingAnalysisResponse.normalizedUrl);
    draft = editScalarField(draft, "annualVehicleTaxSek", "0", completeListingAnalysisResponse.normalizedUrl);
    draft = editScalarField(draft, "towBar", "false", completeListingAnalysisResponse.normalizedUrl);
    draft = {
      ...draft,
      conditionNotes: editCollection(draft.conditionNotes, "empty", [], completeListingAnalysisResponse.normalizedUrl),
    };

    const built = buildSavedListingRequest(
      completeListingAnalysisResponse.submittedUrl,
      completeListingAnalysisResponse.normalizedUrl,
      {
        analyzedAtUtc: completeListingAnalysisResponse.analyzedAtUtc,
        requestedModel: completeListingAnalysisResponse.requestedModel,
        promptVersion: 2,
        schemaVersion: 2,
        sources: completeListingAnalysisResponse.sources,
      },
      draft,
    );

    expect(built.errors).toEqual({});
    expect(built.registrationNumber).toBe("ABC12D");
    expect(built.request?.listing).toMatchObject({
      analyzedAtUtc: completeListingAnalysisResponse.analyzedAtUtc,
      requestedModel: "gpt-5.6-luna",
      promptVersion: 2,
      schemaVersion: 2,
      sources: completeListingAnalysisResponse.sources.map((source) => source.url),
      draft: {
        registrationNumber: { value: "ABC12D" },
        odometerKilometres: { value: 123.45 },
        annualVehicleTaxSek: { value: 0 },
        towBar: { value: false },
        conditionNotes: { values: [] },
      },
    });
  });

  it("keeps manual-only metadata nullable with a stable creation timestamp", () => {
    const now = new Date("2026-09-04T10:11:12Z");
    expect(createManualReviewContext(now)).toEqual({
      analyzedAtUtc: "2026-09-04T10:11:12.000Z",
      requestedModel: null,
      promptVersion: null,
      schemaVersion: null,
      sources: [],
    });
  });

  it("requires a valid registration only for saving", () => {
    const draft = analysisResponseToDraft({
      ...completeListingAnalysisResponse,
      listing: { ...completeListingAnalysisResponse.listing, registrationNumber: null },
    });
    const built = buildSavedListingRequest(
      completeListingAnalysisResponse.submittedUrl,
      completeListingAnalysisResponse.normalizedUrl,
      createManualReviewContext(),
      draft,
    );

    expect(built.request).toBeUndefined();
    expect(built.errors.registrationNumber).toMatch(/registreringsnummer/i);
    expect(normalizeRegistrationNumber(" abc-123 ")).toBe("ABC123");
  });
});

describe("saved listing comparisons", () => {
  it("compares scalars individually, collections as whole values, and excludes registration", () => {
    const existing = savedListingToReviewState(savedListingResponse).draft;
    let candidate = analysisResponseToDraft(completeListingAnalysisResponse);
    candidate = editScalarField(candidate, "registrationNumber", "XYZ987", completeListingAnalysisResponse.normalizedUrl);
    candidate = editScalarField(candidate, "make", "Saab", completeListingAnalysisResponse.normalizedUrl);
    candidate = {
      ...candidate,
      equipment: editCollection(
        candidate.equipment,
        "values",
        candidate.equipment.values.slice(0, 1),
        completeListingAnalysisResponse.normalizedUrl,
      ),
    };

    const differences = compareListingDrafts(existing, candidate);
    expect(differences.map((difference) => difference.key)).toEqual(["make", "equipment"]);
    expect(allComparisonChoicesSelected(differences, {})).toBe(false);
    expect(allComparisonChoicesSelected(differences, { make: "existing", equipment: "candidate" })).toBe(true);
  });

  it("marks retained old values as manual and user-confirmed against the new URL", () => {
    const existing = savedListingToReviewState(savedListingResponse).draft;
    let candidate = analysisResponseToDraft(completeListingAnalysisResponse);
    candidate = editScalarField(candidate, "make", "Saab", "https://cars.example/item/new");
    const differences = compareListingDrafts(existing, candidate);

    const merged = mergeListingComparison(
      existing,
      candidate,
      "https://cars.example/item/new",
      differences,
      { make: "existing" },
    );

    expect(merged.fields.make).toEqual({
      input: "Volvo",
      provenance: {
        origin: "user",
        extractionMethod: "manual",
        verification: "userConfirmed",
        sourceUrl: "https://cars.example/item/new",
      },
    });
  });
});

describe("saved listing server validation paths", () => {
  it.each([
    ["registrationNumber", "registrationNumber"],
    ["listing.submittedUrl", "submittedUrl"],
    ["listing.sources[0]", "sources"],
    ["listing.draft.make.value", "make"],
    ["listing.draft.energyConsumptions.values[0].label", "energyConsumptions.values[0].label"],
  ])("maps %s to %s", (serverPath, expected) => {
    expect(mapSavedListingValidationPath(serverPath)).toBe(expected);
  });

  it("retains all grouped server errors using review control paths", () => {
    expect(savedListingValidationErrors({
      title: "Validation failed",
      errors: {
        registrationNumber: ["Invalid registration."],
        "listing.draft.make.value": ["Invalid make."],
      },
    })).toEqual({
      registrationNumber: expect.stringMatching(/registreringsnummer/i),
      make: "Servern kunde inte godkänna värdet.",
    });
  });
});
