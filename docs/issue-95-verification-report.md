# Issue #95 verification report

## Scope and status

The implementation on `feature/95-review-and-calculation-workflow` adds compact
review dialogs, persistent registration-free drafts, explicit confirmation,
deterministic listing reuse and individual electric-driving shares.
The [workflow contract](review-and-calculation-workflow.md) records the HTTP,
evidence, storage and migration behavior. The pull request's checks record
published CI acceptance. Merge and server deployment are excluded.

Baseline: `4be098db27ef41d89de41ea9fb392598fed2bbe0`, with **1,132 backend,
285 frontend and 72 Chromium tests**. Verification used .NET **10.0.400**,
Node **22.22.2**, PostgreSQL **18**, a fake extractor and one Chromium worker.
Disposable Compose project: `car-expense-e2e`, port **8091**, separate network
`car-expense-issue-95`. No real marketplace or AI calls were used.

## Final local checks

| Check | Result |
| --- | --- |
| Backend restore / Release build | Passed; zero compiler warnings/errors |
| Backend tests | **1,169 passed, 0 failed, 0 skipped** |
| Frontend npm ci | Passed; existing audit findings below |
| Frontend lint / build | Passed; zero lint or build warnings/errors |
| Frontend tests | **305 passed, 0 failed, 0 skipped**, 30 files |
| Full Chromium suite, one worker | **77 passed, 0 failed, 0 skipped** |
| OpenAPI | Regenerated from running API; repeat generation produced the same hash |
| Compose boundaries | Passed |
| Fake URL acceptance | Passed: outcomes, concurrency, explicit retries, isolation and safe logs |
| Streaming log-check regressions | **3 passed, 0 failed, 0 skipped** |
| Database | All eight migrations applied explicitly to disposable PostgreSQL 18 |
| Physical printer / server deployment | Not performed; outside scope |

Backend totals: Core **603**, Infrastructure unit **35**, architecture **4**,
extraction **100**, API **281**, PostgreSQL integration **146**.

The unchanged frontend lockfile reports **two high-severity audit findings**:
`js-yaml` and its transitive `@redocly/openapi-core` chain in OpenAPI tooling.
No dependencies were changed by this issue. These findings remain open.
Diagnostic Playwright runs also reported a `NO_COLOR`/`FORCE_COLOR`
environment conflict; the final run removed redundant `NO_COLOR`. Windows Git
emitted LF/CRLF conversion notices.

## Acceptance evidence

- Multiple registration-free drafts survive reload without entering comparison.
  Tests cover duplicate page identity, reviewed replacement, both revisions,
  atomic adoption, failed adoption and deletion.
- Optional missing values remain saveable; invalid supplied values fail.
  Zero, false and explicitly empty collections retain their meanings.
- Editing is unverified. Explicit confirmation, later edits, legacy confirmations
  and stronger evidence requirements have regressions. Saving never supplies
  registry verification.
- Reuse covers price, tax, fuel, unambiguous consumption and supported facts,
  explicit replacement, exact decimals, source claims, repeat application,
  draft adoption and unchanged calculation-price authority.
- Two fictional hybrids use **20%** and **80%** independently. Their controlled
  twelve-month energy totals are **8,400 SEK** and **3,600 SEK**. Tests also cover
  inheritance, explicit unknown override, complete sensitivity trios, limits
  and unchanged single-fuel/whole-distance behavior.
- Dialog checks cover separate/sequential saves, acknowledged revisions, partial
  failure, edits during save, discard, nested navigation and saved-car conflicts.
  Chromium covers Escape, Back/Forward, focus containment, accessible save actions
  and no page overflow at **390 × 844**. External deletion never silently
  recreates a car.
- Compatibility tests read old listing/cost/fact formats. Downgrade checks reject
  new drafts and payloads while preserving schema/data. Old migrations are unchanged.
- Comparison/PDF retain the captured complete listing and sources; later writes
  and printing do not replace that capture.

## Practical PDF and printing

Headed Chromium created and removed two fictional test cars on the disposable
stack. Each had 36 long Swedish description paragraphs, equipment and unverified
HTML evidence. The frozen report held **57,134 text characters**. The actual PDF
had **52 A4 landscape pages**, approximately **841.92 × 594.96 points** each.

Extracted text contained both final paragraphs, all 72 occurrences of the Swedish
inspection phrase, both equipment lists, both **Handlare** labels, 20%/80% with
vehicle origin and final version metadata. No extracted glyph lay outside its
page. Visual checks covered the first page, energy details, both description
endings, repeated headers and final page. A dark unused area below the final
section was found and fixed with an explicit white print-root background; the
regenerated PDF was checked.

