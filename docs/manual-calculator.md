# Manual calculator specification

## Status and purpose

This document is the decision-complete specification for the deterministic
manual vehicle cost calculator. The Core calculation, unsaved HTTP preview
contract, Swedish unsaved user interface, and PostgreSQL persistence layer are
implemented. Saved-scenario HTTP and UI management remain later work.

The calculation must work without persistence or external services. Core is the
source of truth and must not depend on HTTP, PostgreSQL, AI, marketplace data, or
locale-specific parsing.

## Calculation model

A manual calculation represents estimated cash outflow and, when a residual
value is supplied, estimated net ownership cost over a user-selected ownership
period.

- Currency is fixed to Swedish kronor (`SEK`).
- All money and calculations use decimal arithmetic.
- The calculation period is an integer from 1 through 120 months.
- Core and HTTP distances are expressed in kilometres. The Swedish UI may
  accept Swedish mil and must convert using exactly `1 mil = 10 km` before
  submitting the request.
- Results contain totals and normalized monthly and annual averages. Version 1
  does not return a monthly payment calendar or annual buckets.
- Prices, rates, use, and costs are constant throughout the period. Inflation,
  changing energy prices, variable interest rates, and discounted cash flow are
  outside this specification.

Cash outflow answers how much money leaves the household during the selected
period. Net ownership cost avoids counting repaid loan principal as an expense:
it combines depreciation, interest paid during the period, and operating costs.

## Core operation

Core exposes a pure operation equivalent to:

```text
Calculate(CostScenario) -> CostCalculationResult
```

`CostScenario` is the Core aggregate containing the calculation assumptions
defined by `ManualCalculationRequest`. It has no persistence identity. The
saved-scenario model associates identity and storage metadata without adding
them to this pure operation.

The operation has no clock, database, HTTP, or external-service dependency. The
HTTP request described below maps to the Core input without changing units or
calculation semantics, and `CostCalculationResult` maps to
`ManualCalculationResult`. API DTO names are normative for HTTP; Core type and
member names may follow Core conventions while preserving the same values,
nullability, units, formulas, and validation.

## Saved-scenario persistence

Saving remains optional and does not change the unsaved preview contract. The
persistence layer stores one current scenario per vehicle, while HTTP endpoints
for managing it are introduced separately.

- A saved vehicle uses UUIDv7 as its stable technical identifier and requires a
  normalized ordinary Swedish registration number as its unique current lookup.
- Registration numbers are immutable. Vehicle labels remain optional and are
  stored separately from the registration number.
- All calculation inputs are stored relationally. Ordered energy and custom-cost
  collections use child tables; the complete calculated result is retained in a
  versioned JSONB snapshot.
- Create and replacement always invoke this document's Core calculator. A caller
  cannot provide a trusted result snapshot.
- Replacement is a full atomic overwrite, increments an optimistic-concurrency
  revision, and physically removes superseded child data.
- Delete physically removes the vehicle, scenario, and all related children.
  There are no history, soft-delete, or automatic retention records.
- Persisted results carry calculation and result-schema versions. Formula changes
  do not silently rewrite an older result; a later replacement/recalculation
  writes the current versions.

## HTTP contract

The unsaved preview endpoint is:

```http
POST /api/manual-calculations
Content-Type: application/json
```

A valid request returns `200 OK` with `ManualCalculationResult`. The request is
never persisted and the response contains no database identifier or timestamp.
Invalid input returns `400 Bad Request` as ASP.NET Core
`ValidationProblemDetails` with `application/problem+json`. Validation errors
use JSON field paths as keys in the `errors` dictionary.

JSON field names and enum values use camel case. Decimal values are JSON
numbers, not localized strings. The API never accepts `kr`, spaces as grouping
separators, or comma decimal separators; those are frontend presentation
concerns.

### `ManualCalculationRequest`

