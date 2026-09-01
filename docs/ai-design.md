# AI design

## Deterministic authority

The application must remain useful without AI. Deterministic normalization,
validation, rules, and calculations are authoritative. AI may supply unverified
ingestion input or later advisory observations, but it may not invent facts,
calculate authoritative totals, alter rule outcomes, or replace registry
sources.

## Milestone 2: listing extraction

URL analysis uses OpenAI as a hosted extraction adapter, not as an advisory
reviewer. One user-selected URL is sent per Responses API request. Web Search is
required and its complete source list is checked before any value is accepted.
The browser and backend never fetch the listing page directly.

Planned extraction settings are:

- Model: `gpt-5.6-luna`, with an optional server-only override.
- API: Responses API through a typed `HttpClient`; no OpenAI SDK dependency.
- Reasoning: `low`.
- Tool: required `web_search` with live access, medium search context, and an
  allowed-domain filter derived from the submitted host.
- Output: strict Structured Outputs with a versioned JSON Schema and the
  complete Web Search source list.
- Storage: `store: false`.
- Limits: at most two tool calls, a 45-second timeout, process-wide concurrency
  of two, and no automatic paid retries.

Page content is hostile input. The prompt rejects embedded instructions,
contact data, hidden content, recommendations, and unsupported inference.
Extracted values remain listing-sourced and unverified; missing values stay
missing. Provider failures leave manual entry and deterministic calculations
available. The exact contract is in the
[URL analysis specification](url-analysis.md).

Official references:

- [GPT-5.6 Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna)
- [Web Search](https://developers.openai.com/api/docs/guides/tools-web-search)
- [Responses API](https://developers.openai.com/api/reference/cli/resources/responses/methods/create)

## Milestone 5: advisory review

Advisory review runs only after deterministic normalization, rules, and
calculations. It may explain, summarize, find contradictions, identify missing
information, and suggest seller questions. It never converts listing extraction
into verified data.

The initial advisory configuration also uses `gpt-5.6-luna`, low reasoning,
Structured Outputs, and `store: false`. It has a separate versioned prompt and
schema from listing extraction.

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
extraction. Image analysis remains milestone 6. No live OpenAI integration is
implemented by the repository foundation or this specification.
