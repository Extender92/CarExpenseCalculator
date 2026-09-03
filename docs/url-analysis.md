# URL analysis specification

## Status and purpose

This document defines the target behavior for milestone 2. The dependency-free
Core listing domain, private Codex extraction sidecar, provider-neutral
Infrastructure adapter, unsaved public HTTP endpoint, and Swedish in-memory
review interface are implemented. Saved-listing persistence is not implemented
yet.

URL analysis is a user-triggered ingestion aid. It accepts public listing URLs,
uses a private ChatGPT-authenticated Codex sidecar with hosted web search to
extract a bounded structured draft, and lets the user correct or complete that
draft. Deterministic normalization and validation remain authoritative.
Extraction is separate from the advisory AI review planned for milestone 5.

The backend and browser never fetch listing pages directly. There is no
marketplace-specific scraper or parser.

## User flow and request scheduling

- The Swedish interface accepts 1–10 unique listing URLs.
- Each URL is analyzed independently with one
  `POST /api/listing-analyses` request.
- The browser allows at most two analysis requests in flight. Remaining items
  stay queued and one failed item does not cancel another.
- The API and Codex sidecar also enforce a process-wide extraction concurrency
  limit of two.
- Failed, partial, or unavailable extraction leaves the listing editable so the
  user can complete it manually.
- Preview analysis never reads from or writes to PostgreSQL.
- Reviewed drafts live only in React memory and disappear on navigation or
  reload. A separate manual-draft action remains available when extraction is
  unconfigured or unavailable.

## URL normalization and validation

### Accepted input

The application trims surrounding whitespace and then requires an absolute
`http` or `https` URL. Both the trimmed submitted form and normalized form must
be no longer than 2,048 characters.

Normalization performs these operations in order:

1. Parse the URL using standards-compliant absolute-URI parsing.
2. Lowercase the scheme.
3. Convert an internationalized host to its IDNA ASCII form and lowercase it.
4. Remove port `80` from HTTP and port `443` from HTTPS. Retain every other
   explicit port.
5. Remove the fragment, including its leading `#`.
6. Use `/` when the parsed path is empty.
7. Preserve the parsed escaped path and query contents, order, and casing. Do
   not decode path segments or reorder query parameters.

The normalized URL remains the value shown, sent to the extractor, and retained
with a saved listing. Batch uniqueness uses a separate page identity. Page
identity ignores query and fragment, treats one trailing slash as equivalent,
and treats default-port HTTP and HTTPS forms as the same page. Non-default ports
remain distinct and require the same scheme and port. Host and remaining path
comparison is ordinal after normalization. For example, `/item/123` and
`/item/123?ci=2` are duplicate batch entries even though their complete
normalized URLs differ.

### Rejected input

Reject the URL before any extraction request when it:

- is relative, malformed, or uses a scheme other than HTTP or HTTPS;
- contains a username, password, or any other user-information component;
- exceeds the length limit before or after normalization;
- has no host;
- uses `localhost`, a subdomain of `localhost`, a `.local` host, or a subdomain
  of a `.local` host;
- uses an IP literal that is not globally routable unicast or falls within the
  conservative special-use deny lists below; or
- duplicates another page identity in the same browser submission.

For IPv4 literals, the rejected non-global ranges include at least:

| Purpose | Range |
| --- | --- |
| Unspecified/current network | `0.0.0.0/8` |
| Private | `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` |
| Shared address space | `100.64.0.0/10` |
| Loopback | `127.0.0.0/8` |
| Link-local | `169.254.0.0/16` |
| Protocol/reserved | `192.0.0.0/24`, `192.88.99.0/24` |
| Documentation | `192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24` |
| Benchmarking | `198.18.0.0/15` |
| Multicast | `224.0.0.0/4` |
| Reserved/broadcast | `240.0.0.0/4` |

For IPv6 literals, use this conservative deny list:

