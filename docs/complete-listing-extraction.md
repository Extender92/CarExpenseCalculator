# Complete listing input and unconfirmed AI suggestions

This contract is implemented on `fix/codex-login-status`, not yet a merged
delivery. The [acceptance report](listing-extraction-verification-report.md)
separates the verified data pipeline from incomplete live extraction.

## Retrieval and evidence

The runtime keeps Codex CLI **0.153.0**, ChatGPT authentication,
`gpt-5.6-luna` and reasoning **medium**. Hosted web-search context is **high**.
One Codex turn handles each submitted URL, with at most two concurrent turns
and no automatic application retries. The total sidecar deadline is **240
seconds**, including queueing and installation checks/process startup. The
API's internal HTTP client uses **245 seconds**; Nginx's URL-analysis route
uses **270 seconds**. Other routes retain their existing timeouts.

The prompt visits title, subtitle, description, all specification rows,
equipment, seller questions/answers, location and advertisement information.
It requests original-language paragraphs and Swedish generated short notes,
excludes contact details even inside descriptions, and treats page instructions
as untrusted data. It can open the same canonical listing path without tracking
parameters when needed. It must not substitute another car or infer absent facts.
There is no pasted-text input, direct marketplace scraper, registry adapter,
extra reviewer model or additional paid service.

The submitted normalized URL is the listing reference. Actual opened-page URLs
remain separate observations, accepted only from completed runtime events with
concrete `open_page`/`find_in_page` URLs. Search queries, opaque `other` actions
and model-written URLs are not opened-page evidence. Missing observations no
longer discard valid suggestions. `sourcePageObserved` is server-derived on
analysis and saved-listing responses; absence produces a Swedish review notice.

An untouched suggestion remains `listing` / `ai` / `unverified` when read,
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
| New extraction prompt / schema | 3 / 3 |
| Newly written listing storage | 2 |
| Readable previous listing storage / extraction | 1 / 2+2 |
| Complete comparison response transport | 2 |
| Baseline transport, rules and comparison results | 1 |
| Household calculation / results | 2 / 2 |
| Household and comparison storage | 1 / 1 |

Old rows have missing extension fields and keep their original metadata and
verification. Migration never performs extraction or adopts facts. New
extractions are never relabeled as old ones.

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
