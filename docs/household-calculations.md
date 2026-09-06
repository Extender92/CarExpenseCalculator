# Household calculations specification

## Status and scope

Normative target for stage 3A, agreed 2026-09-06; **partially implemented in Core**. The
[product plan](household-comparison-plan.md) sets scope. This document adds
future contracts without changing the implemented [v1 API](manual-calculator.md).
Core owns pure decimal calculation; API maps HTTP; Infrastructure owns current
PostgreSQL data; React owns Swedish forms and request cancellation.

## Implemented Core financing foundation

Issue #55 implements `Households.HouseholdProfileInput`, `HouseholdLoanTerms`,
`VehiclePurchaseInput`, `SensitivityValue`, `HouseholdInputValidator`, and
`HouseholdFinancingCalculator`. The remaining sections describe the complete
stage target; operating costs, residual, leasing, persistence, HTTP and UI are
delivered by subsequent work items.

The current pure operation is
`Calculate(HouseholdProfileInput, IReadOnlyList<VehiclePurchaseInput>)`.
The profile contains common use, financing, energy-price, charging and budget
inputs; a purchase contains only its candidate key and price, with no household
overrides. Energy/usage/budget inputs are validated but not calculated by #55.
Profile energy prices reuse Core's fuel/unit enums and allow at most one entry
per pair; the enum combinations bound that collection. The price collection is
copied on construction. Calendar months use explicit year/month values.

`SensitivityValue.Constant(value)` and `.Scenarios(favorable, baseline, cautious)`
are immutable, mutually exclusive constructions. Missing assumptions use null;
there is no incomplete-trio fallback. Validation checks every supplied mode,
without imposing a favorable-to-cautious ordering. Missing amounts are allowed
as inputs; supplied out-of-range amounts have stable field errors. Required
financing components are identified separately for each purchase.

The result has currency, active mode, all profile validation errors, and one
purchase result per input in order. Candidate keys are trimmed, ordinal-unique
and 1-120 characters; a batch accepts 0-100 purchases. Malformed keys, duplicate
keys, null entries, unsupported enums and oversized collections throw
`HouseholdInputValidationException`. Numeric errors instead invalidate only
dependent financing results. Core paths start with `profile` or `vehicles[i]`;
future HTTP mappings must preserve the associated field meaning.

Each purchase result exposes state, cash allocation, optional loan/installments,
setup/monthly fees, financing cost (interest plus fees), acquisition cash
outflow, missing-component paths and applicable errors. Cash-only purchases
have zero financing costs and no loan; missing horizon/loan terms are irrelevant
to that financing section. For a financed purchase, missing fees do not hide a
known loan schedule, but complete financing/cash totals remain null. Profile
errors in unrelated driving/energy/budget fields remain visible without hiding
valid financing. Missing price/cash prevents allocation; invalid values never
become zero or reduce another candidate's results.

Core results retain full decimal precision for later cost composition. Display
rounding is a later boundary; callers must not sum prematurely rounded rows.
Installment month offsets start at 1 and end at `min(horizon, loan term)`;
calendar/budget mapping remains later work. Setup is charged once at month 0,
and monthly fees end with installments. The annuity is evaluated as
`principal / sum((1 + monthlyRate)^(-t), t=1..term)` using iterative decimal
discounting, equivalent to the formula below but stable near zero interest.
The last installment clears only decimal residue, preserving principal.

Regression coverage is in
[financing tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdFinancingCalculatorTests.cs)
and [input tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdInputValidationTests.cs).
It covers A1 and the financing portion of A2, independent high-precision
nonzero-rate references, horizon/fee limits, missing/invalid inputs, explicit
mode selection and precision. Existing v1 calculations and contracts are unchanged.

## Inputs and units

The following names describe target wire fields; Core and persistence may use
their own types without changing semantics. JSON fields/enums are camel case,
decimals are JSON numbers, and missing values are `null`, never invented zeros.
No inflation, discounting, variable-rate loans, balloon loans, or automatic
market residual estimation is introduced. All fuel types have equal priority.

