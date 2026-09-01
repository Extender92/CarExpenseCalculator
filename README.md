# Car Expense Calculator

[![Build, test and verify](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml/badge.svg)](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml)

Car Expense Calculator is a local-first web application for comparing used cars before purchase. It will combine deterministic ownership-cost calculations, configurable buying rules, listing and vehicle data, and an optional AI review.

The repository is a monorepo modernized from a console prototype. The old implementation is recoverable through the `legacy-console` Git tag.

## Status

The repository foundation and manual-calculator milestone are complete. The application includes Core calculations, the unsaved preview API, the Swedish calculator interface, and PostgreSQL-backed save, open, replace, and permanent-delete management for one current scenario per vehicle. Listing ingestion, automatic search, and OpenAI calls are intentionally not implemented yet.

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

Docker is the supported development path. Apply the existing database migration
explicitly before using saved scenarios:

```bash
docker compose build
docker compose up --detach postgres
docker compose run --rm api migrate
docker compose up --detach api web
```

Open [http://localhost:8088](http://localhost:8088). The dashboard should report a healthy system and available database. Only this web port is published; Nginx forwards `/api` to the internal API container.

Open **Manuell kalkyl** to calculate without saving. Add an ordinary Swedish registration number to save the scenario, then use **Sparade bilar** to reopen, replace, or permanently delete it. Unsaved previews remain independent from PostgreSQL persistence.

The default Compose password is development-only. For a persistent local installation, copy `.env.example` to `.env`, replace `POSTGRES_PASSWORD`, and then start the stack. Stop and remove the local containers with:

```bash
docker compose down
```

Add `--volumes` only when the local development database should also be deleted.

Database migrations are explicit and never run during API startup. See [Unraid deployment](docs/deployment-unraid.md) before applying or rolling back migrations on persistent data.

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

## API endpoints

- `GET /api/health/live` – process liveness
- `GET /api/health/ready` – readiness including PostgreSQL
- `GET /api/system/status` – version, overall/database state, and feature availability
- `POST /api/manual-calculations` – deterministic unsaved ownership-cost preview
- `POST /api/saved-cost-scenarios` – create a saved vehicle and current scenario
- `GET /api/saved-cost-scenarios` – list saved-vehicle summaries
- `GET /api/saved-cost-scenarios/{vehicleId}` – read a complete saved scenario
- `GET /api/saved-cost-scenarios/by-registration/{registrationNumber}` – find a saved vehicle by registration number
- `PUT /api/saved-cost-scenarios/{vehicleId}` – fully replace a scenario using its expected revision
- `DELETE /api/saved-cost-scenarios/{vehicleId}?expectedRevision={revision}` – permanently delete an aggregate using its expected revision
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
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run test
npm --prefix src/frontend run build
node scripts/verify-compose-boundaries.mjs
docker compose build
docker compose up --detach postgres
docker compose run --rm api migrate
docker compose up --detach api web
curl --fail http://localhost:8088/api/health/ready
npm --prefix src/frontend run e2e -- --project=chromium
docker compose down
```

The Playwright suite expects the Docker stack at `http://localhost:8088`. Add `--volumes` to the final cleanup command only for a disposable database. The complete acceptance procedure and expected failure behavior are documented in [Manual calculator verification](docs/manual-calculator-verification.md).

## Repository layout

```text
src/backend/       API, Core, and Infrastructure projects
src/frontend/      React application, generated API types, and Nginx config
tests/backend/     Architecture and PostgreSQL integration tests
docs/              Product, architecture, integration, AI, and operations notes
scripts/           Repository-level verification utilities
compose.yaml       Self-contained local stack
compose.unraid.yaml  API/web stack using the existing postgresql18 container
```

## Unraid

The target URL is `http://extower.local:${WEB_PORT}` (`8088` by default). The Unraid Compose file joins the API and frontend to `car-expense-network` and connects to the existing `postgresql18:5432` service. It never uses `immich-postgres`. See [Unraid deployment](docs/deployment-unraid.md) for preparation and commands.

## Documentation

- [Product requirements](docs/product-requirements.md)
- [Manual calculator specification](docs/manual-calculator.md)
- [Manual calculator verification](docs/manual-calculator-verification.md)
- [Rules](docs/rules.md)
- [Architecture](docs/architecture.md)
- [Data sources](docs/data-sources.md)
- [AI design](docs/ai-design.md)
- [Unraid deployment](docs/deployment-unraid.md)
- [Roadmap](docs/roadmap.md)
- [Issue workflow](docs/issue-workflow.md)

## Security and data boundaries

The application is designed for a trusted local network and has no authentication or HTTPS in this milestone. Do not port-forward or otherwise expose it to the internet. API keys, database passwords, `.env` files, and other secrets must never be committed.

## License

Copyright © 2026 Extender92. All rights reserved.

No license is granted for this source code. The repository is public so the work can be viewed and reviewed, but public availability does not grant permission to use, copy, modify, or distribute the code except as permitted by applicable law or GitHub's Terms of Service.

This licensing decision was recorded on 2026-09-01.
