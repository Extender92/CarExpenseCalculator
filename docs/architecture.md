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
request. A future browser interface will submit separate requests and limit
itself to two concurrent requests. The API normalizes the URL through Core and
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
[Codex listing extraction](codex-extraction.md).

## Persistence

PostgreSQL 18 is the permanent database. The migrations store one current vehicle aggregate with optional scenario and listing records:

- `vehicles` owns the UUIDv7 identity, unique normalized registration number, optional label, timestamps, and optimistic-concurrency revision.
- `saved_cost_scenarios` has a unique vehicle relationship and stores scalar inputs, calculation/result schema versions, the calculation timestamp, and a persistence-owned JSONB result snapshot.
- Energy sources, custom recurring costs, and custom one-time costs use ordered child tables with foreign keys and cascade deletion.
- `vehicle_listings` has a unique optional vehicle relationship and stores current typed listing scalars, listing/extraction versions, normalized status and missing codes, timestamps, and bounded JSONB values.
- `listing_sources` and `listing_equipment` preserve normalized order in child rows. Fuel types use a nullable string array. Energy consumption, seller claims, condition notes, and field provenance use bounded persistence-owned JSONB. Raw Codex output is never stored.

The user-facing registration number is a current natural key, not the database primary key. Transportstyrelsen stopped future number reuse in 2024 because historical reuse could associate the same registration number with different vehicle individuals. Personal plate text is not accepted as vehicle identity. See [registration-number reuse](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/ateranvandning-av-registreringsnummer-upphor/) and [ordinary formats](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/nu-har-de-nya-registreringsnumrena-lanserats/).

There is no append-only history in the current model. Scenario replacement validates and recalculates through Core. Listing replacement normalizes through `ListingUrl` and `ListingDraftProcessor`, then replaces every scalar, JSONB value, source row, and equipment row. Each operation is atomic, rejects a stale aggregate revision, and retains no superseded values. Deleting either saved resource physically cascade-deletes the complete aggregate.

Migrations are applied only through the explicit backend `migrate [target]` command. Normal API startup never creates, migrates, or rolls back schema.

URL-analysis persistence extends this aggregate without adding history. A vehicle may
be listing-only, scenario-only, or contain both current records. One current
`vehicle_listings` row owns typed listing values plus bounded JSONB, with
ordered source and equipment children. The aggregate revision changes after
any write; a separate listing version changes only when listing content changes.
Saved scenarios sourced from a listing will later record that listing version so a
later listing replacement can mark, but never silently recalculate, the stored
calculation. Deleting a saved listing permanently deletes the complete vehicle
aggregate, including any saved scenario.

Advertised geography is represented by separate nullable `locality` and
`county` sourced values. Each has independent provenance. The domain does not
retain a general location value or street address and does not infer counties
or resolve geographic data. Listing persistence uses separate typed nullable
columns for these current facts.

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
recovery. Calculator-version linkage remains planned. Extractor configuration remains an independent integration status and
does not affect overall database-based health.