| Field | Required | Unit/type | Range and meaning |
| --- | --- | --- | --- |
| `vehicleLabel` | No | string or `null` | Descriptive only; 1-120 characters after trimming when present. |
| `calculationPeriodMonths` | Yes | integer months | 1-120 inclusive. |
| `purchasePriceSek` | Yes | decimal SEK | 0-100,000,000 inclusive. |
| `expectedResidualValueSek` | No | decimal SEK or `null` | 0 through `purchasePriceSek`. `null` makes net ownership cost unavailable. |
| `annualDistanceKilometres` | Yes | decimal km/year | 0-1,000,000 inclusive. |
| `financing` | No | `FinancingInput` or `null` | `null` means the complete purchase price is paid in cash at the start. |
| `energySources` | Yes | array of `EnergySourceInput` | Zero to two entries, subject to the distance rules below. |
| `vehicleTax` | Yes | `RecurringCostInput` or `null` | `null` means unknown; explicit zero means known zero. |
| `insurance` | Yes | `RecurringCostInput` or `null` | `null` means unknown; explicit zero means known zero. |
| `maintenanceAndRepairs` | Yes | `RecurringCostInput` or `null` | Planned combined budget. `null` means unknown; explicit zero means known zero. |
| `otherRecurringCosts` | Yes | array of `NamedRecurringCostInput` | Zero to 50 user-named entries. An empty array means no additional recurring costs. |
| `otherOneTimeCosts` | Yes | array of `OneTimeCostInput` | Zero to 50 user-named entries. An empty array means no additional one-time costs. |

A zero purchase price supports an already owned or free vehicle scenario. Such a
scenario cannot contain financing and its residual value, when supplied, must
also be zero.

### `FinancingInput`

| Field | Required | Unit/type | Range and meaning |
| --- | --- | --- | --- |
| `downPaymentSek` | Yes | decimal SEK | 0 or greater and strictly less than `purchasePriceSek`. |
| `annualNominalInterestRatePercent` | Yes | decimal percent | 0-100 inclusive. `5` means five percent, not `0.05`. |
| `termMonths` | Yes | integer months | 1-120 inclusive. |

The financed principal is always
`purchasePriceSek - downPaymentSek`. A separate loan amount is not accepted, so
the purchase price, down payment, and principal cannot contradict one another.
The loan is a fully amortizing annuity loan with no balloon payment. Setup fees,
invoice fees, and similar known costs can be entered as custom one-time or
recurring costs.

The loan term may be shorter than, equal to, or longer than the calculation
period. Only payments made during the calculation period contribute to cash
outflow and interest cost. The result reports the outstanding principal at the
end of the period.

### `EnergySourceInput`

| Field | Required | Unit/type | Range and meaning |
| --- | --- | --- | --- |
| `label` | Yes | string | 1-120 characters after trimming, for example `Petrol` or `Home charging`. |
| `unit` | Yes | enum | `litre`, `kilowattHour`, or `kilogram`. |
| `consumptionPer100Kilometres` | Yes | decimal unit/100 km | Greater than 0 and at most 10,000. |
| `pricePerUnitSek` | Yes | decimal SEK/unit | 0-100,000 inclusive. |
| `distanceSharePercent` | Yes | decimal percent | Greater than 0 and at most 100. |

When `annualDistanceKilometres` is positive, one or two energy sources are
required and their shares must total exactly 100 percent using decimal
arithmetic. When annual distance is zero, the energy array may be empty. If
sources are nevertheless supplied, the same count, value, and 100-percent share
rules apply and their calculated quantities and costs are zero.

This model supports single-source combustion and electric vehicles as well as a
manual estimate for dual-source vehicles. The calculator does not infer hybrid
usage, convert between energy units, or look up prices.

### Recurring and one-time costs

`RecurringCostInput` contains:

| Field | Required | Unit/type | Range and meaning |
| --- | --- | --- | --- |
| `amountSek` | Yes | decimal SEK per cadence | 0-100,000,000 inclusive. |
| `cadence` | Yes | enum | `monthly` or `annual`. |

`NamedRecurringCostInput` adds a required `label` of 1-120 trimmed characters.
`OneTimeCostInput` contains a required label with the same limit and an
`amountSek` from 0 through 100,000,000. Each one-time cost is included once in
the selected period; version 1 does not assign it to a particular month.

