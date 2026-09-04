# URL analysis verification

## Purpose

This runbook verifies milestone 2 from deterministic URL handling through the
private extraction boundary, Swedish review workspace, PostgreSQL persistence,
and listing-linked cost calculation. Automated acceptance uses only synthetic
URLs and a private fake extractor. It never authenticates to ChatGPT, mounts a
real Codex home, starts a live Codex turn, or consumes Codex allowance.

## Automated coverage

| Layer | Verified behavior |
| --- | --- |
| Core unit tests | URL normalization and rejection, page identity, source matching, bounded listing values, missing fields, provenance, status, and reviewed-input validation. |
| Codex sidecar tests | Owned CLI invocation, deterministic JSONL opened-page evidence, strict schema output, timeout, cancellation, concurrency two, failure classification, process cleanup, no retries, and safe logs. |
| Infrastructure tests | Private sidecar HTTP mapping, current-only PostgreSQL storage, versions, revisions, replacement, conflicts, cascade deletion, and forbidden-history boundaries. |
| API tests | Unsaved and saved contracts, typed failures, configuration status, source gating, validation, optimistic concurrency, and operation without an available database where permitted. |
| Frontend tests | One-to-ten URL validation, FIFO scheduling, review/provenance, manual fallback, saved lifecycle, duplicate comparison, calculator prefilling, and outdated-link handling. |
| OpenAPI verification | The generated TypeScript schema matches the public backend contract without drift. |
| Compose verification | Only Nginx publishes a port; PostgreSQL, API, real sidecar, authentication state, and the private E2E fake remain isolated. |
| Playwright | Same-origin extraction outcomes, concurrency, explicit retry, saved-listing lifecycle, whole-aggregate deletion, and listing-to-calculator version review work against PostgreSQL 18. |

The E2E fake selects deterministic behavior from synthetic URL paths. Its
private test state records only safe case identifiers and aggregate counters.
It does not retain complete URLs, prompts, request bodies, listing values, or
credentials. A post-E2E script verifies that maximum extraction concurrency was
two, failures were not retried automatically, the real sidecar did not run, and
container logs contain no extraction content.

## Repeatable local acceptance

Run the code-level checks from the repository root:

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

Backend integration tests require a running Docker engine because they use
PostgreSQL 18 Testcontainers.

Use a distinct Compose project for disposable acceptance. This prevents the
test stack from mounting or deleting the normal local `codex-home` volume:

```bash
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml build
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml run --rm --no-deps --entrypoint codex codex-extractor --version
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml up --detach postgres
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml run --rm api migrate
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml up --detach api web
curl --fail http://localhost:8088/api/health/ready
npm --prefix src/frontend run e2e -- --project=chromium
node scripts/verify-url-analysis-acceptance.mjs
```

The override replaces the API's real-sidecar dependency with the private fake.
The production sidecar image is still built and its pinned CLI version is
checked, but it is not started for browser acceptance.

Regenerate the public contract from an API running at
`http://localhost:5090`, then require a clean result:

```bash
npm --prefix src/frontend run api:generate
git diff --exit-code -- src/frontend/src/api/schema.d.ts
```

Stop and delete only the disposable acceptance stack:

```bash
docker compose --project-name car-expense-e2e -f compose.yaml -f compose.e2e.yaml down --volumes
```

Do not use `down --volumes` with the normal local or Unraid project when it
contains data or Codex authentication that must be retained.

## Expected behavior

- One through ten unique public URLs are accepted, with no more than two
  analysis requests in flight. Items complete independently in FIFO order.
- Complete and partial source-matched results retain normalized facts and
  unverified listing provenance. No-source and mismatched-source results are
  unavailable and retain no unsupported extracted values.
- Rate limiting, timeout, provider outage, and invalid output affect only their
  own cards. Nothing is retried automatically; a user action starts exactly one
  new request.
- The browser calls only the frontend origin under `/api` and never requests a
  submitted listing host directly.
- Manual drafts and review remain available without extraction. An edited value
  becomes user/manual/user-confirmed, while zero, `false`, known-empty, and
  unknown values remain distinct.
- Saving, reopening, comparing, replacing, and deleting listings uses explicit
  aggregate revisions. A stale write never overwrites current data.
- Permanent deletion removes the complete vehicle aggregate. The UI warns when
  a saved calculation is attached and retains the open card only as an unsaved
  draft afterward.
- A saved listing can open the manual calculator through its vehicle UUID.
  Safe advertised values are suggestions; assumptions are not invented. A
  listing replacement marks a linked calculation outdated without changing its
  stored assumptions or result. Explicit review and save records the current
  listing version.
- System status reports manual calculation and URL analysis as enabled, rule
  search and advisory AI review as disabled, and extractor configuration
  independently from database health. Inspecting status starts no extraction.

## Real local or Unraid extraction smoke test

Real extraction is a manual deployment check, not part of automated acceptance.
Follow [Unraid deployment](deployment-unraid.md) to prepare a dedicated
`CODEX_HOME_PATH`, build the pinned image, and run device-code login. Confirm:

1. `CODEX_MODEL` is `gpt-5.6-luna` and `CODEX_REASONING_EFFORT` is `medium`.
2. `codex login status` succeeds from the sidecar without exposing its output or
   authentication directory to another service.
3. `/api/system/status` reports
   `integrations.codexListingExtractionConfigured: true` without consuming a
   turn.
4. A user-selected disposable public listing either produces a source-matched
   draft or a safe unavailable/error result. Availability is not guaranteed.
5. Manual review, saved listings, and the calculator remain usable after an
   extraction failure.

Never paste authentication files into logs or issues. Re-authentication is
safer than an unencrypted backup of the Codex home.

## Unraid acceptance checklist

1. Back up and verify `car_expense_calculator` before applying migrations or
   replacing images.
2. Run `node scripts/verify-compose-boundaries.mjs` without real credentials in
   command output.
3. Confirm only `web` publishes a LAN port and that API, `postgresql18`, and
   `codex-extractor` are reachable only through `car-expense-network`.
4. Confirm only the sidecar mounts `CODEX_HOME_PATH`; it receives no PostgreSQL
   or Platform API credentials and mounts no repository or application source.
5. Apply migrations explicitly before starting the new API/web containers.
6. Check `/api/health/live`, `/api/health/ready`, and `/api/system/status`
   through `http://extower.local:${WEB_PORT}`.
7. Exercise one disposable URL analysis, manual correction, save/reload/replace,
   calculator linkage, outdated detection, and permanent deletion.
8. Confirm the saved listing contains only current bounded structured values,
   sources, provenance, and versions. It must contain no raw Codex output,
   complete description, seller identity, contact data, street address, or
   superseded history.

Use disposable registration numbers for destructive smoke tests. Deletion is
physical and cannot be restored by the application. Follow the backup and
rollback warnings in [Unraid deployment](deployment-unraid.md); do not roll back
persistent data merely to validate this runbook.
