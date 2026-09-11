# Complete Blocket retrieval and unconfirmed listing input

This contract is implemented on `fix/codex-login-status`, not yet a merged
delivery. The [acceptance report](listing-extraction-verification-report.md)
records integration, four live reference checks and prior failed retrieval attempts.

## Retrieval and evidence

The runtime keeps Codex CLI **0.153.0**, ChatGPT authentication,
`gpt-5.6-luna` and reasoning **medium**. Codex web search is **disabled**.
One Codex turn interprets each captured page, with one complete analysis at a time
and no automatic application retries. The total sidecar deadline is **240
seconds**, including queueing and installation checks/process startup. The
API's internal HTTP client uses **245 seconds**; Nginx's URL-analysis route
uses **270 seconds**. Other routes retain their existing timeouts.

The private sidecar implements `IListingPageFetcher` with .NET `HttpClient` and
`IListingContentParser` with **AngleSharp 1.7.0**, without script execution or
resource loading. Only HTTPS port 443 on `blocket.se`/`www.blocket.se` and
`/mobility/item/{digits}` is supported. Query parameters such as `?ci=3` survive.
Before connecting, all DNS results must be public; the socket connects to a
validated literal IP and checks its actual remote address. At most three
redirects are followed, only to that same ad on an allowed host.

The request identifies `CarExpenseCalculator/0.1 (user-requested listing retrieval)`.
Only the HTML document is fetched, within **30 seconds** and **10 MiB of actual
decompressed UTF-8 content**, even without Content-Length. Images, scripts,
maps and contact actions are not fetched. Non-HTML/non-UTF-8, invalid sections,
ambiguous name/value pairs, a mismatched ad ID or oversized content fail explicitly.
Missing sections remain unknown, never automatically confirmed empty.

The immutable `RetrievedListingContent` captures title/subtitle, total price,
original description, all specifications, equipment, seller answers, location
and advertisement information. Menus, advertisements and contact panels are
excluded. Identified email/telephone contact details in descriptions are masked
before interpretation or storage, preserving paragraphs, dates, VINs and numbers.
The model receives only this cleaned material, treating embedded instructions
as untrusted data. It produces typed facts and Swedish generated short notes.
Infrastructure replaces the model's title/subtitle/description/ID/specification,
equipment and seller-answer fields with the captured originals. Description-only
winter tyres cannot silently become an equipment entry. No reference car values
are present in the production parser or prompt.

The seller profile panel contributes only its explicit kind: the Blocket dealer
panel establishes `dealer`, and the identified private-profile panel establishes
`private`. No seller name, contact details or login text enters the captured
content. Missing/unrecognized panels remain unknown; absence of a dealer is not
evidence of a private seller. Conflicting profile kinds produce an invalid-content
error. The application preserves this captured kind as `listing/html/unverified`,
overriding AI suggestions (including an unsupported model guess when the captured
kind is null). This fixes the missing seller type found in the additional Saab
and Opel checks; it does not alter public contracts, schema or storage versions.
An explicit Swedish postcode immediately before the locality in the captured
location row is also preserved with HTML provenance, preventing model omissions.
Ambiguous/missing postcode patterns are not inferred from street numbers or cities.

There is no pasted-text input, discovery, registry adapter, additional model,
alternate paid provider, VPN rotation or challenge bypass.

The submitted URL is the reference; the actually retrieved URL is the observed
source. The internal API requires captured original content bound to that source.
Model-written addresses, searches and opaque actions cannot replace it. The old
JSONL source-event tests remain compatibility coverage. Older saved observations,
including missing page metadata, remain readable with a Swedish review notice.
`sourcePageObserved` is server-derived and does not imply independent verification.

Captured original values use `listing` / `html` / `unverified`; interpreted
values use `listing` / `ai` / `unverified`. Both remain unchanged when read,
saved or adopted. It can satisfy a comparison's `advertised` evidence level,
but never `userConfirmed` or `registryVerified`. Explicit edits retain existing
manual-confirmation semantics. Development acceptance checks do not change
stored evidence. Registry requirements remain future work.

`complete` means the six existing essential fields are available, not that
every page fact has been extracted or independently verified. `partial` retains
usable values with gaps; `unavailable` means no usable vehicle input remains.
Technical failure, invalid structured output, cancellation and timeout remain
separate typed failures. Registration number is required for storage; VIN and
advertisement ID never replace it.

| Source failure | Public HTTP / code |
| --- | --- |
| Unsupported host or URL form | 400 `listingSourceUnsupported` |
| Rate limit | 429 `listingSourceRateLimited`, with Retry-After seconds |
| Access denied or detected CAPTCHA | 503 `listingSourceBlocked` |
| Missing/unreachable advertisement | 503 `listingSourceUnavailable` |
| Invalid content type, HTML, identity, encoding or size | 503 `listingSourceInvalidContent` |

AI configuration, invalid AI output and total timeout retain their existing
distinct errors. No automatic retries occur. The singleton fetcher honors a
429 Retry-After date/duration, using 60 seconds if missing/invalid, and prevents
new source requests during cooldown across browsers. The browser FIFO pauses
on source 429/blocking and requires **Fortsätt kön** explicitly. Pending items
are not sent until the previous analysis ends. Cancellation propagates through
queue, fetch, parsing and the Codex process.

## Typed listing extension