Vehicle tax, insurance, and maintenance/repairs are standard categories because
omitting them materially affects a vehicle comparison. Other costs such as
parking, inspection, tyres, tolls, accessories, loan fees, and a known initial
repair are accepted through the custom collections.

## Missing values and completeness

Missing data is never guessed. Standard costs are nullable so that unknown and
known-zero values remain distinct:

- `null` vehicle tax, insurance, or maintenance/repairs is excluded from the
  known subtotal and marks cash flow as incomplete.
- An explicit cost object with `amountSek: 0` is known zero and does not make the
  result incomplete.
- Empty custom arrays mean the user has declared no additional costs; they are
  known zero rather than unknown.
- Missing residual value does not affect cash-flow completeness, but makes the
  net ownership cost object `null`.

The result exposes a top-level completeness object containing:

- `isComplete`: `true` only when all standard costs and residual value are
  present.
- `isCashFlowComplete`: `true` when vehicle tax, insurance, and
  maintenance/repairs are all present.
- `isNetOwnershipCostAvailable`: `true` when residual value is present.
- `missingCategories`: stable enum values selected from `vehicleTax`,
  `insurance`, `maintenanceAndRepairs`, and `residualValue`.

Cash-flow and net-cost aggregates use the name `knownTotalSek`. This prevents a
partial sum from being presented as an estimate that includes unknown costs.
The cash-flow breakdown also repeats `isComplete`. When available, the net-cost
breakdown repeats `isComplete`; it is false if any standard cost is unknown.

## Formulas

The symbols below refer to validated request values:

| Symbol | Meaning |
| --- | --- |
| `M` | Calculation period in months. |
| `K` | Annual distance in kilometres. |
| `P` | Purchase price in SEK. |
| `R` | Expected residual value in SEK, when supplied. |
| `D` | Down payment in SEK; `P` for a cash purchase. |
| `L` | Financed principal, `P - D`; zero for a cash purchase. |
| `n` | Loan term in months. |
| `q` | Annual nominal interest rate expressed as a percentage. |
| `i` | Monthly rate, `(q / 100) / 12`. |
| `h` | Payments occurring in the period, `min(M, n)`. |

### Distance and recurring costs

```text
totalDistanceKilometres = K * M / 12

monthlyRecurringTotal = amountSek * M
annualRecurringTotal  = amountSek * M / 12
```

Recurring costs accrue evenly for estimation purposes. The calculator does not
model the actual invoice date.

### Energy

For each source `s`:

```text
sourceDistanceKilometres =
    totalDistanceKilometres * distanceSharePercent / 100

sourceQuantity =
    sourceDistanceKilometres * consumptionPer100Kilometres / 100

sourceCostSek = sourceQuantity * pricePerUnitSek

energyCostSek = sum(sourceCostSek)
```

No conversion is performed between litres, kilowatt-hours, or kilograms.

### Annuity financing

For positive monthly interest:

```text
monthlyPaymentSek = L * i * (1 + i)^n / ((1 + i)^n - 1)

remainingPrincipalSek =
    L * (1 + i)^h
    - monthlyPaymentSek * (((1 + i)^h - 1) / i)
```

For zero interest, division by the rate is not attempted:

```text
monthlyPaymentSek = L / n
remainingPrincipalSek = L - monthlyPaymentSek * h
```

The remaining principal is zero when `h = n`. Derived financing values are:

```text
loanPaymentsDuringPeriodSek = monthlyPaymentSek * h
principalRepaidSek = L - remainingPrincipalSek
interestPaidSek = loanPaymentsDuringPeriodSek - principalRepaidSek
```

A financing object with `termMonths` equal to zero or outside 1-120 is invalid
and returns HTTP 400; it never reaches a division operation. When financing is
`null`, the financing breakdown is `null`, acquisition cash paid is `P`, and
all loan-derived values are treated as zero in aggregate formulas.

### Cash outflow and net ownership cost

Known operating cost is the sum of energy, present standard costs, custom
recurring costs, and custom one-time costs. Unknown standard categories
contribute nothing to this subtotal and are reported through completeness.

