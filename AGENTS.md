# Agent instructions

These instructions apply to every agent working in this repository.

## Before starting work

- Read the root `README.md` and the relevant documents under `docs/` before
  changing code, tests, workflows, deployment configuration, or documentation.
- Inspect the relevant implementation before proposing or making changes.
- Check the current branch, Git status, staged changes, and unstaged changes.
- Preserve existing user changes. Do not reset, discard, overwrite, or reformat
  unrelated work.
- Treat the user's current request as the immediate source of truth. Use
  `docs/product-requirements.md`, `docs/architecture.md`, and `docs/roadmap.md`
  for established product scope, technical boundaries, and delivery order.
- Inspect GitHub Issues when the user references or assigns an issue or when the
  requested work is already tracked there. When assigned an issue, read the
  complete issue and every linked repository document before changing anything.
- Agents never select issues themselves. The user or primary coordinating agent
  must assign an explicit issue number. Only issues with `status:ready` and no
  unmet dependencies may be implemented.
- Do not require an issue for ordinary user-requested work that is not already
  tracked.
- Perform implementation work on an existing matching focused branch or create
  one when authorized. Do not implement changes directly on `main`.

## Project boundaries

- The repository is a modular monolith and monorepo:
  - `src/backend/CarExpenseCalculator.Api` owns HTTP contracts, OpenAPI, health
    endpoints, and application composition.
  - `src/backend/CarExpenseCalculator.Core` contains dependency-free domain
    concepts, calculations, and deterministic rules.
  - `src/backend/CarExpenseCalculator.Infrastructure` owns PostgreSQL and future
    external-service adapters.
  - `src/frontend` contains the React, TypeScript, Vite, Tailwind CSS, and
    shadcn/ui-based web application.
- Keep dependency direction consistent with `docs/architecture.md`. Core must
  not reference API, Infrastructure, database, HTTP, or AI packages.
- User-visible UI text is Swedish. Code, identifiers, commit messages, README,
  and technical documentation are English unless the user requests otherwise.
- PostgreSQL 18 is the permanent database choice. Do not add placeholder tables,
  empty migrations, or speculative persistence entities. Add the first migration
  only with a real product domain model.
- The Unraid deployment must use the existing `postgresql18` container through
  `car-expense-network`. Never connect this application to `immich-postgres`.
- Only the frontend/Nginx port is published. API and PostgreSQL remain internal,
  and Nginx proxies `/api` to the API container.
- The local release has no authentication or HTTPS and must not be designed for
  public internet exposure without a separate security milestone.

## Product and integration rules

- Preserve the three product modes: rule-based search, URL analysis, and manual
  calculation.
- Deterministic normalization, rules, and calculations are the source of truth.
  AI may later review or explain results but must never override them.
- Do not implement scraping, automatic marketplace discovery, or programmatic
  listing ingestion without a documented and permitted data source.
- Missing external data must remain missing or require manual input. Do not guess
  registration facts, owner counts, mileage, prices, or vehicle history.
- Do not add live OpenAI calls during work that is scoped to the foundation.
  Future AI work must follow `docs/ai-design.md`, keep deterministic fallback
  behavior, and never send API keys, database credentials, or unrelated data.
- Keep future feature flags disabled until the corresponding behavior is real,
  tested, and documented.

## Configuration and secrets

- Never commit `.env` files, API keys, real database passwords, connection
  strings containing real credentials, or other secrets.
- Document configuration through `.env.example` using placeholders or clearly
  development-only defaults.
- Do not expose secrets to the React build, logs, test output, container image
  layers, or GitHub Actions output.
- Keep local, Compose, and Unraid configuration behavior documented in the root
  README and `docs/deployment-unraid.md`.

## While working

- Keep work focused on the requested objective and its acceptance criteria.
- Do not add unrelated cleanup merely because nearby files are open.
- Distinguish documented target behavior from the current implementation. Do not
  claim that a planned feature exists without verifying code and tests.
- Update tests and documentation when behavior, public contracts, configuration,
  or deployment steps change.
- Add an automated regression test for corrected defects when the behavior can
  be tested reliably.
- Do not remove, skip, or weaken tests merely to make verification pass.
- Do not hand-edit `src/frontend/src/api/schema.d.ts`. Regenerate it from the
  running backend OpenAPI document with `npm run api:generate`.
