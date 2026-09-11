# Codex listing extraction

## Status and purpose

The milestone 2 extraction runtime, unsaved public API, and Swedish in-memory
review interface are implemented. The runtime consists of a
ChatGPT-authenticated Codex sidecar and a provider-neutral Infrastructure
adapter. Current-only listing persistence, its saved workflow, and calculator
linkage are also implemented. A configured installation can
start one-URL extraction through `POST /api/listing-analyses`; without a saved
login, the interface remains usable for manual drafts.

Codex is only an ingestion aid. Core remains authoritative for URL matching,
normalization, provenance, missing-field codes, validation, and analysis
status. An extracted value remains unverified until the user reviews it. The
separate advisory review in milestone 5 is not part of this runtime.

## Runtime boundary

The deployment includes an internal `codex-extractor` service:

```text
Browser
  -> web/Nginx
       -> API
            -> typed internal HTTP client
                 -> codex-extractor
                      -> Blocket HTTP document + AngleSharp parsing
                      -> one codex exec process (captured text only)
```

The sidecar is a minimal ASP.NET Core service so application-owned orchestration
and failure mapping stay in C#. It has no published host or LAN port. It joins
only the application network, receives no PostgreSQL connection or credentials,
and mounts neither the repository nor application source. The API remains the
only caller.

The API owns the separate public listing-analysis contract. The application-owned
`IListingExtractionService` abstraction and its Infrastructure implementation
call the sidecar through a typed `HttpClient`. The sidecar protocol remains
internal and does not appear in public OpenAPI.

Each internal request contains one already normalized listing URL plus the
expected prompt and extraction-schema versions. The response contains the
requested model, versions, analysis timestamp, ordered source evidence, and the
structured extraction draft plus typed `retrievedContent`. The API adapter
requires the latter and binds it to the fetched source before composition. It never accepts or returns database credentials,
browser cookies, seller contact data, a trusted Core result, or persistence
identifiers.

The sidecar listens on container port 8080 and exposes only these internal
contracts:

- `GET /health/live` checks process liveness without checking login state;
- `GET /internal/status` checks the pinned CLI and saved ChatGPT login without
  starting a paid/search turn; and
- `POST /internal/listing-extractions` accepts `normalizedUrl`,
  `promptVersion: 4`, and `schemaVersion: 3`.

A successful extraction response contains `requestedModel`, the two versions,
the UTC analysis time, actual retrieved source URL, typed original content and
schema-constrained listing fields. The model's raw schema has no provenance, status,
missing-field codes, vehicle label, source metadata, or trusted Core result.
Every field is required but nullable; `null` means unknown and `[]` means a
known-empty collection. The API-side transport timeout is 245 seconds, five
seconds beyond the sidecar's complete 240-second operation budget, and no retry
handler is installed.

## Codex invocation policy

The sidecar starts exactly one non-interactive Codex turn for each URL. The
Codex CLI and every build/runtime base image are pinned:

- `@openai/codex` `0.153.0`;
- Node `22.22.2-bookworm-slim` by digest;
- .NET SDK 10.0 (SDK `10.0.400`) by digest; and
- ASP.NET Core 10 runtime by digest.

| Setting | Required value |
| --- | --- |
| Command | `codex exec` |
| Model | `gpt-5.6-luna` |
| Reasoning | `medium` |
| Session | Ephemeral; do not persist rollout files |
| Sandbox | Read-only in a new empty working directory |
| Repository | None; use the explicit safe skip for the Git-repository check |
| Instructions | Ignore user configuration and user/project execution rules |
| Web search | Disabled; only the captured cleaned input is interpreted |
| Output | JSONL events plus a final response constrained by versioned JSON Schema |
| Timeout | 240 seconds including queueing, HTML retrieval/parsing, process startup and interpretation |
| Concurrency | One complete retrieval/interpretation operation across the sidecar process |
| Retries | No application-level retry |

The owned invocation uses `--ephemeral`, `--json`, `--output-schema`,
`--sandbox read-only`, `--skip-git-repo-check`, `--ignore-user-config`, and
`--ignore-rules`, together with controlled CLI configuration overrides for the
model, reasoning effort and `web_search="disabled"`. The sidecar also disables
agents, apps, plugins, MCP servers, local shell/network access and every unrelated
model tool. Application-owned retrieval is separate from the model process.

