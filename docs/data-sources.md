# Data sources and integration boundaries

## Principles

- Each imported value records its source, retrieval time, and verification state.
- Missing data remains missing; parsers and AI must not guess.
- Provider-specific code lives behind infrastructure adapters.
- The application stores only data permitted by the applicable source agreement.
- Raw HTML is transient. Selected original listing sections and interpreted facts
  are retained only on explicit save; identified seller contact data is masked.
- Advertised locality and county may be retained as separate optional sourced
  facts. Street and seller addresses are excluded, and a missing county is never
  inferred from the locality.

## Listing marketplaces

The explicitly selected first source is a user-submitted Blocket car page at
`https://blocket.se/mobility/item/{id}` or the `www` alias, including `?ci=3`.
The private sidecar fetches the HTML document, parses its relevant sections and
passes that captured input to Codex without web access. No browser resources,
contact functions, discovery, scheduled refresh, VPN rotation or challenge
bypass are involved. Other automatic sources return `listingSourceUnsupported`.

This implementation decision is not evidence of a marketplace agreement. The
application uses a visible identification and reports blocking/CAPTCHA or rate
limits; it never substitutes another access route. A shared cooldown respects
`Retry-After`, and the browser queue requires explicit resume. The source's
statements remain advertised input, including debt and service claims, rather
than registry facts. Full original description/equipment/question sections are
preserved separately from AI-interpreted values. See the
[complete listing contract](complete-listing-extraction.md),
[runtime boundary](codex-extraction.md) and
[live acceptance evidence](listing-extraction-verification-report.md).

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

All external fields can be entered or corrected manually. The UI must retain the distinction between directly retrieved and AI-interpreted listing values, user-entered values, seller claims, and reserved future registry-verified values. See the [URL analysis specification](url-analysis.md) for the exact provenance contract.