- Do not create EF Core migrations until a task introduces a real persistence
  model and explicitly requires the migration.
- Do not use destructive Git or filesystem operations that could remove user
  work.

## Git and GitHub actions

- Use focused branches and clear English commit messages.
- Follow `docs/issue-workflow.md` for assigned issue work. Use one issue per
  branch and pull request, with `feature/<number>-<slug>`,
  `fix/<number>-<slug>`, `docs/<number>-<slug>`, or
  `chore/<number>-<slug>` as the branch name.
- Assignment of a `status:ready` issue authorizes changing that issue to
  `status:in-progress`, creating its branch, implementing and verifying its
  scope, committing and pushing the work, and opening or updating a pull request.
  It does not authorize merging the pull request; merges always require separate
  user approval. After an approved merge, GitHub automatically deletes the
  remote head branch. That automatic cleanup does not require another approval.
- Assignment does not authorize changing the issue's scope, priority, milestone,
  dependencies, or acceptance criteria. The primary coordinating agent owns
  backlog refinement and promotes work to `status:ready` only after verifying
  that all decisions and dependencies are resolved.
- Read-only inspection of repository history, workflow runs, pull requests, and
  issues is allowed when relevant to the task.
- Do not create commits, push branches, open or merge pull requests, delete
  branches, create tags, or publish releases unless the user explicitly requests
  that action or the assigned-issue authorization above applies. Merge is never
  included in assigned-issue authorization. Do not manually delete unmerged
  branches without an explicit user request; GitHub-managed deletion of merged
  pull-request head branches occurs automatically.
- Do not create, edit, comment on, close, reprioritize, or assign GitHub Issues
  unless the user explicitly requests that action. For an assigned issue, the
  authorization above is limited to replacing `status:ready` with
  `status:in-progress` and adding comments that link its pull request or document
  concrete blocking evidence. It never authorizes closing, reprioritizing,
  reassigning, or changing the issue's agreed content.
- Use `Closes #N` only when every acceptance criterion for issue `#N` is met.
- If assigned work becomes blocked, stop rather than guess or expand scope.
  Comment with concrete evidence and notify the primary coordinating agent.
- Keep the `legacy-console` tag intact as the recovery point for the old console
  implementation.

## Verification

Choose checks in proportion to the affected area. Before reporting implementation
work as complete, review the final Git status and complete diff, check for
temporary files or secrets, and run the relevant commands below.

### Backend

```bash
dotnet restore CarExpenseCalculator.sln
dotnet build CarExpenseCalculator.sln --configuration Release --no-restore
dotnet test CarExpenseCalculator.sln --configuration Release --no-build
```

Backend integration tests use PostgreSQL 18 through Testcontainers and therefore
require a running Docker engine.

### Frontend

Run from `src/frontend`:

```bash
npm ci
npm run lint
npm run test
npm run build
```

### OpenAPI contract

When a backend HTTP contract changes, run the API on `http://localhost:5090`, run
`npm run api:generate` from `src/frontend`, and verify that the generated schema
contains only the intended changes. CI checks for contract drift.

### Docker and end-to-end

When frontend routing, API behavior, containers, Nginx, Compose, or deployment
configuration changes:

```bash
docker compose up --build --detach
curl --fail http://localhost:8088/api/health/ready
cd src/frontend
npm run e2e -- --project=chromium
```

Stop the temporary stack afterward. Delete its volumes only when they were
created solely for the current verification and contain no user data.

### Documentation-only work

For documentation-only changes, validate the affected links, commands,
formatting, terminology, and alignment with current code. A full build is not
required unless the documentation changes executable configuration or commands
whose validity needs verification.

Never hide or dismiss verification failures. Investigate them and clearly
separate pre-existing failures from failures introduced by the current work.
Report warnings, failed or skipped checks, and checks that could not be run.

## Completion report

For implementation work, state:

- What changed.
- The current branch.
- Whether a commit or external GitHub change was created.
- Build warnings and errors.
- Passed, failed, and skipped test counts when tests were run.
- Manual, Docker, or external checks performed.
- Checks that could not be performed.
- Remaining limitations or follow-up work.
