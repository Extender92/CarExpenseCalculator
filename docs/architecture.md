# Architecture

## System shape

Car Expense Calculator is a modular monolith in a monorepo:

```text
Browser
  -> React + Nginx
       -> /api/*
            -> ASP.NET Core API
                 -> Core domain and rules
                 -> Infrastructure adapters
                      -> PostgreSQL
                      -> internal Codex extraction sidecar
                           -> hosted Codex web search
                      -> future registry/listing providers
                      -> future advisory OpenAI review
```

The production browser sees one HTTP origin. Nginx serves the React build and proxies `/api` to the internal API service, avoiding a public API port and cross-origin configuration.

## Backend boundaries

- **Api** owns HTTP contracts, OpenAPI, health endpoints, configuration, and dependency injection.
- **Core** contains domain types, calculations, and deterministic rules without database, HTTP, or AI dependencies.
- **Infrastructure** implements PostgreSQL persistence and external-service adapters, including the private Codex extractor client.
- **Extraction.Contracts** contains the dependency-free internal sidecar protocol and is shared only by Infrastructure and the sidecar.
- **CodexExtractor** owns authenticated `codex exec` orchestration, strict extraction-schema validation, source-event parsing, and its private HTTP endpoints.
- Dependency direction is `Api -> Core`, `Api -> Infrastructure`, and `Infrastructure -> Core`.

## Frontend boundaries

- React Router owns the dashboard and the three usage-mode routes.
- A small OpenAPI-typed client owns same-origin API calls. The URL-analysis
  workspace keeps independent reviewed drafts in React memory and uses a FIFO
  browser scheduler capped at two extraction requests.
- Tailwind CSS defines design tokens and shadcn/ui provides accessible component patterns.
- User-visible copy is Swedish.

The [household workspace](household-workspace.md) owns `/manual`, with v1 at
`/manual/legacy` and atomic legacy review at `/manual/transition`. A provider
above lazy routes keeps saved baselines/revisions separate from dirty profile,
vehicle and transition editing. `lossless-json` preserves household JSON numbers;
forms use exact numeric text and BigInt-based display/unit conversion. Preview
generations debounce for 500 ms, cap detail reads at four, and cap calculation
requests at two with both 100-candidate and 2-MiB UTF-8 batching. Old responses
cannot replace newer generations or deleted vehicles. Result/budget authority
remains entirely in the API/Core. Only explicit user actions write data.

## Domain concepts

The implementation is introduced incrementally as each feature milestone begins:

- `Vehicle`: stable UUIDv7 technical identity with an immutable, normalized ordinary Swedish registration number. The persistence foundation currently stores its optional display label; specifications are added with later vehicle-data milestones.
- `Listing`: a current bounded structured listing draft with field-level provenance, source URLs, advertised facts, history signals, and explicit missing values. Complete descriptions and seller contact data are excluded.
- `RegistrySnapshot`: time-stamped verified vehicle and ownership facts.
- `SearchProfile`: user-defined hard requirements and preferences.
- `RuleEvaluation`: explainable results tied to a rule version and data sources.
- `CostScenario`: implemented dependency-free financing, use, energy, tax, maintenance, validation, and calculation assumptions. A vehicle may currently own one persisted current scenario.
- `AiReview`: structured advisory observations that never override deterministic results.

## URL-analysis flow

The implemented `POST /api/listing-analyses` endpoint accepts one URL per
request. The browser interface submits separate requests through a FIFO
scheduler limited to two concurrent requests. The API normalizes the URL through Core and
calls an application-owned Infrastructure adapter. That adapter uses a typed
internal HTTP client to a private ASP.NET Core `codex-extractor`
sidecar. The sidecar runs one ChatGPT-authenticated `codex exec` turn with
host-restricted hosted web search; neither the browser nor application services
fetch the listing page directly.

The implemented sidecar has no published port, database credentials, repository
mount, or application-source mount. Codex output is untrusted ingestion input.
Only completed `open_page` and `find_in_page` events with concrete URLs provide
source evidence, while Core owns source matching,
normalization, validation, provenance, missing-field codes, and analysis status.
Extracted facts remain unverified until the user changes them, at which point
the complete edited value becomes manually entered and user-confirmed. Advisory
AI review is a separate milestone and never shares authority with extraction.

The complete feature contracts and implemented runtime boundary are defined in the
[URL analysis specification](url-analysis.md) and
[Codex listing extraction](codex-extraction.md). The fake-only acceptance
boundary and reproducible deployment checks are documented in
[URL analysis verification](url-analysis-verification.md).

## Persistence

PostgreSQL 18 is the permanent database. The migrations store one current vehicle aggregate with optional scenario and listing records:

