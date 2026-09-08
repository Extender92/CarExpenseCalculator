# Buying rules

## Delivery status

Issue #63, merged through PR #82, implements deterministic Core buying rules,
weighted score intervals, source-labelled signals and separate cost/preference
ordering. Issue #64 adds [storage and HTTP](comparison-api.md) on its PR branch,
pending separate merge approval. Comparison UI remains #65 work. Stage 3B follows
the shared household calculations in 3A.
The example semantics below remain an optional starting profile, never active
defaults. The accepted catalogue, formulas, evidence,
ordering, and HTTP contracts are normative in
[Comparison and buying scores](comparison-and-buying-scores.md), following the
[Household calculations and comparison plan](household-comparison-plan.md).

## Rule categories

### Hard requirements

A known adequately evidenced failure marks a car rejected and excludes it from
recommended winners. It remains visible lower in the tables as **Bortvald**.
Unknown or insufficiently evidenced values produce `needs verification`.

Initial example profile:

| Rule | Requirement | Boundary behavior |
| --- | --- | --- |
| Tow bar | Required | Missing fails; unknown needs verification |
| Price | SEK 5,000–20,000 | Both limits are inclusive |
| Mileage | At most 20,000 Swedish mil | Exactly 20,000 passes |
| Owners | At most 6 | More than 6 fails; unknown needs verification |

### Warnings

Warnings do not automatically reject a car. The user must configure an accepted
corresponding hard criterion. #63 reports explicit inspection thresholds,
unclear service data and cost/budget completeness. Reviewed condition/repair
notes are source-labelled information, with no automatic interpretation of rust,
air-conditioning faults or free text as verified criteria. Import/history signals
require future supported source facts.

### Positive signals

#63 reports documented service, inspection validity meeting the explicit day
threshold, complete cost inputs and budgets within their limits. Source labels
remain visible and do not promote verification. Ownership duration, inferred
inspection recency and mileage history remain future source-dependent examples.

## Evaluation principles

- All numerical comparisons are deterministic and unit-aware.
- A source statement and a verified fact are different values with different confidence.
- Every result records the evaluated rule version.
- Rule explanations are generated from deterministic templates; AI may rephrase them but may not change pass/fail state.
- Owner count is a configurable signal rather than a universal measure of vehicle quality.

## Implemented Core priorities and later application work

- The accepted catalogue adds transmission, seats, model year, fuel, body,
  drivetrain, locality/county, braked towing capacity, inspection/service facts,
  and complete household cost/budget criteria to the existing example fields.
- Let the user change current criterion priorities and weights. Recalculate
  deterministic scores and expose individual contributions. A score describes
  the current preferences, not permanent vehicle quality.
- Keep hard-rule failures, verification requirements, warnings, and positive
  signals explicit. Weighted preferences never override hard-rule failures or
  establish verification of a fact.
- Distinguish compliance according to an advertisement from user-confirmed or
  registry-verified compliance. Define the required evidence level per rule.
- Keep incomplete candidates visible in cost tables with explicit missing
  components. Complete comparable totals sort cheapest first, incomplete
  alternatives follow, and rejected cars remain below other candidates.
- Numeric scores use the user's fixed 0/100 anchors; categorical scores match
  preferred values. Integer weights 0-5, unknown intervals, weighted coverage,
  and deterministic ties follow the normative specification. Score sorting
  uses the lower bound and never describes overlapping intervals as certain.
  All supported fuel types receive equal product priority.
- Retain only current evaluations and their version/input metadata. Changing
  priorities does not create an evaluation history or saved scenario variants.
