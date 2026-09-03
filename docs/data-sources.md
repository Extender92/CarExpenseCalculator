# Data sources and integration boundaries

## Principles

- Each imported value records its source, retrieval time, and verification state.
- Missing data remains missing; parsers and AI must not guess.
- Provider-specific code lives behind infrastructure adapters.
- The application stores only data permitted by the applicable source agreement.
- Raw source responses, copied page content, and seller contact data are not retained for URL analysis.

## Listing marketplaces

Blocket is the first desired marketplace. Its documented Pro Import API manages a dealer's own advertisements and is not a general marketplace search API. Blocket also restricts automated and systematic use without permission. Direct scraping, marketplace-specific programmatic ingestion, and automatic discovery therefore remain disabled until a permitted API, partnership, or other approved source is available.

Milestone 2 uses one user-triggered, ChatGPT-authenticated Codex turn for each
URL the user selects. The private runtime, adapter, and one-URL public preview
endpoint are implemented; the Swedish user flow remains planned. The internal
sidecar gives Codex access only to host-restricted hosted web search. The browser, API, and sidecar
do not directly download or scrape the page, and the application contains no
Blocket-specific parser. Hosted search is an extraction aid, not proof of permission or
availability: applicable source terms still govern use, and an inaccessible or
unmatched page produces an unavailable result with manual fallback rather than
a workaround. See [Codex listing extraction](codex-extraction.md) for the
runtime and authentication boundary.

The architecture must allow additional providers, such as Bytbil, without changing rules or calculations.

## Swedish vehicle data

Transportstyrelsen and approved information intermediaries are potential sources for technical, inspection, registration, and ownership facts. Access, storage, processing, and publication requirements must be confirmed before implementation.

For the intended evaluation, owner count and ownership-change dates are useful. Names, addresses, and personal identity numbers are not required for deterministic rules.

## Manual fallback

All external fields can be entered or corrected manually. The UI must retain the distinction between AI-extracted listing values, user-entered values, seller claims, and reserved future registry-verified values. See the [URL analysis specification](url-analysis.md) for the exact provenance contract.