| Contract | Fields and ownership |
| --- | --- |
| `HouseholdProfileInput` | `startMonth` (`YYYY-MM`), `periodMonths`, `annualDistanceKilometres`, `purchaseCashSek`, common `loanTerms`, energy prices by fuel/unit, `electricDrivingSharePercent`, `homeChargingSharePercent`, home/public SEK/kWh, `chargingLossPercent`, nullable `startupBudgetSek` and `monthlyBudgetSek`, `activeSensitivityMode`. |
| `LoanTerms` | `annualNominalInterestRatePercent`, `termMonths`, `setupFeeSek`, `monthlyFeeSek`; constant nominal annuity, monthly payments in arrears. |
| `VehicleCostInput` | `acquisitionType: purchase` or `lease`, corresponding purchase/lease object, energy inputs, tax, insurance, planned service, known repair events, `additionalRepairAllowancePerMonthSek`, custom recurring/one-time costs, unresolved legacy items, reviewed listing version. |
| `PurchaseInput` | `priceSek`, `residual` with mode `fixedAmount` or `annualPercentage`; fixed amount includes `amountSek` and `periodMonths`, percentage includes `annualRatePercent`. |
| `CostItem` | Stable item key, category, label, known amount/cadence, payment timing, optional bounded evidence note/source URL. Timing and amount can independently be missing. |
| `SensitivityValue` | Either a single amount or all three named values `favorable`, `baseline`, `cautious`; never a silently substituted missing mode. |

Household fields are shared across every candidate, without vehicle overrides.
Numeric assumptions may be missing for partial previews and saved current
inputs. Supplied invalid values are errors. Identifiers, discriminator values,
and collection structure must be valid even for drafts. Budget `null` means no
limit; explicit zero is a real limit. Missing applicability-specific energy or
loan fields affect only candidates that need them.

Retain v1 limits: horizon and loan term 1-120 integer months, annual km
0-1,000,000, money 0-100,000,000 SEK, nominal annual rate 0-100 percent,
consumption greater than zero through 10,000 units/100 km, unit prices
0-100,000 SEK. New money fields use the same money limit. Percentage shares and
depreciation are 0-100; charging loss is 0 through less than 100. Labels are
1-120 trimmed characters, notes at most 1,000, source URLs at most 2,048 with
existing URL validation. Each cost collection is bounded to 50 distinct keys;
energy sources remain bounded to two. Start month is a valid month in years
1900-9989 so the complete supported horizon fits the calendar. No system clock
is read by Core. Distances use km in contracts and exactly 10 km per Swedish mil.

Sensitivity is supported for common loan rate, energy prices, driving/charging
shares and loss, and car-specific consumption, service/repair/custom amounts,
residual inputs, and lease additional-cost estimates. Fixed quote purchase
price, loan term, lease term/quoted payments, calendar timing, use distance,
cash, and budget limits remain single values. Users can edit those shared
assumptions normally. Validate every supplied mode, including share sums and
residual limits. Mode names express user assumptions, not statistical bounds;
do not reorder entered values or promise that favorable is always cheapest.

## Purchase and financing

For each alternative independently, let price be `P`, common cash `C`, horizon
`M`, term `N`, and monthly rate `i = annualNominalRatePercent / 1200`:

```text
cashApplied = min(P, C)
principal L = max(P - C, 0)
unusedPurchaseCash = max(C - P, 0)
A = L / N                                      when i = 0
A = L * i * (1+i)^N / ((1+i)^N - 1)             otherwise
k = min(M, N)
balance B0 = L
interest[t] = balance[t-1] * i
principalPaid[t] = min(balance[t-1], A - interest[t])
balance[t] = balance[t-1] - principalPaid[t]
payment[t] = principalPaid[t] + interest[t]
```

Payments occur in months 1 through `N`. Clear only decimal arithmetic residue
at the final installment; principal repaid must equal `L`. Do not round an
installment before further calculations. Setup fee is paid at month 0 and is
not financed. Monthly fees occur only with installments, up to `k`. When `L=0`,
all interest, loan fees, and payments are zero and missing loan terms are
irrelevant. Cash is for purchase only; unused cash does not silently fund a
separate startup-budget limit. Missing price or cash prevents financing, not
unrelated operating calculations. Negative values are invalid.

## Residual, costs, and output measures

For annual percentage `r = annualRatePercent / 100`:

