# Household workspace

## Implemented scope

Issue #60 implements the Swedish stage 3A frontend over the existing
[household API](household-api.md) and [calculation/persistence contracts](household-calculations.md).
It adds no HTTP contracts or migrations. Calculation/result version remains 2;
storage version remains 1. [Stage acceptance](household-stage-3a-verification-report.md)
was executed for #61 on the report's identified commit. Cross-vehicle
comparison tables, ranking, buying scores and PDF export remain stage 3B.

| Route | Interface |
| --- | --- |
| `/manual` | **Manuell kalkyl** opens the household workspace. |
| `/manual?vehicleId=<uuid>` | Opens a registered vehicle's current inputs. |
| `/manual?listingVehicleId=<uuid>` | Opens the same workspace with explicit listing review. Existing listing links remain valid. |
| `/manual/transition` | **Granska äldre underlag** reviews the whole pending legacy set. |
| `/manual/legacy` | **Äldre kalkyl** preserves the v1 editor for unconverted vehicles; its `listingVehicleId` query remains supported. |

The shared profile appears first, followed by registered vehicles in stable
registration-number order, the shared draft slot, and the active vehicle editor.
Each vehicle shows its input status and complete or known period/month/per-mil
amounts. No winner or cost-based sorting is introduced. Collapsible sections
separate acquisition/residuals, energy, operating costs, repairs, lease payments,
calendars and cash/cost reconciliation. Field labels and errors are Swedish;
error summaries receive focus after rejected saves and lead to the relevant
control, opening its containing details when necessary.

## Editing and exact transport

`features/household` separates API requests, text-valued form definitions,
workspace state, batching, listing/draft adapters, legacy review and result
presentation. API types are derived from the generated OpenAPI schema.
`WorkspaceProvider` outlives individual routes and owns tab-local memory through
`HouseholdWorkspace` and `useSyncExternalStore`. Routes are loaded separately.
No local/session storage or implicit server writes are used.

