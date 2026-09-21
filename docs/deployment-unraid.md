# Unraid deployment

## Install once, then update with one command

- Use Linux x86_64, Docker Compose, Bash, curl, jq, GNU tar/coreutils and flock.
  The updater checks required commands. Git, Node and .NET are not needed.
- Keep the existing `postgresql18` container on `car-expense-network`.
  Use a dedicated database/role. Never use `immich-postgres`.
- Keep the project name `car-expense-calculator`. Only the frontend port is
  published. Choose an available `WEB_PORT`, for example `6425`.
- Preserve the existing `CODEX_HOME_PATH`. The updater refuses a changed bind
  mount when existing Codex containers are present.
- Use the completed public release bundle, once the first publication is
  verified. The repository is not needed for server operation.

### Existing source-build installation: one-time transition

Run from the parent of the existing `repository` directory. The paths below
are generic examples; substitute the existing appdata location. This prepares
a sibling `deploy` directory and copies only configuration, not login files.

```bash
cd /mnt/user/appdata/car-expense-calculator
mkdir deploy
cp repository/.env deploy/.env
chmod 600 deploy/.env
cd deploy
```

Keep your current port (including `6425`), credentials and absolute
`CODEX_HOME_PATH` in that copied file. Then download one complete version:

```bash
(
set -e
release_tag=$(curl --fail --silent --show-error --retry 3 \
  https://api.github.com/repos/Extender92/CarExpenseCalculator/releases/latest \
  | jq -er '.tag_name | select(test("^build-[1-9][0-9]*-[1-9][0-9]*$"))')
release_url="https://github.com/Extender92/CarExpenseCalculator/releases/download/$release_tag"
curl --fail --location --retry 3 --output unraid-bundle.tar.gz "$release_url/unraid-bundle.tar.gz"
curl --fail --location --retry 3 --output unraid-bundle.tar.gz.sha256 "$release_url/unraid-bundle.tar.gz.sha256"
sha256sum --check unraid-bundle.tar.gz.sha256
tar --extract --gzip --file unraid-bundle.tar.gz --no-same-owner \
  compose.unraid.yaml update.sh deploy.sh deploy-lib.sh .env.example manifest.json
chmod 700 update.sh deploy.sh deploy-lib.sh
./update.sh
)
```

The release is selected once, so both assets belong to the same version.
Extraction never contains or overwrites `.env`. The updater validates the
bundle again before touching running services. Keep the original repository
until readiness, preserved data, login status and the web page are checked.
Deleting it is a separate action: inspect its exact path and local/untracked
files first. The updater never deletes source directories or private notes.

### New installation

Prepare the external network and attach `postgresql18` if not already done:

```bash
docker network create car-expense-network
docker network connect car-expense-network postgresql18
```

Create a dedicated `car_expense_calculator` database owned by
`car_expense_app` using an administrative PostgreSQL session. Set its password
interactively; keep it out of terminal history and Git. Enable PostgreSQL
autostart independently of the application.

Create an empty installation directory and download/extract the same bundle
above, **before** its final `./update.sh` line. Copy `.env.example` to `.env`,
set `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `WEB_PORT` and
`CODEX_HOME_PATH`, then `chmod 600 .env`. Prepare that dedicated Codex directory
with owner `1654:1654` and mode `700`, and run `./update.sh`. The database is
migrated explicitly; a login is not required for the manual calculator.

### Every subsequent update

From `deploy`:

```bash
./update.sh
```

The updater downloads while the app runs, then stops only its three services,
runs the new API's `migrate` command and starts them together. It verifies
running image identities and API/database health through Nginx. No Git pull,
local build or manual image cleanup is needed. An already current healthy
installation is not restarted or migrated again.

After success, only the current unused-or-running image version for each app
component is deliberately retained; older unused app images are removed without
force. Images used by any other running/stopped container, unexpected tags and
unidentifiable remnants are reported and preserved. Other applications,
PostgreSQL, volumes, networks, shared build cache and login files are untouched.

If an update fails, read the reported step and private diagnostic location.
A failure before maintenance leaves the running app unchanged; a later failure
can leave it stopped. There is no automatic reset, downgrade, restoration or
retry of migration. Cleanup runs only after successful health checks. Exit 2
means **updated, but cleanup incomplete**; a later explicit `./update.sh` may
retry that cleanup without restarting an already current app.

A backup is not a mandatory updater step for an installation with disposable
data. Back up and verify data you want to recover before schema changes.
The updater keeps neither a backup nor a previous local image version for you.
The explicit rollback notes below describe separate administrative recovery.

## Codex login and health

Existing authentication stays in the same bind mount. To log in on a new
installation, use the running container:

```bash
docker exec -it car-expense-calculator-codex-extractor-1 codex login --device-auth -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
docker exec car-expense-calculator-codex-extractor-1 codex login status -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
```

These commands do not start an AI turn. Device-code login must be enabled in
the account. Never copy authentication files into the repository, bundle or
ordinary backups. There is no Platform API-key fallback. CLI 0.153.0,
`gpt-5.6-luna` and `medium` are unchanged.

For a configured port of `6425`, open `http://<server>:6425` and optionally check:

