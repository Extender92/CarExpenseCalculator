# Listing text retrieval and interpretation probe

> Integration follow-up: the approved direct-retrieval flow is now implemented
> and four application live checks passed. See the [current acceptance report](listing-extraction-verification-report.md)
> and [final field matrix](listing-scraper-live-matrix.md). The probe results below
> are preserved as the earlier diagnostic record.


Date: 2026-09-10. Status: **diagnostic experiment, not completed URL acceptance**.
Tested implementation: `f15a02944ec84af481a920576ab340d0f99bbacc` on
`fix/codex-login-status`; [draft PR #93](https://github.com/Extender92/CarExpenseCalculator/pull/93).
The experiment changes no application code, public contract, database, runtime
configuration or verification level. It does not implement a pasted-text UI.

**Latest user direction:** the application should retrieve/scrape the page
itself and then supply the content to AI. Manual copying/pasting was not the
intended product flow. The [direct-HTML follow-up](#follow-up-direct-html-retrieval)
below succeeded for both reference advertisements; application integration is
still outstanding.

## Conclusion

Separating AI transcription from schema interpretation did **not** produce
complete URL content. The Audi transcription still omitted three specification
rows; the Skoda transcription returned no content. Passing that text to a second
model cannot recover the absent facts without another source or guessing.

Supplying the complete user-provided text directly, with web search disabled,
did recover every predeclared field/membership check when the existing
application's field instructions were reused: **65/65 Audi and 62/62 Skoda**.
These totals include 8 and 12 expected-unknown checks respectively. They are not
a claim that every model output string was exact: the separate equipment-list
check failed for Skoda because a winter-wheel item from its description was
also added to the 15-row equipment list. The Audi list matched exactly.

At this earlier checkpoint, the evidence supported considering explicit manual text input as a fallback;
it does not establish a reliable complete automatic fetcher or a finished
text-input product feature. Any future plan needs to retain original supplied
text separately, preserve section boundaries and unconfirmed provenance, and
validate interpretation before applying fields. One successful field control
per car is not a reliability guarantee across advertisements or repeated runs.

See the [field matrix](listing-text-retrieval-probe-matrix.md), the original
[live acceptance report](listing-extraction-verification-report.md), and the
exact [Audi](../tests/fixtures/listings/audi-a4.json) and
[Skoda](../tests/fixtures/listings/skoda-roomster.json) references.

## Method and isolation

- CLI **0.153.0**, Node **22.22.2**, `gpt-5.6-luna`, reasoning **medium**,
  existing ChatGPT login, and a **240-second** total per-call deadline.
- Eight explicitly initiated calls ran sequentially: two URL transcription
  calls and six text-only controls. No automatic retries or additional model
  provider were introduced. The model was not changed between experiments.
- Disposable containers used the already-built extraction image
  `car-expense-listing-live-codex-extractor:latest` (image ID `38668c4e5c96`).
  No API, Nginx or database stack was required or modified.
- The existing authentication volume was mounted read-only. Only its login
  file was copied to the container's private tmpfs; no login data entered
  host helper files, prompts, reports or output.
- The CLI ran as UID 1654, with an empty work directory, read-only filesystem,
  dropped capabilities, ephemeral execution and ignored user/project rules.
  Shell, apps, image tools and additional agents remained disabled.
- Host helper files and experimental output were confined to the ignored
  `temp/listing-text-probe/` directory. No new package installation was needed.

### URL-only transcription

Each call received only its URL with `?ci=3` and generic transcription
instructions. Neither the references nor expected values were supplied:

- [Audi URL](https://www.blocket.se/mobility/item/26427275?ci=3)
- [Skoda URL](https://www.blocket.se/mobility/item/26434732?ci=3)

Hosted search remained live, high-context and restricted to `www.blocket.se`.
The temporary output schema contained title, subtitle and six original-text
sections: description, specifications, equipment, seller questions, location
and advertisement information. Each section had `read`, `notFound` or
`unavailable` status plus nullable text. The prompt requested original labels,
units and paragraphs without normalization, summary or vehicle inference.
Contact and navigation content were excluded. Open/find could inspect only the
same advertisement, including its canonical path without query parameters.

This is **model-produced transcription**, not a direct capture of the web
tool's internal response. Missing text proves that the downstream interpreter
would lack that input. It does not by itself distinguish a truncated search
response from the first model omitting accessible content. The model's access
explanation and section-status claims are not independent source evidence.

### Text-only controls

The original user-provided advertisement text was supplied as text, including
original labels, values, complete descriptions, equipment and seller answers.
Navigation/contact boilerplate was omitted. Expected normalized JSON fixtures
were kept outside the model inputs. No new browser retrieval was used to build
these controls; this tests supplied text, not a new manual copying workflow.

All six controls had `web_search="disabled"` and emitted **zero web-search
events**. They used the unchanged extraction schema v3. Three prompt variants
were tested in order, with one fresh call per car for each variant:

1. A shorter experimental text-interpretation prompt.
2. That prompt with explicit local-timestamp formatting and the distinction
   between an unspecified weight category and a missing amount.
3. The existing production field instructions, copied from
   [ListingExtractionPrompt.cs](../src/backend/CarExpenseCalculator.CodexExtractor/ListingExtractionPrompt.cs).
   Only URL retrieval instructions were removed and a text-only wrapper added.
   The blocks beginning `Extract only facts` and `In Swedish specification
   tables` through the output instruction were retained verbatim. No new
   production prompt was implemented.

## Observations

### URL transcription

| Check | Audi | Skoda |
|---|---|---|
| Title/subtitle | Both exact | Both unavailable |
| Price/specification label-value pairs | 23/26 correct, 3 missing | 0/23 available |
| Missing Audi rows | Registration date 1999-11-24; owner count 4; sale form Begagnad bil till salu | All sections unavailable |
| Description | All words retained; blank paragraph separators became single line breaks | Unavailable |
| Equipment | All 18 items, exact order | No items returned |
| Location and advertisement information | Match reference, including ID and update time | Unavailable |
| Seller answers | None returned, consistent with Audi reference | Required debt question/answer unavailable |

The Audi result marked its specification section `read` despite the omitted
rows. Its note said direct opening failed with a cache error and content came
from a live search result. The Skoda note said the page could not be retrieved.
These are model explanations; no particular Blocket blocking mechanism, request
rate or upstream truncation cause was independently established. No second
interpretation was run on these incomplete transcriptions as a supposed repair.

### Interpretation attempts, including failures

| Prompt | Audi field/membership checks | Skoda field/membership checks | Separate equipment-list check |
|---|---|---|---|
| Initial text control | 62 correct, 2 missing, 1 incorrect | 60 correct, 1 missing, 1 incorrect | Both exact |
| Refined text control | 63 correct, 0 missing, 2 incorrect | 61 correct, 0 missing, 1 incorrect | Both exact |
| Existing production field instructions | 65 correct, 0 missing, 0 incorrect | 62 correct, 0 missing, 0 incorrect | Audi exact; Skoda has one extra entry |

The initial controls omitted the applicable `unspecified` weight categories
and formatted local update times with a space instead of `T`. The refined
controls corrected those fields but put amounts into weight-label fields:
Audi `1 370 kg` / `1 300 kg` and Skoda `1 240 kg`, instead of the original headings.
These were experimental-prompt failures, not changes or newly discovered
regressions in the production prompt. All attempts remain in this report.

With the existing field instructions, the full descriptions, all original
equipment items, prices, distances, engine data, owners, VINs, registration and
inspection dates, sale forms, update timestamps and Skoda debt answer matched.
Unknown registration numbers, tax, county, time zones and Skoda consumption
remained unknown. Generic weight/trailer categories were not promoted to
curb/gross or braked weight.

Skoda's final equipment result contained the original 15 entries in order plus
`Vinterdäck på ALU fälg`, taken from the description. It is source-supported but
does not reproduce the equipment section exactly. Consequently, the final
combined field/membership and list-fidelity checks are **128 passed, 1 failed**,
not an unqualified successful acceptance. Generated summaries, variant wording
and duplicate supplementary specifications are reviewed separately from these
counts; they are not fixed-text acceptance criteria. Image counts are unknown
because the supplied texts do not contain them.

## Execution log and validation

All calls exited 0, completed a turn and returned parseable final JSON. No call
timed out or hit the 10 MiB diagnostic output limit. JSON Schema validation
passed for **8/8** outputs using the existing `@redocly/ajv` draft-2020 validator.
This validates structure, not factual correctness or the full application
normalization pipeline; the field matrix separately detects format/content
errors which the schema permits.

| Attempt | Start UTC | Seconds | Completed web-search items |
|---|---|---:|---:|
| Audi URL transcription | 20:50:44.896 | 62.123 | 9 |
| Skoda URL transcription | 20:51:47.022 | 36.276 | 5 |
| Audi initial text | 20:53:50.838 | 24.969 | 0 |
| Skoda initial text | 20:54:15.809 | 24.124 | 0 |
| Audi refined text | 20:56:32.723 | 28.180 | 0 |
| Skoda refined text | 20:57:00.904 | 21.725 | 0 |
| Audi production-field text | 20:58:36.367 | 23.969 | 0 |
| Skoda production-field text | 20:59:00.337 | 22.206 | 0 |

Each CLI invocation emitted 176 stderr bytes. The warning captured in the later
controls says PATH helper aliases cannot be created under the temporary Codex
home. Calls completed using the installed executable and shell helpers were
disabled. Stderr from the initial four attempts was counted but not retained,
so its exact text was not independently checked.

The reference comparison uses decimal JSON parsing, exact field values, exact
description paragraphs, equipment membership plus a separate exact-list check,
and an equivalent consumption label only when it preserves `NEDC` and the exact
unit/amount. Raw specification checks match an original label to its following
value, rather than accepting the same number from an unrelated field.

No application build, backend/frontend test-suite, OpenAPI, API/Nginx or PDF
rerun was performed: application code and contracts are unchanged. This is not
an additional end-to-end live acceptance pass and does not supersede the draft
PR's outstanding URL failures. No commit, push, merge or GitHub change was made
for this experiment.

## Cleanup

All eight disposable probe containers exited and were automatically removed.
Their tmpfs credentials, working files and runtime state were discarded. No
new database volumes, images or persistent stacks were created. Existing
application containers, authentication, user data and earlier deferred cleanup
inventories remain untouched.

The automatic tool policy rejected native PowerShell removal of the verified
work-owned directory with `blocked by policy`. No alternate deletion mechanism
was used. The remaining **40 files, 246,001 bytes** (helpers, reference text,
prompts, responses, metadata and the temporary verification matrix) are all under:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\listing-text-probe\
```

The root Windows Temp inventory showed no entries modified since the start
window, 2026-09-10 20:49 UTC, attributable to this experiment. No .NET build or
SDK workload process was started; Python ran with bytecode writes disabled.
Earlier deferred directories and SDK logs remain outside this cleanup scope.

## Follow-up: direct HTML retrieval

The user subsequently clarified that retrieval must be performed by the
application, rather than asking the user to copy advertisement text. A new,
isolated diagnostic tested that exact sequence: **direct HTTP GET → deterministic
section extraction → one text-only Codex interpretation**. This supersedes the
manual-input recommendation, not the previous observations or their failure log.

### Actual source acquisition

Two sequential, unauthenticated requests used Node 22.22.2 native `fetch`, a
descriptive application user-agent and the original URLs including `?ci=3`.
There was one request per advertisement, no redirect following and no retries.
The diagnostic capped each response at 10 MiB and each fetch at 30 seconds.
Neither request encountered a block, challenge or rate-limit response.

| Advertisement | UTC start | HTTP | HTML bytes | Fetch and initial parse |
|---|---|---:|---:|---:|
| Audi 26427275 | 2026-09-10 21:17:55.017 | 200 | 366,617 | 342 ms |
| Skoda 26434732 | 2026-09-10 21:17:55.360 | 200 | 355,927 | 137 ms |

Both responses were `text/html; charset=utf-8`. The server-rendered HTML already
contained the entire relevant text. Browser rendering and additional asset,
script or marketplace-data requests were unnecessary in this diagnostic.
Response SHA-256 checksums:

```text
Audi  173f0273583263c32559fb6c2a86539874ed9221fd2abbf0722d0ef24da009aa
Skoda 0e275f6155b038f4e3fe63753016642a6f8eb8b35f3ed429e2a954734d9d5a05
```

The existing local JSDOM dependency parsed the captured HTML without executing
scripts or loading resources. The helper selected the title/subtitle, labelled
price, full description, specification definition-list pairs, equipment list,
seller question/answer pairs, postal/locality text and advertisement ID/update
rows. It preserved paragraphs and original measurement labels; non-breaking
spaces were normalized to ordinary spaces. It did not normalize car facts or
infer absent categories.

Navigation, contact panels, advertising, sharing dialogs, map-cookie messages
and specification tooltips were excluded by section selection. The two selected
descriptions contained no contact-pattern matches and were inspected before
model submission. This is not a claim of a generally validated contact-data
filter for every future advertisement.

The captured source passed **34/34 Audi** and **31/31 Skoda** checks against the
references. These counts include exact description and equipment-array checks,
25/22 specification pairs, price, title/subtitle, location, seller answers and
advertisement information. In particular, the three omitted Audi rows were
present and Skoda's debt answer was `Nej`. Reference data was used solely by the
checker, not to fill missing source content.

### Interpretation of the retrieved content

The model received the fresh, deterministically extracted section snapshot,
not the user's reference text or the expected JSON fixture. Codex retained
the same CLI/model/authentication/reasoning configuration and v3 output schema;
web search was disabled. The production field-instruction blocks were reused
with a text-only wrapper, plus an explicit instruction to retain the equipment
array and description separately without adding description-only equipment.
That wrapper exists only in the diagnostic helper, not in the product.

| Advertisement | UTC start | Seconds | JSON Schema | Field and exact-list checks | Web events |
|---|---|---:|---|---|---:|
| Audi | 2026-09-10 21:21:51.705 | 25.454 | Pass | 66/66 pass | 0 |
| Skoda | 2026-09-10 21:22:17.160 | 23.106 | Pass | 63/63 pass | 0 |

Both invocations exited 0 and completed their turn. The same nonfatal 176-byte
temporary-home PATH-alias warning appeared. Neither timed out or exceeded its
output limit. No repeat model calls were required for this follow-up.

All **129** interpretation checks passed, including both exact equipment lists
of **18/15** entries. Of the earlier 127 field/membership checks, 20 explicitly
test that unknown values remain unknown; the two additional checks verify the
entire equipment list. Descriptions, dates, original weight headings and
unspecified categories, consumption units, VINs, owners, update times and the
debt answer matched. See the [appended matrix](listing-text-retrieval-probe-matrix.md#direct-http-follow-up).

This establishes a successful **technical prototype for these two pages**.
It does not establish reliability for changed page layouts, other providers or
future blocked responses. It does not prove the seller's claims against a
vehicle register. No current API route, database or PDF flow used the new
fetcher, and no production code or deployment was changed.

### Remaining integration work

The selected direction is an application-owned fetcher followed by text-only
Codex interpretation. A production implementation still needs:

- URL/host, resolved-address and redirect controls before each network request;
  bounded bytes, cancellation and a queue for page retrieval.
- A tested section parser which reports missing or changed structure explicitly;
  source labels, original text, unknown values and section associations must
  survive interpretation. AI output alone must not certify completeness.
- Integration into the existing sidecar/API flow with actual fetched-source
  metadata, unchanged unconfirmed evidence levels and existing save/review rules.
- Regression tests for parser drift, missing sections, body limits, blocked
  responses, cancellation, hostile page content and source/field fidelity.
- Updated version/configuration documentation and the ordinary backend,
  frontend, Docker, contract, browser and full URL acceptance checks.

The diagnostic has **65 source checks and 129 interpretation checks passed,
0 failed**, plus 2/2 JSON Schema checks. These are probe counts, not additions to
the repository's automated test-suite totals. Previous failed experiments
remain documented. The two direct fetches and two text-only calls are additional
to the eight calls in the earlier execution log.

### Follow-up cleanup

The two new Codex probe containers were automatically removed. No new persistent
volumes, images or application stacks were created. Helpers and raw source
captures were confined to `temp/direct-listing-probe/`; earlier deferred
inventories were not modified. The final resolved path was checked to be a
regular directory under the workspace, with no junction/symlink target.

Native PowerShell `Remove-Item -LiteralPath ... -Recurse -Force` was rejected
by automatic tool policy with `blocked by policy`. No alternate removal method
was attempted. The **26 files, 834,612 bytes** remain at:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\direct-listing-probe\
```

The Windows Temp inventory contained no entries modified since the 21:16 UTC
start window. Host process TEMP/TMP settings were directed to the probe folder
for the fetch. No application build, deployment, commit, push or GitHub change
was performed during the follow-up; document links, UTF-8 and Git whitespace
checks passed. The production branch remains `fix/codex-login-status` with
local report changes only.
