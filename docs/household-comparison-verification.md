# Household and comparison verification plan

## Status

Acceptance specification for stages 3A/3B. The implementation notes below name
automated coverage; the practical whole-stage #61 execution is recorded in the
[stage 3A report](household-stage-3a-verification-report.md). Stage 3B acceptance
remains future work. The report records both tested branch evidence and the
approved PR #78 merge, including green merged-main CI.
Use the normative [calculation](household-calculations.md) and
[comparison](comparison-and-buying-scores.md) specifications. Existing v1 tests
remain required. Routine extraction tests use the existing synthetic fake;
no real AI calls, provider purchases, or production-data experiments.

Issue #58 implements the persistence portions in PostgreSQL integration tests:
[current inputs](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdCostStoreTests.cs),
[legacy transition](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdTransitionStoreTests.cs),
[shared draft](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/SharedVehicleDraftStoreTests.cs),
[separate-client concurrency and consistent snapshots](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdConcurrencyTests.cs),
and [seeded upgrade/rollback/reapply](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdMigrationTests.cs).
They exercise failures after partial in-transaction work, preserved empty-slot
revisions, legacy result corruption and the 50+50 old cost collections. The
UI recovery is implemented in #60 and exercised in the #61 acceptance report.
Passing store or HTTP tests alone does not complete that gate.

Issue #59 adds [preview contracts and independent completeness](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/HouseholdPreviewEndpointTests.cs),
[PostgreSQL HTTP lifecycle and concurrency](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/HouseholdPersistenceEndpointTests.cs),
and [storage/extraction isolation and cancellation](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/HouseholdIsolationEndpointTests.cs).
These cover numeric errors versus malformed requests, original 50+50 review
collections, 101-vehicle atomic transition, full deletion through all three
routes, draft conflicts/adoption and corrupt current/legacy results. The
[browser/proxy checks](../src/frontend/e2e/household-api.spec.ts) exercise preview
from the browser and exact/oversized 2 MiB bodies through Nginx, including chunked
transfer. See the [implemented API contract](household-api.md).

## Required automated regression coverage

Issue #62 implements the fact portions of the rule-facts row, merged through
approved PR #80. [VehicleFactsProcessorTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/VehicleFactsProcessorTests.cs)
cover the entire field catalogue, inclusive numeric bounds, enum/text errors,
date domains, independent service facts, unknown/false/zero/empty/not-applicable,
conflicts and immutable collections. [VehicleFactsEvidenceTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/VehicleFactsEvidenceTests.cs)
cover explicit listing/manual mapping, source matching, preserved missing times,
replacement/resolution and rejected registry promotion, plus no inferred service,
inspection, geography, equipment or registration-year facts.
[SwedishMilTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/SwedishMilTests.cs)
cover the B7 conversion, precise decimal round trips, domain errors and
unrepresentable conversion without silent rounding. These 61 new cases do not
implement B7 hard-rule evaluation or B1-B8 score/order behavior; #63 adds those
separately as described below.