| Purpose | Range |
| --- | --- |
| Unspecified | `::/128` |
| Loopback | `::1/128` |
| IPv4-mapped | `::ffff:0:0/96`; classify the embedded IPv4 address as well |
| Translation | `64:ff9b::/96`, `64:ff9b:1::/48` |
| Discard-only | `100::/64` |
| IETF protocol assignments | `2001::/23` |
| Documentation | `2001:db8::/32`, `3fff::/20` |
| 6to4 | `2002::/16` |
| AS112 special service | `2620:4f:8000::/48` |
| Segment-routing SIDs | `5f00::/16` |
| Unique-local | `fc00::/7` |
| Deprecated site-local | `fec0::/10` |
| Link-local | `fe80::/10` |
| Multicast | `ff00::/8` |

IPv4-mapped IPv6 cannot bypass the IPv4 rules.

The backend does not resolve DNS during validation. This avoids introducing a
direct network fetch and DNS-dependent validation. The accepted design rejects
private or reserved IP literals; it does not claim that a hostname will always
resolve publicly when Codex later searches it.

### Returned-source matching

Every returned source URL is normalized using the same rules. Source matching
uses page identity, ignores query and fragment, and treats one trailing slash as
equivalent. A default-port HTTP submission may match its HTTPS upgrade, but an
HTTPS submission never matches an HTTP downgrade. When either URL has a
non-default port, scheme and port must both match exactly. Host and remaining
path comparison stays ordinal.

The response retains the complete ordered source-URL list reported by Web
Search and marks every entry with `matchesSubmittedUrl`. Source titles are not
retained. If no source matches, every extracted value is discarded and the
analysis is `unavailable`, even when the model returned plausible data.

## Analysis status

`ListingAnalysisStatus` is a closed enum:

| Value | Meaning |
| --- | --- |
| `complete` | A source matches and registration number, price, make, model, model year, and odometer are all populated. |
| `partial` | A source matches and at least one usable externally sourced fact is populated, but one or more essential fields are missing. |
| `unavailable` | No source matches or no usable externally sourced fact remains after normalization. |

`unavailable` is a successful HTTP 200 preview result when the Codex turn itself
succeeded. Runtime or configuration failures use the typed HTTP errors
defined below.

Structurally invalid JSON or a response that violates the strict extraction
schema is a runtime failure. A structurally valid individual value that fails Core
normalization is discarded, reported as missing, and does not discard other
valid facts.

## Provenance model

Every populated field carries provenance. Missing fields are `null` rather than
wrappers containing null values.

```text
SourcedValue<T>
  value: T
  provenance: FieldProvenance

SourcedCollection<T>
  values: ordered read-only list<T>
  provenance: FieldProvenance

FieldProvenance
  origin: listing | user | registry
  extractionMethod: ai | manual
  verification: unverified | userConfirmed | registryVerified
  sourceUrl: normalized absolute URL
```

The response-level `analyzedAtUtc` is the retrieval timestamp for every
AI-extracted field in that analysis. It is stored with a saved current listing.

`registry` and `registryVerified` are reserved for a later approved registry
integration. They are not produced in milestone 2.

An AI-extracted field is `listing`, `ai`, and `unverified`. Editing a scalar,
structured value, or collection replaces that complete value's provenance with
`user`, `manual`, and `userConfirmed`. Its source URL remains the normalized
listing URL that the user was reviewing. Untouched values keep their original
listing provenance. Collection provenance applies to the complete collection,
which permits a known-empty collection to remain different from an unknown
`null` collection.

Those are the only active provenance combinations in milestone 2. The reserved
registry values exist in the closed enums but fail validation until an approved
registry integration defines how they are created. Every accepted provenance
source is normalized back to the submitted listing URL; other returned URLs
remain only in the ordered response source list.

The optional vehicle label is user-owned. The extraction adapter returns it as
`null`; a user-supplied label receives manual provenance and never contributes a
missing-field code.

## Listing draft

`ListingDraft` is dependency-free and contains only bounded structured values.

### Identity