The ordinary Chrome print preview was opened, cancelled and reopened, using
**Save as PDF**, all pages and an A4 landscape preview. Captured text remained
identical and both attempts made **zero API requests**. PDF generation used
Chromium's PDF output. No physical printer or operating-system file-save dialog
was tested. The report also fit a 390-pixel mobile viewport.

## Execution history

Initial backend runs: 1,108 passed/37 failed, then 1,144 passed/1 failed, then
1,166/1,166. Updated version assertions and integration fixes preceded additional
compatibility/downgrade tests, bringing the final total to 1,169.

Browser verification found obsolete inline-form selectors, an old household
response-version gate, background actions blocked by native dialogs, duplicate
save controls and dialog timing. Intermediate subsets passed 25/37 and 23/28;
full runs passed 62/75, 60/76 and 76/77. Failed-run fixture leftovers also affected
intermediate results. Two full 77-test runs then passed.

An added mobile Forward regression subsequently found a history entry replaced
during blocked Back navigation. Dialog closing now leaves URL transitions to the
router. The test also waits for completed Back navigation before requesting
Forward, rather than racing the pending history transition. Focused repeated and
full reruns verify the fix. A household test now inspects the reopened control
instead of a detached input. A test-only TypeScript locator-option error was
caught by the build and fixed. No test was removed or skipped to obtain a pass.

Accumulated logs from repeated suites exceeded the acceptance helper's 128 MiB
buffer. It now scans all log chunks with bounded overlap, preserving every
forbidden-content check. Three utility regressions cover split matches, safe
content and inspection beyond 128 MiB; CI runs them before URL acceptance.

Initial published CI passed, but both checks on the cleanup-documentation commit
reported one flaky URL lifecycle test (two retries). Its helper enumerated hidden
cost-tab summaries while reviewing the listing tab; timing of background loading
determined whether they were present. A strengthened regression preloads that
hidden form before reopening. Reintroducing the old selector reproduced a click
timeout on hidden **Energi**; scoping expansion to the **Annons** panel passed
three consecutive focused runs. The correction changes the test helper, without
relaxing assertions or increasing timeouts. Full local and published checks were
rerun after the correction.

Final presentation review found raw kilometre values under the reuse preview's
Swedish mil label and untranslated typed choice values. The preview now uses
exact decimal conversion and the existing Swedish choice catalogue. Four unit
regressions cover conversion precision, choice labels, missing/zero/false/empty
values and unchanged free text; the browser reuse test checks the visible unit
and translated fuel value. This increased the frontend total from 301 to 305.

Early native-print helpers timed out because Chrome's internal print target is
not a normal Playwright page. One interrupted helper left two fictional fixtures;
they were removed by exact identity/revision before retry. Using the browser
print target allowed successful cancel/retry checks. Interrupted diagnostics are
not counted as passes.

One test command mistakenly targeted the ordinary local application port. It was
interrupted. Its single newly created fictional car and first profile/rule writes
were identified by exact identity, payload and revisions. Guarded cleanup restored
the prior empty inventory and null/revision-zero profiles. No existing user records
were overwritten. This was a verification error, not an acceptable test target.

A new Playwright guard checks, before fixture writes, that the URL matches the
disposable web container's published port, its API uses fake extraction and the
inventory is empty. A deliberate wrong-port run was rejected before any tests.
The same guard applies in CI.

## Cleanup and delivery

The disposable stack's four containers, database volume, two networks and four
test image tags were removed. No work-owned browser or API process remains.
The feature worktree and branch remain available for review.

Automatic execution policy rejected bulk removal of helper artifacts with
`blocked by policy`; deletion was not retried through another mechanism. Helpers,
screenshots, PDFs, logs and tool downloads under `temp/issue-95/` therefore remain.
The local `temp/issue-95/cleanup-manifest.txt` lists exact absolute paths, including
build outputs, a diagnostic `temp/chromium-mobile-release/` directory and its log,
and three identified Playwright temp directories. Do not remove the feature
worktree's source while cleaning helpers. This is a cleanup limitation, not an
unreported successful deletion. Existing local documentation edits, private
installation notes, older deferred inventories and backups are preserved.

The PR includes this report, migration and generated types. Registry lookup and
advisory AI remain separate work. Merge and installation updates require separate
approval.
