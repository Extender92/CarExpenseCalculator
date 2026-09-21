# Prebuilt deployment verification

## Status

Implementation on `chore/prebuilt-unraid-deployment` starts from main
`d55d0abf5a867247bbe7769d7b7ca8aa106c4b4d`. It changes deployment artifacts,
CI, tests and operations documentation; no HTTP contracts, domain models,
generated frontend types or migrations change. Existing local documentation
edits and private installation notes in the original worktree are preserved.

The source-free updater and isolated real-container upgrade pass locally.
The PR's CI remains the final pre-merge gate. Public GHCR visibility, the first
GitHub Release and anonymous public installation are deliberately deferred
until a separately approved merge. No existing local installation or Unraid
server has been updated, and no live listing/AI calls were made.

## Results

| Check | Result |
| --- | --- |
| .NET 10.0.400 restore/build/test, PostgreSQL 18 Testcontainers | 1,169 passed; 0 failed/skipped; build 0 warnings/errors |
| Node 22.22.2 `npm ci`, lint, Vitest, production build | 305 passed; 0 failed/skipped; lint/build passed |
| Bash ShellCheck | Passed |
| Update/publication regressions | 36 passed; 0 failed/skipped |
| Real registry/PostgreSQL upgrade in disposable Docker daemon | 1 passed; 0 failed/skipped |
| Chromium, one worker, fake extractor, port 8091 | 77 passed; 0 failed/skipped on full rerun |
| Log-content scanner | 3 passed; 0 failed/skipped |
| URL acceptance: scheduling, failure cases, retries, isolation and safe logs | Passed |
| Compose port/network/source/image boundaries | Passed |
| OpenAPI generated from the running disposable API through Nginx | Identical content; LF/CRLF only |
| Workflow lint (actionlint 1.7.12) | One unsupported-key diagnostic for documented GitHub `concurrency.queue`; see below |

The 1,169 backend tests comprise 603 Core, 100 extractor, 281 API integration,
146 Infrastructure integration, 35 Infrastructure unit and 4 architecture tests.

## Deployment evidence

- A complete bundle selects one version and pins all three registry digests.
  Real Bash tests reject corrupt checksums, altered files, symlinks, extra files,
  missing image entries, an altered installed version and stale releases.
- Publication tests execute the publisher with fake Docker/GitHub commands:
  PRs cannot publish; the workflow requires every verification job; incomplete
  pushes, private packages and failed asset upload cannot promote a release.
  An older completing run keeps `make_latest=false`. No rebuild command exists
  in the publication job.
- The updater preserves the exact `.env` bytes and Codex bind path. It takes a
  lock, checks PostgreSQL readiness, pulls before maintenance, migrates once,
  checks actual image IDs and HTTP health, then activates and cleans up.
- Pre-maintenance failures leave running services unchanged. Migration, start,
  identity and health failures never mark a version current or clean images.
  No mutating step is automatically retried. Concurrent update is refused.
- Cleanup checks stopped containers too. Protected, foreign-tagged or failing
  removals report an updated app with incomplete cleanup. Failure to inventory
  containers prevents image removal. An unrelated image survives every case.
- The real integration test imports the exported production images into its
  own Docker daemon and serves them through a local registry. It starts at
  `20260910214541_AllowHtmlListingExtraction`, with a fictitious `TST951` vehicle,
  revision 7, version-1 cost input containing an exact 12345.67 price and a
  household transition revision of 9. All eight existing migrations apply;
  JSON/data/revisions stay byte-equivalent at the PostgreSQL JSON snapshot
  level. The API reads the old price and default inherited electric share.
- Port 8091, an authentication-directory marker, PostgreSQL container identity
  and an unrelated stopped container/image survive. The three running images
  match the tested image IDs exactly. Previous app image IDs disappear. A second
  update does not change service start timestamps or rerun migration.
- Codex CLI 0.153.0 and process health are checked without authentication or
  AI turns. Missing login is displayed separately from successful database/API
  health. Chromium uses only the private fake extractor.

## Attempts, warnings and limitations

- Initial Chromium run: 75 passed and two failed in legacy-data setup because
  the local runner lacked `E2E_POSTGRES_USER`/`E2E_POSTGRES_DB` for its custom
  disposable database. Supplying those existing variables resolved it; all
  77 tests passed in a full rerun. No tests were weakened or skipped.
- Extending cleanup discovery exposed an empty-line fixture case on a second
  updater invocation: 35 of 36 checks passed before the fix. Image inventory now
  filters empty entries and keeps diagnostics private; the full regression suite
  was rerun before delivery.
- The first GitHub container run passed Chromium but failed the new upgrade
  fixture: the runner's classic Docker image IDs did not resolve after loading
  into containerd. Build, test and publication now pin Docker 29.8.0 and the
  same containerd store. Exact image-ID assertions remain in place. Older
  superseded CI runs were cancelled; final results refer to the published head.
- The OpenAPI hash check initially detected Windows CRLF versus generated LF.
  A content comparison and Git diff confirmed no schema changes. Starting a
  separate local API process was rejected by automatic command review; the
  already-running disposable API provided the read-only contract check instead.
- `npm ci` reports two pre-existing high-severity dependency advisories in
  `@redocly/openapi-core` and `js-yaml`. No dependency lockfile was changed or
  automatic audit fix applied in this deployment task.
- Playwright emits the existing `NO_COLOR`/`FORCE_COLOR` warning. Git emits
  expected working-copy line-ending notices. Shell scripts/bundles explicitly
  normalize LF to work on Unraid.
- Actionlint 1.7.12 does not recognize `queue: max`. GitHub documents this
  supported property and its 100-pending-run limit; it prevents pending
  publications from cancelling one another. The unsupported linter diagnostic
  is retained, not silently suppressed. GitHub's actual workflow validation
  and the PR run validate the workflow. See
  [GitHub concurrency documentation](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency).
- Public package visibility requires the first-time owner setting in GHCR.
  The anonymous-pull gate deliberately prevents publication while packages are
  private. Actual public registry/release verification requires approved merge;
  it cannot be claimed from these fake/local-registry tests.
- The real Unraid transition remains a separate agreed step. No backup is
  required by the updater for disposable data; no reset or rollback exists.
  Only Linux amd64 is supported by this first deployment format.

## Resource accounting

All new helper files are under ignored `temp/prebuilt-deployment/`. The browser
stack uses project `car-expense-e2e`, port 8091 and the separate
`car-expense-prebuilt-tests` network. Registry, PostgreSQL and deployment fixtures
live inside the `car-expense-deployment-tests` daemon and its test-only volume.
Cleanup is restricted to these resources. Existing installation containers,
login/data volumes, prior deferred temporary inventories and backups are outside
the cleanup scope. The focused worktree is retained for PR review.

Final cleanup and CI links are recorded in the PR delivery summary.