- `vehicles` owns the UUIDv7 identity, unique normalized registration number, optional label, timestamps, and optimistic-concurrency revision.
- `saved_cost_scenarios` has a unique vehicle relationship and stores scalar inputs, calculation/result schema versions, an optional source-listing version, the calculation timestamp, and a persistence-owned JSONB result snapshot.
- Energy sources, custom recurring costs, and custom one-time costs use ordered child tables with foreign keys and cascade deletion.
- `vehicle_listings` has a unique optional vehicle relationship and stores current typed listing scalars, listing/extraction versions, normalized status and missing codes, timestamps, and bounded JSONB values.
- `listing_sources` and `listing_equipment` preserve normalized order in child rows. Fuel types use a nullable string array. Energy consumption, seller claims, condition notes, and field provenance use bounded persistence-owned JSONB. Raw Codex output is never stored.
- `household_state` holds a nullable shared profile, its revision and a separate legacy-transition revision. Its seeded singleton contains no financial defaults.
- `vehicle_cost_inputs` extends the same vehicle UUID with one current purchase/lease payload and unresolved current legacy review items, plus the reviewed listing version. Storage-owned JSONB DTOs preserve decimal input precision and have storage version 1; household calculation/result versions remain 2.
- `vehicle_draft` holds one registered cost/listing draft, its original vehicle UUID/revision when applicable, and independent slot revision. Empty slots retain revision metadata, with no automatic expiry.

The user-facing registration number is a current natural key, not the database primary key. Transportstyrelsen stopped future number reuse in 2024 because historical reuse could associate the same registration number with different vehicle individuals. Personal plate text is not accepted as vehicle identity. See [registration-number reuse](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/ateranvandning-av-registreringsnummer-upphor/) and [ordinary formats](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/nu-har-de-nya-registreringsnumrena-lanserats/).

There is no append-only history in the current model. Scenario replacement validates and recalculates through Core. Listing replacement normalizes through `ListingUrl` and `ListingDraftProcessor`, then replaces every scalar, JSONB value, source row, and equipment row. Each operation is atomic, rejects a stale aggregate revision, and retains no superseded values. Deleting either saved resource physically cascade-deletes the complete aggregate.

Migrations are applied only through the explicit backend `migrate [target]` command. Normal API startup never creates, migrates, or rolls back schema.

URL-analysis persistence extends this aggregate without adding history. A vehicle may
be listing-only, scenario-only, or contain both current records. One current
`vehicle_listings` row owns typed listing values plus bounded JSONB, with
ordered source and equipment children. The aggregate revision changes after
any write; a separate listing version changes only when listing content changes.
Saved scenarios sourced from a listing record that listing version. A later
listing replacement marks, but never silently recalculates, the stored
calculation. Manual-only scenarios have no source version and are never marked
outdated. Deleting a saved listing permanently deletes the complete vehicle
aggregate, including any saved scenario.

Advertised geography is represented by separate nullable `locality` and
`county` sourced values. Each has independent provenance. The domain does not
retain a general location value or street address and does not infer counties
or resolve geographic data. Listing persistence uses separate typed nullable
columns for these current facts.

## Household calculations and comparison

