# Household HTTP contracts

## Implemented boundary

Issue #59 exposes the [household calculation](household-calculations.md#partial-preview-and-http-contract)
and [persistence](household-calculations.md#implemented-household-persistence)
contracts. Core calculation/result versions remain **2**, storage version **1**.
There is no new migration, result cache, history, automatic save or AI invocation.
The [Swedish household workspace](household-workspace.md) is implemented in #60;
practical stage acceptance is recorded in the [#61 report](household-stage-3a-verification-report.md).
Its exact JSON adapter preserves input
decimals/revisions without changing this HTTP contract or the generated schema.
Existing manual-calculator and listing routes retain their contracts, with
additive recovery metadata for v1 writes to converted vehicles.

API-owned DTOs are defined in
[Contracts/Households](../src/backend/CarExpenseCalculator.Api/Contracts/Households/HouseholdInputs.cs).
They use JSON numbers mapped directly to decimal, strict string enums, and explicit
Core/store mappings. Effective input values are never rounded. Result amounts
round to two decimals, other derived quantities to three, away from zero.
Financing detail projection rounds only after Core has completed calculation;
the API never recomputes budgets from rounded display values.

## Preview

`POST /api/household-calculations/preview` accepts `{ requestId, profile, vehicles }`.
The opaque generation ID has 1-120 nonblank characters and is echoed unchanged.
Each candidate contains `input`, optional `registrationNumber`, and required
`unresolvedLegacyItems` (`[]` when none). `input.candidateKey` is trimmed and
unique. A single manual preview may omit registration. Multi-car previews require
distinct normalized registrations. No database values are fetched or saved.

The response contains the normalized effective profile, request ID, currency,
versions, active sensitivity mode, profile errors and vehicles in request order.
Each vehicle contains normalized input, identity, outstanding review facts,
`isCostComparable`, and `sections` with financing details, depreciation, energy,
operating categories, repair allowance, leasing, calendar, budgets, reconciliation
and totals. A missing field or invalid numeric value affects only dependent parts.
`isCostComparable` follows the complete ownership cost, not payment-date completeness.

For example, this request returns 200 with known financing/depreciation and
missing operating inputs; it does not return a complete ownership total:

```json
{
  "requestId": "edit-1",
  "profile": { "periodMonths": 12, "purchaseCashSek": 30000 },
  "vehicles": [{
    "registrationNumber": null,
    "unresolvedLegacyItems": [],
    "input": {
      "candidateKey": "manual",
      "priceSek": 25000,
      "residual": {
        "mode": "fixedAmount",
        "value": { "single": 20000 },
        "periodMonths": 12
      }
    }
  }]
}
```

Sensitivity is either `{ "single": 100 }` or all three values:
`{ "favorable": 80, "baseline": 100, "cautious": 150 }`. Mixing these forms,
an empty object or an incomplete trio is a structural error. Null is unknown.
Category null, `{ "isIncluded": false, "items": [] }`, and
`{ "isIncluded": true, "items": [...] }` mean unknown, confirmed zero, and
an included base with explicit extras, respectively. Items must be supplied when
the category exists. All sensitivity modes, evidence and timing are retained.
Omitted acquisition type defaults to purchase, sensitivity mode to baseline and
listing-link mode to preserve. No economic defaults are supplied.

Unknown properties in new household input DTOs are rejected, including client
result snapshots, status flags, affected-section masks and per-car household
overrides. Reviewed listing inputs reuse the existing listing contract and its
provenance rules; no verification is inferred by the household API.

## Persistence envelopes

The [route table](household-calculations.md#partial-preview-and-http-contract)
defines methods and status codes. Successful reads and updates return 200,
vehicle creation returns 201 with a Location header, and whole-vehicle deletion
returns 204. Draft deletion returns 200 with null input and the new slot revision.

| Operation | JSON request |
| --- | --- |
| Profile PUT | `{ expectedRevision, input }` |
| Vehicle POST | `{ registrationNumber, cost }` |
| Vehicle PUT | `{ expectedRevision, cost }` |
| Draft PUT | `{ expectedRevision, input, replaceExisting }` |
| Draft adoption | `{ expectedRevision }` |
| Transition POST | `{ profile, expectedProfileRevision, expectedTransitionRevision, vehicles }` |

A cost write contains `input`, optional `vehicleLabel`, `listingLinkMode`
(`preserve`/`current`), and optional `legacyDecisions`. A decision contains
`key`, `disposition` (`keepForReview`/`map`/`discard`) and optional `targetKey`.
Transition vehicles contain `vehicleId`, `expectedRevision`, and `cost`.
The confirmation must include the exact pending set; no pagination or preview
candidate limit splits the transaction.

Draft input contains registration, cost and/or reviewed listing, and
`baseVehicleId`/`baseVehicleRevision` for an existing vehicle. Cross-registration
replacement additionally requires `replaceExisting: true`. Adoption uses the
stored base revision, returns current vehicle input, and consumes the slot
atomically. Read the draft when its latest revision is needed. Omitted aggregate
parts remain unchanged. No draft has a household profile or automatic expiry.

Every DELETE takes `expectedRevision` in the query. Profile creation and empty
draft operations can use revision 0; existing vehicle writes require a positive
revision. Revision conflicts never trigger automatic retry. The profile begins
empty: GET returns 404 until saved; draft GET returns 200 with null input/revision.

Vehicle listing includes listing-only, legacy-pending and current summaries,
with listing versions and review obligations. GET by UUID returns current inputs
or separately recovered legacy input, independent of old result deserialization.
Whole deletion through either generation of routes removes all vehicle parts
and matching draft, preserving the household profile and draft revision metadata.

## Outstanding legacy review

Review responses pair typed `input` facts with server-derived `reason` and
`affectedSections`. Send only the typed input facts in preview: the API derives
their effect again, without reading PostgreSQL. Limits preserve 50 recurring,
50 one-time, two energy and three scalar facts, with stable unique keys.
An unresolved key cannot simultaneously contribute to current costs or energy.

Review amounts are never automatically added. Known subtotals and independent
parts remain available; affected complete amounts and derived monthly/per-mil
totals and reconciliation are blocked with `legacyReview:<key>` missing markers.
Known payment months remain visible, with unscheduled review sources and partial
calendar/payment totals indicating that the calendar is not exhaustive. Refunds,
internal saving, financing and residual equity remain independent.

A review-affected `withinLimit` budget becomes `unknown`; a safely established
`exceeded`, `invalid`, or `notConfigured` state is retained. The decision uses
Core's unrounded evidence, including when a small exceedance displays as zero.
This is a preview of fully supplied facts, not database verification of their
currency. Saved review decisions are checked against authoritative store items.

## Errors and bounds

Errors use `application/problem+json`. Field validation includes `code:
invalidHouseholdInput`, `fieldErrors` containing path/code/message, and the standard
validation dictionary. Other problems include stable codes and available vehicle
identity and expected/actual revisions.

| HTTP status | Meaning and codes |
| --- | --- |
| 400 | Malformed JSON/envelopes, unparseable numbers, unsupported enums, duplicate keys, excessive collections and supplied invalid saved values or review decisions. Parsed out-of-range preview values instead remain section errors in a 200 response. |
| 404 | `profileNotFound`, `vehicleNotFound`, `draftEmpty` during adoption. |
| 409 | `profileRevisionConflict`, `vehicleRevisionConflict`, `draftRevisionConflict`, `transitionRevisionConflict`, `transitionSetConflict`, `registrationNumberConflict`, `draftReplacementRequired`, `draftIdentityMismatch`, `householdTransitionRequired`, `listingRequired`, `listingLabelRequiresReview`, `unsupportedHouseholdInputVersion`. |
| 413 | `payloadTooLarge`: request or stored payload exceeds 2 MiB. |
| 503 | `householdStorageUnavailable`: database unavailable or household input unreadable; database details are omitted. |

Converted v1 writes return `householdTransitionRequired` with `recoveryRoute`
pointing to `/api/vehicle-cost-inputs/{vehicleId}`. Writes blocked by pending
legacy confirmation point to `/api/household-transition`. Invalid saves do not
write partially. Cancellation propagates to the stores and is not reported as
success or retried. Required constructor members and nullability are validated
when reading storage payloads, so malformed format-1 objects yield controlled
unavailability without changing valid stored representations.

The 2 MiB request limit applies at both API and Nginx, including chunked bodies.
The API buffers at most the limit plus one byte in memory and creates no temporary
request files. Preview accepts up to 100 candidates; saved lists and complete
transition sets do not inherit that cap. Existing Core limits remain: two energy
sources, 50 current items per cost collection, 120 lease payments and 1-120 months.

OpenAPI generation and regression commands are in the
[verification plan](household-comparison-verification.md). Never hand-edit
`schema.d.ts`. Legacy review uses its own nullable cadence enum so adding this
contract cannot widen the existing v1 recurring-cost type.