```text
acquisitionCashPaidSek = financing is null ? P : D

knownCashOutflowSek =
    acquisitionCashPaidSek
    + loanPaymentsDuringPeriodSek
    + knownOperatingCostSek
```

When residual value is present:

```text
depreciationSek = P - R

knownNetOwnershipCostSek =
    depreciationSek
    + interestPaidSek
    + knownOperatingCostSek

estimatedEquityAtPeriodEndSek =
    R - remainingPrincipalSek
```

Down payment and principal repayment are cash movements that acquire equity in
the vehicle. They are deliberately absent from net ownership cost, preventing
the principal from being counted both in cash payments and depreciation.

For any aggregate total `T`:

```text
averagePerMonthSek = T / M
averagePerYearSek  = T * 12 / M
```

## Rounding

All formulas are evaluated with decimal arithmetic and no display rounding is
fed into another calculation. At the response boundary:

- Money is rounded to two decimal places.
- Calculated distances, energy quantities, and other non-money decimal results
  are rounded to three decimal places.
- Midpoints are rounded away from zero.
- A mathematically repaid loan reports a remaining principal of exactly
  `0.00 SEK`; implementation precision must not produce negative zero.

The frontend may localize these response values for Swedish display but must not
recalculate totals from already rounded breakdown values.

## Result shape

`ManualCalculationResult` has the following normative JSON shape. A nullable
property is present with `null`; it is not silently omitted. Names, nesting, and
nullability form the HTTP contract.

```text
ManualCalculationResult
  currency: "SEK"
  calculationPeriodMonths: integer
  totalDistanceKilometres: decimal
  completeness:
    isComplete: boolean
    isCashFlowComplete: boolean
    isNetOwnershipCostAvailable: boolean
    missingCategories: MissingCategory[]
  cashFlow:
    acquisitionCashPaidSek: decimal
    loanPaymentsDuringPeriodSek: decimal
    energyCostSek: decimal
    vehicleTaxSek: decimal | null
    insuranceSek: decimal | null
    maintenanceAndRepairsSek: decimal | null
    otherRecurringCostSek: decimal
    otherOneTimeCostSek: decimal
    knownOperatingCostSek: decimal
    knownTotalSek: decimal
    averagePerMonthSek: decimal
    averagePerYearSek: decimal
    isComplete: boolean
  financing: FinancingResult | null
  energy:
    sources: EnergySourceResult[]
    totalCostSek: decimal
  otherRecurringCosts: RecurringCostResult[]
  otherOneTimeCosts: OneTimeCostResult[]
  netOwnershipCost: NetOwnershipCostResult | null

FinancingResult
  downPaymentSek: decimal
  principalSek: decimal
  annualNominalInterestRatePercent: decimal
  termMonths: integer
  monthlyPaymentSek: decimal
  paymentsMade: integer
  loanPaymentsDuringPeriodSek: decimal
  principalRepaidSek: decimal
  interestPaidSek: decimal
  remainingPrincipalSek: decimal

EnergySourceResult
  label: string
  unit: "litre" | "kilowattHour" | "kilogram"
  distanceSharePercent: decimal
  allocatedDistanceKilometres: decimal
  consumptionPer100Kilometres: decimal
  consumedQuantity: decimal
  pricePerUnitSek: decimal
  costSek: decimal

RecurringCostResult
  label: string
  amountSek: decimal
  cadence: "monthly" | "annual"
  costDuringPeriodSek: decimal

OneTimeCostResult
  label: string
  amountSek: decimal

NetOwnershipCostResult
  residualValueSek: decimal
  depreciationSek: decimal
  interestPaidSek: decimal
  knownOperatingCostSek: decimal
  knownTotalSek: decimal
  averagePerMonthSek: decimal
  averagePerYearSek: decimal
  estimatedEquityAtPeriodEndSek: decimal
  isComplete: boolean
```

`MissingCategory` is the closed string enum `vehicleTax`, `insurance`,
`maintenanceAndRepairs`, or `residualValue`. Standard recurring inputs are
represented by their period totals directly in `cashFlow`; custom breakdowns
preserve input order and labels.

