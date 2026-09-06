import type { ListingWorkspaceItem } from "@/features/url-analysis/review-model";
import { buildSavedListingRequest } from "@/features/url-analysis/saved-listings";
import { fromOrdinary, n, shiftDecimal, stringifyExact } from "./numbers";
import type { ReviewedListing } from "./api";

export function reviewedListingForDraft(item: ListingWorkspaceItem): {
  registrationNumber: string;
  listing: ReviewedListing;
} {
  const built = buildSavedListingRequest(
    item.submittedUrl,
    item.normalizedUrl,
    item.context,
    item.draft,
  );
  if (!built.request) throw new Error(Object.values(built.errors).join(" "));
  const listing = fromOrdinary(built.request.listing);
  // Keep the existing provenance/normalization contract, then restore numerical
  // values directly from editable text before any exact serialization.
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
    const field = listing.draft[key];
    if (field)
      field.value = n(
        key === "odometerKilometres"
          ? shiftDecimal(item.draft.fields[key].input.trim(), 1)
          : item.draft.fields[key].input.trim(),
      );
  }
  listing.draft.energyConsumptions?.values.forEach((entry, index) => {
    entry.consumptionPer100Kilometres = n(
      item.draft.energyConsumptions.values[index].consumptionPer100Kilometres,
    );
  });
  stringifyExact(listing); // Reject non-serializable text before opening the shared slot.
  return { registrationNumber: built.request.registrationNumber, listing };
}
