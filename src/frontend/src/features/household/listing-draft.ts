import type { ListingWorkspaceItem } from "@/features/url-analysis/review-model";
import { buildSavedListingRequest } from "@/features/url-analysis/saved-listings";
import { fromOrdinary, stringifyExact } from "./numbers";
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
  stringifyExact(listing); // Reject non-serializable text before opening the shared slot.
  return { registrationNumber: built.request.registrationNumber, listing };
}
