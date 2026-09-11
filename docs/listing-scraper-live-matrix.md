# Blocket integration: final live field matrix

Four independent API/Nginx requests on 2026-09-10 at 22:02–22:03 UTC (11 September in Stockholm).
Only each URL was submitted. The model received the application-fetched source, never these reference answers.
The [acceptance report](listing-extraction-verification-report.md) records timing, versions, tests and limitations.
The [earlier matrix](listing-extraction-reference-matrix.md) retains the failed hosted-retrieval attempts.

All **576 checks passed**: Audi 149 per request, Skoda 139 per request. Expected null values are checked for absence of invented data, not counted as retrieved facts.

Descriptions and equipment arrays were compared exactly, including paragraph boundaries and order. The NEDC consumption label must retain NEDC and its exact numeric value/unit; supplementary wording may vary. Original specification labels/values are compared in order with whitespace/NFC normalization.

The generated variant, image count, condensed seller claims and condition notes are not fixed-text equality assertions: the supplied references do not define those generated summaries or an image-count requirement. Original descriptions and complete equipment/question sections are checked independently. Model-generated short notes remain unverified. No registration number was supplied or invented.

## Audi A4 (26427275)

| Field / check | Expected | Request 1 observed | Request 2 observed | Status 1 / 2 |
|---|---|---|---|---|
| registrationNumber | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| make | Audi | Audi | Audi | korrekt / korrekt |
| model | A4 | A4 | A4 | korrekt / korrekt |
| modelYear | 2000 | 2000 | 2000 | korrekt / korrekt |
| vin | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | korrekt / korrekt |
| priceSek | 28888 | 28888 | 28888 | korrekt / korrekt |
| odometerKilometres | 130000 | 130000 | 130000 | korrekt / korrekt |
| sellerType | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| locality | Västerhaninge | Västerhaninge | Västerhaninge | korrekt / korrekt |
| county | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| publishedDate | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| updatedDate | 2026-09-08 | 2026-09-08 | 2026-09-08 | korrekt / korrekt |
| fuelTypes | ["petrol"] | ["petrol"] | ["petrol"] | korrekt / korrekt |
| transmission | manual | manual | manual | korrekt / korrekt |
| drivetrain | frontWheelDrive | frontWheelDrive | frontWheelDrive | korrekt / korrekt |
| bodyType | sedan | sedan | sedan | korrekt / korrekt |
| colour | Röd | Röd | Röd | korrekt / korrekt |
| horsepower | 125 | 125 | 125 | korrekt / korrekt |
| engineDisplacementCubicCentimetres | 1800 | 1800 | 1800 | korrekt / korrekt |
| energyConsumptions | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | [{"label": "NEDC", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | korrekt / korrekt |
| annualVehicleTaxSek | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| ownerCount | 4 | 4 | 4 | korrekt / korrekt |
| firstRegistrationDate | 1999-11-24 | 1999-11-24 | 1999-11-24 | korrekt / korrekt |
| lastInspectionDate | 2026-08-14 | 2026-08-14 | 2026-08-14 | korrekt / korrekt |
| nextInspectionDate | 2027-09-14 | 2027-09-14 | 2027-09-14 | korrekt / korrekt |
| towBar | True | True | True | korrekt / korrekt |
| equipment[1] | ABS | ABS | ABS | korrekt / korrekt |
| equipment[2] | Sidoairbags | Sidoairbags | Sidoairbags | korrekt / korrekt |
| equipment[3] | El-sidospeglar m. värme | El-sidospeglar m. värme | El-sidospeglar m. värme | korrekt / korrekt |
| equipment[4] | ESC | ESC | ESC | korrekt / korrekt |
| equipment[5] | Servostyrning | Servostyrning | Servostyrning | korrekt / korrekt |
| equipment[6] | Startspärr | Startspärr | Startspärr | korrekt / korrekt |
| equipment[7] | Airbag fram | Airbag fram | Airbag fram | korrekt / korrekt |
| equipment[8] | Aircondition | Aircondition | Aircondition | korrekt / korrekt |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | Uppvärmda säten, fram | korrekt / korrekt |
| equipment[10] | Fällbart baksäte | Fällbart baksäte | Fällbart baksäte | korrekt / korrekt |
| equipment[11] | Dragkrok, fast | Dragkrok, fast | Dragkrok, fast | korrekt / korrekt |
| equipment[12] | Centrallås | Centrallås | Centrallås | korrekt / korrekt |
| equipment[13] | Färddator | Färddator | Färddator | korrekt / korrekt |
| equipment[14] | 12V-uttag | 12V-uttag | 12V-uttag | korrekt / korrekt |
| equipment[15] | Mörktonade rutor | Mörktonade rutor | Mörktonade rutor | korrekt / korrekt |
| equipment[16] | Justerbart svankstöd | Justerbart svankstöd | Justerbart svankstöd | korrekt / korrekt |
| equipment[17] | Klimatanläggning | Klimatanläggning | Klimatanläggning | korrekt / korrekt |
| equipment[18] | Elektriska fönster | Elektriska fönster | Elektriska fönster | korrekt / korrekt |
| equipment.exactList | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | korrekt / korrekt |
| details.title | Audi A4 | Audi A4 | Audi A4 | korrekt / korrekt |
| details.subtitle | Sedan 1.8 Manuell | Sedan 1.8 Manuell | Sedan 1.8 Manuell | korrekt / korrekt |
| details.description | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | korrekt / korrekt |
| details.listingId | 26427275 | 26427275 | 26427275 | korrekt / korrekt |
| details.seats | 5 | 5 | 5 | korrekt / korrekt |
| details.doors | 4 | 4 | 4 | korrekt / korrekt |
| details.luggageLitres | 440 | 440 | 440 | korrekt / korrekt |
| details.weightKilograms | 1370 | 1370 | 1370 | korrekt / korrekt |
| details.weightLabel | Vikt | Vikt | Vikt | korrekt / korrekt |
| details.weightCategory | unspecified | unspecified | unspecified | korrekt / korrekt |
| details.trailerWeightKilograms | 1300 | 1300 | 1300 | korrekt / korrekt |
| details.trailerWeightLabel | Max trailervikt | Max trailervikt | Max trailervikt | korrekt / korrekt |
| details.trailerWeightCategory | unspecified | unspecified | unspecified | korrekt / korrekt |
| details.postalCode | 13738 | 13738 | 13738 | korrekt / korrekt |
| details.country | Sverige | Sverige | Sverige | korrekt / korrekt |
| details.feeClass | Personbil | Personbil | Personbil | korrekt / korrekt |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | Begagnad bil till salu | korrekt / korrekt |
| details.updatedLocalDateTime | 2026-09-08T16:35:00 | 2026-09-08T16:35:00 | 2026-09-08T16:35:00 | korrekt / korrekt |
| details.updatedTimeZone | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.updatedUtcOffsetMinutes | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.sellerAnswers | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| specifications.count | 25 | 25 | 25 | korrekt / korrekt |
| promptVersion | 4 | 4 | 4 | korrekt / korrekt |
| schemaVersion | 3 | 3 | 3 | korrekt / korrekt |
| sourcePageObserved | True | True | True | korrekt / korrekt |
| title[0].method | html | html | html | korrekt / korrekt |
| title[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| description[0].method | html | html | html | korrekt / korrekt |
| description[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[0].method | html | html | html | korrekt / korrekt |
| specifications[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[1].method | html | html | html | korrekt / korrekt |
| specifications[1].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[2].method | html | html | html | korrekt / korrekt |
| specifications[2].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[3].method | html | html | html | korrekt / korrekt |
| specifications[3].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[4].method | html | html | html | korrekt / korrekt |
| specifications[4].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[5].method | html | html | html | korrekt / korrekt |
| specifications[5].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[6].method | html | html | html | korrekt / korrekt |
| specifications[6].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[7].method | html | html | html | korrekt / korrekt |
| specifications[7].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[8].method | html | html | html | korrekt / korrekt |
| specifications[8].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[9].method | html | html | html | korrekt / korrekt |
| specifications[9].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[10].method | html | html | html | korrekt / korrekt |
| specifications[10].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[11].method | html | html | html | korrekt / korrekt |
| specifications[11].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[12].method | html | html | html | korrekt / korrekt |
| specifications[12].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[13].method | html | html | html | korrekt / korrekt |
| specifications[13].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[14].method | html | html | html | korrekt / korrekt |
| specifications[14].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[15].method | html | html | html | korrekt / korrekt |
| specifications[15].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[16].method | html | html | html | korrekt / korrekt |
| specifications[16].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[17].method | html | html | html | korrekt / korrekt |
| specifications[17].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[18].method | html | html | html | korrekt / korrekt |
| specifications[18].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[19].method | html | html | html | korrekt / korrekt |
| specifications[19].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[20].method | html | html | html | korrekt / korrekt |
| specifications[20].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[21].method | html | html | html | korrekt / korrekt |
| specifications[21].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[22].method | html | html | html | korrekt / korrekt |
| specifications[22].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[23].method | html | html | html | korrekt / korrekt |
| specifications[23].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[24].method | html | html | html | korrekt / korrekt |
| specifications[24].verification | unverified | unverified | unverified | korrekt / korrekt |
| Märke | Audi | Audi | Audi | korrekt / korrekt |
| Modell | A4 | A4 | A4 | korrekt / korrekt |
| Modellår | 2000 | 2000 | 2000 | korrekt / korrekt |
| Biltyp | Sedan | Sedan | Sedan | korrekt / korrekt |
| Drivmedel | Bensin | Bensin | Bensin | korrekt / korrekt |
| Effekt | 125 Hk | 125 Hk | 125 Hk | korrekt / korrekt |
| Motorvolym | 1,8 L | 1,8 L | 1,8 L | korrekt / korrekt |
| Miltal | 13 000 mil | 13 000 mil | 13 000 mil | korrekt / korrekt |
| Bränsleförbrukning (NEDC) | 8,5 L/100 km | 8,5 L/100 km | 8,5 L/100 km | korrekt / korrekt |
| Växellåda | Manuell | Manuell | Manuell | korrekt / korrekt |
| Max trailervikt | 1 300 kg | 1 300 kg | 1 300 kg | korrekt / korrekt |
| Drivhjul | Framhjulsdrift | Framhjulsdrift | Framhjulsdrift | korrekt / korrekt |
| Vikt | 1 370 kg | 1 370 kg | 1 370 kg | korrekt / korrekt |
| Säten | 5 | 5 | 5 | korrekt / korrekt |
| Antal dörrar | 4 | 4 | 4 | korrekt / korrekt |
| Bagageutrymme | 440 L | 440 L | 440 L | korrekt / korrekt |
| Färg | Röd | Röd | Röd | korrekt / korrekt |
| Bilens plats | Sverige | Sverige | Sverige | korrekt / korrekt |
| Senaste besiktningsdatum | 2026-08-14 | 2026-08-14 | 2026-08-14 | korrekt / korrekt |
| Nästa besiktningsdatum | 2027-09-14 | 2027-09-14 | 2027-09-14 | korrekt / korrekt |
| Avgiftsklass | Personbil | Personbil | Personbil | korrekt / korrekt |
| Chassinummer | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | korrekt / korrekt |
| Registreringsdatum | 1999-11-24 | 1999-11-24 | 1999-11-24 | korrekt / korrekt |
| Antal ägare | 4 | 4 | 4 | korrekt / korrekt |
| Försäljningsform | Begagnad bil till salu | Begagnad bil till salu | Begagnad bil till salu | korrekt / korrekt |

## Skoda Roomster (26434732)

| Field / check | Expected | Request 1 observed | Request 2 observed | Status 1 / 2 |
|---|---|---|---|---|
| registrationNumber | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| make | Skoda | Skoda | Skoda | korrekt / korrekt |
| model | Roomster | Roomster | Roomster | korrekt / korrekt |
| modelYear | 2008 | 2008 | 2008 | korrekt / korrekt |
| vin | TMBTHB5J185009503 | TMBTHB5J185009503 | TMBTHB5J185009503 | korrekt / korrekt |
| priceSek | 29500 | 29500 | 29500 | korrekt / korrekt |
| odometerKilometres | 129090 | 129090 | 129090 | korrekt / korrekt |
| sellerType | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| locality | Norrköping | Norrköping | Norrköping | korrekt / korrekt |
| county | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| publishedDate | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| updatedDate | 2026-09-08 | 2026-09-08 | 2026-09-08 | korrekt / korrekt |
| fuelTypes | ["petrol"] | ["petrol"] | ["petrol"] | korrekt / korrekt |
| transmission | manual | manual | manual | korrekt / korrekt |
| drivetrain | frontWheelDrive | frontWheelDrive | frontWheelDrive | korrekt / korrekt |
| bodyType | van | van | van | korrekt / korrekt |
| colour | Vit | Vit | Vit | korrekt / korrekt |
| horsepower | 69 | 69 | 69 | korrekt / korrekt |
| engineDisplacementCubicCentimetres | 1200 | 1200 | 1200 | korrekt / korrekt |
| energyConsumptions | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| annualVehicleTaxSek | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| ownerCount | 2 | 2 | 2 | korrekt / korrekt |
| firstRegistrationDate | 2007-12-05 | 2007-12-05 | 2007-12-05 | korrekt / korrekt |
| lastInspectionDate | 2026-04-21 | 2026-04-21 | 2026-04-21 | korrekt / korrekt |
| nextInspectionDate | 2027-06-30 | 2027-06-30 | 2027-06-30 | korrekt / korrekt |
| towBar | True | True | True | korrekt / korrekt |
| equipment[1] | ABS | ABS | ABS | korrekt / korrekt |
| equipment[2] | Sidoairbags | Sidoairbags | Sidoairbags | korrekt / korrekt |
| equipment[3] | Antispinn | Antispinn | Antispinn | korrekt / korrekt |
| equipment[4] | ESC | ESC | ESC | korrekt / korrekt |
| equipment[5] | Takrails | Takrails | Takrails | korrekt / korrekt |
| equipment[6] | Servostyrning | Servostyrning | Servostyrning | korrekt / korrekt |
| equipment[7] | Startspärr | Startspärr | Startspärr | korrekt / korrekt |
| equipment[8] | Airbag fram | Airbag fram | Airbag fram | korrekt / korrekt |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | Uppvärmda säten, fram | korrekt / korrekt |
| equipment[10] | Dragkrok, fast | Dragkrok, fast | Dragkrok, fast | korrekt / korrekt |
| equipment[11] | Centrallås | Centrallås | Centrallås | korrekt / korrekt |
| equipment[12] | 12V-uttag | 12V-uttag | 12V-uttag | korrekt / korrekt |
| equipment[13] | Klimatanläggning | Klimatanläggning | Klimatanläggning | korrekt / korrekt |
| equipment[14] | AUX-ingång | AUX-ingång | AUX-ingång | korrekt / korrekt |
| equipment[15] | CD-spelare | CD-spelare | CD-spelare | korrekt / korrekt |
| equipment.exactList | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | korrekt / korrekt |
| details.title | Skoda Roomster | Skoda Roomster | Skoda Roomster | korrekt / korrekt |
| details.subtitle | Praktik 1.2 12v Manuell | Praktik 1.2 12v Manuell | Praktik 1.2 12v Manuell | korrekt / korrekt |
| details.description | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | korrekt / korrekt |
| details.listingId | 26434732 | 26434732 | 26434732 | korrekt / korrekt |
| details.seats | 2 | 2 | 2 | korrekt / korrekt |
| details.doors | 4 | 4 | 4 | korrekt / korrekt |
| details.luggageLitres | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.weightKilograms | 1240 | 1240 | 1240 | korrekt / korrekt |
| details.weightLabel | Vikt | Vikt | Vikt | korrekt / korrekt |
| details.weightCategory | unspecified | unspecified | unspecified | korrekt / korrekt |
| details.trailerWeightKilograms | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.trailerWeightLabel | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.trailerWeightCategory | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.postalCode | 60209 | 60209 | 60209 | korrekt / korrekt |
| details.country | Sverige | Sverige | Sverige | korrekt / korrekt |
| details.feeClass | Transportbil | Transportbil | Transportbil | korrekt / korrekt |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | Begagnad bil till salu | korrekt / korrekt |
| details.updatedLocalDateTime | 2026-09-08T20:15:00 | 2026-09-08T20:15:00 | 2026-09-08T20:15:00 | korrekt / korrekt |
| details.updatedTimeZone | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.updatedUtcOffsetMinutes | Unknown (null) | Unknown (null) | Unknown (null) | korrekt / korrekt |
| details.sellerAnswers | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | korrekt / korrekt |
| specifications.count | 22 | 22 | 22 | korrekt / korrekt |
| promptVersion | 4 | 4 | 4 | korrekt / korrekt |
| schemaVersion | 3 | 3 | 3 | korrekt / korrekt |
| sourcePageObserved | True | True | True | korrekt / korrekt |
| title[0].method | html | html | html | korrekt / korrekt |
| title[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| description[0].method | html | html | html | korrekt / korrekt |
| description[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[0].method | html | html | html | korrekt / korrekt |
| specifications[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[1].method | html | html | html | korrekt / korrekt |
| specifications[1].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[2].method | html | html | html | korrekt / korrekt |
| specifications[2].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[3].method | html | html | html | korrekt / korrekt |
| specifications[3].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[4].method | html | html | html | korrekt / korrekt |
| specifications[4].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[5].method | html | html | html | korrekt / korrekt |
| specifications[5].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[6].method | html | html | html | korrekt / korrekt |
| specifications[6].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[7].method | html | html | html | korrekt / korrekt |
| specifications[7].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[8].method | html | html | html | korrekt / korrekt |
| specifications[8].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[9].method | html | html | html | korrekt / korrekt |
| specifications[9].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[10].method | html | html | html | korrekt / korrekt |
| specifications[10].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[11].method | html | html | html | korrekt / korrekt |
| specifications[11].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[12].method | html | html | html | korrekt / korrekt |
| specifications[12].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[13].method | html | html | html | korrekt / korrekt |
| specifications[13].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[14].method | html | html | html | korrekt / korrekt |
| specifications[14].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[15].method | html | html | html | korrekt / korrekt |
| specifications[15].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[16].method | html | html | html | korrekt / korrekt |
| specifications[16].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[17].method | html | html | html | korrekt / korrekt |
| specifications[17].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[18].method | html | html | html | korrekt / korrekt |
| specifications[18].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[19].method | html | html | html | korrekt / korrekt |
| specifications[19].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[20].method | html | html | html | korrekt / korrekt |
| specifications[20].verification | unverified | unverified | unverified | korrekt / korrekt |
| specifications[21].method | html | html | html | korrekt / korrekt |
| specifications[21].verification | unverified | unverified | unverified | korrekt / korrekt |
| sellerAnswers[0].method | html | html | html | korrekt / korrekt |
| sellerAnswers[0].verification | unverified | unverified | unverified | korrekt / korrekt |
| Märke | Skoda | Skoda | Skoda | korrekt / korrekt |
| Modell | Roomster | Roomster | Roomster | korrekt / korrekt |
| Modellår | 2008 | 2008 | 2008 | korrekt / korrekt |
| Biltyp | Skåpbil | Skåpbil | Skåpbil | korrekt / korrekt |
| Drivmedel | Bensin | Bensin | Bensin | korrekt / korrekt |
| Effekt | 69 Hk | 69 Hk | 69 Hk | korrekt / korrekt |
| Motorvolym | 1,2 L | 1,2 L | 1,2 L | korrekt / korrekt |
| Miltal | 12 909 mil | 12 909 mil | 12 909 mil | korrekt / korrekt |
| Växellåda | Manuell | Manuell | Manuell | korrekt / korrekt |
| Drivhjul | Framhjulsdrift | Framhjulsdrift | Framhjulsdrift | korrekt / korrekt |
| Vikt | 1 240 kg | 1 240 kg | 1 240 kg | korrekt / korrekt |
| Säten | 2 | 2 | 2 | korrekt / korrekt |
| Antal dörrar | 4 | 4 | 4 | korrekt / korrekt |
| Färg | Vit | Vit | Vit | korrekt / korrekt |
| Bilens plats | Sverige | Sverige | Sverige | korrekt / korrekt |
| Senaste besiktningsdatum | 2026-04-21 | 2026-04-21 | 2026-04-21 | korrekt / korrekt |
| Nästa besiktningsdatum | 2027-06-30 | 2027-06-30 | 2027-06-30 | korrekt / korrekt |
| Avgiftsklass | Transportbil | Transportbil | Transportbil | korrekt / korrekt |
| Chassinummer | TMBTHB5J185009503 | TMBTHB5J185009503 | TMBTHB5J185009503 | korrekt / korrekt |
| Registreringsdatum | 2007-12-05 | 2007-12-05 | 2007-12-05 | korrekt / korrekt |
| Antal ägare | 2 | 2 | 2 | korrekt / korrekt |
| Försäljningsform | Begagnad bil till salu | Begagnad bil till salu | Begagnad bil till salu | korrekt / korrekt |
