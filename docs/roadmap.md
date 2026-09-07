# Roadmap

## 0. Repository foundation — complete

- Preserve and tag the console prototype.
- Create the .NET 10, React/TypeScript, PostgreSQL, Docker, testing, CI, and documentation foundation.
- Provide health/status contracts and Swedish placeholder routes.

## 1. Manual calculator — complete

- Implement the new domain model and first database migration.
- Port valid cost-calculation ideas from the legacy prototype with explicit units and decimal money values.
- Support unsaved calculations and optionally saved scenarios.

## 2. URL analysis — complete

The specification, dependency-free listing domain, private Codex extraction
runtime, unsaved public preview API, and Swedish review interface are
implemented. Current-listing PostgreSQL persistence and its public saved-listing
HTTP lifecycle are also implemented, together with the Swedish saved-listing
workflow and calculator linkage. Fake-only complete-flow verification covers
the extraction boundary, PostgreSQL lifecycle, same-origin deployment, and
linked-calculation versioning without consuming ChatGPT usage.

- Define URL normalization, source matching, bounded listing facts, provenance, and missing-data contracts.
- Add dependency-free listing concepts and deterministic validation in Core.
- Add a bounded internal Codex extraction sidecar using ChatGPT authentication,
  host-restricted hosted web search, and no direct scraping or
  marketplace-specific parsing.
- Expose independent unsaved previews and current saved-listing APIs.
- Persist one current listing per vehicle with optimistic concurrency and no analysis history.
- Build Swedish analysis, manual-review, saved-listing, and calculator-prefill workflows.
- Verify the complete flow through a fake extractor, PostgreSQL, Compose, and
  browser tests without live Codex calls or ChatGPT usage.

## 3A. Household calculations — acceptance verified

This is a separate delivery stage before rules and comparison. The accepted
direction and normative specifications are in the
[Household calculations and comparison plan](household-comparison-plan.md).
The existing manual-calculator milestone remains complete for its version 1
scope. Shared inputs and purchase financing (#55), plus partial ownership costs,
energy, depreciation and sensitivity (#56), and leasing, calendar payments,
cash/cost reconciliation and budgets (#57), are implemented in Core.
Persistence, explicit legacy transition and the shared draft (#58) are implemented
in Infrastructure. Household HTTP contracts and generated types (#59) are
implemented, together with the [Swedish household workspace](household-workspace.md)
(#60). Practical stage acceptance (#61) has a
[verification report](household-stage-3a-verification-report.md). The report
identifies the tested branch; GitHub closure follows approved merge. See the
[Core cost contract](household-calculations.md#implemented-core-ownership-costs).

- Define one editable household profile for common use, available cash, loan
  assumptions, energy prices, and optional budget limits across all cars.
- Keep cash available for the purchase separate from startup expenses/reserves.
- Derive purchase financing from available cash and recalculate previews when
  shared assumptions change.
- Extend selected-period and monthly cost outputs with cost per mil, payment
  scheduling, explicit depreciation assumptions, and current-data sensitivity
  views for favorable, baseline, and cautious assumptions.
- Support all car fuel types equally, define electric/hybrid consumption and
  charging boundaries, and add an explicit leasing contract model.
- Separate planned service, known repairs, and an additional repair allowance
  using manual/evidenced amounts.
  Optional AI suggestions are later work and do not block this stage.
- Preserve registration-required identity, one current input set per car, no
  calculation history, complete vehicle deletion, and a bounded recoverable
  draft. Define compatibility when current calculation versions change.
- Keep mileage-triggered maintenance as separate later refinement. Implement
  resolved work only after documentation and dependency gates are complete.
- End the stage with automated verification and a practical acceptance check
  using representative cars and changes to the common assumptions.

## 3B. Comparison and configurable buying scores — planned

This stage depends on 3A and is refined from the existing rules/comparison
tracker #12. Stage labels 3A and 3B preserve later milestone numbering; the
[delivery backlog](household-comparison-backlog.md) defines the ordered issues.

- Build one dynamic workspace with a main selected-period total-cost table and
  detail tables for monthly/per-mil cost, financing, energy, service/repairs,
  cash flow, and sensitivity using the same household assumptions.
- Keep incomplete candidates visible and identify missing components and
  partial totals explicitly.
- Implement configurable hard rules and verification requirements, warnings,
  positive signals, and explainable scores that respond to changed priorities.
- Support the accepted price, mileage, owners, tow-bar, gearbox, seats, year,
  fuel, body, drive, location, towing-capacity, inspection, and service criteria.
- Use user-defined score anchors, weights 0-5, unknown score intervals and
  weighted coverage; retain rejected cars below other candidates.
- Store only current evaluations and inputs; provide a downloadable PDF of the
  current comparison and its assumptions.
- Refine registry-source access independently of automatic discovery and
  finish with automated verification and practical comparison acceptance.

## 4. Automatic discovery

- On hold pending permitted access and explicit resumption.
- Enable scheduled searches only after approved marketplace access is available.
- Track runs, deduplicate listings, detect changes, and evaluate new or updated candidates.

## 5. AI review

- Add advisory GPT-5.6 Luna review with Structured Outputs and deterministic fallback behavior, separate from milestone 2 extraction.
- Review manual/URL candidates and automatic-search finalists.
- Add separately requested, cited web research.
- Refine evidence-backed maintenance/repair suggestions with explicit user
  adoption after manual inputs; profile changes never invoke this assistance.

## 6. Image review and refinement

- Image review is on hold pending explicit resumption. Practical calculator
  and comparison acceptance is included in stages 3A/3B and does not depend on
  completing image review.
- Add bounded image selection and vision review for visible risks.
- Evaluate prompts and model results against a curated set of known listings.
- Add cost dashboards, prompt/version tracking, and quality metrics.
