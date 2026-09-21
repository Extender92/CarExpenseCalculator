import type { ListingDraftResponse } from "@/api/client";
import { listingResponseToDraft, type ListingWorkspaceItem } from "./review-model";
import type { ReviewDraftResponse } from "./review-drafts-api";

export function reviewDraftToItem(value: ReviewDraftResponse): ListingWorkspaceItem {
  // The backend returns a validated, normalized reviewed input. Input/response field
  // values are identical; the response requires provenance on every known value.
  const draft = listingResponseToDraft(value.input.draft as ListingDraftResponse);
  return {
    id: `review-${value.id}`, submittedUrl: value.input.submittedUrl, normalizedUrl: value.listingReference,
    context: { analyzedAtUtc: value.input.analyzedAtUtc, requestedModel: value.input.requestedModel ?? null,
      promptVersion: value.input.promptVersion ?? null, schemaVersion: value.input.schemaVersion ?? null,
      sources: (value.input.sources ?? []).map(url => ({ url, matchesSubmittedUrl: samePage(url, value.listingReference) })) },
    draft, baseline: draft, reviewDraft: { id: value.id, revision: value.revision },
    saved: null, phase: "partial", dirty: false, error: null, saving: false,
    persistenceNotice: null, validationErrors: {}, controller: null,
  };
}
function samePage(left: string, right: string) {
  try {
    const a = new URL(left), b = new URL(right);
    return a.hostname.replace(/^www\./, "") === b.hostname.replace(/^www\./, "") && a.pathname === b.pathname;
  } catch { return false; }
}
