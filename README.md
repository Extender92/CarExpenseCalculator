# Car Expense Calculator

[![Build, test and verify](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml/badge.svg)](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml)

Car Expense Calculator is a local-first web application for comparing used cars before purchase. It will combine deterministic ownership-cost calculations, configurable buying rules, listing and vehicle data, and an optional AI review.

The repository is a monorepo modernized from a console prototype. The old implementation is recoverable through the `legacy-console` Git tag.

## Status

The runnable web foundation is implemented. It includes a Swedish dashboard, typed API contract, PostgreSQL 18 readiness, Docker and Unraid configuration, automated tests, and CI. Actual car calculations, listing ingestion, automatic search, and OpenAI calls are intentionally not implemented yet.

The three product modes are:

1. **Rule-based search** – define requirements and evaluate matching listings.
2. **URL analysis** – paste one or more listing URLs and evaluate only those cars.
3. **Manual calculation** – enter vehicle and financing details without an external listing.

The initial example profile requires a tow bar, a price from SEK 5,000 through SEK 20,000, no more than 20,000 Swedish mil, and no more than six owners.

## Technology

- ASP.NET Core on .NET SDK `10.0.400`
- React, TypeScript, Vite, Tailwind CSS, and shadcn/ui component conventions
- PostgreSQL 18 through Entity Framework Core and Npgsql
- Node.js `22.22.2` and npm with a committed lockfile
- Nginx and Docker Compose for local and Unraid deployment
- xUnit, Testcontainers, Vitest, Testing Library, and Playwright
- Optional OpenAI Responses API integration with `gpt-5.6-luna` in a later phase

## Quick start with Docker

Docker is the supported zero-setup development path:

```bash
docker compose up --build
```

Open [http://localhost:8088](http://localhost:8088). The dashboard should report a healthy system and available database. Only this web port is published; Nginx forwards `/api` to the internal API container.

The default Compose password is development-only. For a persistent local installation, copy `.env.example` to `.env`, replace `POSTGRES_PASSWORD`, and then start the stack. Stop and remove the local containers with:

```bash
docker compose down
```

Add `--volumes` only when the local development database should also be deleted.

## Local development

Prerequisites are the pinned .NET and Node versions plus PostgreSQL 18. The API expects `ConnectionStrings__Postgres`; the development fallback in `appsettings.json` uses `localhost:5433`.

```bash
dotnet restore
dotnet run --project src/backend/CarExpenseCalculator.Api
```

In another terminal:

```bash
cd src/frontend
npm ci
npm run dev
```

Vite runs at [http://localhost:5173](http://localhost:5173) and proxies `/api` to `http://localhost:5090`.

## Configuration

| Variable | Purpose | Default |
| --- | --- | --- |
| `WEB_PORT` | Only LAN-facing Docker port | `8088` |
| `POSTGRES_DB` | Dedicated database | `car_expense_calculator` |
| `POSTGRES_USER` | Dedicated application role | `car_expense_app` |
| `POSTGRES_PASSWORD` | Local/Unraid role password | development fallback locally; required on Unraid |
| `ConnectionStrings__Postgres` | Direct API connection override | composed internally |

`OPENAI_API_KEY` and `OPENAI_MODEL` are documented as future settings only. The foundation does not read them or call OpenAI.

## API foundation

- `GET /api/health/live` – process liveness
- `GET /api/health/ready` – readiness including PostgreSQL
- `GET /api/system/status` – version, overall/database state, and disabled feature flags
- `GET /api/openapi/v1.json` – OpenAPI document used to generate frontend types

Regenerate the committed TypeScript API contract while the API is running on port 5090:

```bash
cd src/frontend
npm run api:generate
```

CI fails if regeneration changes `src/frontend/src/api/schema.d.ts`.

## Verification

GitHub Actions runs the `Build, test and verify` workflow. Pull requests to `main` must pass these checks:

- `Backend - build and test`
- `Frontend - lint, test and build`
- `OpenAPI - verify contract`
- `Docker - build and end-to-end test`

```bash
dotnet test CarExpenseCalculator.sln --configuration Release
cd src/frontend
npm run lint
npm run test
npm run build
npm run e2e
```

The Playwright suite expects the Docker stack at `http://localhost:8088`.

## Repository layout

```text
src/backend/       API, Core, and Infrastructure projects
src/frontend/      React application, generated API types, and Nginx config
tests/backend/     Architecture and PostgreSQL integration tests
docs/              Product, architecture, integration, AI, and operations notes
compose.yaml       Self-contained local stack
compose.unraid.yaml  API/web stack using the existing postgresql18 container
```

## Unraid

The target URL is `http://extower.local:${WEB_PORT}` (`8088` by default). The Unraid Compose file joins the API and frontend to `car-expense-network` and connects to the existing `postgresql18:5432` service. It never uses `immich-postgres`. See [Unraid deployment](docs/deployment-unraid.md) for preparation and commands.

## Documentation

- [Product requirements](docs/product-requirements.md)
- [Rules](docs/rules.md)
- [Architecture](docs/architecture.md)
- [Data sources](docs/data-sources.md)
- [AI design](docs/ai-design.md)
- [Unraid deployment](docs/deployment-unraid.md)
- [Roadmap](docs/roadmap.md)
- [Issue workflow](docs/issue-workflow.md)

## Security and data boundaries

The application is designed for a trusted local network and has no authentication or HTTPS in this milestone. Do not port-forward or otherwise expose it to the internet. API keys, database passwords, `.env` files, and other secrets must never be committed.

The source repository is public. An open-source license has not yet been selected and will be handled separately.
