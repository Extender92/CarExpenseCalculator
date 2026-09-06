# Household calculations and comparison plan

## Status and authority

These decisions were agreed on 2026-09-06. Stages **3A, Household calculations**,
and **3B, Comparison and buying scores**, are specified but not implemented.
The implemented contracts remain in [Manual calculator](manual-calculator.md)
and [URL analysis](url-analysis.md).

The normative target contracts, formulas, failure behavior, and worked examples
are in [Household calculations](household-calculations.md) and
[Comparison and buying scores](comparison-and-buying-scores.md). The
[verification plan](household-comparison-verification.md) defines regressions
and practical acceptance; the [delivery backlog](household-comparison-backlog.md)
maps the work to GitHub. This planning delivery changes documentation and the
backlog only, without enabling features or introducing code or migrations.

## Objective and accepted decisions

Compare buying or leasing alternative cars under the same household assumptions
to find the estimated cheapest suitable alternative after chosen months or
years. Monthly cost is a prominent equivalent of the selected-period cost.
All supported fuel types have equal priority. Keeping an already owned car is
outside this scope.

| Area | Accepted target behavior |
| --- | --- |
| Shared profile | One profile owns distance, horizon, purchase cash, common loan terms, energy prices and charging assumptions, and budget limits. |
| Editing | Changes dynamically preview all candidates. Only **Spara** replaces the saved profile; obsolete responses cannot replace current results. |
| Financing | Each alternative independently uses `min(price, cash)` and borrows `max(price - cash, 0)`. Startup expenses have a separate budget. |
| Main measure | Estimated selected-period net ownership cost, with monthly and per-mil equivalents. Principal, depreciation, payments, and reserve transfers are not double counted. |
| Depreciation | Annual percentage compounds on remaining value: `P * (1-r)^(M/12)`. A fixed SEK residual is valid only for its entered horizon. |
| Sensitivity | Favorable, baseline, and cautious use three explicit values for uncertain inputs. Other inputs remain unchanged; one active mode applies to all cars. No alternative saved scenarios or history. |
| Service and repairs | Manual/evidenced planned service, known repairs, and an additional uncertain repair allowance. The allowance contributes to estimated cost and separate monthly saving, not workshop bills. |
| Payments | Explicit start month, due months, and one-time events. Loan fees stop with the loan. Ongoing-budget warnings use monthly average funding including repair saving and excluding startup expenses. |
| Leasing | Own contract model. Fully comparable only when horizon equals term; otherwise retain known payments and a mismatch reason. No guessed renewal or termination. |
| Energy | Separate whole-distance and mode-specific consumption, driving shares, charging-price mix, and charging losses. |
| Tables | Main total table plus monthly/per-mil, financing, energy, tax, insurance, service, repairs, payments, and sensitivity tables. Missing components stay visible. |
| Cost order | Complete comparable alternatives first, cheapest first; incomplete alternatives below. Hard failures remain visible as **Bortvald**, below other candidates. |
| Buying requirements | Price, mileage, owner count, tow bar, transmission, seats, model year, fuel, body type, drivetrain, locality/county, towing capacity, inspection, and service information. |
| Scores | User goals define 0-100 numeric scales and categorical preferences. Weights 0-5; zero disables. Changing priorities recalculates contributions. |
| Unknown scores | Possible score interval and weighted coverage; sort by lower bound. Overlapping intervals are not a certain ranking. |
| Evidence | Advertising, user confirmation, and registry verification stay separate. Hard and verification requirements cannot be outweighed. |
| Retention | Registration required for saved vehicles and drafts. One current vehicle input set, no historical results. Whole-vehicle deletion preserves shared profiles. |
| Recovery | One shared registration-linked draft across devices, explicit **Spara utkast**, revision checks, and explicit replacement choice. Opening does not consume it; successful adoption into current vehicle data does. |
| Export | Current comparison PDF, including assumptions, period, sensitivity, weights, missing data, and marked unsaved edits. No retained export history. |

## Stage 3A: household calculations

