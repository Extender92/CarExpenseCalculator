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
implement B7 hard-rule evaluation or B1-B8 score/order behavior; those remain #63.

The #62 implementation uses the ordinary backend/CI groups below. Full-stage
3B browser/PDF acceptance remains #67; existing household and URL regressions
are retained without new product routes or external provider calls.
The current merged baseline is 760 backend, 195 frontend and 33 Chromium tests.
The [#63 preparation audit](buying-rules-implementation-preparation.md) maps B1-B8
and the integration regressions to the next Core implementation. This preparation
adds no new automated tests and does not claim rule/scoring behavior is verified.

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

## Documentation delivery checks

For this planning PR, check changed Markdown links/anchors, UTF-8/formatting,
worked-example arithmetic with an independent decimal calculation, scope versus
current implementation, and the Git diff for unrelated files or secrets. Check
GitHub milestone names, issue contents/statuses, published document links, and
acyclic dependencies. No implementation issue becomes ready before the plan
is merged and its prerequisites are complete. Ordinary repository CI must pass
for the documentation PR; new feature acceptance remains future work.
