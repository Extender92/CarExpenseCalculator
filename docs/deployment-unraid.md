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

## Codex extraction service

Compose includes a private `codex-extractor` sidecar on
`car-expense-network`. It has no published port, PostgreSQL settings, repository
mount, or application-source mount. Only the API calls its internal port 8080.
Its liveness check does not require authentication, so a missing or expired
Codex login cannot block the manual calculator.

The sidecar runs as container user `1654`. Prepare the dedicated appdata path
before the first login:

```bash
codex_state_path=/mnt/user/appdata/car-expense-calculator/codex
mkdir -p "$codex_state_path"
chown 1654:1654 "$codex_state_path"
chmod 700 "$codex_state_path"
```

Set `CODEX_HOME_PATH` to that path in `.env`, build the pinned image, perform
device-code login once, and verify the saved ChatGPT session without starting a
search:

```bash
docker compose -f compose.unraid.yaml build codex-extractor
docker compose -f compose.unraid.yaml run --rm --no-deps --entrypoint codex codex-extractor login --device-auth -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
docker compose -f compose.unraid.yaml run --rm --no-deps --entrypoint codex codex-extractor login status -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
```

Device-code login must be enabled in the ChatGPT account's security settings.
The mounted Codex home preserves refreshed credentials across container
replacement. Treat the directory as a secret: exclude it from Git, logs, shares
exposed to untrusted users, and mounts into API, web, or PostgreSQL containers.
Do not copy its contents into `.env` or an ordinary unencrypted backup.

Back up this directory only to secret-capable encrypted storage if retaining the
session is operationally necessary. Restoring it gives access equivalent to the
saved Codex session. Re-authentication is safer than retaining an unprotected
copy. The application has no Platform API-key fallback. CI never authenticates,
starts a live turn, or consumes ChatGPT usage. See
[Codex listing extraction](codex-extraction.md).

## Initial deployment

The existing `postgresql18` container must be running and attached to `car-expense-network`. Complete the one-time Codex login above, build the application images, apply the database migration, and only then start the services:

```bash
docker compose -f compose.unraid.yaml build
docker compose -f compose.unraid.yaml run --rm api migrate
docker compose -f compose.unraid.yaml up --detach codex-extractor api web
```

The migration command uses the API service's configured `ConnectionStrings__Postgres` value and exits after all pending migrations have been applied. This includes `AddCurrentVehicleListings`, which adds the current saved-listing tables without exposing a saved-listing API yet. A failure returns a nonzero exit code. Do not start the application until the failure has been investigated and resolved.

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

Create and verify a PostgreSQL backup before an upgrade. This is mandatory before applying `20260904100409_AddCurrentVehicleListings`. Build the new images while the current application is still running, then use a short maintenance window for migration and replacement:

```bash
docker compose -f compose.unraid.yaml build
docker compose -f compose.unraid.yaml stop web api codex-extractor
docker compose -f compose.unraid.yaml run --rm api migrate
docker compose -f compose.unraid.yaml up --detach codex-extractor api web
curl --fail http://extower.local:8088/api/health/ready
```

Image replacement preserves the bound `CODEX_HOME_PATH`. After a Codex CLI
upgrade, verify `codex --version` and `codex login status` with the commands
above before testing extraction. Never solve an authentication failure by
mounting the Codex home into another service.

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
docker compose up --detach codex-extractor api web
```

An explicit migration name may be supplied as the final argument. Target `0` rolls back every application migration:

```bash
docker compose -f compose.unraid.yaml run --rm api migrate 0
```

Rolling back only the listing migration uses:

```bash
docker compose -f compose.unraid.yaml run --rm api migrate 20260830181537_InitialSavedCostScenarios
```

**Warning:** rolling back to `InitialSavedCostScenarios` permanently removes all
saved listing data and deletes every listing-only vehicle root. Combined vehicles
retain their saved scenarios, and scenario-only vehicles are unaffected. A
rollback to `0` removes the complete application schema and all saved scenarios
as well. Back up and verify PostgreSQL first, stop application writes, confirm
the exact target, and restore the newer application/migration before expecting
listing persistence again. Separate runtime and migration database roles are
future hardening; the current dedicated application role owns only
`car_expense_calculator` and must never receive access to other application
databases.
