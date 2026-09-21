# Listing review and calculation workflow (#95)

## Subsequent frontend simplification

Issue #95 was delivered through PR #96. The
[simplified entry and comparison flow](frontend-simplification.md) now supersedes
its presentation and ordinary save-button behavior: new cars receive missing-only
prefills, selected cars can be saved together, and **Spara bil** saves a car's
changed resources sequentially. Existing-car saves retain cost/facts/listing
ordering; new cars first persist the listing, then bind and save cost sources and
facts. Per-step checkpoints preserve partial success without automatic retries.
The #95 API, evidence rules, revisions, storage formats and migrations remain
unchanged. Full advertisement content is retained.

This document describes the implementation for [issue #95](https://github.com/Extender92/CarExpenseCalculator/issues/95).
Delivery evidence and outstanding checks are recorded in the
[verification report](issue-95-verification-report.md). Registry lookup, advisory
AI, automatic discovery and deployment are outside this change.

## Review and editing

The URL workspace keeps the complete advertisement, but puts registration,
make/model, model year, advertised price, mileage, fuel and transmission first.
Owner count and tow bar retain visible evidence. Description, original
specifications, equipment, history and source information have separate
expandable sections. Missing registration is labelled **Saknas för att lägga
till i jämförelsen**; it does not prevent **Spara utkast**.

Saved cars open in **Redigera bil**, with **Annons**, **Kalkyl** and
**Jämförelsefakta** tabs. Shared household inputs and rules have their own
dialogs. Native `dialog.showModal()` supplies modal keyboard behavior; the
shared editor restores focus and page scroll, limits mobile width, and keeps
save/close actions outside the scrolling content. Economic error links identify
the vehicle, section and field; collection paths retain stable row keys.

Ordinary save writes the selected resource. Closing dirty work, Escape,
backdrop clicks and router navigation offer **Spara och stäng**, **Kasta
ändringar**, or **Fortsätt redigera**. Save-and-close processes changed resources
sequentially, using each acknowledged revision. A failure or a later edit keeps
the dialog open. Discard restores the most recent successful save, or the
original analysis for an unsaved listing. Browser reload retains its native
unsaved-work warning. Manual comparison candidates remain browser-local.

The dedicated `/manual/transition` page retains one atomic review of the shared
profile and all legacy cars. Its profile remains in that transaction's form:
an ordinary profile save is prohibited while the transition is pending.
Older `/manual/legacy` addresses remain supported.

Direct saved-car links use `/search?vehicleId=<uuid>&tab=listing|cost|facts`;
saved review drafts use `/analyze-urls?reviewDraftId=<uuid>`. A lost unsaved
analysis cannot be reconstructed from a URL and is explained as missing.

## Registration-free review drafts

`listing_review_drafts` is independent of `vehicles` and of the existing
registration-bound `vehicle_draft` calculation slot. It contains UUID identity,
revision, UTC creation/update times, a normalized page identity, storage version
and typed JSONB containing the complete reviewed listing and extraction metadata.
It has no automatic expiry. Such drafts do not participate in comparison.

| HTTP operation | Behavior |
| --- | --- |
| `GET /api/listing-review-drafts` | Lists current drafts. |
| `POST /api/listing-review-drafts` | Creates `{ input: ReviewedListingInput }`. |
| `GET /api/listing-review-drafts/{id}` | Opens a draft by UUID. |
| `PUT /api/listing-review-drafts/{id}` | Replaces using `{ expectedRevision, input }`. |
| `DELETE /api/listing-review-drafts/{id}?expectedRevision=...` | Deletes only the checked revision. |
| `POST /api/listing-review-drafts/{id}/adopt` | Uses `{ expectedRevision, existingVehicleId?, expectedVehicleRevision? }`. |

Only one draft per existing page identity is allowed; URL tracking variants do
not create duplicates. Conflicts carry the existing draft identity/revision.
Adoption requires registration. It writes the listing and consumes the draft
in one locked transaction. Updating an existing registration requires an
explicit reviewed choice and both revisions. A failed adoption leaves the
draft intact. Existing costs and reviewed facts are retained.

## Evidence and reuse

Editing a value records `user/manual/unverified`; changing a confirmed value
removes that confirmation. Separate confirmation actions confirm the selected
current value. Existing legacy confirmation operations keep their original
meaning; backend-controlled confirmation timestamps and evidence validation
remain authoritative. HTML/AI listing values remain unverified on save,
opening, reuse and adoption. No operation grants registry verification.

`POST /api/listing-reuse/preview` is read-only. Supply exactly one source:
`vehicleId` with expected vehicle revision and listing version, `reviewDraftId`
with its expected revision, or a validated `unsavedListing`. `target` contains
the current cost inputs and optional `factEdits` represent current selections.
Core returns typed purchase-price, annual-tax, energy and comparison-fact
proposals. The UI previews choices before applying them to unsaved state.

Nonmissing target values require explicit replacement, including zero, false
and empty collections. Reapplying the same source does not duplicate rows.
Purchase-price reuse also proposes the corresponding evidence-bearing price
fact; the chosen cost-input price remains the calculation authority. Existing
rule and scoring evidence requirements are unchanged.

Consumption retains its exact decimal, unit and original label (such as NEDC).
Multiple possible fuels, hybrid driving-mode assumptions and battery-versus-wall
electricity bases require a user choice. Insurance, residual value and future
service/repair assumptions are never invented. Cost source records carry the
listing reference, source field, original label, listing version and optional
source item index. Target collection keys remain stable. New source claims are
checked against the listing at save; draft sources bind to the listing version
assigned during adoption.

## Individual electric-driving shares

`VehicleCostInput.electricDrivingShare` has `mode: inherit|override` and an
optional sensitivity `value`. Missing old input behaves as `inherit`.
`override` with null means explicitly unknown and never falls back to the
household. A specified value is either one exact decimal or a complete
favorable/baseline/cautious trio, all within 0–100.

Core resolves the selected share before the existing energy calculation.
Single-source cars and whole-distance consumption retain their previous
behavior. Cost confirmations include this choice/value. Results expose
`energy.electricDrivingShare` with value and household/vehicle origin; comparison
and frozen PDF display it. The report captures listing sources and inputs once;
printing and reopening that capture do not fetch ads or rerun AI.

## Versions, migration and recovery

| Contract | Written/emitted | Still readable |
| --- | --- | --- |
| Listing storage | 3 | 1, 2, 3 |
| Vehicle costs and shared calculation draft | 2 | 1, 2 |
| Comparison facts | 2 | 1, 2 |
| Listing review drafts | 1 | 1 |
| Household calculation/result | 3 | Prior stored inputs remain usable |
| Complete comparison response | 3 | Clients must require the current response |
| Shared profiles, rule/scoring versions | Unchanged | Unchanged |
| Extraction prompt/schema | 4/3 | Unchanged compatibility |

Apply `20260918105352_AddListingReviewWorkflow` through the explicit API
`migrate` command. Earlier migrations are unchanged. Existing rows are read
without mass rewriting; new saves use the new storage versions.

Before migrating an installation with data that must be recoverable, stop writers and take a PostgreSQL
custom-format backup (`pg_dump -Fc`) outside disposable test directories. Verify
it by restoring into a separate PostgreSQL 18 database with the compatible
application version. Preserve credentials volumes without copying their
contents into the repository. The [prebuilt updater](deployment-images.md)
deliberately removes old unused app images after success; historical digests
remain in releases for a separately planned recovery. Backups are not a
mandatory updater step for an installation with disposable data.

Downgrading to `20260910214541_AllowHtmlListingExtraction` is refused when review
drafts or new listing/cost/fact/shared-draft payloads would be lost. Use a
compatible backup if those inputs must be preserved. Never relabel new rows as
an older storage version. No automatic downgrade or restoration over user data
is performed.
