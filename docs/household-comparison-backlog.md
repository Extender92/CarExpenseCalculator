# Household and comparison delivery backlog

## Status and publication gate

The [product plan](household-comparison-plan.md), normative
[household](household-calculations.md) and
[comparison](comparison-and-buying-scores.md) specifications, and
[verification plan](household-comparison-verification.md) define this queue.
The original planning PR defined the queue without implementing its features.
The documentation gate and stage 3A are now complete. Trackers are overviews,
never implementation assignments.

Completed documentation gate: [PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54).

## Stage 3A - Household calculations

Tracker: [#71](https://github.com/Extender92/CarExpenseCalculator/issues/71). Milestone: [3A](https://github.com/Extender92/CarExpenseCalculator/milestone/7).

All seven issues #55–#61 are delivered through merged PRs #72–#78.
The [acceptance report](household-stage-3a-verification-report.md) records the
approved final merge `15b281f` and green merged-main CI: 699 backend,
195 frontend and 33 Chromium tests. Stage 3A is complete.

| Key | Issue | Deliverable | Prerequisite work |
| --- | --- | --- | --- |
| A1 | [#55](https://github.com/Extender92/CarExpenseCalculator/issues/55) | Implement shared household inputs and purchase financing | Documentation merge |
| A2 | [#56](https://github.com/Extender92/CarExpenseCalculator/issues/56) | Implement partial ownership costs, energy, depreciation and sensitivity | A1 |
| A3 | [#57](https://github.com/Extender92/CarExpenseCalculator/issues/57) | Implement leasing, payment calendars and average budget checks | A2 |
| A4 | [#58](https://github.com/Extender92/CarExpenseCalculator/issues/58) | Persist household inputs, explicit legacy transition and one shared draft | A3 |
| A5 | [#59](https://github.com/Extender92/CarExpenseCalculator/issues/59) | Expose household calculation and persistence APIs with generated frontend types | A4 |
| A6 | [#60](https://github.com/Extender92/CarExpenseCalculator/issues/60) | Build Swedish household profile, current cost editing and draft recovery flows | A5 |
| A7 | [#61](https://github.com/Extender92/CarExpenseCalculator/issues/61) | Verify the complete household calculation stage | A6 |

## Stage 3B - Comparison and buying scores

Tracker: [#12](https://github.com/Extender92/CarExpenseCalculator/issues/12). Existing milestone ID 3 is
renamed [3B - Comparison and buying scores](https://github.com/Extender92/CarExpenseCalculator/milestone/3).
Every 3B item depends transitively on completed 3A acceptance, not on closing
a tracker merely because its planning has been refined.

The 2026-09-07 audit confirms that #62's prerequisites, planning PR #54 and
acceptance #61 / PR #78, are merged on `main`. Existing specifications and Core
listing contracts provide the implementation basis. #62 is closed through
approved PR #80, merged as `60d2b49ab910ba17352637cc94b6649df06b117b` with green
merged-main CI. See the [vehicle-facts implementation record](vehicle-facts-implementation-plan.md).
The [#63 preparation audit](buying-rules-implementation-preparation.md) verifies
all its prerequisites; #63 is closed through approved PR #82. #64 is closed
through approved PR #83, merged as `77939ff2c013dc6e1b3db01059aeb50f0be0b3dd`
with [green main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34221394540).
Its [storage/API contract](comparison-api.md) is on main. The user confirmed
comparison of all saved cars without a fixed total count on 2026-09-08. The new
[#85 backend handoff](all-vehicle-comparison-preparation.md) precedes #65.
Six of the seven current stage 3B implementation items are delivered.
The [#65 workspace preparation](comparison-workspace-preparation.md) records
all six accepted choices. Preparation PR #84, #85 / PR #86 and #65 / PR #87 are
merged. Main `4ba0a0b0f386f7e077c4466a046726cdf3a9df97` has
[green CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34274854144):
1,012 backend, 255 frontend and 55 Chromium tests pass.
The [#66 PDF report](comparison-pdf.md) is now merged through approved PR #89,
as `72ded81784950860ab9ab186a5f4c30974ee2951`. Its new baseline is 1,012 backend,
273 frontend and 63 Chromium tests. The
[#67 preparation](comparison-stage-3b-preparation.md) verifies all functional
dependencies and was merged through approved PR #90. After readiness was
rechecked, the user assigned execution and #67 moved to `status:in-progress`.
The [acceptance report](comparison-stage-3b-verification-report.md) records
1,012 backend, 273 frontend and 69 Chromium passes plus fresh PDF/native-print
inspection. Separate acceptance merge approval remains; #12 and the milestone
stay open until the final stage audit. No later work is assigned by this update.
No registry, AI or service-provider decision blocks this manual comparison stage.

| Key | Issue | Deliverable | Prerequisite work |
| --- | --- | --- | --- |
| B1 | [#62](https://github.com/Extender92/CarExpenseCalculator/issues/62) | Define extended vehicle facts and provenance for buying criteria | A7 |
| B2 | [#63](https://github.com/Extender92/CarExpenseCalculator/issues/63) | Implement deterministic buying rules, weighted score intervals and ordering | B1 |
| B3 | [#64](https://github.com/Extender92/CarExpenseCalculator/issues/64) | Persist current buying profiles and vehicle facts and expose comparison APIs | B2 |
| B3a | [#85](https://github.com/Extender92/CarExpenseCalculator/issues/85) | Compare all current vehicles with coherent server-side ordering | B3 and preparation PR #84 |
| B4 | [#65](https://github.com/Extender92/CarExpenseCalculator/issues/65) | Build shared comparison tables and editable buying priorities | B3a and preparation PR #84 |
| B5 | [#66](https://github.com/Extender92/CarExpenseCalculator/issues/66) | Export the current comparison as a printable PDF report | B4 |
| B6 | [#67](https://github.com/Extender92/CarExpenseCalculator/issues/67) | Verify complete comparison, buying scores and PDF export | B5 |

## Independent later refinement

These issues remain **status:needs-refinement** and do not block 3A/3B. Registry
and mileage-based service work have no delivery milestone until separately
refined. AI assistance belongs to milestone 5 under tracker #14.

| Key | Issue | Refinement | Prerequisite work |
| --- | --- | --- | --- |
| F1 | [#68](https://github.com/Extender92/CarExpenseCalculator/issues/68) | Refine permitted registry access and evidence freshness | Documentation merge |
| F2 | [#69](https://github.com/Extender92/CarExpenseCalculator/issues/69) | Refine evidence-backed maintenance and repair AI suggestions | A7 |
| F3 | [#70](https://github.com/Extender92/CarExpenseCalculator/issues/70) | Refine mileage-triggered service and tyre scheduling | A7 |

Tracker [#14](https://github.com/Extender92/CarExpenseCalculator/issues/14) includes later evidence-backed
maintenance/repair suggestions with explicit adoption.
[#13](https://github.com/Extender92/CarExpenseCalculator/issues/13) and
[#15](https://github.com/Extender92/CarExpenseCalculator/issues/15) remain on hold. Closed milestones
0-2 are not reopened. No provider, AI call, implementation, or deployment is
authorized by creating this queue.

## Dependency and readiness policy

Every issue body includes scope/exclusions, published document links, contracts,
explicit blocking issues, observable acceptance and relevant existing commands.
All issues depend on the planning PR merge; table prerequisites are additional.
The implementation chain is A1 through A7, then B1, B2, B3, B3a and B4 through B6. Trackers depend
on their children; children never depend on their own tracker. Later work is
not a prerequisite for the manual flow. This direction avoids dependency cycles.

After each separately approved merge, audit the next assigned preparation item:
required specifications must be on main, no unresolved decisions/dependencies
may remain, and no newer conflicting work may exist. Only then may the
coordinator mark it status:ready. Subsequent issue
completion does not automatically promote the next issue. Implementation still
requires explicit assignment and its own focused issue branch/PR.
