# Comparison and buying scores specification

## Status and scope

Normative target for stage 3B. The #62 Core fact foundation is merged through
[PR #80](https://github.com/Extender92/CarExpenseCalculator/pull/80). Core rules,
scores and ordering (#63) are merged through PR #82. Issue #64 delivered
[fact/rule persistence and HTTP](comparison-api.md) through approved PR #83.
The [#65 preparation](comparison-workspace-preparation.md) records the UI
handoff and the comparison-size decision gate.
Comparison UI, PDF and stage acceptance remain #65–#67. This stage depends on
accepted [household calculations](household-calculations.md), including shared
assumptions, partial results, and current data. It evaluates manually entered
or explicitly reviewed registered candidates from all three product modes.
No provider selection, automatic discovery, or AI is needed to compare them.
See the [product plan](household-comparison-plan.md) and
[acceptance procedure](household-comparison-verification.md).
The [#63 preparation audit](buying-rules-implementation-preparation.md) records
completed dependencies and the existing Core integration boundaries; it adds
no new product behavior to this specification.

## Current facts and evidence

Keep each field's typed value, source kind/reference, observation time,
verification state, and confirmation time where applicable. Use the existing
listing provenance contract from [URL analysis](url-analysis.md), extended to
new fields rather than replacing its source boundary. User editing a sourced
value makes it user-confirmed, not registry-verified. Changing any value
invalidates verification of the previous value. Unverified extraction never
becomes a fact merely because a rule used it.

`minimumEvidence: advertised | userConfirmed | registryVerified` is configured
per hard rule and preference. A user-confirmed value satisfies advertised or
user-confirmed requirements; a registry-verified value satisfies all three.
Conflicting unresolved source values produce `needsVerification`, not an
automatic source selection. Registry verification can only be assigned by a
future permitted provider adapter, never by a manual checkbox or ordinary
client-supplied write. In 3B manual work, choosing registry evidence therefore
leaves unsupported criteria unverified. No arbitrary freshness expiry is
invented: display observation age; provider-specific validity rules belong to
the later provider contract. Time-dependent rules use explicit `asOfDate`.

### Accepted criterion catalogue

| Key | Fact, unit, and hard-rule operators | Preference type |
| --- | --- | --- |
| `purchasePriceSek` | Purchase asking/input price, SEK; inclusive min/max. Lease with no purchase price is not applicable. | Numeric |
| `odometerKilometres` | Current odometer km, displayed in mil; inclusive min/max. | Numeric |
| `ownerCount` | Integer count; inclusive min/max. | Numeric |
| `towBar` | Known true/false; equality. Unknown is distinct from false. | Categorical |
| `transmission` | Existing normalized gearbox fact, including manual/automatic; allowed set. | Categorical |
| `seats` | Integer 1-100; inclusive min/max. | Numeric |
| `modelYear` | Integer 1886-2100, retaining the existing listing bound; inclusive min/max. No year inferred from registration. | Numeric |
| `fuelTypes` | Existing normalized fuel set; intersects allowed set. | Categorical set match |
| `bodyType` | Existing normalized body fact; allowed set. | Categorical |
| `drivetrain` | Existing normalized drive fact; allowed set. | Categorical |
| `locality`, `county` | Independent sourced text; allowed set using trimmed case-insensitive ordinal matching. No geocoding or inferred county/distance. | Categorical |
| `towingCapacityKilograms` | Braked trailer capacity, integer 0-100,000 kg; inclusive min/max. Do not mix with unbraked capacity or claim driver/combination legality. | Numeric |
| `inspectionValidThrough` | Explicit ISO date; remaining whole days from asOfDate must meet inclusive minimum. Negative remaining days are expired. | Numeric remaining days |
| `serviceDocumentation` | `documented`, `partial`, or `absent`, with evidence and optional last service date/odometer; allowed set. Null is unknown. | Categorical |
| `netCostSek`, `costPerMonthSek`, `costPerMilSek` | Complete comparable derived 3A cost in active mode; inclusive limits. | Numeric |
| `startupBudget`, `monthlyBudget` | 3A budget status; require within limit when enabled. Unknown/disabled limit cannot pass an enabled rule. | Hard rule/warning only |

Retain existing validated bounds/normalization for established listing fields.
New location allowed-set entries use the existing field length limit. At most
one hard rule and one preference per key; at most 50 entries of each, at most
50 allowed/preferred values per categorical set. Empty enabled sets are
invalid. Notes have the 3A 1,000-character bound. Inspection/service data is
evidence about the car, not a mechanical diagnosis. No automatic service
interval or hidden-condition score is inferred.

Known not-applicable values, such as purchase price for a lease, cannot pass an
enabled hard requirement and yield `needsVerification` with `notApplicable`.
They also cannot earn a preference advantage through a smaller denominator.
The user may disable an unsuitable criterion for the whole comparison. Fuel
types are treated equally; no implicit fuel-specific bonus or penalty exists.

## Implemented Core facts (#62)

`Core.Comparisons` provides the following dependency-free contracts. These are
Core inputs/value objects, not new HTTP or storage DTOs. Household calculation/
result version 2 and storage version 1 remain unchanged.

| Contract | Behavior |
| --- | --- |
| `VehicleComparisonFacts` | Current typed source facts, with no duplicate identity or editable derived totals. A nullable field input normalizes to `Unknown`. |
| `VehicleFact<T>` / `FactObservation<T>` | Read-only state and copied current observation collection; each observation binds an immutable supported value to its evidence. `Known` requires exactly one observation; `Unknown`/`NotApplicable` require none; `Conflicting` requires at least two different normalized values. Known does not mean verified. |
| `ComparisonEvidence` | Existing origin, extraction method and verification enums; optional `ListingUrl`, `DateTimeOffset? ObservedAt`, and `DateTimeOffset? ConfirmedAt`. No invented timestamps or automatic freshness expiry. |
| `FuelTypeSet` | Copies its input; every existing fuel enum is supported. Explicit empty is known empty, not missing. Invalid enum members and duplicates are rejected. Equality for conflicts compares sets, not their ordering. |
| `ComparisonCriterionCatalog.All` | Read-only metadata for all 20 criteria: key, kind, unit, bounds and applicability. Fifteen are vehicle facts, three consume complete comparable 3A costs, two consume 3A budget status. It does not calculate scores. |
| `VehicleFactsProcessor.Normalize(input, acquisitionType)` | Normalizes all supplied fields and aggregates typed errors. Purchase is the default category. A lease with unknown purchase price becomes `NotApplicable`; an explicitly supplied price, including zero, is retained. |
| `VehicleFactsProcessor.FromReviewedListing(submittedUrl, returnedSources, input, acquisitionType)` | Validates supplied comparison values, reuses `ListingDraftProcessor.ProcessReviewed` source matching, and explicitly maps the supported fields. Import does not confirm advertised data. |
| `ReplaceWithManual(value, confirmedAt, observedAt?)` | Produces one user/manual/userConfirmed observation with the supplied times; discards old observations, source verification and old timestamps. |
| `ResolveWithManual(value, confirmedAt, observedAt?)` | Requires an existing conflict, then explicitly replaces it using the same manual operation. No source wins automatically and no historical observations are retained. |
| `SwedishMil.FromKilometres` / `ToKilometres` | Decimal division/multiplication by exactly 10 within the odometer domain. Reversibility is checked; an unrepresentable conversion raises `conversionNotExact` rather than rounding. |

Construction preserves supplied input errors for validation; it is not evidence
authentication. Call `Normalize` before accepting a fact set. For an edit to
an existing value, use the explicit replacement/resolution operations rather
than pairing the new value with old evidence. Future application adapters must
enforce that editing boundary when comparing current and submitted data.
Public normalization accepts only listing/ai/unverified (with a source URL) and
user/manual/userConfirmed (URL optional). Registry origin, registry verification,
and unsupported combinations are rejected. Unverified evidence cannot carry a
confirmation time. Legacy manually confirmed listing values may have null
confirmation times because that contract never recorded them. New explicit
manual operations require the caller's confirmation time and never read a clock.

All current source fields permit missing, explicitly not-applicable and
conflicting input states; none is a measured zero or automatically passes a
rule. The price/odometer limits are 0-100,000,000 SEK and 0-10,000,000 km;
owner count is 0-10,000, seats 1-100, model year 1886-2100 and braked towing
capacity 0-100,000 kg. All numeric endpoints are inclusive. Locality/county
trim and normalize Unicode Form C with the existing 100-character limit.
Conflicting location values are compared ordinally ignoring case after that
normalization. Unsupported enum values never become the first enum member.

Service documentation uses `ServiceDocumentationStatus.Documented`, `Partial`,
or `Absent`. Supporting `lastServiceDate`, `lastServiceOdometerKilometres` and
`serviceNotes` are independent facts with their own evidence; they never infer
documentation completeness. Service odometer uses the same decimal km bounds;
notes trim/normalize with a 1,000-character bound. Inspection and service dates
accept the full `DateOnly` range, including past dates. Expiry and remaining days
belong to the #63 evaluator with explicit `asOfDate`.

The mapping retains price, odometer, owners, tow bar, transmission, model year,
fuels, body, drivetrain, locality and county. It never maps last/next inspection
to explicit inspection validity, free-text equipment to seats/towing/tow bar,
condition claims to service facts, locality to county, or first registration to
model year. Publication/update dates are not observation timestamps. New typed
facts require explicit manual input until a supported source is implemented.
Invalid supplied comparison values are rejected before the existing listing
processor's best-effort treatment of AI values; unmatched AI sources still
become unknown through the established source boundary.

`VehicleFactsValidationException.Errors` is a copied, read-only list of
`Path`, `Code`, and English technical `Message`. Paths identify the field,
observation, value/collection index, or evidence property, for example
`seats.observations[0].value` and
`fuelTypes.observations[0].value.values[1]`. Stable codes are `required`,
`outOfRange`, `invalidEnum`, `tooLong`, `invalidText`, `duplicateValue`,
`invalidState`, `invalidConflict`, `unsupportedEvidence`, `invalidEvidence`,
`invalidListing`, and `conversionNotExact`. Listing-boundary errors retain their
existing listing paths with `invalidListing`. Invalid supplied input throws;
it is not silently relabeled as missing. Ordinary null top-level arguments use
`ArgumentNullException`. The [#64 HTTP boundary](comparison-api.md#errors-size-and-cancellation)
maps these errors without exposing storage details.

```csharp
var processor = new VehicleFactsProcessor();
var facts = processor.Normalize(new VehicleComparisonFacts());
var edited = facts with
{
    Seats = facts.Seats!.ReplaceWithManual(5, confirmedAt), // caller-supplied time
};
var accepted = processor.Normalize(edited);
var displayedMil = SwedishMil.FromKilometres(200_000m); // exactly 20_000
```

## Implemented Core evaluation (#63)

The #63 implementation adds `ComparisonEvaluator.EvaluateComparison(profile,
rules, asOfDate, candidates)` in `Core.Comparisons`. It is a pure calculation:
no writes, providers, clock, UI feature enablement or retained evaluations.
Rule and comparison-result versions start at **1**, independently of household
calculation/result version **2** and storage version **1**. Existing household
HTTP responses remain unchanged. #64 extends OpenAPI with the comparison contract.

| Contract | Implemented semantics |
| --- | --- |
| `RuleProfileInput` | Copied hard rules, preferences and selected signals. Empty means no active defaults. `RuleProfileProcessor.Normalize` validates and normalizes before evaluation. |
| `HardRuleInput` | Stable criterion key, typed operator, evidence requirement, inclusive limits or typed `ComparisonChoice` set, explicit enabled state. |
| `PreferenceInput` | Stable key, weight 0-5, required evidence, numeric zero/full anchors or typed preferred choices. |
| `ComparisonCandidateInput` | Existing UUID/`RegistrationNumber`, current source facts, optional current cost inputs, confirmation, source revisions, review items and reviewed condition-note facts. Collections are copied. |
| `CostAssumptionConfirmation.Confirm(input, confirmedAt)` | Explicit adoption of the actual immutable car assumptions and caller-supplied time. Structural value comparison preserves confirmation across equivalent reads, but changes to amounts, sensitivity values, inclusion, energy, payment timing, notes or lease terms invalidate it. |
| `ComparisonReviewItem` | Stable source-review key/reason and typed affected sections supplied by a trusted application adapter. This is not a client-authoritative flag or a second storage model. |
| `CurrentEvaluation` | Normalized effective input, current household cost sections, independent errors, hard results/eligibility, per-preference assessment and weighted interval contribution, score/coverage, selected signals and recommendation flags. |
| `ComparisonPreview` | Effective shared profile, normalized rules, explicit date, versions/profile errors, results in input order, separate UUID cost/score order lists and a preference-recommendation reason. Source revisions are echoed, not checked against a database. |

`HardRuleOperator` supports inclusive ranges, boolean equality, allowed sets,
fuel intersection, minimum remaining inspection days, and within-budget rules.
Only the operator appropriate to the catalogue entry is accepted. Budget
preferences are invalid. Numeric anchors may increase or decrease; whole-number
hard limits are required for integer fields and remaining days. Date anchors use
remaining days within the full `DateOnly` difference range. Numeric cost anchors
have the decimal domain, without introducing a vehicle-price cap on totals.
The unchanged household validator prevents residuals above purchase price and
negative expense inputs, so it cannot currently produce negative complete cost.
Negative anchors remain valid; invalid attempted negative costs remain unavailable.

At most 100 candidates, 50 hard rules/preferences per collection and 50 choices
per set are accepted. Duplicate UUIDs, normalized registrations and criterion
keys are invalid. Supplied disabled-rule values are still validated, while
missing targets are allowed for disabled entries. Signals have one entry per
supported key; inspection signals require an explicit nonnegative whole-day
threshold. Reviewed condition notes retain the listing limit of 10 notes,
300 characters each; their source/evidence is validated through the fact path.

### Effective purchase price and cost authority

With current purchase cost inputs, price rules and preferences use their
`PriceSek`, including an explicit missing price. They do not fill missing price
from source facts. Without cost inputs they use the current price fact; lease
applicability follows #62. If advertised facts say 40,000 and current purchase
inputs say 35,000, the assessed price is **35,000**. Evidence for 40,000 cannot
confirm it. A known source fact for exactly 35,000 can supply its own evidence;
otherwise explicit adoption of the current cost inputs supplies user evidence.
An unconfirmed changed price returns `effectivePriceNeedsConfirmation` and an
unknown preference interval, while displaying the effective numeric value.
`EffectiveInput.Facts` preserves the current source facts; consumers use
`Assessment.Actual` for the evaluated price rather than substituting the source
price. Confirmation never changes advertised source facts or creates history.

The household engine now has one internal calculation path returning both its
existing presentation and unrounded net/monthly/per-mil measures. Public
`HouseholdCostCalculator.Calculate` projects the same existing version-2 result.
Comparison consumes complete unrounded measures and authoritative budget
statuses. It never accepts a submitted cost total or rebuilds a budget from
rounded funding. A confirmed car-input snapshot remains applicable when the
shared profile changes; the resulting costs remain estimates of those explicit
household assumptions. Estimated totals cannot satisfy registry requirements.

Unresolved review removes affected complete totals and score inputs, preserving
known subtotals and independent sections. Cost-category review also propagates
to ownership/monthly/per-mil completeness. Payment and budget impacts remain
explicit. Full reconciliation is blocked where its cost/payment components are
unresolved. A reviewed budget pass becomes unknown; a safe known exceedance,
invalid budget or missing limit retains its state. Even an independent invalid
expense cannot erase a safe exceedance already established by the cost engine.
The #64 adapter must derive review impact and confirmations from typed/current
underlying data; arbitrary client affected-section lists or confirmation flags
must never be mapped as trusted evidence.

### Independent validation, precision and explanations

Strict public fact normalization still throws aggregated errors for invalid
supplied values. An internal evaluation path preserves normalized fields and
their individual errors so invalid seats, for example, cannot erase a known
failed tow-bar rule. Errors retain stable codes and paths under
`candidates[i].facts`, `candidates[i].costInput`, or `candidates[i].scores`;
shared calculation errors retain `profile.*` paths. Existing structural
household exceptions remain structural exceptions. Invalid rules/candidate
structure throw `ComparisonInputValidationException`, with immutable
`Path`/`Code`/technical English `Message` errors. Null top-level arguments use
`ArgumentNullException`. HTTP mapping belongs to #64.

Assessments expose actual values, available/required evidence, reasons and
errors. Representative reasons are `unknownFact`, `notApplicable`,
`conflictingFacts`, `invalidFact`, `insufficientEvidence`,
`effectivePriceNeedsConfirmation`, `estimatedValueNotRegistryVerifiable`,
`incompleteCost`, `budgetNotConfigured`, `unknownBudget`, `invalidBudget`,
`zeroDistance`, and `legacyReview:<key>`. Existing calculation errors are
preserved. Structure/rule codes include `required`, `tooManyItems`,
`duplicateKey`, `identityMismatch`, `unsupportedCriterion`, `invalidOperator`,
`invalidEnum`, `invalidLimits`, `invalidInteger`, `invalidAnchors`,
`unsupportedPreference`, `invalidChoice`, `duplicateValue`, `invalidState`,
`invalidText`, and `outOfRange`.

Scores use decimal arithmetic with early endpoint clamping. Multiplication
before division preserves representable tiny scores; division first is used
when multiplying the numerator would overflow. An unrepresentable intermediate
returns `calculationOutOfRange` only for that contribution, keeping its interval
and other contributions. Raw scores/weighted totals determine order and winners;
only returned score/percentage presentation rounds to two places away from zero.
Raw equal costs share the cheapest label; display-equal costs are not necessarily
ties. Registration tie order is ordinal over normalized registration strings.

Hard results contain Swedish explanations and an independent
`ObservedConditionSatisfied` value so an insufficiently evidenced seller claim
does not masquerade as a trusted pass/fail. Selected condition notes are
source-labelled informational text, without keyword diagnosis. Service and
inspection signals distinguish explicit positive/unclear/expired/short facts;
equality with the inspection threshold meets it. Cost/budget signals report
current calculation completeness/status. They never modify rules or scores.
`PreferenceRecommendationReason` distinguishes `definiteWinner`,
`overlapOrTie`, `noEligibleCandidate`, and `noActiveCriteria`.

## Hard rules and explanations

`RuleProfileInput` contains enabled hard rules, preferences, and selected
warning/positive-signal keys. It has one current saved household-wide instance,
its own revision, and explicit **Spara**. Edits preview without saving. The
historical example (tow bar, 5,000-20,000 SEK, max 20,000 mil, max six owners)
is an optional example only, not an automatically active default.

Each hard evaluation returns `pass | fail | needsVerification`, actual value,
operator/limits, evidence used and required, stable reason codes, and Swedish
template explanation. A trusted known failure means `fail`. Missing,
insufficiently evidenced, conflicting, or not-applicable facts mean
`needsVerification`; where a seller claims a failure, show that claim alongside
the unverified state. Aggregate state is `rejected` if any rule fails,
`needsVerification` if none fail but any is unresolved, otherwise `eligible`.
An empty hard-rule list is eligible. A high score cannot alter these states.

Warnings and positive signals explain observed facts separately. Provide
deterministic templates for disclosed condition/repair notes, unclear service
documentation, expired/short inspection validity (threshold explicitly set by
the user), and cost/budget incompleteness. Existing listing claims can be shown
with source labels; do not automatically interpret free text as verified
criteria. Signals do not affect scores or rejection unless the user explicitly
configures a supported corresponding preference or hard rule.

Derived cost criteria use complete backend calculation results, never a client
total. They represent estimates from explicit inputs, not registry facts.
`userConfirmed` requires explicit adoption of the underlying car assumptions;
unsaved household edits remain labeled assumptions. Registry evidence is not
applicable to an estimated total. Incomplete totals are unknown for scoring
even when a known subtotal is available. No inference of cheapness from missing
tax, insurance, maintenance, residual, or unmatched lease coverage.

## Scores, coverage, and ordering

Each numeric preference declares `zeroPoint` and `fullPoint` in the fact's
unit, with unequal finite values within its supported domain. Direction follows
the anchors; no dataset-dependent normalization:

```text
score(x) = clamp(100 * (x - zeroPoint) / (fullPoint - zeroPoint), 0, 100)
```

For categorical values, an explicitly preferred value scores 100 and a known
nonpreferred value scores 0. Fuel sets score 100 if they intersect the user's
preferred set. Each weight is integer 0-5; zero removes the criterion from
contributions, coverage, and required inputs. No active preferences yields
`score: null` and `coverage: null` with `noActiveCriteria`, not an invented zero.

With `W` the sum of all positive weights, known adequately evidenced scores
`s_i`, and unknown/insufficient/not-applicable scores allowed in [0,100]:

```text
lower = sum(known weight_i * s_i) / W
upper = (sum(known weight_i * s_i) + 100 * sum(unknown weight_i)) / W
coveragePercent = 100 * sum(known weight_i) / W
```

The denominator includes every active criterion for every car. Unknown values
do not become observed zero scores; their minimum contribution is only the
lower bound. Expose each criterion's interval and missing reason, weighted
contribution, and total weighted coverage. Calculate in decimal with full
precision; display scores and percentages to 2 places, midpoint away from
zero. Sorting uses unrounded values and stable registration order for ties.
Adding/removing another candidate never changes an existing contribution.

Cost sorting groups non-rejected complete comparable estimates first, ascending
net selected-period cost, then non-rejected incomplete candidates ordered by
registration, then rejected candidates (complete cost first, then registration
for partials). Show `needsVerification` explicitly even when cost is complete.
The cheapest eligible complete candidate(s) can receive **Billigast bland
kompletta, godkända alternativ**. An unverified hard requirement cannot win that
recommendation. Missing candidates remain visible so the label never claims
to know the globally cheapest car. Equal full-precision costs share the label.

Score sorting keeps non-rejected candidates before rejected ones, orders by
descending lower bound, then registration. No-active-score candidates follow
scored candidates in their group. A definite preference winner requires
eligible hard state and its lower bound strictly above every other
non-rejected candidate's upper bound; otherwise show overlap/uncertainty or
ties, not a certain ranking. Recommendation copy also exposes cost completeness
and never calls a score winner the cheapest car. A rejected car remains
**Bortvald** with reasons regardless of score.

## Workspace and reports

The Swedish comparison workspace has one household-profile editor, one rule
editor, and one active sensitivity mode. It shows a main total table and detail
tables for monthly/mil costs, financing, energy, tax, insurance, service, known
repairs/allowance, calendar payments/budgets, and all three sensitivity views.
Each row/column uses registration identity. Missing parts are named with links
to the relevant editor; distinguish included, known zero, not applicable,
invalid, and unavailable. Let users sort by cost or preferences explicitly.

Keep the `/manual`, `/analyze-urls`, and `/search` modes. `/search` becomes the
manual/reviewed-candidate rule/comparison workspace; it must not imply automatic
discovery. Saved candidate selection is local current data. Dashboard/status
copy and feature flags change only when corresponding behavior is delivered
and tested. Stage 3A already supplies profile/vehicle editing; do not duplicate
household overrides in this workspace.

All tables publish one coherent preview generation. Edits debounce 500 ms and
cancel obsolete requests; multi-batch requests use one captured input set and
publish together. Report generation captures the displayed snapshot, including
unsaved edits, so edits during generation do not mix rows or profile values.
If displayed data is obsolete or invalid, require recalculation before export;
valid partial comparisons remain exportable with explicit gaps.

PDF includes registrations/labels, generation time, start/horizon, annual km,
cash and common terms/prices/budgets, effective car assumptions, mode, all three
sensitivity totals where available, main and detail cost tables, cost-versus-
cash definitions, criterion anchors/weights, hard outcomes, score ranges and
coverage, missing-data reasons, provenance, and calculation/rule versions.
Mark unsaved profile, rules, and car edits prominently. Use the same rounded
values/labels as the UI, repeated table headings, Swedish text and characters,
and page breaks without clipped columns. Provide a printable report and PDF
download through browser print-to-PDF; the UI explains the browser's save-PDF
step. No server PDF renderer, third-party export service, or persisted report
history is required. Downloaded copies remain outside application deletion.

## Future contracts and persistence

Issue #64 implements the storage and HTTP portions below; the heading is retained
for existing issue links. [Comparison storage and HTTP](comparison-api.md) specifies
the API-owned DTOs, typed fact actions, explicit cost confirmation lifecycle,
source listing versions, two preview modes, transaction/revision checks, error
mapping and migration rollback. `ComparisonReportInput` remains #66 work.

`EvaluateComparison(profile, rules, asOfDate, candidates)` calls 3A calculation
and deterministic rules with full current inputs. Client-submitted calculated
totals, verified flags, and evaluation snapshots are never authoritative.

| Contract/route | Required semantics |
| --- | --- |
| `RuleProfileInput` | Hard operators/limits/evidence, numeric anchors or preferred sets, weights, and selected signal thresholds. |
| `CurrentEvaluation` | Registration/UUID, source revisions, rule/calculation versions, hard state and explanations, per-criterion contributions, `ScoreRange`, missing reasons, and current cost sections. |
| `ComparisonReportInput` | Captured normalized effective inputs and result generation, dirty flags, explicit asOfDate, and derived results from the current trusted preview. |
| `GET/PUT /api/rule-profile` | Read or explicit full save; 404 before create, expectedRevision=0 for create and current revision thereafter. |
| `GET/PUT /api/vehicle-facts/{vehicleId}` | Current typed facts with provenance; aggregate revision checks; attach facts to an existing registered vehicle only. |
| `POST /api/comparisons/preview` | Supplied profile, rule input, explicit asOfDate, registered candidates (max 100), requestId; no writes or AI calls. |

Use 3A JSON/error/body limits. Invalid rule structure/anchors/weights returns
400 with paths; missing candidate facts return partial per-candidate evaluation.
Invalid candidate numeric values invalidate dependent criteria while other cars
remain evaluable. Unknown vehicle reads are 404, conflicts are 409, storage
unavailability is 503. Manual writes cannot assert registry verification.
Generate frontend types from OpenAPI, never hand-edit the schema file.

One current rule profile and one current fact set per vehicle are persisted.
Evaluations are recomputed from current inputs without a cache or history table.
Stored preview checks profile, rule, vehicle and listing revisions in one
RepeatableRead snapshot and rejects changed baselines. Manual preview is an
explicit independent mode, never an automatic fallback after storage failure.
Ordinary vehicle changes retain saved drafts; their older base revision prevents
adoption. Full vehicle deletion clears facts, confirmations and matching draft
contents while preserving the shared household and rule profiles.

## Worked examples

| ID | Inputs | Required output |
| --- | --- | --- |
| B1 | Price anchors zero=100,000/full=20,000; price 40,000, weight 3. Preferred automatic gearbox true, weight 2. | Price score 75; gearbox 100; weighted score 85 and coverage 100%. |
| B2 | B1 with unknown or insufficiently evidenced gearbox | Score interval [45,85]; weighted coverage 60%; gearbox contribution is unknown, not an observed zero. |
| B3 | B1, gearbox weight 0 | Score 75, coverage 100%; gearbox no longer required. All weights 0 gives no score. |
| B4 | Price score 75 and known nonpreferred gearbox 0; weights change from 3:2 to 1:4 | Score changes from 45 to 15, facts unchanged. |
| B5 | Candidate X [45,85], Y [60,80] | Y sorts above X by lower bound; overlapping ranges forbid a definite preference winner. |
| B6 | Eligible A cost 30,000; eligible B 25,000; partial C known 10,000; rejected D complete 20,000 | Cost order B,A,C,D; B wins complete eligible cost. C retains gaps, D retains rejection reasons. |
| B7 | Price 20,000 against max 20,000; mileage 200,000 km against max 20,000 mil | Both inclusive limits pass with adequate evidence. Unknown owners needs verification. Confirmed no tow bar fails a required tow-bar rule. |
| B8 | Add a new candidate with price 1,000 or remove the most expensive candidate | B1's scores and contribution values remain 85/75/100. |

Use the [verification plan](household-comparison-verification.md) for Core,
persistence, API, UI, PDF, and practical end-of-stage acceptance.