```text
residual R = P * (1-r)^(M/12)
depreciation = P - R
distance D = annualDistanceKilometres * M / 12
estimatedPurchaseCost = depreciation + interest[1..k] + loanFees
                      + accruedOperatingCosts + repairAllowance
monthlyCost = estimatedPurchaseCost / M
costPerMil = estimatedPurchaseCost / (D / 10)    only when D > 0
estimatedEndEquity = R - balance[k]
```

Use decimal arithmetic throughout, including fractional-year depreciation.
A deterministic implementation takes a decimal twelfth root of `(1-r)` by
bounded bisection on [0,1], stopping when the bracket is at most `1e-24` wide or
no representable progress remains, then raises its midpoint to integer `M`.
Use exact branches for `r=0`, `r=1`, and whole-year powers. Test this algorithm
in Core; do not introduce `double`/`Math.Pow` monetary authority. Money outputs
round to 2 places, other calculated quantities to 3, midpoint away from zero,
only after full-precision aggregation as in v1. The schedule is an estimate,
not a bank's contractually rounded invoice ledger.

Fixed residual must be 0 through purchase price and carries its entered
horizon. At a different `M`, return `residualHorizonMismatch` and no complete net
total until the user supplies a new value or chooses percentage mode. Retain
the entered amount for review; never extrapolate it. Returning to its original
horizon makes it applicable again. For `D=0`, per-mil output is unavailable
(`zeroDistance`), while other complete outputs remain valid.

Recurring monthly amounts accrue `amount*M`; annual amounts accrue
`amount*M/12`. Service/known-repair one-time events accrue once in their entered
month if within 0..M. Custom events follow the same rule. Each category uses
either its own item collection or an explicit known zero/included state;
`null` means unknown. Empty confirmed collections mean known zero. An item has
one category and one key, and cannot also appear in custom costs. Users see
that the additional allowance excludes known work; do not infer duplicate
repairs merely from equal amounts.

The additional repair allowance accrues `monthlyAllowance*M`. It is an estimate
of additional uncertain repairs and also the proposed monthly reserve transfer.
Add it once to estimated cost; exclude transfers from external cash outflow.
Do not generate workshop invoices from an allowance. Planned service, known
repairs, and this allowance have separate completeness and totals. Evidence is
manual input or a relevant quote; AI proposals are deferred.

`externalCashOutflow` includes purchase cash, loan installments and fees, and
actual scheduled operating/lease payments. Refunds are separate inflows;
`netExternalCashFlow = outflow - inflow`. A purchase's hypothetical residual
and end-loan settlement are shown as end equity, not booked as an actual sale.
Neither principal nor a refundable deposit is an ownership expense. A reserve
transfer is internal funding. Show explicit reconciliation of cost versus cash
using depreciation, principal, fees, accrued-versus-paid costs, deposits/refunds,
and allowance. Do not derive estimated cost by summing cash rows.

## Energy

Each source declares fuel identity, unit (`litre | kilowattHour | kilogram`),
`consumptionPer100Kilometres`, and
`consumptionBasis: wholeDistance | drivingMode`. Electricity additionally
declares `electricityBasis: battery | metered`; the latter already includes
charging losses. Household prices match fuel and unit, not a free-text label.

For driving-mode inputs, all single-energy cars (including battery cars) use 100 percent;
plug-in electric mode uses the common electric-driving share and the other
mode its complement. Modes with zero share need no consumption or price.
Positive shares must sum to 100. A non-plug-in hybrid with only supplied fuel
consumption is a single fuel source; do not invent paid electric energy.

```text
baseQuantity = D * consumption / 100           wholeDistance
baseQuantity = D * (modeShare/100) * consumption / 100
                                                drivingMode
meteredKwh = baseQuantity / (1 - lossPercent/100) battery basis
meteredKwh = baseQuantity                       metered basis
electricPrice = homeShare/100 * homePrice
              + (1 - homeShare/100) * publicPrice
energyCost = sum(sourceMeteredQuantity * sourcePrice)
```

Whole-distance sources may coexist (for example quoted hybrid fuel and kWh
per 100 km), but must not receive another driving-share multiplier. Reject
mixing whole-distance and mode-specific source bases in one car until the user
normalizes them explicitly. Charging mix weights purchased kWh, not km; loss
means the fraction of purchased energy lost. A missing price in a zero-weight
location is irrelevant. For zero driving distance energy use/cost is zero
without fabricated consumption. Otherwise missing source/basis/price makes
energy partial. Keep energy quantities available when only prices are missing.

