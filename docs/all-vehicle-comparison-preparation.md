# All-vehicle comparison: backend handoff for issue #85

## Decision and delivery order

On 2026-09-08 the user confirmed that the comparison must include **all current
saved cars, without a fixed maximum number of cars**. This is not a request for
a 100-car selection screen. [Issue #85](https://github.com/Extender92/CarExpenseCalculator/issues/85)
provides the backend prerequisite for the [#65 workspace](comparison-workspace-preparation.md).

The approved delivery order is #64 (delivered), preparation PR #84, #85, #65,
#66 PDF, then #67 whole-stage acceptance. #85 and #65 remain `status:blocked`
until their respective prerequisites are merged. After the preparation merge,
audit #85 for readiness and produce its detailed implementation plan before
explicit assignment. The user-facing decisions are resolved; choosing concrete
wire shapes and implementation mechanics belongs to that plan.

This handoff changes no executable code, current HTTP limits, generated schema
or database. Existing comparison/rule versions are 1, household versions 2 and
storage formats 1. The [#64 API](comparison-api.md) remains the implemented
contract until the explicit #85 extension is delivered and verified.

## Current limitation and inspected boundaries

- [ComparisonPreviewService](../src/backend/CarExpenseCalculator.Api/Comparisons/ComparisonPreviewService.cs)
  requires a candidate list and rejects more than 100 candidates. The HTTP/Nginx
  body limit is 2 MiB for one request, not a total inventory definition.
- [ComparisonSnapshotStore](../src/backend/CarExpenseCalculator.Infrastructure/Persistence/Comparisons/ComparisonStores.cs)
  reads only explicitly supplied UUIDs in a RepeatableRead snapshot. It does
  not currently capture the whole saved membership through an all-cars request.
- [ComparisonEvaluator](../src/backend/CarExpenseCalculator.Core/Comparisons/ComparisonEvaluator.cs)
  computes exact cost/score ordering for at most 100 candidates. Its unrounded
  work values are internal; its public result fields are presentation-rounded.
- [Household frontend batching](../src/frontend/src/features/household/preview.ts)
  can concatenate independent cost rows, but cannot combine rounded comparison
  scores or local winner flags into an authoritative global order.

The numeric counterexamples are in the
[workspace preparation](comparison-workspace-preparation.md#complete-set-comparison-prerequisite).
Increasing a count/body constant alone does not verify snapshot coherence,
complete membership or correctness across calculation/transport boundaries.

## Required backend behavior

1. **Read the entire current vehicle set on the server.** Include listing-only,
   unconverted legacy, current purchase and lease alternatives. Reuse existing
   UUID/registration identity. Missing facts or costs never exclude a car; a
   shared recovery draft is not an additional saved car until adopted.
2. **Capture one coherent generation.** Membership, both profiles, vehicle and
   listing revisions, facts, costs and unresolved legacy items must belong to
   one consistent read. All sensitivity views use those same captured car inputs.
   Concurrent additions/deletions cannot be lost between internal read pages.
3. **Retain one calculation authority.** Process manageable groups through the
   existing household/comparison formulas and evidence rules, preserving raw
   decimals for global ordering and winner tests. Share the final ordering
   implementation with existing comparisons rather than maintain two definitions.
   An internal 100-item group is permissible; a 100-car total restriction is not.
4. **Keep transport separate from membership.** The all-saved request should
   reference server-owned current data instead of re-uploading unchanged payloads
   for every car. Common effective assumptions and explicit unsaved overlays must
   still preview without saving. Candidate-level transport fragments are never
   independent comparisons. No client omission can erase a car or review item.
5. **Detect changes without silently rebasing edits.** Define an explicit complete-set
   baseline/change contract in the implementation plan. Profile, rule, vehicle,
   listing and membership changes must be detectable, including cars untouched
   by the local editor. Return effective source identities/revisions. Do not
   weaken the existing expected-revision semantics of saved writes or previews.
6. **Finalize orders over the full set.** Apply the accepted complete/partial/
   rejected grouping, raw cost/lower-bound order, registration ties, eligible
   cheapest ties and strict preference-winner test globally. Pagination or
   collapsing a table does not change scores, membership or recommendations.
7. **Handle interruption and incomplete delivery explicitly.** Bound individual
   transfers and intermediate processing, propagate cancellation and validate
   UTF-8 payload sizes. Resource/transport errors must identify that a complete
   generation was not delivered; never truncate or publish a whole-set winner
   from the surviving groups. Valid partial *costs* remain distinct from a
   missing portion of the *candidate set*.
8. **Preserve manual and trust boundaries.** Explicit manual mode remains
   database-independent and supports more than 100 valid transient registered
   candidates through the planned transport. No inherited registry/source claims,
   implicit saves, AI, result history, stored preview sessions or persisted
   result caches are introduced. Existing null/zero/included states, current cost
   confirmation and unresolved review behavior remain authoritative.

The implementation plan must give #65 a concrete complete-set request/result
contract, revision/error behavior, bounded transport mechanism for large
unsaved/manual inputs and an all-sensitivity generation strategy. It must
document compatibility/version handling and regenerate intended frontend types.
It must not substitute a hard selection count for the accepted requirement.
No new product decision is needed merely to choose internal batch sizes.

## Verification and readiness evidence

| Boundary | Required regression |
| --- | --- |
| Complete membership | 0/1/100/101/250 saved vehicles, mixed listing/legacy/purchase/lease and incomplete/rejected facts; every UUID appears exactly once. |
| Group independence | Repeat with different internal group boundaries; identical scores, coverage, order and winner flags for the whole set. |
| Exact comparison | Costs and score bounds differing below presentation precision in different groups; equal exact costs, overlapping intervals, rejected cars and no-active-criteria cases. |
| Large data | Aggregate saved inputs over 2 MiB with a compact all-saved request; actual UTF-8 transfer boundaries, oversized unsaved entries, malformed/chunked input and cancellation. No implicit save or truncation. |
| Consistent reads | Separate PostgreSQL clients add/delete cars and change profile/rules/facts/costs/listings during reads. A generation is coherent or explicitly rejected; old calls cannot restore deleted data. |
| Sensitivity and failure | All three sensitivity views use one membership/source set. A dropped group/view or obsolete response cannot become a complete current comparison. |
| Evidence and review | All existing price-confirmation, source-version, legacy 50+50, independent-error and budget cases remain correct through complete-set composition. |
| Manual isolation | At least 101 transient registered candidates without PostgreSQL; no store/AI resolution, evidence promotion or hidden writes. |
| Compatibility | Existing comparison, household, legacy, URL, status and generated-contract tests remain intact. Browser/proxy contract tests exercise the new transport. |

Baseline: 938 backend, 195 frontend and 37 Chromium tests. Use .NET SDK 10.0.400,
Node 22.22.2 and disposable PostgreSQL 18. Run the
[ordinary backend/frontend/OpenAPI/Docker commands](../README.md#verification),
regenerating types from port 5090, Chromium with one worker, and URL acceptance.
Record passed/failed/skipped counts, warnings and reruns. No Unraid data or live AI.

The implementation branch will be `feature/85-all-vehicle-comparison` after
readiness and assignment. Its PR closes #85 only after every criterion is met
and CI passes; merge needs separate approval. Then audit #65 against the newly
published contract and updated test baseline. #85 does not implement the UI.

The [deferred cleanup inventory](comparison-workspace-preparation.md#deferred-cleanup-inventory)
remains untouched as requested. Preparation creates no helper scripts or test
stacks. Its own delivery checks cover documentation, links and backlog alignment.
