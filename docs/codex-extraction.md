# Codex listing extraction

## Status and purpose

This document defines the planned milestone 2 extraction runtime. It is not
implemented yet. URL analysis will use a private, ChatGPT-authenticated Codex
sidecar instead of calling the OpenAI Platform API directly.

Codex is only an ingestion aid. Core remains authoritative for URL matching,
normalization, provenance, missing-field codes, validation, and analysis
status. An extracted value remains unverified until the user reviews it. The
separate advisory review in milestone 5 is not part of this runtime.

## Runtime boundary

The planned deployment adds an internal `codex-extractor` service:

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

The API owns the public listing-analysis contract and an application-owned
listing-extraction abstraction. Its Infrastructure implementation calls the
sidecar through a typed `HttpClient`. The sidecar protocol is internal and must
not appear in public OpenAPI.

Each internal request contains one already normalized listing URL plus the
expected prompt and extraction-schema versions. The response contains the
actual model, versions, analysis timestamp, ordered source evidence, and the
structured extraction draft. It never accepts or returns database credentials,
browser cookies, seller contact data, a trusted Core result, or persistence
identifiers.

## Codex invocation policy

The sidecar starts exactly one non-interactive Codex turn for each URL. The
Codex CLI version and container image digest will be pinned when issue #32 is
implemented.

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
the source list only from completed web-search events that show an actual page
open. Model-authored URLs in the final structured response are never accepted as
proof that a page was opened.

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

Prompt and schema versions start at 1. The actual model reported by the Codex
turn is returned; configuration never overwrites runtime evidence.

## Authentication and configuration

The sidecar authenticates with the owner's ChatGPT account and consumes the
account's Codex allowance. It does not support `OPENAI_API_KEY`,
`CODEX_API_KEY`, Platform API billing, or an automatic API-key fallback.

For a headless Unraid deployment, device-code login is the preferred one-time
setup:

```bash
codex login --device-auth
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

Planned non-secret configuration is:

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
is requested.

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
duration, model identifier when safely available, and aggregate event counts.
They must not contain the submitted or source URLs, prompts, extracted values,
JSONL lines, final JSON, stdout/stderr bodies, filesystem paths to credentials,
or token contents. Raw Codex output and execution history are never persisted.

`--ephemeral` prevents local Codex rollout persistence for the turn. It does not
change provider-side handling; that follows the owner's ChatGPT account and data
settings.

## Testing boundary

Automated tests never authenticate to ChatGPT, mount a real Codex home, start a
live Codex turn, or consume subscription allowance. Issue #32 will use fake
processes and deterministic JSONL fixtures to verify:

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
