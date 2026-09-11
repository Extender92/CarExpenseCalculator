# Additional Blocket checks and seller-type correction

The Saab/Opel follow-up found that filtering seller contact panels also removed
the explicit seller kind. The correction on `fix/codex-login-status` preserves
dealer/private kind from the identified panel, including the private declarative
shadow-root template, without executing scripts or retaining contact information.
Unknown/ambiguous markers never become guessed private sellers or confirmations.
A later live run also exposed an intermittent omitted Saab postcode; an explicit
postcode in the captured location row now survives independently of the AI copy.

Both are `listing/html/unverified`. Existing public contracts and all version
numbers are unchanged. This is [PR #93](https://github.com/Extender92/CarExpenseCalculator/pull/93)
branch delivery, not a merge or deployment. The original [acceptance report](listing-extraction-verification-report.md)
and its Audi/Skoda evidence remain applicable to the earlier implementation.
Tested application commit: [`e5951fa`](https://github.com/Extender92/CarExpenseCalculator/commit/e5951fa69857aa9bb28ab0052d29d4c84d73ee63).
Subsequent test/report corrections do not change that implementation.

## Execution history

All times below are UTC on 2026-09-11. Calls were sequential through the real
Nginx/API/Codex stack; the model received only URL-retrieved content, never the
user reference text. The two source layouts were also inspected directly once
each without AI; only sanitized minimal panel fixtures were committed.

| Stage | Saab | Opel | Observation |
| --- | --- | --- | --- |
| Before fix, `90c7729` | 08:35:28.059; 27.598 s; 200 | 08:35:55.657; 30.554 s; 200 | Seller type absent; all checked car content retained. |
| First fix | 08:49:19.630; 23.489 s; 200 | 08:49:43.120; 29.211 s; 200 | Dealer recognized; private template still missed. |
| Template fix | 08:51:57.390; 25.932 s; 200 | 08:52:23.323; 32.520 s; 200 | Both seller kinds correct; Saab postcode omitted by model. |
| Test-stack restart | 08:55:29.067; 0.038 s; 502 | Not sent | Nginx was restarted after replacing API; readiness checked before next explicit run. No source/model request in this failed start. |

The corrected private template fixture first reproduced a failing parser test
(one pass, one fail). The final implementation passes it. Earlier green tests
used an oversimplified private-panel fixture; that fixture was corrected rather
than weakening the expectation. There were no automatic live retries, source
rate limits or challenge bypasses.

The earlier [e5951fa PR run](https://github.com/Extender92/CarExpenseCalculator/actions/runs/34581886310)
also exposed a timing assumption in `Timeout_is_typed_and_releases_capacity`:
it required two process starts within two 30 ms whole-operation budgets, although
HTML parsing may consume that deadline first. The test now asserts the released
gate after timeout and a successful subsequent caller using the same gate, with
normal startup budget. Timeout behavior and production timing remain unchanged;
no test was skipped. The later run of the unchanged application had already passed
backend tests, but the timing-dependent test was corrected before final delivery.

## Final live checks

| Car / source | Start UTC | Seconds | HTTP | UTF-8 bytes | Correct / missing / incorrect |
| --- | --- | ---: | ---: | ---: | --- |
| [saab](https://www.blocket.se/mobility/item/26475617?ci=7) | 2026-09-11T08:56:07.696Z | 25.120 | 200 | 15278 | 322 / 0 / 0 |
| [opel](https://www.blocket.se/mobility/item/25199773?ci=1) | 2026-09-11T08:56:32.817Z | 30.430 | 200 | 15640 | 285 / 0 / 0 |

The check counts include values, ordered content and per-field provenance,
not separate automated test cases. Complete descriptions, 27/21 specification
rows, 30/18 equipment entries and all four Opel seller answers match the user
references. The description comparison normalizes whitespace while independently
checking retained paragraph breaks. Original equipment order is exact.

Saab remains four owners despite the seller claim of one user. Opel retains
116 hp in its description alongside the 115 hp specification, plus the described
defects/repairs and the four negative seller answers. No claim becomes verified.
Opel registration, owner count, door count and last inspection remain unknown.
Menus, marketing, dealer biography and contact details are intentionally excluded.

## Automated and browser verification

- .NET SDK 10.0.400: restore, Release build and full tests with PostgreSQL 18;
  **1,132 passed, 0 failed, 0 skipped**, 0 build warnings/errors (585 Core,
  100 sidecar, 35 Infrastructure unit, 270 API, 138 PostgreSQL, 4 architecture).
- New parser and adapter regressions cover real panel structure, missing/hidden/
  incidental markers, conflicting kinds, unsupported type values, model overrides,
  contact exclusion and explicit/ambiguous postcode patterns. Existing PostgreSQL
  tests additionally check HTML seller type through draft/adoption/snapshot.
- The changed Chromium URL test passed locally against disposable fake extraction;
  known private/unknown seller fields and HTML provenance were checked. One worker.
- Both actual final live responses were replayed into the review UI without further
  source/AI calls. All descriptions, specification rows, seller answers and seller
  selectors were checked; screenshots were inspected at desktop and 390 px width.
  No cars were saved in the live test database.
- Local Docker builds, explicit migrations and Nginx readiness passed. A fresh empty
  database logged its absent migration-history table before creating it successfully.
- The browser run emitted the existing NO_COLOR/FORCE_COLOR environment notice.
  Full frontend/OpenAPI/Chromium CI results are identified by the
  [published-head checks](https://github.com/Extender92/CarExpenseCalculator/pull/93/checks).
  No frontend production code or generated schema was changed.

## Cleanup

Both disposable stacks (`car-expense-seller-fix` and `car-expense-seller-test`),
their containers, networks and database volumes were removed. Existing application
containers, authentication and user data were preserved. Native deletion of
`C:\Users\dann_\Source\repos\CarExpenseCalculator\temp\listing-seller-type`
was rejected by automatic execution policy (`blocked by policy`); the directory
therefore still contains the work-owned HTML captures, responses, helpers, logs
and screenshots. Windows Temp was inspected without identifying attributable
new work files. Previous deferred inventories, including
`temp/listing-additional-probe`, were left untouched.

## Final value matrix

Per-field provenance checks passed as summarized above. The following tables show
the expected and observed values; array entries retain their original ordering.

### Saab

| Field | Expected | Observed | Result |
| --- | --- | --- | --- |
| registrationNumber | UGS135 | UGS135 | correct |
| make | Saab | Saab | correct |
| model | 9-3 | 9-3 | correct |
| modelYear | 2003 | 2003 | correct |
| vin | YS3FB45F331059690 | YS3FB45F331059690 | correct |
| priceSek | 34900 | 34900 | correct |
| odometerKilometres | 167000 | 167000 | correct |
| sellerType | dealer | dealer | correct |
| locality | Norrköping | Norrköping | correct |
| bodyType | sedan | sedan | correct |
| horsepower | 150 | 150 | correct |
| engineDisplacementCubicCentimetres | 2000 | 2000 | correct |
| ownerCount | 4 | 4 | correct |
| firstRegistrationDate | 2003-06-27 | 2003-06-27 | correct |
| lastInspectionDate | 2026-05-07 | 2026-05-07 | correct |
| nextInspectionDate | 2027-07-31 | 2027-07-31 | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| colour | Grå | Grå | correct |
| county | unknown | unknown | correct |
| annualVehicleTaxSek | unknown | unknown | correct |
| towBar | True | True | correct |
| details.title | Saab 9-3 | Saab 9-3 | correct |
| details.subtitle | SportSedan 1.8t Linear Euro 4 | SportSedan 1.8t Linear Euro 4 | correct |
| details.listingId | 26475617 | 26475617 | correct |
| details.seats | 5 | 5 | correct |
| details.doors | 4 | 4 | correct |
| details.luggageLitres | 425 | 425 | correct |
| details.weightKilograms | 1395 | 1395 | correct |
| details.trailerWeightKilograms | 1530 | 1530 | correct |
| details.postalCode | 60361 | 60361 | correct |
| details.updatedLocalDateTime | 2026-09-10T15:20:00 | 2026-09-10T15:20:00 | correct |
| details.sellerAnswers | unknown | unknown | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightLabel | Max trailervikt | Max trailervikt | correct |
| details.trailerWeightCategory | unspecified | unspecified | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Personbil | Personbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedTimeZone | unknown | unknown | correct |
| details.updatedUtcOffsetMinutes | unknown | unknown | correct |
| description.completeText | Besiktas senast 2027-07-31, Full Servad. Sommar o vinterdäck, Endast  1 Brukare | Besiktas senast 2027-07-31, Full Servad. Sommar o vinterdäck, Endast  1 Brukare | correct |
| description.paragraphs | True | True | correct |
| equipment.exactOrderedList | ["ABS-bromsar", "ACC", "Airbag förare", "Airbag passagerare fram", "Avstängningsbar airbag passagerare", "Barnlås", "Broms-assistans", "CD-Stereo", "Centrallås (fjärrstyrt)", "Delbart baksäte", "Dragkrok, fast", "Elhissar (fram och bak)", "Elstol förare", "Eluppvärmda sidospeglar", "Farthållare", "Fällbara baksäten", "Färddator", "ISOFIX-fästen bak", "Läslampa", "Lättmetallfälgar", "Reservhjul", "Servostyrning", "Sidoairbags", "Sidokrockgardiner", "Sminkspegel", "Startspärr", "Stöldlarm", "Svensksåld", "Sätesvärme (fram)", "Yttertemperaturmätare"] | ["ABS-bromsar", "ACC", "Airbag förare", "Airbag passagerare fram", "Avstängningsbar airbag passagerare", "Barnlås", "Broms-assistans", "CD-Stereo", "Centrallås (fjärrstyrt)", "Delbart baksäte", "Dragkrok, fast", "Elhissar (fram och bak)", "Elstol förare", "Eluppvärmda sidospeglar", "Farthållare", "Fällbara baksäten", "Färddator", "ISOFIX-fästen bak", "Läslampa", "Lättmetallfälgar", "Reservhjul", "Servostyrning", "Sidoairbags", "Sidokrockgardiner", "Sminkspegel", "Startspärr", "Stöldlarm", "Svensksåld", "Sätesvärme (fram)", "Yttertemperaturmätare"] | correct |
| equipment[0] | ABS-bromsar | ABS-bromsar | correct |
| equipment[1] | ACC | ACC | correct |
| equipment[2] | Airbag förare | Airbag förare | correct |
| equipment[3] | Airbag passagerare fram | Airbag passagerare fram | correct |
| equipment[4] | Avstängningsbar airbag passagerare | Avstängningsbar airbag passagerare | correct |
| equipment[5] | Barnlås | Barnlås | correct |
| equipment[6] | Broms-assistans | Broms-assistans | correct |
| equipment[7] | CD-Stereo | CD-Stereo | correct |
| equipment[8] | Centrallås (fjärrstyrt) | Centrallås (fjärrstyrt) | correct |
| equipment[9] | Delbart baksäte | Delbart baksäte | correct |
| equipment[10] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[11] | Elhissar (fram och bak) | Elhissar (fram och bak) | correct |
| equipment[12] | Elstol förare | Elstol förare | correct |
| equipment[13] | Eluppvärmda sidospeglar | Eluppvärmda sidospeglar | correct |
| equipment[14] | Farthållare | Farthållare | correct |
| equipment[15] | Fällbara baksäten | Fällbara baksäten | correct |
| equipment[16] | Färddator | Färddator | correct |
| equipment[17] | ISOFIX-fästen bak | ISOFIX-fästen bak | correct |
| equipment[18] | Läslampa | Läslampa | correct |
| equipment[19] | Lättmetallfälgar | Lättmetallfälgar | correct |
| equipment[20] | Reservhjul | Reservhjul | correct |
| equipment[21] | Servostyrning | Servostyrning | correct |
| equipment[22] | Sidoairbags | Sidoairbags | correct |
| equipment[23] | Sidokrockgardiner | Sidokrockgardiner | correct |
| equipment[24] | Sminkspegel | Sminkspegel | correct |
| equipment[25] | Startspärr | Startspärr | correct |
| equipment[26] | Stöldlarm | Stöldlarm | correct |
| equipment[27] | Svensksåld | Svensksåld | correct |
| equipment[28] | Sätesvärme (fram) | Sätesvärme (fram) | correct |
| equipment[29] | Yttertemperaturmätare | Yttertemperaturmätare | correct |
| specifications.count | 27 | 27 | correct |
| specifications[0].Märke | {"name": "Märke", "value": "Saab"} | {"name": "Märke", "value": "Saab"} | correct |
| specifications[1].Modell | {"name": "Modell", "value": "9-3"} | {"name": "Modell", "value": "9-3"} | correct |
| specifications[2].Modellår | {"name": "Modellår", "value": "2003"} | {"name": "Modellår", "value": "2003"} | correct |
| specifications[3].Biltyp | {"name": "Biltyp", "value": "Sedan"} | {"name": "Biltyp", "value": "Sedan"} | correct |
| specifications[4].Drivmedel | {"name": "Drivmedel", "value": "Bensin"} | {"name": "Drivmedel", "value": "Bensin"} | correct |
| specifications[5].Effekt | {"name": "Effekt", "value": "150 Hk"} | {"name": "Effekt", "value": "150 Hk"} | correct |
| specifications[6].Motorvolym | {"name": "Motorvolym", "value": "2 L"} | {"name": "Motorvolym", "value": "2 L"} | correct |
| specifications[7].CO₂-utsläpp | {"name": "CO₂-utsläpp", "value": "192 g/km"} | {"name": "CO₂-utsläpp", "value": "192 g/km"} | correct |
| specifications[8].Miltal | {"name": "Miltal", "value": "16 700 mil"} | {"name": "Miltal", "value": "16 700 mil"} | correct |
| specifications[9].Bränsleförbrukning (NEDC) | {"name": "Bränsleförbrukning (NEDC)", "value": "8,1 L/100 km"} | {"name": "Bränsleförbrukning (NEDC)", "value": "8,1 L/100 km"} | correct |
| specifications[10].Växellåda | {"name": "Växellåda", "value": "Manuell"} | {"name": "Växellåda", "value": "Manuell"} | correct |
| specifications[11].Max trailervikt | {"name": "Max trailervikt", "value": "1 530 kg"} | {"name": "Max trailervikt", "value": "1 530 kg"} | correct |
| specifications[12].Drivhjul | {"name": "Drivhjul", "value": "Framhjulsdrift"} | {"name": "Drivhjul", "value": "Framhjulsdrift"} | correct |
| specifications[13].Vikt | {"name": "Vikt", "value": "1 395 kg"} | {"name": "Vikt", "value": "1 395 kg"} | correct |
| specifications[14].Säten | {"name": "Säten", "value": "5"} | {"name": "Säten", "value": "5"} | correct |
| specifications[15].Antal dörrar | {"name": "Antal dörrar", "value": "4"} | {"name": "Antal dörrar", "value": "4"} | correct |
| specifications[16].Bagageutrymme | {"name": "Bagageutrymme", "value": "425 L"} | {"name": "Bagageutrymme", "value": "425 L"} | correct |
| specifications[17].Färg | {"name": "Färg", "value": "Grå"} | {"name": "Färg", "value": "Grå"} | correct |
| specifications[18].Bilens plats | {"name": "Bilens plats", "value": "Sverige"} | {"name": "Bilens plats", "value": "Sverige"} | correct |
| specifications[19].Senaste besiktningsdatum | {"name": "Senaste besiktningsdatum", "value": "2026-05-07"} | {"name": "Senaste besiktningsdatum", "value": "2026-05-07"} | correct |
| specifications[20].Nästa besiktningsdatum | {"name": "Nästa besiktningsdatum", "value": "2027-07-31"} | {"name": "Nästa besiktningsdatum", "value": "2027-07-31"} | correct |
| specifications[21].Avgiftsklass | {"name": "Avgiftsklass", "value": "Personbil"} | {"name": "Avgiftsklass", "value": "Personbil"} | correct |
| specifications[22].Registreringsnummer | {"name": "Registreringsnummer", "value": "UGS135"} | {"name": "Registreringsnummer", "value": "UGS135"} | correct |
| specifications[23].Chassinummer | {"name": "Chassinummer", "value": "ys3fb45f331059690"} | {"name": "Chassinummer", "value": "ys3fb45f331059690"} | correct |
| specifications[24].Registreringsdatum | {"name": "Registreringsdatum", "value": "2003-06-27"} | {"name": "Registreringsdatum", "value": "2003-06-27"} | correct |
| specifications[25].Antal ägare | {"name": "Antal ägare", "value": "4"} | {"name": "Antal ägare", "value": "4"} | correct |
| specifications[26].Försäljningsform | {"name": "Försäljningsform", "value": "Begagnad bil till salu"} | {"name": "Försäljningsform", "value": "Begagnad bil till salu"} | correct |
| consumption | 8.1 | 8.1 | correct |
| consumption.label.NEDC | True | True | correct |
| promptVersion | 4 | 4 | correct |
| schemaVersion | 3 | 3 | correct |
| sourcePageObserved | True | True | correct |

### Opel

| Field | Expected | Observed | Result |
| --- | --- | --- | --- |
| registrationNumber | unknown | unknown | correct |
| make | Opel | Opel | correct |
| model | Astra | Astra | correct |
| modelYear | 2009 | 2009 | correct |
| vin | W0L0AHL35A2019888 | W0L0AHL35A2019888 | correct |
| priceSek | 16900 | 16900 | correct |
| odometerKilometres | 257110 | 257110 | correct |
| locality | Nynäshamn | Nynäshamn | correct |
| bodyType | wagon | wagon | correct |
| horsepower | 115 | 115 | correct |
| ownerCount | unknown | unknown | correct |
| firstRegistrationDate | 2009-12-01 | 2009-12-01 | correct |
| lastInspectionDate | unknown | unknown | correct |
| nextInspectionDate | 2027-09-30 | 2027-09-30 | correct |
| sellerType | private | private | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| colour | Grå | Grå | correct |
| county | unknown | unknown | correct |
| annualVehicleTaxSek | unknown | unknown | correct |
| towBar | True | True | correct |
| details.title | Opel Astra | Opel Astra | correct |
| details.subtitle | Caravan 1.6 ECOTEC Manuell | Caravan 1.6 ECOTEC Manuell | correct |
| details.listingId | 25199773 | 25199773 | correct |
| details.seats | 5 | 5 | correct |
| details.doors | unknown | unknown | correct |
| details.luggageLitres | 500 | 500 | correct |
| details.weightKilograms | 1278 | 1278 | correct |
| details.trailerWeightKilograms | 660 | 660 | correct |
| details.postalCode | 14991 | 14991 | correct |
| details.updatedLocalDateTime | 2026-09-02T11:34:00 | 2026-09-02T11:34:00 | correct |
| details.sellerAnswers | [{"question": "Har bilen några kända skador?", "answer": "Nej"}, {"question": "Har det genomförts omfattande reparationer?", "answer": "Nej"}, {"question": "Är eller har motorn varit trimmad?", "answer": "Nej"}, {"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några kända skador?", "answer": "Nej"}, {"question": "Har det genomförts omfattande reparationer?", "answer": "Nej"}, {"question": "Är eller har motorn varit trimmad?", "answer": "Nej"}, {"question": "Har bilen några skulder?", "answer": "Nej"}] | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightLabel | Max trailervikt | Max trailervikt | correct |
| details.trailerWeightCategory | unspecified | unspecified | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Personbil | Personbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedTimeZone | unknown | unknown | correct |
| details.updatedUtcOffsetMinutes | unknown | unknown | correct |
| description.completeText | Nu säljer jag min Opel Astra H Kombi 1.6 (116 hk) från 2009. En rymlig, driftsäker och lättkörd kombi med manuell växellåda som alltid har startat och fungerat bra. Förra året kördes bilen till Bosnien och tillbaka utan några som helst problem. ⏎ Bilen är nybesiktigad och har den senaste tiden fått flera större service- och reparationsarbeten utförda, vilket gör att nästa ägare slipper många vanliga underhållskostnader. ⏎ Utrustning ⏎ Manuell växellåda ⏎ AC (fungerar) ⏎ Farthållare ⏎ AUX-ingång ⏎ Dragkrok ⏎ Sommar- och vinterdäck ingår ⏎ Nyligen utfört ⏎ Ny ventilkåpa ⏎ Ny ventilkåpspackning ⏎ Nya tändstift ⏎ Bromsbelägg fram ⏎ Bromsskivor och bromsbelägg bak ⏎ Fjädring bytt runt om (inom de senaste två åren) ⏎ Slutdämpare bytt ⏎ Avgasläckage åtgärdat ⏎ Att känna till ⏎ Förarrutan behöver ibland lite hjälp den sista biten upp. ⏎ Centrallåset låser upp när vänster bakdörr öppnas. ⏎ Mindre ytrost finns på båda främre trösklarna nedan för dörrarna. ⏎  ⏎ Trots miltalet går bilen fint i både motor och växellåda. Den är nybesiktigad, pålitlig och har varit en mycket bra bruksbil. Perfekt som pendlarbil, extrabil eller första bil. | Nu säljer jag min Opel Astra H Kombi 1.6 (116 hk) från 2009. En rymlig, driftsäker och lättkörd kombi med manuell växellåda som alltid har startat och fungerat bra. Förra året kördes bilen till Bosnien och tillbaka utan några som helst problem. ⏎ Bilen är nybesiktigad och har den senaste tiden fått flera större service- och reparationsarbeten utförda, vilket gör att nästa ägare slipper många vanliga underhållskostnader. ⏎ Utrustning ⏎  ⏎ Manuell växellåda ⏎  ⏎ AC (fungerar) ⏎  ⏎ Farthållare ⏎  ⏎ AUX-ingång ⏎  ⏎ Dragkrok ⏎  ⏎ Sommar- och vinterdäck ingår ⏎  ⏎ Nyligen utfört ⏎  ⏎ Ny ventilkåpa ⏎  ⏎ Ny ventilkåpspackning ⏎  ⏎ Nya tändstift ⏎  ⏎ Bromsbelägg fram ⏎  ⏎ Bromsskivor och bromsbelägg bak ⏎  ⏎ Fjädring bytt runt om (inom de senaste två åren) ⏎  ⏎ Slutdämpare bytt ⏎  ⏎ Avgasläckage åtgärdat ⏎  ⏎ Att känna till ⏎  ⏎ Förarrutan behöver ibland lite hjälp den sista biten upp. ⏎  ⏎ Centrallåset låser upp när vänster bakdörr öppnas. ⏎  ⏎ Mindre ytrost finns på båda främre trösklarna nedan för dörrarna. ⏎  ⏎ Trots miltalet går bilen fint i både motor och växellåda. Den är nybesiktigad, pålitlig och har varit en mycket bra bruksbil. Perfekt som pendlarbil, extrabil eller första bil. | correct |
| description.paragraphs | True | True | correct |
| equipment.exactOrderedList | ["Airbag fram", "Aircondition", "Antispinn", "AUX-ingång", "Centrallås", "Dragkrok, fast", "Elektriska fönster", "El-sidospeglar m. värme", "ESC", "Farthållare", "Fällbart baksäte", "Färddator", "Isofix", "Klimatanläggning", "Servostyrning", "Sidoairbags", "Takrails", "Uppvärmda säten, fram"] | ["Airbag fram", "Aircondition", "Antispinn", "AUX-ingång", "Centrallås", "Dragkrok, fast", "Elektriska fönster", "El-sidospeglar m. värme", "ESC", "Farthållare", "Fällbart baksäte", "Färddator", "Isofix", "Klimatanläggning", "Servostyrning", "Sidoairbags", "Takrails", "Uppvärmda säten, fram"] | correct |
| equipment[0] | Airbag fram | Airbag fram | correct |
| equipment[1] | Aircondition | Aircondition | correct |
| equipment[2] | Antispinn | Antispinn | correct |
| equipment[3] | AUX-ingång | AUX-ingång | correct |
| equipment[4] | Centrallås | Centrallås | correct |
| equipment[5] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[6] | Elektriska fönster | Elektriska fönster | correct |
| equipment[7] | El-sidospeglar m. värme | El-sidospeglar m. värme | correct |
| equipment[8] | ESC | ESC | correct |
| equipment[9] | Farthållare | Farthållare | correct |
| equipment[10] | Fällbart baksäte | Fällbart baksäte | correct |
| equipment[11] | Färddator | Färddator | correct |
| equipment[12] | Isofix | Isofix | correct |
| equipment[13] | Klimatanläggning | Klimatanläggning | correct |
| equipment[14] | Servostyrning | Servostyrning | correct |
| equipment[15] | Sidoairbags | Sidoairbags | correct |
| equipment[16] | Takrails | Takrails | correct |
| equipment[17] | Uppvärmda säten, fram | Uppvärmda säten, fram | correct |
| specifications.count | 21 | 21 | correct |
| specifications[0].Märke | {"name": "Märke", "value": "Opel"} | {"name": "Märke", "value": "Opel"} | correct |
| specifications[1].Modell | {"name": "Modell", "value": "Astra"} | {"name": "Modell", "value": "Astra"} | correct |
| specifications[2].Modellår | {"name": "Modellår", "value": "2009"} | {"name": "Modellår", "value": "2009"} | correct |
| specifications[3].Biltyp | {"name": "Biltyp", "value": "Kombi"} | {"name": "Biltyp", "value": "Kombi"} | correct |
| specifications[4].Drivmedel | {"name": "Drivmedel", "value": "Bensin"} | {"name": "Drivmedel", "value": "Bensin"} | correct |
| specifications[5].Effekt | {"name": "Effekt", "value": "115 Hk"} | {"name": "Effekt", "value": "115 Hk"} | correct |
| specifications[6].Miltal | {"name": "Miltal", "value": "25 711 mil"} | {"name": "Miltal", "value": "25 711 mil"} | correct |
| specifications[7].Bränsleförbrukning (NEDC) | {"name": "Bränsleförbrukning (NEDC)", "value": "7,5 L/100 km"} | {"name": "Bränsleförbrukning (NEDC)", "value": "7,5 L/100 km"} | correct |
| specifications[8].Växellåda | {"name": "Växellåda", "value": "Manuell"} | {"name": "Växellåda", "value": "Manuell"} | correct |
| specifications[9].Max trailervikt | {"name": "Max trailervikt", "value": "660 kg"} | {"name": "Max trailervikt", "value": "660 kg"} | correct |
| specifications[10].Drivhjul | {"name": "Drivhjul", "value": "Framhjulsdrift"} | {"name": "Drivhjul", "value": "Framhjulsdrift"} | correct |
| specifications[11].Vikt | {"name": "Vikt", "value": "1 278 kg"} | {"name": "Vikt", "value": "1 278 kg"} | correct |
| specifications[12].Säten | {"name": "Säten", "value": "5"} | {"name": "Säten", "value": "5"} | correct |
| specifications[13].Bagageutrymme | {"name": "Bagageutrymme", "value": "500 L"} | {"name": "Bagageutrymme", "value": "500 L"} | correct |
| specifications[14].Färg | {"name": "Färg", "value": "Grå"} | {"name": "Färg", "value": "Grå"} | correct |
| specifications[15].Bilens plats | {"name": "Bilens plats", "value": "Sverige"} | {"name": "Bilens plats", "value": "Sverige"} | correct |
| specifications[16].Nästa besiktningsdatum | {"name": "Nästa besiktningsdatum", "value": "2027-09-30"} | {"name": "Nästa besiktningsdatum", "value": "2027-09-30"} | correct |
| specifications[17].Avgiftsklass | {"name": "Avgiftsklass", "value": "Personbil"} | {"name": "Avgiftsklass", "value": "Personbil"} | correct |
| specifications[18].Chassinummer | {"name": "Chassinummer", "value": "W0L0AHL35A2019888"} | {"name": "Chassinummer", "value": "W0L0AHL35A2019888"} | correct |
| specifications[19].Registreringsdatum | {"name": "Registreringsdatum", "value": "2009-12-01"} | {"name": "Registreringsdatum", "value": "2009-12-01"} | correct |
| specifications[20].Försäljningsform | {"name": "Försäljningsform", "value": "Begagnad bil till salu"} | {"name": "Försäljningsform", "value": "Begagnad bil till salu"} | correct |
| consumption | 7.5 | 7.5 | correct |
| consumption.label.NEDC | True | True | correct |
| promptVersion | 4 | 4 | correct |
| schemaVersion | 3 | 3 | correct |
| sourcePageObserved | True | True | correct |
