import type { SavedListingResponse } from "@/api/client";
import type { ListingReviewDraft } from "@/features/url-analysis/review-model";
import { savedListingToReviewState } from "@/features/url-analysis/saved-listings";
import { householdApi } from "./api";
import { shiftDecimal, stringifyExact } from "./numbers";

/** Retain old URL presentation metadata, but never derive editable household
 * amounts or the shared draft's origin revision from rounded JS numbers. */
export async function readListingForHouseholdDraft(id: string) {
  const exact = await householdApi.listing(id);
  const ordinary = JSON.parse(stringifyExact(exact)) as SavedListingResponse;
  const state = savedListingToReviewState(ordinary);
  const draft: ListingReviewDraft = state.draft;
  for (const key of [
    "modelYear",
    "imageCount",
    "horsepower",
    "ownerCount",
    "priceSek",
    "odometerKilometres",
    "engineDisplacementCubicCentimetres",
    "annualVehicleTaxSek",
  ] as const) {
    const value = exact.listing[key]?.value;
    if (value != null)
      draft.fields[key].input =
        key === "odometerKilometres"
          ? shiftDecimal(value.text, -1)
          : value.text;
  }
  exact.listing.energyConsumptions?.values.forEach((entry, index) => {
    draft.energyConsumptions.values[index].consumptionPer100Kilometres =
      entry.consumptionPer100Kilometres.text;
  });
  return { ...state, householdBaseRevision: exact.revision.text };
}