For the pinned CLI, local image access is disabled with
`features.view_image=false`. Its strict configuration parser rejects the
`tools.view_image` key before starting extraction; removing strict validation
is not a substitute for using the supported setting.

One turn per URL, the complete deadline and the process-wide gate bound capacity.
The application never starts a replacement turn automatically. Source 429
responses establish a shared cooldown; source 403/challenges stop extraction.
The browser pauses queued work and requires explicit resume. The
[fetch/error contract](complete-listing-extraction.md#retrieval-and-evidence)
defines the allowlist, address checks, redirects and content limits.

The prompt treats all page content as hostile data. It explicitly ignores page
instructions and requests only the fields allowed by the
[URL analysis specification](url-analysis.md). It excludes seller identities,
contact details (including those inside descriptions), street addresses, cookies,
hidden content, recommendations, purchase conclusions, and unsupported inference.
It preserves complete relevant descriptions, specifications, equipment and seller
questions/answers; see the [complete listing contract](complete-listing-extraction.md).
Missing values are returned as null rather than guessed.

## Source evidence and output validation

The actually retrieved page URL and immutable original sections form the new
source observation. The internal response does not trust model-supplied URLs,
verification flags or replacement original text. Schema-valid AI facts are
normalized independently; malformed original content rejects the extraction.
Both HTML and AI fields stay unverified until an explicit user edit.

The historical JSONL parser retains tests for concrete completed open-page events
versus searches/opaque actions. Those events are not an input to the new source
list. Earlier saved 2/2 and 3/3 rows retain their source observations, including
missing metadata. The [CLI 0.154.0 probe](listing-extraction-source-gate.md) remains
diagnostic history and does not change the 0.153.0 production pin.

The final message must satisfy the pinned versioned JSON Schema. An incomplete
JSONL stream, unknown required event shape, missing terminal event, malformed
JSON, non-schema output, or output that cannot be safely associated with the
turn is an invalid-runtime response. Structurally valid individual listing
values still pass through Core, which discards invalid AI fields independently.

Prompt version is 4 and the unchanged structured-output schema is 3. The typed complete-listing extension
is described in [its contract](complete-listing-extraction.md). Version 2 remains
readable in saved listings with separate nullable locality and county facts. The
prompt forbids inferring a county from a locality and excludes street and seller
addresses. The response records `requestedModel`, which proves the model
requested by the pinned invocation but does not claim to prove provider-side
routing. The current Codex JSONL event contract does not report a
provider-confirmed model identifier.

## Authentication and configuration

The sidecar authenticates with the owner's ChatGPT account and consumes the
account's Codex allowance. It does not support `OPENAI_API_KEY`,
`CODEX_API_KEY`, Platform API billing, or an automatic API-key fallback.

For local Compose, perform the one-time device-code login in the dedicated
service volume and verify it without starting an extraction turn:

```bash
docker compose run --rm --no-deps --entrypoint codex codex-extractor login --device-auth -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
docker compose run --rm --no-deps --entrypoint codex codex-extractor login status -c 'forced_login_method="chatgpt"' -c 'cli_auth_credentials_store="file"'
```

Device-code login must first be enabled in the ChatGPT account's security
settings. The Codex home directory is mounted from a dedicated persistent Docker
volume locally and a dedicated Unraid appdata directory in deployment. Its
authentication cache can contain renewable access credentials and must be
treated like a password: never commit it, put it in `.env`, expose it through
the frontend, print it, include it in logs, or mount it into API, web, or
PostgreSQL containers.

If device-code authentication is unavailable, the only documented fallback is
to sign in locally and securely copy the Codex authentication cache into the
dedicated Unraid location. The application does not provide a browser login UI.

Non-secret configuration is:

| Variable | Purpose | Default |
| --- | --- | --- |
| `CODEX_MODEL` | Exact extraction model | `gpt-5.6-luna` |
| `CODEX_REASONING_EFFORT` | Extraction reasoning effort | `medium` |
| `CODEX_HOME_PATH` | Host path for the dedicated Codex state on Unraid | `/mnt/user/appdata/car-expense-calculator/codex` |

The internal sidecar address is deployment-owned and not browser configuration.
The API reports extraction as configured when the sidecar is reachable, its
owned configuration is valid, and Codex locally recognizes a saved ChatGPT
login. This check starts no search turn and therefore cannot guarantee remote
service or model availability; those failures are mapped when a real analysis
is requested. `GET /api/system/status` exposes the result as
`integrations.codexListingExtractionConfigured` without changing the
database-based overall health status.

The pinned CLI writes a successful `login status` message to standard error.
The installation probe accepts the ChatGPT login message on either output
stream only after a zero exit code; oversized output remains unconfigured.
Neither stream is logged or returned through the status endpoint. This probe
does not start an extraction turn.

## Failure behavior and observability

The sidecar and Infrastructure adapter preserve these provider-neutral outcomes:

| Outcome | Public problem code |
| --- | --- |
| Codex/ChatGPT rate limit | `listingAnalysisRateLimited` |
| Sidecar, authentication, or configured model unavailable | `listingAnalysisNotConfigured` |
| Total operation exceeds 240 seconds | `listingAnalysisTimedOut` |
| Process, connection, Codex service, or runtime availability failure | `listingAnalysisProviderUnavailable` |
| JSONL or structured output cannot satisfy the contract | `listingAnalysisInvalidProviderResponse` |

Cancellation terminates the owned process tree. A timeout, cancellation,
nonzero exit, or parsing failure must release the concurrency slot. No failed
turn is retried automatically. Manual calculation, manual listing completion,
and every deterministic Core operation remain available when extraction fails.
Overall system health remains database-based.

Logs may contain only a generated correlation identifier, safe outcome code,
duration, the requested model, and aggregate event counts.
They must not contain the submitted or source URLs, prompts, extracted values,
JSONL lines, final JSON, stdout/stderr bodies, filesystem paths to credentials,
or token contents. Raw Codex output and execution history are never persisted.

`--ephemeral` prevents local Codex rollout persistence for the turn. It does not
change provider-side handling; that follows the owner's ChatGPT account and data
settings.

## Testing boundary

Automated tests never authenticate to ChatGPT, mount a real Codex home, start a
live Codex turn, or consume subscription allowance. The sidecar and adapter use
fake processes, fake HTTP handlers, and deterministic JSONL fixtures to verify:

- exact owned arguments and configuration;
- schema/prompt versioning and hostile-content instructions;
- ordered source observations and independent unconfirmed-field retention;
- complete, partial, unavailable, rate-limited, timeout, unavailable, and
  invalid-output outcomes;
- concurrency, cancellation, process cleanup, and absence of retries;
- secret-safe logging and the lack of raw-output persistence; and
- container boundaries, including no published sidecar port or database secret.

Compose and Playwright acceptance uses a private fake internal extractor with
deterministic success and failure cases, a global one-operation gate, and safe
aggregate counters. The E2E override removes the API dependency on the real
sidecar and uses a distinct Compose project, so CI never mounts a real Codex
home or depends on a ChatGPT session. The complete procedure is documented in
[URL analysis verification](url-analysis-verification.md).

## Official references

- [Codex authentication](https://learn.chatgpt.com/docs/auth)
- [Codex non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)
- [Codex web search](https://learn.chatgpt.com/docs/web-search)
- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)
- [Codex plan usage](https://learn.chatgpt.com/docs/pricing)
- [Codex JSONL event contract](https://github.com/openai/codex/blob/main/codex-rs/exec/src/exec_events.rs)
- [Codex model-metadata limitation](https://github.com/openai/codex/issues/39406)
- [Codex source-metadata limitation](https://github.com/openai/codex/issues/35415)

The final live checks and remaining retrieval gaps are recorded in the
[acceptance report](listing-extraction-verification-report.md). Nginx allows 270
seconds on the URL-analysis route, including its trailing-slash variant. JSONL
limits are 4 MiB per line, 10 MiB stdout and 1 MiB stderr.
