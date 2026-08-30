# Product requirements

## Goal

Help a household find and compare suitable used cars by combining explicit requirements, verified vehicle facts, ownership-cost calculations, and explainable review results.

The application must remain useful without external AI. Deterministic rules and calculations are the source of truth; AI is an additional reviewer.

## Audience and environment

- A small household using the application on a trusted home network.
- Hosted on an Unraid server and reached through `http://extower.local:<port>`.
- Swedish user interface with English code and technical documentation.
- No user accounts, authentication, public internet exposure, or HTTPS in the local-only release.

## Usage modes

### Rule-based search

The user creates a search profile containing hard requirements and softer preferences. Once a permitted listing source is available, a background process can collect candidates, reject hard-rule failures, and rank the remaining cars.

### URL analysis

The user pastes one or more listing URLs. The application imports whatever data an authorized source exposes, clearly marks missing or unverified values, applies the same rules as automatic search, and stores the result for comparison.

### Manual calculation

The user enters vehicle, operating, financing, and usage values directly. Saving is optional; calculations must also work without persistence.

## Shared behavior

- Show the source and verification state of important facts.
- Distinguish hard-rule failures, warnings, positive signals, and missing data.
- Never invent registration numbers, ownership counts, prices, mileage, or vehicle history.
- Permit side-by-side comparison of saved candidates in a later milestone.
- Continue showing deterministic results if AI or another external service is unavailable.

## Current delivery status

The repository foundation and manual-calculator milestone are implemented. The application provides the three-mode navigation, health and status endpoints, deterministic manual calculations, optional PostgreSQL-backed saved scenarios, Docker deployment, tests, CI, and documentation.

Rule-based search, URL analysis, listing ingestion, automatic discovery, comparison, and OpenAI review remain future milestones.