All monetary result fields use SEK. All derived costs are non-negative except
`estimatedEquityAtPeriodEndSek`, which may be negative when outstanding
principal exceeds residual value. `totalDistanceKilometres` is between 0 and
10,000,000; allocated distances and consumed quantities are non-negative.
Aggregate output values are derived from validated inputs and may exceed the
100,000,000 SEK limit applied to each individual money input.

`paymentsMade` equals `min(calculationPeriodMonths, termMonths)`. The result does
not expose a future-payment schedule.

## Validation behavior

Validation occurs before calculation and accumulates field errors where
practical. In addition to the field ranges above, the following combinations
are invalid:

- Financing on a zero-price vehicle.
- A down payment equal to or greater than the purchase price.
- A residual value greater than the purchase price.
- Positive annual distance with no energy source.
- More than two energy sources, or more than 50 entries in either custom-cost
  collection.
- Energy shares that are non-positive or do not total exactly 100 percent.
- An energy source supplied without a nonblank label, supported unit, positive
  consumption, or valid price.
- A custom cost supplied without a nonblank label, amount, or supported cadence.
- Negative numbers, non-finite JSON numbers, and values outside their documented
  limits.

Duplicate custom labels are allowed and input order is preserved. Labels are
trimmed before use. The API does not parse localized numeric text.

## Worked example

Consider a twelve-month scenario with all standard costs known:

| Input | Value |
| --- | ---: |
| Purchase price | SEK 20,000 |
| Down payment | SEK 5,000 |
| Loan | 12 months at 0 percent |
| Residual value after 12 months | SEK 15,000 |
| Annual distance | 15,000 km (1,500 Swedish mil) |
| Petrol share | 100 percent |
| Petrol consumption | 8 litres/100 km |
| Petrol price | SEK 20/litre |
| Vehicle tax | SEK 2,400/year |
| Insurance | SEK 500/month |
| Maintenance and repairs | SEK 6,000/year |
| Other recurring: parking | SEK 300/month |
| Other one-time: initial repair | SEK 2,000 |

Distance and energy:

```text
total distance = 15,000 * 12 / 12 = 15,000 km
petrol quantity = 15,000 * 100% * 8 / 100 = 1,200 litres
energy cost = 1,200 * 20 = SEK 24,000
```

Financing:

```text
principal = 20,000 - 5,000 = SEK 15,000
monthly payment at zero interest = 15,000 / 12 = SEK 1,250
payments during period = 1,250 * 12 = SEK 15,000
principal repaid = SEK 15,000
interest paid = SEK 0
remaining principal = SEK 0
```

Operating costs:

```text
energy                         SEK 24,000
vehicle tax                    SEK  2,400
insurance                      SEK  6,000
maintenance and repairs        SEK  6,000
parking                        SEK  3,600
initial repair                 SEK  2,000
known operating cost           SEK 44,000
```

Final results:

```text
cash outflow = 5,000 + 15,000 + 44,000 = SEK 64,000
cash outflow average per month              = SEK 5,333.33
cash outflow normalized per year            = SEK 64,000.00

depreciation = 20,000 - 15,000              = SEK 5,000
net ownership cost = 5,000 + 0 + 44,000     = SEK 49,000
net cost average per month                  = SEK 4,083.33
net cost normalized per year                = SEK 49,000.00
estimated equity at period end              = SEK 15,000.00
```

The result is complete because tax, insurance, maintenance/repairs, and residual
value are all explicit.

## Required calculation scenarios

Core and API tests derive their expectations from this specification. Future
frontend tests must use the same scenarios and cover at least:

| Scenario | Required behavior |
| --- | --- |
| Cash purchase | Financing is `null`; purchase price is acquisition cash, and financing output is `null`. |
| Zero-interest loan | Principal is divided by a valid term without division by zero or interest cost. |
| Interest-bearing loan | Nominal annual percent is divided by 100 and 12 before applying the annuity formulas. |
| Horizon shorter than loan term | Only payments within the horizon are cash outflow; remaining principal and period interest are reported. |
| Horizon longer than loan term | Payments stop at the term and remaining principal is zero. |
| Two energy sources | Each distance share is applied independently and the quantities and costs sum to the energy total. |
| Zero annual distance | An empty energy collection is accepted and energy cost is known zero. |
| Partial year | Annual costs and distance are multiplied by `M / 12`; monthly costs are multiplied by `M`. |
| Missing standard costs | Known totals are returned with null category values, missing codes, and incomplete flags. |
| Missing residual value | Cash flow is returned and can be complete; net ownership cost is `null`. |
| Rounding boundary | Full-precision calculations are aggregated before the defined response rounding. |
| Validation failures | Invalid term, range, share total, count, label, cadence, and cross-field combinations return HTTP 400. |

