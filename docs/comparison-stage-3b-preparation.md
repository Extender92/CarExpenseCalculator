# Stage 3B acceptance preparation (#67)

## Audit and delivery gate

Completion update: the user assigned #67 after approved preparation PR #90
merged as `16f6981`, then approved the acceptance delivery through PR #91.
#67 is closed; tracker #12 and milestone 3B were closed after the final audit.
The [execution report](comparison-stage-3b-verification-report.md) records
1,012 backend, 273 frontend and 69 Chromium passes and fresh PDF/native-print
inspection, the approved merge and green merged-main CI. It supplies the evidence
requested below. The preparation-only statements below preserve the earlier
dated audit and its original gates; they are not outstanding work or current
readiness instructions. Later planning remains at the
[documented checkpoint](roadmap.md#current-planning-checkpoint).

### Historical preparation audit

Prepared on 2026-09-09 against clean `main`
`72ded81784950860ab9ab186a5f4c30974ee2951`. The user assigned preparation,
not execution of stage acceptance. This document maps the existing #67 scope
to implemented behavior, tests and the practical evidence still to collect.
It introduces no product decision, calculation rule, endpoint or migration.

| Dependency | Verified delivery |
| --- | --- |
| Planning #54 | Approved, merged [PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54). |
| Stage 3A #61 | Approved, merged [PR #78](https://github.com/Extender92/CarExpenseCalculator/pull/78) and [acceptance report](household-stage-3a-verification-report.md). |
| Facts #62 | Approved, merged [PR #80](https://github.com/Extender92/CarExpenseCalculator/pull/80). |
| Rules and scores #63 | Approved, merged [PR #82](https://github.com/Extender92/CarExpenseCalculator/pull/82). |
| Storage/API #64 | Approved, merged [PR #83](https://github.com/Extender92/CarExpenseCalculator/pull/83). |
| Full inventory #85 | Approved, merged [PR #86](https://github.com/Extender92/CarExpenseCalculator/pull/86). |
| Workspace #65 | Approved, merged [PR #87](https://github.com/Extender92/CarExpenseCalculator/pull/87). |
| PDF #66 | Approved, merged [PR #89](https://github.com/Extender92/CarExpenseCalculator/pull/89); #66 is closed. |

The implemented baseline is **1,012 backend, 273 frontend and 63 Chromium
tests**, confirmed by successful
[main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34361733192)
with no failed/skipped tests or build warnings/errors. Six of seven stage 3B
implementation issues are merged.
The current [PDF evidence](comparison-pdf.md#verification-evidence) is feature
verification, not a substitute for the final whole-stage report.

No unresolved product decision was found. Keep #67 `status:blocked` while this
preparation PR awaits its separately approved merge. After merging, recheck
current main/CI, the published handoff and all prerequisites, then set
`status:ready`. This is not an implementation assignment. A later explicit
assignment starts `chore/67-comparison-stage-acceptance`; only that execution
may satisfy/close #67. The #12 tracker and milestone stay open meanwhile.

## Scope and existing authority

Execute the [stage acceptance procedure](household-comparison-verification.md#practical-stage-3b-acceptance),
fill meaningful gaps between layers, and publish
`docs/comparison-stage-3b-verification-report.md`. Demonstrate the complete
Swedish comparison/report flow while retaining 3A, URL and legacy regressions.
Correct a narrowly evidenced defect in already specified behavior with a
regression test if needed. Broader behavior changes or new product decisions
must be recorded as blockers for separate planning; do not weaken an assertion
or change expected amounts to fit an implementation defect.

Use the [comparison specification](comparison-and-buying-scores.md),
[HTTP contract](comparison-api.md), [workspace](comparison-workspace.md),
[household specification](household-calculations.md) and [PDF guide](comparison-pdf.md).
The Core engine owns raw cost/score order, budgets, evidence and recommendations.
Unknown/unverified criteria retain their weight and interval, rather than
reducing a car's denominator. Hard failures and verification gaps cannot be
overridden by scores. Current purchase inputs own purchase price, including an
explicitly missing price; confirmation never transfers to changed assumptions.

Keep comparison/rule/complete-transport versions 1, household versions 2 and
storage formats 1. No new HTTP, schema, storage history or report archive is
planned. OpenAPI regeneration must be unchanged unless a separately reviewed
defect genuinely requires a contract change. Registry providers, discovery,
advisory AI, real extraction quality, deployment to user data and image review
are outside #67. Later #68–#70 and paused #13/#15 do not block this stage.

## Inspected coverage and execution handoff

The following are inspected existing tests and targeted execution needs, not
new test results from this preparation. Reuse detailed lower-layer regressions;
add a test only where the combined observable behavior lacks evidence.

| Requirement | Existing evidence | #67 handoff |
| --- | --- | --- |
| B1–B8, weights, intervals, eligibility and deterministic ordering | [Core evaluator tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonEvaluatorTests.cs), [HTTP tests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPreviewEndpointTests.cs), [workspace browser suite](../src/frontend/e2e/comparison-workspace.spec.ts) | Run all examples; record actual API and Swedish table values. Include changed weights, disabled criteria, stronger evidence, added/removed cars and rejected/partial alternatives. |
| Full criterion catalogue and independent field/evidence errors | [Rules/signals tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonRulesAndSignalsTests.cs), [cost evidence tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ComparisonCostEvidenceTests.cs), [form tests](../src/frontend/src/features/comparison/components.test.tsx) | Demonstrate every criterion through the actual editor/API, especially categories, place text, inspection date and service. Lower-layer tests already cover enum/boundary combinations; do not reproduce every combination in Chromium. |
| Source review and current price/confirmation | [Comparison store tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/ComparisonStoreTests.cs), [persistence HTTP tests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/ComparisonPersistenceEndpointTests.cs), workspace browser source/price tests | Join explicit listing adoption, economic editing, confirmation, comparison and captured report in one flow. Verify 40,000 → 35,000 and missing price, then listing replacement without silent adoption. |
| Consistent shared data and recovery | [Concurrency tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/ComparisonConcurrencyTests.cs), [snapshot tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/CompleteComparisonSnapshotTests.cs), [workspace state tests](../src/frontend/src/features/comparison/workspace.test.ts) and two-browser cases | Use two browser contexts for an end-to-end save/conflict/review/recalculation/report flow. Local changes survive; stale baseline responses and late writes cannot create current recommendations. |
| Full inventory, exact order and request bounds | [Complete Core tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/CompleteComparisonTests.cs), [complete HTTP tests](../tests/backend/CarExpenseCalculator.Api.IntegrationTests/CompleteComparisonEndpointTests.cs), [browser transport tests](../src/frontend/e2e/complete-comparison-api.spec.ts), 101/250-car workspace/report cases | Reuse 0/1/50/51/100/101/250 membership evidence at the appropriate layer. Check off-page winners and common page/order; 50 is a display page, 100 an internal/old-route limit, not a full-inventory cap. |
| Partial costs, legacy review and 3A | [Household acceptance](../src/frontend/e2e/household-acceptance.spec.ts), [household workspace](../src/frontend/e2e/household-workspace.spec.ts), comparison cost-evidence tests | Demonstrate a migrated legacy car with unresolved items in comparison and report. Preserved 50+50 records cannot silently become complete costs/budgets or be counted twice after an explicit mapping. |
| Report capture and print lifetime | [Report tests](../src/frontend/src/features/comparison/report.test.tsx), [report state tests](../src/frontend/src/features/comparison/report-workspace.test.ts), [report browser tests](../src/frontend/e2e/comparison-report.spec.ts) | Inspect fresh complete/partial PDFs after realistic prior edits/conflicts; verify native print/cancel/retry, returned editing, no API/AI side effects and invalidation on observed member deletion. |
| Full deletion, three routes and isolation | Comparison/household browser suites, [URL suite](../src/frontend/e2e/url-analysis.spec.ts), [legacy calculator suite](../src/frontend/e2e/manual-calculator.spec.ts), [foundation](../src/frontend/e2e/foundation.spec.ts) | Check one joined lifecycle through URL, household and comparison with both shared profiles preserved. Keep independent manual comparison/report available without storage; use only fake extraction. |

### Catalogue checklist

The [implemented catalogue](../src/frontend/src/features/comparison/catalogue.ts)
contains 20 criteria: 15 vehicle facts, three derived costs and two budgets.
For each criterion record an input, selected rule/preference, evidence
requirement, observed server outcome and displayed Swedish outcome. An explicit
unsupported registry requirement demonstrates `needsVerification`, not a new
registry integration. Missing service/inspection data remains missing.

| Kind | Criteria to demonstrate |
| --- | --- |
| Numeric facts | `purchasePriceSek`, `odometerKilometres`, `ownerCount`, `seats`, `modelYear`, `towingCapacityKilograms`. Include exact mil/km and inclusive bounds. |
| Categories/text | `towBar`, `transmission`, `fuelTypes`, `bodyType`, `drivetrain`, `locality`, `county`, `serviceDocumentation`. Include false, empty/unknown fuels, Unicode/case matching and explicit source evidence. |
| Date | `inspectionValidThrough`, evaluated from the explicit `asOfDate`; below/equal/above the selected day threshold. |
| Complete derived cost | `netCostSek`, `costPerMonthSek`, `costPerMilSek`, using 3A's unrounded authority and explicit cost confirmation. |
| Hard budget only | `startupBudget`, `monthlyBudget`; no preference weight. Include within/exceeded/unknown/invalid/not-configured results using existing tests and representative UI observations. |

### Reference observations

Use the specification's exact fixture inputs. Seed cars through existing APIs;
do not compute expected scores or costs by calling the production engine again.
Reuse independent constants and the positive-interest reference from 3A.

| Example | Expected reference |
| --- | --- |
| B1 | Price contribution 75, automatic transmission 100, weighted score 85, coverage 100%. |
| B2 | Unknown/insufficient gearbox evidence: [45,85], coverage 60%, same common denominator. |
| B3 | Gearbox weight zero: 75; all preferences zero: no score/coverage and `noActiveCriteria`. |
| B4 | Weights 3:2 → 1:4 with price score 75 and known unwanted gearbox: 45 → 15. |
| B5 | [60,80] precedes [45,85]; overlapping intervals forbid a definite winner. |
| B6 | Cost order B,A,C,D; B alone is the cheapest complete eligible alternative. |
| B7 | Inclusive 20,000 SEK / 200,000 km limits pass with evidence; unknown owners need verification; confirmed missing required tow bar fails. |
| B8 | Adding/removing another candidate leaves existing contributions/score unchanged. |
| A1–A11 | Retain 3A regressions. In joined comparisons/report observations include A2 cost 20,750/outflow 80,750; A5/A6 energy 9,504/10,320; A8 cost 7,200, external bills 3,600 and saving 3,600; A9 cost 60,000/outflow 63,000/refund 3,000; period mismatches and A11's exact budget boundary. |

## Practical run and evidence report

Use fictional registered purchase, financed, electric/hybrid, lease, incomplete
and legacy candidates; one car may cover multiple roles. Fix the evaluation
date and profile explicitly. Exercise criteria and shared assumptions, save and
reopen from a second context, resolve a genuine revision conflict explicitly,
and check source/confirmation boundaries before capturing a report. Record the
same UUIDs/revisions across API, screen and report rather than comparing
unrelated seed data. No example rules or economic defaults are auto-activated.

Generate real complete and partial multipage PDFs using installed Playwright
Chromium and `page.pdf({ preferCSSPageSize: true })`. Inspect Swedish glyphs,
all candidates/assumptions, repeated headings, full 121-month calendars and
legacy 50+50 records, long URLs/notes, clipping and page breaks. Check the native
print dialog, cancel/retry and retained report explicitly. Do not infer saving
from `afterprint`. Earlier #66 samples establish a useful reference (including
long reports); they do not prove the #67 combined session. Record page counts
as observations, never as a required output limit.

Review keyboard-only editing/report navigation, linked errors and focused
sections, Swedish explanations and 390px layout. Differentiate stale displayed
results from current ones and suppress stale recommendations. Check stored and
explicit manual modes; storage failure never switches mode automatically.

The final report must identify tested commits, environment, commands/CI links,
fixture inputs, expected/observed results, requirement-to-test mapping, practical
observations, corrected defects, reruns, warnings and every failed/skipped or
unperformed check. Separate pre-existing warnings from new failures. Do not
count an old green feature PR as newly executed whole-stage evidence.

## Verification environment and commands

Use .NET SDK **10.0.400**, Node **22.22.2**, disposable PostgreSQL **18** and
the existing Playwright Chromium, with **one worker**. Explicit browser contexts
test concurrency; parallel workers must not race the shared profile/draft.
Use the exact **`car-expense-e2e`** project: the corrupt-legacy fixture checks
that name, running fake service and owned UUIDs before SQL. Verify any existing
stack/volumes are disposable before using them. Never weaken this guard.

Follow the [README sequence](../README.md#verification): backend
restore/build/test; frontend `npm ci`, lint/test/build; Compose boundaries;
production/fake image build and pinned CLI version (no live extraction); start
PostgreSQL, explicit migration, start API/web, readiness; whole Chromium with
`--workers=1`, then URL acceptance. Start the separate API on 5090 for
`api:generate` and require `git diff --exit-code -- src/frontend/src/api/schema.d.ts`.
No migrations run at normal API startup. No test uses Unraid/user data.

Fixtures clean their registered data and restore singleton assumptions even
after assertion failures. Stop test services/processes, remove only run-owned
volumes and collect helpers/PDFs/screenshots in ignored `temp/issue67/`.
Redirect process temp output there where supported; audit work-owned Windows
Temp leftovers. Preserve older deferred inventories, including #66's
[documented cleanup block](comparison-pdf.md#cleanup-inventory). Report a denied
deletion with exact paths instead of claiming success or bypassing the policy.

## Preparation publication and completion

This preparation changes documentation and the authorized #67/#12 backlog only.
Publish it on `docs/67-stage-acceptance-preparation`, link immutable document
versions from both issues, and require separately approved merge. Validate
links, commands, terminology, source/test references and the full Git diff.
Ordinary CI applies; no local full test rerun or new acceptance claim is needed
for this documentation-only change. No `Closes #67` belongs on this PR.

After actual #67 execution, a green acceptance PR with its completed report may
close #67 only when all criteria pass. After that separately approved merge,
audit all seven implementation items and the report before closing #12 and the
3B milestone through authorized backlog maintenance. Keep later paused work
paused; stage completion does not authorize provider selection or deployment.
