# Product requirements

## Goal

Help a household find and compare suitable used cars by combining explicit requirements, verified vehicle facts, ownership-cost calculations, and explainable review results.

The application must remain useful without external AI. Deterministic normalization, rules, and calculations are the source of truth. Hosted AI may help extract a user-selected listing or provide a later advisory review, but it never makes a fact authoritative.

## Audience and environment

- A small household using the application on a trusted home network.
- Hosted on an Unraid server and reached through `http://extower.local:<port>`.
- Swedish user interface with English code and technical documentation.
- No user accounts, authentication, public internet exposure, or HTTPS in the local-only release.

## Usage modes

### Rule-based search

The user creates a search profile containing hard requirements and softer preferences. Once a permitted listing source is available, a background process can collect candidates, reject hard-rule failures, and rank the remaining cars.

### URL analysis

The user pastes one through ten public listing URLs. The application analyzes
each URL independently through a source-aware, ChatGPT-authenticated Codex
integration, clearly marks missing and unverified values, permits manual
correction, and can store one current reviewed listing per vehicle. Extraction
failure leaves manual entry available.

Rule evaluation and side-by-side comparison are applied to these saved candidates in milestone 3; they are not part of the URL-ingestion milestone itself.

### Manual calculation

The user enters vehicle, operating, financing, and usage values directly. Saving is optional; calculations must also work without persistence.

## Shared behavior

- Show the source and verification state of important facts.
- Distinguish hard-rule failures, warnings, positive signals, and missing data.
- Never invent registration numbers, ownership counts, prices, mileage, or vehicle history.
- Permit side-by-side comparison of saved candidates in a later milestone.
- Keep manual calculation and manually entered listing data available if AI or another external service is unavailable.

## Current delivery status

The repository foundation and manual-calculator milestone are implemented. The application provides the three-mode navigation, health and status endpoints, deterministic manual calculations, optional PostgreSQL-backed saved scenarios, Docker deployment, tests, CI, and documentation.

The URL-analysis Core domain, private ChatGPT-authenticated Codex extraction
runtime, unsaved public preview endpoint, and Swedish in-memory review interface
are implemented. Listing persistence, rule-based search, automatic discovery,
comparison, and advisory AI review remain future work.
