# Listing report presentation correction

## Scope and status

Verified on 2026-09-11 on `fix/listing-report-presentation`, based on merged
main `2488a8912d5c70cd6da92c98e3523bd9c4458559`. The implementation and regression
tests are in commit
[`ed46fa9`](https://github.com/Extender92/CarExpenseCalculator/commit/ed46fa99db7e9c38c3c71d55700c2cddcdd955e2).
This is a verified branch delivery. Merge and updating the existing local web
container require the separately approved follow-up. No backend, generated
OpenAPI, storage, extraction or calculation changes are included.

The [post-merge PR #93 observation](https://github.com/Extender92/CarExpenseCalculator/pull/93#issuecomment-5632794141)
remains the historical record: with a saved listing, the report was 724px wide
at a 390px viewport and showed the untranslated `dealer` value. The earlier
manual-mode mobile test contained no listings and could not detect this gap.

`ListingContent` now uses a named, keyboard-focusable local scroll region in
both comparison and report. The readable 700px table width is retained on screen;
the existing print rules release overflow and minimum width. The seller field
and review choices share a typed label map: **Handlare**, **Privat**, or the
existing unknown-value presentation. Original prose, specification values,
saved enum values and source/verification metadata remain unchanged.

## Automated evidence

| Requirement | Evidence |
| --- | --- |
| Reproduce before correcting | New component cases failed for `dealer` and `private`; the unknown case passed. The stored report browser test reproduced document width **724px**, and the comparison test found no accessible scroll region. |
| Swedish field labels only | [Component tests](../src/frontend/src/features/url-analysis/details.test.tsx) check Handlare, Privat and missing data; literal `dealer`/`private` in the original description and specifications remain. Input serialization and unverified provenance are unchanged. |
| Saved complete and partial listings | [Browser fixture](../src/frontend/e2e/listing-presentation-fixtures.ts) creates fictional LPR100/LPR101 through the real API. One has complete basic listing fields, the other lacks price, VIN and owners. Both carry 18 description paragraphs, 30 equipment entries and a 660-character source URL. |
| Mobile comparison and report | [Browser regressions](../src/frontend/e2e/listing-presentation.spec.ts) run at **390 × 844px**, check document width ≤390, focus and ArrowRight scrolling, the far table edge and complete content. Both views retain HTML/unverified evidence and Swedish seller labels. |
| Print and frozen capture | Print media releases the scroll restriction; every listing table fits its container. A later saved seller change does not alter the captured report. PDF generation triggers zero browser requests and leaves report text unchanged. |
| Existing behavior | The full 72-test Chromium suite retains the previous 70 tests, including URL review, household calculations, saved listing/draft lifecycles, comparison and report behavior. URL acceptance and Compose boundary checks pass. |

Local verification used Node **22.22.2**, npm **10.9.7**, Playwright **1.62.1**
Chromium and disposable PostgreSQL **18**. The sidecar image's pinned CLI
**0.153.0** was checked with `--version` only. The runtime used the fake extractor;
there were no live Blocket or AI requests.

| Command/check | Final local result |
| --- | --- |
| `npm --prefix src/frontend ci` with pinned Node | Passed; two previously recorded high-severity development dependency advisories remain. |
| `npm --prefix src/frontend run lint` | Passed, zero lint warnings/errors. |
| `npm --prefix src/frontend run test` | **285 passed**, zero failed/skipped, 27 files. |
| `npm --prefix src/frontend run build` | Passed, zero build warnings/errors. |
| `node scripts/verify-compose-boundaries.mjs` | Passed. |
| Isolated Compose build, explicit migration and Nginx readiness | Passed. |
| `npm --prefix src/frontend run e2e -- --project=chromium --workers=1` | **72 passed**, zero failed/skipped, 3.1 minutes; no retries in the full run. |
| `node scripts/verify-url-analysis-acceptance.mjs` | Passed: concurrency, outcomes, isolation and safe logs. |
| Backend/OpenAPI | No local backend rerun or schema regeneration was needed for this frontend-only change. Ordinary PR CI must retain **1,132 backend tests** and verify unchanged generated OpenAPI. The PR linked from the PR #93 follow-up records the published head's CI result. |

## Fresh practical PDF and print inspection

The session reused the fictional listing fixture through the isolated API.
LPR100 also received current purchase inputs: price 40,000 SEK, fixed 12-month
residual 20,000 SEK, explicitly empty operating-cost collections, zero repair
reserve and petrol consumption 1 L/100km. Only unsaved profile assumptions were
edited: 12 months, January 2026 start, zero driving distance, 100,000 SEK purchase
cash and 7,000 SEK monthly budget. This produces a complete 20,000 SEK ownership
total while per-distance cost remains unavailable. LPR101 remains partial.

`page.pdf({ preferCSSPageSize: true })` produced **79 A4-landscape pages**,
approximately 841.92 × 594.96 PDF points. All **57 content checks** passed,
including both Swedish seller labels, every paragraph and equipment entry,
the complete long source address, source methods and unverified markers.
All non-whitespace character bounds across every page were checked: **zero
outside page bounds**. Images of pages 1, 13, 20, 23, 49, 56, 59 and 79 were
reviewed for Swedish glyphs, repeated headers, continued rows, wrapping and
page boundaries. No content was clipped or omitted. Mobile screenshots and
keyboard interaction confirmed local scrolling in both screens.

The actual headed Chromium print preview was opened with **Spara som PDF**,
**Alla** pages and visibly landscape pages. CSS supplies A4/orientation; this
dialog did not expose separate paper/orientation selectors. Cancel and retry
were exercised twice in the final session. The captured report stayed unchanged,
**zero API requests occurred while printing**, and return retained the unsaved
7,000 SEK budget. No browser exceptions occurred. Printer-specific settings
remain controlled by the browser.

## Development failures and reruns

The intentional red regressions are preserved above. During test preparation,
a Testing Library `exact` option caused a TypeScript build error and the initial
API fixture omitted the required registration envelope/used response-shaped
sources. These test errors were corrected before the red browser run; no
production validation was weakened. The first shell helper could not load under
PowerShell's signing policy, so an initial `npm ci` used Node 22.13.0 and emitted
engine warnings. Installation and all final frontend checks were rerun with
explicit process-local Node 22.22.2 settings.

The first practical fixture used invalid zero fuel consumption and was rejected
with HTTP 400. It was corrected to valid consumption and explicit zero distance;
its temporary cars were cleaned in `finally`. The first PDF text check failed
only for the long URL: PDFium represented visible line-ending hyphens as U+0002.
After inspecting those rendered glyphs, the temporary text-check helper normalized
that extraction marker. All 57 checks then passed. Screenshots/PDF and native
print sessions were repeated for final visual inspection and to separate print
requests from normal requests after returning to the workspace. Node's
NO_COLOR/FORCE_COLOR notice and Git's normal LF/CRLF notices were also observed.

## Isolation and cleanup

The test stack used exactly `car-expense-e2e`, port **8091** and its own network
`car-expense-e2e-listing-presentation`; an ignored temporary Compose override
supplied the network name. Tests never used the ordinary app's network or volumes.
The fixture registered each created UUID immediately and deleted it in `finally`
using its current revision. The final disposable inventory contained zero cars.
All work browsers/processes, four test containers, both test networks and the
work-owned PostgreSQL/empty Codex volumes were stopped or removed.

The existing `car-expense-calculator` services at **8088** were untouched and
remained healthy. The earlier backup, old container images and deferred temporary
inventories were preserved. No new attributable work files were found in Windows
Temp; unrelated empty files were left alone.

Native PowerShell deletion of the verified work directory was rejected by the
automatic execution policy with `blocked by policy`. No alternate deletion
mechanism was attempted. The exact retained directory is:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\listing-report-presentation\
```

It contains this work's helper files, test logs/caches, PDFs, screenshots and the
closed test-only browser profile. No application login or user data was copied.
This cleanup limitation does not represent an unresolved presentation defect.

The same policy also rejected removal of the generated build/cache directories
outside that temp folder. These exact paths remain (installed dependencies are
otherwise retained):

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\src\frontend\dist\
C:\Users\dann_\Source\repos\CarExpenseCalculator\src\frontend\node_modules\.tmp\
C:\Users\dann_\Source\repos\CarExpenseCalculator\src\frontend\node_modules\.vite\
C:\Users\dann_\Source\repos\CarExpenseCalculator\src\frontend\node_modules\.vite-temp\
```
