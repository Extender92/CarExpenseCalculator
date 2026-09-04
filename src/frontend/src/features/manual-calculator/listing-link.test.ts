import { describe, expect, it } from "vitest";
import { savedListingResponse } from "@/test/listing-analysis";
import { createEnergySource, createInitialManualCalculationForm } from "./form-model";
import {
  applyListingEnergyConsumption,
  applyListingPrice,
  applyListingTax,
  listingToManualCalculationForm,
} from "./listing-link";

describe("listing to manual calculator mapping", () => {
  it("prefills only the explicitly safe listing values", () => {
    const saved = {
      ...savedListingResponse,
      listing: {
        ...savedListingResponse.listing,
        vehicleLabel: {
          value: "Familjebilen",
          provenance: savedListingResponse.listing.make!.provenance,
        },
      },
    };

    const form = listingToManualCalculationForm(saved);

    expect(form.vehicleLabel).toBe("Familjebilen");
    expect(form.purchasePriceSek).toBe("20000");
    expect(form.vehicleTax).toEqual({
      isKnown: true,
      amountSek: "2400",
      cadence: "annual",
    });
    expect(form.energySources).toHaveLength(1);
    expect(form.energySources[0]).toMatchObject({
      label: "Bensin",
      unit: "litre",
      consumptionPer100Kilometres: "8",
      pricePerUnitSek: "",
      distanceSharePercent: "",
    });
    expect(form.calculationPeriodMonths).toBe("12");
    expect(form.annualDistanceMil).toBe("");
    expect(form.insurance.isKnown).toBe(false);
    expect(form.maintenanceAndRepairs.isKnown).toBe(false);
    expect(form.financing.enabled).toBe(false);
    expect(form.residualValueKnown).toBe(false);
    expect(form.otherRecurringCosts).toEqual([]);
    expect(form.otherOneTimeCosts).toEqual([]);
  });

  it("does not invent missing listing values", () => {
    const form = listingToManualCalculationForm({
      ...savedListingResponse,
      listing: {
        ...savedListingResponse.listing,
        priceSek: null,
        annualVehicleTaxSek: null,
        energyConsumptions: null,
      },
    });

    expect(form.purchasePriceSek).toBe("");
    expect(form.vehicleTax.isKnown).toBe(false);
    expect(form.energySources).toEqual([]);
  });

  it("applies selected suggestions without clearing user-owned assumptions", () => {
    const initial = createInitialManualCalculationForm();
    initial.purchasePriceSek = "19000";
    initial.vehicleTax = { isKnown: true, amountSek: "1000", cadence: "monthly" };
    initial.energySources = [{
      ...createEnergySource("75"),
      label: "Tidigare bensin",
      unit: "litre",
      consumptionPer100Kilometres: "7",
      pricePerUnitSek: "21,50",
    }];
    const advertised = savedListingResponse.listing.energyConsumptions!.values[0];

    const withPrice = applyListingPrice(initial, 20_000);
    const withTax = applyListingTax(withPrice, 2_400);
    const replacedEnergy = applyListingEnergyConsumption(withTax, advertised, 0);

    expect(replacedEnergy.purchasePriceSek).toBe("20000");
    expect(replacedEnergy.vehicleTax).toEqual({
      isKnown: true,
      amountSek: "2400",
      cadence: "annual",
    });
    expect(replacedEnergy.energySources[0]).toMatchObject({
      label: "Bensin",
      unit: "litre",
      consumptionPer100Kilometres: "8",
      pricePerUnitSek: "21,50",
      distanceSharePercent: "75",
    });

    const addedEnergy = applyListingEnergyConsumption(initial, advertised, "new");
    expect(addedEnergy.energySources).toHaveLength(2);
    expect(addedEnergy.energySources[1]).toMatchObject({
      label: "Bensin",
      unit: "litre",
      consumptionPer100Kilometres: "8",
      pricePerUnitSek: "",
      distanceSharePercent: "",
    });
  });
});
