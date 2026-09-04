import { describe, expect, it } from "vitest";
import type { ManualCalculationRequest, SavedCostScenarioResponse } from "@/api/client";
import { completeManualCalculationResult } from "@/test/manual-calculation-result";
import { validateManualCalculationForm } from "./validation";
import {
  normalizeRegistrationNumber,
  savedScenarioMetadata,
  savedScenarioToForm,
  validateRegistrationNumber,
} from "./saved-scenarios";

describe("saved scenario form mapping", () => {
  it("normalizes and validates the same ordinary registration formats as Core", () => {
    expect(normalizeRegistrationNumber(" ab-c 12d ")).toBe("ABC12D");
    expect(validateRegistrationNumber("abc 123")).toEqual({ normalized: "ABC123" });
    expect(validateRegistrationNumber("abc-12d")).toEqual({ normalized: "ABC12D" });
    expect(validateRegistrationNumber("").error).toMatch(/Ange registreringsnummer/);
    expect(validateRegistrationNumber("ABC12O").error).toMatch(/vanligt svenskt/);
    expect(validateRegistrationNumber("III123").error).toMatch(/vanligt svenskt/);
    expect(validateRegistrationNumber("ABC 1234").error).toMatch(/vanligt svenskt/);
  });

  it("maps kilometres to mil and preserves null, zero, order, and collection identities", () => {
    const saved = createSavedResponse({
      vehicleLabel: "Volvo V70",
      calculationPeriodMonths: 18,
      purchasePriceSek: 20_000.5,
      expectedResidualValueSek: null,
      annualDistanceKilometres: 15_005,
      financing: {
        downPaymentSek: 0,
        annualNominalInterestRatePercent: 0,
        termMonths: 60,
      },
      energySources: [
        {
          label: "Bensin",
          unit: "litre",
          consumptionPer100Kilometres: 8,
          pricePerUnitSek: 20,
          distanceSharePercent: 60,
        },
        {
          label: "El",
          unit: "kilowattHour",
          consumptionPer100Kilometres: 18.5,
          pricePerUnitSek: 2.25,
          distanceSharePercent: 40,
        },
      ],
      vehicleTax: { amountSek: 0, cadence: "annual" },
      insurance: null,
      maintenanceAndRepairs: { amountSek: 500, cadence: "monthly" },
      otherRecurringCosts: [
        { label: "Parkering", amountSek: 300, cadence: "monthly" },
        { label: "Besiktning", amountSek: 600, cadence: "annual" },
      ],
      otherOneTimeCosts: [
        { label: "Däck", amountSek: 2_000 },
        { label: "Leverans", amountSek: 0 },
      ],
    });

    const form = savedScenarioToForm(saved);

    expect(form.annualDistanceMil).toBe("1500.5");
    expect(form.residualValueKnown).toBe(false);
    expect(form.expectedResidualValueSek).toBe("");
    expect(form.vehicleTax).toEqual({ isKnown: true, amountSek: "0", cadence: "annual" });
    expect(form.insurance.isKnown).toBe(false);
    expect(form.energySources.map((source) => source.label)).toEqual(["Bensin", "El"]);
    expect(new Set(form.energySources.map((source) => source.id)).size).toBe(2);
    expect(form.otherRecurringCosts.map((cost) => cost.label)).toEqual(["Parkering", "Besiktning"]);
    expect(form.otherOneTimeCosts.map((cost) => cost.amountSek)).toEqual(["2000", "0"]);
    expect(validateManualCalculationForm(form).request).toEqual(saved.scenario);
  });

  it("extracts persisted identity, concurrency, and listing-link metadata", () => {
    const saved = createSavedResponse(baseScenario());

    expect(savedScenarioMetadata(saved)).toEqual({
      vehicleId: saved.vehicleId,
      registrationNumber: "ABC123",
      revision: 3,
      sourceListingVersion: null,
      currentListingVersion: null,
      isListingOutdated: false,
      hasSavedListing: false,
      createdAtUtc: saved.createdAtUtc,
      updatedAtUtc: saved.updatedAtUtc,
    });
  });
});

function baseScenario(): ManualCalculationRequest {
  return {
    vehicleLabel: null,
    calculationPeriodMonths: 12,
    purchasePriceSek: 20_000,
    expectedResidualValueSek: null,
    annualDistanceKilometres: 0,
    financing: null,
    energySources: [],
    vehicleTax: null,
    insurance: null,
    maintenanceAndRepairs: null,
    otherRecurringCosts: [],
    otherOneTimeCosts: [],
  };
}

function createSavedResponse(scenario: ManualCalculationRequest): SavedCostScenarioResponse {
  return {
    vehicleId: "0194f7a8-5c33-7f43-b516-d5c2f94dcd31",
    registrationNumber: "ABC123",
    revision: 3,
    calculationVersion: 1,
    resultSchemaVersion: 1,
    sourceListingVersion: null,
    currentListingVersion: null,
    isListingOutdated: false,
    hasSavedListing: false,
    createdAtUtc: "2026-08-30T08:00:00Z",
    updatedAtUtc: "2026-08-30T09:00:00Z",
    calculatedAtUtc: "2026-08-30T09:00:00Z",
    scenario,
    result: completeManualCalculationResult,
  };
}
