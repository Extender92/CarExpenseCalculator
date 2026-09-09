# Comparison PDF report (#66)

## Delivery status

Delivered through approved [PR #89](https://github.com/Extender92/CarExpenseCalculator/pull/89),
merged as `72ded81784950860ab9ab186a5f4c30974ee2951`, after preparation PR #88 and
dependencies #65/#85. Issue #66 is closed. The feature verification below was
performed on `feature/66-comparison-pdf` at `bd0c92b95927cabcffbfb99d02a2a804ef40a4f6`;
[PR CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34344637466)
passed before the approved merge. The [#67 preparation](comparison-stage-3b-preparation.md)
defines the remaining whole-stage evidence; feature delivery does not complete it.
HTTP, storage and all existing versions are unchanged. There is no new runtime
package, export service, result calculation or report archive.

## Using the report

On **Jämförelse**, wait for the current comparison and choose **Öppna rapport**.
The same-tab `/search/report` preview includes the complete saved/manual
inventory in the captured cost/score order, including cars beyond the current
50-car page and all collapsed detail sections. All three sensitivity views use
the same membership. The main table follows the captured active mode.

Choose **Skriv ut / Spara som PDF**, then **Spara som PDF**, all pages and A4
landscape in the browser dialog. The application supplies a landscape A4 print
layout; the browser controls final destination, paper, margins and headers.
Cancelling leaves the preview available for another attempt. `afterprint` does
not mean a PDF was saved. **Tillbaka till jämförelsen** releases the report and
retains editing, sorting, pagination and open workspace sections.

No response, zero cars, a pending calculation/write/read, a baseline conflict,
invalid local request data or stale results block capture with Swedish guidance.
**Beräkna nu** is available on the comparison page. A completely received
comparison containing partial/invalid individual sections remains exportable;
its known subtotals, gaps and field errors are preserved.

## State and presentation contract

- [Capture model](../src/frontend/src/features/comparison/report-model.ts):
  `ComparisonReportInput` holds an independent `cloneExact` copy of the accepted
  response, sort, capture time and timezone. Recursive freezing protects all
  arrays, observations, exact decimals and revision values. Evaluation date and
  source timestamps remain separate.
- [Workspace](../src/frontend/src/features/comparison/workspace.ts): capture
  checks both accepted response identity and generation in the operation as well
  as the button gate. Late responses, saves and edits cannot change a capture.
  One report exists in tab memory only, with no URL/history-state/storage copy.
- [Report route](../src/frontend/src/pages/ComparisonReportPage.tsx): providers
  remain mounted outside the normal navigation layout. Both workspaces pause
  automatic refresh/preview work until return. A direct visit or reload shows
  recovery guidance. Observed deletion of a member invalidates the entire report;
  it never removes a row while retaining old rankings.
- [Print document](../src/frontend/src/features/comparison/Report.tsx) uses only
  captured API values. It makes no API/AI request, write, confirmation or new
  calculation. Printing waits for the rendered document and used fonts, and
  cancellation of preparation cannot print an incomplete report.
- [Formatters](../src/frontend/src/features/comparison/report-format.ts) reuse
  exact number/unit functions and screen score/budget labels. Calculated money
  and scores use screen presentation rounding; inputs/anchors retain all decimal
  digits. No displayed value decides ranking or a budget result. Unknown, zero,
  false, empty and included-with-additions remain distinct. Unused null slots of
  typed value unions are not reported as missing alternative values.
- [Print CSS](../src/frontend/src/features/comparison/report.css) supplies dark
  text on white, semantic repeated table headers, wrapping source URLs/notes,
  narrow per-car detail tables and unrestricted page continuation. Every calendar
  month, category and legacy review item remains. Payment source month lists are
  complete wrapping lists. Screen preview tables scroll internally at mobile
  width; printed tables have no scroll clipping or sticky columns.

The report includes shared profile/rules, effective car costs/facts/conflicts,
sources, confirmations, hard outcomes, contributions, signals, unsaved labels,
finance/depreciation/equity, energy and every operating category, leasing,
deposits/refunds, calendar, reconciliation and both budgets. It distinguishes
ownership cost, external cash and internal repair saving. Source revisions and
transport/calculation versions remain visible without dominating the summary.

## Verification evidence

Verified on 2026-09-09 from base main
`d3115f106dbbf3a8995c4c9fe044c0f45d03e091`, with the implementation diff on
`feature/66-comparison-pdf`. The PR supplies the final commit and CI run links.
Environment: Windows, Node 22.22.2, .NET SDK 10.0.400, Docker Engine 29.5.3,
PostgreSQL 18, Playwright 1.62.1 Chromium. The isolated
`car-expense-issue66` Compose/fake-extractor database contained only test fixtures.
The final full browser run used the README's `car-expense-e2e` project, created
after verifying that it had no existing containers or volumes.

| Requirement | Evidence |
| --- | --- |
| Independent capture, exact inputs/revisions, all modes and server order | [Report unit tests](../src/frontend/src/features/comparison/report.test.tsx): mutations after capture, 1/50/51/101/250 membership, full decimal assumptions/anchors, large revisions and exact mil/km. |
| Gates, pending work, conflicts, late replies, deletion and retained editing | [Workspace regressions](../src/frontend/src/features/comparison/report-workspace.test.ts); real-browser malformed-response, deletion, reload and frozen report checks. |
| Partial/complete amounts, unknown/nil/false/empty/included, sources and all records | Report unit tests, including a 121-row calendar, conflicts and 100 legacy entries; real HTTP reports from the cases below. |
| Whole inventory and off-page order | [Chromium report suite](../src/frontend/e2e/comparison-report.spec.ts): 250 saved cars opened from page two with collapsed details; one report includes every UUID exactly once and retains API order. Return restores page two. |
| Cost/score authority | Chromium A2: ownership 20,750, external outflow 80,750, no fees after month 10. A5/A6: energy 9,504/10,320. A8: cost 7,200, external bills 3,600, saving 3,600. A9: cost 60,000, outflow 63,000, refund 3,000, startup 9,000, monthly funding 2,250. B1 85; B2 [45,85], coverage 60%. All matched API and report text. |
| Isolation/accessibility | Chromium explicit manual report with unavailable storage, no print-triggered requests, keyboard focus, 390px preview without page overflow and no local/session storage. Font readiness and cancelled print retry are unit tested. |
| Existing behavior | Entire backend/frontend/Chromium suites, OpenAPI no-drift, Compose boundaries, readiness and URL acceptance; commands follow the [README](../README.md#verification). |

### Actual PDF and print-dialog inspection

Real files were generated from the built Nginx-served report with
`page.pdf({ preferCSSPageSize: true })`, using fictional API-created fixtures.
Inspection used temporary pypdf/PDFium/Pillow tools, never a product dependency.

| File case | Observed output |
| --- | --- |
| Complete lease, PRT104, A9, 24 months | 34 pages; 60,000 cost, deposit/refund and complete payment/source sections. |
| Purchase/partial, PRT101–103, A2/A8 and missing costs | 71 pages; complete and known-only labels, financing versus cash, repair saving and explicit gaps. |
| Long calendar/legacy, PRT105–106, 120 months | 337 pages; 121-month calendar, long note and URL, and separate 50 recurring + 50 one-time legacy records with stable review keys. No truncation or inferred classification. |

All files use approximately 841.92 × 594.96 PDF points (A4 landscape).
Rendered first, second, middle and last pages were inspected for Swedish glyphs,
readable sizes, repeated registration/table headings, wrapping and page breaks.
All non-whitespace glyph bounds across the files were additionally checked
against the printable page area; none fell outside. This supplements visual
inspection rather than substituting a file-header/DOM check.

The actual headed Chromium dialog was opened through the report button, with
Swedish **Spara som PDF**, **Alla** pages and the landscape preview. Cancelling
twice retained the same report and re-enabled another print attempt; return to
the comparison worked. No native-save completion was inferred from closing the
dialog. Actual PDF-file verification used the files above.

Large complete reports can be long: the 120-month case intentionally retains
each payment, error and source record. Browser pagination can split long rows;
the following page repeats the table/registration headings. Other browsers,
physical printers and user-selected paper/header settings were not tested.
Unraid data and live AI were not used. Whole-stage acceptance remains #67.

### Execution notes

A separate fresh stage session is now recorded in the
[#67 acceptance report](comparison-stage-3b-verification-report.md#fresh-pdf-and-native-print-observations).
It generated new 34/39/71/337-page PDFs, checked every page's bounds and all
121-month/50+50 content, visually reviewed representative pages, and repeated
native print/cancel/retry and 390px inspection after realistic edits. The
earlier feature evidence below remains historical; #67's separate merge
approval and stage audit are still pending.

The baseline was 1,012 backend, 255 frontend and 55 Chromium tests. Local
backend restore/build/test passed all **1,012** tests with zero warnings/errors,
failures or skips. Frontend lint/test/build passed **273** tests; the full
one-worker Chromium suite passed **63** tests, with zero failures/skips in the
successful final runs. OpenAPI regeneration from port 5090 left `schema.d.ts`
unchanged. Compose boundaries, pinned CLI 0.153.0, API readiness and URL
acceptance passed. Frontend dependencies and lockfile are unchanged. CI run
links and final commit are recorded in the implementation PR.

Development reruns corrected a pagination selector, avoided Playwright's large
response inspector cache by inspecting the forwarded API response, and waited
for the comparison screen to finish remounting after the 250-car report. The
report document is memoized so print readiness does not rerender all details.
An interrupted development run's fictional cars were explicitly removed; the
final fixtures clean their cars and restore shared profiles in `finally`.
One full development run passed 62/63: the legacy corrupt-result fixture
correctly refused the differently named `car-expense-issue66` SQL target.
The full rerun used the required `car-expense-e2e` project and passed all 63;
the fixture's safety guard was not weakened. A final Swedish payment-label
correction added one frontend regression and reran the report browser suite.

`npm ci` reports two existing high advisories in the development OpenAPI tooling
(js-yaml and its @redocly/openapi-core dependency). No new dependency is added or
unrelated lockfile upgrade made. Playwright retains its existing NO_COLOR /
FORCE_COLOR notice. Initial explicit migration on the empty database logged the
expected missing history-table probe before applying all migrations successfully.

### Cleanup inventory

Both task-created Compose stacks, disposable PostgreSQL/Codex-home volumes and
networks were stopped/removed. The port-5090 API, headed Chromium inspection
browser and task-created MSBuild nodes were stopped. No task listener remains
on ports 5090, 8088 or 9229. Generated types/dependencies remain unchanged.

Native PowerShell deletion of the verified paths below was rejected by the
execution tool with `blocked by policy`, without further explanation. These
files remain; cleanup is not claimed successful and no alternate deletion
mechanism was used to bypass the rejection:

- `C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\issue66\`:
  work-owned helpers, PDFs, rendered pages, test/build logs, inspection-only
  Python packages, Node cache and browser artifacts. The whole ignored directory
  is disposable; it contains no user input or required product source.
- Seven empty directories created by the headed-browser inspection under
  `C:\Users\dann_\AppData\Local\Temp\`:
  `playwright-artifacts-csPRAT`, `playwright-artifacts-fRvMWV`,
  `playwright-artifacts-GsXhoJ`, `playwright-artifacts-hyntcp`,
  `playwright-artifacts-IlhrhX`, `playwright-artifacts-IYE6ij` and
  `playwright-artifacts-Q41Eib`.

Windows Temp was inventoried. Unattributable empty GUID `.tmp` files and
background-installer logs/directories were preserved. Existing build outputs
and dependencies are not report helpers. Deferred #64/#85/#65 inventories,
including the previously installed pinned Node used read-only, remain untouched.
No helper, PDF sample, screenshot or cache is committed.
