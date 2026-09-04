import { describe, expect, it } from "vitest";
import { completeListingAnalysisResponse } from "@/test/listing-analysis";
import {
  analysisResponseToDraft,
  createEmptyReviewDraft,
  createStringEntry,
  deriveMissingFields,
  editCollection,
  editScalarField,
} from "./review-model";

describe("listing review mapping", () => {
  it("maps every API field, order, zero/false, known-empty, and kilometres to mil", () => {
    const response = structuredClone(completeListingAnalysisResponse);
    response.listing.priceSek!.value = 0;
    response.listing.towBar!.value = false;
    const draft = analysisResponseToDraft(response);

    expect(draft.fields.registrationNumber.input).toBe("ABC123");
    expect(draft.fields.priceSek.input).toBe("0");
    expect(draft.fields.odometerKilometres.input).toBe("16710");
    expect(draft.fields.towBar.input).toBe("false");
    expect(draft.fields.locality.input).toBe("Tenhult");
    expect(draft.fields.county.input).toBe("Jönköpings län");
    expect(draft.fuelTypes.values).toEqual(["petrol"]);
    expect(draft.equipment.values.map((entry) => entry.value)).toEqual(["AC", "Isofix"]);
    expect(draft.conditionNotes).toMatchObject({ mode: "empty", values: [] });
    expect(draft.energyConsumptions.values[0]).toMatchObject({
      label: "Bensin",
      unit: "litre",
      consumptionPer100Kilometres: "8",
    });
    expect(deriveMissingFields(draft)).toEqual([]);
  });

  it("keeps all 31 missing codes in stable order for an empty draft", () => {
    const missing = deriveMissingFields(createEmptyReviewDraft());
    expect(missing).toHaveLength(31);
    expect(missing.slice(0, 4)).toEqual(["registrationNumber", "make", "model", "variant"]);
    expect(missing.slice(8, 12)).toEqual(["sellerType", "locality", "county", "publishedDate"]);
    expect(missing.slice(-4)).toEqual(["towBar", "equipment", "sellerClaims", "conditionNotes"]);
  });

  it("changes only the edited complete value to manual provenance", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    const changed = editScalarField(draft, "make", "Saab", completeListingAnalysisResponse.normalizedUrl);

    expect(changed.fields.make.provenance).toEqual({
      origin: "user",
      extractionMethod: "manual",
      verification: "userConfirmed",
      sourceUrl: completeListingAnalysisResponse.normalizedUrl,
    });
    expect(changed.fields.model.provenance).toEqual(draft.fields.model.provenance);
    expect(editScalarField(changed, "make", "", completeListingAnalysisResponse.normalizedUrl).fields.make.provenance).toBeNull();
  });

  it("tracks locality and county values and provenance independently", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    const changed = editScalarField(
      draft,
      "county",
      "Östergötlands län",
      completeListingAnalysisResponse.normalizedUrl,
    );

    expect(changed.fields.locality).toEqual(draft.fields.locality);
    expect(changed.fields.county.input).toBe("Östergötlands län");
    expect(changed.fields.county.provenance?.origin).toBe("user");
    expect(deriveMissingFields(editScalarField(changed, "locality", "", changed.fields.county.provenance!.sourceUrl)))
      .toContain("locality");
  });

  it("distinguishes unknown, known-empty, and manually entered collections", () => {
    const draft = createEmptyReviewDraft();
    const knownEmpty = editCollection(draft.equipment, "empty", [], completeListingAnalysisResponse.normalizedUrl);
    const populated = editCollection(knownEmpty, "values", [createStringEntry("AC")], completeListingAnalysisResponse.normalizedUrl);

    expect(draft.equipment.provenance).toBeNull();
    expect(knownEmpty).toMatchObject({ mode: "empty", values: [] });
    expect(populated.values[0].value).toBe("AC");
    expect(populated.provenance?.origin).toBe("user");
  });
});