The #62 implementation uses the ordinary backend/CI groups below. Full-stage
3B browser/PDF acceptance remains #67; existing household and URL regressions
are retained without new product routes or external provider calls.
The #62 merged baseline was 760 backend, 195 frontend and 33 Chromium tests.
The [#63 preparation audit](buying-rules-implementation-preparation.md) maps B1-B8
and the integration regressions to the next Core implementation. This preparation
adds no new automated tests and does not claim rule/scoring behavior is verified.

### Issue #63 Core evaluation evidence

Issue #63, merged through approved PR #82, adds 114 Core cases to the #62
baseline. These exercise the pure Core boundary; #64 adds storage and HTTP
evidence below, while comparison UI and stage #67 acceptance remain later work.
Existing household and URL routes keep their regression suites.

| Acceptance / boundary | Automated evidence |
| --- | --- |
| B1-B5/B8; fixed targets, weights, common denominator, intervals, clamping, no active score, overlaps and candidate-set independence | [ComparisonEvaluatorTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonEvaluatorTests.cs) |
| B6-B7; hard failures/unverified requirements cannot win, partial cost remains visible, inclusive limits and registration ties | [ComparisonEvaluatorTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonEvaluatorTests.cs) |
| All accepted source categories/fuels, numeric fields, ordinal/Unicode location matching, known empty fuels, inspection days and selected Swedish source signals | [ComparisonRulesAndSignalsTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonRulesAndSignalsTests.cs) |
| Current 35,000 price versus 40,000 source price; missing price; exact reconstructed confirmation and invalidation after edits; explicit cost evidence and profile changes | [ComparisonCostEvidenceTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonCostEvidenceTests.cs) |
| Raw net/monthly/per-mil costs, lease periods, fixed residual mismatch, zero distance, sensitivity, both budgets, safe exceedance despite missing/invalid expenses, and unresolved legacy review | [ComparisonCostEvidenceTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonCostEvidenceTests.cs) |
| Sub-ore cost order, representable tiny scores and local score/cost overflow without rounding-induced winners | [ComparisonEvaluatorTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonEvaluatorTests.cs), [ComparisonCostEvidenceTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonCostEvidenceTests.cs) |
| Invalid anchors/operators/weights/choices, disabled values, identity conflicts, 100/101 candidates, collection limits and immutable snapshots | [ComparisonInputValidationTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonInputValidationTests.cs) |

The negative-cost regression preserves existing 3A validation: residual greater
than purchase price cannot fabricate a complete negative estimate or cheapest
winner. Negative numeric anchors remain valid. No existing economic bounds are
relaxed to manufacture a test fixture. Derived nonzero-distance examples include
explicit positive fuel consumption and a known price, rather than treating an
empty source list or zero consumption as a complete energy model.

The merged #63 verification totals are 874 backend
(551 Core + 323 existing other backend cases), 195 frontend and 33 Chromium
tests. This Core record does not claim an end-to-end comparison UI delivery.

### Issue #64 persistence and HTTP evidence

Delivered through approved [PR #83](https://github.com/Extender92/CarExpenseCalculator/pull/83),
merged as `77939ff2c013dc6e1b3db01059aeb50f0be0b3dd` with
[green main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34221394540).
See the [wire and storage contract](comparison-api.md). The #64 verification
added 64 backend cases and four browser/proxy cases to the #63 baseline;
the current baseline is **938 backend, 195 frontend and 37 Chromium tests**.
The implementation PR records command outcomes, development reruns and warnings.

| Acceptance / boundary | Automated evidence |
| --- | --- |
| Empty rule singleton, full replacement, all fact fields/states, exact decimals, dates, source versions, independent confirmation lifecycle and deletion through all existing stores | [ComparisonStoreTests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/ComparisonStoreTests.cs) |
| Competing first rule saves; facts versus cost/listing/deletion; RepeatableRead consistency; cancellation and rollback after database failure; copied action collections | [ComparisonConcurrencyTests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/ComparisonConcurrencyTests.cs) |
| Upgrade/rollback/reapply with preserved household identity/data; both new tables removed; no pending EF changes | [ComparisonStoreTests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/ComparisonStoreTests.cs), [MigrationTests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/MigrationTests.cs) |
| B1–B8 score/order semantics through HTTP, zero/100/101 candidates, strict actions/enum/source claims, independent numeric failures, lease coverage, exact sorting and budget decisions | [ComparisonPreviewEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPreviewEndpointTests.cs) |
| Stored snapshot revision/identity conflicts; 40,000 to 35,000 confirmation; no null-price backfill; large revisions; corrupt/versioned data; legacy 50+50 review and budget completeness | [ComparisonPersistenceEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPersistenceEndpointTests.cs) |
| Cancelled/failed writes cannot return success or retry; sanitized unavailable storage and extraction isolation | [ComparisonCancellationEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonCancellationEndpointTests.cs), [ComparisonPreviewEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPreviewEndpointTests.cs) |
| Required enum schemas remain nonnullable; real browser B1, explicit persistence/deletion, exactly 2 MiB and oversize with/without Content-Length | [ComparisonPreviewEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPreviewEndpointTests.cs), [comparison-api.spec.ts](../src/frontend/e2e/comparison-api.spec.ts) |

Use .NET SDK 10.0.400, Node 22.22.2, PostgreSQL 18 and README's ordered isolated
`car-expense-e2e` stack. The existing corrupt-legacy browser fixture deliberately
requires that exact disposable project name; do not weaken its SQL target guard.
Run restore/build/test, frontend ci/lint/test/build, API generation on 5090,
Compose boundary validation, Chromium with one worker and the URL acceptance
script. The generated schema should change only for the new routes/types and
the scoped nullable-enum component separation. Backend totals are 938
(551 Core, 213 API, 115 PostgreSQL, 16 Infrastructure unit, 39 extractor, four
architecture), frontend 195 and Chromium 37. UI/PDF acceptance remains #65–#67.

Issue #60 adds the [Swedish workspace](household-workspace.md), focused frontend
tests under `features/household`, and the real
[household browser suite](../src/frontend/e2e/household-workspace.spec.ts).
The frontend tests exercise lossless JSON and revisions, numeric form states,
all sensitivity entries, exact mil conversion, two concurrent preview batches,
four concurrent detail reads, 101/201 candidates, UTF-8 sizing, individual
failures, reversed responses, editing during saves, shared drafts and review
decisions. Browser tests cover separate saves, purchase/fixed residuals, lease
coverage/deposits/budgets, electric/hybrid/kg fuels, navigation, shared listing
drafts, two-browser conflicts, whole deletion, 50+50 legacy posts, database
unavailability and keyboard/mobile behavior. Existing v1/URL regression coverage
is retained at the new routes, with deletion assertions updated to require
local cleanup. Playwright has one worker; explicit multiple-context tests cover
concurrency without racing the singleton fixtures.

| Area | Required cases and observable assertion |
| --- | --- |
| Financing | A1/A2; below/equal/above cash; zero/nonzero rate; horizon shorter/equal/longer than term; principal conservation; no fees when principal zero or after final installment; setup once. |
| Cost authority | Principal excluded from estimated cost, depreciation included once; negative end equity allowed; cost/cash reconciliation; full-precision aggregation before display rounding. |
| Residual | A3/A4; 0%/100%, 1/6/12/24/120 months; independent high-precision decimal reference for fractional powers; changed fixed horizon invalidates only dependent totals. |
| Energy | A5/A6; whole-distance versus mode-specific; battery versus metered; shares 0/100 and mixed; home/public missing at zero/nonzero shares; kg fuel; no invented electric fuel for non-plug-in hybrid. |
| Partial data | Missing tax/insurance/service/repair allowance/price/rate/basis; known zero versus included versus unknown; invalid car leaves another car intact; zero distance only disables per-mil output. |
| Sensitivity | All three explicit values, no missing-mode fallback, shared selected mode and price assumptions across cars; changing mode persists only on explicit save; no result history. |
| Service and repairs | A8; stable keys prevent duplicate category entry; allowance counted once as estimated cost and once as internal saving, never as a workshop invoice or second expense. |
| Calendar/budget | A7/A11; January/December boundaries; annual due month inside/outside horizon; unknown timing preserves known accrual; startup separate; equality passes; monthly spike does not warn when average fits. |
| Lease | A9/A10; same/shorter/longer horizon; explicit inclusion/extras; unknown end obligations; refundable/withheld deposit; no purchase depreciation/principal; zero/excess distance. |
| Migration | Different legacy household assumptions, unsupported snapshot version, ambiguous maintenance/energy/undated entries; current facts recoverable; no silent shared defaults; atomic confirm preserves facts and removes superseded assumptions/results. |
| Concurrent writes | Two profile saves, vehicle replacement, draft update/replacement/adoption/delete, and transition races; stale request returns conflict without overwrite; empty-slot revision prevents stale recreation. |
| Retention/deletion | Listing-only, cost-only, combined, new registered draft and existing-car draft; whole deletion removes children/current results/draft, preserves shared profiles, prevents stale UI/request resurrection. |
| Rule facts | B7; inclusive boundaries and exact km/mil conversion; each accepted new criterion; null/false/not applicable separate; no inferred geography or registry verification. |
| Scores | B1-B5/B8; increasing/decreasing anchors, clamping, invalid equal anchors, categorical sets, weight bounds/zero, weighted coverage, insufficient evidence, no active criteria, deterministic ties. |
| Hard rules/order | B6; high score never overrides hard failure or required verification; incomplete cost cannot win; overlapping intervals cannot imply a certain ranking. |
| React state | Latest valid request wins across tables/batches; pending save cannot replace subsequent edits; failure keeps drafts; changes preview without implicit saves/AI; explicit draft replacement choice. |
| HTTP/schema | Documented success, validation, conflict, missing resource, and storage failure cases; 100-candidate/2-MiB limits; no client-trusted results/verification; generated OpenAPI types agree. |
| Report | Snapshot during concurrent edits, partial inputs, long tables over multiple pages, Swedish text, repeated headings, no clipped columns, dirty labels, complete assumptions/weights/sources, no persisted export. |

### Issue #85 complete-set comparison handoff

The user confirmed that all current saved cars must be compared without a fixed
total count. [#85's backend handoff](all-vehicle-comparison-preparation.md#verification-and-readiness-evidence)
is a prerequisite for #65 and specifies future tests, not executed evidence.
Verify 0/1/100/101/250 cars, full membership, grouping-independent exact order,
global ties/overlaps/winners, more than 2 MiB of aggregate saved input, bounded
transport errors, coherent membership/revisions across concurrent changes,
all sensitivity views and explicit database-independent manual comparisons.
Dropped groups must never yield a whole-set winner. Retain all #62–#64/3A
evidence tests and the existing 938/195/37 baseline until implementation adds
verified cases. New HTTP/generated types are delivered through #85's own PR.

### Original issue #65 workspace handoff

The [workspace preparation](comparison-workspace-preparation.md) audits the
merged #64 contract and planned frontend flows. The following is a future-test
handoff, not executed #65 evidence. Baseline: 938 backend, 195 frontend and
37 Chromium cases. Preserve all existing suites.

| Acceptance / boundary | Existing authority and required new evidence |
| --- | --- |
| Main and all detail tables share inputs and registration identity | 3A A1–A11 backend/browser references; new comparison UI assertions for period/month/mil, purchase/lease, energy, payments, reserves, budgets and all sensitivity views. |
| Complete/partial/rejected order and honest recommendation | B5–B7 Core/HTTP cases; new UI tests for server order, equal displayed values with different raw order, insufficient evidence, unknown totals and overlapping intervals. |
| Editable common priorities without saves | B1–B4/B8 Core/HTTP references; UI tests for all criteria, weights 0–5, anchors, empty profile, common denominator, evidence requirements and no hidden save/AI calls. |
| Exact transport and independent errors | Existing household numeric/preview tests; comparison cases for decimal text, large revisions, invalid rules versus independent candidate fields, and stable paths after selection changes. |
| Latest generation across requests | Reversed responses, edits during active/three-sensitivity requests, cancellation, request failure and navigation; never combine generations or preserve a winner after omitting a failed intended candidate. |
| Comparison-size boundary | Consume the delivered #85 complete-set contract. Regress 101/250 cars, aggregate inputs over 2 MiB, explicit transport errors and no selection cap/truncation; global server winners must survive display pagination and include every saved candidate. |
| Explicit fact, listing and cost adoption | HTTP lifecycle references plus UI tests for source versions, user edits, conflicts, no registry claims, separate cost confirmation and 40,000 → 35,000/missing-price behavior. |
| Saving, refresh and deletion | Separate rule/fact/profile/cost saves; two browser contexts, dirty editing during saves, rejected revision retries, source refresh, full deletion from all three routes and stale-response protection. |
| Manual versus stored comparison | Explicit mode selection, no database dependency in manual mode, no inherited evidence, no automatic fallback after a stored request fails, registration-required transient candidates. |
| Accepted UI choices and accessibility | **Jämförelse** at `/search`, initial cost order, expandable detail tables/**Öppna alla**, cost-editor deep links with retained edits, explicit today-initialized date, keyboard/focus, linked field errors, semantic headers, 390-pixel width and precise feature-status copy. |

PDF layout/export remains #66, and the cross-stage practical report remains #67.
The preparation PR only verifies documentation and existing prerequisite CI.

### Issue #65 workspace evidence

Implemented on `feature/65-comparison-workspace` from merged #85 / PR #86
(`885826a9b367337fa3f7610365f69a8ffa1207bf`), then delivered through approved
[PR #87](https://github.com/Extender92/CarExpenseCalculator/pull/87), merge
`4ba0a0b0f386f7e077c4466a046726cdf3a9df97` on 2026-09-08.
[Merged-main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34274854144)
passed all ordinary checks. Whole-stage acceptance (#67) remains separate.
[Workspace behavior and recovery](comparison-workspace.md) describe the delivered
flows and module boundaries; the following local evidence remains unchanged.

| Acceptance / boundary | Executed evidence |
| --- | --- |
| Common priorities and honest scores, B1–B8 | [Comparison browser suite](../src/frontend/e2e/comparison-workspace.spec.ts): B1 85/100% coverage, B2 [45,85]/60%, disabled weights and fixed-goal changes, B5 [60,80] before [45,85] with overlap, B6 B,A,C,D complete/partial/rejected cost order and only B cheapest, B7 inclusive limits/unknown owners/confirmed missing tow bar, B8 unchanged contributions after adding a cheaper car. |
| All cars / 50 per display page | Browser cases for 101 and 250 saved cars, an off-page definite preference winner, shared expanded detail pages and no extra request on pagination. [Response tests](../src/frontend/src/features/comparison/preview.test.ts) exercise 0/1/50/51/100/101/250 membership and permutation integrity; workspace tests retain page/expanded state and reset on sort. |
| Costs, sensitivity, partial totals and budgets | Browser cases show A2 20,750 ownership versus 80,750 outflow; A5/A6 energy 9,504/10,320; A7 600 accrued tax and 200 average payments at six months; A8 7,200 costs with separate 3,600 workshop outflow and 3,600 saving; A9/A10 60,000 lease cost, 63,000 outflow, 3,000 refund and short/long incomplete periods; 200 versus 199 budget. Existing [household acceptance](../src/frontend/e2e/household-acceptance.spec.ts) retains exact A1–A11 and positive-interest reference coverage. The combined comparison energy/payment fixture explicitly uses zero-priced diesel for non-hybrid examples so all cars share one profile. |
| Forms and exact authority | [Form/table tests](../src/frontend/src/features/comparison/components.test.tsx) exercise false/unknown, cleared versus absent collections, explicit evidence, exact mil input with trailing zeros, intervals/coverage and stale badges. [API tests](../src/frontend/src/features/comparison/api.test.ts) retain high precision and large revisions; response tests retain server order for equal displayed amounts. Existing household forms cover purchase/lease/null/zero/included/triple sensitivity and exact units. |
| Complete current generations | [Workspace tests](../src/frontend/src/features/comparison/workspace.test.ts) cover debounce, reversed responses, latest queued work with two transports, four lazy detail reads, invalid candidate retention, independent numeric errors, dirty navigation, deletion and in-flight writes. Response tests reject missing view/count/identity/order/version/request/generation inconsistencies. Browser fault injection verifies that an incomplete third view leaves old results stale without a recommendation. |
| Request sizes and failures | Comparison API client tests cover UTF-8 measurement, existing 2-MiB write limits, no fixed 2/32-MiB preview cap, server limits below/above default, 413/busy/timeout and truncated JSON without retry. Existing #85 Kestrel/Nginx exact-limit and chunked tests remain unchanged and pass in the full suites. |
| Sources, conflicts and confirmation | Browser test reads a listing proposal without adoption, explicitly adopts version 1, retains it when version 2 arrives, records current/listing conflict and resolves it manually. Another keeps dirty gearbox facts while saving economic price 40,000 → 35,000, observes [0,100] after confirmation invalidation and [48.75,88.75] after explicit saved-cost confirmation. No comparison operation invokes extraction or AI. |
| Concurrent editing and recovery | Two browser contexts change a shared profile while local weights remain dirty; explicit baseline review preserves local edits. Workspace tests preserve later rule/fact edits, expose refreshed facts before replacement, block old conflict references and detect unrelated concurrent inventory changes even during an own save. A delayed economic read test hides the old/blank editor until the requested car is loaded. |
| Links, accessibility and isolation | Browser checks use keyboard Enter, focused error summary and focused criterion/economic fields, explicit manual mode with baseline storage failure, exact values retained through `/manual`, and a 390-pixel viewport without document overflow. Keyed-path regression follows a cost row through insertion/reordering. Full deletion retains both shared profiles. Existing URL, legacy and shared-draft regressions pass. Desktop/mobile screenshots were inspected locally; temporary images are not product assets. |

Local verification uses .NET SDK **10.0.400**, Node **22.22.2**, Docker Engine
**29.5.3**, disposable PostgreSQL **18** and the fake extractor. The backend
restore/Release build/test passes **1,012 tests** (Core 562, API 262,
PostgreSQL Infrastructure 129, Infrastructure unit 16, extractor 39,
architecture 4), **0 failed/skipped**, with **0 build warnings/errors**.
Frontend `npm ci`, lint, test and production build pass: **255 tests**,
**0 failed/skipped**, no lint/build warnings. OpenAPI was regenerated from the
API on port 5090 and has **no schema diff**. The isolated Docker build, explicit
migration, readiness, `nginx -t`, Compose-boundary check and pinned
`codex-cli 0.153.0` check pass. Full Chromium with one worker passes **55 tests**,
**0 failed/skipped**; URL-acceptance state/concurrency/isolation/safe-log checks
also pass. No production data or real AI service was used.

Development reruns were necessary and are not omitted: corrected new fixtures
used valid registration letters, positive consumption, an explicit zero-priced
energy source, the existing partial-cost wording and complete version-2 listing
metadata. An initial fixture TypeScript cast blocked Docker compilation and
was corrected. Product regressions found and fixed during verification were
trailing-zero loss while typing exact mil values, premature economic editing
before a linked car finished loading, and collapsed fact sections during refresh.
Additional tests protect revision coordination, late edits and partial-response
publication. A transient hook-dependency lint warning was corrected. Playwright
reports its existing `NO_COLOR`/`FORCE_COLOR` environment warning; npm printed an
update notice and Git reported local LF/CRLF conversion notices. These did not
change contracts or suppress any check. Later field-link refinements receive
focused reruns and the PR's complete CI verification.

#### Issue #65 cleanup inventory

The temporary API process on 5090 and the `car-expense-e2e` stack were stopped.
Its work-owned PostgreSQL and empty Codex volumes and networks were removed;
no listener remained on 5090/8088 and no E2E container/volume remained.

Work files remain in the single ignored directory
`C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\issue65\`.
Native PowerShell deletion of this verified work-owned directory was rejected
by automatic tool policy with **`blocked by policy`** before execution. No
specific reason was provided and no alternative deletion bypass was attempted.
The directory contains these work-owned groups, all disposable:

- `docker-build.log`, `web-build.log` and two `Microsoft.NET.Workload_*.log` files.
- `playwright/`, `focused-playwright/`, `full-playwright/`, `final-links/`
  (test diagnostics and inspected desktop/mobile screenshots).
- `npm-cache/` (the temporary pinned formatter), `node-compile-cache/`,
  `playwright-transform-cache/`, `NuGetScratch/`, `MSBuildTempfezh4m0r.h0r/`,
  `VBCSCompiler/`, `car-expense-process-test/` and temporary SDK subdirectories
  `pdpqxdfb.1q0/`, `t5hnwfq1.lkx/`, `wktphv2b.f4f/`, `ymeiyxop.bvq/`.

Windows Temp was also inspected. The first Compose build's metadata file
`compose-build-metadataFile-1619021b-cbb0-4693-9b0f-be0378f36a22.json` had already
been removed by the tool. Remaining recent empty GUID-named `.tmp` files and
an empty `nsjD64.tmp` directory had no verifiable project ownership and were
preserved. No project-attributable Windows Temp remainder was identified.
There is no cleanup helper script. The deferred #64/#85 inventories and the
pre-existing Node installation under `temp/issue64/` were left untouched.

### Issue #66 PDF evidence

The [report implementation and evidence](comparison-pdf.md#verification-evidence)
maps each report requirement to frontend, Chromium and practical checks on
`feature/66-comparison-pdf`. The preparation baseline was 1,012 backend,
255 frontend and 55 Chromium tests; preparation itself did not verify PDF.
The report document records final counts, actual multipage files, native
print/cancel/retry checks, development reruns and browser-controlled limitations.

Coverage includes immutable capture through edits/saves/late
responses; stale/invalid-generation gating versus valid partial export; complete
1/50/51/101/250-car membership independent of page/expanded sections; captured
server order and exact assumptions; all three sensitivities, evidence, review
items and unsaved flags; actual Swedish multipage PDFs without clipping; print
cancel/return/reload/deletion behavior; and no writes, recalculation, AI or report
archive. Inspect complete and partial PDF files visually in addition to automated
print-media checks. Reuse A1–A11/B1–B8 references and existing cross-route tests.
The browser's actual print/save-PDF dialog was checked separately from headless
PDF generation. Whole-stage #67 acceptance and an approved #66 merge remain
separate gates; no milestone completion is claimed here.

## Verification commands by work area

Commands below run from the repository root unless stated otherwise. They are
existing repository commands; individual issues select their relevant groups.
Do not add tests that merely duplicate documentation formatting or implementation.

### Backend (Core, persistence, or HTTP)

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
```

PostgreSQL 18 integration tests use Testcontainers and need Docker. Core-only
development can first run the existing Core unit-test project; before a backend
implementation PR is ready, run the applicable repository backend checks.

### Frontend

```bash
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run test
npm --prefix src/frontend run build
```

### OpenAPI

Start the API on port 5090 in a separate terminal:

```bash
dotnet run --project src/backend/CarExpenseCalculator.Api --no-launch-profile -- --urls http://localhost:5090
npm --prefix src/frontend run api:generate
git diff -- src/frontend/src/api/schema.d.ts
```

The first command runs until stopped; run generation/diff in the second
terminal. Review intended contract changes and commit generated types. Stop the
temporary API after verification. Never hand-edit generated declarations.

### Docker and browser flow

Use the isolated fake-extractor stack and cleanup procedure in
[README verification](../README.md#verification). The ordered commands build
images, start PostgreSQL, explicitly migrate, start API/web, check readiness
through Nginx, run Chromium and the URL acceptance script, and stop the stack.
Only delete volumes created solely for that disposable verification. Run:

```bash
node scripts/verify-compose-boundaries.mjs
```

Do not use the Unraid application database for tests or change its deployment
boundaries. Document any unperformed Unraid/live integration checks explicitly.

## Practical stage 3A acceptance

Create synthetic registered candidates using permitted ordinary registration
formats: a cash purchase, a financed purchase, a combustion car, a battery car,
a plug-in hybrid, a lease, and a car with incomplete costs. Candidates may
combine these properties; all fixtures are fictional and labeled as such.

1. Reproduce A1-A11 using explicit assumptions; match the documented rounded
   amounts exactly. For nonzero-rate loans independently check principal plus
   interest versus payments and the remaining balance.
2. Change common cash, annual km, horizon, prices, and active sensitivity mode.
   Confirm all affected candidates update together and unrelated known sections
   remain available. Invalid inputs never look like current valid results.
3. Save the profile, reload on a second browser, and confirm it is shared.
   Edit without saving, reload, and confirm the saved profile was not replaced.
4. Exercise startup/average-month budgets and calendar due months. Confirm a
   spike does not produce an average-budget warning, and repair saving is
   distinguishable from known external bills.
5. Migrate disposable v1 fixtures with differing assumptions and an unsupported
   result version. Inspect preserved inputs before confirmation and unresolved
   items after it; no row is lost or guessed into several cost categories.
6. Save/open/replace/adopt the one draft across two browsers, force a revision
   conflict, and delete a complete vehicle. Verify recovery on failure and full
   related-data deletion on success, with household profile preserved.

Record candidate inputs, expected/observed outputs, commit, commands, pass/fail
counts, warnings, and limitations in the stage acceptance issue and a committed
verification report. Stage 3A closes only when the flow and regressions pass.

## Practical stage 3B acceptance

1. Use B1-B8 and the 3A fixtures. Confirm main/detail tables use the same profile,
   period, mode, and registration identity; every missing calculation is named.
2. Exercise every accepted buying criterion, change numeric targets/weights,
   and verify the changed contributions, interval, and weighted coverage.
3. Add/remove a candidate and confirm existing scores do not change. Compare
   cost and score sorting; rejected and incomplete cars remain visible.
4. Require stronger evidence, leave an owner count unknown, and fail a tow-bar
   rule. Confirm score cannot remove verification/rejection or create a false
   cost winner. Check overlap and equal-cost/tie labels.
5. Save rules and reopen from a second browser; force a concurrent edit and
   verify explicit conflict recovery without hidden overwrite or history.
6. Export the current complete and partial comparisons via browser print-to-PDF.
   Inspect the resulting files visually for Swedish text, all tables and
   assumptions, page breaks, missing-data labels, and unsaved changes. Change
   inputs during export and confirm the report remains one captured snapshot.
7. Recheck all three routes and whole-vehicle deletion, fake extraction fallback,
   and same-origin Docker behavior. No automatic marketplace search is enabled.

Record the same evidence as 3A, including PDF visual inspection and explicit
limits of fake-only extraction. All relevant automated checks must pass; a
manual example alone does not replace regression coverage.

## Issue #85 complete-set evidence

Implementation branch: `feature/85-all-vehicle-comparison`, based on merged PR
#84 (`1bf03875c6fa65a1cf477dff427e43c5758d0cf6`). This records verification of
the PR work; it does not claim an approved merge or delivery of #65's UI.
The [transport contract](comparison-api.md#complete-set-comparison-85) and
[#65 handoff](comparison-workspace-preparation.md) define remaining publication
and interaction work. No migration or version change to existing engines.

| Acceptance boundary | Automated evidence |
| --- | --- |
| Entire membership, 0/1/100/101/250, group independence | [CompleteComparisonTests](../tests/backend/CarExpenseCalculator.Core.UnitTests/CompleteComparisonTests.cs): identical complete serialized results for batch sizes 1/7/99/100; duplicate detection and global paths beyond index 99. [CompleteComparisonSnapshotTests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/CompleteComparisonSnapshotTests.cs): mixed listing/legacy/purchase/lease roots exactly once, draft excluded. [CompleteComparisonPersistenceTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/CompleteComparisonPersistenceTests.cs): compact stored request and all three HTTP views for each count. |
| Raw ordering and recommendation | Core and HTTP tests retain 100.001/100.002 SEK differences across groups despite equal displayed costs/scores; global cheapest ties and overlap, rejected alternatives. Existing B1–B8 evaluator tests use the same finalization; HTTP examples retain independent expected 85, [45,85], 75 and 15 scores. |
| Coherent reads / stale baselines | Snapshot tests use separate PostgreSQL contexts to mutate profile, rules, facts, costs, listings, membership and deletion between payload groups. Current result stays on its captured snapshot; next old-token read conflicts. Invariant/culture-independent hashes and revision changes are checked. HTTP tests check whole-baseline and individual overlay identity/revisions. |
| Large saved/manual data | Persistence test loads more than 2 MiB of stored cost notes using a request below 2 KiB. Endpoint test sends actual manual input above 2 MiB for 101 candidates. No upload sessions or implicit saves. |
| Evidence, sensitivity and review | Persistence tests cover input-bound confirmation and 40,000→35,000 price edits, missing price, existing source checks, three views using the same confirmation, maximum 50+50 legacy input with unknown results, unresolved review and unknown/exceeded/invalid/unconfigured budgets. Explicit preview decisions do not change stored review. |
| Request limits | [CompleteComparisonEndpointTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/CompleteComparisonEndpointTests.cs): real Kestrel exact 32 MiB/+1 with Content-Length and chunked UTF-8; configured limit affects only new route. [Browser HTTP tests](../src/frontend/e2e/complete-comparison-api.spec.ts) repeat exact proxy limits and 101-car stored/manual flows. Existing 100-car/2-MiB routes keep their regression coverage. |
| Admission / cancellation / failure | [CompleteComparisonCancellationTests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/CompleteComparisonCancellationTests.cs): two active slots, immediate third-request 503, injected 120-second deadline, cancelled database wait, raw TCP upload disconnect, response disconnect, sanitized group fault/missing group and slot reuse. Core cancellation never returns a partial set. |
| Isolation and compatibility | Manual tests register throwing storage resolvers and still evaluate 101/250 candidates. No comparison code invokes extraction/AI or writes. Existing household, v1, listing, browser and URL acceptance suites remain mandatory. |

Local environment: .NET SDK **10.0.400**, Node **22.22.2**, PostgreSQL **18**
in disposable Testcontainers/`car-expense-e2e`, Docker Engine **29.5.3**.
The complete backend verification passes **1,012 tests**: Core 562, API 262,
PostgreSQL Infrastructure 129, Infrastructure unit 16, extractor 39 and
architecture 4. Build: **0 warnings, 0 errors**; tests: **0 failed, 0 skipped**.
Frontend verification passes **195 tests**, lint and production build.
The final Chromium run passes **41 tests** with one worker, **0 failed/skipped**;
URL acceptance and Nginx readiness/configuration checks pass. The pinned private
CLI reports `codex-cli 0.153.0`; extraction tests use only the fake service.

Commands are the README's restore/Release build/test, frontend `npm ci`, lint,
test/build, `node scripts/verify-compose-boundaries.mjs`, API on port 5090 and
`npm --prefix src/frontend run api:generate`, followed by ordered Docker build,
PostgreSQL startup, explicit migration, API/web readiness, Chromium with one
worker and `node scripts/verify-url-analysis-acceptance.mjs`. Generated schema
adds only the intended routes/transport types and optional problem fields.
Compose validation also checks a nondefault shared API/web request limit.

Development reruns are recorded rather than hidden: fixtures were corrected for
PostgreSQL timestamp precision, the existing constant-or-complete-trio sensitivity
shape, and ephemeral Kestrel ports. The raw TCP upload fixture observes actual
request admission before disconnecting. Known-length oversize tests use HTTP
Expect/Continue to avoid a race between early rejection and continued uploading.
The first full proxy run exposed a real exact-boundary defect: Kestrel counts
HTTP/1.1 chunk framing in its limit. The new route now delegates decoded body
size to its bounded reader for chunked transfers; exact 32 MiB is accepted and
one additional decoded byte is still rejected. Old limits are unchanged.

Browser reruns also exposed an existing fixture timing assumption: the car editor
can appear before the independent shared-profile read finishes. An attempted
request-event synchronization produced eight fixture failures and was reverted.
The final fixture explicitly waits for the loaded profile before interaction,
retaining all amount, save/conflict and legacy-review assertions. The complete
41-test suite then passed without retries; no product UI change was needed.

PowerShell initially rejected the unsigned `npm.ps1` wrapper; `npm.cmd` with
the pinned Node binary completed all checks without changing execution policy.
Playwright may emit its existing NO_COLOR/FORCE_COLOR notice. Initial migration
on the empty disposable database logs a missing history-table probe before
successfully applying all existing migrations. No new migration is introduced.
CI links, final Chromium/URL results and cleanup are recorded in the PR linked
from #85. No Unraid user data, real AI, comparison UI or PDF acceptance is claimed.
The deferred #64 cleanup inventory remains untouched.

### Issue #85 cleanup inventory

The task API, MSBuild/compiler servers and disposable Compose stack were stopped;
the task's PostgreSQL/Codex-home volumes and test networks were removed. The
execution tool rejected the native PowerShell deletion of the verified work
directory with `blocked by policy`, giving no further reason. The ignored
`temp/issue85/` therefore remains: **2,490 files, 35,326,903 bytes** at the final
local audit, comprising logs/TRX results, browser artifacts and process/Node
caches. No helper is committed. This whole task-owned directory can be removed
once permitted; the prior #64 directories are explicitly outside this cleanup.

Windows Temp was inventoried for recent work-owned remnants. Anonymous empty
files and background-installer logs could not be attributed to #85 and were
preserved. Existing shared bin/obj, node_modules/dist and the pinned Node under
the deferred #64 inventory were also preserved. No failure is reported as a
successful cleanup.

## Documentation delivery checks

For this planning PR, check changed Markdown links/anchors, UTF-8/formatting,
worked-example arithmetic with an independent decimal calculation, scope versus
current implementation, and the Git diff for unrelated files or secrets. Check
GitHub milestone names, issue contents/statuses, published document links, and
acyclic dependencies. No implementation issue becomes ready before the plan
is merged and its prerequisites are complete. Ordinary repository CI must pass
for the documentation PR; new feature acceptance remains future work.
