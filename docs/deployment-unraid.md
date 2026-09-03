# Unraid deployment

## Target

- Unraid host: `extower.local`
- Web URL: `http://extower.local:8088` by default
- Database: existing `postgresql18` container
- Network: a shared user-defined Docker network named `car-expense-network`
- Public ports: frontend only

`immich-postgres` is owned by Immich and must never be reused by this application.

## PostgreSQL preparation

Create a dedicated database and role:

- Database: `car_expense_calculator`
- Application role: `car_expense_app`
- Password: supplied outside Git

Attach `postgresql18`, the API, and the frontend proxy to `car-expense-network`. The API connection host is `postgresql18` and the container port is `5432`; never depend on a dynamic `172.x.x.x` address.

One possible one-time network setup from the Unraid terminal is:

```bash
docker network create car-expense-network
docker network connect car-expense-network postgresql18
```

If the network or attachment already exists, do not recreate it. Create the role and database from an administrative PostgreSQL session, using a strong password that matches `.env`:

```sql
CREATE ROLE car_expense_app WITH LOGIN PASSWORD 'replace-this-password';
CREATE DATABASE car_expense_calculator OWNER car_expense_app;
```

Do not grant the application role access to Immich or other application databases.

## HTTP routing

Nginx publishes `${WEB_PORT:-8088}` and serves the React application. Requests under `/api` are proxied to `api:8080`. API and database ports remain internal.

## Configuration

Copy `.env.example` to `.env` and provide local values. The `.env` file is ignored by Git. Do not place credentials in Compose YAML, React variables, source files, container images, or GitHub Actions logs.

Validate the repository-owned port, network, and connection boundaries before deployment. The script overrides Compose variables with fixed verification values, so real deployment credentials are not required or printed:

```bash
node scripts/verify-compose-boundaries.mjs
```

## Planned Codex extraction service

URL extraction is not implemented in the current Compose files. Issue #32 will
add a private `codex-extractor` sidecar on `car-expense-network`. It will have no
published port, PostgreSQL settings, repository mount, or application-source
mount. Only the API will call it over the internal network.

The future sidecar will authenticate once through the Unraid terminal with:

```bash
codex login --device-auth
```

Device-code login must be enabled in the ChatGPT account's security settings.
The future Compose service will mount its Codex home from
`${CODEX_HOME_PATH:-/mnt/user/appdata/car-expense-calculator/codex}` so refreshed
credentials survive container replacement. This directory is a secret: exclude
it from Git, logs, shares exposed to untrusted users, and mounts into the API,
web, or PostgreSQL containers. Do not place its contents in `.env`.

The application will not support a Platform API key as a fallback. CI and
deployment smoke tests will use a fake extractor and never mount the real Codex
home or consume ChatGPT usage. Exact build, login, upgrade, and verification
commands will be added together with the sidecar implementation. See
[Codex listing extraction](codex-extraction.md).

## Initial deployment

The existing `postgresql18` container must be running and attached to `car-expense-network`. Build the application images, apply the database migration, and only then start API and web:

```bash
docker compose -f compose.unraid.yaml build
docker compose -f compose.unraid.yaml run --rm api migrate
docker compose -f compose.unraid.yaml up --detach api web
```

The migration command uses the API service's configured `ConnectionStrings__Postgres` value and exits after all pending migrations have been applied. A failure returns a nonzero exit code. Do not start the application until the failure has been investigated and resolved.

Verify liveness, database readiness, and feature status through the single published origin:

```bash
curl --fail http://extower.local:8088/api/health/live
curl --fail http://extower.local:8088/api/health/ready
curl --fail http://extower.local:8088/api/system/status
docker compose -f compose.unraid.yaml ps
```

Use the configured `WEB_PORT` instead of `8088` when it has been changed. The Compose output must show a published port only for `web`; `api` must not have a host-port mapping. The existing `postgresql18` container must not publish PostgreSQL to the LAN for this application.

Complete the browser and saved-data checks in [Manual calculator verification](manual-calculator-verification.md).

## Upgrades

Create and verify a PostgreSQL backup before an upgrade. Build the new images while the current application is still running, then use a short maintenance window for migration and replacement:

```bash
docker compose -f compose.unraid.yaml build
docker compose -f compose.unraid.yaml stop web api
docker compose -f compose.unraid.yaml run --rm api migrate
docker compose -f compose.unraid.yaml up --detach api web
curl --fail http://extower.local:8088/api/health/ready
```

If migration fails, leave the updated services stopped, preserve the command output, and investigate before restarting. Do not attempt an arbitrary rollback against persistent data.

## Boundaries

This release assumes a trusted LAN. It has no HTTPS or authentication and must not be port-forwarded or otherwise exposed to the public internet. Add TLS and authentication before any remote-access deployment.

## Database migrations

Migrations are an explicit one-shot backend mode. Normal API startup never applies, creates, or rolls back schema. The initial-deployment and upgrade procedures above are the canonical Unraid sequences.

Local development uses the same image and command:

```bash
docker compose build api
docker compose up --detach postgres
docker compose run --rm api migrate
docker compose up --detach api web
```

An explicit migration name may be supplied as the final argument. Target `0` rolls back every application migration:

```bash
docker compose -f compose.unraid.yaml run --rm api migrate 0
```

**Warning:** rollback can permanently drop tables and saved data. Back up PostgreSQL first, confirm the exact target migration, and use `0` only when intentionally removing the complete application schema. Separate runtime and migration database roles are future hardening; the current dedicated application role owns only `car_expense_calculator` and must never receive access to other application databases.
