# Frontend simplification verification

Verified on 2026-09-21 on `feature/simplify-car-workflow`, based on
`67ddfa22cbc296cf5ca83feb420b02db544c64b0`. HTTP contracts, generated OpenAPI,
storage versions, migrations, calculations and evidence requirements are unchanged.
Merge and installation updates require separate approval.

## Environment and checks

Disposable PostgreSQL 18 and fake extractor, Compose project `car-expense-e2e`,
network `car-expense-frontend-simplification`, port 8091. Existing installations,
credentials and user data were excluded.

| Check | Result |
| --- | --- |
| .NET SDK 10.0.400 restore/build | Passed; zero warnings/errors |
| Backend | 1,169 passed; zero failed/skipped |
| Node 22.22.2 / npm ci | Passed; no dependency changes |
| Frontend lint / production build | Passed |
| Frontend component/unit tests | 323 passed; zero failed/skipped or unhandled errors |
| OpenAPI regeneration | Passed; no content diff |
| Complete Chromium, one worker | 85 passed; zero failed/skipped |
| URL acceptance and log-content scanner | Passed: outcomes, FIFO, explicit retries, isolation and safe logs |
| Ordinary GitHub CI | Required on the delivery PR |

The existing npm tree reports two high-severity development advisories involving
OpenAPI tooling. Local builds occasionally emit Vite's plugin-timing diagnostic;
Playwright emits the existing NO_COLOR/FORCE_COLOR warning.

## Measured workflows and regressions

- After introduction, two complete fictional advertisements take three primary
  clicks: **Lägg till bil → Hämta annonser → Spara och jämför**. Tests inspect
  persisted price, tax, fuel, consumption, facts and listing versions.
  Values remain unconfirmed and cost confirmation remains absent.
- Introduction covers skip without writing, partial valid save, reopening,
  existing profiles and preservation of exact uncertainty scenarios.
- A mixed car/draft batch injects a cost-write failure. Listing and draft save
  once; the next explicit attempt writes only remaining resources.
  Duplicate registrations require review and preserve the first saved listing.
- A deferred cost write allows later editing. That edit prevents navigation and
  remains until the next explicit save; completed fact writes are not repeated.
- Reuse preserves zero, false, empty collections, exact decimals, consumption
  labels and manually cleared values. Sources acquire the adopted listing
  version; unsupported claims are removed without refilling cleared values.
- Prefill starts from committed component state. A regression suppresses
  animation-frame callbacks to prove that preparation cannot become stuck.
- Unified car saving persists costs and fact edits in revision order and clears
  old cost confirmation. Conflict, deletion, legacy-address and original-content
  regressions remain covered.
- At 390 × 844, cards, editors, comparison and reports fit the document width.
  Local table scrolling, keyboard focus, Escape, Back/Forward, unsaved navigation
  and discard are covered.

## Before and after

Baseline images use the separately built frontend from `67ddfa2`, connected only
to the disposable stack. All cars and prices are fictional test fixtures.

| View | Before | After |
| --- | --- | --- |
| First visit | [Before](assets/frontend-simplification/before-start.png) | [After](assets/frontend-simplification/after-start.png) |
| Retrieved cars | [Before](assets/frontend-simplification/before-cars.png) | [After](assets/frontend-simplification/after-cars.png) |
| Mobile | — | [390 × 844](assets/frontend-simplification/after-mobile.png) |

Previously, each car required separate listing/reuse/resource actions before
comparison. The batch path saves two new cars without individual reuse or
confirmation clicks. Complete original text remains behind details.

## Practical PDF inspection

Real A4 landscape PDFs were generated from two saved listings, one complete and
one partial, with long original descriptions, 30 equipment entries, a long
source address, and individual electric shares of 20% and 80%. An independent
profile update does not change the frozen report. Opening and printing make no
browser API, advertisement or AI requests.

The stress fixture produced a six-page summary and a 102-page full report.
Full mode retains all three sensitivity views and the existing detailed tables;
its length reflects this deliberately verbose fixture. Summary deduplicates
missing costs and omits original advertisement text. Rendered first, middle and
final pages were inspected for Swedish characters, wrapping, repeated headers
and page boundaries. PDF text coordinates remain inside every page.
Full mode contains **Handlare**, **Privat**, the last equipment entry and unchanged
original text; both modes contain every car, sources, gaps and electric shares.
Native print-dialog cancellation has existing component simulation coverage;
this run does not claim a manual operating-system dialog test.

## Interim failures retained as history

Early failures involved obsolete labels, the changed report default, hidden
score columns and tests selecting controls in hidden modal tabs. The legacy
fixture now creates an existing listing-only car, independently of modern
automatic prefills. The unified-save regression verifies saved fact edits too.

An intermediate full Chromium run passed 83 and failed two: an obsolete
resource-specific save expectation and the real prefill/frame race. Both were
corrected; all 21 affected browser tests then passed.
A later run passed 84 and exposed an ambiguous test selector matching both a
recommendation and its table row. Scoping it to the main comparison fixed the
selector; the final full run passed all 85 tests.
An initial JSON unit report showed all assertions passing while its process
failed. The ordinary reporter exposed an incomplete API mock's unhandled
rejection. After correcting the mock, the ordinary command exited successfully
with 323 passing tests and no unhandled errors.

## Delivery and cleanup

Helpers, logs, PDFs and temporary builds use the ignored
`temp/frontend-simplification/` directory. The screenshots above are intentionally
tracked. Final stack cleanup and PR verification are recorded with delivery.
Unrelated temporary folders and private installation notes are preserved.
