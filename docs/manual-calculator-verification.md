# Manual calculator verification

## Purpose

This runbook verifies the completed manual-calculator milestone from the pure
Core calculation through the single-origin Docker deployment. It covers
automatic unsaved previews, the PostgreSQL-backed saved-vehicle lifecycle, and
saved-listing version linkage without requiring live marketplace access or a
ChatGPT-authenticated Codex turn.

## Automated coverage

| Layer | Verified behavior |
| --- | --- |
| Core unit tests | Decimal formulas, validation, completeness, financing, energy, recurring costs, rounding, and the documented example. |
| Architecture tests | Core remains independent from API and Infrastructure. |
| API integration tests | Unsaved previews work with unreachable PostgreSQL; saved endpoints validate contracts, listing-link modes, outdated metadata, conflicts, revisions, stored versions, and deletion. |
| Infrastructure integration tests | PostgreSQL 18 migrations, constraints, listing source versions, current/outdated transitions, atomic replacement, optimistic concurrency, and cascade deletion. |
| Frontend tests | Swedish form behavior, automatic-preview debounce/cancellation, safe listing prefills, explicit suggestions, result presentation, persistence failures, conflicts, accessibility, and draft preservation. |
| OpenAPI verification | Generated TypeScript contracts match the backend document without drift. |
| Compose boundary verification | Only Nginx publishes a port, services use the intended networks, and Unraid targets the dedicated PostgreSQL database and role. |
| Playwright | The documented result, saved lifecycle, and listing-create/link/refresh/review lifecycle work through one browser origin with the private fake extractor. |

The saved-lifecycle Playwright test uses PostgreSQL. Its successful create after
an explicit migration also proves that the deployed schema is available.

## Repeatable local acceptance

Run backend and frontend verification from the repository root:

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run test
npm --prefix src/frontend run build
node scripts/verify-compose-boundaries.mjs
```

The backend integration tests require a running Docker engine because they use
PostgreSQL 18 Testcontainers.

Build and start a disposable application stack in migration-first order:

```bash
docker compose build
docker compose up --detach postgres
docker compose run --rm api migrate
docker compose up --detach api web
curl --fail http://localhost:8088/api/health/ready
npm --prefix src/frontend run e2e -- --project=chromium
```

The readiness response must be healthy and report PostgreSQL as available. The
browser must load at `http://localhost:8088`, and all browser API requests must
stay on that origin under `/api`; CORS is neither configured nor required.

To verify the generated contract, run the API on `http://localhost:5090` in one
terminal. In another terminal, run:

```bash
npm --prefix src/frontend run api:generate
git diff --exit-code -- src/frontend/src/api/schema.d.ts
```

Stop the temporary stack afterward:

```bash
docker compose down
```

Use `docker compose down --volumes` only when the database volume was created
solely for this verification and none of its saved vehicles need to be kept.

## Expected application behavior

- The unsaved calculation endpoint is deterministic and does not access
  PostgreSQL. It remains usable when saved-scenario persistence is unavailable.
- Valid form edits start a preview after 500 ms. Obsolete requests are canceled,
  only the latest response is displayed, and no preview writes to PostgreSQL.
- A persistence failure is shown separately in the Swedish interface and does
  not disable unsaved previews or discard entered values.
- Saved create and replacement operations recalculate through Core. Reopening a
  vehicle displays its stored, versioned result snapshot.
- Replacement requires the current revision. A conflict never overwrites newer
  data automatically.
- Permanent deletion removes the vehicle aggregate from PostgreSQL. When the
  open vehicle is deleted in the interface, its current form and result remain
  as an unsaved draft.
- A saved listing can create or open its calculation. Updating the listing marks
  a linked calculation outdated without altering its assumptions or snapshot;
  explicit review and save acknowledge the current listing version.
- Manual calculation and URL analysis are enabled. Rule search and advisory AI
  review remain disabled. Automated acceptance uses the fake extractor and does
  not require Codex authentication or an external listing service.

## Unraid smoke test

After following [Unraid deployment](deployment-unraid.md):

1. Confirm `docker compose -f compose.unraid.yaml ps` publishes a port only for
   `web`; `api` must have no host-port mapping.
2. Confirm the existing `postgresql18` container and both application services
   are attached to `car-expense-network`. PostgreSQL must not be published to
   the LAN for this application.
3. Request `/api/health/live`, `/api/health/ready`, and `/api/system/status`
   through `http://extower.local:8088` or the configured web port.
4. Open `/manual`, run the documented SEK 64,000/49,000 example, and confirm the
   result is shown without saving.
5. With a disposable registration number, save the vehicle, reload the page,
   open it, replace one value, and verify that its revision increases.
6. Delete that disposable vehicle, confirm it disappears from the saved list,
   and confirm the form remains available as an unsaved draft.

Do not use a real saved vehicle for the deletion smoke test. The delete is
physical and cannot be restored by the application.
