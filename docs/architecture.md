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
                      -> future listing/vehicle providers
                      -> future OpenAI Responses API
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

## Planned domain concepts

These concepts are documented now and implemented only when their feature milestone begins:

- `Vehicle`: stable technical identity and specifications.
- `Listing`: source-specific price, seller, URL, description, and advertised facts.
- `RegistrySnapshot`: time-stamped verified vehicle and ownership facts.
- `SearchProfile`: user-defined hard requirements and preferences.
- `RuleEvaluation`: explainable results tied to a rule version and data sources.
- `CostScenario`: financing, use, energy, tax, and maintenance assumptions.
- `AiReview`: structured advisory observations that never override deterministic results.

## Persistence

PostgreSQL 18 is the permanent database. The foundation registers connectivity and health checks but creates no placeholder tables or empty migrations. Schema migrations begin with real domain entities and are applied through a controlled deployment step rather than automatically on API startup.

## Public foundation API

- `GET /api/health/live` checks process liveness only.
- `GET /api/health/ready` checks PostgreSQL readiness.
- `GET /api/system/status` returns application version, overall state, database state, and feature availability.

