# Listing extraction source gate — 2026-09-10

> Historical experiment. The user has superseded its blocking policy. Valid AI
> suggestions are now retained as unconfirmed listing input when page-opening
> metadata is absent. The deployed CLI remains 0.153.0. Current implementation
> and acceptance are in [complete listing input](complete-listing-extraction.md)
> and [the verification report](listing-extraction-verification-report.md).

## Outcome

**Blocked: neither listing passed the required source-evidence gate with Codex
CLI 0.154.0.** Both correctly configured runs completed and returned
schema-valid populated drafts, but no completed web-search action supplied a
concrete opened-page URL. The application must not promote those drafts to
source-backed extraction.

This is the first checkpoint of the user-approved complete-listing plan. The
expanded field model, persistence migration, HTTP/UI changes, comparison
attachments and PDF extensions have therefore **not started**. There is no
completed implementation PR. Existing startup fixes remain on
`fix/codex-login-status`, based on `fc7eefc138ed904e5eb4e2a916f48f530f15dc0f`.

The deployed CLI remains 0.153.0. Prompt/schema versions remain 2. No application
configuration, database migration, registration identity, calculation, score or
report contract was changed by the gate experiment. The previously implemented,
uncommitted login-status and supported image-feature configuration fixes were
preserved.

## Reproduction conditions

