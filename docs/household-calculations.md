# Household calculations specification

## Status and scope

Normative target for stage 3A, agreed 2026-09-06; **Core, persistence, HTTP and Swedish UI implemented**. The
[product plan](household-comparison-plan.md) sets scope. This document adds
household contracts alongside the implemented [v1 API](manual-calculator.md).
Core owns deterministic decimal calculation; API maps HTTP; Infrastructure owns current
PostgreSQL data; React owns Swedish forms and request cancellation.

## Implemented Core financing foundation

Issue #55 implements `Households.HouseholdProfileInput`, `HouseholdLoanTerms`,
`VehiclePurchaseInput`, `SensitivityValue`, `HouseholdInputValidator`, and
`HouseholdFinancingCalculator`. The remaining sections describe the complete
stage target. The ownership-cost subset added by #56 is documented below;
leasing, payment calendars and budgets added by #57 are documented below.
Persistence (#58), HTTP (#59) and the [Swedish workspace](household-workspace.md)
(#60) are implemented. Practical whole-stage acceptance remains #61 work.

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
HTTP mappings preserve the associated field meaning.

Each purchase result exposes state, cash allocation, optional loan/installments,
setup/monthly fees, financing cost (interest plus fees), acquisition cash
outflow, missing-component paths and applicable errors. Cash-only purchases
have zero financing costs and no loan; missing horizon/loan terms are irrelevant
to that financing section. For a financed purchase, missing fees do not hide a
known loan schedule, but complete financing/cash totals remain null. Profile
errors in unrelated driving/energy/budget fields remain visible without hiding
valid financing. Missing price/cash prevents allocation; invalid values never
become zero or reduce another candidate's results.

These financing results retain full decimal precision for cost composition. Display
rounding is a later boundary; callers must not sum prematurely rounded rows.
Installment month offsets start at 1 and end at `min(horizon, loan term)`;
calendar/budget mapping is now composed by #57. Setup is charged once at month 0,
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

## Implemented Core ownership costs

Issue #56 adds `HouseholdCostCalculator.Calculate(HouseholdProfileInput,
IReadOnlyList<VehicleCostInput>) -> HouseholdCostPreview`. The preview contains
SEK, the common active mode, calculation/result-schema versions (both initially
1 in the separate household version family, now 2 with #57), profile errors and ordered car
results. It has no persistence, HTTP, request-generation, clock or AI dependency.
The public v1 calculator and its stored-version handling are unchanged.

`VehicleCostInput` owns price, optional `HouseholdResidualInput`, at most two
keyed energy sources, category inputs for tax/insurance/service/repairs/custom
costs, and the additional monthly repair allowance. It reuses the profile and
financing contracts from #55 with no per-car household overrides. Residual
factories select either a fixed sensitivity amount plus horizon or an annual
percentage sensitivity value. Source fuel, unit, consumption basis and
electricity basis can be missing; supplied unknown enum values are structural
errors. Electricity requires kWh. Two driving-mode sources require electricity
and one other fuel; two non-electric fuels can use whole-distance consumption.

`HouseholdCostCategoryInput.FromItems` snapshots the supplied collection;
`KnownZero()` and `Included()` create distinct confirmed zero-cost categories.
A null category is unknown. Each item has a key, label, amount, nullable
monthly/annual/once cadence, optional month offset/due month, note and source
URL. Keys are ordinal-unique after trimming across all cost categories, so an
item cannot also appear in custom costs. Equal amounts with different keys are
not automatically duplicates. Quoted tax/insurance amounts use constants;
service/repair/custom amounts and the allowance also accept sensitivity trios.
The bounds and evidence-URL validation below apply to every supplied value.

Each result has independent cost sections, category item rows, energy source
quantities/prices, residual, ownership/monthly/per-mil totals and end equity.
`FinancingDetails` preserves the existing unrounded #55 contract; new cost
sections and energy/residual display fields round only after full-precision
composition. Internal accumulators are not composed from rounded result rows.
An unknown price cannot erase known energy quantities or other priced sources;
a partially known charging-price mix retains its known priced contribution.
Non-electric fuel prices require an exact fuel/unit match. Electricity always
uses the common home/public mix rather than a fallback energy-price entry.

`CostSectionResult` uses the documented states, missing paths and field errors.
Known monetary sums include only computable contributions; zero in an unknown
section is an empty known sum, never confirmation of a zero complete cost.
Known quantities or financing allocation can give a partial section even when
no monetary contribution is known. Complete totals require all applicable
purchase-cost sections and a valid residual. Missing or invalid due months and
start months remain visible in input errors without invalidating known accrual;
unknown one-time timing prevents that item's period cost. Out-of-period events
contribute zero without requiring an unused amount.

All supplied sensitivity values are validated, including inactive modes.
Unrelated numeric errors stay in profile/car input errors without blocking
independent sections; needed invalid inputs invalidate dependent sections.
Malformed keys/enums/collections/labels/evidence throw the structural validation
exception. Additional calculation codes are `energyBasisMismatch`,
`unsupportedDrivingModes`, `invalidEnergyUnit`, `residualHorizonMismatch`, and
`calculationOutOfRange`; `zeroDistance` is a per-mil unavailability reason.
Decimal overflow or loss of a positive distance/energy/weighted-price value
below representable precision produces a dependent calculation error. An
unrepresentable known monetary sum is null, retaining its individual section
or source rows; it is never saturated or reported as zero. A per-mil overflow
does not invalidate a representable period/monthly cost.

Regression tests cover the A2 ownership total, A3-A6, A7 accrual and A8 estimated
cost, independent 70-digit residual references, near-total depreciation,
source/aggregate/per-mil overflow, partial inputs, collection validation and
sensitivity across candidates. See [cost tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdCostCalculatorTests.cs),
[energy tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdEnergyCalculatorTests.cs)
and [validation tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdCostInputValidationTests.cs).
Actual payment/funding reconciliation is implemented by #57 as described below;
saved/legacy-input integration and the practical whole-stage acceptance remain
later issues.

## Implemented Core leasing and payments

Issue #57 extends the same `HouseholdCostCalculator.Calculate` operation. Both
household calculation and result-schema versions are now **2**. Existing
purchase constructors and the v1 application calculator remain supported.
`VehicleCostInput.AcquisitionType` defaults to purchase; `ForLease` creates a
lease candidate with no purchase price/residual. Mixed purchase/lease fields
and unsupported discriminators are structural errors. Lease results have null
`FinancingDetails` and `notApplicable` financing/depreciation/end-equity sections;
purchases have a `notApplicable` lease section. These sections contribute no
cost and require no assumptions.

`HouseholdLeaseInput` snapshots keyed-month `MonthlyPayments`, `EndFees`, and
`OtherPayments`. Missing/null payment amounts or absent contract months remain
unknown; a zero payment is explicit. End/other fee collections use the same
unknown-versus-confirmed-empty distinction as operating categories. End fees
always fall at the contract end and reject an independent offset; other
payments require an offset within the term. Their keys share the operating
item namespace. Each fee collection accepts at most 50 items, monthly payments
at most 120 unique months. Included contract distance is 0-10,000,000 km and
excess distance price is 0-100,000 SEK/km; existing money/period bounds apply.

`PriceBasis` is quoted, estimated, unresolved, or missing. Explicit estimated
payment assumptions remain single values and are labeled in results. Missing
or unresolved pricing preserves known quoted payments while preventing a
complete cost or false budget pass. Additional charges, excess-distance rate
and deposit-refund estimates accept the common sensitivity modes; every
supplied mode is validated, including the refund limit against the deposit.
No deposit needs no unused refund input; a positive deposit with unknown refund
does not become a known expense. Known refunds remain separate inflows and
cannot improve an expenditure-budget test.

`HouseholdCostCategoryInput.Included(extras)` explicitly marks every supplied
item as outside the included base service. `Included()` confirms no extra cost.
Lease `EnergyIncluded` means the whole energy charge is included: additional
energy cost is zero without requiring missing prices/consumption. Known usage
quantities remain available. Explicit money extras can be entered separately;
no partial energy allowance or invented consumption is inferred.

`HouseholdLeaseResult` distinguishes term, covered months, estimated pricing,
excess distance and withheld deposit from the contract-cost section. For
`M!=N`, `leaseHorizonMismatch` blocks comparable totals. Only covered months
`min(M,N)` contribute known lease, energy, operating and reserve values; no
payments or saving are extended beyond the contract. Overall distance and
monthly/per-mil denominators still refer to the requested common period.
Longer horizons carry `payments.uncoveredMonths` in cash/funding completeness.
Shorter horizons may have a complete selected-period expenditure budget while
their ownership cost remains noncomparable. No separate full-term report is
generated for mismatched periods.

`HouseholdPaymentCalendar` exposes requested/covered months, calendar status,
at most 121 relative month rows, source summaries, external outflow/inflow,
net external cash flow, and internal saving. Month zero has no invented calendar
date. Other rows use the explicit start month. Rows have category/direction
totals; source summaries identify calculation/input paths, labels, applicable
offsets, estimates, payment completeness, and known unscheduled amounts.
Unscheduled quoted amounts are not added to period payment subtotals. Missing
annual timing affects ongoing funding, whereas an undated one-time item may
also affect startup funding. Missing calendar labels alone do not invalidate
known relative monthly payments or their average.

Energy is an explicitly estimated uniform monthly payment over the covered
period. Each unrounded source amount is distributed independently, with only
decimal residue assigned to its last month. Display-rounded rows are never
summed to compute totals. Period/source errors remain local; representable
monthly rows survive a period aggregate overflow. If the ongoing period sum
overflows, the average is calculated from the independently representable
source contributions before rounding, preserving source errors and missing
evidence. Unrepresentable results retain `calculationOutOfRange` and null sums.

`HouseholdBudgetResult` includes the optional limit, funding section and
`notConfigured | withinLimit | exceeded | unknown | invalid` status. Compare
unrounded funding with the limit; equality passes. A safe known subtotal above
the limit can establish exceeded even with incomplete evidence. Invalid limits
affect their own budget only; no configured limit is distinct from a zero limit.

`HouseholdCashReconciliation` exposes purchase cash, repaid principal,
depreciation, accrued/paid operating costs, paid/refunded/withheld deposit and
repair allowance. The unrounded reconciliation is:

```text
cost = netExternalCashFlow - purchaseCash - principalRepaid + depreciation
     + accruedOperatingCosts - paidOperatingCosts
     - depositPaid + depositRefund + depositWithheld + repairAllowance
```

Nonapplicable terms contribute zero internally. The full reconciled result
requires complete applicable inputs and a comparable ownership period; partial
terms remain visible. Reserve saving is internal, never a workshop invoice.

See [calendar tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdPaymentCalendarTests.cs),
[lease tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdLeaseCalculatorTests.cs),
[lease validation tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdLeaseValidationTests.cs),
and [payment precision tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/HouseholdPaymentPrecisionTests.cs).
They cover A2/A7-A11, year boundaries, partial data, deposits/inclusions,
bounded immutable inputs, sensitivity, local arithmetic errors and budget
thresholds. Persistence/migration (#58) and HTTP/types (#59) are implemented.
Swedish UI (#60) and practical whole-stage acceptance (#61) remain later work.

## Implemented household persistence

Issue #58 adds PostgreSQL stores under
[`Persistence/Households`](../src/backend/CarExpenseCalculator.Infrastructure/Persistence/Households/HouseholdContracts.cs).
These are Infrastructure contracts, separate from the implemented HTTP DTOs. Core
calculation/result versions remain **2**; the persisted input format starts at
**1**. No household result cache or history table is introduced.

| Table | Stored state |
| --- | --- |
| `household_state` | Singleton `id=1`, nullable typed JSONB profile, storage version, independent profile and transition revisions. Initially both revisions are 0 and profile is null. |
| `vehicle_cost_inputs` | One JSONB input/review payload per existing vehicle UUID, storage version and nullable reviewed listing version. Vehicle identity, label, timestamps and revision remain in `vehicles`. |
| `vehicle_draft` | Singleton `id=1`, own revision, nullable registered payload, storage version, original vehicle UUID/revision when editing an existing car. Initially revision 0 with no content. |

Persistence-owned payload records explicitly map all profile, purchase, lease,
energy, cost, timing, evidence and reviewed listing fields to/from Core. Decimal
JSON numbers are serialized/deserialized as `decimal`, with no display rounding
or floating-point conversion. Null, zero, confirmed empty and included-with-extras
remain distinct, including array order and sensitivity trios. Each JSONB payload
is bounded to 2 MiB. Core validation rejects every supplied invalid value,
including inactive sensitivity values; missing typed fields remain saveable.
Vehicle and draft saves do not require a populated household profile. The Core
100-candidate calculation limit does not truncate the collection of saved cars.

| Store | Implemented operations |
| --- | --- |
| `IHouseholdProfileStore` | Read nullable profile/revision; replace the complete profile with an expected revision. A null row never supplies financial defaults. |
| `IVehicleCostInputStore` | Create a registered car, read by UUID or normalized registration, list all listing-only/legacy-pending/current cars, replace current inputs, delete the whole vehicle. Persisted calculation candidate keys use the normalized registration. |
| `ISharedVehicleDraftStore` | Read, explicit save/replace, delete, atomically adopt the saved contents. All mutations require the slot revision. |
| `IHouseholdTransitionStore` | Read a consistent review snapshot; explicitly confirm the newly entered profile and complete set of pending vehicle mappings. |

All relevant writes, including existing v1 scenario and listing writes, acquire
`SELECT ... FOR UPDATE` on `household_state` inside a transaction **before**
loading and checking revision-owned data. Stores discard their previous tracked
entities before loading fresh values. Profile revision changes only on profile
save or transition confirmation. Vehicle revision changes once per aggregate
write; listing version changes only on listing writes. Transition revision
changes when pending legacy inputs are created, replaced, deleted, their listing
changes, or confirmation removes the pending set. A draft-only save does not
alter profile/vehicle/transition revisions. Transition and combined input reads
use a repeatable-read database snapshot, including split child queries.

`HouseholdStoreException` exposes a stable code, affected vehicle UUID and
expected/actual revision when applicable. Conflict codes include
`profileRevisionConflict`, `vehicleRevisionConflict`, `draftRevisionConflict`,
`transitionRevisionConflict`, `transitionSetConflict` and
`registrationNumberConflict`. Deleted originals return `vehicleNotFound`;
unsupported new storage formats return `unsupportedHouseholdInputVersion`.
Core input errors retain their field paths in `HouseholdInputValidationException`.
There is no automatic retry or overwrite. Cancellation/database failure rolls
back every modified row, including intermediate listing-child replacements.
The v1 stores retain their existing conflict types for current HTTP compatibility.

Drafts require cost input, a normalized bounded reviewed listing, or both.
Cross-registration replacement additionally requires `replaceExisting: true`.
An existing-car draft requires its original UUID and current base revision on
save and adoption; a new-car draft must still have an unused registration on
adoption. Reads do not consume it. Adoption writes only supplied parts, leaving
omitted cost/listing parts unchanged, then clears the slot in the same transaction.
The aggregate revision increases once, even when both parts change.
`SavedScenarioListingLinkMode.Preserve` retains the reviewed listing version;
`Current` explicitly acknowledges the version after any included listing write.
It requires a listing. Deleting or consuming a draft increases the slot revision;
even deleting an already empty slot increases it. No expiry is scheduled.
`VehicleCostWrite.VehicleLabel` sets a cost-only vehicle's label. When a listing
exists, its label and provenance are preserved; a differing supplied cost label
returns `listingLabelRequiresReview`. Change that label through the reviewed
listing part, keeping any supplied cost label consistent with it.

Schema migration preserves all existing v1 data. Direct profile saves and new
cost replacement/adoption on a legacy car return `householdTransitionRequired`
until explicit confirmation. Legacy recovery reconstructs inputs without
deserializing or checking derived result versions. It exposes original individual
assumptions for review, never selects one car's assumptions as a shared profile.
Suggestions retain purchase price, fixed residual with its original horizon,
unambiguous tax/insurance and recurring amounts. Missing payment dates stay missing.

Each original tax, insurance, combined-maintenance, energy, recurring and
one-time item has a stable key based on its persisted source UUID. Confirmation
requires a disposition for **every** source: `KeepForReview`, `Map` to an explicit
current item key, or `Discard`. Mappings require distinct existing targets;
retained/discarded sources cannot also be included under their old keys.
Combined maintenance is not guessed into service/repairs/reserve, energy fuel
identity/basis is not inferred from a label, and undated one-time costs are not
assigned a month. All 50 recurring plus 50 one-time sources fit in the review
envelope separately, alongside up to three scalar costs and two energy sources
(105 review items maximum). The 50-item current custom-cost limit is unchanged.
Unmapped overflow remains review material rather than being truncated or merged.

Confirmation verifies transition/profile revisions, the exact pending vehicle
set and every vehicle revision. It writes the entire profile and reviewed inputs
atomically, preserves vehicle/listing identity and explicitly acknowledged links,
then deletes replaced legacy inputs, children and result snapshots. Only unresolved
car facts survive as current `UnresolvedLegacyItems`; old household overrides and
completed disposition decisions are not archived. Later input replacement must
either omit decisions to retain the complete unresolved set or explicitly account
for each remaining item. Each review item exposes `Reason` and `AffectedSections`.
**The HTTP layer combines this metadata with Core previews** so unresolved legacy costs
block affected completeness even if other current categories appear complete.

Existing v1 writes cannot reintroduce a scenario on a converted car; they return
the typed `householdTransitionRequired` store error, mapped to HTTP 409 by #59.
Every old/new whole-vehicle deletion path removes listings, old/new inputs,
child rows/results and a matching UUID/registration draft, while retaining the
household profile and empty draft revision metadata. Rules/evaluations remain
future 3B work and have no placeholder tables.

The real migration is `20260906151351_AddHouseholdPersistence`. See the
[explicit migration, backup and destructive rollback procedure](deployment-unraid.md#household-storage-migration-and-rollback).
Automated PostgreSQL coverage is in
[storage tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdCostStoreTests.cs),
[transition tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdTransitionStoreTests.cs),
[draft tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/SharedVehicleDraftStoreTests.cs),
[concurrency tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdConcurrencyTests.cs)
and [migration tests](../tests/backend/CarExpenseCalculator.Infrastructure.IntegrationTests/HouseholdMigrationTests.cs).
HTTP/generated types (#59) are implemented. Swedish flows (#60) and practical
stage acceptance (#61) remain subsequent work.

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
bounded bisection on [0,1], with at most 96 iterations, stopping when the bracket
is at most `1e-24` wide or no representable progress remains, then raises its
midpoint to integer `M`. The bisection compares twelfth powers exactly using
scaled base-ten `BigInteger` coefficients; ordinary decimal multiplication can
lose significant digits near 100 percent depreciation. Money, root brackets
and final powers remain decimal; no external mathematics package is needed.
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
each applicable month. Energy is estimated evenly across covered months from
the common annual driving and price assumptions. Annual recurring costs supply one `dueMonthOfYear`
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

For a longer horizon, known operating costs, energy and repair saving also end
with the contract. Subsequent months remain unknown and cannot produce a full
budget pass; do not silently extend the car's use assumptions beyond coverage.

Included categories contribute zero *additional* cost with an included label;
prevent entry of the same service/insurance twice. Allow explicitly excluded
extras as distinct items. Lease payments never receive purchase depreciation,
purchase cash allocation, principal, interest, or resale equity. Lease price
and use assumptions remain quote-dependent; saving still requires registration.

<a id="partial-preview-and-future-http-contract"></a>

## Partial preview and HTTP contract

Issue #59 implements this boundary with API-owned DTOs and generated frontend
types. See the [implemented HTTP contracts](household-api.md) for exact envelopes,
review handling, status codes and examples. UI integration and practical whole-flow
acceptance remain #60-#61 work. Calculation/result versions stay 2 and storage
version stays 1; no migration is introduced by the HTTP layer.

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
invalid | notApplicable`, `knownSubtotalSek` (null only if the known sum cannot
be represented), nullable `completeTotalSek`,
`missingComponents`, and `errors` with stable code and JSON input path. Keep
known energy quantities separate from unknown money. Global errors invalidate
only dependent sections. A missing start month need not hide distance or cost
accrual. A result is cost-comparable only when acquisition and all applicable
cost sections are complete, residual/horizon is valid, and unresolved legacy
cost items do not remain. Missing payment timing affects cash/budget completeness
but does not by itself invalidate an otherwise complete accrued cost.

| Route | Semantics |
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
| `GET/PUT/DELETE /api/vehicle-draft` | Read/explicitly save/remove the shared singleton draft with its own expected revision; all return 200 with payload and slot revision. Empty content is null. DELETE takes the expected revision in the query. |
| `POST /api/vehicle-draft/adopt` | Atomically adopt draft into current registered data and consume it, checking draft and vehicle revisions; 200 current vehicle input. Read the draft again when its latest slot revision is needed. |

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
semantics. Both the store guard (#58) and its HTTP mapping (#59) are implemented.

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
