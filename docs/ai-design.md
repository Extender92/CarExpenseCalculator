# AI design

## Deterministic authority

The application must remain useful without AI. Deterministic normalization,
validation, rules, and calculations are authoritative. AI may supply unverified
ingestion input or later advisory observations, but it may not invent facts,
calculate authoritative totals, alter rule outcomes, or replace registry
sources.

## Milestone 2: listing extraction

URL analysis uses the private Codex sidecar to interpret one captured Blocket
listing. Application-owned HttpClient retrieval and AngleSharp parsing precede
the turn. The model cannot fetch other pages. Original sections are preserved
by deterministic composition even if the model rewrites or omits them.

- Runtime: pinned Codex CLI **0.153.0**, private ASP.NET Core sidecar.
- Authentication: existing dedicated ChatGPT device-code login; no API-key fallback.
- Model/reasoning: `gpt-5.6-luna`, `medium`.
- Tools: web search, shell, apps, plugins, agents and unrelated tools disabled.
- Input: normalized URL and cleaned original sections; no reference answers,
  authentication material or unrelated application data.
- Output: strict schema **3**, prompt **4**, plus application-owned captured
  content. `html` and `ai` both remain unverified listing sources.
- Isolation: ephemeral execution in an empty read-only working directory,
  ignoring user/project rules and configuration.
- Limits: one complete analysis at a time, one turn per URL, no automatic retry;
  30-second/10-MiB fetch within the 240-second total deadline including queueing.
  The API client waits 245 seconds and Nginx 270 seconds.

The [historical source-event probe](listing-extraction-source-gate.md) does not
gate this flow. Source observation comes from the actual HTTP retrieval, while
the model's suggestions still require user review.

Page content is hostile input. The prompt rejects embedded instructions,
contact data, hidden content, recommendations, and unsupported inference.
Extracted values remain listing-sourced and unverified; missing values stay
missing. Codex failures leave manual entry and deterministic calculations
available. The exact contract is in the
[URL analysis specification](url-analysis.md), and the runtime boundary is in
[Codex listing extraction](codex-extraction.md). Fake-only end-to-end acceptance
and deployment checks are documented in
[URL analysis verification](url-analysis-verification.md).

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

### Planned maintenance and repair assistance

The [Household calculations and comparison plan](household-comparison-plan.md)
starts with manually entered amounts and documented evidence such as workshop
estimates. Optional AI assistance is later work and does not block the
deterministic household-calculation stage.

Such assistance may suggest relevant service items, questions, and supported
cost assumptions for a specific car using its known model, age, mileage, and
history. Suggestions must cite their relevant evidence, expose uncertainty,
and require explicit user adoption before entering the calculator. Unsupported
amounts remain missing; the user may separately enter an explicit estimate.
AI must not diagnose hidden defects or imply that a model-level issue is a
verified defect of an individual vehicle.

The exact proposal schema, permitted sources, and quality checks require
refinement. Profile edits and table refreshes never automatically invoke this
assistance. It does not reuse the narrow listing-extraction endpoint as a
general review service, retain superseded calculations, or override Core.

### Review package

This future advisory package is separate from URL extraction. The current
[listing extraction contract](complete-listing-extraction.md) returns complete
relevant descriptions from the supplied URL; it does not send stored descriptions
or the development reference fixtures to an additional AI reviewer.

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
extraction. Image analysis remains milestone 6. The milestone 2 private Codex
runtime, URL-analysis API, Swedish review and saved-listing interface, and
calculator linkage are implemented and verified. No advisory AI integration is
available yet.
