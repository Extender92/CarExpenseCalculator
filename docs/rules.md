# Buying rules

## Rule categories

### Hard requirements

A known failure excludes a car from the result set. Unknown values do not silently pass; they produce a `needs verification` result.

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