Missing profile values have no financial defaults; purchase and normal
sensitivity mode are categorical defaults. Editable numeric values remain
`Numeric` text. The pinned [lossless-json 4.3.1](https://github.com/josdejong/lossless-json/tree/v4.3.1)
adapter parses every household JSON number, including revisions, without an
intermediate JavaScript floating-point conversion. Serialization writes JSON
numbers, never quoted number strings. It rejects invalid text and values that
cannot be represented exactly as a .NET decimal. Comma and point are accepted;
mil/km conversion moves decimal digits exactly. BigInt-based formatting rounds
only the displayed amount. Browser code does not recalculate costs, energy,
loan schedules, totals or budget verdicts.

Null, explicit zero, confirmed empty collections and included costs with extras
remain separate choices. Changing sensitivity mode does not fill missing values
or reorder the three scenarios. Tax and insurance use single quoted amounts as
required by Core. Changing the household period leaves a fixed residual's own
period unchanged. End fees use the lease's final month. The explicit repeated
lease-fee action fills a chosen 1–120 interval and asks before replacing existing
payments; no unknown fee/refund becomes zero.

## Preview generations

Edits debounce for 500 ms; **Beräkna nu** calculates immediately. Every generation
captures one effective profile, ordered candidates and unresolved review inputs.
Older transports are aborted, and their responses are ignored even if transport
cancellation is ineffective. Previously displayed results remain visibly stale
until the new generation finishes. Results and per-candidate failures are then
published together, never merged with previous generations.

All registered summaries are read; detail reads have at most four requests in
flight. Preview batches have at most 100 registered candidates and at most
2 MiB of actual serialized UTF-8 bytes, with at most two calls in flight. A
manual candidate without registration gets its own call in that generation.
Oversized or structurally invalid candidates receive local errors without
preventing valid candidates from running. Serializable numeric range errors
remain server inputs so independent calculation sections can survive. Stable
candidate keys and source-item keys survive batching and map field errors back
to the correct editor. Incomplete legacy mapping decisions remain unresolved
until an existing target is selected; review amounts are never added implicitly.

Profile, vehicle and draft reads refresh on route return, window focus or
**Uppdatera serverläget**. Dirty forms are retained. Remote changes expose a
side-by-side input review and explicit choices before replacing local values or
rebasing a later save. A storage outage leaves manual previews available.

## Explicit persistence and recovery

**Spara hushållsprofil** and **Spara bilunderlag** write separate resources with
their own expected revisions. A save completing after further editing advances
the saved revision but retains newer form values. A registration changed during
a pending create remains a separate new editor; the completed save appears in
the overview. The same rule applies while adopting a new-car draft. Failures
never trigger automatic retries.

**Spara utkast** writes the singleton slot and requires registration, content and
the slot revision. Existing vehicles retain their UUID and original revision.
Replacing another registration is an explicit choice. Opening a draft never
consumes it. **Ta utkastet i bruk** requires any draft edits to have been saved,
uses atomic server adoption and then rereads the empty slot's new revision.
Failure leaves recovery data in place. Listing-only drafts keep their cost part
omitted until the user edits costs.

The URL workspace can save **Spara gemensamt annonsutkast**. The same reviewed
listing/provenance contract is used, with exact text-valued amounts and an exact
origin revision when opening a saved listing for this path. Existing transient
URL cards remain separate in-memory work. Draft adoption writes only the
supplied parts. Listing prices, tax and consumption enter household editing only
through their explicit use buttons. An explicit review choice acknowledges the
listing version on save; concurrent listing changes still cause revision
conflicts. Rebasing a vehicle does not silently acknowledge a newer listing.

The transition page shows original legacy inputs, the API's unambiguous
suggestions and every stable review item, including 50 recurring plus 50
one-time sources. Decisions are **Behåll för granskning**, **Mappa till
kostnad/energi**, or **Ta bort**. Mapping chooses one existing target or creates
one in an explicitly selected category. Unknown energy identity/basis and
payment timing remain unknown. A single explicit confirmation sends the new
profile, complete pending set and all profile/transition/vehicle revisions.
Oversized confirmation is rejected before sending, never truncated or split.
No individual legacy vehicle provides default household assumptions.
Mapping targets must match the original cost/energy kind and may belong to only
one source. Invalid mappings remain unresolved in previews. Deleting one car
preserves other cars' local review decisions without accepting a new transition
revision; confirmation still requires reviewing the current server set.

Permanent deletion asks for confirmation and clears the vehicle's local editor,
results, matching draft and pending reads. The profile is retained. The URL and
legacy pages now clear the deleted vehicle's local form/card as well and notify
the shared workspace. A stale response cannot restore a deleted vehicle.

## Verification

Focused tests are in `src/frontend/src/features/household/*.test.*`; real
single-origin browser lifecycles are in
[household-workspace.spec.ts](../src/frontend/e2e/household-workspace.spec.ts).
They cover exact decimals/revisions, all sensitivity modes, null/zero/empty/
included values, UTF-8/count batching, reversed responses, edits during saves,
parallel reads, storage outage, draft adoption/reload, lease coverage/budgets,
electric/hybrid/kg energy, full legacy collections, two browser contexts,
deletion, navigation, keyboard focus and a 390-pixel viewport.

Run the [repository checks](../AGENTS.md#verification), regenerate OpenAPI on
port 5090 and require no schema drift. Use the isolated PostgreSQL 18/fake
extractor/Nginx stack from [URL verification](url-analysis-verification.md).
Playwright uses one worker because the profile and draft slot are shared;
concurrency is exercised explicitly with separate browser contexts. New fixtures
remove their vehicles and draft content in teardown even after assertion errors.
The acceptance log verifier reads up to 32 MiB so the larger household suite's
complete SQL/access log is inspected instead of hitting Node's 1-MiB default.
The additional [stage acceptance suite](../src/frontend/e2e/household-acceptance.spec.ts)
checks A1–A11 through HTTP and the UI, shared multi-car edits, two-browser
profile/draft recovery and two-car legacy transition with corrupt results.
The [report](household-stage-3a-verification-report.md) records practical and
visual observations, limits and stage approval gates.

Acceptance fixed interpretation of stored sensitivity trios whose unused
`single` member is null, recovery of the empty draft slot after vehicle deletion,
Swedish labels for original legacy inputs, and a navigation race that could
reopen the previous URL-linked car immediately after choosing **Ny bil**.
The old URL remains consumed until navigation commits; browser Back can still
reopen it. Unknown amounts remain distinct
from incomplete sensitivity trios, which are structural form errors. No values
are filled from another sensitivity mode and no HTTP contract changed.
