import { describe, expect, it } from "vitest";
import { completeListingAnalysisResponse } from "@/test/listing-analysis";
import {
  analysisResponseToDraft,
  createEnergyEntry,
  createStringEntry,
} from "./review-model";
import { normalizeScalarInput, parseLocalizedNumber, validateReviewDraft } from "./validation";

describe("listing review validation", () => {
  it("accepts comma or point decimals and rejects grouping, units, exponents, and malformed values", () => {
    expect(parseLocalizedNumber("12,5").value).toBe(12.5);
    expect(parseLocalizedNumber("12.5").value).toBe(12.5);
    for (const invalid of ["1 000", "1,000.5", "12 kr", "1e3", "-1", ".5"]) {
      expect(parseLocalizedNumber(invalid).error).toBeDefined();
    }
    expect(parseLocalizedNumber("12.5", true).error).toBeDefined();
  });

  it("normalizes labels, registration numbers, and VIN", () => {
    expect(normalizeScalarInput("make", "  Rå Saab  ")).toBe("Rå Saab");
    expect(normalizeScalarInput("registrationNumber", " abc-12d ")).toBe("ABC12D");
    expect(normalizeScalarInput("vin", " yv1 test ")).toBe("YV1 TEST");
  });

  it("accepts a complete extracted draft", () => {
    expect(validateReviewDraft(analysisResponseToDraft(completeListingAnalysisResponse))).toEqual({});
  });

  it("accepts every supported listing enum and rejects unsupported values", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    const scalarValues = {
      sellerType: ["private", "dealer"],
      transmission: ["manual", "automatic"],
      drivetrain: ["frontWheelDrive", "rearWheelDrive", "allWheelDrive"],
      bodyType: ["sedan", "hatchback", "wagon", "suv", "coupe", "convertible", "minivan", "pickup", "van", "other"],
      towBar: ["true", "false"],
    } as const;

    for (const [field, values] of Object.entries(scalarValues)) {
      for (const value of values) {
        draft.fields[field as keyof typeof scalarValues].input = value;
        expect(validateReviewDraft(draft)[field]).toBeUndefined();
      }
      draft.fields[field as keyof typeof scalarValues].input = "unsupported";
      expect(validateReviewDraft(draft)[field]).toBeDefined();
      draft.fields[field as keyof typeof scalarValues].input = values[0];
    }

    for (const fuel of ["petrol", "diesel", "electricity", "ethanol", "biogas", "naturalGas", "liquefiedPetroleumGas", "hydrogen", "other"]) {
      draft.fuelTypes.values = [fuel];
      expect(validateReviewDraft(draft).fuelTypes).toBeUndefined();
    }
    for (const unit of ["litre", "kilowattHour", "kilogram"] as const) {
      draft.energyConsumptions.values[0].unit = unit;
      expect(validateReviewDraft(draft)["energyConsumptions.values[0].unit"]).toBeUndefined();
    }
  });

  it("validates every numeric boundary and ordinary registration format", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    draft.fields.registrationNumber.input = "ABC12O";
    draft.fields.modelYear.input = "1885";
    draft.fields.priceSek.input = "100000001";
    draft.fields.odometerKilometres.input = "1000000,1";
    draft.fields.horsepower.input = "0";
    draft.fields.engineDisplacementCubicCentimetres.input = "100001";
    draft.fields.ownerCount.input = "10001";
    draft.fields.imageCount.input = "1,5";

    expect(validateReviewDraft(draft)).toEqual(expect.objectContaining({
      registrationNumber: expect.any(String),
      modelYear: expect.any(String),
      priceSek: expect.any(String),
      odometerKilometres: expect.any(String),
      horsepower: expect.any(String),
      engineDisplacementCubicCentimetres: expect.any(String),
      ownerCount: expect.any(String),
      imageCount: expect.any(String),
    }));
  });

  it("validates collection limits, duplicates, empty rows, and energy entries", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    draft.equipment.mode = "values";
    draft.equipment.values = [createStringEntry("AC"), createStringEntry(" ac ")];
    draft.sellerClaims.values = Array.from({ length: 21 }, () => createStringEntry("Påstående"));
    draft.sellerClaims.mode = "values";
    draft.conditionNotes.mode = "values";
    draft.conditionNotes.values = [createStringEntry("")];
    draft.energyConsumptions.mode = "values";
    draft.energyConsumptions.values = [
      { ...createEnergyEntry(), label: "Bensin", consumptionPer100Kilometres: "0" },
      { ...createEnergyEntry(), label: " bensin ", consumptionPer100Kilometres: "8" },
      { ...createEnergyEntry(), label: "El", consumptionPer100Kilometres: "20" },
    ];

    const errors = validateReviewDraft(draft);
    expect(errors["equipment.values[1]"]).toContain("flera gånger");
    expect(errors.sellerClaims).toContain("20");
    expect(errors["conditionNotes.values[0]"]).toBeDefined();
    expect(errors.energyConsumptions).toContain("två");
    expect(errors["energyConsumptions.values[0].consumptionPer100Kilometres"]).toBeDefined();
    expect(errors["energyConsumptions.values[1].label"]).toContain("unik");
  });

  it("accepts documented extrema and rejects dates and text beyond their bounds", () => {
    const draft = analysisResponseToDraft(completeListingAnalysisResponse);
    draft.fields.modelYear.input = "2100";
    draft.fields.priceSek.input = "100000000";
    draft.fields.odometerKilometres.input = "1000000";
    draft.fields.imageCount.input = "10000";
    draft.fields.horsepower.input = "10000";
    draft.fields.engineDisplacementCubicCentimetres.input = "100000";
    draft.fields.annualVehicleTaxSek.input = "100000000";
    draft.fields.ownerCount.input = "10000";
    draft.energyConsumptions.values[0].consumptionPer100Kilometres = "10000";
    expect(validateReviewDraft(draft)).toEqual({});

    draft.fields.make.input = "a".repeat(101);
    draft.fields.vin.input = "V".repeat(51);
    draft.fields.publishedDate.input = "2026-02-30";
    draft.fields.annualVehicleTaxSek.input = "100000001";
    const errors = validateReviewDraft(draft);
    expect(errors.make).toBeDefined();
    expect(errors.vin).toBeDefined();
    expect(errors.publishedDate).toBeDefined();
    expect(errors.annualVehicleTaxSek).toBeDefined();
  });
});
