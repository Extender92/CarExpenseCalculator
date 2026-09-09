# Stage 3B verification report (#67)

## Decision and tested revision

The local stage acceptance checks passed on **2026-09-09**: **1,012 backend,
273 frontend and 69 Chromium tests**, with zero failed/skipped tests in the
final complete runs. A new practical session inspected generated PDFs and the
native print dialog. No product defect or unresolved product decision was
found. Six browser regressions join previously separate feature flows; no
production code, dependency, HTTP contract, calculation or migration changed.

The tested code and tests are commit
`4068820c39a2daf46561ca0bfb96ba048ed1a9dc` on
`chore/67-comparison-stage-acceptance`. It is based on merged preparation
`16f6981f34f7d858717f56dacd56034a98a0ca5d` / PR #90 and its successful
[main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34364427068).
Subsequent documentation commits record this execution without changing its
tested implementation. PR CI and publication are recorded below before delivery.
This is branch acceptance evidence; separate merge approval and a final review
of #12 and milestone 3B are still required. Neither tracker nor milestone is
closed by this report.

The [preparation matrix](comparison-stage-3b-preparation.md) and
[acceptance procedure](household-comparison-verification.md#practical-stage-3b-acceptance)
define the scope. Dependencies #61–#66 and #85, and preparation #90, were
rechecked as merged before #67 changed from ready to in progress. Versions
remain comparison/rule/complete transport **1**, household **2**, storage **1**.

## Environment and executed checks

Local Windows/PowerShell, Europe/Stockholm; .NET SDK **10.0.400**; Node
**22.22.2**, npm **10.9.7**; Docker Engine **29.5.3**; PostgreSQL **18**;
Playwright **1.62.1** Chromium. Node was selected through process-local PATH
from the existing `temp/issue64/node-v22.22.2-win-x64` installation, without
modifying it or machine settings. Process TEMP/TMP and npm cache were directed
to `temp/issue67/acceptance/` where supported. Windows commands used `npm.cmd`
because the unsigned `npm.ps1` wrapper is prohibited by the local policy.

| Check | Observed result |
| --- | --- |
| `dotnet restore CarExpenseCalculator.sln` | Passed. |
| `dotnet build CarExpenseCalculator.sln --configuration Release --no-restore` | Passed, **0 warnings, 0 errors**. |
| `dotnet test CarExpenseCalculator.sln --configuration Release --no-build` | **1,012 passed / 0 failed / 0 skipped**: Core 562, API integration 262, Infrastructure integration 129, extractor unit 39, Infrastructure unit 16, architecture 4. Disposable PostgreSQL via Testcontainers. |
| `npm --prefix src/frontend ci` | Passed; existing development-tool advisories recorded below. No lockfile changes. |
| `npm --prefix src/frontend run lint` | Passed with `--max-warnings 0`. |
| `npm --prefix src/frontend run test` | **273 passed / 0 failed / 0 skipped**, 26 files. |
| `npm --prefix src/frontend run build` | TypeScript/Vite build passed. |
| Separate TypeScript check of new E2E files | Passed against generated API types and existing Node types. |
| `node scripts/verify-compose-boundaries.mjs` | Passed. |
| API at port 5090; `npm --prefix src/frontend run api:generate`; `git diff --exit-code -- src/frontend/src/api/schema.d.ts` | Passed; generated schema unchanged. API stopped afterward. |
| README's ordered Docker sequence | Built production/fake images; version-only CLI check returned **0.153.0**; started PostgreSQL, explicitly migrated, then started API/web; readiness passed through Nginx. |
| `npm --prefix src/frontend run e2e -- --project=chromium --workers=1` | **69 passed / 0 failed / 0 skipped**, 2.9 minutes; no retries in this final local run. Baseline was 63. |
| `node scripts/verify-url-analysis-acceptance.mjs` | Passed state, concurrency, isolation and safe-log checks after the full browser run. |
| New practical report session | Four real multipage PDFs, image and full-text checks; headed Chromium print/cancel/retry, keyboard navigation, retained edits and 390px inspection completed. |

The exact Compose project was **`car-expense-e2e`**, with `compose.yaml` and
`compose.e2e.yaml`. No pre-existing project containers/volumes were present.
Only the synthetic private extractor ran; the real CLI was checked for its
version without an extraction/authentication call. No Unraid data, credentials,
real AI service or live marketplace was used.

## Criterion catalogue: editor, HTTP and Swedish presentation

The new [acceptance suite](../src/frontend/e2e/comparison-acceptance.spec.ts)
creates fictional TAA100 with a 12-month January-2026 profile, 12,000 km/year,
100,000 SEK purchase cash, a 12,000 SEK purchase and zero fixed residual at
12 months. All operating collections are explicitly empty, petrol is explicitly
priced at zero, and repair reserve is zero. Startup/monthly budgets are 0/2,000.
Cost assumptions are expressly confirmed. Evaluation date is **2026-09-09**.
Every rule and preference below is created through the actual editor with
explicit `userConfirmed` evidence. Active preference weights are 1.

| Criterion | Actual input / displayed unit | Hard condition; preference 0 → 100 anchors or choice | HTTP result / score |
| --- | --- | --- | --- |
| Purchase price | 12,000 SEK | Minimum 12,000; 0 → 24,000 | pass / 50 |
| Odometer | 200,000 km = 20,000 Swedish mil | Minimum 20,000 mil; 0 → 40,000 mil | pass / 50 |
| Owners | 3 | Minimum 3; 0 → 6 | pass / 50 |
| Seats | 5 | Minimum 5; 2 → 8 | pass / 50 |
| Model year | 2010 | Minimum 2010; 2000 → 2020 | pass / 50 |
| Braked towing capacity | 1,500 kg | Minimum 1,500; 0 → 3,000 | pass / 50 |
| Inspection validity | 2026-10-09 = 30 remaining days | Minimum 30; 0 → 60 days | pass / 50 |
| Net ownership cost | 12,000 SEK from cost engine | Minimum 12,000; 0 → 24,000 | pass / 50 |
| Monthly cost | 1,000 SEK from cost engine | Minimum 1,000; 0 → 2,000 | pass / 50 |
| Cost per mil | 10 SEK from cost engine | Minimum 10; 0 → 20 | pass / 50 |
| Tow bar | Explicit false | Nej | pass / 100 |
| Transmission | Automatic | Automat | pass / 100 |
| Fuels | Petrol and electricity | El; set overlap | pass / 100 |
| Body | Wagon | Kombi | pass / 100 |
| Drivetrain | All-wheel drive | Fyrhjulsdrift | pass / 100 |
| Locality | Örebro | Trimmed, upper-case, decomposed `O` + combining diaeresis + `REBRO` | pass / 100 |
| County | Örebro län | ` ÖREBRO LÄN ` | pass / 100 |
| Service | Documented | Dokumenterat | pass / 100 |
| Startup budget | Known funding 0 against limit 0 | Within budget; no preference available | pass / no score |
| Monthly budget | Known cash funding 0 against limit 2,000 | Within budget; no preference available | pass / no score |

All three API views contain the same car, 20 passing hard rules and 18
contributions. Independent total `(10 × 50 + 8 × 100) / 18` displays
**72.22** with **100%** coverage. The Swedish screen shows 20 **Uppfyllt**
results and `[72,22, 72,22]`; the saved rules and captured report retain them.
Depreciation contributes cost but is not a cash payment, explaining the monthly
budget's zero funding in this fixture.

TAA101 separately checks inspection dates 2026-09-08, 2026-10-08,
2026-10-09 and 2026-10-10: **−1/29/30/31 days** against a 30-day limit.
HTTP returns fail/fail/pass/pass; the screen shows Bortvald/Bortvald/Godkänd/
Godkänd. A 0 → 100-day preference gives 0/29/30/31 points. The short-validity
signal applies only below 30. Requiring registry evidence changes the last
result to **Behöver verifieras**, **[0,100]**, **0%** coverage, without a cheapest
mark, also in the report. No manual registry verification is manufactured.

## Fixed reference results

The exact fixture constants are in the
[comparison examples](comparison-and-buying-scores.md#worked-examples) and
[household examples](household-calculations.md#worked-examples-and-acceptance).
Existing [comparison browser cases](../src/frontend/e2e/comparison-workspace.spec.ts),
[Core examples](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonEvaluatorTests.cs)
and [HTTP examples](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPreviewEndpointTests.cs)
were rerun, rather than duplicating the calculation engine as a reference.

| Case and exact defining inputs | Expected = observed |
| --- | --- |
| B1: price 40,000, anchors 100,000 → 20,000, weight 3; preferred automatic, weight 2 | Price 75, transmission 100, total **85**, coverage **100%**, in HTTP/UI/report. |
| B2: B1, unknown or insufficiently evidenced transmission | **[45,85]**, coverage **60%**; unknown weight remains in denominator. |
| B3: transmission weight 0, then both weights 0 | **75 / 100%**; then null score/coverage and no active criteria. |
| B4: price 75, unwanted transmission 0; weights 3:2 → 1:4 | **45 → 15** with unchanged facts. |
| B5: X [45,85], Y [60,80] | Y before X; overlap, no definite winner. |
| B6: eligible A 30,000; eligible B 25,000; partial C known 10,000; rejected D complete 20,000 | **B,A,C,D**; cheapest mark on **B only**. |
| B7: price 20,000 at max 20,000; 200,000 km at max 20,000 mil; unknown owners; required tow bar confirmed false | Inclusive limits pass; owners need verification; tow bar fails. |
| B8: add a 1,000 SEK candidate/remove another candidate | Existing B1 contributions and score remain **75/100/85**. |
| A1: cash 30,000; prices 25,000/30,000/80,000 | Applied cash 25,000/30,000/30,000; loans **0/0/50,000**. |
| A2: purchase 80,000, cash 30,000, loan 10 months, period 12, 0%, setup 500, monthly fee 25, residual 60,000 | Cost **20,750**, external outflow **80,750**, fees **750**; no loan payments/fees after month 10. |
| A3: price 100,000, 10% yearly depreciation, 12/24/6 months | Residual **90,000 / 81,000 / 94,868.33**. |
| A4: fixed residual 60,000 at 24 months, change to 36 then restore | Horizon mismatch blocks dependent totals while retaining known operating costs; restored period makes residual applicable again. |
| A5: 12,000 km; 60% electric; battery 18 kWh/100 electric km; petrol 6 L/100 remaining km; 10% charging loss; 80% home at 2, rest at 5; petrol 20 | Purchased electricity 1,440 kWh / 3,744 SEK; petrol 288 L / 5,760 SEK; total **9,504**. |
| A6: same distance/prices; whole-distance metered 10 kWh and 3 L per 100 km | 1,200 kWh, 360 L; total **10,320**, without extra weighting/loss. |
| A7: January start, 6 months; annual tax 1,200 due March | Cost **600**; March payment **1,200**; monthly funding contribution **200**. |
| A8: 12 months; service 1,200 in month 4, repair 2,400 in month 2, reserve 300/month | Cost **7,200**, workshop payments **3,600**, internal saving **3,600**, average funding **600**. |
| A9: 24-month lease, 6,000 upfront, 2,000/month, 24,000 included km, 15,000 annual km, 1/km excess, 3,000 deposit/refund | Cost **60,000**, outflow **63,000**, refund **3,000**, startup **9,000**, average funding **2,250**. |
| A10: A9 at 12/36 months | Known outflows **33,000/63,000**, refunds **0/3,000**, no complete comparable total; no assumed successor agreement. |
| A11: 2,400 in month 2 over 12 months, separate 500 at start | Average **200** passes limit 200, exceeds 199; startup remains separate. |

All A1–A11 exact inputs and the independent positive-interest amortization
reference passed again in [household acceptance](../src/frontend/e2e/household-acceptance.spec.ts).
The [report suite](../src/frontend/e2e/comparison-report.spec.ts) follows A2,
A5/A6, A8/A9 and B1/B2 into captured report values; other lower-layer boundary
and precision tests remain intact.

## Joined lifecycle and requirement evidence

| Acceptance requirement | Newly observed behavior and retained regression evidence |
| --- | --- |
| Listing → cost → confirmation → comparison → report | New TAA102 flow: proposal alone leaves [45,85]; explicit adoption gives 85 with unverified listing observation at version 1. Saving price 40,000 → 35,000 clears cost confirmation, giving [40,100]; separate confirmation gives **88.75**. Saving missing price preserves null instead of filling from the listing. Replacing listing with version 2/manual leaves the adopted version-1 automatic fact and review marker intact. The report remains unchanged after a later independent fact write and focus event, with no report API requests. |
| Two-browser save/conflict/recovery | New TAA103 flow: local price weight 1, other browser saves 5; first browser save returns **409**, keeps 1 and disables report. Explicit read/review/keep gives **91.67** with unsaved rules; report marks them. Returning and explicitly saving allows the second browser to reload 91.67. Existing [workspace state tests](../src/frontend/src/features/comparison/workspace.test.ts) retain late responses, edits during save, current-generation and baseline tests. |
| Legacy 50+50, corrupt results and atomic transition | New TAA104/TAA105 use different annual km (11,111/22,222), period 24, price 20,000/residual 15,000, ambiguous energy/annual combined maintenance 1,000. First car has 50 monthly amounts 1…50 and 50 undated once amounts 1…50. Owned legacy results are deliberately version 999 / `{}`. Original inputs remain readable; one transition sends both cars. Fifty recurring mappings yield known custom cost **30,600**; fifty one-time keys stay unresolved in comparison/report. Total stays null, score [0,100], budget is not approved. Explicitly mapping one 1-SEK item to repairs/month 1 leaves **49** unresolved, adds exactly **1**, and leaves custom cost 30,600. |
| SQL isolation and fixture retention | [Shared legacy helper](../src/frontend/e2e/legacy-test-data.ts) extracts the existing household guard: exact project, running fake service, no real extractor, exactly two owned UUIDs and `ON_ERROR_STOP`. No arbitrary SQL target or endpoint was added. New fixtures register IDs immediately, attempt all deletions/restores after assertion failures, and close the second context in `finally`. |
| Complete inventory, exact order and pagination | Existing [complete Core tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/CompleteComparisonTests.cs), [snapshot tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/CompleteComparisonSnapshotTests.cs), [HTTP tests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/CompleteComparisonEndpointTests.cs), workspace/report browser cases passed for **0/1/50/51/100/101/250** at their appropriate layers. All views retain membership; 50-car pages share order; off-page winners and full reports survive. Raw differences before display rounding, ties and overlapping intervals retain server authority. |
| Deletion and both common profiles | New TAA106 joins cost, adopted listing, facts, comparison/report and matching draft. URL deletion returns 204; cost/facts/listing/legacy resources return 404. Draft clears with increased revision; stale adoption returns 409. Both complete shared-profile responses remain equal. The comparison has zero candidates and no report. Existing household, legacy and report-deletion tests retain the other deletion/event routes. |
| Manual isolation | Existing manual comparison/report browser and API tests passed without storage; mode is explicitly selected. No automatic switch on storage failure, AI call or hidden report save. |
| Partial/error generation and transport | Existing workspace/report state tests, [browser API tests](../src/frontend/e2e/complete-comparison-api.spec.ts) and HTTP tests passed for missing views, wrong counts/identity, late or truncated responses, UTF-8 size, direct Kestrel/Nginx and chunked limits, busy/timeout, baseline changes and global sorting. Incomplete generations never become current recommendations. |
| Precision, evidence and independent failures | Core/API/frontend coverage passed for exact decimal/revision values, mil/km, all fact states, confirmation lifetime, complete/partial costs, budgets and unchanged unrelated criteria after numeric errors. No client recalculation or register claim was introduced. |
| Keyboard, focus and Swedish/mobile presentation | New lifecycle asserts the economic price link opens and focuses the actual field. Existing keyboard/error/mobile browser cases passed. The new headed session additionally used keyboard report/return navigation, inspected focused headings and a 390px report, with no outer viewport overflow. Swedish incomplete/unsaved messages and text wrapping were visually reviewed. |

## Fresh PDF and native-print observations

Fresh fictional **STG101–STG108** fixtures were created through existing APIs
on the disposable stack. API responses, report text and PDFs were captured
together; every case used explicit date **2026-09-09**. This is a new session,
not a reuse of #66's output files. Existing local PDF tools were read-only:
PDFium rendered images and pypdf extracted text and page dimensions.

| Generated case | Fresh inputs and observations |
| --- | --- |
| `purchase-partial.pdf`, **71 pages** | STG101 exact A2; STG102 maintenance amount variant (annual service 1,200 due March, repair 2,400 in month 6, reserve 300/month); STG103 missing residual/insurance. API and extracted PDF preserve 20,750/80,750 and 7,200/3,600/3,600. The amount variant complements, rather than replaces, the exact A8 month-4/month-2 browser fixture. Known versus complete values and missing-price/zero-distance reasons remain explicit. |
| `lease.pdf`, **34 pages** | STG104 exact A9, including included energy; 60,000 cost, 63,000 outflow, 3,000 refund, 9,000 startup and 2,250 average funding. Deposit/start/monthly/end entries remain separate. |
| `hybrid-energy.pdf`, **39 pages** | STG107/STG108 exact A5/A6 energy inputs, with zero other costs; **9,504 / 10,320** retained in all views and printed details. |
| `long-calendar-legacy.pdf`, **337 pages** | STG105 period 120 with monthly 1-SEK custom item, long source URL and Swedish note; STG106 older period-24 scenario with all 50+50 labels, deliberately still awaiting transition. Both calendars have **121 entries in every API view**. Full extracted PDF contains months 0…120, all 50 recurring and all 50 once labels and stable review keys. No truncation or record cap. The separate new browser case above tests actual atomic conversion. |

All pages are A4 landscape (approximately **841.92 × 594.96 PDF points**).
Full-page glyph-bound checks found **zero characters outside page bounds**.
First, second, middle and last pages were rendered for each PDF. Visual review
covered purchase pages 1/71, lease 1/18, hybrid 1/20 and long-report
33/169/208/237/337, including the repeated calendar headers, end metadata,
last recurring/once items, long URL and Swedish notes. Columns fit; long text
wraps; tables continue with repeated registration/header context. The long
report is intentionally large because gaps and all three views are preserved.
No claim is made that 337 individually inspected images were reviewed.

In a fresh **headed Chromium** session, the real `chrome://print/` dialog was
opened, cancelled and reopened. **Spara som PDF**, **Alla** and the CSS-defined
A4-landscape preview were checked; browser headers/footers were disabled on
the second attempt. Chromium's CSS-controlled paper/orientation did not expose
separate size/orientation selectors in that dialog. Cancellation retained a
print-ready report; it was never treated as a saved PDF.

Between attempts, keyboard navigation returned to comparison. The period
changed **12 → 6 → 12**, giving missing dependent total then **20,750** again.
An unsaved **7,000** monthly budget was captured in a new report. Another API
client changed saved rules; a focus event made no API calls and the captured
article stayed byte-for-byte equal. At **390 × 844** pixels the page had no
outer horizontal overflow and the heading/buttons/unsaved explanation remained
readable. After the second cancellation and return, the local budget still
read **7000**. Existing automated report isolation tests additionally verify
printing without API/AI effects, report invalidation and storage-free manual use.

Physical printers, other browsers, OS save-file dialogs and Unraid deployment
were not tested; they are outside this local Chromium acceptance. No required
practical check is being substituted with historical evidence.

## Warnings, reruns and defects

No product correction was required and no test was removed, weakened or skipped.
Development of the new fixture initially failed on an obsolete category shape,
two incorrect generated-property names, a textContent/innerText assertion and
a DELETE response selector that omitted the query string. Those test errors
were corrected; focused runs then passed and the complete 69-test suite passed
without retries. The new tests use independently fixed expected values.

The temporary native-inspection helper initially surfaced the expected closure
of the print-dialog page during cancellation as an exception; the dialog had
cancelled successfully. Explicit post-cancel inspection and the corrected
second attempt passed. A PDF inspection helper's wrong property name was fixed;
the subsequent full content/bounds checks passed. Neither issue affected the
application. A temporary inline shell invocation had a quoting error and was
replaced by stdin-fed JavaScript before process cleanup.

Existing environment notices remain: `npm ci` reports **two high advisories**
in development OpenAPI tooling (js-yaml / @redocly/openapi-core), and npm offers
a newer major version. No runtime dependency or lockfile was changed.
Playwright emits its existing NO_COLOR/FORCE_COLOR notice. On the empty
database, explicit migration logged the expected absent history-table probe;
readiness initially returned 502 before the configured retry succeeded.

The execution tool rejected a detached API `Start-Process` with `blocked by
policy`, without a detailed reason. A foreground tool-managed API session
performed the contract check and was stopped. The headed helper's tool session
had no writable stdin for its final prompt; its browser/process and one
remaining owned car were explicitly stopped/deleted before the disposable
stack was removed. There is no remaining process or test-data dependency.

## Cleanup and delivery boundary

All owned cars were removed (the final inventory was empty), and the
`car-expense-e2e` containers, PostgreSQL/Codex-home volumes and both created
networks were removed. No owned listener remained on **5090, 8088 or 9229**;
no task-created Node/.NET process remained. Windows Temp was inspected for
identifiable work output; recent zero-byte GUID files without attributable
ownership were left alone. No identified work-owned file outside the issue
directory remained to remove.

Native PowerShell removal of the resolved, checked directory below was rejected
by automatic execution approval with **`blocked by policy`**, without further
reason. No alternate deletion mechanism was used. This cleanup remains for
manual action; the files are ignored and are not in any commit:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\issue67\acceptance\
```

At inventory it contained **2,752 files / 86,909,921 bytes**: run logs, helper
scripts, four PDFs, screenshots/renderings, captured fictional API/text data,
test results and process caches (npm, Playwright, Node, NuGet/compiler and SDK
workload temporary output). Publication may add small PR-body/link-check files
under the same directory. This is the complete new work-owned cleanup root.
The three prior files in `temp/issue67/preparation/` and all older deferred
inventories, including the read-only Node/PDF tools, remain untouched.

Local acceptance and the practical session are complete. Delivery additionally
requires published report/tests and all ordinary PR CI checks green. After
separate approval to merge, audit the stage and this evidence before closing
tracker #12 and milestone 3B. Registry integration, advisory AI and later/paused
work remain outside this acceptance.