`ListingDraft.Details` is nullable. Scalars use `SourcedValue<T>`; each ordered
specification/question entry has its own provenance. Collections distinguish
unknown (`null`) from explicitly empty (`[]`). Existing listing fields remain
unchanged, including consumption labels such as NEDC and distinct model year,
first registration, last inspection and next inspection dates.

| Fields | Type and limit |
| --- | --- |
| `title`, `subtitle` | Text, 500 characters each |
| `description` | Original paragraphs, 32,000 characters |
| `listingId`, `postalCode`, `country`, `feeClass`, `saleForm` | Text, 100 characters each |
| `seats`, `doors` | Integer, 1–100 / 0–100 |
| `luggageLitres` | Decimal, 0–100,000 litres |
| `weightKilograms` | Decimal, 0–1,000,000 kg |
| `weightLabel`, `weightCategory` | Original label (100 characters); `unspecified`, `curb` or `gross` |
| `trailerWeightKilograms` | Decimal, 0–100,000 kg |
| `trailerWeightLabel`, `trailerWeightCategory` | Original label (100 characters); `unspecified`, `braked` or `unbraked` |
| `updatedLocalDateTime` | Explicit local `YYYY-MM-DDTHH:mm:ss`, without implicit timezone |
| `updatedTimeZone`, `updatedUtcOffsetMinutes` | Optional text (100 characters) / integer −840…840; missing stays missing |
| `specifications` | At most 100 sourced name/value pairs; name 100 and value 1,000 characters |
| `sellerAnswers` | At most 100 sourced question/answer pairs; each text 1,000 characters |

Text is trimmed and NFC-normalized, with description paragraph boundaries
preserved. Content-limit violations produce errors, never silent truncation.
The output schema, Core validation and forms use these limits. Monetary and
distance values remain `decimal`; URL form serialization now uses the existing
lossless numeric adapter for all numbers and revisions, including shared drafts.
JSONL allows 4 MiB per line, 10 MiB total stdout and 1 MiB stderr. Existing 2 MiB
HTTP/storage limits still apply to complete write envelopes; the two reference
listings are far below them. Oversized combinations must fail explicitly.

Generic `Vikt` does not become curb/gross weight. Generic `Max trailervikt`
does not become braked towing capacity. Only explicitly braked, whole-kilogram
values can be offered for that existing criterion. Seats map directly. Next
inspection is not inspection validity, service claims do not establish complete
history, locality does not establish county, and seller debt answers remain
unconfirmed statements. Other details are display-only input, not new criteria.

## Storage and version compatibility

Migration `20260910132449_AddListingDetails` adds nullable JSONB column
`vehicle_listings.details` and updates version constraints. Storage-owned
`DraftListingDetails` DTOs map explicitly to Core and preserve the complete
extension in the shared draft, reopening and atomic adoption. Existing locks,
vehicle/listing revisions and cascading full deletion remain authoritative.
Replacement keeps one current value set; it creates no archive or result cache.

| Contract | Version |
| --- | --- |
| New extraction prompt / schema | 4 / 3 |
| Newly written listing storage | 2 |
| Readable previous listing storage / extraction | 1 or 2 / 2+2 or 3+3 |
| Complete comparison response transport | 2 |
| Baseline transport, rules and comparison results | 1 |
| Household calculation / results | 2 / 2 |
| Household and comparison storage | 1 / 1 |

Old rows have missing extension fields and keep their original metadata and
verification. Migration never performs extraction or adopts facts. New
extractions are never relabeled as old ones.

The separate follow-up `20260910214541_AllowHtmlListingExtraction` changes the
metadata constraint to allow exactly 2/2, 3/3 and 4/3. The earlier details
migration is not rewritten. Rolling back to `20260910132449_AddListingDetails`
rejects prompt-4 metadata or HTML provenance in saved listings, shared drafts
or comparison facts before changing the schema. A compatible backup is needed
to retain such content with an older application; neither version nor source
method is downgraded by relabeling.

Apply migrations only with the explicit API migration command. Before rollback,
stop writes and take a verified backup. Downgrade to
`20260908103211_AddComparisonPersistence` is guarded: any listing in storage
format 2, non-null extension, or new extraction/extension inside a saved draft
rejects the downgrade transaction. Use a compatible backup to return to the
older application when such content must be retained. The rollback never
silently deletes new details or invents older extraction metadata. Compatible
version-1 listings, household data and vehicle identities survive a permitted
down/up cycle.

## Review, comparison and report

URL review exposes all added fields and sourced question/specification entries
for explicit correction. Replacement review shows both complete detail values.
The existing registration requirement, explicit draft replacement and stale
revision protection remain in force.

`POST /api/comparisons/preview-all` includes `listings: SavedListingResponse[]`
once outside `views`. Only vehicles with a saved listing occur in this array;
manual mode returns `[]`. Listings are read from the same `RepeatableRead`
snapshot as costs and facts, before calculation. Their current listing version
is distinct from versions referenced by previously reviewed comparison facts.
The client checks identity, revision, uniqueness and coverage against the
accepted candidate set. Listing import does not change calculator price,
cost confirmation or already reviewed comparison facts.

The comparison's listing section and the PDF report show the complete supplied
listing input, including gaps, provenance and source-page observations. The
report's existing exact clone includes `listings`; opening/printing never reads
another listing. Later server changes cannot alter the captured report. Text is
rendered as text, never HTML, and long descriptions/collections can continue
across printed pages.
