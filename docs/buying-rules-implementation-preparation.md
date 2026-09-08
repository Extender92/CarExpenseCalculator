# Issue #63: buying rules and scoring preparation

## Implementation status (2026-09-08)

Preparation PR #81 was approved and merged as
`6fc9470f04e0df0fc70b3767d55882c117c00c2f`; its merged-main CI passed. The user
then explicitly assigned implementation on `feature/63-buying-rules-scores`.
#63 is closed through approved PR #82, merged as
`7e5514c376311863084c0c3201e9c263a0bcd884`. Core contracts, independent rule
evaluation, weighted score intervals, ordering, Swedish signals and tests are
on main. Issue #64 now implements [trusted storage/API composition](comparison-api.md)
on its PR branch, pending separate merge approval. See the
[implemented contract](comparison-and-buying-scores.md#implemented-core-evaluation-63).

The user confirmed one price clarification: current purchase cost input owns
the evaluated price, including missing price; changing it cannot inherit older
advertisement confirmation. Input-bound explicit adoption and exact matching
evidence implement that boundary. The household engine supplies internal raw
cost measures and unchanged version-2 presentation from one calculation path.

All B1-B8 examples and integration regressions are mapped in the
[verification matrix](household-comparison-verification.md#issue-63-core-evaluation-evidence).
Current household validation prohibits residuals above price and negative
expenses, so a negative complete ownership cost cannot be produced. Regression
tests preserve that validation and exercise negative anchors without adding a
cost-total injection route or broadening #63 into a 3A economic model change.

The #63 implementation had no storage/API/UI changes. Its #64 successor adds
trusted mapping for persisted facts, confirmations and review impacts; #65–#67
remain dependent on later delivery. The preparation audit below is a historical
handoff, not the current issue status or an instruction to repeat its publication.

## Preparation readiness and authority (2026-09-07)

Preparation for [#63](https://github.com/Extender92/CarExpenseCalculator/issues/63),
the second implementation item in stage 3B. This document records the dependency
audit, existing implementation boundaries and verification handoff. It introduces
no scoring implementation or new product decisions. The normative behavior is
already on `main` in the [comparison specification](comparison-and-buying-scores.md).

Audit on 2026-09-07:

- Planning [PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54)
  is merged as `62cd19aaab1c1c59fdeca4feb3519c5df01d48e6`.
- Prerequisite #62 is closed through approved
  [PR #80](https://github.com/Extender92/CarExpenseCalculator/pull/80), merged as
  `60d2b49ab910ba17352637cc94b6649df06b117b`. Its tested tree is present on `main`.
- [Merged-main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34118937565)
  is green. The current baseline is **760 backend, 195 frontend and 33 Chromium
  tests**, with no failed/skipped tests and zero backend build warnings/errors.
- No prerequisite implementation, provider choice or additional product decision
  blocks this manual/reviewed-candidate Core scope. #63 is `status:ready` after
  this audit. Preparation does not assign implementation or mark it in progress.
- Tracker #12 records #62 complete; #64-#67 remain blocked by their respective
  implementation prerequisites. Stage 3A remains closed.

The preparation branch is `docs/63-buying-rules-preparation`. Its supporting
documentation PR requires separate merge approval. Existing merged normative
specifications and code remain authoritative; this audit adds no new dependency
or acceptance criterion. The next step is the detailed #63 implementation plan
and explicit assignment, on `feature/63-buying-rules-scores` when work starts.

## Scope already agreed

Implement only Core rule-profile inputs, deterministic hard evaluations,
source-labelled Swedish explanations/signals, weighted score intervals and
cost/preference ordering. Reuse the complete accepted criterion catalogue.
Database, HTTP, generated frontend types, UI, PDF and provider verification
remain #64 or later work. No extraction/schema changes, live AI or migrations
are needed for #63.

All cars use the same criteria, anchors, evidence requirements and weights 0-5.
Weight zero disables a preference for the entire comparison. Unknown,
insufficiently evidenced, conflicting and not-applicable values retain an
interval and weighted coverage using the common denominator. Adding/removing a
car never changes another car's score. High points cannot override hard failures
or verification requirements, and incomplete cost estimates cannot win a
complete-cost recommendation. Overlapping score intervals cannot imply a
certain winner. These are existing decisions, not new choices for this audit.

## Inspected implementation and handoff

| Existing boundary | Consequence for the implementation plan |
| --- | --- |
| [VehicleComparisonFacts](../src/backend/CarExpenseCalculator.Core/Comparisons/VehicleComparisonFacts.cs), [VehicleFact](../src/backend/CarExpenseCalculator.Core/Comparisons/VehicleFact.cs), [criterion catalogue](../src/backend/CarExpenseCalculator.Core/Comparisons/ComparisonCriterionCatalog.cs) | Reuse the 15 source facts, 3 derived cost criteria and 2 budget criteria, with existing units, applicability and independent evidence. Supporting service facts are not extra scoring criteria. |
| [VehicleFactsProcessor](../src/backend/CarExpenseCalculator.Core/Comparisons/VehicleFactsProcessor.cs) | Normalization currently throws aggregated field-path errors. The comparison path must preserve invalid-field diagnostics while evaluating independent valid criteria and candidates; do not catch an error and relabel the entire car as unknown or erase its known hard failures. Preserve the existing strict normalization contract for saving. |
| [HouseholdCostCalculator](../src/backend/CarExpenseCalculator.Core/Households/HouseholdCostCalculator.cs) and [CostSection](../src/backend/CarExpenseCalculator.Core/Households/CostSection.cs) | Internal sections retain decimal precision, but `Result()` rounds money. Comparison must consume an internal unrounded projection from the same calculation, not rank or apply anchors to display-rounded `CompleteTotalSek` values. Keep the public 3A result presentation and version 2 unchanged; do not duplicate the cost engine. |
| [HouseholdCostResult](../src/backend/CarExpenseCalculator.Core/Households/HouseholdCostResult.cs) and [payment/budget results](../src/backend/CarExpenseCalculator.Core/Households/HouseholdPaymentResult.cs) | Complete comparable cost, zero-distance unavailability, lease coverage and independent budget statuses are existing authorities. Known partial sums must not become full cost criteria, and budgets must not be recalculated from rounded funding amounts. |
| [Existing review completeness](../src/backend/CarExpenseCalculator.Api/Mapping/HouseholdReviewCompleteness.cs) | Outstanding legacy review can block otherwise calculated totals/budget passes. The Core comparison contract needs explicit incompleteness supplied through a trusted application mapping; #64 wires that mapping to stored/typed reviews. No Core dependency on API/Infrastructure and no client-authored totals, evidence promotions or affected-section flags become authoritative. |
| [ListingDraft](../src/backend/CarExpenseCalculator.Core/Listings/ListingDraft.cs) and comparison evidence | Optional reviewed condition claims can support source-labelled explanatory signals, never inferred criteria or mechanical diagnoses. Changes to an effective purchase value/cost assumption must not inherit the previous value's confirmation. Registry requirements remain unmet without a permitted adapter; no registry promotion path is added. |

Time-dependent rules take explicit `asOfDate`. The planned rule/evaluation version
is separate from existing household calculation/result version 2 and storage
version 1. The detailed implementation plan must specify the pure Core inputs,
results and internal projections above without expanding the public API scope.
It must retain partial results and existing listing/household normalization.

## Verification handoff

Use the [normative B1-B8 examples](comparison-and-buying-scores.md#worked-examples)
and [stage verification matrix](household-comparison-verification.md). Expected
arithmetic is independent of candidate-set minima/maxima:

| Example | Required evidence |
| --- | --- |
| B1 | Price 40,000 with anchors 100,000/20,000 gives 75; weights 3:2 and preferred gearbox give total 85, coverage 100%. |
| B2 | Unknown gearbox gives [45,85], coverage 60%; its lower bound is not an observed zero. |
| B3 | Gearbox weight 0 gives 75/100% coverage; all weights 0 give null score/coverage. |
| B4 | Known nonpreferred gearbox: weights 3:2 give 45; changing to 1:4 gives 15. |
| B5 | [60,80] sorts above [45,85] by lower bound, without a definite winner. |
| B6 | Eligible complete costs 25,000 and 30,000 precede partial 10,000 and rejected complete 20,000; partial/rejected alternatives cannot win. |
| B7 | Equality at 20,000 SEK and 200,000 km / 20,000 mil passes inclusive limits with adequate evidence. Unknown owners remain unverified; confirmed false tow bar fails a required rule. |
| B8 | Adding an extreme-price candidate or removing a car leaves other contributions unchanged. |

Additional regressions cover increasing/decreasing anchors, clamping, invalid
anchors and weights, empty/duplicate sets, all supported fields and evidence
requirements, no active preferences, hard-rule aggregation, decimal ties before
presentation rounding, lease mismatch, zero distance, missing budgets and
legacy-review incompleteness. Include an invalid field beside an independent
known hard failure and cost totals that differ below one ore. Signals cannot
change rule/score results. Preserve the existing 61 #62 regression cases.

For implementation, use .NET SDK 10.0.400 and Docker with PostgreSQL 18:

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
```

Ordinary frontend, OpenAPI and isolated Docker/Chromium CI remains required;
unchanged HTTP contracts must generate no schema drift. Preparation itself is
documentation-only: check links/anchors, formulas, terminology, source references,
GitHub readiness and the Git diff. No local build, database, browser or live AI
session is needed for this audit. Its CI results do not establish #63 behavior.

## Publication and cleanup

Publish the preparation documents in a focused PR referring to #63, without
`Closes #63`. Pin issue/tracker normative links to merged commit `60d2b49` and
identify the preparation PR separately. Keep #63 ready and unassigned until
explicit implementation authorization. Do not promote #64 prematurely or reopen
closed issues/milestones. All original #63 scope/acceptance criteria are retained.

Own helper bodies belong in ignored `temp/issue63/`, never `.git` or a tracked
file. Remove them before delivery and inspect Windows Temp for work-generated
leftovers. No test stack is needed for documentation-only preparation. The
implementation later receives its own focused PR and separate merge approval.
