# PDF export preparation (#66)

## Audit and delivery gate

**Implementation update:** preparation PR #88 is merged. The user subsequently
assigned #66, now `status:in-progress` on `feature/66-comparison-pdf`.
The [implemented report and test evidence](comparison-pdf.md) describe the
delivered branch behavior and actual PDF/native-dialog inspection. The original
audit and proposed seams below are retained as the planning record, not as
outstanding readiness instructions. Implementation merge requires separate
approval; #67 is still the subsequent whole-stage acceptance.

Audited on 2026-09-09 against `main` commit
`4ba0a0b0f386f7e077c4466a046726cdf3a9df97`. The worktree was clean and
[merged-main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34274854144)
passed: **1,012 backend, 255 frontend and 55 Chromium tests**. These are the
existing baseline, not new PDF verification.

Both immediate prerequisites are delivered: documentation
[PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54) and
[#65](https://github.com/Extender92/CarExpenseCalculator/issues/65) through
approved [PR #87](https://github.com/Extender92/CarExpenseCalculator/pull/87).
The complete-inventory backend (#85 / PR #86) is also on main. Five of the seven
stage 3B implementation items are merged; #66 and #67 remain.

At the preparation audit, the user had assigned preparation, not implementation. This document refines the
existing [report specification](comparison-and-buying-scores.md#workspace-and-reports)
without adding a product mode or changing calculation rules. No outstanding
product decision was found. Implementation choices below are the proposed
technical approach, not a claim that a report screen exists.

The preparation gate has now been satisfied: its separately approved merge and
the prerequisites were checked before promoting #66 to `status:ready` and
starting the assigned implementation. #67 remains dependent on #66; neither
the tracker nor the milestone is complete. The preparation PR did not close #66.

## Scope already agreed

- Export one current, completely received comparison generation, including valid
  partial candidates, through the browser's print-to-PDF flow.
- Include every candidate in the generation. The workspace's 50-car page and
  collapsed sections are display choices, not report filters. Preserve the
  selected cost/score order from the API and the active sensitivity mode.
- Include shared and car assumptions, all three sensitivity views, main and
  detailed costs, buying rules, weights, evidence, missing data and unsaved marks.
- Use Swedish text, readable multipage tables and explicit save-PDF instructions.
  Exporting never saves profiles, facts, costs, confirmations or a report archive.

Server PDF rendering, new HTTP routes or database migrations, external export
services, automatic sending, report history and live AI are out of scope. Existing
comparison/transport/rule versions remain 1, household versions 2 and storage
formats 1. Whole-stage practical acceptance belongs to #67.

## Inspected implementation and required seams

| Existing code | Consequence for #66 |
| --- | --- |
| [Comparison workspace](../src/frontend/src/features/comparison/workspace.ts) stores one accepted `ComparisonResponse`, freshness and in-flight state | Add an explicit report capture operation. Do not call `capture()` to create a new calculation request or read live editors while rendering the report. |
| [Response validation](../src/frontend/src/features/comparison/preview.ts) checks complete membership and all three views before publication | Consume only a response accepted by this path. Reuse its invariants; a malformed or truncated response is not a printable partial report. |
| [Generated schema](../src/frontend/src/api/schema.d.ts): `CompleteComparisonResponse`, `ComparisonPreviewResponse`, `ComparisonCandidateResult` | Already contains effective profile/rules/date, effective facts/cost inputs, unresolved legacy items, source revisions, confirmation times, dirty flags, results and server order. No extra car-detail reads or OpenAPI changes are needed. Registration is the available car label; do not invent make/model or fetch unrelated display labels. |
| [Comparison page](../src/frontend/src/pages/ComparisonPage.tsx) slices rows after ordering | Capture the complete response and order, never `pageRows`, the selected car or just the mounted table DOM. |
| [Table presentation](../src/frontend/src/features/comparison/Tables.tsx) has interactive links, sticky columns and scroll wrappers | Share value/label presentation with the screen, but provide a print layout without interactive controls, clipping or a single oversized table cell. Do not copy cost/score formulas. |
| [Exact numbers](../src/frontend/src/features/household/numbers.ts), household labels and comparison catalogue | Preserve `Numeric` values through `cloneExact` or an equivalent typed copy; native JSON/Number conversion is unsuitable. Reuse displayed-money formatting and exact mil/km conversion. |
| [Providers and routes](../src/frontend/src/App.tsx) retain tab-local editing | Keep at most one transient report separate from live profile/rule/fact state. Opening/closing it must not discard unsaved editing. |
| [Existing browser tests](../src/frontend/e2e/comparison-workspace.spec.ts) and [Playwright configuration](../src/frontend/playwright.config.ts) | Extend the real Nginx/API/PostgreSQL flow with one worker. Reuse fixtures, exact references and explicit two-context concurrency. |

## Report capture and lifetime

The proposed frontend-owned `ComparisonReportInput` contains an independent,
readonly copy of the accepted complete response, its generation/request identity,
the selected sorting choice and the report capture timestamp with timezone.
The response already carries the evaluation date, active sensitivity, effective
inputs, source revisions and unsaved flags. Keep capture time distinct from the
user's evaluation date and from source observation/confirmation times.

Before capture, require a matching current mode/generation with no stale result,
pending or queued recalculation, pending relevant write, unresolved baseline
conflict or local request/structure error. Recheck this gate in the handler,
not only through a disabled button. Show a Swedish reason and **Beräkna nu**
when recalculation is needed. An empty comparison shows guidance to add cars;
there is no report to export.

A valid HTTP 200 response may contain incomplete or invalid individual cost/fact
sections. Those remain exportable with their field errors and known subtotals;
they must never acquire a complete total or winner. Invalid rules, failed
requests, missing response views and locally unparseable edits block capture.
Use the API's per-candidate unsaved flags rather than treating a client dirty
boolean as evidence or silently considering a manual candidate saved.

Calculated outputs use the screen's final display rounding. Input assumptions
and rule anchors also need exact readable text, preserving all supplied decimal
digits with Swedish units/separators. Do not use the rounded rule-summary display
as the sole record of a high-precision goal or pass exact inputs through Number.

Once captured, ordinary edits, profile saves, focus refreshes, sorting changes,
page changes and late network responses cannot alter that report. Render all
values, warnings and recommendations from the captured generation, never from
`workspace.results()` or current form state. Mark it as a report of the
comparison captured at the stated time. Printing does not revalidate the
database at print time and must not claim that it does.

Use a same-tab preview route such as `/search/report`, with **Skriv ut / Spara
som PDF** and **Tillbaka till jämförelsen**. The route carries no inputs or
snapshot in query parameters, router history state, local storage or session
storage. A direct visit/reload without the in-memory snapshot explains that a
new report must be opened from the comparison. Returning releases the report
while preserving editing. A new explicit capture replaces the previous one.

Do not infer successful saving from `afterprint`: the user may have cancelled.
Keep a cancelled dialog's report available for another print attempt until the
user leaves it. A locally observed whole-car deletion invalidates a report
containing that car and requires a fresh capture, rather than silently removing
a row and keeping its old ranking. Follow existing deletion invalidation events;
do not add polling or a remote-delete subscription. A PDF or print job already
handed to the browser remains outside application deletion, as already specified.

The print route must not start workspace loading/calculation or lazy fact reads.
Account for the providers' existing focus listeners when the print dialog opens
and closes: printing itself must not trigger server work. Resume the normal
workspace refresh behavior on return. Any already pending response stays separate
from the captured report.

## Content and layout handoff

The proposed layout uses a white background, dark text and A4 landscape. Treat
this as a tested default; the browser controls final paper/printer settings.
No report section relies solely on color to distinguish gaps or rejection.

| Report part | Required content |
| --- | --- |
| Identity and context | Capture time, evaluation date, stored/manual mode, active sensitivity, candidate count, selected order, version metadata and prominent unsaved summary. Include request/generation and source-revision metadata in a compact provenance section. |
| Main comparison | All registrations, input status, total/month/mil, known-vs-complete labels, hard outcome, score interval, coverage and the captured server recommendations. |
| Shared assumptions and priorities | Every effective household value, all uncertainty values, units and missing/zero states; criteria, activation, inclusive bounds, anchors, choices, weights, evidence requirements and signal thresholds. |
| Cost breakdowns | Financing/depreciation/equity; energy/tax/insurance/custom costs; service/known repair/repair saving; lease coverage/deposit/refund; payments, reconciliation and both budgets. Explain cost versus cash and internal saving. |
| Sensitivity | Baseline, favorable and cautious totals/gaps for exactly the same candidates. Main order remains the selected active-view API order, including ties and off-page winners. |
| Per-car assumptions and evidence | Complete effective cost inputs, current facts and conflicting observations, sources and listing versions, review needs, cost-confirmation time, unresolved legacy keys/reasons and all field errors. Null, false, zero, empty and included-with-additions stay distinct. |

Include all detail groups even when collapsed on `/search`. Compact overview
tables may be followed by narrower per-car tables for long explanations,
payment calendars and input/source details; do not put 121 months or all evidence
into one unbreakable row. Repeat registration and relevant headings where a car
continues. Preserve every monthly row and every source/review entry, including
maximum legacy 50+50 items. Avoid ellipsis, line clamping or arbitrary page/car
limits. Split wide column groups with the registration repeated instead of
shrinking text until it is unreadable.

Print CSS must remove screen navigation/actions/sticky positioning, constrain
columns to printable width, wrap long source URLs/notes, repeat semantic `thead`
headers and permit oversized content to continue across pages. Short rows and
headings should stay together where they fit; do not apply blanket break-avoid
to arbitrarily large sections. The proposed default uses `@media print` and
`@page`; verify the actual output, since page fragmentation is browser-controlled.
See [MDN printing guidance](https://developer.mozilla.org/en-US/docs/Web/CSS/Guides/Media_queries/Printing).

Enable printing only after the complete report has rendered and used fonts are
ready; handle leaving/cancelling preparation without printing incomplete content.
[`document.fonts.ready`](https://developer.mozilla.org/en-US/docs/Web/API/Document/fonts)
provides the used-font readiness signal. No externally hosted assets or new PDF
runtime dependency are needed. Explain choosing **Spara som PDF**, all pages and
the tested A4/landscape settings. Do not show a successful-download message just
because the dialog opened.

## Required verification for implementation

| Boundary | New or extended evidence |
| --- | --- |
| Capture consistency | Change profile/rules/car values, sensitivity and sort during report rendering; resolve a late preview/save and focus refresh. Assert report values, ordering, sources and unsaved labels stay on one captured generation. |
| Gating and recovery | No response, zero cars, debounce/queue, stale/error/409, malformed JSON/missing view and local invalid rules block capture. Fresh complete and partial responses export. Direct report reload and cancelled print preserve the defined recovery behavior. |
| Whole inventory | 1/50/51/101/250 candidates, selected page beyond one and collapsed details: no omissions/duplicates; off-page recommendations and all details are present. No fixed cap or silent truncation. |
| Authority and precision | A2 20,750 versus 80,750; A5/A6 9,504/10,320; A8 repair saving; A9 deposit/refund; B1 85, B2 [45,85]/60%; rejected and overlapping candidates, equal displayed amounts with unequal server order, exact assumptions and large revisions. |
| Complete content | Purchase/lease, all three sensitivity views, manual/stored mode, unsaved profile/rule/fact/cost/confirmation/review flags, missing/zero/false/empty/included states, conflicting facts, sources/versions and maximum legacy review entries. |
| Print layout | Actual Chromium PDFs with Swedish å/ä/ö, multiple pages, repeated headings, no clipped columns, long URLs/notes and a maximal payment calendar. Inspect complete and partial files visually; screen CSS or `%PDF` alone is not sufficient evidence. |
| Isolation and lifecycle | Print causes no calculation/detail/save/AI requests; manual report works without storage; returning retains edits; locally observed full deletion invalidates the report; no report in local/session storage or persisted history. |
| Accessibility and compatibility | Keyboard opening/printing/return, visible focus/readiness/errors, readable screen preview at 390 px; existing household, URL, legacy and comparison regressions remain. |

Use the installed Playwright Chromium to generate test PDFs from the real print
view with `page.pdf({ preferCSSPageSize: true })`, and inspect the resulting
pages. This is test tooling, not a server/product PDF renderer. The native
print-dialog action and Swedish instructions also need a practical check;
headless PDF generation alone does not exercise that dialog. Record any manual
check that could not be completed. See [Playwright PDF documentation](https://playwright.dev/docs/api/class-page#page-pdf).

Run frontend `npm ci`, lint, test and build; regenerate OpenAPI from port 5090
and require no diff. Follow the [README verification sequence](../README.md#verification)
for the isolated PostgreSQL 18/fake-extractor/Nginx stack, explicit migration,
readiness, Compose-boundary script, the full Chromium suite with one worker and
URL acceptance. Ordinary CI also runs all backend tests. Use Node 22.22.2 and
.NET SDK 10.0.400. Record counts, warnings, skipped checks and development reruns.
No production database or real AI service is part of verification.

## Preparation publication and cleanup

This preparation changes documentation and the authorized #66/#12 backlog only.
Validate Markdown links/anchors, terminology, current code references, dependency
order and Git diff. No product code, generated types, tests, runtime dependency,
deployment setting or feature flag changes in this PR. Ordinary documentation
PR CI still applies; successful baseline tests are not PDF acceptance evidence.

Publish the preparation on `docs/66-pdf-export-preparation` with immutable
document links in #66 and an updated #12 tracker. Its merge requires separate
approval. After the approved merge and dependency recheck, promote #66 to
`status:ready`; actual implementation still requires the user's issue assignment.

Create any needed work helpers only under `temp/issue66/`, never in `.git` or
unrelated Windows Temp directories. This documentation audit requires no helper
files, test stack or temporary service. Keep the deferred #64/#85/#65 cleanup
inventories untouched. Implementation-owned PDF samples, screenshots and tools
must be inventoried and cleaned after inspection; preserve evidence descriptions
in docs and report any blocked removal without claiming cleanup succeeded.
