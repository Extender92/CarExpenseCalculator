# Comparison persistence and HTTP contract

## Delivery status

Implemented for issue #64 through approved
[PR #83](https://github.com/Extender92/CarExpenseCalculator/pull/83), merged as
`77939ff2c013dc6e1b3db01059aeb50f0be0b3dd` on 2026-09-08 with
[green main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34221394540).
Core facts (#62 / PR #80) and evaluation (#63 /
PR #82) are already merged. UI #65, PDF #66 and whole-stage acceptance #67
remain separate deliveries. No comparison screen or feature flag is enabled.

The [#65 preparation](comparison-workspace-preparation.md) distinguishes these
implemented contracts from the pending workspace and its readiness gate.

The [normative comparison specification](comparison-and-buying-scores.md) owns
criteria, evidence, exact scoring and ordering. The API composes those Core
operations with current stored input; it never accepts calculated scores,
budget outcomes, verification flags or affected-section lists from clients.

## Versions and persistence

Rule/result versions remain **1**, household calculation/result versions **2**,
household storage **1**, and comparison storage is independently **1**.
The generated TypeScript contract is regenerated from the running API; clients
should use the existing lossless JSON adapter for decimal input and int64
revisions. Money is not converted through floating point by the backend.

Migration `20260908103211_AddComparisonPersistence` adds:

| Table | Current contents |
| --- | --- |
| `rule_profile` | Singleton ID 1, nullable typed JSONB rule input, revision, schema version. Initially null/revision 0. |
| `vehicle_comparison_facts` | One FK to the existing vehicle UUID, typed JSONB facts and per-observation source listing versions, reviewed listing version, optional cost confirmation time, schema version. Cascade deletion. |

Infrastructure owns the versioned DTOs and explicit Core mapping.
`IRuleProfileStore`, `IVehicleFactsStore` and
`IComparisonSnapshotStore` expose the operations. All writes obtain the existing
`household_state` transaction lock before checking revisions. Rule saves change
only rule revision; a fact/action save increments vehicle revision exactly once.
No result cache, default rules, implicit import or history is introduced.

The comparison reader uses one PostgreSQL `RepeatableRead` snapshot for profiles,
identities, facts, costs, listings and legacy review. It reads recoverable legacy
input without deserializing old calculation results. Unsupported versions are
not silently overwritten.

### Confirmation and listing lifecycle

A manual edit is an explicit user action; Core replaces its evidence with
user/manual/userConfirmed. The API injects `TimeProvider`, and the shared
operation boundary normalizes new UTC confirmation instants to PostgreSQL's
microsecond precision. Missing old observation/confirmation times remain null.
No write operation can claim registry verification.

Cost confirmation is separate: `preserve` (default), `confirm`, or `clear`.
Confirm requires existing/effective current cost input. It does not confirm
profile assumptions or all facts. Storage saves only the time and reconstructs
the Core input-bound confirmation from the current immutable cost input.

The common household cost writer uses Core
`CostAssumptionConfirmation.IsApplicableTo` to compare complete assumptions.
A changed cost save, draft adoption or transition clears confirmation; equivalent
decimal representations preserve it. Restoring old saved values after a change
does not restore the cleared timestamp. Profile, rule, listing and independent
fact changes do not move confirmation to different assumptions.

Listing proposals are returned separately and never adopted on read. Each
adopted observation retains the actual source listing version. Reviewing version
2 without replacing a retained version-1 observation does not relabel it.
`factsReviewedListingVersion` and `costReviewedListingVersion` remain separate.
A current listing with no matching facts review produces `needsListingReview`.
Explicit review can acknowledge the current advertisement while preserving
older selected facts and their visible source versions.

Ordinary changes preserve existing drafts; their stale base revision prevents
adoption. Full deletion through household, saved listing or legacy scenario
routes deletes facts, confirmations and matching drafts, while retaining both
shared profiles and the draft slot's revision.

## Routes

| Method and path | Success and revision contract |
| --- | --- |
| GET `/api/rule-profile` | 200 with `input`, `revision`, `storageVersion`; 404 before first explicit save, with actual revision 0. |
| PUT `/api/rule-profile` | 200; `{expectedRevision,input}` replaces the entire rule profile. An empty profile is valid. |
| GET `/api/vehicle-facts/{vehicleId}` | 200 for an existing car, including `input:null` when no facts are saved; unknown UUID is 404. |
| PUT `/api/vehicle-facts/{vehicleId}` | 200; `{expectedRevision,input}` applies explicit actions atomically. Never creates an identity. |
| POST `/api/comparisons/preview` | 200 independent evaluation; no writes or AI. Stored mode checks one coherent snapshot; manual mode resolves no store. |

GET facts includes vehicle revision, current/reviewed listing versions, review
marker, cost confirmation, current facts and a separate listing proposal.
Unknown facts have no observations; zero/false/empty fuel sets are real values.
Null condition notes and an explicitly empty collection stay distinct.

### Fact actions

The PUT envelope's `input` contains `edits`, optional
`expectedListingVersion`, `reviewCurrentListing` and `costConfirmation`.
The same action input is accepted under a preview candidate's `facts`.
Each typed field in `edits` accepts:

| `kind` | Payload and meaning |
| --- | --- |
| omitted / `preserve` | Keep the field and evidence; no replacement payload. |
| `unknown` / `notApplicable` | Explicitly clear observations and set the state. |
| `manual` | `manual:{value,observedAt?}`; replace using Core manual operation. Cannot silently resolve a conflict. |
| `listing` | Select the equivalent field from the current saved listing proposal. Requires `expectedListingVersion`. |
| `conflict` | At least two different normalized current values in `observations`. |
| `resolve` | `manual:{value,observedAt?}`; explicit resolution of an existing conflict. |

Conflict observation selectors are `{kind:"current",observationIndex:0}`,
`{kind:"listing"}`, or `{kind:"manual",manual:{value:...}}`.
Current indexes address the field's observations at the checked vehicle revision;
listing selections require the checked current listing version. Values and
sources remain together. Ordinary manual replacement and explicit conflict
resolution create user confirmation; they cannot retain old listing evidence.

Condition notes are a bounded list of these string actions. Omission preserves
the collection; `[]` clears it. Current/listing references address the same note
index. There are at most ten notes, each at most 300 characters. The accepted
fact/collection limits from Core also apply.

Example PUT for an already registered car:

```json
{
  "expectedRevision": 4,
  "input": {
    "edits": {
      "seats": { "kind": "manual", "manual": { "value": 5 } },
      "towBar": { "kind": "manual", "manual": { "value": false } },
      "purchasePriceSek": { "kind": "listing" }
    },
    "expectedListingVersion": 2,
    "reviewCurrentListing": true,
    "costConfirmation": "confirm"
  }
}
```

The price criterion still uses the current purchase **cost input's** price when
that input exists, even if null. A fact action does not edit the cost input.
An absent cost price is never backfilled from the adopted listing price.

## Preview modes

Both modes require `mode`, `requestId` (1–120 nonblank characters), the full
effective `profile`, `rules`, explicit `asOfDate` and ordered `candidates`.
Zero through 100 candidates are allowed. UUIDs and normalized registrations
must be unique, and every candidate needs an ordinary registration number.

### Manual

```json
{
  "mode": "manual",
  "requestId": "generation-1",
  "profile": {},
  "rules": {
    "preferences": [
      {
        "criterionKey": "purchasePriceSek",
        "weight": 3,
        "minimumEvidence": "userConfirmed",
        "zeroPoint": 100000,
        "fullPoint": 20000
      }
    ]
  },
  "asOfDate": "2026-09-08",
  "candidates": [
    {
      "vehicleId": "09086400-0000-4000-8000-000000000001",
      "registrationNumber": "ABC123",
      "facts": {
        "edits": {
          "purchasePriceSek": { "kind": "manual", "manual": { "value": 40000 } }
        }
      }
    }
  ]
}
```

This returns price score 75 while missing household costs remain incomplete.
UUIDs identify only this request. No saved revisions, listing references or
legacy decisions can be asserted. Optional `costInput` uses the
[household input contract](household-api.md); an explicit `costConfirmation:
"confirm"` may adopt that effective input in memory. The response has
`mode:"manual"`, `storageChecked:false` and null saved source revisions.
The caller must explicitly choose this mode; storage failure never causes a
silent switch from stored comparison.

### Stored

The same envelope uses `mode:"stored"` plus:

```json
{
  "storedBase": { "householdProfileRevision": 0, "ruleProfileRevision": 0 },
  "candidates": [
    {
      "vehicleId": "09086400-0000-4000-8000-000000000001",
      "registrationNumber": "ABC123",
      "storedBase": { "vehicleRevision": 3, "listing": { "version": null } }
    }
  ]
}
```

This fragment is combined with the required request/profile/rules/date fields.
The `listing` wrapper and its nullable `version` are required: null means no
listing at the checked revision, not permission to skip the check. Shared
revision 0 means not saved yet. All candidates and shared baselines are checked
in one snapshot; any mismatch fails the request with 409, and missing cars with
404. No automatic reread, retry or overwrite takes place.

Omitted `costInput` uses saved input; a supplied object is a complete replacement
for this preview. Optional fact actions apply to saved facts in memory.
`legacyDecisions` uses the household decision contract and is checked against
server-read review items. The server derives remaining impacts; omission cannot
make unresolved work disappear. Mapped/discarded decisions do not persist.

### Result authority

The response echoes normalized effective inputs, dates, request ID and versions.
Each candidate contains source revisions, source evidence, cost confirmation,
review state and server-derived `unsaved` flags for profile/rules/facts/costs/
decisions/confirmation/listing review. Numeric equality in dirty indicators
does not coerce decimals through floating point. These flags describe the
snapshot used for the request, not a promise that the database cannot change
after the read transaction.

Results retain input order, independent field errors, household cost sections,
hard rules and eligibility, contributions, intervals, coverage, explanations
and signals. `costOrder` and `scoreOrder` are separate UUID lists. Cheapest and
definite-winner flags come directly from Core's unrounded authority.
Unresolved review blocks dependent complete totals, cost scores, derived
monthly/per-mil totals and reconciliation. A dependent budget pass becomes
unknown; safe exceedance, invalid and unconfigured states remain intact.
Review amounts are never silently added to costs.

## Errors, size and cancellation

All new JSON request bodies are limited to **2 MiB of UTF-8 bytes**, including
chunked requests without Content-Length. Both API middleware and Nginx enforce
the limit and return `payloadTooLarge`. The API bounds its in-memory buffer;
it does not spill request bodies into temporary files.

| HTTP | Meaning and representative stable code |
| --- | --- |
| 400 | `invalidComparisonInput` with `fieldErrors` and grouped `errors`: malformed/unknown fields, invalid enums/actions/rules, duplicate identities, invalid persisted values. |
| 200 | Parsed numeric candidate/profile domain errors remain in dependent result sections. Other fields and candidates survive. |
| 404 | `ruleProfileNotFound` or `vehicleNotFound`. |
| 409 | `ruleProfileRevisionConflict`, `profileRevisionConflict`, `vehicleRevisionConflict`, `listingVersionConflict`, `comparisonIdentityMismatch`, `unsupportedComparisonStorageVersion`, or existing unsupported source-version codes. |
| 413 | `payloadTooLarge`. |
| 503 | `comparisonStorageUnavailable`: unavailable database or unreadable stored input, without database details. |

ValidationProblemDetails reports stable paths/codes; conflict ProblemDetails
includes available vehicle UUID and expected/actual revisions. Invalid model
binding and structural action validation do not become successful partial
results. Existing HTTP/storage contracts and v1 recovery errors are preserved.
Cancellation propagates into the store/transaction; it never becomes a success
or triggers automatic retry. Failure leaves aggregate writes uncommitted.

Nullable comparison choices get separate OpenAPI enum components for enum types
previously used only as required values. This prevents nullable choices from
changing existing listing and household result types; runtime enum validation
is unchanged. The configuration uses the documented
[OpenAPI schema reference customization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0).

## Operations and remaining work

Migration is explicit and never runs on API startup. The supported comparison
rollback target is `20260906151351_AddHouseholdPersistence`; it removes rules,
facts and confirmations but retains household data, drafts, legacy scenarios,
listings and vehicle identities. Restoring removed comparison data requires a
backup. See [Unraid migration/rollback](deployment-unraid.md#comparison-storage-migration-and-rollback).
Only disposable PostgreSQL 18 databases are used for migration tests.

The [verification matrix](household-comparison-verification.md#issue-64-persistence-and-http-evidence)
links implementation tests and commands. These HTTP/storage checks prepare #65;
they do not complete the comparison UI, PDF or #67 acceptance.
