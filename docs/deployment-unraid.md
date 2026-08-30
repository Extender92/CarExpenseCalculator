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

Build and start only the API and web services on Unraid:

```bash
docker compose -f compose.unraid.yaml up --build --detach
```

Verify the deployment through its single published origin:

```bash
curl --fail http://extower.local:8088/api/health/ready
```

Use the configured `WEB_PORT` instead of `8088` when it has been changed.

## Boundaries

This release assumes a trusted LAN. It has no HTTPS or authentication and must not be port-forwarded or otherwise exposed to the public internet. Add TLS and authentication before any remote-access deployment.

Database migrations will be run as a deliberate deployment step after real persistence entities exist. The runtime API identity should not receive unnecessary schema-management permissions.