Deliver the deterministic model before comparison/scoring. Car-specific facts
remain separate from shared assumptions: purchase/lease offer, consumption,
tax, insurance, service, repair work, and residual assumptions belong to each
car. Household fields cannot receive independent per-car overrides.

Purchase cash is reused for each alternative because the household is comparing
which one car to acquire. It is not distributed across simultaneous purchases.
Zero principal means zero interest, payments, and loan fees. A calculation does
not establish eligibility for a lender's offer.

The payment schedule distinguishes cost accrual, external payments, refundable
deposits, and internal repair saving. Known repairs are entered once. The
allowance represents additional uncertain work. Missing values remain missing;
explicit zero and lease-included costs are distinguishable from unknown amounts.

The stage includes partial results, current-data persistence, explicit profile
transition, the shared draft, HTTP contracts, generated frontend types, and
the Swedish profile/calculation flow. The transient 1-10 URL workspace continues;
only one registered draft can be explicitly persisted for recovery. Profile
edits and table refreshes never invoke AI or adopt unreviewed listing facts.

Before replacing old per-vehicle household assumptions, the user fills in a
new shared profile and confirms the transition. Preserve vehicle-specific
inputs and listing review boundaries. Ambiguous combined maintenance, energy
basis, and undated legacy costs require review, never guessed classification.
Unsupported derived-result versions cannot hide recoverable current inputs.
Revisions and schema versions support compatibility, not archives.

Keep SEK, decimal calculations, exact `1 mil = 10 km`, and the 1-120 month range.
Mileage-triggered service, registry integration, and AI maintenance assistance
are separate later tasks and do not block manual calculations.

## Stage 3B: comparison and buying scores

Use completed 3A contracts in one comparison workspace. Cost sorting and
preference sorting have distinct controls. Every candidate retains registration
and current missing-data, verification, and hard-rule states. An incomplete
estimate cannot win the complete-cost comparison; a known hard failure cannot
be recommended. Unverified hard requirements remain unresolved.

The full criteria catalogue above is accepted, including fuel, body, drive,
location, towing, inspection, and service criteria. Acceptance does not imply
that all fields exist in today's extraction schema or have a selected provider.
Manual entry remains available. Do not infer seats, towing capacity, service
facts, or travel distance from place names.

Scores use fixed user targets, so adding another candidate cannot change an
existing score. Explanations show actual values, source, required evidence,
contributions, weights, and missing reasons. PDF generation captures the same
consistent inputs as the tables. Downloaded copies are outside application
deletion; the application keeps no report archive.

## Later refinement boundaries

The 3A/3B product choices above are resolved. Separate later work still requires:

- Registry provider: permitted access, household eligibility, field coverage,
  freshness, cost, storage rights, and registration matching. See
  [Data sources](data-sources.md). This is independent of paused discovery #13.
- AI maintenance assistance: evidence/proposal schema, bounded invocation,
  runtime, adoption flow, cost limits, and evaluations under
  [AI design](ai-design.md) and tracker #14. Manual estimates come first.
- Mileage-triggered service: interval evidence, current odometer, event
  generation, and interaction with existing time-based service entries.

Automatic discovery #13 and image review #15 remain on hold. Closed milestones
remain closed. No live extraction or AI use is authorized by this document.

## Delivery and readiness

Publish these documents in a PR on `docs/household-comparison-plan`, create the
3A milestone/tracker, rename milestone 3 to 3B, and update #12. Each implementation
issue links published specifications and states scope, contracts, dependencies,
acceptance criteria, and verification commands.

Decision-complete items remain `status:blocked` while documentation awaits merge
or prerequisite work is unfinished. Unresolved later work stays
`status:needs-refinement`. Trackers are overviews, never implementation assignments.
After separate merge approval and a dependency audit, the first unblocked
implementation issue may become `status:ready`.

This planning delivery is complete when documents are published in the PR, the
backlog agrees with them, links/formulas/dependencies are checked, and ordinary
CI passes. Product implementation follows separately assigned issues. Each
stage ends with automated verification and the practical acceptance session;
it does not require buying test cars or waiting through years of ownership.
