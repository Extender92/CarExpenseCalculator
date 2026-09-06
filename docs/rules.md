# Buying rules

## Delivery status

Buying rules are planned, not implemented. Stage 3B follows the shared
household-calculation work in stage 3A. The current example semantics below
remain an optional starting profile. The accepted catalogue, formulas, evidence,
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

Warnings do not automatically reject a car unless the user promotes them to hard rules. Examples include rust, broken air conditioning, short inspection validity, unclear service history, import history, discrepancies between sources, and disclosed repair needs.

### Positive signals

Examples include a long recent ownership period, documented service history, recent inspection, consistent mileage history, and complete source data.

## Evaluation principles

- All numerical comparisons are deterministic and unit-aware.
- A source statement and a verified fact are different values with different confidence.
- Every result records the evaluated rule version.
- Rule explanations are generated from deterministic templates; AI may rephrase them but may not change pass/fail state.
- Owner count is a configurable signal rather than a universal measure of vehicle quality.

## Planned configurable priorities

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