Stages 3A and 3B are defined in the
[Household calculations and comparison plan](household-comparison-plan.md),
[household contract](household-calculations.md), and
[comparison contract](comparison-and-buying-scores.md).
They extend the implemented system. Core now contains the shared-input and
purchase-financing foundation described in the
[Core financing contract](household-calculations.md#implemented-core-financing-foundation).
It composes immutable household assumptions and car purchase inputs, with
independent validation/missing results and unrounded decimal installments.
The [Core ownership-cost engine](household-calculations.md#implemented-core-ownership-costs)
composes that financing with independent depreciation, energy and operating
sections. Internal amounts retain decimal precision; result sections round
only after aggregation. Exact integer comparisons protect fractional-year
depreciation from decimal underflow without introducing external packages.
Infrastructure implements the shared profile, purchase/lease inputs, explicit
legacy transition and shared draft. The
[storage contract](household-calculations.md#implemented-household-persistence)
defines the four store interfaces and typed conflict outcomes. The
[household HTTP layer](household-api.md) exposes these stores and Core previews
through API-owned DTOs and generated frontend types (#59). The Swedish workspace
(#60) is implemented. [Stage acceptance evidence](household-stage-3a-verification-report.md)
for #61 covers the complete flow. Comparison persistence is delivered through #64.
No new household result cache or historical tables are introduced.

Issue #62, merged through PR #80, adds the dependency-free `Core.Comparisons` fact foundation. Its
`VehicleComparisonFacts` value object belongs to the existing vehicle aggregate;
it does not introduce another UUID/registration identity. `VehicleFact<T>` keeps
known, unknown, not-applicable and conflicting current observations distinct.
Supported values and copied collections are immutable. `ComparisonEvidence`
extends the listing semantics with nullable source URL and explicit observation/
confirmation times without changing listing or extraction contracts.
`VehicleFactsProcessor` validates supplied facts and maps reviewed listings
through the existing source boundary. Manual replacement discards previous
verification; client registry claims are rejected. The criterion catalogue
identifies 3A cost/budget sources without accepting duplicate calculated values.
See the [implemented Core contract](comparison-and-buying-scores.md#implemented-core-facts-62).
This foundation is on `main` with green CI. Issue #63, merged through PR #82,
implements `ComparisonEvaluator`. `RuleProfileProcessor` validates typed operators, common anchors/weights
and evidence requirements. Internal fact normalization preserves independent
errors while public saving stays strict. One household calculation path supplies
unchanged rounded version-2 output and internal unrounded cost measures for
comparison; budget authority remains in that engine. Core returns immutable
hard results, score intervals/coverage, Swedish explanations/signals and separate
cost/score order lists using full precision and registration ties.

Effective purchase price comes from current cost inputs when present, without
silently filling gaps from the advertisement. A `CostAssumptionConfirmation`
binds explicit adoption to exact immutable input values and caller-provided
time, not a transferable verification flag. Typed review impacts block dependent
complete costs/budget passes while preserving known parts and safe exceedance.
#64 maps stored reviews and current value confirmations through trusted
application boundaries; Core does not reference its storage/HTTP DTOs. Rule and
comparison-result versions are 1, separate from household/storage versions.
See the [implemented evaluation contract](comparison-and-buying-scores.md#implemented-core-evaluation-63).
Issue #64 delivered [comparison persistence and HTTP](comparison-api.md) through
approved PR #83. Infrastructure
owns typed version-1 JSONB in `rule_profile` and `vehicle_comparison_facts` and
uses the existing household transaction lock and vehicle identity/revision.
One RepeatableRead snapshot backs stored comparison revision checks. Manual
comparison resolves no store. API-owned typed actions use Core manual/listing/
conflict operations; clients cannot assert evidence or calculated outcomes.
Per-observation listing versions remain separate from the reviewed version.
The shared cost writer invalidates input confirmations through Core structural
equality, including draft adoption. Both shared profiles survive car deletion.
UI (#65), PDF (#66) and acceptance (#67) are separate deliveries. Approved PR #87
delivers the [comparison workspace](comparison-workspace.md) for #65 and enables the
existing `RuleBasedSearch` availability status. PDF (#66) is merged through
approved PR #89; stage acceptance remains #67. Discovery/AI are not enabled.
The [workspace preparation](comparison-workspace-preparation.md) identifies
reusable frontend state and the per-request boundary of server ordering.
The [#85 extension](comparison-api.md#complete-set-comparison-85), implemented
and merged through PR #86, adds complete-set server reads and global
evaluation before #65. Infrastructure hashes a thin invariant revision manifest
and reads full inputs in groups of 100 within the same RepeatableRead snapshot.
The transaction closes before computation. Core retains raw decimals across
groups and shares final ordering/upper-bound winner summaries with the original
entry point. The API applies explicit actions once, then calculates three modes
from that captured input. Manual mode resolves no store. No sessions/history
or migration are introduced.

API middleware bounds incoming memory with `COMPARISON_MAX_REQUEST_BYTES`
(32 MiB by default), updates Kestrel's limit before reading, and admits two
complete previews with a 120-second deadline and no queue. The frontend image's
Nginx template uses the same setting, HTTP/1.1, disabled request/response
buffering and 150-second timeouts on `/preview-all`; other routes keep 2 MiB.
Candidate count and output size are not transport limits. #65 must publish
only a completely received, current three-view generation.

The comparison provider lives above page routes alongside the existing household
provider. Household state alone owns the effective shared profile and stored
economic editor; comparison owns rules, per-car fact actions, local date, manual
candidates and results. Baseline loading does not start household previews.
An explicit write notification and shared vehicle-write guard coordinate revisions
between the two editors without erasing later changes. Refreshed fact/conflict
references need explicit review before rebasing. Own writes also verify that no
unacknowledged concurrent inventory change is hidden in the next response.
Complete preview transport retains exact numbers with the existing lossless JSON
adapter, two active requests at most, latest-only queued work, generation checks
and four bounded lazy fact reads. The main and seven detail tables consume API
orders in shared 50-car pages; stale outcomes suppress every recommendation.
Manual candidate economics reuse household fields in a memory-only route context.

The [#66 PDF report](comparison-pdf.md) implements frontend-owned
`ComparisonReportInput`: an exact, recursively frozen copy of one accepted
complete response and selected order, with separate report/evaluation times and
timezone. The comparison workspace owns one capture; `/search/report` sits
under the providers but outside the navigation layout. Both workspaces pause
automatic focus reads/calculations there. Returning releases the capture and
resumes refresh without resetting editing, page, order or open sections.
Existing whole-car deletion events invalidate a report containing that identity.

Pure report formatters share exact numbers, money, score and budget labels with
the screen. A memoized print document consumes the capture, not live editors,
lazy reads or paginated DOM. All cars, three sensitivity views, assumptions,
sources, partial-result reasons and dirty flags come from the HTTP response.
Native printing waits for rendering and used fonts; cancellation is not saving.
No server renderer, additional API, schema change, runtime package or report
history is introduced. Implementation is merged through approved PR #89. The
[#67 preparation](comparison-stage-3b-preparation.md) maps implemented contracts
to whole-stage tests and practical evidence. The
[execution report](comparison-stage-3b-verification-report.md) now records the
joined browser lifecycles and fresh PDF/native-print checks on the acceptance
branch. No production layers, endpoints, types or migrations changed; separate
acceptance merge approval remains.

Core also implements explicit lease contracts, bounded payment calendars,
cash/cost reconciliation and separate startup/average-month funding checks.
Purchase and lease candidates share the same profile; lease coverage limits
known calculations without changing the requested household horizon. Internal
decimal components feed costs and payments before display rounding. The
[payment contract](household-calculations.md#implemented-core-leasing-and-payments)
defines partial calendars, deposits, repair saving and budget evidence.

The household profile owns common driving and financing assumptions,
purchase cash, energy prices, and separate startup/ongoing budget limits.
Current vehicle facts remain car-specific and registration-based. Core
composes these inputs into deterministic purchase-cost results without HTTP,
database, clock, or AI dependencies; calculation dates are explicit inputs.

Profile edits refresh derived previews across candidates, with explicit save,
independent profile/vehicle revisions, and one response generation across all
tables. Listing facts retain the implemented explicit review boundary.
Profile edits do not authorize automatic adoption of unreviewed listing facts
or background AI calls.

Retain current data only: one shared profile and one current vehicle input set,
with version/revision metadata for compatibility and concurrency. Sensitivity
views and changed buying weights do not create saved scenario alternatives or
evaluation history. Calculation upgrades need a defined way to recover current
inputs and replace current derived results without archival snapshots.

One shared registration-linked draft has explicit save, its own revision, no
automatic expiry, and atomic adoption; opening it does not consume it.
Saved vehicles continue to require registration numbers. Deleting a vehicle
removes its associated inputs, listing, derived results, and any associated
draft, while retaining the shared household and rule profiles.
All vehicle/profile/draft/transition writers, including v1 scenario and listing
stores, first take a transaction row lock on `household_state`, then check fresh
revisions. Multi-query input and transition reads use repeatable-read snapshots.
Draft adoption updates the supplied aggregate parts and consumes the slot in
one transaction, with one vehicle revision increase. Legacy inputs remain
accessible independently of result version/deserialization until an explicit
all-vehicle transition confirmation replaces them atomically. Current review
items identify affected calculation sections; the API blocks dependent complete
totals and budget passes while preserving known contributions. No result or input archive
is introduced; old results are removed on confirmation. Migrations remain an
explicit command, with [documented rollback](deployment-unraid.md#household-storage-migration-and-rollback).

## Public foundation API

- `GET /api/health/live` checks process liveness only.
- `GET /api/health/ready` checks PostgreSQL readiness.
- `GET /api/system/status` returns application version, overall state, database state, and feature availability.
- `POST /api/manual-calculations` returns an unsaved deterministic preview without accessing PostgreSQL.
- `/api/saved-cost-scenarios` exposes create, summary list, UUID/registration lookup, full replacement, and permanent deletion over the current saved aggregate.

Saved-scenario writes use explicit optimistic-concurrency revisions. Duplicate
registration numbers and stale writes return typed conflicts instead of
silently overwriting current data. API DTOs remain separate from Core and
persistence types, and stored result snapshots are never accepted from clients.

The URL-analysis API exposes its unsaved preview endpoint and the complete
current saved-listing lifecycle. Create, list, UUID/registration reads, full
replacement, and permanent aggregate deletion delegate to the current-listing
store. Preview analysis never accesses PostgreSQL, and saved reads/writes never
invoke Codex or the extractor. System status reports whether the Codex extractor is configured
without starting a search turn. Overall health remains database-based and URL
analysis is enabled because its complete unsaved Swedish interface and manual
fallback exist. The Swedish interface also exposes current-listing management,
including explicit field-by-field duplicate comparison and optimistic-concurrency
recovery. Saved listings can open the manual calculator through a reload-safe
vehicle UUID query, and the UI requires explicit review before linking a saved
scenario to the current listing version. Extractor configuration remains an
independent integration status and does not affect overall database-based health.
