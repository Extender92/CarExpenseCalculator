import { savedListingToReviewState } from "@/features/url-analysis/saved-listings";
import { householdApi } from "./api";

/** Retain old URL presentation metadata, but never derive editable household
 * amounts or the shared draft's origin revision from rounded JS numbers. */
export async function readListingForHouseholdDraft(id: string) {
  const exact = await householdApi.listing(id);
  const state = savedListingToReviewState(exact);
  return { ...state, householdBaseRevision: exact.revision.text };
}
