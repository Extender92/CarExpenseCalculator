# Car Expense Calculator

[![Build, test and verify](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml/badge.svg)](https://github.com/Extender92/CarExpenseCalculator/actions/workflows/ci.yml)

Car Expense Calculator is a local-first web application for comparing used cars before purchase. It will combine deterministic ownership-cost calculations, configurable buying rules, listing and vehicle data, and an optional AI review.

The repository is a monorepo modernized from a console prototype. The old implementation is recoverable through the `legacy-console` Git tag.

## Status

The repository foundation and manual-calculator milestone are complete. The application includes Core calculations, automatic unsaved previews, the Swedish calculator interface, and PostgreSQL-backed save, open, replace, and permanent-delete management for one current scenario per vehicle. URL analysis now includes its Core domain, private Codex extraction runtime, public preview API, Swedish review interface, PostgreSQL-backed current-listing management, and explicit listing-to-calculator linkage with outdated-version detection. Complete URL-flow verification, automatic search, and advisory AI review are not implemented yet.

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
- Private Codex extraction sidecar using ChatGPT-authenticated `gpt-5.6-luna`; the URL-analysis, saved-listing, and linked-calculator workflows are implemented while advisory AI review remains later work

## Quick start with Docker

Docker is the supported development path. Apply the database migrations
explicitly before using saved scenarios or the saved-listing API:

```bash
docker compose build
docker compose up --detach postgres
docker compose run --rm api migrate
docker compose up --detach api web
```

Open [http://localhost:8088](http://localhost:8088). The dashboard should report a healthy system and available database. Only this web port is published; Nginx forwards `/api` to the internal API container.

Open **Manuell kalkyl** to calculate without saving. Valid edits refresh the unsaved preview automatically after a short delay, while **Beräkna nu** remains available for an immediate calculation. Add an ordinary Swedish registration number to save the scenario, then use **Sparade bilar** to reopen, replace, or permanently delete it. Preview calculations never persist changes.

Open **URL-analys** to analyze one through ten public listing URLs with at most two requests in flight. Extracted facts remain visibly unverified and can be corrected or completed manually. Add an ordinary Swedish registration number to save a reviewed listing. A saved listing can create or open its vehicle calculation through a reload-safe link. Safe advertised values are offered as explicit calculator inputs; a later listing replacement marks the saved calculation outdated without changing its assumptions or result. Unsaved drafts still disappear on reload. A missing Codex login disables automatic extraction without disabling manual drafts, saved-listing management, or the manual calculator.

The default Compose password is development-only. For a persistent local installation, copy `.env.example` to `.env`, replace `POSTGRES_PASSWORD`, and then start the stack. Stop and remove the local containers with:

```bash
docker compose down
```

Add `--volumes` only when the local development database should also be deleted.

Database migrations are explicit and never run during API startup. See [Unraid deployment](docs/deployment-unraid.md) before applying or rolling back migrations on persistent data.

Saved calculations linked to a listing store the reviewed listing version, not
an independently mutable outdated flag. Replacing the listing preserves the
calculation snapshot and exposes it as outdated until the user reviews and
explicitly saves the calculation against the current version.

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

The internal URL-extraction runtime uses `CODEX_MODEL`,
`CODEX_REASONING_EFFORT`, and a dedicated persistent `CODEX_HOME_PATH` on
Unraid. It requires a saved ChatGPT Codex login and deliberately has no Platform
API-key fallback. URL analysis is exposed through both the public API and the
Swedish review and saved-listing interface; see [Codex listing extraction](docs/codex-extraction.md)
for the private runtime and one-time login procedure.

## API endpoints

- `GET /api/health/live` – process liveness
- `GET /api/health/ready` – readiness including PostgreSQL
- `GET /api/system/status` – version, overall/database state, feature availability, and non-paid Codex extraction configuration status
- `POST /api/listing-analyses` – source-aware unsaved listing extraction for one public URL
- `POST /api/saved-listings` – create a current saved listing for a new vehicle
- `GET /api/saved-listings` – list current saved-listing summaries
- `GET /api/saved-listings/{vehicleId}` – read a complete current saved listing
- `GET /api/saved-listings/by-registration/{registrationNumber}` – find a saved listing by registration number
- `PUT /api/saved-listings/{vehicleId}` – attach or fully replace a listing using its expected aggregate revision
- `DELETE /api/saved-listings/{vehicleId}?expectedRevision={revision}` – permanently delete the complete vehicle aggregate
- `POST /api/manual-calculations` – deterministic unsaved ownership-cost preview
- `POST /api/saved-cost-scenarios` – create a saved vehicle and current scenario
- `GET /api/saved-cost-scenarios` – list saved-vehicle summaries
- `GET /api/saved-cost-scenarios/{vehicleId}` – read a complete saved scenario
- `GET /api/saved-cost-scenarios/by-registration/{registrationNumber}` – find a saved vehicle by registration number
- `PUT /api/saved-cost-scenarios/{vehicleId}` – fully replace a scenario using its expected revision and explicitly preserve or acknowledge the current listing version
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
docker compose -f compose.yaml -f compose.e2e.yaml build
docker compose -f compose.yaml -f compose.e2e.yaml run --rm --no-deps --entrypoint codex codex-extractor --version
docker compose -f compose.yaml -f compose.e2e.yaml up --detach postgres
docker compose -f compose.yaml -f compose.e2e.yaml run --rm api migrate
docker compose -f compose.yaml -f compose.e2e.yaml up --detach api web
curl --fail http://localhost:8088/api/health/ready
npm --prefix src/frontend run e2e -- --project=chromium
docker compose -f compose.yaml -f compose.e2e.yaml down --volumes
```

The Playwright suite expects the Docker stack at `http://localhost:8088`. The E2E override routes extraction to a private deterministic fake and never authenticates to ChatGPT or consumes Codex allowance. Use `--volumes` only for disposable test data. The manual-calculator acceptance procedure and expected failure behavior are documented in [Manual calculator verification](docs/manual-calculator-verification.md).

## Repository layout

```text
src/backend/       API, Core, Infrastructure, extraction contracts, and private sidecar
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
- [URL analysis specification](docs/url-analysis.md)
- [Codex listing extraction](docs/codex-extraction.md)
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