## Calendar, payments, and budget

Month 0 is acquisition immediately before month 1, the selected start calendar
month. Month `t` (1..M) maps to `startMonth + t-1`. Monthly charges are paid in
each applicable month. Annual recurring costs supply one `dueMonthOfYear`
(1-12) and pay their full annual amount whenever that calendar month occurs;
their economic accrual remains prorated. No guessed refund at period end or
catch-up bill before the first entered due month. One-time events use an
explicit `monthOffset` 0-120 and appear only within the horizon. Missing timing
can leave an accrual computable while the cash schedule is incomplete.

Month-0 non-purchase outlays, loan setup fees, and refundable lease deposits
belong to `startupFundingRequired`. Purchase cash has its own allocation and
is excluded from this startup limit. The ongoing requirement is
`(externalOutflowsInMonths1ThroughM + allowance*M) / M`; incoming deposit refunds
do not reduce the ongoing spending test. Optional limits compare with `>` so
equality passes. Single expensive months remain visible but do not trigger the
ongoing-budget warning if its average is within the limit. Missing required
amounts/timing produce an unknown budget result; a known subtotal already above
the limit can report exceeded with incomplete evidence, never a false pass.

## Leasing

`LeaseInput` has `termMonths` (1-120), `upfrontNonRefundableSek`, one quoted
monthly payment for each contract month (bounded to 120), `refundableDepositSek`,
`depositRefundSek` (0 through deposit), `includedDistanceKilometres` for the whole
term, `excessDistancePricePerKilometreSek`, explicit end fees and other dated
payments, and inclusion states for each operating category. Refund/end costs
are paid in contract month `N`. Missing quoted amounts or possible obligations
are represented as unknown; explicit zero means the user has entered no charge.
An unresolved variable-price clause makes the total incomplete until the user
provides explicit payment assumptions, identified as estimates.

```text
excessKm = max(annualKm*N/12 - includedKm, 0)
estimatedLeaseCost = upfrontNonRefundable + sum(quotedMonthlyPayments)
                   + excessKm*excessPrice + endFees + otherContractPayments
                   + depositWithheld
                   + nonIncludedAccruedOperatingCosts + repairAllowance
depositWithheld = deposit - refund
```

For `M=N`, all known obligations and inclusions can produce a complete total.
For `M!=N`, return `leaseHorizonMismatch`, no fully comparable total, and known
cash payments limited to months 0..min(M,N). Do not prorate an exit fee, assume
a refund before contract end, invent a replacement lease, or extrapolate past
N. Show coverage ending at N for longer horizons. Full-term obligations may be
shown separately, explicitly labeled. Excess-distance charge is scheduled only
at N based on full-term common use; it is zero when excess distance is zero
even if an unused rate is missing.

Included categories contribute zero *additional* cost with an included label;
prevent entry of the same service/insurance twice. Allow explicitly excluded
extras as distinct items. Lease payments never receive purchase depreciation,
purchase cash allocation, principal, interest, or resale equity. Lease price
and use assumptions remain quote-dependent; saving still requires registration.

## Partial preview and future HTTP contract

`CalculateHousehold(profile, vehicles) -> HouseholdPreview` is pure. A preview
contains a client `requestId`, normalized profile, active mode, calculation
version, and one result per unique `candidateKey` in input order (max 100 per
request; the UI batches larger saved sets consistently). A candidate can be
unsaved/unregistered in a single manual preview; persisted/comparison identities
must have registration. Transport/body size limit is 2 MiB for these new JSON
endpoints. Malformed JSON, invalid envelopes, duplicate keys, and invalid enum
structure return 400. A parsed numeric field outside its range yields a field
error and invalid dependent sections in a 200 preview, preserving other cars.
Persisting supplied invalid values returns 400; missing values are permitted.

Each result contains `sections` for financing, depreciation, energy, tax,
insurance, service, repairs, allowance, custom costs, lease, payment calendar,
budgets, and totals. A section exposes `state: complete | partial | unavailable |
invalid | notApplicable`, `knownSubtotalSek`, nullable `completeTotalSek`,
`missingComponents`, and `errors` with stable code and JSON input path. Keep
known energy quantities separate from unknown money. Global errors invalidate
only dependent sections. A missing start month need not hide distance or cost
accrual. A result is cost-comparable only when acquisition and all applicable
cost sections are complete, residual/horizon is valid, and unresolved legacy
cost items do not remain. Missing payment timing affects cash/budget completeness
but does not by itself invalidate an otherwise complete accrued cost.