## Legacy behavior decisions

The legacy source remains available through the `legacy-console` Git tag. The
new design does not preserve legacy behavior merely for compatibility.

| Legacy field or behavior | Decision | Rationale |
| --- | --- | --- |
| `CarInstance` groups `Car` and `Loan` | Correct | Replace the mutable container with an immutable calculation input/result boundary. |
| `Car.Name` | Retain | It becomes optional descriptive `vehicleLabel` and never affects formulas. |
| `Car.Price` as `double` | Correct | Retain purchase price but use decimal SEK with explicit range and rounding. |
| `Car.Mileage` | Reject from this calculation | Current odometer mileage does not affect any defined formula; it belongs to future `Vehicle` metadata and may inform later estimates. |
| `Car.KilometerPerLiter` | Correct | Use consumption per 100 km with an explicit energy unit, supporting electricity and kg-based fuels without false litre conversions. |
| `Car.Taxes` | Retain and correct | Keep vehicle tax as a nullable recurring decimal SEK cost with explicit cadence. |
| `Car.FuelType` and `SecondaryFuelType` | Correct | Replace fixed objects with one or two request energy sources and explicit distance shares. |
| `Fuel.Name` | Retain and correct | Keep a user-provided display label; calculation behavior comes from the typed unit and numeric inputs. |
| `Fuel.PricePerLiter` | Correct | Replace it with decimal price per declared litre, kWh, or kg. |
| `Fuel.Eco` | Reject | It has no deterministic cost meaning and is not a substitute for emissions data. |
| `Loan.Amount` as an independent integer | Reject and replace | Derive decimal principal from purchase price and down payment to prevent contradictory financing inputs. |
| `Loan.InterestRate` | Correct | Define it as nominal annual percentage where `5` means 5 percent, then divide by 100 and 12. |
| `Loan.Months` | Retain and correct | Keep an integer term with a 1-120 range and explicit behavior relative to the calculation horizon. |
| Zero-interest branch | Retain and correct | Preserve principal divided by term, but validate the term before calculation. |
| Hard-coded fuel catalogue and prices | Reject | Prices become scenario inputs; no value is guessed or treated as current. |
| Hybrid yes/no flag | Reject | The presence of two energy sources expresses a dual-source estimate without separate formulas. |
| Fixed 10-year horizon | Reject | The user selects 1-120 months. |
| Fixed 10,000 km/year | Reject | Annual distance is a required scenario input in kilometres. |
| Fuel cost based on distance divided by km/litre | Correct | Use consumption per 100 km, an allocated distance share, and price per typed unit. |
| Full lifetime loan cost shown at every yearly horizon | Reject | Include only payments and interest occurring within the selected period and report the outstanding principal. |
| Purchase price minus independent loan amount plus total loan cost | Correct | Separate cash outflow from net ownership cost and prevent principal from being counted twice. |
| Cumulative output for years 1-10 | Reject | Return one selected-period total plus normalized monthly and annual averages. |
| In-memory console save prompt | Reject from calculation | Unsaved calculation is pure; persistent saved scenarios are defined in later issues. |

## Explicit exclusions

Version 1 calculates depreciation only from the purchase price and a
user-supplied residual value. It does not infer the residual value, insurance,
maintenance, tax, energy prices, or driving mix; the user supplies those values
or the result marks them missing. The specification excludes loan balloons,
straight-line amortization, irregular payment schedules, tax deductions,
leasing, currency conversion, inflation, present-value calculations, automatic
vehicle data, persistence, AI review, and image analysis.
