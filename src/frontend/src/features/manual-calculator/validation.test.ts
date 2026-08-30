import { describe, expect, it } from "vitest";
import {
  createEnergySource,
  createInitialManualCalculationForm,
  createNamedRecurringCost,
  createOneTimeCost,
} from "./form-model";
import {
  convertMilToKilometres,
  parseLocalizedDecimal,
  parseUnsignedInteger,
  validateManualCalculationForm,
} from "./validation";

describe("manual calculator form validation", () => {
  it("accepts Swedish and API decimal separators without accepting formatted or signed values", () => {
    expect(parseLocalizedDecimal(" 7,5 ")).toBe(7.5);
    expect(parseLocalizedDecimal("7.5")).toBe(7.5);
    expect(parseLocalizedDecimal("1 000")).toBeNull();
    expect(parseLocalizedDecimal("1,000.5")).toBeNull();
    expect(parseLocalizedDecimal("1e3")).toBeNull();
    expect(parseLocalizedDecimal("-1")).toBeNull();
    expect(parseUnsignedInteger("120")).toBe(120);
    expect(parseUnsignedInteger("12,0")).toBeNull();
    expect(convertMilToKilometres("1500,5")).toBe(15_005);
  });

  it("builds a cash request with explicit unknown standard costs", () => {
    const form = createInitialManualCalculationForm();
    form.vehicleLabel = "  Testbil  ";
    form.purchasePriceSek = "20000,50";
    form.annualDistanceMil = "0";

    const validation = validateManualCalculationForm(form);

    expect(validation.errors).toEqual({});
    expect(validation.request).toEqual({
      vehicleLabel: "Testbil",
      calculationPeriodMonths: 12,
      purchasePriceSek: 20_000.5,
      expectedResidualValueSek: null,
      annualDistanceKilometres: 0,
      financing: null,
      energySources: [],
      vehicleTax: null,
      insurance: null,
      maintenanceAndRepairs: null,
      otherRecurringCosts: [],
      otherOneTimeCosts: [],
    });
  });

  it("maps financing, two energy sources, known zero, and custom costs", () => {
    const form = createInitialManualCalculationForm();
    form.purchasePriceSek = "20000";
    form.annualDistanceMil = "1500";
    form.residualValueKnown = true;
    form.expectedResidualValueSek = "15000";
    form.financing = {
      enabled: true,
      downPaymentSek: "5000",
      annualNominalInterestRatePercent: "5,5",
      termMonths: "60",
    };
    const petrol = createEnergySource("60");
    Object.assign(petrol, {
      label: " Bensin ",
      unit: "litre",
      consumptionPer100Kilometres: "8",
      pricePerUnitSek: "20",
    });
    const electricity = createEnergySource("40");
    Object.assign(electricity, {
      label: "El",
      unit: "kilowattHour",
      consumptionPer100Kilometres: "18,5",
      pricePerUnitSek: "2,25",
    });
    form.energySources = [petrol, electricity];
    form.vehicleTax = { isKnown: true, amountSek: "0", cadence: "annual" };
    const recurring = createNamedRecurringCost();
    Object.assign(recurring, { label: " Parkering ", amountSek: "300", cadence: "monthly" });
    form.otherRecurringCosts = [recurring];
    const oneTime = createOneTimeCost();
    Object.assign(oneTime, { label: " Däck ", amountSek: "2000" });
    form.otherOneTimeCosts = [oneTime];

    const request = validateManualCalculationForm(form).request;

    expect(request).toMatchObject({
      expectedResidualValueSek: 15_000,
      annualDistanceKilometres: 15_000,
      financing: {
        downPaymentSek: 5_000,
        annualNominalInterestRatePercent: 5.5,
        termMonths: 60,
      },
      vehicleTax: { amountSek: 0, cadence: "annual" },
      insurance: null,
      energySources: [
        { label: "Bensin", unit: "litre", distanceSharePercent: 60 },
        { label: "El", unit: "kilowattHour", distanceSharePercent: 40 },
      ],
      otherRecurringCosts: [{ label: "Parkering", amountSek: 300, cadence: "monthly" }],
      otherOneTimeCosts: [{ label: "Däck", amountSek: 2_000 }],
    });
  });

  it("reports range and cross-field errors on stable API paths", () => {
    const form = createInitialManualCalculationForm();
    form.purchasePriceSek = "0";
    form.annualDistanceMil = "100001";
    form.residualValueKnown = true;
    form.expectedResidualValueSek = "1";
    form.financing.enabled = true;
    form.financing.downPaymentSek = "0";
    form.financing.annualNominalInterestRatePercent = "101";
    form.financing.termMonths = "0";
    const energy = createEnergySource("99,9");
    Object.assign(energy, {
      label: " ",
      unit: "litre",
      consumptionPer100Kilometres: "0",
      pricePerUnitSek: "100001",
    });
    form.energySources = [energy];

    const paths = Object.keys(validateManualCalculationForm(form).errors);

    expect(paths).toEqual(expect.arrayContaining([
      "annualDistanceKilometres",
      "expectedResidualValueSek",
      "financing",
      "financing.downPaymentSek",
      "financing.annualNominalInterestRatePercent",
      "financing.termMonths",
      "energySources",
      "energySources[0].label",
      "energySources[0].consumptionPer100Kilometres",
      "energySources[0].pricePerUnitSek",
    ]));
  });

  it("enforces both custom collection limits", () => {
    const form = createInitialManualCalculationForm();
    form.purchasePriceSek = "1";
    form.annualDistanceMil = "0";
    form.otherRecurringCosts = Array.from({ length: 51 }, createNamedRecurringCost);
    form.otherOneTimeCosts = Array.from({ length: 51 }, createOneTimeCost);

    const errors = validateManualCalculationForm(form).errors;

    expect(errors.otherRecurringCosts).toContain("Högst 50 återkommande kostnader får anges.");
    expect(errors.otherOneTimeCosts).toContain("Högst 50 engångskostnader får anges.");
  });
});
