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

- Add one-or-many URL submission and a provider abstraction.
- Import only through a permitted source and ask for manual completion when values are unavailable.
- Apply deterministic rules and store source-aware candidate results.

## 3. Rules and comparison

- Implement configurable search profiles, hard rules, warnings, positive signals, versioned evaluations, candidate lists, and side-by-side comparison.

## 4. Automatic discovery

- Enable scheduled searches only after approved marketplace access is available.
- Track runs, deduplicate listings, detect changes, and evaluate new or updated candidates.

## 5. AI review

- Add the GPT-5.6 Luna Responses API integration with Structured Outputs and deterministic fallback behavior.
- Review manual/URL candidates and automatic-search finalists.
- Add separately requested, cited web research.

## 6. Image review and refinement

- Add bounded image selection and vision review for visible risks.
- Evaluate prompts and model results against a curated set of known listings.
- Add cost dashboards, prompt/version tracking, and quality metrics.
