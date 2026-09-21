# Prebuilt deployment design

## Publication

`Build, test and verify` builds Linux amd64 images once in the container job,
using `compose.release-build.yaml`. It adds OCI source/revision labels and the
`se.car-expense-calculator.component` label. .NET 10.0.400, Node 22.22.2, Codex
CLI 0.153.0, `gpt-5.6-luna` and `medium` remain pinned. The browser suite uses
fake extraction; the real sidecar is checked for process health and CLI version,
without a login or AI turn.

Only a successful push to this repository's `main` can publish, after backend,
frontend, OpenAPI, deployment regression, Compose, Chromium and URL acceptance
checks. The container job exports `docker save` plus expected image IDs/commit;
the publication job loads and checks them instead of rebuilding. Only that job
receives package/content write permission.

Repositories:

- `ghcr.io/extender92/car-expense-calculator-api`
- `ghcr.io/extender92/car-expense-calculator-web`
- `ghcr.io/extender92/car-expense-calculator-codex-extractor`

Each publication has `build-<run-id>-<run-attempt>` tags. The manifest pins
registry digests, not moving image tags. Publication is serialized without
cancelling an in-progress publisher. It uses
[`queue: max`](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)
so up to 100 pending publications wait instead of replacing one another.
Promotion compares numeric run ID and
attempt across published releases, so an older run finishing later cannot
replace the latest release. Release-tag pushes do not trigger this workflow.

The tested artifact is scoped to its workflow run, retained for one day and
replaced only after a successful container verification. A publisher-only retry
can reuse that exact artifact for a new run-attempt version; after artifact
expiry, rerun all jobs. Commit/image identity checks still apply.

After all three pushes, pulls using an empty Docker credential configuration
must succeed. Only then does the job create a draft GitHub Release and upload
`unraid-bundle.tar.gz` and its SHA-256 file. The draft becomes public/latest only
after both uploads succeed. Partial pushes and failed uploads are never
installable releases. GitHub/GHCR may retain history; server retention is separate.

**First publication:** GHCR initially creates private packages. The repository
owner must set all three packages to public in their GitHub package settings.
The first workflow may therefore fail its anonymous-pull gate deliberately.
Make the packages public and rerun the full workflow, which produces a new
attempt identity. The first public release and anonymous download must be
verified after the separately approved merge. Nothing is published from a PR.
See [GitHub's registry authentication and visibility documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry).

## Bundle and updater state

The bundle contains `compose.unraid.yaml`, `update.sh`, `deploy.sh`,
`deploy-lib.sh`, `.env.example` and `manifest.json`. Manifest format 1 records
the version, commit, architecture, three image references and SHA-256 values
for every installation file. This is a deployment format, not an API/storage
version. Archives reject unexpected names, symlinks, oversized contents and
invalid checksums before maintenance.

`update.sh` is a stable launcher. On first use it loads the adjacent runtime;
subsequent runs use `.deploy-state/current`, an atomically replaced symlink to
the complete active runtime. The installation owns `.env` independently. The
visible Compose file points to the current runtime; digest variables are loaded
from its manifest. A private installation lock prevents concurrent updates.
The current manifest and image history are retained in `.deploy-state`.

`CEC_RELEASE_API` normally needs no configuration. Its default is the public
repository's latest-release API. Tests use a loopback HTTP release fixture;
other custom endpoints require HTTPS. One latest lookup selects all assets for
that update, so publication during a download cannot mix versions. An older
release or changed content under the installed version identity is rejected.

The updater validates Docker, Linux amd64, Compose, external PostgreSQL/network,
the single published frontend port, and the existing Codex bind mount. It
pulls/validates all images before stopping only the three app services. Migration
is `run --rm --no-deps api migrate`; startup does not build or repeat migrations.
Process state, image IDs and three API health/status endpoints through Nginx
must pass before activation and cleanup. Login availability is displayed
separately and does not block the manual calculator. No advertisement or AI
request is made. An already current, healthy installation is not restarted.

Downloads and read-only health polling have bounded retries. Migration, service
mutations and restoration have no automatic retry. Pre-maintenance failure
leaves the running application intact. Later failure can leave it stopped:
the failed step and a private diagnostic directory are shown, no cleanup or
automatic rollback follows, and another invocation is an explicit user action.
Exit 0 means success; 1 means update failure (130/143 for interruption);
exit 2 means the app is updated but cleanup is incomplete.

## Cleanup boundary

Image history records observed existing service image IDs during transition,
then verified pulled images. App-labelled images from earlier interrupted
downloads are discovered as well. Cleanup excludes the current three IDs and
any ID referenced by a running **or stopped** container. Legacy transition
images need the exact `car-expense-calculator-<component>:local` reference;
new images need matching source/component labels and application repository
names. Unexpected tags, unidentified images or inventory errors fail closed.

There is no forced image removal or global prune. PostgreSQL/other application
images, containers, volumes, networks and Codex files are outside cleanup.
Shared build cache and unidentifiable old images are not guessed at or removed.
Blocked cleanup reports IDs and leaves the working app running. No previous
app version is deliberately reserved on the server. Compatible historical
images may be downloaded manually if needed; that is not an automatic database
rollback or a promise of schema compatibility.

## Verification

`tests/deployment/update.test.mjs` executes the real Bash runtime against fake
Docker/HTTP commands, including failures and protected resources.
`publication.test.mjs` executes the real publisher with fake Docker/GitHub
commands and guards the workflow dependencies. No CI test publishes a release.

The integration Compose file runs a private Docker-in-Docker daemon. Its runner
has no host Docker socket and no application credentials. Inside that daemon a
local registry serves the exact exported images, PostgreSQL 18 starts at the
previous seven-migration schema with fictitious data/revisions, and an old app
fixture is upgraded on port 8091. The test verifies the eighth migration,
unchanged data/configuration/auth marker, current IDs, scoped image removal and
a second invocation without restarts. The outer Chromium stack uses a separate
fake extractor and disposable database. See the [verification report](prebuilt-deployment-verification.md).