| Field | Type | Notes |
| --- | --- | --- |
| `registrationNumber` | `SourcedValue<string>?` | Uses the existing normalized ordinary Swedish registration-number rules. |
| `make` | `SourcedValue<string>?` | Manufacturer. |
| `model` | `SourcedValue<string>?` | Model family. |
| `variant` | `SourcedValue<string>?` | Trim level, engine, or other advertised variant. |
| `modelYear` | `SourcedValue<int>?` | Model year, not first-registration year unless the source explicitly equates them. |
| `vin` | `SourcedValue<string>?` | Uppercase advertised VIN; do not infer missing characters. |
| `vehicleLabel` | `SourcedValue<string>?` | User-owned model or nickname; never generated by extraction. |

### Advertisement

| Field | Type | Notes |
| --- | --- | --- |
| `priceSek` | `SourcedValue<decimal>?` | Advertised vehicle price in SEK. |
| `odometerKilometres` | `SourcedValue<decimal>?` | Core and HTTP use kilometres; the Swedish UI may display mil. |
| `sellerType` | `SourcedValue<SellerType>?` | `private` or `dealer`; unknown stays null. |
| `location` | `SourcedValue<string>?` | General advertised place, never a street or seller address. |
| `publishedDate` | `SourcedValue<DateOnly>?` | Date only. |
| `updatedDate` | `SourcedValue<DateOnly>?` | Date only. |
| `imageCount` | `SourcedValue<int>?` | Count only; images and image URLs are not retained. |

### Technical data

| Field | Type | Notes |
| --- | --- | --- |
| `fuelTypes` | `SourcedCollection<FuelType>?` | Multiple values represent hybrid or multi-fuel vehicles. |
| `transmission` | `SourcedValue<Transmission>?` | `manual` or `automatic`. |
| `drivetrain` | `SourcedValue<Drivetrain>?` | Driven axle layout. |
| `bodyType` | `SourcedValue<BodyType>?` | Closed body-style enum. |
| `colour` | `SourcedValue<string>?` | Advertised exterior colour. |
| `horsepower` | `SourcedValue<int>?` | Metric horsepower as advertised. |
| `engineDisplacementCubicCentimetres` | `SourcedValue<decimal>?` | Explicit cubic centimetres. |
| `energyConsumptions` | `SourcedCollection<EnergyConsumption>?` | At most two entries. |
| `annualVehicleTaxSek` | `SourcedValue<decimal>?` | Annual SEK amount. |

`EnergyConsumption` contains a normalized label, one of the existing
`litre|kilowattHour|kilogram` units, and decimal consumption per 100 kilometres.
Hybrid is not a boolean. A hybrid vehicle uses multiple `fuelTypes` and, when
advertised, multiple consumption entries.

Closed `FuelType` values are `petrol`, `diesel`, `electricity`, `ethanol`,
`biogas`, `naturalGas`, `liquefiedPetroleumGas`, `hydrogen`, and `other`.
`Drivetrain` values are `frontWheelDrive`, `rearWheelDrive`, and
`allWheelDrive`. `BodyType` values are `sedan`, `hatchback`, `wagon`, `suv`,
`coupe`, `convertible`, `minivan`, `pickup`, `van`, and `other`.

### History

| Field | Type | Notes |
| --- | --- | --- |
| `ownerCount` | `SourcedValue<int>?` | Count only; never owner identity. |
| `firstRegistrationDate` | `SourcedValue<DateOnly>?` | Full date only when shown by the source. |
| `lastInspectionDate` | `SourcedValue<DateOnly>?` | Most recent inspection date shown. |
| `nextInspectionDate` | `SourcedValue<DateOnly>?` | Next inspection or due date shown. |

### Buying signals

| Field | Type | Notes |
| --- | --- | --- |
| `towBar` | `SourcedValue<bool>?` | Null means unknown; false means explicitly known absent. |
| `equipment` | `SourcedCollection<string>?` | Up to 100 short equipment names. |
| `sellerClaims` | `SourcedCollection<string>?` | Up to 20 short paraphrased factual claims. |
| `conditionNotes` | `SourcedCollection<string>?` | Up to 10 short paraphrased visible-condition notes. |

