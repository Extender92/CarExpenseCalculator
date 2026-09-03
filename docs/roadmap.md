# Roadmap

## 0. Repository foundation — complete

- Preserve and tag the console prototype.
- Create the .NET 10, React/TypeScript, PostgreSQL, Docker, testing, CI, and documentation foundation.
- Provide health/status contracts and Swedish placeholder routes.

## 1. Manual calculator — complete

- Implement the new domain model and first database migration.
- Port valid cost-calculation ideas from the legacy prototype with explicit units and decimal money values.
- Support unsaved calculations and optionally saved scenarios.

## 2. URL analysis

- Define URL normalization, source matching, bounded listing facts, provenance, and missing-data contracts.
- Add dependency-free listing concepts and deterministic validation in Core.
- Add a bounded internal Codex extraction sidecar using ChatGPT authentication,
  host-restricted hosted web search, and no direct scraping or
  marketplace-specific parsing.
- Expose independent unsaved previews and current saved-listing APIs.
- Persist one current listing per vehicle with optimistic concurrency and no analysis history.
- Build Swedish analysis, manual-review, saved-listing, and calculator-prefill workflows.
- Verify the complete flow through a fake extractor, PostgreSQL, Compose, and
  browser tests without live Codex calls or ChatGPT usage.

## 3. Rules and comparison

- Implement configurable search profiles, hard rules, warnings, positive signals, versioned evaluations, candidate lists, and side-by-side comparison.

## 4. Automatic discovery

- Enable scheduled searches only after approved marketplace access is available.
- Track runs, deduplicate listings, detect changes, and evaluate new or updated candidates.

## 5. AI review

- Add advisory GPT-5.6 Luna review with Structured Outputs and deterministic fallback behavior, separate from milestone 2 extraction.
- Review manual/URL candidates and automatic-search finalists.
- Add separately requested, cited web research.

## 6. Image review and refinement

- Add bounded image selection and vision review for visible risks.
- Evaluate prompts and model results against a curated set of known listings.
- Add cost dashboards, prompt/version tracking, and quality metrics.
