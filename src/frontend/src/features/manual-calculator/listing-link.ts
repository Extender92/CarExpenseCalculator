import type { SavedListingResponse } from "@/api/client";
import {
  createEnergySource,
  createInitialManualCalculationForm,
  type ManualCalculationForm,
} from "./form-model";

type AdvertisedEnergy = NonNullable<
  SavedListingResponse["listing"]["energyConsumptions"]
>["values"][number];

export function listingToManualCalculationForm(
  savedListing: SavedListingResponse,
): ManualCalculationForm {
  const form = createInitialManualCalculationForm();
  const listing = savedListing.listing;

  form.vehicleLabel = listing.vehicleLabel?.value ?? "";
  form.purchasePriceSek = inputNumber(listing.priceSek?.value);
  form.vehicleTax = listing.annualVehicleTaxSek
    ? {
        isKnown: true,
        amountSek: inputNumber(listing.annualVehicleTaxSek.value),
        cadence: "annual",
      }
    : form.vehicleTax;
  form.energySources = (listing.energyConsumptions?.values ?? []).map((source) => ({
    ...createEnergySource(),
    label: source.label,
    unit: source.unit,
    consumptionPer100Kilometres: inputNumber(source.consumptionPer100Kilometres),
  }));

  return form;
}

export function applyListingPrice(
  form: ManualCalculationForm,
  priceSek: number,
): ManualCalculationForm {
  return { ...form, purchasePriceSek: inputNumber(priceSek) };
}

export function applyListingTax(
  form: ManualCalculationForm,
  annualVehicleTaxSek: number,
): ManualCalculationForm {
  return {
    ...form,
    vehicleTax: {
      isKnown: true,
      amountSek: inputNumber(annualVehicleTaxSek),
      cadence: "annual",
    },
  };
}

export function applyListingEnergyConsumption(
  form: ManualCalculationForm,
  source: AdvertisedEnergy,
  targetIndex: number | "new",
): ManualCalculationForm {
  if (targetIndex === "new") {
    if (form.energySources.length >= 2) return form;
    return {
      ...form,
      energySources: [
        ...form.energySources,
        {
          ...createEnergySource(),
          label: source.label,
          unit: source.unit,
          consumptionPer100Kilometres: inputNumber(source.consumptionPer100Kilometres),
        },
      ],
    };
  }

  if (!form.energySources[targetIndex]) return form;
  return {
    ...form,
    energySources: form.energySources.map((current, index) => index === targetIndex
      ? {
          ...current,
          label: source.label,
          unit: source.unit,
          consumptionPer100Kilometres: inputNumber(source.consumptionPer100Kilometres),
        }
      : current),
  };
}

function inputNumber(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}