Seller claims remain unverified and must not be rewritten as confirmed facts.
Condition notes must be short paraphrases, not copied listing descriptions.

### Validation bounds

| Value | Accepted range |
| --- | --- |
| Money | SEK 0 through 100,000,000 |
| Model year | 1886 through 2100 |
| Odometer | 0 through 10,000,000 km |
| Horsepower | 1 through 10,000 |
| Engine displacement | 1 through 100,000 cm³ |
| Energy consumption | Greater than 0 through 10,000 units/100 km |
| Owner count and image count | 0 through 10,000 |
| Equipment | At most 100 entries |
| Seller claims | At most 20 entries, 200 characters each |
| Condition notes | At most 10 entries, 300 characters each |
| Energy-consumption entries | At most two |

General labels and equipment entries are trimmed, normalized to Unicode NFC,
and limited to 100 characters. VIN is trimmed, uppercased, Unicode-normalized,
and limited to 50 characters because this version does not assume every
historical vehicle has a modern 17-character VIN.

After trimming and NFC normalization, duplicate entries in any closed or string
collection are invalid using ordinal case-insensitive comparison. Input order is
otherwise preserved. Decimal values are never converted through `double`.

For AI extraction, an invalid scalar or structured field is discarded without
discarding other valid fields. Invalid collection entries and later normalized
duplicates are discarded individually while the first valid entry and order are
retained. An originally empty collection remains known-empty; a non-empty
collection that loses every entry becomes unknown. Reviewed manual input instead
accumulates every error and rejects the complete draft without returning a
partial result.

## Missing fields

`ListingFieldCode` is a stable closed enum returned in the order below whenever
the corresponding externally sourced field is null:

1. `registrationNumber`
2. `make`
3. `model`
4. `variant`
5. `modelYear`
6. `vin`
7. `priceSek`
8. `odometerKilometres`
9. `sellerType`
10. `location`
11. `publishedDate`
12. `updatedDate`
13. `imageCount`
14. `fuelTypes`
15. `transmission`
16. `drivetrain`
17. `bodyType`
18. `colour`
19. `horsepower`
20. `engineDisplacementCubicCentimetres`
21. `energyConsumptions`
22. `annualVehicleTaxSek`
23. `ownerCount`
24. `firstRegistrationDate`
25. `lastInspectionDate`
26. `nextInspectionDate`
27. `towBar`
28. `equipment`
29. `sellerClaims`
30. `conditionNotes`

Vehicle label is deliberately absent from this enum. Explicit numeric zero,
false, and a known-empty `SourcedCollection` do not produce missing codes.

## Codex extraction boundary

The Infrastructure adapter uses a typed internal `HttpClient` to a private
ASP.NET Core `codex-extractor` sidecar. The sidecar runs one non-interactive
`codex exec` turn per URL with a saved ChatGPT login. It does not call the
Platform API with an application API key.

Each request uses this policy:

| Setting | Required value |
| --- | --- |
| Model | `gpt-5.6-luna` by default; optional server-only override through `CODEX_MODEL` |
| Reasoning | `medium`, configurable through `CODEX_REASONING_EFFORT` |
| Search | Live hosted web search |
| Search context | Medium |
| Domain filter | One allowed domain derived from the submitted normalized host, without scheme or path |
| Source evidence | Completed JSONL web-search events that opened a page |
| Output | JSONL events and a final response constrained by versioned JSON Schema |
| Session | Ephemeral, with no local rollout persistence |
| Isolation | Read-only empty working directory, no repository, user instructions, project rules, plugins, apps, MCP servers, agents, or unrelated tools |
| Timeout | 60 seconds for queueing, process startup, search, and parsing |
| Concurrency | Process-wide maximum of two Codex turns |
| Retries | None automatically |
| Search-event limit | None; the one turn is bounded by timeout and concurrency |

