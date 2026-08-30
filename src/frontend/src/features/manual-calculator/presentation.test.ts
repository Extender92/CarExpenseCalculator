import { describe, expect, it } from "vitest";
import { fieldLabel, savedValidationProblemToErrors } from "./presentation";

describe("saved scenario validation presentation", () => {
  it("maps saved API scenario paths back to calculator controls", () => {
    const errors = savedValidationProblemToErrors({
      status: 400,
      errors: {
        registrationNumber: ["Registration number is invalid."],
        "scenario.purchasePriceSek": ["Value must be at least 0 and at most 100000000."],
        "scenario.energySources[0].label": ["Label must contain at least one non-whitespace character."],
      },
    });

    expect(errors).toEqual({
      registrationNumber: ["Servern godkände inte värdet. Kontrollera det och försök igen."],
      purchasePriceSek: ["Värdet ligger utanför det tillåtna intervallet."],
      "energySources[0].label": ["Ange ett namn."],
    });
    expect(fieldLabel("registrationNumber")).toBe("Registreringsnummer");
  });
});
