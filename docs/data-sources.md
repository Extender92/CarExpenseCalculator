# Data sources and integration boundaries

## Principles

- Each imported value records its source, retrieval time, and verification state.
- Missing data remains missing; parsers and AI must not guess.
- Provider-specific code lives behind infrastructure adapters.
- The application stores only data permitted by the applicable source agreement.
- Raw source responses, copied page content, and seller contact data are not retained for URL analysis.
- Advertised locality and county may be retained as separate optional sourced
  facts. Street and seller addresses are excluded, and a missing county is never
  inferred from the locality.

## Listing marketplaces

Blocket is the first desired marketplace. Its documented Pro Import API manages a dealer's own advertisements and is not a general marketplace search API. Blocket also restricts automated and systematic use without permission. Direct scraping, marketplace-specific programmatic ingestion, and automatic discovery therefore remain disabled until a permitted API, partnership, or other approved source is available.

Milestone 2 uses one user-triggered, ChatGPT-authenticated Codex turn for each
URL the user selects. The private runtime, adapter, one-URL public preview
endpoint, Swedish review flow, current-only saved-listing lifecycle, and
calculator linkage are implemented and covered by fake-only acceptance. The internal
sidecar gives Codex access only to host-restricted hosted web search. The browser, API, and sidecar
do not directly download or scrape the page, and the application contains no
Blocket-specific parser. Hosted search is an extraction aid, not proof of permission or
availability: applicable source terms still govern use, and an inaccessible or
unmatched page produces an unavailable result with manual fallback rather than
a workaround. See [Codex listing extraction](codex-extraction.md) for the
runtime and authentication boundary and
[URL analysis verification](url-analysis-verification.md) for the synthetic
source, same-origin, persistence, and manual-fallback acceptance boundary.

The architecture must allow additional providers, such as Bytbil, without changing rules or calculations.

## Swedish vehicle data

Transportstyrelsen and approved information intermediaries are potential sources for technical, inspection, registration, and ownership facts. Access, storage, processing, and publication requirements must be confirmed before implementation.

For the intended evaluation, owner count and ownership-change dates are useful. Names, addresses, and personal identity numbers are not required for deterministic rules.

### Planned source selection

No registry provider has been selected for stages 3A/3B. Evaluate provider
eligibility for a household application, permitted programmatic access,
registration-based matching, field coverage, timestamps/freshness, storage
rights, and cost before choosing an adapter. This refinement is independent of
automatic marketplace discovery and must not block manual calculation.

Transportstyrelsen distinguishes public lookup services from direct register
access for organizations. A public lookup page is not itself an application
integration agreement. Use the official
[vehicle-information overview](https://www.transportstyrelsen.se/Fordons-agaruppgift/)
and [direct-access information](https://www.transportstyrelsen.se/direktanmal-och-sok-i-vagtrafikregistret)
as starting points; confirm actual provider terms before implementation.

The planned household calculator uses manual/evidenced maintenance and repair
amounts first. A later advisory AI proposal needs sources relevant to the
specific assumption and explicit user adoption. A generic model observation
does not establish a particular vehicle's condition or repair bill. See the
[Household calculations and comparison plan](household-comparison-plan.md).

## Manual fallback

All external fields can be entered or corrected manually. The UI must retain the distinction between AI-extracted listing values, user-entered values, seller claims, and reserved future registry-verified values. See the [URL analysis specification](url-analysis.md) for the exact provenance contract.
