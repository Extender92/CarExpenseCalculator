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
                      -> future internal Codex extraction sidecar
                           -> hosted Codex web search
                      -> future registry/listing providers
                      -> future advisory OpenAI review
```

The production browser sees one HTTP origin. Nginx serves the React build and proxies `/api` to the internal API service, avoiding a public API port and cross-origin configuration.

## Backend boundaries

- **Api** owns HTTP contracts, OpenAPI, health endpoints, configuration, and dependency injection.
- **Core** contains domain types, calculations, and deterministic rules without database, HTTP, or AI dependencies.
- **Infrastructure** implements PostgreSQL persistence and future integrations.
- Dependency direction is `Api -> Core`, `Api -> Infrastructure`, and `Infrastructure -> Core`.

## Frontend boundaries

- React Router owns the dashboard and the three usage-mode routes.
- TanStack-style server-state concerns can be added when feature endpoints exist; the foundation uses a small typed API client.
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

## Planned URL-analysis flow

The browser will submit one URL per `POST /api/listing-analyses` request and
limit itself to two concurrent requests. The API will normalize the URL through
Core and call an application-owned Infrastructure adapter. That adapter will
use a typed internal HTTP client to a private ASP.NET Core `codex-extractor`
sidecar. The sidecar runs one ChatGPT-authenticated `codex exec` turn with
host-restricted hosted web search; neither the browser nor application services
fetch the listing page directly.

The sidecar has no published port, database credentials, repository mount, or
application-source mount. Codex output is untrusted ingestion input. Actual
opened-page events provide source evidence, while Core owns source matching,
normalization, validation, provenance, missing-field codes, and analysis status.
Extracted facts remain unverified until the user changes them, at which point
the complete edited value becomes manually entered and user-confirmed. Advisory
AI review is a separate milestone and never shares authority with extraction.

The complete future contracts are defined in the
[URL analysis specification](url-analysis.md) and
[Codex listing extraction](codex-extraction.md).

## Persistence

PostgreSQL 18 is the permanent database. The first migration stores the accepted vehicle and saved manual-scenario aggregate:

- `vehicles` owns the UUIDv7 identity, unique normalized registration number, optional label, timestamps, and optimistic-concurrency revision.
- `saved_cost_scenarios` has a unique vehicle relationship and stores scalar inputs, calculation/result schema versions, the calculation timestamp, and a persistence-owned JSONB result snapshot.
- Energy sources, custom recurring costs, and custom one-time costs use ordered child tables with foreign keys and cascade deletion.

The user-facing registration number is a current natural key, not the database primary key. Transportstyrelsen stopped future number reuse in 2024 because historical reuse could associate the same registration number with different vehicle individuals. Personal plate text is not accepted as vehicle identity. See [registration-number reuse](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/ateranvandning-av-registreringsnummer-upphor/) and [ordinary formats](https://www.transportstyrelsen.se/sv/vagtrafik/fordon/intressenter/nu-har-de-nya-registreringsnumrena-lanserats/).

There is no append-only history in the current model. Replacement validates and recalculates through Core, removes old child rows, writes the new versioned result, and increments the revision in one transaction. A stale revision is rejected. Deleting a saved vehicle physically cascade-deletes the complete aggregate.

Migrations are applied only through the explicit backend `migrate [target]` command. Normal API startup never creates, migrates, or rolls back schema.

URL analysis will extend this aggregate without adding history. A vehicle may
be listing-only, scenario-only, or contain both current records. One current
`vehicle_listings` row will own typed listing values plus bounded JSONB, with
ordered source and equipment children. The aggregate revision changes after
any write; a separate listing version changes only when listing content changes.
Saved scenarios sourced from a listing will record that listing version so a
later listing replacement can mark, but never silently recalculate, the stored
calculation. Deleting a saved listing permanently deletes the complete vehicle
aggregate, including any saved scenario.

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

The planned URL-analysis API adds one unsaved preview endpoint plus a current
saved-listing lifecycle. Preview analysis never accesses PostgreSQL. System
status will report whether the Codex extractor is configured without starting a
search turn; overall health remains database-based and URL analysis stays
disabled until its complete Swedish interface exists.
