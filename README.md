# Car Expense Calculator

Car Expense Calculator is a private, local-first web application for comparing used cars before purchase. It will combine deterministic ownership-cost calculations, configurable buying rules, listing and vehicle data, and an optional AI review.

The repository is currently being modernized from a small console prototype into a Docker-hosted application for an Unraid server.

## Planned usage modes

1. **Rule-based search** – define requirements and evaluate matching listings.
2. **URL analysis** – paste one or more listing URLs and evaluate only those cars.
3. **Manual calculation** – enter vehicle and financing details without an external listing.

The initial example search profile requires a tow bar, a price from SEK 5,000 through SEK 20,000, no more than 20,000 Swedish mil, and no more than six owners.

## Technology

- ASP.NET Core and .NET 10 backend
- React, TypeScript, Vite, Tailwind CSS, and shadcn/ui frontend
- PostgreSQL 18 through Entity Framework Core and Npgsql
- Docker Compose for local development and Unraid deployment
- Optional OpenAI Responses API integration using `gpt-5.6-luna` in a later phase

## Current status

The existing console application is retained until the `legacy-console` Git tag is created. The first web-foundation milestone will provide a runnable frontend, API, PostgreSQL connectivity, health checks, tests, Docker configuration, and CI without implementing car-search or calculation features.

## Target local addresses

- Docker web application: `http://localhost:8088`
- Unraid web application: `http://extower.local:8088`
- Vite development server: `http://localhost:5173`

Only the frontend port is intended to be exposed on Unraid. Nginx forwards `/api` requests to the internal ASP.NET Core container.

## Documentation

- [Product requirements](docs/product-requirements.md)
- [Rules](docs/rules.md)
- [Architecture](docs/architecture.md)
- [Data sources](docs/data-sources.md)
- [AI design](docs/ai-design.md)
- [Unraid deployment](docs/deployment-unraid.md)
- [Roadmap](docs/roadmap.md)

## Security and data boundaries

The application is designed for a trusted local network and has no authentication in the foundation milestone. It must not be exposed to the internet over HTTP. API keys, database passwords, `.env` files, and other secrets must never be committed.

This is a private project. No open-source license has been selected.

