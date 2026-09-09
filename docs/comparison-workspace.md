# Swedish comparison workspace (#65)

## Delivery status

Delivered through approved [PR #87](https://github.com/Extender92/CarExpenseCalculator/pull/87)
on 2026-09-08, merge `4ba0a0b0f386f7e077c4466a046726cdf3a9df97`, with
[green main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34274854144).
PDF remains #66, with a [preparation audit](comparison-pdf-preparation.md);
complete stage 3B acceptance remains #67. See the
[verification evidence](household-comparison-verification.md#issue-65-workspace-evidence).

The screen uses the existing [comparison API](comparison-api.md) and
[deterministic specification](comparison-and-buying-scores.md). There are no new
endpoints, schema changes, migrations, local cost calculations or score formulas.
Rule/result/complete-transport versions remain 1; household versions remain 2
and storage formats 1. `RuleBasedSearch` now means the delivered comparison
screen is available. It does not enable discovery or AI.

## Using the workspace

Open **Jämförelse** at `/search`. It starts with all saved cars and a visible,
editable local calendar date. **Använd dagens datum** changes the date explicitly.
The same date, shared household profile and priorities apply to every candidate.
An absent household profile has no financial defaults and absent rules are empty.

The main table stays visible. Cost order is the default; choose priority order
explicitly. Both orders and all recommendation markers come from the server.
Unknown or insufficiently verified criteria retain score intervals and weighted
coverage. A zero weight disables that criterion for every car. Rejected cars
remain visible; incomplete costs display **känd del**. Stale results cannot show
recommendations. The full-inventory summary also identifies an off-page winner.

Each page contains **50 cars**. Every detail table follows the same page and
order. Recalculation preserves page/expanded sections, sorting resets page one,
and deletion clamps the page to the remaining range. Pagination never narrows
the API request. **Öppna alla** and **Stäng alla** control seven detail groups:
requirements/scores/sources; financing/depreciation/equity; energy/tax/insurance;
service/repairs/custom costs/reserve; leasing; payments/budgets/reconciliation;
and the three sensitivity views already returned in the same response.

Expand the profile or rules to edit. The catalogue contains fifteen vehicle
criteria, three derived cost criteria and two budget criteria. Budget criteria
have hard rules and explanatory signals, but no score weights. New requirements
and preferences require an explicit evidence choice and applicable goals.
All amounts use text-backed exact decimals; comma and point are accepted.
Unknown, explicit zero, empty and included collections remain distinct through
the reused household forms. The browser does not determine budget or score
outcomes from displayed rounding.

Select a registration number to edit its facts. Existing observations and listing
proposals show separate evidence and listing versions. Reading a proposal does
not adopt it. Explicit actions preserve, clear, mark not applicable, set a manual
value, import a listing observation, record a conflict or resolve a conflict.
Manual actions never claim registry evidence. Service dates/mileage/notes and
source-labelled condition text remain independent facts, without diagnosis or
automatic interpretation. A calculation-owned price is explained with a link to
the economic editor; a missing calculation price is not filled from the listing.

**Spara hushållsprofil**, **Spara köpkrav och prioriteringar** and **Spara
biluppgifter** save separate resources. Financial inputs save on `/manual`.
**Bekräfta sparat kostnadsunderlag** and its revoke action are separate writes.
Unsaved economic changes must be saved first. Preview-only confirmation is
labelled unsaved and bound to the effective cost assumptions; changing those
assumptions requires a new confirmation. Saving independent facts does not
silently persist or discard a preview-only confirmation.

## Navigation, revisions and recovery

Both providers live above routes. The household provider remains the only owner
of profile and stored economic editing. The comparison provider owns rules,
per-car fact actions, date, manual candidates and complete results. Reading the
comparison baseline does not start household-page previews. Own economic writes
publish their acknowledged revision to the comparison; fact writes acknowledge
the same vehicle revision to the household editor. Shared write coordination
prevents concurrent writes against the same local base.

Edits made after a save begins remain dirty when its response arrives. A changed
listing/conflict reference or a reverted action during a pending save requires
review against the returned base. Refresh on focus, return or explicit reload
never silently discards edits. Changed baselines show profile/rule differences,
counts and revisions. Fact refresh exposes current values and sources before
the user chooses server or reviewed local values. Old listing/conflict references
cannot be attached to a newer revision automatically. Even after an own write,
an unexpected concurrent change elsewhere in the inventory invalidates the
result and requires baseline review.

Economic links carry the car, section and field, open the proper details and
focus after rendering. Indexed cost errors are bound to stable item keys before
navigation and resolved against the current row order. A link whose item was
removed cannot silently focus another item's value. Existing `vehicleId` and
`listingVehicleId` links still work. **Tillbaka till jämförelsen** returns without
discarding tab-local editing. Existing household prompts still protect replacement
of dirty economic editing. The legacy review/transition editor remains the owner
of those decisions; stored preview obtains unresolved items from the server.

Full deletion uses the existing confirmation and vehicle-delete route. It removes
local facts, stale results and matching household/draft editing, and cancels
pending generations. Both shared profiles remain. Other existing deletion routes
broadcast the same invalidation. Reload warnings cover dirty work; there is no
local storage or implicit server save.

## Explicit manual mode

Choose **Fristående manuella bilar** explicitly. The current common profile and
rules become manual assumptions; saved cars, revisions and evidence are not
copied. The manual collection starts empty. Add registered candidates and edit
their financial inputs using the same household fields at
`/manual?comparisonCandidateId=...`. This context is tab memory and does not need
PostgreSQL. Mode changes retain each mode's work. A database failure never selects
manual mode automatically. Refreshing the browser clears manual candidates.

## Transport and current results

The [API client](../src/frontend/src/features/comparison/api.ts) uses generated
types and the existing `lossless-json` 4.3.1 adapter. Baseline reads are thin;
fact/proposal details are lazy, with four concurrent reads at most. Stored
`preview-all` requests contain only changed cars in `overrides`; unsaved new cars
and unadopted drafts do not become extra saved candidates.

The [workspace](../src/frontend/src/features/comparison/workspace.ts) debounces
500 ms and exposes **Beräkna nu**. One request captures a whole generation. Older
requests are aborted, at most two transports remain active, and only the latest
unsent generation is queued. No automatic retry, batch concatenation or omission
of an invalid candidate occurs. Numeric domain errors go to the server for
independent outcomes; malformed text/structure blocks the complete request with
linked errors.

The client measures actual UTF-8 bytes but imposes no fixed 32-MiB preview cap;
the deployment's server limit is authoritative. Rule/fact writes retain 2 MiB.
413, busy, timeout, baseline conflict, connection and malformed-body failures
have Swedish recovery text. Full JSON is parsed before checking request/mode/
baseline/count/identity/version/order consistency across all three views.
Only then are all views published together. Failure retains explicitly stale
results and suppresses recommendation markers. Sensitivity detail expansion
does not request a new calculation or create new confirmation.

## Verification and remaining work

The [comparison browser suite](../src/frontend/e2e/comparison-workspace.spec.ts)
checks real Nginx/API/PostgreSQL responses alongside rendered results; focused
tests under [the comparison module](../src/frontend/src/features/comparison)
cover response integrity, exact transport, scheduling, revisions, form states
and rendering. Existing 3A, URL and legacy suites remain in the full run.
The [matrix](household-comparison-verification.md#issue-65-workspace-evidence)
records commands, counts, reruns and cleanup outcomes. #66 adds PDF; #67 performs
the combined final acceptance. No claim is made about live AI, external discovery,
authentication, public hosting or production/Unraid data.
