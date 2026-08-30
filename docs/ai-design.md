# AI review design

## Role

AI is an advisory reviewer after deterministic normalization, rules, and calculations. It may explain, summarize, find contradictions, identify missing information, and suggest seller questions. It may not calculate authoritative totals, alter rule outcomes, invent facts, or replace registry sources.

## Planned OpenAI configuration

- Model: `gpt-5.6-luna`
- API: Responses API
- Reasoning effort: `low` initially
- Output: Structured Outputs with a versioned JSON Schema
- Response storage: request with `store: false`
- External web search: separate user-requested deep-research action
- Image review: later milestone, not AI v1

Official references:

- [GPT-5.6 Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna)
- [Responses API](https://developers.openai.com/api/reference/cli/resources/responses/methods/create)

## Review package

The backend may send all meaningful vehicle information: listing URL and visible text, structured listing fields, registration number, registry facts, owner count, inspection and tax information, deterministic rule output, cost output, data provenance, and relevant user notes. It must never send API keys, database credentials, internal secrets, or unrelated application data.

## Execution policy

- Manual and URL analyses receive an AI review when AI is configured.
- Large searches run deterministic hard filters first and review only finalists or unresolved candidates.
- An input hash prevents duplicate calls when the review package and prompt version have not changed.
- Failure, timeout, invalid JSON, or rate limiting produces an unavailable AI section while deterministic results remain visible.
- A separate deep-research action may use web search and must display cited sources.

## Future structured result

`AiReview` will contain a summary, confirmed facts, contradictions, unverified claims, missing information, risks, positive signals, seller questions, overall advisory conclusion, confidence, model and prompt versions, token usage, and creation time.

No live OpenAI integration or SDK dependency is part of the foundation milestone.

