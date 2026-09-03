# AI design

## Deterministic authority

The application must remain useful without AI. Deterministic normalization,
validation, rules, and calculations are authoritative. AI may supply unverified
ingestion input or later advisory observations, but it may not invent facts,
calculate authoritative totals, alter rule outcomes, or replace registry
sources.

## Milestone 2: listing extraction

URL analysis uses a private Codex sidecar as a hosted extraction adapter, not as
an advisory reviewer. One user-selected URL starts one non-interactive Codex
turn. Live web search is required, and actual opened-page events are checked
before any value is accepted. The browser and application services never fetch
the listing page directly.

Planned extraction settings are:

- Runtime: an internal ASP.NET Core `codex-extractor` sidecar invoking
  `codex exec`; the API calls it through a typed `HttpClient`.
- Authentication: a dedicated persisted ChatGPT Codex login created with
  device-code authentication; there is no Platform API-key fallback.
- Model: exactly `gpt-5.6-luna` by default, configurable server-side.
- Reasoning: `medium`.
- Tool: live hosted web search with medium context and an allowed-domain filter
  derived from the submitted host.
- Output: JSONL runtime events plus a final versioned JSON Schema result. Source
  evidence comes only from actual opened-page events.
- Isolation: ephemeral, read-only execution in an empty directory with user and
  project configuration/rules ignored and unrelated tools disabled.
- Limits: one turn per URL, a 60-second total timeout, process-wide concurrency
  of two, no application retries, and no artificial search-event limit.

Page content is hostile input. The prompt rejects embedded instructions,
contact data, hidden content, recommendations, and unsupported inference.
Extracted values remain listing-sourced and unverified; missing values stay
missing. Codex failures leave manual entry and deterministic calculations
available. The exact contract is in the
[URL analysis specification](url-analysis.md), and the runtime boundary is in
[Codex listing extraction](codex-extraction.md).

Official references:

- [Codex authentication](https://learn.chatgpt.com/docs/auth)
- [Codex non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)
- [Codex web search](https://learn.chatgpt.com/docs/web-search)
- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)
- [Codex plan usage](https://learn.chatgpt.com/docs/pricing)

## Milestone 5: advisory review

Advisory review runs only after deterministic normalization, rules, and
calculations. It may explain, summarize, find contradictions, identify missing
information, and suggest seller questions. It never converts listing extraction
into verified data.

The initial advisory design also targets `gpt-5.6-luna` and structured output.
Its exact runtime, reasoning level, retention controls, versioned prompt, and
schema will be decided independently when milestone 5 is refined; milestone 2
does not silently establish those choices.

### Review package

The backend may send all meaningful structured vehicle information: listing URL, reviewed listing fields, short seller claims and condition notes, registration number, registry facts, owner count, inspection and tax information, deterministic rule output, cost output, data provenance, and relevant user notes. It must never send complete listing descriptions, copied page text, API keys, database credentials, internal secrets, or unrelated application data.

### Execution policy

- Manual and URL candidates may receive an advisory review when that later feature is configured.
- Large searches run deterministic hard filters first and review only finalists or unresolved candidates.
- An input hash prevents duplicate calls when the review package and prompt version have not changed.
- Failure, timeout, invalid JSON, or rate limiting produces an unavailable AI section while deterministic results remain visible.
- A separate deep-research action may use web search and must display cited sources.

### Future structured result

`AiReview` will contain a summary, confirmed facts, contradictions, unverified claims, missing information, risks, positive signals, seller questions, overall advisory conclusion, confidence, model and prompt versions, token usage, and creation time.

Broad cited research remains an explicit later action rather than part of URL
extraction. Image analysis remains milestone 6. No live Codex extraction or
advisory AI integration is implemented by the current repository.