| Future route | Semantics |
| --- | --- |
| `POST /api/household-calculations/preview` | Fully supplied profile and candidate inputs; 200 independent results, no database or AI access. |
| `GET /api/household-profile` | Current values/revision, or 404 before initialization. |
| `PUT /api/household-profile` | Explicit full save, `expectedRevision` (0 only for create); 200 current profile. |
| `POST /api/vehicle-cost-inputs` | Registration plus current input, create 201; cannot create a duplicate vehicle. |
| `GET /api/vehicle-cost-inputs` | All registered current summaries including listing-only and legacy/review states. |
| `GET /api/vehicle-cost-inputs/{vehicleId}` | Current inputs independent of result-version support. |
| `PUT /api/vehicle-cost-inputs/{vehicleId}` | Attach/replace current input using aggregate `expectedRevision` and listing-link mode. |
| `DELETE /api/vehicle-cost-inputs/{vehicleId}?expectedRevision={revision}` | 204 whole-vehicle deletion. |
| `GET /api/household-transition` | Current legacy inputs, review obligations, transition revision, and vehicle revisions; no guessed shared defaults. |
| `POST /api/household-transition` | Atomically confirm supplied shared profile and reviewed mappings using all expected revisions. |
| `GET/PUT/DELETE /api/vehicle-draft` | Read/explicitly save/remove the shared singleton draft with its own expected revision; empty GET returns 200 with null payload and current slot revision. |
| `POST /api/vehicle-draft/adopt` | Atomically adopt draft into current registered data and consume it, checking draft and vehicle revisions. |

When legacy scenarios await transition, profile creation/replacement uses the
transition operation; direct profile PUT returns `householdTransitionRequired`
instead of bypassing confirmation. A fresh household without legacy scenarios
can initialize through profile PUT normally.

Saved operations never invoke AI. GET inputs do not require supported stored
result schemas. Duplicate registrations, stale profile/vehicle/draft/transition
revisions, and legacy transition-required writes return 409 with stable codes
and current revision metadata; unavailable resources return 404; storage failure
returns 503 without success or partial writes. Normal errors use ProblemDetails;
field validation uses ValidationProblemDetails. Never accept trusted client
result snapshots. Existing v1 routes remain compatible for unconverted records;
v1 writes to converted records return `householdTransitionRequired` with the
new route, preventing divergence. Existing delete routes retain whole-aggregate
semantics. This guard is future API work, not part of this documentation PR.

## Saving, concurrency, draft, and migration

Profile and vehicle revisions are independent. Preview requests include an
opaque client generation ID and all effective values; server echoes it. The
browser debounces 500 ms, cancels older work, and publishes only the latest
generation across all tables. Invalid edits mark affected sections, never show
old totals as current. Save responses cannot replace edits made after a save
started. Conflicts require reload/review, never silent retry or last-write-wins.
Only **Spara** persists a profile. Unsaved changes, including sensitivity mode,
are labeled. Saved vehicles keep current inputs; results may be recomputed from
them. Any cache must match profile/vehicle/listing/calculation versions.

PostgreSQL stores one singleton profile and current vehicle-owned input rows;
decimal values retain precision. Current listing and its explicit reviewed
version remain separate. Replacing a listing flags review needed and does not
silently adopt advertised facts. No historical profiles, inputs, evaluations,
or calculations are retained. Schema/revision metadata is allowed.

The draft slot stores at most one registered vehicle's partial cost input and/or
bounded reviewed listing payload, base vehicle revision (when existing), and
own monotonically increasing revision. The empty slot retains only revision
metadata to prevent delete/recreate races. It has no automatic expiry. Explicit
**Spara utkast** can update the same draft; replacing another registration also
requires `replaceExisting: true` after a visible choice and expected revision.
Opening does not consume it. Successful explicit adoption consumes it in the
same transaction; cancellation/failure does not. The draft owns no independent
household profile. The existing 1-10 transient URL forms remain in memory and
do not become ten saved drafts. Invalid numeric text must be corrected before
saving; missing typed values are allowed.

