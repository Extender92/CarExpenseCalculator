# Complete listing extraction: verification and remaining blocker

Status: **implementation under verification; complete live extraction is not accepted**.
Branch: `fix/codex-login-status`, based on `fc7eefc138ed904e5eb4e2a916f48f530f15dc0f`.
Implementation commit: [`f043686`](https://github.com/Extender92/CarExpenseCalculator/commit/f043686).
Published as [draft PR #93](https://github.com/Extender92/CarExpenseCalculator/pull/93).
The [PR checks](https://github.com/Extender92/CarExpenseCalculator/pull/93/checks)
show CI for the current head; later documentation-only commits do not alter the tested implementation.
This report describes the branch, not a merged or deployed release. No merge is authorized.

## Acceptance finding

The application retains unconfirmed AI suggestions without opened-page metadata
and carries the new sourced fields through review, shared draft, PostgreSQL,
comparison and the frozen PDF. The remaining blocker is retrieval completeness,
not the former source-evidence gate.

The two final Audi requests both omit owner count **4**, first registration
**1999-11-24**, and sale form **Begagnad bil till salu**. The first final Skoda
request omits the sale form. The second final Skoda request returns **only the
listing ID**, with no useful vehicle content. HTTP 200 and a structurally valid
response do not establish complete extraction.

See the [field-by-field matrix](listing-extraction-reference-matrix.md) and exact
[Audi](../tests/fixtures/listings/audi-a4.json) /
[Skoda](../tests/fixtures/listings/skoda-roomster.json) reference fixtures. The
matrix includes every original description, equipment entry, question/answer,
date, quantity, identity and relevant location/detail field. Expected unknowns
are counted separately in the interpretation: they are not retrieved facts.

| Final request | Correct rows, including expected unknowns | Missing reference rows | Incorrect values |
|---|---:|---:|---:|
| Audi 1 | 62 | 3 | 0 |
| Audi 2 | 62 | 3 | 0 |
| Skoda 1 | 61 | 1 | 0 |
| Skoda 2 | 13 | 49 | 0 |

The second Skoda response exposed a presentation defect: an ID alone counted as
partial vehicle content. The new Core regression retains the ID but classifies
that shape as `unavailable`. The matrix records the **actual original live
response**, before this classification correction. No additional paid/live
request was made to conceal or replace the failed final attempt.

## Live method and complete attempt log

Environment: Codex CLI **0.153.0**, ChatGPT authentication, `gpt-5.6-luna`,
reasoning **medium**. Retrieval context changed from medium to high during
development. Final prompts use high context, explicit specification traversal
and allow opening the same canonical URL without its gallery query. There are
no vehicle-specific production values, automatic retries or second reviewer.

Each row below is a separate request through Nginx → API → sidecar in the
disposable `car-expense-listing-live` stack on loopback port 8091. Only the URL
with `?ci=3` was submitted. Reference text and reference fixtures were never
provided to the live extraction model. At most two requests ran together, one
Codex turn per request. No real advertisement was saved with an invented plate.

| Attempt | Ad ID | Start UTC, 2026-09-10 | Seconds | HTTP | Response UTF-8 bytes |
|---|---|---|---:|---:|---:|
| Development 1 | 26427275 | 13:52:14.700 | 60.992 | 200 | 9484 |
| Development 1 | 26434732 | 13:52:14.718 | 61.182 | 200 | 8470 |
| Development 2 | 26427275 | 13:55:50.890 | 53.148 | 200 | 9235 |
| Development 2 | 26434732 | 13:55:50.908 | 87.663 | 200 | 8290 |
| Development 3 | 26427275 | 14:01:41.598 | 68.185 | 200 | 9779 |
| Development 3 | 26434732 | 14:01:41.615 | 44.878 | 200 | 8532 |
| Final 1 | 26427275 | 14:07:40.638 | 58.670 | 200 | 9717 |
| Final 1 | 26434732 | 14:07:40.677 | 42.566 | 200 | 8266 |
| Final 2 | 26427275 | 14:10:01.570 | 70.832 | 200 | 9872 |
| Final 2 | 26434732 | 14:10:01.590 | 57.234 | 200 | 2065 |

All ten responses lack opened-page metadata. This does **not** block retention
or advertised-tier use, and is not a reason to erase the returned data.
Development 1 also lost blank paragraph separators and included an imprecise
weight label/category; the generic prompt was corrected before the final runs.
Later populated responses preserve the full descriptions, 18/15 equipment
entries and the Skoda debt answer **Nej**. The final Skoda failure remains in
the report instead of selecting only its better earlier response.

An additional isolated diagnostic CLI turn completed in 44.406 seconds. Its
events contained queries/opaque `other` actions, not identifiable completed
open URLs. Its explanation reported a direct-open cache miss and search content
ending before lower specification rows. This explanation is diagnostic, not
independent proof about the source page.

An independent Chromium session opened both submitted URLs with HTTP 200. The
visible source contained the user-supplied owner/registration/sale rows,
equipment and descriptions. No source change was found that explains the
omissions. The page gallery additionally confirmed image counts 10 and 7,
which were not part of the pasted text. Browser inspection was a development
check; no direct browser scraper or alternate production data provider was added.

The earlier CLI 0.154.0 experiment and login/configuration attempts remain in
the [historical source-gate report](listing-extraction-source-gate.md). Its old
blocking policy is superseded by the [current contract](complete-listing-extraction.md).

## Automated requirement evidence

| Requirement | Concrete coverage |
|---|---|
| Login, runtime restrictions, one turn, cancellation, timeout and concurrency | `CodexProcessRunnerTests`, `CodexExtractorEndpointTests`; live runtime pin checked separately |
| True open URLs versus query/opaque/model URLs | `CodexJsonlParserTests`, including the preserved opaque-action regression |
| Both full reference shapes, schema 3 and no opened sources | `CompleteListingSchemaTests` loads both committed fixtures through the real schema/JSONL parser |
| Null, zero, empty collections, labels, precision and limits | `ListingDetailsTests`, `ListingDraftProcessorTests`, `details.test.tsx` |
| Malformed collection response is a typed provider error | `CodexListingExtractionServiceTests.Malformed_detail_collection_is_an_invalid_response_not_an_unhandled_error` |
| Advertised evidence without invented user/register confirmation | `VehicleFactsEvidenceTests`, `ListingDetailsTests`, existing comparison regressions |
| 32,000-character multibyte HTTP payload and exact decimal | `ListingDetailsEndpointTests`; 32,001 characters rejected without creating a vehicle |
| Storage 1 compatibility, storage 2, draft round-trip, revisions and guarded rollback | `ListingDetailsPersistenceTests`, existing migration/listing/household integration tests |
| Same-snapshot listings once outside sensitivity views; manual isolation | `ListingDetailsEndpointTests`, `ListingDetailsPersistenceTests`, existing complete-comparison tests |
| Review → save/draft adoption → comparison → frozen report | `e2e/listing-details.spec.ts`, using fictional ULA100/ULA101 and fake extraction |
| Unchanged calculation/price/score authority and old flows | Full backend, frontend and Chromium suites |
| Full rendered PDF, long content and mobile preview | The practical session below; fake fixture data only |

## Verification environment and commands

Windows host; .NET SDK **10.0.400**, process-local Node **22.22.2**,
PostgreSQL **18**, Chromium through the repository-pinned Playwright.
Automated browser checks use only disposable project `car-expense-e2e`,
fake extraction and loopback port 8092, with one worker. No Unraid database,
user application data or real AI service participates in automated tests.

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run test
npm --prefix src/frontend run build
npm --prefix src/frontend run api:generate
node scripts/verify-compose-boundaries.mjs
npm --prefix src/frontend run e2e -- --project=chromium --workers=1
node scripts/verify-url-analysis-acceptance.mjs
```

OpenAPI is generated from the development API on port 5090, never hand-edited.
The intended additions are listing details/source observation and the listing
attachment on complete comparison responses. No calculation endpoints or
calculation versions change.

| Check | Final result |
|---|---|
| .NET restore/build | Pass; 0 build warnings, 0 errors |
| Backend tests | **1,047 passed**, 0 failed, 0 skipped (576 Core, 51 sidecar, 17 Infrastructure unit, 264 API, 135 PostgreSQL, 4 architecture) |
| Frontend lint/build | Pass, no lint warnings |
| Frontend tests | **279 passed**, 0 failed, 0 skipped; six affected detail tests additionally rerun after presentation refinements |
| Chromium | **70 passed**, 0 failed, 0 skipped; the new complete-listing lifecycle additionally rerun once after presentation refinements |
| OpenAPI | Regenerated successfully; only intended listing/complete-response additions |
| Docker/readiness/CLI pin | Disposable stacks built and explicitly migrated; Nginx readiness passed; CLI 0.153.0 retained |
| Compose boundaries and URL acceptance script | Both passed, including concurrency, isolation and safe logs |
| Final live reference acceptance | **Failed**; four final requests completed, omissions recorded above |

The first complete Chromium run had 69 passing tests and one failed new editor
label assertion. After correcting the label and UTF-8 fixture issue, a focused
run passed and the complete 70-test suite passed. No tests were skipped or
weakened. Later focused runs checked the small Swedish report-label changes;
they are not counted as additional unique tests.

### Practical PDF session

The fresh disposable session used fictitious **TSA100/TSA101** and generated
actual A4-landscape PDFs through Chromium `page.pdf({preferCSSPageSize:true})`.
One report contains both complete listing references and incomplete economic
inputs (83 pages). A second includes a **31,999-character** description,
**100** supplementary specifications and **100** seller answers (123 pages).
These are exhaustive reports; the existing detailed cost/uncertainty appendix
also prints missing values and produces many pages.

Both documents were rendered with PDFium, not checked only by file headers.
Text checks found both vehicle identities/VINs and the seller debt question;
the long report also retained its final description marker, specification 100,
question 100 and answer 100. Character-box checks found **zero characters
outside the page bounds**. Representative first, description, continuation and
last-entry pages were inspected visually: Swedish characters, paragraph breaks,
repeated registration/table headers and column wrapping remained readable.
The 390-pixel report preview was also inspected. Source content is rendered as
text; the frontend regression verifies that a script-shaped description creates
no script element.

The end-to-end regression changed the stored advertisement after capture and
verified unchanged report content and **zero new browser requests during PDF
generation**. No AI calls were made by report opening/printing. Test cars and
matching drafts were removed after the sessions. The final small label cleanup
translates raw annual-tax/unit labels; it changes no captured values or layout.

The PDFium helper emitted its documented `get_text_range` deprecation warning;
this did not affect text/render checks. Playwright also reported that
`FORCE_COLOR` overrides `NO_COLOR`. These are tooling warnings, not suppressed
test failures. The operating system's interactive print dialog was not exercised
in this task; actual PDF generation and visual review were performed.

## Failures and limitations

- Full live extraction acceptance **failed**, as detailed above. A different
  retrieval mechanism or an upstream retrieval correction needs an explicit
  follow-up decision; another prompt cannot be claimed to have resolved it.
- Development verification caught an API record-validation annotation that
  produced HTTP 500 for seller answers, a mislabelled question editor, and an
  incorrectly UTF-8-decoded reference generator. These were corrected, the
  fixtures regenerated with explicit UTF-8, and regression checks rerun.
- Earlier build attempts encountered a development API holding output DLLs
  (MSB3026); the process was stopped before a clean rebuild. These were not
  hidden as successful builds. An initial test expectation used 201 for a 200
  preview response and was corrected to the documented contract.
- `npm ci` reports two high-severity audit entries in the unchanged development
  dependency tree (`js-yaml` and its dependent `@redocly/openapi-core`,
  [GHSA-2883-xcg3-v3hh](https://github.com/advisories/GHSA-2883-xcg3-v3hh)).
  No dependency lockfile or runtime package was changed to conceal the warning.
- A shell request combining background process startup and termination was
  rejected by automatic policy review. Contract generation instead used a
  foreground tool-managed API session, stopped through its own console. An
  initial foreground start from the repository root lacked API configuration;
  restarting in the API directory succeeded.

## Cleanup and publication boundary

The foreground development API was stopped. Both work-owned Compose stacks
(`car-expense-e2e` and `car-expense-listing-live`), their networks and their
disposable database volumes were removed. The live probe's authentication copy
was in tmpfs and disappeared with its container. The original
`car-expense-calculator` app, PostgreSQL volume and authentication volume were
preserved and remained running.

Automatic policy review rejected the direct native PowerShell deletion with
`blocked by policy`. No alternative deletion mechanism was used. These exact
work-owned paths remain:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\listing-complete\
C:\Users\dann_\AppData\Local\Temp\Microsoft.NET.Workload_15876_20260910_152334_189.log
C:\Users\dann_\AppData\Local\Temp\Microsoft.NET.Workload_29232_20260910_152445_143.log
C:\Users\dann_\AppData\Local\Temp\Microsoft.NET.Workload_34304_20260910_152352_264.log
```

The repository temp directory contains this task's helpers, logs, reference
responses, PDFs, screenshots and process/test caches, all ignored by Git. The
three SDK logs identify this repository's build commands. Windows Temp was
also inventoried; generic empty folders, editor/compiler caches, background
installer logs and unrelated temporary files were left intact where ownership
could not be established. Earlier deferred inventories were not touched.

This work is suitable for a **draft review only** while live completeness is
blocked. Passing automated CI does not change that acceptance finding. No issue
is closed and no merge is performed. A follow-up retrieval decision is required
before the full user-requested delivery can be marked complete.
