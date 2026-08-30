# Data sources and integration boundaries

## Principles

- Each imported value records its source, retrieval time, and verification state.
- Missing data remains missing; parsers and AI must not guess.
- Provider-specific code lives behind infrastructure adapters.
- The application stores only data permitted by the applicable source agreement.
- Raw source responses are retained only when necessary and permitted.

## Listing marketplaces

Blocket is the first desired marketplace. Its documented Pro Import API manages a dealer's own advertisements and is not a general marketplace search API. Blocket also restricts automated and systematic use without permission. Automatic discovery and programmatic URL ingestion therefore remain disabled until a permitted API, partnership, or other approved source is available.

The architecture must allow additional providers, such as Bytbil, without changing rules or calculations.

## Swedish vehicle data

Transportstyrelsen and approved information intermediaries are potential sources for technical, inspection, registration, and ownership facts. Access, storage, processing, and publication requirements must be confirmed before implementation.

For the intended evaluation, owner count and ownership-change dates are useful. Names, addresses, and personal identity numbers are not required for deterministic rules.

## Manual fallback

All external fields can be entered or corrected manually. The UI must retain the distinction between user-entered, seller-claimed, marketplace-provided, and registry-verified values.