Whole-vehicle deletion removes listing, cost inputs, children, caches/current
evaluations, and a matching draft, preserving shared profile/rules. Existing
vehicle draft saves/adoption check the original UUID/revision; deletion cannot
be undone by a stale draft or in-flight request. Unregistered drafts cannot be
persisted; a draft for a not-yet-created registration can be deleted directly.
Deletion of an open vehicle clears its recoverable UI data and invalidates
requests so it is not inadvertently saved back. No application PDF archive.

Migration adds only the explicitly assigned real model. Existing vehicle facts
and original legacy household inputs stay accessible pending transition; there
is one current legacy record, not a historical copy. The user starts an empty
shared profile, reviews affected vehicle mappings, and explicitly confirms
before legacy individual household assumptions are replaced. Concurrent changes
abort the transaction. Clearly car-specific values are preserved; combined
maintenance, undated items, or ambiguous energy basis stay as current
`unresolvedLegacyItems` until explicitly classified. They block only affected
totals. Do not copy them into several categories. Replace/discard old derived
snapshots after confirmation; unsupported result versions must not block lists
or recovery of valid current inputs. Rollback and migration verification use
disposable fixtures and documented backups, never live-data experimentation.

## Worked examples and acceptance

All amounts below are synthetic test assumptions, not market estimates. Unless
stated otherwise, unused categories are explicitly confirmed zero.

| ID | Inputs | Required output |
| --- | --- | --- |
| A1 | Cash 30,000; prices 25,000 / 30,000 / 80,000 | Applied cash 25,000 / 30,000 / 30,000; loans 0 / 0 / 50,000; unused cash 5,000 / 0 / 0. |
| A2 | P=80,000, C=30,000, N=10, M=12, rate=0%, setup=500, monthly fee=25, R=60,000 | Installment 5,000; principal repaid 50,000; fees 750; net cost 20,750; purchase/loan cash outflow 80,750. Months 11-12 have no loan payment/fee. |
| A3 | P=100,000, annual depreciation 10%, M=12 / 24 / 6 | R=90,000 / 81,000 / 94,868.33 SEK. |
| A4 | Fixed R=60,000 at M=24; change M to 36 | Retain entered R and original horizon for review; net total unavailable with residualHorizonMismatch, operating costs still calculable. |
| A5 | D=12,000 km, 60% electric, battery 18 kWh/100 electric km; fuel 6 L/100 remaining km; 10% loss; 80% home at 2 SEK, 20% public at 5; fuel 20 SEK/L | Battery 1,296 kWh, purchased 1,440; electricity 3,744 SEK; fuel 288 L and 5,760 SEK; total 9,504 SEK, 7.92 SEK/mil. |
| A6 | Same D, whole-distance 10 kWh and 3 L per 100 km, metered electricity, mixed price 2.60, fuel 20 | 1,200 kWh and 360 L; total 10,320 SEK. Do not multiply by driving shares or losses again. |
| A7 | Start Jan, M=6, annual tax 1,200 due March | Accrued cost 600; March payment 1,200; payment-average budget contribution 200/month. |
| A8 | M=12, service 1,200 at month 4, known repair 2,400 at month 2, additional reserve 300/month | Estimated maintenance/repair cost 7,200; workshop outflow 3,600; reserve transfers 3,600; ongoing funding average 600. |
| A9 | Lease N=M=24, upfront 6,000, 2,000/month, 24,000 included km, annual km=15,000, excess=1 SEK/km, deposit/refund=3,000, no other cost | Excess 6,000 km; estimated total 60,000, monthly 2,500; external outflow 63,000, refund 3,000, net 60,000. Startup funding 9,000; ongoing average 2,250. |
| A10 | A9 at M=12 / 36 | Horizon mismatch; known outflows 33,000 / 63,000, refunds 0 / 3,000. Neither result is a full comparable cost for the selected period. |
| A11 | One known 2,400 payment in month 2 over M=12; monthly limit 200 | Average 200 passes despite month 2; limit 199 warns. A separate 500 startup item does not change the average. |

The [verification plan](household-comparison-verification.md) adds regression
and practical acceptance gates. Code, migrations, routes, schema generation,
and UI are delivered only through the corresponding backlog issues.
