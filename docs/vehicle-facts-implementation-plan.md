# Issue #62: current vehicle facts and evidence

## Readiness and scope

Implementation record for [#62](https://github.com/Extender92/CarExpenseCalculator/issues/62),
the first implementation item in stage 3B. The Core foundation described below
was implemented and tested on `feature/62-vehicle-facts-provenance`, then merged
through approved [PR #80](https://github.com/Extender92/CarExpenseCalculator/pull/80)
as `60d2b49ab910ba17352637cc94b6649df06b117b`. #62 is closed and
[merged-main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34118937565)
is green. Rules and scores remain future work; see the
[#63 preparation audit](buying-rules-implementation-preparation.md).

Dependency audit on 2026-09-07:

- Planning [PR #54](https://github.com/Extender92/CarExpenseCalculator/pull/54)
  is merged as `62cd19aaab1c1c59fdeca4feb3519c5df01d48e6`.
- Stage acceptance #61 is delivered by approved
  [PR #78](https://github.com/Extender92/CarExpenseCalculator/pull/78), merged as
  `15b281f793e2f51d4a01500ced32e9de2d0d411d`.
- All #55–#61 implementation issues are closed.
  [Merged-main CI](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34111101486)
  passed all ordinary checks. The baseline is 699 backend, 195 frontend and
  33 Chromium tests, with no failed/skipped tests.
- The normative [comparison specification](comparison-and-buying-scores.md)
  and [verification matrix](household-comparison-verification.md) are on `main`.
  There are no unresolved product decisions or provider dependencies for #62.

The user assigned implementation after preparation PR #79 was merged as
`9994f7f8ae2d7330f6dcaa18146c6466f1fb0e9d`. The completed implementation branch
was `feature/62-vehicle-facts-provenance`; it was deleted locally and remotely
after the approved merge and verification of the clean `main` worktree.

The scope is dependency-free Core facts, normalization, validation, explicit
mapping and tests. Rules and scores belong to #63; persistence/API to #64;
UI to #65; PDF to #66; complete-flow acceptance to #67. No extraction schema,
HTTP endpoint, database migration, provider access or AI call is added by #62.
Household calculation/result version 2 and storage version 1 are unchanged.

## Existing implementation to reuse

- [ListingDraft](../src/backend/CarExpenseCalculator.Core/Listings/ListingDraft.cs)
  already carries asking price, decimal odometer km, owner count, tow bar,
  transmission, model year, fuel types, body, drive, locality/county and
  inspection dates.
- [Listing enums](../src/backend/CarExpenseCalculator.Core/Listings/ListingEnums.cs)
  define normalized categories, origin and verification levels. Reuse their
  meanings rather than creating competing gearbox/fuel/drive vocabularies.
- [Listing values](../src/backend/CarExpenseCalculator.Core/Listings/ListingValues.cs)
  hold field provenance and immutable sourced collections. The existing
  provenance requires a listing URL and has no observation/confirmation time;
  a comparison envelope must support manual facts without inventing a URL or
  time. Keep the current listing/extraction wire representation compatible.
- [ListingDraftProcessor](../src/backend/CarExpenseCalculator.Core/Listings/ListingDraftProcessor.cs)
  enforces normalization, bounds and the allowed advertised/manual provenance
  combinations. Its public review path rejects registry-verification claims.
- [RegistrationNumber](../src/backend/CarExpenseCalculator.Core/Vehicles/RegistrationNumber.cs)
  remains the normalized registration identity; the existing vehicle UUID is
  retained. Facts do not create a parallel vehicle identity.
- [Listing processor tests](../tests/backend/CarExpenseCalculator.Core.UnitTests/ListingDraftProcessorTests.cs)
  provide regression coverage for existing normalization, source boundaries,
  numeric limits and independent location fields.

## Implemented Core foundation

The following agreed requirements are implemented by `VehicleComparisonFacts`,
`VehicleFact<T>`, `ComparisonEvidence`, `ComparisonCriterionCatalog`,
`VehicleFactsProcessor`, and `SwedishMil` in `Core.Comparisons`. The
[normative Core contract](comparison-and-buying-scores.md#implemented-core-facts-62)
documents operations, field paths, evidence boundaries and exact-conversion
errors. The requirement list is retained for traceability:

1. Add a `Comparisons` area for immutable current vehicle facts and typed
   evidence. Represent known, unknown, not applicable and conflicting current
   evidence explicitly; a known boolean false must remain known. Copy incoming
   collections so later caller edits cannot mutate the normalized facts.
2. Implement the complete source-fact catalogue below with stable field keys,
   units and field-addressable validation errors. Keep invalid supplied values
   distinct from missing values. Cost-derived criteria are documented as
   outputs of the existing 3A engine; callers cannot populate trusted cost
   scores through the source-fact model.
3. Map supported reviewed listing values explicitly into the comparison model,
   preserving their actual origin, verification and source reference. Missing
   observation dates remain unknown. Plain manual inputs require no fabricated
   advertisement or registry reference. Explicit user confirmation is distinct
   from merely importing an advertisement.
4. Bind evidence to the current value. Changing a sourced value invalidates
   the previous value's verification; an explicit manual replacement can be
   user-confirmed but cannot become registry-verified. Unresolved conflicting
   sources remain visible until an explicit resolution. No historical snapshot
   collection or automatic source precedence is introduced.
5. Add documented mapping exclusions for fields that are not semantically
   equivalent. In particular, a last/next inspection date must not silently
   become `inspectionValidThrough`; free-text equipment or condition claims
   must not become typed service, seat or towing facts. New facts remain manual
   until an explicit, supported source exists.
6. Document the implemented types and operations in the normative comparison
   specification, then verify the focused Core tests and full backend baseline.

| Fact group | Representation and existing/accepted boundaries |
| --- | --- |
| Purchase asking price | Decimal SEK, 0–100,000,000. A lease without a purchase price is not applicable; it is not a zero-price purchase. |
| Odometer | Decimal km, 0–10,000,000. Swedish mil conversion is exact division/multiplication by 10, without floating point or display rounding. |
| Owners | Integer 0–10,000, retaining the existing listing contract. Unknown is not zero. |
| Tow bar | Known true/false or an explicit non-known state. Do not infer from equipment text. |
| Gearbox, fuel, body and drive | Existing Core enums and normalized fuel sets; every supported fuel is treated equally. |
| Model year | Integer 1886–2100. Never infer from registration or first registration date. |
| Locality and county | Separate normalized text values, existing 100-character bound, with independent evidence. No geocoding or inferred county. |
| Seats | Integer 1–100, explicitly supplied. |
| Towing capacity | Braked trailer capacity, integer kg, 0–100,000. Never substitute unbraked capacity. |
| Inspection validity | Explicit `DateOnly` value with evidence. Retain supplied dates; expiry evaluation uses explicit `asOfDate` in #63, not the system clock. |
| Service documentation | Documented, partial or absent; unknown remains separate. Optional explicit last-service date and odometer use date/odometer types and bounds. Notes retain the specification's 1,000-character bound. No inferred service intervals. |
| Derived cost and budget criteria | Document applicability and the existing 3A source. No editable duplicate totals, budget verdicts or score calculations in #62. |

## Scoring decisions carried into the next issue

These are already normative and were reaffirmed by the user. They are inputs
to the design boundary, not additional implementation scope for #62:

- Every car uses the same active criteria, target levels and weights (0–5).
  Weight zero disables a preference for the entire comparison.
- Missing or insufficient evidence leaves a possible score interval and
  weighted coverage. It is not a measured zero and does not remove the
  criterion from only that car's denominator.
- Adding/removing a car does not change another car's criterion scores.
- Hard failures and unverified requirements cannot be overridden by points.
- Cost ordering and preference ordering have separate meanings. Incomplete
  costs cannot win complete-cost recommendations; overlapping score intervals
  cannot establish a certain preference winner.
- Editing priorities previews dynamically; only explicit save persists them.

## Verification and delivery

Local implementation verification on 2026-09-07 used .NET SDK 10.0.400 and
Docker 29.5.3 with disposable PostgreSQL 18 Testcontainers. Restore and Release
build passed with zero build warnings/errors. All **760 backend tests** passed:
437 Core (61 new), 4 architecture, 16 Infrastructure unit, 39 extractor unit,
96 Infrastructure integration and 168 API integration; none failed or skipped.
The first focused `--no-restore` invocation encountered absent assets after
prior cleanup (`NETSDK1004`); restoring the solution resolved it before tests
ran. No test assertion failed or required a rerun. Ordinary PR CI additionally
verifies the unchanged 195 frontend and 33 Chromium regressions, generated
OpenAPI and isolated fake-extractor/Docker behavior. The public API and
extraction schema receive no intended changes. Live providers, Unraid data and
stage 3B UI/PDF acceptance are outside this Core delivery.

Automate observable Core behavior: every field's valid boundaries and invalid
values; null/false/zero/not-applicable/conflict distinctions; exact km/mil;
immutable collections; reviewed/manual mapping; preserved source metadata;
missing observation times; invalidated evidence after edits; rejected manual
registry claims; and no inferred inspection, service or geography facts.
Keep existing listing, registration, v1 and household regressions intact.

During implementation, run the focused Core project first as appropriate:

```bash
dotnet test tests/backend/CarExpenseCalculator.Core.UnitTests/CarExpenseCalculator.Core.UnitTests.csproj --configuration Release
```

Before publishing the implementation PR, use the documented repository checks
with Docker running for PostgreSQL 18 integration tests:

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
```

Ordinary CI must still pass frontend, OpenAPI and Docker/browser checks. #62
does not change public HTTP shapes, so generated OpenAPI must remain unchanged.
Review the complete diff, links and temporary artifacts. Helpers belong in
ignored `temp/issue62/` and are removed before delivery; use no production data.
Commit/push a focused implementation PR with `Closes #62` only when its
acceptance criteria are met. Merge requires separate user approval. This delivery
does not start #63 or close the stage 3B milestone.
