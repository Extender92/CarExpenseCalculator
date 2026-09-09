# Issue #65: comparison workspace preparation

## Status and authority

Preparation for [#65](https://github.com/Extender92/CarExpenseCalculator/issues/65),
authorized on 2026-09-08. The original branch was `docs/65-comparison-workspace-preparation`.
[Preparation PR #84](https://github.com/Extender92/CarExpenseCalculator/pull/84)
is merged. The #85 backend extension was merged through approved PR #86 as
`885826a9b367337fa3f7610365f69a8ffa1207bf`. The subsequent #65 readiness audit
confirmed green [main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34237584426)
and 1,012 backend, 195 frontend and 41 Chromium tests. The user then assigned
implementation on `feature/65-comparison-workspace`; #65 is now closed through
approved [PR #87](https://github.com/Extender92/CarExpenseCalculator/pull/87),
merged as `4ba0a0b0f386f7e077c4466a046726cdf3a9df97` with
[green main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34274854144).
The [implemented workspace](comparison-workspace.md) and
[verification evidence](household-comparison-verification.md#issue-65-workspace-evidence)
describe that delivered implementation: 1,012 backend, 255 frontend and 55
Chromium tests passed at that feature's delivery. [PDF preparation](comparison-pdf-preparation.md)
was followed by #66 / PR #89 and whole-stage acceptance #67 / PR #91.
The [acceptance report](comparison-stage-3b-verification-report.md#approved-merge-and-stage-closure)
records completed stage 3B. The earlier audit below is historical context.

The [product plan](household-comparison-plan.md),
[comparison specification](comparison-and-buying-scores.md),
[implemented comparison API](comparison-api.md) and
[issue workflow](issue-workflow.md) remain authoritative. The UI, PDF and
whole-stage acceptance are not delivered by publishing this document.

## Original dependency audit (before #85)

Audited base: `77939ff2c013dc6e1b3db01059aeb50f0be0b3dd` on `main`.

| Prerequisite | Evidence |
| --- | --- |
| Original specifications | [PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54) merged. |
| Stage 3A | #55–#61 delivered; [PR #78](https://github.com/Extender92/CarExpenseCalculator/pull/78) and the [acceptance report](household-stage-3a-verification-report.md). |
| Vehicle facts | #62 / [PR #80](https://github.com/Extender92/CarExpenseCalculator/pull/80) merged. |
| Rules, scores and sorting | #63 / [PR #82](https://github.com/Extender92/CarExpenseCalculator/pull/82) merged. |
| Comparison persistence/API | #64 / [PR #83](https://github.com/Extender92/CarExpenseCalculator/pull/83) merged as the audited base. |
| Main verification | [CI run 34221394540](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34221394540): backend, frontend, OpenAPI and Docker/browser jobs passed. |

The current baseline is **938 backend, 195 frontend and 37 Chromium tests**.
The #64 build had zero warnings/errors and no failed/skipped tests in the final
verification. Its PR records development reruns and the benign Playwright
`NO_COLOR`/`FORCE_COLOR` warning. Preparation does not claim new UI test coverage.
The audited worktree was clean and there were no open competing PRs.

The original implementation dependencies are complete. The user resolved all
six preparation questions on 2026-09-08, including comparison of every saved
car without a fixed total count. This requires the separate
[#85 backend prerequisite](https://github.com/Extender92/CarExpenseCalculator/issues/85).
#85 passed its dependency audit against merged PR #84 and is `status:in-progress`.
#65 remains `status:blocked` pending #85 delivery. It has no open product-choice
gate. After the approved #85 merge, audit #65 against the
[complete-set contract](comparison-api.md#complete-set-comparison-85) and
[verification evidence](household-comparison-verification.md#issue-85-complete-set-evidence).
Each implementation still requires explicit assignment.

## Accepted user decisions

| Question | Decision confirmed on 2026-09-08 |
| --- | --- |
| Comparison membership | All current saved cars are compared together, with no fixed total vehicle-count limit. Separate backend delivery #85 precedes #65. |
| Navigation | **Jämförelse** replaces **Regelsökning** in visible navigation; `/search` remains the route. |
| Table layout | Main table visible immediately; expandable detail tables below it, with **Öppna alla**. |
| Display pagination | Confirmed in the #65 implementation plan: 50 cars per page, shared across all tables; all cars still participate in every calculation. |
| Initial ordering | Start with cost ordering and offer explicit preference ordering. Existing incomplete/rejected/evidence rules apply. |
| Editing | Facts and source review live in comparison; economic inputs open the existing household editor at the right car/section. Dirty editing survives navigation. |
| Evaluation date | Initialize a new workspace with the browser's current local calendar date, show an editable date field, and retain that date until explicit change/update. |

## Implementation inspected during original preparation

| Existing implementation | Handoff |
| --- | --- |
| [Routes](../src/frontend/src/App.tsx), [layout](../src/frontend/src/components/layout/AppLayout.tsx), [dashboard](../src/frontend/src/pages/DashboardPage.tsx) | `/search` is still a placeholder. Preserve `/manual`, `/analyze-urls` and legacy/transition routes. Use **Jämförelse** for the delivered comparison page and explain that it evaluates entered/reviewed candidates. |
| [Household provider](../src/frontend/src/features/household/WorkspaceProvider.tsx), [workspace](../src/frontend/src/features/household/workspace.ts), [page](../src/frontend/src/pages/HouseholdPage.tsx) | Reuse the single effective household profile, revisions, active cost editor and cross-route deletion/change notifications. Do not create independent household overrides. |
| [Numbers](../src/frontend/src/features/household/numbers.ts), [API](../src/frontend/src/features/household/api.ts), [fields](../src/frontend/src/features/household/Fields.tsx), [results](../src/frontend/src/features/household/Results.tsx) | Reuse lossless numeric transport, exact mil/km conversion and Swedish field/result formatting. Extract reusable components without a broad rewrite of the 3A flow. |
| [Household preview scheduler](../src/frontend/src/features/household/preview.ts) | Reuse generation cancellation, byte measurement and bounded reads. Household batch concatenation is not a comparison-wide ranking algorithm. |
| [Comparison HTTP service](../src/backend/CarExpenseCalculator.Api/Comparisons/ComparisonPreviewService.cs), [results](../src/backend/CarExpenseCalculator.Api/Contracts/Comparisons/ComparisonResults.cs), [generated types](../src/frontend/src/api/schema.d.ts) | Separate stored and explicit manual modes, trusted fact actions, review completeness, revisions and server-generated orders. No hand-edited schema or client evidence claims. |
| [Core evaluator](../src/backend/CarExpenseCalculator.Core/Comparisons/ComparisonEvaluator.cs) | A maximum of 100 candidates per evaluation; raw values determine ordering/winners, while response amounts and score ranges are rounded for presentation. |
| [System status](../src/backend/CarExpenseCalculator.Api/Controllers/SystemController.cs) | `RuleBasedSearch` is currently false. #65 may switch this existing flag only with the delivered rule/comparison workspace and matching status tests; no new schema is needed and automatic discovery remains disabled. |

## Complete-set comparison prerequisite

The original `/api/comparisons/preview` accepts at most 100 candidates and 2 MiB UTF-8 per request.
It returns `costOrder`, `scoreOrder` and winner flags for exactly that request's
candidate set. It does not provide a global merge operation or unrounded rank
keys. A stored request checks its own profiles/candidates in one database
snapshot; separate requests are not one database snapshot.

Consequently, copying the household batch scheduler and concatenating comparison
orders would not satisfy the accepted ordering/evidence rules. For example:

- Costs 1,000.001 and 1,000.004 both display as 1,000.00. Registration order
  cannot safely break their apparent tie across separate responses.
- Score lower bounds 80.001 and 80.004 both display as 80.00. Display rounding
  cannot supply the specified exact preference ordering.
- Each one-car request may mark its eligible candidate as a definite winner,
  while intervals [60,80] and [45,85] overlap when evaluated together.
- A failed/missing batch must not make a remaining candidate look like a winner
  over the entire intended selection.

The user chose the complete-set solution. A per-transfer limit or an internal
batch size is a technical processing boundary, not a limit on which saved cars
are compared. #65 must not require selecting a subset to fit those boundaries.
Do not silently truncate candidates, merge local winner flags or recompute costs,
scores, budgets or recommendations from displayed values. A display page or
collapsed panel does not narrow the evaluated set.

The [#85 handoff](all-vehicle-comparison-preparation.md) defines required backend
behavior and regression evidence. The server reads all current saved vehicles,
evaluates manageable groups through the same engine, and decides ordering and
recommendations across the entire set using unrounded measures. The client
does not resend unchanged saved fact/cost payloads merely to include a car.
Membership, revisions, unsaved overlays and all sensitivity views must belong
to one coherent generation. The explicit transport extension and resource
handling are implemented by #85's new baseline/preview-all routes, pending merge.

Use GET `/api/comparisons/baseline`, then POST `/api/comparisons/preview-all`
with the complete effective profile/rules/date and only explicit stored-car
`overrides`. Do not upload unchanged saved facts/costs. Preserve local editing
on `comparisonBaselineConflict`; show the new baseline for explicit resolution.
Do not silently retry/rebase or fall back to manual mode. Manual mode sends the
complete transient candidate list, without stored claims.

The default incoming JSON limit is 32 MiB, configurable through the shared
`COMPARISON_MAX_REQUEST_BYTES` setting. It is not a car-count/output limit.
Use the lossless adapter and actual UTF-8 size for local error reporting; the
server remains authoritative. Handle `comparisonBusy` and `comparisonTimedOut`
without automatic retries. All three views arrive in one response with
`generationId`, request ID, candidate count and stored baseline token. Publish
only after the entire current response parses and its three ordered identity
sets agree. Never publish a surviving group/view from an incomplete response.
Switching displayed sensitivity mode consumes the captured view; it does not
create another confirmation or user edit.

## Workspace and editing handoff

Use a dedicated `features/comparison` area for API adapters, text-valued rule/fact
forms, tab-local workspace state, preview generations and tables. It consumes
generated OpenAPI shapes through the existing lossless adapter. No new library
or local/session storage is required. The comparison provider lives above routes;
the shared household provider remains the single owner of household editing.

The page flow is:

1. Show the comparison mode, explicit evaluation date, current shared household
   assumptions and save/stale indicators. Reuse the profile editor and its
   separate **Spara hushållsprofil** operation.
2. Show every registered alternative with identity and cost/fact/listing review
   state. Never silently discard incomplete or hard-rejected cars. Selecting
   one car for editing does not remove other cars from the comparison.
3. Edit one shared rule profile: hard requirements, required evidence,
   preferences, numeric 0/100 anchors, categorical wishes and weights 0–5.
   Include selectable explanatory signals and their explicit date thresholds.
4. Show the main table immediately, criterion contributions/evidence, and
   expandable detail tables with **Öppna alla**. Start with cost ordering and
   expose an explicit score-order control. Missing fields lead to the relevant
   fact or household editor.
5. Edit the selected car's facts and review its sources. Cost editing remains in
   the existing shared household editor; navigation preserves dirty state.

Start missing rules with an empty profile, never an activated example. All 15
fact criteria, three derived cost criteria and two hard-only budget criteria
use the existing catalogue. Numeric goals may rise or fall; disabled supplied
values still receive validation. Weight zero disables that preference for every
candidate. Missing/weak evidence retains the interval and shared denominator;
do not limit scoring to the facts known for every car. No score or signal can
override a failed hard requirement or missing required evidence.

Use **Spara köpkrav och prioriteringar** only for the rule profile. Editing weights,
goals, dates or shared assumptions triggers previews after 500 ms and never
writes data or calls AI. **Beräkna nu** captures an immediate generation.
Give numeric fields Swedish labels, comma/point input, linked error text and
focusable error summaries. A newly initialized workspace uses today's local
calendar date; internal navigation retains its edited date. The browser supplies
that explicit editable date to the API. **Använd dagens datum** updates it by
user choice; no date is inferred in Core or silently rolled forward at midnight.

## Fact review, modes and saving

| Action or situation | Required behavior |
| --- | --- |
| Read facts or listing proposal | Display values, verification and source versions separately. Reading/importing does not confirm or adopt a listing. |
| Manual edit | Send a typed manual action; never send authoritative verification flags or a client-generated confirmation time. Unknown, zero, false, empty, not-applicable and conflicting facts remain distinct. |
| Adopt advertisement | Explicit field choice against the current listing version. Retained observations keep their original source version. Reviewing the current advertisement does not relabel older values. |
| Conflict | Show observations with their sources; require explicit resolution. An ordinary field replacement cannot silently resolve a stored conflict. |
| Save facts | **Spara biluppgifter** sends pending actions with the expected vehicle revision. Omitted fields are preserved; cost confirmation defaults to preserve. |
| Confirm cost assumptions | **Bekräfta kostnadsunderlag** is a separate action for the current exact cost input. Save dirty cost input before confirming the persisted input. A preview-only confirmation must be explicitly chosen and labelled unsaved. |
| Effective price | Show that a purchase cost input owns its price, including a missing price. Link to that editor; changing an advertised fact cannot change the calculated purchase price or inherit its confirmation. |
| Concurrent save | A completed save advances the saved baseline/revision without overwriting edits made afterward. Do not automatically retry a conflict or silently adopt a newer revision. |
| Refresh/focus/return | Read current server baselines with at most four detail reads in flight. Preserve dirty forms and the active editor; offer explicit review of differences. All existing shared vehicle-change notifications invalidate affected comparison results. |
| Whole-car deletion | Use existing explicit confirmation and full deletion API. Remove the car's editor, facts, pending actions and stale results; retain both shared profiles. Stale requests cannot resurrect the car. |

Stored mode uses #85's complete-set contract, with server-read membership,
saved facts/costs/review items, revision/change detection and explicit unsaved
overrides. The existing #64 selected-request contract remains documented for
compatibility and is not the all-cars workflow. Reuse
the existing legacy-decision editor and send only valid decisions; omitted
reviews cannot manufacture complete costs. A listing-only or legacy candidate
can still have useful independently assessed facts while costs are incomplete.

Manual mode must be chosen explicitly and remains usable without storage.
Registrations are required even for these transient candidates; UUIDs are
request identities. It must not carry saved revisions, listing selectors or
inherited verification claims. A transition to manual mode must identify which
values are being used as manual assumptions and require explicit adoption;
a storage failure never performs that conversion automatically. The read-only
comparison transport resolves no database or AI in this mode. Creating a saved
vehicle remains an explicit existing household/listing operation, never a side
effect of facts saving or previewing.

## Results, generations and accessibility

Every generation captures mode, profile, rules, date, complete membership, all
source revisions, effective car edits and review decisions. Use one immutable
captured set for the active sensitivity and the favorable/normal/cautious detail
views. Each view differs only in its explicit sensitivity mode; missing values
are not filled from another mode. Limit preview transports to two in flight.
Use #85's coherent transport contract; candidate chunks must never be treated
as separate comparisons or independent winners.

Cancel obsolete transports and ignore their replies even if cancellation fails.
Publish the generation together after its required calls settle; old results
stay visibly stale until then. A failed view shows a specific current error and
cannot borrow an old view. Numerical domain errors that the API can interpret
retain independent results. Locally unserializable candidate text stays visible
as an error and suppresses any complete-set recommendation that would
otherwise ignore the candidate. Invalid shared rules block that generation.

| Table or panel | Content and authority |
| --- | --- |
| Main comparison | Registration, status, net selected-period cost, monthly/mil equivalents, known subtotal versus complete total, hard eligibility, score interval and coverage. |
| Rules and priorities | Actual value, required/available evidence, hard outcome, each weighted contribution, reason codes rendered in Swedish, overlap and no-active-criteria states. |
| Financing and depreciation | Cash, principal, interest, fees, residual and end equity from 3A; leasing not-applicable states remain explicit. |
| Energy, tax and insurance | Quantities/units, charging assumptions, each known/complete category and its gaps. |
| Service and repairs | Planned service, known repairs and additional reserve separately; no double counting. |
| Leasing | Contract coverage, fees, deposit/refund, included items/extras and horizon mismatch. |
| Payments and budgets | Month 0 versus later months, external payments/refunds versus internal repair saving, accrual reconciliation and API budget verdicts. |
| Sensitivity | Favorable, normal and cautious costs with their own gaps; only the shared active mode drives main-table ranking. |

Cost/score sort controls consume the server's order for the complete comparison
set. Keep incomplete cars visible and hard failures lower down as **Bortvald**.
Show **Behöver verifieras** separately. Cheapest labels require eligible complete
costs; a definite preference winner requires the server's strict interval test.
Equal displayed values alone are not proof of a tie. Source-labelled signals
never add points. Explanations identify whether a recommendation concerns cost
or the user's priorities.

Use semantic tables, captions and header scopes; preserve registration context
when scrolling horizontally. At mobile width, contain overflow inside the
table/panel and retain all details through accessible expansion. Buttons and
editors work by keyboard, focus returns after dialogs, and status never relies
only on color. Missing-field links open the right section and focus its input.
Retain existing #60/URL/legacy behavior and all three navigation modes.

## Verification handoff and publication

The [verification matrix](household-comparison-verification.md#original-issue-65-workspace-handoff)
maps future UI acceptance to current authority and new tests. Use .NET SDK
10.0.400 and Node 22.22.2. For #65, run frontend ci/lint/test/build, OpenAPI
regeneration with no unexpected schema drift, Compose boundary validation and
the isolated PostgreSQL 18/fake-extractor/Nginx/Chromium suite with one worker.
Retain URL acceptance checks. The existing status-flag adjustment also needs its
backend status regression and ordinary backend CI. No live AI or Unraid data.

This preparation runs documentation/link/diff checks only; ordinary PR CI still
applies. It does not enable a route, change an endpoint, add a migration, update
generated types or implement PDF. The future implementation branch is
`feature/65-comparison-workspace` after readiness and explicit assignment.
The prerequisite implementation branch is
`feature/85-all-vehicle-comparison`; PDF remains #66 and full practical acceptance
remains #67. Existing contracts and version metadata are unchanged in this
preparation; #65 consumes the separately reviewed/generated #85 extension.

Publish the preparation PR without `Closes #65`. Link immutable published docs
from #85/#65 and update tracker #12 to record #64 delivered and #85 before #65.
Keep exactly one status label per issue. Promote #85 only after this preparation
is merged, and #65 only after #85 is also delivered. Publishing an unmerged
specification does not satisfy either gate.

## Deferred cleanup inventory

The user deferred #64 cleanup on 2026-09-08 after direct and scripted deletions
were rejected by the execution tool. No new deletion attempt is part of this
preparation. Retain this inventory until the user removes the files:

- `temp/issue64/`: 4,118 files, 538,009,865 bytes at the last read-only audit.
- Generated `bin/` and `obj/` in the following audited projects (22 directories
  total). Project names below all start with `CarExpenseCalculator.`:
  `src/backend/` contains `Api`, `CodexExtractor`, `Core`,
  `Extraction.Contracts`, `Infrastructure`; `tests/backend/` contains
  `Api.IntegrationTests`, `ArchitectureTests`, `CodexExtractor.UnitTests`,
  `Core.UnitTests`, `Infrastructure.IntegrationTests`,
  `Infrastructure.UnitTests`. These are generated child directories, not
  the projects or source files themselves.
- `src/frontend/dist/` and `src/frontend/node_modules/.tmp/`, `.vite/`,
  `.vite-temp/`. Keep normal installed dependencies.
- Older Windows Temp SDK discovery cache:
  `%LOCALAPPDATA%\Temp\dotnet\runfile-discovery\CarExpenseCalculator-727914507b9949ef295ff19ffad03d80fbb72ed96658fcabf1fd4346a70ca2ca\cache.staging.json`.
  This pre-existing cache was not attributed to #64 and was preserved separately.

The work-owned temporary services/stacks were stopped; none of these artifacts
is tracked by Git. This preparation needs no local test stack or helper scripts.
Do not create additional temporary files merely to record cleanup.
