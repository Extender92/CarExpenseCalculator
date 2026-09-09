# Product requirements

## Goal

Help a household find and compare suitable used cars by combining explicit requirements, verified vehicle facts, ownership-cost calculations, and explainable review results.

The application must remain useful without external AI. Deterministic normalization, rules, and calculations are the source of truth. Hosted AI may help extract a user-selected listing or provide a later advisory review, but it never makes a fact authoritative.

## Audience and environment

- A small household using the application on a trusted home network.
- Hosted on an Unraid server and reached through `http://extower.local:<port>`.
- Swedish user interface with English code and technical documentation.
- No user accounts, authentication, public internet exposure, or HTTPS in the local-only release.

## Usage modes

### Comparison and buying rules

The user edits common hard requirements and preferences and compares all saved
or explicitly entered registered candidates. The implemented comparison keeps
rejected and incomplete cars visible, with explainable scores and evidence gaps.
Automatic collection from a permitted listing source is separate paused work.

### URL analysis

The user pastes one through ten public listing URLs. The application analyzes
each URL independently through a source-aware, ChatGPT-authenticated Codex
integration, clearly marks missing and unverified values, permits manual
correction, and can store one current reviewed listing per vehicle. Extraction
failure leaves manual entry available.

Rule evaluation and side-by-side comparison apply to these saved candidates
through completed stage 3B, following household calculations in stage 3A.
They remain separate from URL ingestion.

### Manual calculation

The user enters vehicle, operating, financing, and usage values directly. Saving is optional; calculations must also work without persistence.

## Shared behavior

- Show the source and verification state of important facts.
- Distinguish hard-rule failures, warnings, positive signals, and missing data.
- Never invent registration numbers, ownership counts, prices, mileage, or vehicle history.
- Permit side-by-side comparison of saved or explicitly entered candidates.
- Keep manual calculation and manually entered listing data available if AI or another external service is unavailable.

## Current delivery status

The repository foundation, manual-calculator milestone, and URL-analysis
milestone are implemented. The application provides three-mode navigation,
health and status endpoints, deterministic calculations and normalization,
PostgreSQL-backed current scenarios and listings, private source-aware
extraction, Docker deployment, tests, CI, and documentation.

The complete URL flow is covered by fake-only automated acceptance from
independent extraction outcomes through manual review, saved-listing lifecycle,
and listing-linked calculation versioning. Stage 3B facts, the deterministic
rule/score engine and comparison persistence/API (#62–#64) are merged through
PRs #80, #82 and #83. The all-car backend #85 / PR #86 and Swedish comparison
interface #65 / PR #87 are also merged. The [PDF report](comparison-pdf.md)
(#66 / PR #89) is merged, including feature-level PDF/native-print verification.
Whole-stage acceptance (#67) is delivered through approved PR #91. The
[3B verification report](comparison-stage-3b-verification-report.md#approved-merge-and-stage-closure)
records the merge, green main CI and completed practical session. All seven 3B
implementation issues, tracker #12 and the milestone are closed.
Automatic discovery, advisory AI review and image review remain future work;
the [planning checkpoint](roadmap.md#current-planning-checkpoint) identifies
their unchanged refinement and pause gates.

Stage 3A Core supports shared household assumptions, purchase financing and
partial ownership costs, including energy, compound depreciation, separate
service/repairs/allowance and explicit sensitivity modes. Core also supports
leasing, calendar payments, cash/cost reconciliation and separate startup and
average-month budgets. Shared input persistence and household HTTP contracts are
implemented. The Swedish household interface (#60) owns `/manual`; the v1
editor remains available at `/manual/legacy` for unconverted data. Whole-stage
acceptance (#61) was merged through PR #78 with green CI. The
[verification report](household-stage-3a-verification-report.md) records the
tested implementation, results and limits. Stage 3A is complete.

## Delivered household calculation and comparison direction

The work was split into household calculations (3A, complete), then comparison
and configurable buying scores (3B, complete). The objective is to compare
cars under the same household assumptions and determine estimated total cost
after the chosen months or years, with a prominent monthly equivalent.

- All supported car fuel types have equal priority, including combustion,
  electric, and hybrid vehicles. Purchase and leasing are implemented;
  keeping an already owned car is not a planned comparison mode.
- One editable household profile owns common driving, cash, financing, energy,
  and budget assumptions. Purchase cash and the startup budget are separate.
  Profile changes update calculated tables dynamically; persistence requires
  explicit **Spara**. Monthly budget checks use average funding, not peak months.
- Each car retains its own current facts and costs. There are no independently
  named saved scenarios per car and no superseded calculation history.
- The comparison workspace compares all current saved cars without a fixed
  maximum vehicle count. Internal batches/transfers do not restrict membership;
  #85 delivered the complete-set backend before #65. The UI contains a main
  total-cost table and multiple breakdown tables. Incomplete candidates stay
  visible with explicit missing calculations and partial totals.
- User-defined favorable, baseline, and cautious sensitivity views operate on
  current data. They do not create historical or separately saved scenarios.
- Buying priorities and weights are editable and produce current explainable
  scores without overriding hard rules or verification boundaries.
- Saved vehicles still require registration numbers. Deletion removes all
  vehicle-owned data while preserving shared profiles. One registration-linked
  recoverable draft is shared across devices and explicitly saved/replaced.
- A downloadable PDF presents the current comparison and its assumptions.
- Maintenance/repair amounts start with manual/evidenced inputs; optional AI
  suggestions are later work. Each stage includes practical acceptance with
  representative cars alongside its automated tests.

The accepted criteria, fixed-target weighted scores, unknown-score intervals,
and complete-cost ordering are recorded in the normative
[Household calculations](household-calculations.md) and
[Comparison and buying scores](comparison-and-buying-scores.md) specifications.
The delivery stages and separate later refinement work are recorded in the
[Household calculations and comparison plan](household-comparison-plan.md).
The linked acceptance reports identify the delivered 3A/3B behavior. Explicitly
later registry, AI and mileage-based service work remains unimplemented.
