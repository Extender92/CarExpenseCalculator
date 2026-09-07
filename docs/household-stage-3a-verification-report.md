# Household stage 3A verification report

## Decision and provenance

Stage 3A acceptance was executed on 2026-09-07 for
[#61](https://github.com/Extender92/CarExpenseCalculator/issues/61).
The automated and practical checks below pass. This is evidence for the
acceptance branch, not authorization to merge or close the GitHub milestone.
Stage 3B starts only after the approved acceptance PR is merged and its
dependencies are audited.

- Baseline: `8918f7e7fb04b1393e025b64de11945a2102934f` (merged #60 / PR #77).
- Tested implementation and tests: `4d35813` on
  `chore/61-household-stage-acceptance`. Subsequent report/status commits do not
  change the tested product or tests.
- Acceptance source: [3A practical procedure](household-comparison-verification.md#practical-stage-3a-acceptance)
  and [normative examples A1–A11](household-calculations.md#worked-examples-and-acceptance).
- Public contracts are unchanged: calculation/result version 2, storage version
  1, unchanged generated OpenAPI declarations, no new migrations.

The primary acceptance evidence is the real
[household browser acceptance suite](../src/frontend/e2e/household-acceptance.spec.ts).
It performs HTTP-backed browser actions against PostgreSQL through Nginx and
checks API values and Swedish rendered output. Expected amounts are constants
from the specification and independent mathematical references, not results
obtained by calling the production calculator as a test oracle.

## Environment and commands

Windows, .NET SDK 10.0.400, Node 22.22.2, npm 10.9.7, Docker Engine 29.5.3,
PostgreSQL 18.6, Playwright 1.62.1, Chromium 151.0.7922.34.
The disposable `car-expense-e2e` Compose project used the existing fake extractor.
Its application network was explicitly named `car-expense-e2e-app` locally.
No ordinary local or Unraid database was used. Only the Nginx port was published.

Commands run from the repository root (Windows used `npm.cmd`):

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run test
npm --prefix src/frontend run build
node scripts/verify-compose-boundaries.mjs
```

OpenAPI was generated from a separate temporary API process:

```bash
dotnet run --project src/backend/CarExpenseCalculator.Api --configuration Release --no-build --no-launch-profile -- --urls http://localhost:5090
npm --prefix src/frontend run api:generate
git diff --exit-code -- src/frontend/src/api/schema.d.ts
```

The isolated [README procedure](../README.md#verification) built all images,
checked `codex-cli 0.153.0` without starting extraction, started PostgreSQL,
applied the migration explicitly, started API/web and checked readiness through
Nginx. A local empty environment file and a network-only override under
`temp/issue61/` isolated configuration from any ordinary `.env` file.

```bash
npm --prefix src/frontend run e2e -- --project=chromium --workers=1 --output=../../temp/issue61/playwright-results
node scripts/verify-url-analysis-acceptance.mjs
```

The existing ordinary CI discovers the new tests without workflow changes.
The acceptance PR must pass all four groups: backend, frontend, OpenAPI and
Docker/browser. Published evidence is available in [PR #78 checks](https://github.com/Extender92/CarExpenseCalculator/pull/78/checks)
and the [initial PR workflow run](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34108946950).
The PR checks show the current head, including any subsequent report-only update.

| Check | Passed | Failed | Skipped | Observation |
| --- | ---: | ---: | ---: | --- |
| Backend | 699 | 0 | 0 | Core 376; extractor 39; architecture 4; Infrastructure unit 16; Infrastructure PostgreSQL 96; API 168. |
| Frontend | 194 | 0 | 0 | 19 files; baseline 190 plus four regression/contract checks. |
| Chromium | 33 | 0 | 0 | Baseline 24 plus nine complete-flow acceptance cases; one worker; no retries in the successful local run. |
| Lint/build | Pass | 0 | 0 | Backend build: zero warnings/errors. Frontend lint/build succeeds. |
| OpenAPI | Pass | 0 | 0 | No generated-schema diff. |
| Compose/readiness | Pass | 0 | 0 | Intended network/port boundaries and healthy PostgreSQL through Nginx. |
| Fake acceptance | Pass | 0 | 0 | Required extraction outcomes/concurrency, fake isolation and safe logs. |

## Worked examples: expected and observed

All registrations below are synthetic fixtures, labeled **Fiktiv acceptansbil**.
Amounts are SEK. Unless an example overrides them, the fixtures use January
2026, 12 months, zero annual distance, explicit zero operating collections and
repair allowance, no financing fees and sufficient purchase cash. Zero distance
isolates non-energy examples; per-mil output is then explicitly unavailable.
Energy examples use exactly the documented distance. The complete executable
inputs are in the linked acceptance suite.

| Example / fixture | Explicit inputs | Expected | Observed |
| --- | --- | --- | --- |
| A1 / SAA001–003 | Cash 30,000; prices 25,000 / 30,000 / 80,000. | Cash 25,000 / 30,000 / 30,000; principal 0 / 0 / 50,000; unused cash 5,000 / 0 / 0. | Exact match. Editing shared cash to 10,000 gives principal 15,000 / 20,000 / 70,000 together. |
| A2 / SAA003 | Price 80,000; cash 30,000; loan 10 months at 0%; horizon 12; setup 500; monthly fee 25; residual 60,000. | Cost 20,750; outflow 80,750; principal repaid 50,000; no payments or fees in months 11–12. | Exact match, including reconciliation and rendered calendar. |
| Positive rate / SAA004 | Principal 50,000; annual nominal rate 6%; term 24; horizon 12; no fees; residual zero. | Installment 2,216.03; payments 26,592.37; principal repaid 24,252.09; interest 2,340.27; remaining debt 25,747.91. | Exact rounded match; negative end equity −25,747.91 retained. |
| A3 / SAA005 | Price 100,000; annual depreciation 10%; horizons 12 / 24 / 6. | Residual 90,000 / 81,000 / 94,868.33. | Exact match through profile edits and Swedish output. |
| A4 / SAA006 | Price 80,000; fixed residual 60,000 at 24 months; insurance 100/month; change to 36 months. | Total unavailable; `residualHorizonMismatch`; insurance 3,600 remains known; entered horizon stays 24. | Exact match. Returning to 24 months restores total 22,400. |
| A5 / SAA007 | 12,000 km; electric share 60%; battery 18 kWh/100 electric km; fuel 6 L/100 remaining km; loss 10%; 80% home at 2, public at 5; fuel at 20. | Battery 1,296 kWh; purchased 1,440; electric cost 3,744; fuel 288 L / 5,760; total 9,504. | Exact match in the HTTP result and displayed cost. |
| A6 / SAA008 | Same distance/prices; whole-distance metered 10 kWh and 3 L/100 km. | Energy 10,320; no repeated driving-share or loss adjustment. | Exact match. A5/A6 both double with annual distance 24,000. |
| A7 / SAA011 | January start; horizon 6; annual tax 1,200 due March; monthly limit 200. | Cost 600; March outflow 1,200; average funding 200; within budget. | Exact API and calendar/UI match. |
| A8 / SAA012 | Horizon 12; service 1,200 in month 4; repair 2,400 in month 2; reserve 300/month. | Cost 7,200; external bills 3,600; internal saving 3,600. | Exact match; separate saving displayed, no duplicate workshop expense. |
| A9 / SAA014 | Lease 24 months; 6,000 upfront; 2,000/month; 24,000 included km; annual 15,000; excess 1/km; deposit/refund 3,000. | Excess 6,000 km; cost 60,000; monthly cost 2,500; outflow 63,000; refund 3,000; startup 9,000; average funding 2,250. | Exact match and both configured budgets pass at equality. |
| A10 / SAA014 | A9 at 12 / 36 months. | Known outflows 33,000 / 63,000; refunds 0 / 3,000; no complete comparable total. | Exact match; coverage 12 / 24 months; no assumed renewal; 36-month budget unknown. |
| A11 / SAA013 | Horizon 12; 2,400 in month 2; separate startup 500; monthly limits 200 then 199. | Average 200 passes at 200 and exceeds 199; startup remains separate. | Exact match in API verdicts and Swedish budget status. |

The positive-rate reference uses the independently evaluated closed-form annuity
and balance already documented in `HouseholdFinancingCalculatorTests`:
installment 2216.0305126378452369645486624 and remaining balance
25747.907984331467924885066832. Core tests also check principal conservation and
independent 70-digit fractional-depreciation references. Display-rounded loan
parts are not added together as an unrounded identity.

## Practical flow and regression evidence

| Acceptance area | Execution and observation | Supporting automated coverage |
| --- | --- | --- |
| Shared profile and candidates | A1 changes cash for three cars. A5/A6 edit distance and charging price together; changing all three sensitivity modes updates the EV allowance to 1,200 / 2,400 / 3,600 without changing saved profile values. | New acceptance suite; Core financing/energy/cost tests; workspace generation tests. |
| Missing costs and zero distance | SAA010 retains known tax with unknown insurance. Making SAA009's allowance unknown retains energy 16,320 but removes the complete total. Zero distance gives zero energy and `zeroDistance` for per-mil output. | Acceptance suite; `HouseholdPreviewEndpointTests`; frontend preview tests. |
| Precision, residual and cost authority | A2–A6 and the positive-rate case pass. Principal is excluded from cost, negative equity remains visible, yearly depreciation compounds, and fixed residual horizons survive edits. | Core financing, cost, energy and payment-precision suites; exact JSON/form tests. |
| Calendar, lease and budgets | A7–A11 demonstrate accrual versus payment, deposits/refunds, internal saving, exact budget equality and incomplete lease coverage. | Acceptance suite; Core calendar/lease/precision tests. |
| Profile recovery across browsers | SAA015: hold a successful PUT response, edit further, release it and retain the newer form. Second browser reads saved 20,000, saves 40,000; stale save conflicts without overwrite. Explicit server adoption restores 40,000. A later car save persists 26,000 without saving dirty cash 50,000; reload restores cash 40,000. | New two-browser case plus workspace pending-save/reversed-response tests. |
| Shared draft recovery | SAA016–017: opening preserves revision; competing draft save conflicts; changed car base rejects adoption and preserves the saved draft after reload. Explicit replacement adopts another registered draft. Empty slot revision rejects a stale write. Deletion clears a matching saved draft. | New two-browser case; existing draft lifecycle/browser and store/API tests. |
| Legacy transition | SAA018–019 use original annual distances 11,111 and 22,222, fixed residual 15,000 at 24 months, combined maintenance and energy. Both derived results are deliberately unreadable/version 999 in the disposable database. Original inputs remain visible. Profile is entered explicitly as 12,000 km. One atomic request contains both cars/revisions. | New browser case; `HouseholdTransitionStoreTests`, `HouseholdPersistenceEndpointTests`, migration and concurrency tests. |
| Maximum legacy collections | SAA018 preserves 50 recurring posts as 50 mapped current posts (sum 1,275/month), retains 49 undated one-time posts and explicitly discards one. Maintenance and energy each remain one review item. Known annual custom cost is 15,300; full total remains unavailable. Old scenario endpoints return 404 after conversion. | New browser case plus existing 50+50 browser/API/store coverage and transactional rollback tests. |
| Whole deletion and compatibility | Household deletion removes matching draft/current data and preserves profile. Existing v1 and URL browser flows still remove complete aggregates and clear local forms. Deleted cars cannot return through stale responses. | Acceptance/workspace browser suites; three-route API deletion tests; store cascade/retention tests. |
| Limits and concurrent previews | Retained coverage exercises 101/201 candidates, 100-candidate batches, actual UTF-8 sizing, a single oversized candidate, two concurrent previews, four reads, reversed responses and failed batches. Exact/chunked 2 MiB requests are checked through Nginx. | Frontend `preview.test.ts`/`workspace.test.ts`; household API browser and endpoint suites. |
| Isolation and failure | Unsaved preview still works without storage; household edits cause no extraction writes/calls. Failed writes preserve editing and do not retry automatically. | Existing workspace browser outage test; `HouseholdIsolationEndpointTests`; workspace tests; fake acceptance verifier. |
| Accessibility and visual review | Keyboard errors focus the summary and relevant field. Desktop 1440×1000 and mobile 390×844 screenshots were inspected: clear complete/known-part amounts, separate budget categories and payment directions; no page-level mobile overflow. | Existing keyboard/mobile browser case plus the visual session described below. |

The visual session used additional fictional SAB061 (A9 lease) and SAB062
(partial cash purchase), inspected four screenshots, and removed both vehicles
afterward. The lease displayed 60,000 total, 2,500/month and 20/mil; the partial
car displayed the known 5,000 cost with explicit partial labels. Calendar
columns separated external payments, refunds and repair saving. Screenshots
were temporary inspection artifacts, not retained application data.

All stage 3A rows in the verification matrix are covered. Rule facts, scores,
hard-rule ordering and PDF report rows belong to stage 3B and were not claimed
as stage 3A acceptance.

## Findings, limitations and cleanup

Three bounded frontend defects were corrected:

1. A stored complete sensitivity trio also contains `single: null` in HTTP
   responses. The UI previously selected single-value mode by key presence and
   rejected valid saved cars locally. Form rendering and validation now agree
   on the populated representation. Values are neither reordered nor filled in.
2. A refresh started during vehicle deletion was invalidated by the completed
   write's revision epoch, leaving loading active and the draft slot unknown.
   Finishing a write now restarts an invalidated pending read after that epoch.
   Deleted candidates remain protected by the existing deletion markers.
3. Legacy period, residual, tax and combined/custom cost collections lacked
   Swedish display names in the original-input review. They now have explicit
   labels without changing input classification or calculations.

The first two defects were reproduced by failing unit regressions before their
fixes and by browser acceptance failures; both now pass. Initial new-test runs
also exposed invalid fixture assumptions (zero consumption and an incomplete
sensitivity trio) and overly broad/incorrect text locators. Those fixtures and
locators were corrected to the existing contracts; no API/Core semantics were
relaxed. A complete trio requires three values; an unknown amount uses a null
sensitivity object. Original invalid/unfinished-input regression assertions
remain in place. The final full suites passed without skipped tests or local
retries; earlier failed development runs are not counted as successful evidence.

Non-blocking tool output included npm's update notice, Git's Windows LF/CRLF
notice, and Playwright's `NO_COLOR`/`FORCE_COLOR` warning. PowerShell required
`npm.cmd` because its execution policy rejects unsigned `npm.ps1`. No machine
execution policy was changed. The temporary OpenAPI server used a managed tool
session after a detached-launch command was rejected by automatic review.

No live extraction quality, external registration provider, AI advice, Unraid
deployment, real-car facts or long-term ownership outcomes were tested. These
limits do not substitute for or weaken the deterministic stage 3A acceptance.

Temporary artifacts are restricted to the ignored `temp/issue61/` directory
where supported; root `temp/` is also excluded from Docker build contexts.
The OpenAPI process, build servers, isolated Compose stack and task-started
Docker Desktop session were stopped. Both disposable Compose volumes and the
test networks were removed. Generated backend build directories, frontend
build output and test caches were removed. Cleanup also removed the temporary
helper scripts, logs, screenshots and portable Node installation under
`temp/issue61/`, followed by the empty root `temp/` directory.

Windows Temp was inventoried against the startup record. The task-started
Docker/WSL diagnostic directory, updater download, plugin directory and 30
temporary startup icons were removed; the runtime swap directory disappeared
when that session stopped. No identified task-owned file remained inaccessible
or undeleted. Unrelated or unattributed Windows Temp files were retained;
ordinary project dependencies remain installed in `node_modules`.
