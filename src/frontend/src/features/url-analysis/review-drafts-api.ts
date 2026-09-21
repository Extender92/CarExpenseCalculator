import type { components } from "@/api/schema";
import type { ReviewedListingInput, SavedListingResponse } from "@/api/client";
import { request } from "@/features/household/api";
import { n, type Exact } from "@/features/household/numbers";
import { listingNumberText, type ListingNumber } from "./exact";

export type ReviewDraftResponse = Exact<components["schemas"]["ListingReviewDraftResponse"]>;
const route = "/api/listing-review-drafts";
type Response = components["schemas"]["ListingReviewDraftResponse"];
export const reviewDraftApi = {
  list: (signal?: AbortSignal) => request<Response[]>(route, "GET", undefined, signal),
  read: (id: string) => request<Response>(route, "GET", undefined, undefined, `/${encodeURIComponent(id)}`),
  create: (input: ReviewedListingInput) => request<Response>(route, "POST", { input }),
  replace: (id: string, revision: ListingNumber, input: ReviewedListingInput) =>
    request<Response>(route, "PUT", { input, expectedRevision: n(listingNumberText(revision)) }, undefined, `/${encodeURIComponent(id)}`),
  remove: (id: string, revision: ListingNumber) => request<void>(route, "DELETE", undefined, undefined,
    `/${encodeURIComponent(id)}?expectedRevision=${encodeURIComponent(listingNumberText(revision))}`),
  adopt: (id: string, revision: ListingNumber, existingVehicleId?: string, expectedVehicleRevision?: ListingNumber): Promise<SavedListingResponse> =>
    request<components["schemas"]["SavedListingResponse"]>(route, "POST", {
      expectedRevision: n(listingNumberText(revision)), existingVehicleId,
      expectedVehicleRevision: expectedVehicleRevision == null ? undefined : n(listingNumberText(expectedVehicleRevision)),
    }, undefined, `/${encodeURIComponent(id)}/adopt`),
};