- User-selected inputs: [Audi A4](https://www.blocket.se/mobility/item/26427275?ci=3)
  and [Skoda Roomster](https://www.blocket.se/mobility/item/26434732?ci=3).
- The existing `ListingExtractionPrompt.Create` text and extraction schema v2
  were used unchanged. The user's pasted reference descriptions and expected
  values were **not** supplied to the model.
- CLI 0.154.0 was installed only in a disposable Node 22.22.2 container. The
  requested model was exactly `gpt-5.6-luna`, reasoning `medium`.
- The normal argument/configuration contract was retained: strict configuration,
  ephemeral JSONL output, read-only empty work directory, ignored user config and
  rules, live hosted search restricted to `www.blocket.se`, and disabled unrelated
  tools. No experimental search flag or alternative provider was enabled.
- Each correctly configured URL check used one turn, sequentially, with a
  60-second limit and bounded output. There was no automatic replacement turn.
- The existing authentication volume was mounted read-only. Only the login file
  was copied into the probe's private tmpfs home; it never entered host files,
  logs, test fixtures or command output. The source volume was not modified.
- The probe used the same public CA certificate bundle as the running sidecar,
  through `SSL_CERT_FILE`. Raw CLI output stayed in process memory. Only aggregate
  observations and anonymized event shapes are recorded here.
- Final JSON was independently checked against the unchanged draft-2020-12
  schema using Ajv 8.17.1, installed only in tmpfs. This check did not replace the
  production `JsonSchema.Net` validator or Core normalization.

Input file SHA-256 hashes:

| File | SHA-256 |
| --- | --- |
| `Schemas/listing-extraction-v2.schema.json` | `768ad0ae9428b7f6d5ac5e88b14bd76e032e669119c35ca2aa30ec960cf3ea1e` |
| `ListingExtractionPrompt.cs` | `8360699b22e3b7e5a7235db8c58bfb41b609e48a248bd15f6388608fc598d90c` |

## Observed results

| Observation | Audi | Skoda |
| --- | --- | --- |
| Exit code | 0 | 0 |
| Duration | 50.464 s | 42.906 s |
| Completed turn | Yes | Yes |
| Final JSON satisfies schema v2 | Yes | Yes |
| Non-null top-level fields | 25 | 24 |
| JSONL events | 17 | 15 |
| Completed web-search items | 6 | 5 |
| `other` actions | 4 | 3 |
| `search` actions | 2 | 2 |
| Concrete opened-page URLs | 0 | 0 |
| Matching opened source | No | No |
| Malformed lines / output-limit failures | 0 / 0 | 0 / 0 |
| CLI stderr bytes | 0 | 0 |
| Gate | **Failed** | **Failed** |

The non-null counts are observations, not a claim of accurate or complete
extraction. No full field-by-field acceptance was performed after the source
gate failed. These were isolated CLI checks, not API responses from a deployment
upgraded to 0.154.0.

The following synthetic event preserves the observed shape without retaining a
real listing or provider identifier:

```json
{
  "type": "item.completed",
  "item": {
    "id": "item_1",
    "type": "web_search",
    "query": "https://example.com/item/1",
    "action": { "type": "other" }
  }
}
```

Other items were ordinary `search` actions. Neither shape supplies the required
opened URL. The probe also recognized both snake-case and camel-case names for
explicit open/find actions; none appeared. No item supplied a `results` payload.
The existing synthetic regression
[`Parse_does_not_promote_url_queries_from_opaque_actions_to_opened_sources`](../tests/backend/CarExpenseCalculator.CodexExtractor.UnitTests/CodexJsonlParserTests.cs)
already exercises this boundary with a populated draft. Its positive-source,
model-authored-source and incomplete-stream companion tests remain applicable.

This finding does not prove that Blocket contains no data, that Blocket blocked
the model, or where the metadata was lost upstream. It proves that the tested
CLI output is insufficient for this application's source contract. See the
[source boundary](codex-extraction.md#source-evidence-and-output-validation) and
[official web-search documentation](https://learn.chatgpt.com/docs/web-search).

## Follow-up: comparison against the user's reference texts

After the checkpoint, the user explicitly requested a content comparison against
the two pasted advertisements. This can verify correspondence with those
references independently of the CLI source-event problem. It does not verify
the seller's claims against a vehicle registry or change stored verification
levels.

Two fresh checks used the currently deployed CLI **0.153.0**, the same v2
prompt/schema and `gpt-5.6-luna` / `medium`. Only the respective URL and normal
extraction instructions went to the model. The expected values stayed in the
external comparison harness. Both turns completed, with exit code 0 and no
stderr, in 47.018 seconds (Audi) and 54.039 seconds (Skoda).

| Reference check | Audi | Skoda |
| --- | --- | --- |
| Matching scalar/categorical fields | 18 of 21 | 21 of 21 |
| Missing fields present in the reference | Owner count 4; first registration 1999-11-24 | None among the 21 checked fields |
| Variant wording | `1.8` rather than `Sedan 1.8 Manuell` | Exact match |
| Equipment | All 18 items matched; no additions | All 15 items matched; no additions |
| Energy | NEDC, 8.5 litres/100 km matched | Unknown, consistent with the supplied text |
| Registration number | Unknown | Unknown |

The 21 checks cover make, model, variant, model year, VIN, price, odometer,
locality, body type, colour, horsepower, engine displacement, owners, first
registration, last/next inspection, fuel, transmission, drivetrain, tow bar and
updated date. Text comparison normalizes whitespace, Unicode and casing; unit
conversion uses the explicit reference values. The Audi variant difference is
a loss of full title wording, not evidence of a different engine: sedan and
manual transmission were correctly returned in their separate fields.

The shared key values matched: prices SEK 28,888 / 29,500; odometers 130,000 /
129,090 km; engines 125 / 69 hp and 1,800 / 1,200 cm3. Both VINs and inspection
dates matched. The Audi's missing owner count and first registration show why
populated-field counts alone cannot establish complete extraction.

Returned Audi seller claims matched the reference's service, service-book,
wheel-set and inspection statements, but were written in English and omitted
the statement about replaced parts. The Skoda returned Swedish claims about
annual workshop service and included winter tyres. Its debt question/answer
was not retained. Neither run can preserve the full original description under
the current v2 prompt.

Image counts (10 / 7) cannot be checked from the pasted text. The Audi's returned
private-seller category was not an explicitly labelled fact in that reference;
it is not counted as a verified match. Missing county, publication date and
annual tax were not filled. No registration number was invented.

The current 31-field extraction schema has no dedicated fields for seats,
doors, weight, luggage volume, full description or seller questions, among the
planned additions. Those omissions are application-contract limitations, not
successful checks. The follow-up demonstrates useful matching content **and**
specific gaps; it does not establish that the complete-listing feature is done.
The application source gate was not changed by this diagnostic comparison.

The temporary per-turn directories were removed after both checks. No helper
file, raw JSONL, credential copy or new Windows Temp artifact was created for
this follow-up. No build, unit-suite rerun, commit, push or PR was needed for
the content comparison; the previously reported automated test counts remain
unchanged.

## Failed setup attempts and verification

The first PowerShell preparation attempt stalled before Docker or an extraction
process started. It was stopped; direct file reads replaced PowerShell objects
carrying additional metadata in the serialized configuration.

The first Node-container attempt lacked the operating-system CA bundle. Both
turns reached the 60-second limit without web-search items or a completed draft.
That environment differed from the application and is **not** used as evidence
against source extraction. A second isolated attempt supplied the application's
public CA bundle; both turns then completed with the results above. In total,
four CLI extraction invocations were attempted: two invalid-environment attempts
and two completed source-gate checks. Both probe containers were removed.

The isolated npm installation reported zero warnings and five informational
notices. No package or lockfile in the repository was changed.

Targeted existing tests were rerun:

```powershell
dotnet test tests/backend/CarExpenseCalculator.CodexExtractor.UnitTests/CarExpenseCalculator.CodexExtractor.UnitTests.csproj --configuration Release --no-build --no-restore --logger 'console;verbosity=minimal'
```

The native Windows run passed **48 tests, 0 failed, 0 skipped**, using the existing
Release build containing the startup fixes and opaque-action regression. It
reported no warnings. No fresh build was performed during this checkpoint.

An earlier attempt to run those Windows-built assemblies in a read-only Linux
SDK container passed 46 and failed 2 endpoint tests: their generated content-root
metadata referenced the Windows source path. It also emitted a workload
verification warning. The assemblies were rerun in their native environment;
no tests were changed, skipped or weakened. This was a test-host mismatch, not a
successful Linux verification of a freshly built artifact.

Full backend/PostgreSQL, frontend, OpenAPI, Chromium and PDF acceptance were not
run for the unstarted expansion. No CI run, commit, push, PR or GitHub issue
change was created by this checkpoint.

## Cleanup and next gate

Probe packages, private authentication copies, schemas and runtime files lived
only in disposable tmpfs mounts. Both live-probe containers and the SDK test
container were removed. The two image references pulled specifically for these
checks were removed. The normal application containers and authentication/data
volumes remain available for the user.

The native test process directed temporary files into this work-owned directory:

```text
C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\listing-source-gate-20260910
```

The automatic tool policy rejected a direct `Remove-Item -LiteralPath ...
-Recurse -Force` operation with `blocked by policy`. No alternative deletion
mechanism was attempted. The remaining contents are:

```text
2tpnsgti.crz\
2um3vqny.o2q\
car-expense-process-test\
car-expense-process-test\03beb0d8ba8440928e094efbd8aec0b2\
Microsoft.NET.Workload_29868_20260910_144045_202.log
```

The four directories are empty apart from the listed child directory; the SDK
workload log is 1,095 bytes. These are test artifacts, not extraction output or
credentials. No identifiable new work-owned Windows Temp leftovers were found.
Previously deferred cleanup inventories were left untouched.

At this historical checkpoint, resuming the expansion required a verified Codex source-transport
solution that produces concrete opened-source evidence for both supplied URLs
under the agreed model/authentication/tool restrictions. A search query or a
model-authored URL is not an acceptable substitute. No Platform API fallback,
scraper, model switch or pasted-text feature was authorized by that plan.

The later user decision superseded that gate: unconfirmed AI suggestions may
be retained without opened-page metadata. The current remaining acceptance
blocker is omitted content, recorded in the [new report](listing-extraction-verification-report.md).
