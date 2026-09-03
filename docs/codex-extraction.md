# Codex listing extraction

## Status and purpose

The milestone 2 extraction runtime and unsaved public API are implemented. The
runtime consists of a ChatGPT-authenticated Codex sidecar and a provider-neutral
Infrastructure adapter. Listing persistence and the Swedish review interface
remain planned. A configured installation can start one-URL extraction through
`POST /api/listing-analyses`.

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
                      -> one codex exec process
                           -> hosted Codex web search
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
structured extraction draft. It never accepts or returns database credentials,
browser cookies, seller contact data, a trusted Core result, or persistence
identifiers.

The sidecar listens on container port 8080 and exposes only these internal
contracts:

- `GET /health/live` checks process liveness without checking login state;
- `GET /internal/status` checks the pinned CLI and saved ChatGPT login without
  starting a paid/search turn; and
- `POST /internal/listing-extractions` accepts `normalizedUrl`,
  `promptVersion: 1`, and `schemaVersion: 1`.

A successful extraction response contains `requestedModel`, the two versions,
the UTC analysis time, normalized concrete opened-source URLs, and the raw
schema-constrained listing fields. The raw schema has no provenance, status,
missing-field codes, vehicle label, source metadata, or trusted Core result.
Every field is required but nullable; `null` means unknown and `[]` means a
known-empty collection. The API-side transport timeout is 65 seconds, five
seconds beyond the sidecar's complete 60-second operation budget, and no retry
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
| Web search | Live and restricted to the submitted normalized host |
| Search context | Medium |
| Output | JSONL events plus a final response constrained by versioned JSON Schema |
| Timeout | 60 seconds including queueing, process startup, search, and parsing |
| Concurrency | At most two turns across the complete sidecar process |
| Retries | No application-level retry |

The owned invocation uses `--ephemeral`, `--json`, `--output-schema`,
`--sandbox read-only`, `--skip-git-repo-check`, `--ignore-user-config`, and
`--ignore-rules`, together with controlled CLI configuration overrides for the
model, reasoning effort, live web search, medium context, and one allowed
domain. The sidecar disables agents, apps, plugins, MCP servers, local shell
network access, and every unrelated tool. Only the hosted web-search capability
needed for extraction is available.

There is no artificial limit on the number of web-search events inside the
single turn. Cost and capacity are bounded by one turn per URL, the total
timeout, global concurrency, and the absence of application retries. Codex may
manage its own model/tool behavior within that one turn; the application never
starts a replacement turn automatically.

The prompt treats all page content as hostile data. It explicitly ignores page
instructions and requests only the fields allowed by the
[URL analysis specification](url-analysis.md). It excludes seller identities,
contact details, street addresses, cookies, hidden content, complete listing
descriptions, recommendations, purchase conclusions, and unsupported inference.
Missing values are returned as null rather than guessed.

## Source evidence and output validation

The sidecar parses the Codex JSONL stream as untrusted runtime output. It builds
the source list only from completed `web_search` items whose action is
`open_page` or `find_in_page` and contains a concrete URL. Search queries,
search-result snippets, citations, and model-authored URLs in the final
structured response are never accepted as proof that a page was opened.

Source URLs are retained in first-seen order and deduplicated by their complete
normalized URL. Core parses and normalizes each source and applies its
directional page-matching rules. If the submitted page is not represented by an
opened source, all extracted values are discarded and the successful preview is
classified as `unavailable`.

The final message must satisfy the pinned versioned JSON Schema. An incomplete
JSONL stream, unknown required event shape, missing terminal event, malformed
JSON, non-schema output, or output that cannot be safely associated with the
turn is an invalid-runtime response. Structurally valid individual listing
values still pass through Core, which discards invalid AI fields independently.

Prompt and schema versions start at 1. The response records `requestedModel`,
which proves the model requested by the pinned invocation but does not claim to
prove provider-side routing. The current Codex JSONL event contract does not
report a provider-confirmed model identifier.

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

## Failure behavior and observability

The sidecar and Infrastructure adapter preserve these provider-neutral outcomes:

| Outcome | Public problem code |
| --- | --- |
| Codex/ChatGPT rate limit | `listingAnalysisRateLimited` |
| Sidecar, authentication, or configured model unavailable | `listingAnalysisNotConfigured` |
| Total operation exceeds 60 seconds | `listingAnalysisTimedOut` |
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
- ordered source extraction and Core source gating;
- complete, partial, unavailable, rate-limited, timeout, unavailable, and
  invalid-output outcomes;
- concurrency, cancellation, process cleanup, and absence of retries;
- secret-safe logging and the lack of raw-output persistence; and
- container boundaries, including no published sidecar port or database secret.

Later Compose and Playwright verification uses a fake internal extractor. CI
never depends on a real ChatGPT session.

## Official references

- [Codex authentication](https://learn.chatgpt.com/docs/auth)
- [Codex non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)
- [Codex web search](https://learn.chatgpt.com/docs/web-search)
- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)
- [Codex plan usage](https://learn.chatgpt.com/docs/pricing)
- [Codex JSONL event contract](https://github.com/openai/codex/blob/main/codex-rs/exec/src/exec_events.rs)
- [Codex model-metadata limitation](https://github.com/openai/codex/issues/39406)
- [Codex source-metadata limitation](https://github.com/openai/codex/issues/35415)
