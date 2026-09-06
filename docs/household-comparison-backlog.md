# Household and comparison delivery backlog

## Status and publication gate

The [product plan](household-comparison-plan.md), normative
[household](household-calculations.md) and
[comparison](comparison-and-buying-scores.md) specifications, and
[verification plan](household-comparison-verification.md) define this queue.
Features are not implemented by the planning PR. All implementation items are
initially **status:blocked**, including A1, until the documentation PR is merged
and their prerequisites are complete. Trackers are overviews, never assignments.

## Stage 3A - Household calculations

| Key | Issue | Deliverable | Prerequisite work |
| --- | --- | --- | --- |
| A1 | Pending publication | Implement shared household inputs and purchase financing | Documentation merge |
| A2 | Pending publication | Implement partial ownership costs, energy, depreciation and sensitivity | A1 |
| A3 | Pending publication | Implement leasing, payment calendars and average budget checks | A2 |
| A4 | Pending publication | Persist household inputs, explicit legacy transition and one shared draft | A3 |
| A5 | Pending publication | Expose household calculation and persistence APIs with generated frontend types | A4 |
| A6 | Pending publication | Build Swedish household profile, current cost editing and draft recovery flows | A5 |
| A7 | Pending publication | Verify the complete household calculation stage | A6 |

## Stage 3B - Comparison and buying scores

Tracker: [#12](https://github.com/Extender92/CarExpenseCalculator/issues/12). Existing milestone ID 3 is
renamed [3B - Comparison and buying scores](https://github.com/Extender92/CarExpenseCalculator/milestone/3).
Every 3B item depends transitively on completed 3A acceptance, not on closing
a tracker merely because its planning has been refined.

| Key | Issue | Deliverable | Prerequisite work |
| --- | --- | --- | --- |
| B1 | Pending publication | Define extended vehicle facts and provenance for buying criteria | A7 |
| B2 | Pending publication | Implement deterministic buying rules, weighted score intervals and ordering | B1 |
| B3 | Pending publication | Persist current buying profiles and vehicle facts and expose comparison APIs | B2 |
| B4 | Pending publication | Build shared comparison tables and editable buying priorities | B3 |
| B5 | Pending publication | Export the current comparison as a printable PDF report | B4 |
| B6 | Pending publication | Verify complete comparison, buying scores and PDF export | B5 |

## Independent later refinement

These issues remain **status:needs-refinement** and do not block 3A/3B. Registry
and mileage-based service work have no delivery milestone until separately
refined. AI assistance belongs to milestone 5 under tracker #14.

| Key | Issue | Refinement | Prerequisite work |
| --- | --- | --- | --- |
| F1 | Pending publication | Refine permitted registry access and evidence freshness | Documentation merge |
| F2 | Pending publication | Refine evidence-backed maintenance and repair AI suggestions | A7 |
| F3 | Pending publication | Refine mileage-triggered service and tyre scheduling | A7 |

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
The implementation chain is A1 through A7, then B1 through B6. Trackers depend
on their children; children never depend on their own tracker. Later work is
not a prerequisite for the manual flow. This direction avoids dependency cycles.

After separately approved merge, audit A1 first: documentation must be on main,
no unresolved decisions/dependencies may remain, and no newer conflicting work
may exist. Only then may the coordinator mark it status:ready. Subsequent issue
completion does not automatically promote the next issue. Implementation still
requires explicit assignment and its own focused issue branch/PR.
