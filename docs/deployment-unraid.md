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

## HTTP routing

Nginx publishes `${WEB_PORT:-8088}` and serves the React application. Requests under `/api` are proxied to `api:8080`. API and database ports remain internal.

## Configuration

Copy `.env.example` to `.env` and provide local values. The `.env` file is ignored by Git. Do not place credentials in Compose YAML, React variables, source files, container images, or GitHub Actions logs.

## Boundaries

This release assumes a trusted LAN. It has no HTTPS or authentication and must not be port-forwarded or otherwise exposed to the public internet. Add TLS and authentication before any remote-access deployment.

Database migrations will be run as a deliberate deployment step after real persistence entities exist. The runtime API identity should not receive unnecessary schema-management permissions.