The pinned invocation is recorded as `requestedModel`. This is configuration
evidence and does not claim to prove provider-side routing because the current
Codex JSONL event contract contains no provider-reported model identifier.
Prompt and extraction-schema versions begin at `1` and are returned with every
structurally successful Codex response.

The instruction treats all page material as untrusted data and says to ignore
instructions embedded in the page. It requests only supported listing facts and
rejects contact details, seller identities, addresses, cookies, hidden-page
content, recommendations, purchase conclusions, and unsupported inference.
Missing values must be null.

Codex receives the normalized listing URL and the sidecar request. It does not
receive authentication data as prompt content, database credentials, unrelated
application data, or the browser's direct connection. Authentication state,
URL-bearing prompts, JSONL output, final structured output, and credentials are
never logged.

The [Codex authentication guide](https://learn.chatgpt.com/docs/auth) documents
ChatGPT sign-in, headless device-code login, and the secret authentication
cache. [Non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)
documents ephemeral execution, read-only sandboxing, JSONL, isolated
configuration, and JSON Schema output. The [web-search guide](https://learn.chatgpt.com/docs/web-search)
and [configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)
document live search, domain filters, and context size. The complete sidecar,
authentication, source-evidence, isolation, and test decisions are in
[Codex listing extraction](codex-extraction.md).

Hosted search does not guarantee that a page is accessible, that its facts are
correct, or that a source permits a particular use. The feature must respect
applicable source terms and show unavailable/manual fallback instead of working
around source restrictions.

## Unsaved HTTP contract

### `POST /api/listing-analyses`

`ListingAnalysisRequest` contains one required `url` string. A browser batch is
not an API batch; the frontend calls the endpoint separately for every URL.

A successful `ListingAnalysisResponse` returns HTTP 200 with:

```text
submittedUrl: string
normalizedUrl: string
status: complete | partial | unavailable
analyzedAtUtc: UTC timestamp
requestedModel: string
promptVersion: integer
schemaVersion: integer
sources: ordered ListingAnalysisSource[]
listing: ListingDraft
missingFields: ordered ListingFieldCode[]
```

`ListingAnalysisSource` contains only a normalized `url` and
`matchesSubmittedUrl`. `requestedModel`, `promptVersion`, and `schemaVersion`
are required even when the analysis result is unavailable. `requestedModel`
records configured request intent rather than provider-reported routing.

Semantic URL and manually entered Core validation failures return HTTP 400
`ValidationProblemDetails` using deterministic camel-case/indexed paths.
Malformed JSON and missing required properties use ASP.NET Core's standard
validation problem response.

Extraction failures use `application/problem+json` and these stable codes:

| Status | Code | Meaning |
| --- | --- | --- |
| 429 | `listingAnalysisRateLimited` | Codex or ChatGPT rate limited the turn. No retry duration is returned because the runtime has no reliable value. |
| 503 | `listingAnalysisNotConfigured` | The sidecar, ChatGPT authentication, or configured Codex model is unavailable. |
| 503 | `listingAnalysisTimedOut` | The complete operation exceeded 60 seconds. |
| 503 | `listingAnalysisProviderUnavailable` | Sidecar connection, process, Codex service, or runtime availability failure. |
| 503 | `listingAnalysisInvalidProviderResponse` | JSONL or the final structured response cannot satisfy the contract. |

Unexpected failures remain generic HTTP 500 problem responses without internal
details. Codex failure never changes the behavior of manual calculations or
manually edited listing drafts.

`GET /api/system/status` includes:

```text
integrations:
  codexListingExtractionConfigured: boolean
```

The API bounds this non-paid check to two seconds. Timeout, unavailable or
invalid sidecar status, and incompatible runtime configuration report `false`.
A `true` value verifies internal sidecar reachability, valid owned
configuration, and a locally recognized saved ChatGPT login without starting a
search turn. It does not guarantee remote service or model availability. Overall
`healthy|degraded` remains based on the existing database readiness behavior.
`features.urlAnalysis` is `true` because the complete unsaved Swedish interface
and manual fallback are implemented. This availability does not imply that the
optional Codex integration is configured. `features.aiReview` remains false
until milestone 5.

## Saved-listing HTTP lifecycle

The future saved-listing API exposes:

- `POST /api/saved-listings`
- `GET /api/saved-listings`
- `GET /api/saved-listings/{vehicleId}`
- `GET /api/saved-listings/by-registration/{registrationNumber}`
- `PUT /api/saved-listings/{vehicleId}`
- `DELETE /api/saved-listings/{vehicleId}?expectedRevision={revision}`

`CreateSavedListingRequest` contains a required `registrationNumber` and a
required `listing` of type `ReviewedListingInput`.
`ReplaceSavedListingRequest` contains a required positive `expectedRevision`
and the same required `listing`. `ReviewedListingInput` contains the submitted
URL, analysis timestamp, nullable extraction model/prompt/schema metadata, ordered
sources, and the reviewed `ListingDraft`. The server recomputes normalized URL,
source matches, missing codes, and analysis status; callers cannot submit those
as trusted results.

`SavedListingResponse` contains:

```text
vehicleId: UUID
registrationNumber: normalized string
revision: positive aggregate revision
listingVersion: positive current-listing version
listingSchemaVersion: integer, initially 1
createdAtUtc: UTC timestamp
updatedAtUtc: UTC timestamp
analyzedAtUtc: UTC timestamp
submittedUrl: string
normalizedUrl: string
status: ListingAnalysisStatus
requestedModel: string?
promptVersion: integer?
schemaVersion: integer?
sources: ordered ListingAnalysisSource[]
listing: ListingDraft
missingFields: ordered ListingFieldCode[]
hasSavedCostScenario: boolean
savedCostScenarioOutdated: boolean
```

`SavedListingSummaryResponse` contains vehicle identity/revisions, optional
vehicle label, make, model, model year, price, odometer, status, missing-field
count, update timestamp, `hasSavedCostScenario`, and
`savedCostScenarioOutdated`. List results are ordered by `updatedAtUtc`
descending and then UUID, with no pagination in the local-only version.

Create returns HTTP 201 with a UUID `Location`; reads and replacement return
HTTP 200; deletion returns HTTP 204. Formatted registration lookup normalizes
through the existing Core value object before querying.

Create requires a valid normalized ordinary Swedish registration number and a
reviewed listing draft. If the draft also contains registration-number
provenance, its normalized value must equal the aggregate registration number.
Extraction metadata may be null for a manually completed listing created while
extraction was unavailable. AI provenance requires matching source and extraction
version metadata.

Requests never accept a vehicle UUID, aggregate revision override, listing
version, listing schema version, status, missing codes, database timestamps, or
raw extractor content. PUT cannot change registration number.

Duplicate registration returns `409 registrationNumberConflict` with the
existing UUID and current aggregate revision; create never overwrites. PUT uses
the vehicle UUID and positive expected aggregate revision, keeps registration
immutable, and fully replaces the current listing. A stale revision returns
`409 revisionConflict` with expected and actual revisions. Missing resources
return `404 savedListingNotFound`, and unsupported stored versions return
`409 unsupportedSavedListingVersion`.

DELETE permanently removes the complete vehicle aggregate, including its current
listing and any saved calculation. The Swedish UI must warn about both before
issuing the request. There is no soft delete, restore, patch, upsert, batch,
append-only history, or automatic refresh in milestone 2.

## Persistence model

PostgreSQL continues to use `vehicles` as the aggregate root. A vehicle may be
listing-only, scenario-only, or contain both current records.

| Table | Planned responsibility |
| --- | --- |
| `vehicles` | Existing UUID, immutable normalized registration, label, aggregate revision, and timestamps. |
| `vehicle_listings` | One current listing, analysis/source version metadata, typed rule-relevant values, bounded provenance/claims/notes/consumption JSONB, listing version, and timestamps. |
| `listing_sources` | Ordered normalized source URLs and submitted-page match flag. |
| `listing_equipment` | Ordered normalized equipment entries. |

The public `revision` is the vehicle aggregate optimistic-concurrency revision
and changes after any aggregate write. `listingVersion` starts at 1 and changes
only when the current listing is created or fully replaced. A listing replacement
increments both. Updating only a saved scenario increments aggregate revision
without changing `listingVersion`.

Full replacement is atomic: validate, verify aggregate revision, replace scalar
and JSONB values, delete/recreate ordered listing children, increment revisions,
and update timestamps. It never retains superseded values or raw Codex output.
Permanent vehicle deletion cascades through listing and calculation
children without orphans.

Existing saved-scenario queries continue to return only vehicles that have a
scenario. A future explicit scenario operation may attach a scenario to a
listing-only vehicle rather than creating a duplicate registration. In the
opposite direction, an explicit saved-listing PUT may attach the first listing
to an existing scenario-only vehicle using its UUID and current aggregate
revision. Neither operation creates a second vehicle for the same registration
number.

## Manual-calculator relationship

A saved scenario created from a listing stores nullable
`sourceListingVersion`. When it differs from the vehicle's current
`listingVersion`, the calculation and stored result remain unchanged but are
reported as outdated. No automatic recalculation occurs. The user clears the
outdated state only by reviewing and explicitly saving the scenario against the
current listing version.

Manual-only scenarios store no source listing version and are unaffected by
listing replacement.

`Skapa kalkyl från bilen` may prefill only:

- vehicle label;
- normalized registration number;
- advertised purchase price;
- annual vehicle tax; and
- each advertised energy-consumption value and unit.

It never maps odometer to annual driving distance. Distance, unit prices,
insurance, maintenance/repairs, financing, residual value, and other unknown
assumptions remain empty. The user reviews all values before previewing or
saving a calculation.

## Privacy and retention

Retain only current structured values needed for review, rules, comparison, and
calculator prefilling. Do not retain:

- complete listing descriptions or copied page text;
- HTML, cookies, images, or image URLs;
- seller names, phone numbers, email addresses, street addresses, or other
  contact data;
- hidden or inaccessible page content;
- raw Codex prompts, events, or responses; or
- analysis history superseded by a current replacement.

The ordered concrete opened-source URL list, current structured facts, field
provenance, requested model, prompt/schema versions, and timestamps are retained
only when the user saves the listing.

## Explicit exclusions

Milestone 2 does not include direct scraping, browser extensions, scheduled or
automatic discovery, background refresh, marketplace-specific parsing,
registry integration, rule evaluation, comparison, broad model research,
advisory purchase recommendations, or image analysis.

Rule evaluation and comparison belong to milestone 3. Automatic discovery
belongs to milestone 4 and still requires approved marketplace access. Advisory
AI review and separately requested cited research belong to milestone 5. Image
review belongs to milestone 6.

## Required verification scenarios

Later implementation issues must cover at least:

- normalization of casing, IDNA hosts, default/non-default ports, empty paths,
  fragments, escaped paths, and preserved queries;
- every rejected hostname and IPv4/IPv6 category, including mapped IPv4;
- unique and duplicate URL batches, one through ten items, and concurrency of
  no more than two;
- page-identity duplicates, query-insensitive matching, one-trailing-slash
  equivalence, directional HTTP-to-HTTPS matching, strict non-default ports,
  and no requested-page source;
- complete, partial, unavailable, structurally invalid, and individually
  discarded values;
- null, explicit zero/false, known-empty collections, stable missing-code order,
  collection bounds, duplicates, and provenance transitions;
- Codex invocation shape, source-event parsing, timeout, no retries, process concurrency, errors,
  configuration reporting, and absence of secrets or raw response logs;
- preview operation with an unreachable database;
- listing-only, scenario-only, and combined persistence; atomic replacement;
  aggregate/listing revisions; conflicts; and cascade deletion;
- safe calculator prefilling and outdated versus manual-only calculations; and
- fake-extractor Compose and browser flows without live Codex calls or ChatGPT
  usage.
