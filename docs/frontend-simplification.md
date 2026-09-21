# Simple first visit and saved comparison

This frontend delivery follows PRs #93, #94, #96 and #97. It preserves the existing HTTP,
storage, calculation and evidence contracts. Registry lookup and advisory AI
are subsequent preparation tasks, not part of this delivery.

## Main flow

The short **Start** page offers **Lägg till bil**, **Jämför sparade bilar**, and
continuation of drafts. **Systemstatus** contains technical details; errors
that prevent saving or retrieval remain visible. Navigation retains the existing
addresses `/`, `/analyze-urls`, `/search`, and `/manual`. Manual entry is linked
from **Lägg till bil**; legacy addresses and contextual editor links remain valid.

When no profile exists, an optional introduction asks for period/start/distance,
purchase cash and optional financing, then selected fuel prices. Empty economic
values remain unknown. Advanced inputs and existing sensitivity trios use the
same typed editor and validation. **Spara och fortsätt** saves a valid partial
profile. **Hoppa över** does not write a profile. A browser-local boolean stores
only whether to offer the guide automatically; the guide can always be reopened.

Completed usable listings are selected for **Spara och jämför**. The action is
available once the analysis queue and prefill finish and selected inputs are
valid. After introduction, the complete-new-listing path uses three primary
clicks: **Lägg till bil → Hämta annonser → Spara och jämför**, excluding pasting.

## Reuse and persistence

The read-only reuse endpoint supplies deterministic proposals. A shared frontend
function applies missing targets to per-car state, without calculations or
confirmation. Price, annual tax, fuels, unambiguous consumption and supported
facts are reusable. Existing zero, false and deliberately empty collections
require explicit replacement. Saved cars have **Fyll saknade uppgifter från
annonsen** and retain the advanced replacement preview.

Original consumption labels and exact decimals are retained. Ambiguous hybrid
pairing and electricity measurement bases remain unresolved. Missing insurance,
residual value and future costs are not invented. Full original descriptions,
equipment, specifications and source metadata remain stored without summarizing.

New registered cars save listing/adoption, bind typed cost sources to the saved
listing version, then save costs and facts with acknowledged revisions. Existing
car edits preserve the earlier cost/fact/listing order so a changed listing never
silently changes reviewed sources. Registration-free listings remain separate
listing-only drafts and do not enter comparison. Duplicates require explicit
review. Each successful step is retained on subsequent failure; remaining edits
stay available without automatic retries or rollback. Only complete selected
work without later edits navigates to comparison. Draft-only batches stay put.

The car editor uses **Spara bil** across changed car resources. Shared household
inputs and buying rules remain separately saved resources. Native dialogs retain
keyboard behavior, focus restoration, scroll restoration and unsaved-edit
choices. Saving and reuse never imply confirmation or registry verification.
**Bekräfta uppgifter** offers an explicit selection with no preselected values.

## Comparison and reports

The main table emphasizes monthly and period cost, requirements and a deduplicated
completion list per car. Shared gaps appear once. No priorities means no score or
coverage columns; no hard requirements means **Inga köpkrav valda**. Known partial
amounts remain visibly incomplete. Ranking and evidence requirements are unchanged.
Cost per mil, detailed sources, sensitivity and payment calendars remain available
in expandable sections.

Reports default to **Sammanfattning**, with **Fullständigt underlag** retaining
the complete earlier report. Both cover the full comparison inventory. Summary
includes results, central assumptions, sources, conflicts and gaps; full mode adds
original listing content and all detailed results/calendars. The selected mode is
captured with the immutable exact-decimal response before report navigation.
Opening and printing the capture make no new API, listing or AI calls.

See the [verification report](frontend-simplification-verification-report.md).