```bash
curl --fail http://localhost:6425/api/health/ready
curl --fail http://localhost:6425/api/system/status
```

This release assumes a trusted LAN, with no app authentication or HTTPS.
It is not intended for public internet exposure.

## Manual Compose administration

Normal operation needs only `./update.sh`. For the explicit administrative
Compose commands below, run from `deploy` and first load the installed digests:

```bash
export CEC_API_IMAGE=$(jq -er '.images.api' .deploy-state/current.json)
export CEC_WEB_IMAGE=$(jq -er '.images.web' .deploy-state/current.json)
export CEC_EXTRACTOR_IMAGE=$(jq -er '.images["codex-extractor"]' .deploy-state/current.json)
```

The manifest contains no secrets. `compose.unraid.yaml` points at the complete
active runtime and reads the installation's unchanged `.env`. Do not add
`build` entries or change the project name. Never use a general Docker prune
to implement application cleanup.

## Transport configuration

`COMPARISON_MAX_REQUEST_BYTES` defaults to `33554432` (32 MiB). API and Nginx
receive the same value. Complete previews retain their bounded memory,
two-operation/120-second limits and explicit errors; other routes retain their
existing limits. Listing retrieval keeps its existing 240/245/270-second total
sidecar/client/Nginx timeouts. Deployment introduces no new domain contracts.
See [comparison transport](comparison-api.md#complete-set-comparison-85),
[listing extraction](complete-listing-extraction.md),
[publication design](deployment-images.md) and
[verification](prebuilt-deployment-verification.md).

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

After accounting for the household-data removal described below, a further
rollback of calculation-to-listing linkage metadata uses:

```bash
docker compose -f compose.unraid.yaml run --rm api migrate 20260904100409_AddCurrentVehicleListings
```

Existing scenarios become manual-only after this rollback because the nullable
source-listing version column no longer exists. Rolling back the complete
listing persistence layer uses:

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

### Household storage migration and rollback

`20260906151351_AddHouseholdPersistence` adds `household_state`,
`vehicle_cost_inputs` and `vehicle_draft`. Applying it leaves existing scenarios,
listings and result snapshots intact. The two singleton rows start at revision
0 with null contents; no financial profile is inferred from an existing car.
The explicit migrations command above is still required before running the new
application. Normal API startup never migrates. Persistence contracts are
implemented, including the household HTTP/UI flows from #59-#60.

Before any rollback, stop application writes and make a verified PostgreSQL
backup of `car_expense_calculator` using the existing `postgresql18` container.
Keep the current application image available to execute its Down migration;
an older image does not know this migration. Never use `immich-postgres`.

The explicitly supported rollback to the previous schema is:

```bash
docker compose -f compose.unraid.yaml stop web api codex-extractor
docker compose -f compose.unraid.yaml run --rm api migrate 20260904132333_LinkSavedScenariosToListings
```

**This command permanently removes the shared household profile, all new
purchase/lease inputs and current review material, and the shared draft.** It
also deletes vehicle roots with neither a remaining legacy scenario nor a
listing. Unconverted legacy scenarios, their children/results, existing listings
and their vehicle identities remain. Converted calculations are not recreated:
recovering their removed information requires restoring the backup. Lower
migration targets also perform this household-data removal before their own
documented destructive steps.

Deploy the matching older application only after the target migration succeeds.
Reapplying the latest migration recreates empty household/draft metadata and
preserves surviving listings and unconverted scenarios; it does not restore
discarded household data. Test upgrade/rollback/reapply only on disposable
PostgreSQL 18 fixtures. Unraid's persistent application data is never a test
target. See the [implemented storage/transition contract](household-calculations.md#implemented-household-persistence).

### Comparison storage migration and rollback

Issue #64 delivered `20260908103211_AddComparisonPersistence` through approved
[PR #83](https://github.com/Extender92/CarExpenseCalculator/pull/83).
Run the existing explicit migration command before deploying the matching API;
API startup never migrates. It creates an empty revision-0 rule singleton and
current per-vehicle comparison facts without importing advertisements or
confirming assumptions. Existing household, legacy and listing data is preserved.

To roll back only comparison storage, first stop writes and verify a backup of
the application's database in `postgresql18`. Use the newer image, which knows
the Down migration:

```bash
docker compose -f compose.unraid.yaml stop web api codex-extractor
docker compose -f compose.unraid.yaml run --rm api migrate 20260906151351_AddHouseholdPersistence
```

This explicitly removes rule input, comparison facts, per-observation source
versions and cost confirmations. Household profile, cost inputs, shared draft,
legacy scenarios/results, listings and vehicle identities remain. Reapplying
creates empty comparison storage; restoring removed comparison data requires
the backup. Lower rollback targets also remove comparison data before performing
their documented actions. Upgrade/rollback/reapply verification uses only
disposable PostgreSQL 18 databases, never Unraid user data. See the
[comparison persistence contract](comparison-api.md#versions-and-persistence).

### Complete-listing format upgrade and guarded rollback

Migration `20260910132449_AddListingDetails` adds nullable typed JSONB and version
constraints. Follow-up `20260910214541_AllowHtmlListingExtraction` admits metadata
pairs 2/2, 3/3 and 4/3 without relabeling rows. Upgrade API, sidecar and frontend
together: new extraction is 4/3 and
the complete comparison transport was 2 at that migration (the review workflow
now uses transport 3). CLI 0.153.0 and the existing authentication
volume remain unchanged. Use the explicit `api migrate` command; do not migrate
at startup. Back up and verify data that must be recoverable before upgrading.

The previous schema target is:

```bash
docker compose -f compose.unraid.yaml run --rm api migrate 20260908103211_AddComparisonPersistence
```

Run this only in a controlled rollback with writes stopped. The command rejects
new-format listing rows or new extraction/details in the shared draft and rolls
back the operation. It never deletes that data or relabels extraction versions.
Use a compatible backup when reverting an installation containing new-format
content. Compatible old listings and household data are preserved by an allowed
rollback. See [format and evidence rules](complete-listing-extraction.md).

The sidecar retrieves only supported Blocket HTML, then interprets captured text
with web disabled. Keep outbound HTTPS/DNS available for Blocket and Codex;
there is no extra container or proxy. One full analysis runs at a time, with
30 seconds/10 MiB for source retrieval and 240/245/270-second total
sidecar/client/Nginx budgets. Source 429 respects Retry-After (60-second fallback);
403 or CAPTCHA is reported without bypass. Resuming the browser queue is explicit.

To roll back only the follow-up migration, the previous target is
`20260910132449_AddListingDetails`. The command rejects prompt-4 metadata or
HTML provenance in current listings, shared drafts or comparison facts. Restore
a compatible backup if those values must survive; do not relabel them as AI or
older extraction. The earlier details rollback guard still applies to lower
targets. Apply migrations explicitly with writes controlled, never at API startup.

### Review workflow migration

`20260918105352_AddListingReviewWorkflow` adds review drafts and the current
storage-version guards. The updater applies this existing migration explicitly;
prebuilt deployment itself adds no migration. Existing data is not relabeled or
mass-rewritten. Its guarded downgrade refuses new formats or drafts that would
be lost. See [review workflow recovery](review-and-calculation-workflow.md#versions-migration-and-recovery).
