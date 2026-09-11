# Listing text probe: field matrix

> Integration follow-up: the approved direct-retrieval flow is now implemented
> and four application live checks passed. See the [current acceptance report](listing-extraction-verification-report.md)
> and [final field matrix](listing-scraper-live-matrix.md). The probe results below
> are preserved as the earlier diagnostic record.


See the [experiment report](listing-text-retrieval-probe.md) for all attempts, limitations and the exact-source fixtures. Raw calls received only the URL. Final text controls received the user-supplied text with web search disabled and reused the production field instructions. This matrix does not mark the URL feature complete.

The final field checks include expected unknowns. Equipment membership and exact section fidelity are separate: an extra description-derived item must not be hidden by passing membership checks.

## audi: URL transcription

| Original label | Expected value | Observed text | Status |
|---|---|---|---|
| Totalt pris | 28 888 kr | 28 888 kr | correct |
| Märke | Audi | Audi | correct |
| Modell | A4 | A4 | correct |
| Modellår | 2000 | 2000 | correct |
| Biltyp | Sedan | Sedan | correct |
| Drivmedel | Bensin | Bensin | correct |
| Effekt | 125 Hk | 125 Hk | correct |
| Motorvolym | 1,8 L | 1,8 L | correct |
| Miltal | 13 000 mil | 13 000 mil | correct |
| Bränsleförbrukning (NEDC) | 8,5 L/100 km | NEDC var den officiella testcykeln i Europa för att mäta bränsleförbrukning och utsläpp fram till september 2018<br>8,5 L/100 km | correct |
| Växellåda | Manuell | Manuell | correct |
| Max trailervikt | 1 300 kg | 1 300 kg | correct |
| Drivhjul | Framhjulsdrift | Framhjulsdrift | correct |
| Vikt | 1 370 kg | 1 370 kg | correct |
| Säten | 5 | 5 | correct |
| Antal dörrar | 4 | 4 | correct |
| Bagageutrymme | 440 L | 440 L | correct |
| Färg | Röd | Röd | correct |
| Bilens plats | Sverige | Sverige | correct |
| Senaste besiktningsdatum | 2026-08-14 | 2026-08-14 | correct |
| Nästa besiktningsdatum | 2027-09-14 | 2027-09-14 | correct |
| Avgiftsklass | Personbil | Personbil | correct |
| Chassinummer | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | correct |
| Registreringsdatum | 1999-11-24 | Unknown | missing |
| Antal ägare | 4 | Unknown | missing |
| Försäljningsform | Begagnad bil till salu | Unknown | missing |

## audi: final text interpretation

| Field | Expected | Observed | Status |
|---|---|---|---|
| registrationNumber | Unknown | Unknown | correct |
| make | Audi | Audi | correct |
| model | A4 | A4 | correct |
| modelYear | 2000 | 2000 | correct |
| vin | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | correct |
| priceSek | 28888 | 28888 | correct |
| odometerKilometres | 130000 | 130000 | correct |
| sellerType | Unknown | Unknown | correct |
| locality | Västerhaninge | Västerhaninge | correct |
| county | Unknown | Unknown | correct |
| publishedDate | Unknown | Unknown | correct |
| updatedDate | 2026-09-08 | 2026-09-08 | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| bodyType | sedan | sedan | correct |
| colour | Röd | Röd | correct |
| horsepower | 125 | 125 | correct |
| engineDisplacementCubicCentimetres | 1800 | 1800 | correct |
| energyConsumptions | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | correct |
| annualVehicleTaxSek | Unknown | Unknown | correct |
| ownerCount | 4 | 4 | correct |
| firstRegistrationDate | 1999-11-24 | 1999-11-24 | correct |
| lastInspectionDate | 2026-08-14 | 2026-08-14 | correct |
| nextInspectionDate | 2027-09-14 | 2027-09-14 | correct |
| towBar | true | true | correct |
| equipment[1] | ABS | ABS | correct |
| equipment[2] | Sidoairbags | Sidoairbags | correct |
| equipment[3] | El-sidospeglar m. värme | El-sidospeglar m. värme | correct |
| equipment[4] | ESC | ESC | correct |
| equipment[5] | Servostyrning | Servostyrning | correct |
| equipment[6] | Startspärr | Startspärr | correct |
| equipment[7] | Airbag fram | Airbag fram | correct |
| equipment[8] | Aircondition | Aircondition | correct |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | correct |
| equipment[10] | Fällbart baksäte | Fällbart baksäte | correct |
| equipment[11] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[12] | Centrallås | Centrallås | correct |
| equipment[13] | Färddator | Färddator | correct |
| equipment[14] | 12V-uttag | 12V-uttag | correct |
| equipment[15] | Mörktonade rutor | Mörktonade rutor | correct |
| equipment[16] | Justerbart svankstöd | Justerbart svankstöd | correct |
| equipment[17] | Klimatanläggning | Klimatanläggning | correct |
| equipment[18] | Elektriska fönster | Elektriska fönster | correct |
| details.title | Audi A4 | Audi A4 | correct |
| details.subtitle | Sedan 1.8 Manuell | Sedan 1.8 Manuell | correct |
| details.description | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | correct |
| details.listingId | 26427275 | 26427275 | correct |
| details.seats | 5 | 5 | correct |
| details.doors | 4 | 4 | correct |
| details.luggageLitres | 440 | 440 | correct |
| details.weightKilograms | 1370 | 1370 | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightKilograms | 1300 | 1300 | correct |
| details.trailerWeightLabel | Max trailervikt | Max trailervikt | correct |
| details.trailerWeightCategory | unspecified | unspecified | correct |
| details.postalCode | 13738 | 13738 | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Personbil | Personbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedLocalDateTime | 2026-09-08T16:35:00 | 2026-09-08T16:35:00 | correct |
| details.updatedTimeZone | Unknown | Unknown | correct |
| details.updatedUtcOffsetMinutes | Unknown | Unknown | correct |
| details.sellerAnswers | Unknown | Unknown | correct |

Exact equipment list: passed.

## skoda: URL transcription

| Original label | Expected value | Observed text | Status |
|---|---|---|---|
| Totalt pris | 29 500 kr | Unknown | missing |
| Märke | Skoda | Unknown | missing |
| Modell | Roomster | Unknown | missing |
| Modellår | 2008 | Unknown | missing |
| Biltyp | Skåpbil | Unknown | missing |
| Drivmedel | Bensin | Unknown | missing |
| Effekt | 69 Hk | Unknown | missing |
| Motorvolym | 1,2 L | Unknown | missing |
| Miltal | 12 909 mil | Unknown | missing |
| Växellåda | Manuell | Unknown | missing |
| Drivhjul | Framhjulsdrift | Unknown | missing |
| Vikt | 1 240 kg | Unknown | missing |
| Säten | 2 | Unknown | missing |
| Antal dörrar | 4 | Unknown | missing |
| Färg | Vit | Unknown | missing |
| Bilens plats | Sverige | Unknown | missing |
| Senaste besiktningsdatum | 2026-04-21 | Unknown | missing |
| Nästa besiktningsdatum | 2027-06-30 | Unknown | missing |
| Avgiftsklass | Transportbil | Unknown | missing |
| Chassinummer | TMBTHB5J185009503 | Unknown | missing |
| Registreringsdatum | 2007-12-05 | Unknown | missing |
| Antal ägare | 2 | Unknown | missing |
| Försäljningsform | Begagnad bil till salu | Unknown | missing |

## skoda: final text interpretation

| Field | Expected | Observed | Status |
|---|---|---|---|
| registrationNumber | Unknown | Unknown | correct |
| make | Skoda | Skoda | correct |
| model | Roomster | Roomster | correct |
| modelYear | 2008 | 2008 | correct |
| vin | TMBTHB5J185009503 | TMBTHB5J185009503 | correct |
| priceSek | 29500 | 29500 | correct |
| odometerKilometres | 129090 | 129090 | correct |
| sellerType | Unknown | Unknown | correct |
| locality | Norrköping | Norrköping | correct |
| county | Unknown | Unknown | correct |
| publishedDate | Unknown | Unknown | correct |
| updatedDate | 2026-09-08 | 2026-09-08 | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| bodyType | van | van | correct |
| colour | Vit | Vit | correct |
| horsepower | 69 | 69 | correct |
| engineDisplacementCubicCentimetres | 1200 | 1200 | correct |
| energyConsumptions | Unknown | Unknown | correct |
| annualVehicleTaxSek | Unknown | Unknown | correct |
| ownerCount | 2 | 2 | correct |
| firstRegistrationDate | 2007-12-05 | 2007-12-05 | correct |
| lastInspectionDate | 2026-04-21 | 2026-04-21 | correct |
| nextInspectionDate | 2027-06-30 | 2027-06-30 | correct |
| towBar | true | true | correct |
| equipment[1] | ABS | ABS | correct |
| equipment[2] | Sidoairbags | Sidoairbags | correct |
| equipment[3] | Antispinn | Antispinn | correct |
| equipment[4] | ESC | ESC | correct |
| equipment[5] | Takrails | Takrails | correct |
| equipment[6] | Servostyrning | Servostyrning | correct |
| equipment[7] | Startspärr | Startspärr | correct |
| equipment[8] | Airbag fram | Airbag fram | correct |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | correct |
| equipment[10] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[11] | Centrallås | Centrallås | correct |
| equipment[12] | 12V-uttag | 12V-uttag | correct |
| equipment[13] | Klimatanläggning | Klimatanläggning | correct |
| equipment[14] | AUX-ingång | AUX-ingång | correct |
| equipment[15] | CD-spelare | CD-spelare | correct |
| details.title | Skoda Roomster | Skoda Roomster | correct |
| details.subtitle | Praktik 1.2 12v Manuell | Praktik 1.2 12v Manuell | correct |
| details.description | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | correct |
| details.listingId | 26434732 | 26434732 | correct |
| details.seats | 2 | 2 | correct |
| details.doors | 4 | 4 | correct |
| details.luggageLitres | Unknown | Unknown | correct |
| details.weightKilograms | 1240 | 1240 | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightKilograms | Unknown | Unknown | correct |
| details.trailerWeightLabel | Unknown | Unknown | correct |
| details.trailerWeightCategory | Unknown | Unknown | correct |
| details.postalCode | 60209 | 60209 | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Transportbil | Transportbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedLocalDateTime | 2026-09-08T20:15:00 | 2026-09-08T20:15:00 | correct |
| details.updatedTimeZone | Unknown | Unknown | correct |
| details.updatedUtcOffsetMinutes | Unknown | Unknown | correct |
| details.sellerAnswers | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | correct |

Exact equipment list: failed: the 15 source equipment entries are retained, with an additional winter-wheel entry from the description.

## Direct HTTP follow-up

These source rows came from fresh direct HTTP responses. The model received only those retrieved sections, with web search disabled; references stayed in the checker. See the [follow-up report](listing-text-retrieval-probe.md#follow-up-direct-html-retrieval).

### audi: HTML section extraction

| Field | Expected | Observed | Status |
|---|---|---|---|
| Märke | Audi | Audi | correct |
| Modell | A4 | A4 | correct |
| Modellår | 2000 | 2000 | correct |
| Biltyp | Sedan | Sedan | correct |
| Drivmedel | Bensin | Bensin | correct |
| Effekt | 125 Hk | 125 Hk | correct |
| Motorvolym | 1,8 L | 1,8 L | correct |
| Miltal | 13 000 mil | 13 000 mil | correct |
| Bränsleförbrukning (NEDC) | 8,5 L/100 km | 8,5 L/100 km | correct |
| Växellåda | Manuell | Manuell | correct |
| Max trailervikt | 1 300 kg | 1 300 kg | correct |
| Drivhjul | Framhjulsdrift | Framhjulsdrift | correct |
| Vikt | 1 370 kg | 1 370 kg | correct |
| Säten | 5 | 5 | correct |
| Antal dörrar | 4 | 4 | correct |
| Bagageutrymme | 440 L | 440 L | correct |
| Färg | Röd | Röd | correct |
| Bilens plats | Sverige | Sverige | correct |
| Senaste besiktningsdatum | 2026-08-14 | 2026-08-14 | correct |
| Nästa besiktningsdatum | 2027-09-14 | 2027-09-14 | correct |
| Avgiftsklass | Personbil | Personbil | correct |
| Chassinummer | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | correct |
| Registreringsdatum | 1999-11-24 | 1999-11-24 | correct |
| Antal ägare | 4 | 4 | correct |
| Försäljningsform | Begagnad bil till salu | Begagnad bil till salu | correct |
| title | Audi A4 | Audi A4 | correct |
| subtitle | Sedan 1.8 Manuell | Sedan 1.8 Manuell | correct |
| description | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | correct |
| equipment | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | correct |
| sellerAnswers | Unknown | Unknown | correct |
| location | 13738 Västerhaninge | 13738 Västerhaninge | correct |
| price | 28 888 kr | 28 888 kr | correct |
| Annons-ID | 26427275 | 26427275 | correct |
| Uppdaterad | 8 september 2026, 16:35 | 8 september 2026, 16:35 | correct |

### audi: AI interpretation of fetched sections

| Field | Expected | Observed | Status |
|---|---|---|---|
| registrationNumber | Unknown | Unknown | correct |
| make | Audi | Audi | correct |
| model | A4 | A4 | correct |
| modelYear | 2000 | 2000 | correct |
| vin | WAUZZZ8DZYA044660 | WAUZZZ8DZYA044660 | correct |
| priceSek | 28888 | 28888 | correct |
| odometerKilometres | 130000 | 130000 | correct |
| sellerType | Unknown | Unknown | correct |
| locality | Västerhaninge | Västerhaninge | correct |
| county | Unknown | Unknown | correct |
| publishedDate | Unknown | Unknown | correct |
| updatedDate | 2026-09-08 | 2026-09-08 | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| bodyType | sedan | sedan | correct |
| colour | Röd | Röd | correct |
| horsepower | 125 | 125 | correct |
| engineDisplacementCubicCentimetres | 1800 | 1800 | correct |
| energyConsumptions | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | correct |
| annualVehicleTaxSek | Unknown | Unknown | correct |
| ownerCount | 4 | 4 | correct |
| firstRegistrationDate | 1999-11-24 | 1999-11-24 | correct |
| lastInspectionDate | 2026-08-14 | 2026-08-14 | correct |
| nextInspectionDate | 2027-09-14 | 2027-09-14 | correct |
| towBar | true | true | correct |
| equipment[1] | ABS | ABS | correct |
| equipment[2] | Sidoairbags | Sidoairbags | correct |
| equipment[3] | El-sidospeglar m. värme | El-sidospeglar m. värme | correct |
| equipment[4] | ESC | ESC | correct |
| equipment[5] | Servostyrning | Servostyrning | correct |
| equipment[6] | Startspärr | Startspärr | correct |
| equipment[7] | Airbag fram | Airbag fram | correct |
| equipment[8] | Aircondition | Aircondition | correct |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | correct |
| equipment[10] | Fällbart baksäte | Fällbart baksäte | correct |
| equipment[11] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[12] | Centrallås | Centrallås | correct |
| equipment[13] | Färddator | Färddator | correct |
| equipment[14] | 12V-uttag | 12V-uttag | correct |
| equipment[15] | Mörktonade rutor | Mörktonade rutor | correct |
| equipment[16] | Justerbart svankstöd | Justerbart svankstöd | correct |
| equipment[17] | Klimatanläggning | Klimatanläggning | correct |
| equipment[18] | Elektriska fönster | Elektriska fönster | correct |
| details.title | Audi A4 | Audi A4 | correct |
| details.subtitle | Sedan 1.8 Manuell | Sedan 1.8 Manuell | correct |
| details.description | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | correct |
| details.listingId | 26427275 | 26427275 | correct |
| details.seats | 5 | 5 | correct |
| details.doors | 4 | 4 | correct |
| details.luggageLitres | 440 | 440 | correct |
| details.weightKilograms | 1370 | 1370 | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightKilograms | 1300 | 1300 | correct |
| details.trailerWeightLabel | Max trailervikt | Max trailervikt | correct |
| details.trailerWeightCategory | unspecified | unspecified | correct |
| details.postalCode | 13738 | 13738 | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Personbil | Personbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedLocalDateTime | 2026-09-08T16:35:00 | 2026-09-08T16:35:00 | correct |
| details.updatedTimeZone | Unknown | Unknown | correct |
| details.updatedUtcOffsetMinutes | Unknown | Unknown | correct |
| details.sellerAnswers | Unknown | Unknown | correct |
| equipment.exactList | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | ["ABS", "Sidoairbags", "El-sidospeglar m. värme", "ESC", "Servostyrning", "Startspärr", "Airbag fram", "Aircondition", "Uppvärmda säten, fram", "Fällbart baksäte", "Dragkrok, fast", "Centrallås", "Färddator", "12V-uttag", "Mörktonade rutor", "Justerbart svankstöd", "Klimatanläggning", "Elektriska fönster"] | correct |

### skoda: HTML section extraction

| Field | Expected | Observed | Status |
|---|---|---|---|
| Märke | Skoda | Skoda | correct |
| Modell | Roomster | Roomster | correct |
| Modellår | 2008 | 2008 | correct |
| Biltyp | Skåpbil | Skåpbil | correct |
| Drivmedel | Bensin | Bensin | correct |
| Effekt | 69 Hk | 69 Hk | correct |
| Motorvolym | 1,2 L | 1,2 L | correct |
| Miltal | 12 909 mil | 12 909 mil | correct |
| Växellåda | Manuell | Manuell | correct |
| Drivhjul | Framhjulsdrift | Framhjulsdrift | correct |
| Vikt | 1 240 kg | 1 240 kg | correct |
| Säten | 2 | 2 | correct |
| Antal dörrar | 4 | 4 | correct |
| Färg | Vit | Vit | correct |
| Bilens plats | Sverige | Sverige | correct |
| Senaste besiktningsdatum | 2026-04-21 | 2026-04-21 | correct |
| Nästa besiktningsdatum | 2027-06-30 | 2027-06-30 | correct |
| Avgiftsklass | Transportbil | Transportbil | correct |
| Chassinummer | TMBTHB5J185009503 | TMBTHB5J185009503 | correct |
| Registreringsdatum | 2007-12-05 | 2007-12-05 | correct |
| Antal ägare | 2 | 2 | correct |
| Försäljningsform | Begagnad bil till salu | Begagnad bil till salu | correct |
| title | Skoda Roomster | Skoda Roomster | correct |
| subtitle | Praktik 1.2 12v Manuell | Praktik 1.2 12v Manuell | correct |
| description | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | correct |
| equipment | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | correct |
| sellerAnswers | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | correct |
| location | 60209 Norrköping | 60209 Norrköping | correct |
| price | 29 500 kr | 29 500 kr | correct |
| Annons-ID | 26434732 | 26434732 | correct |
| Uppdaterad | 8 september 2026, 20:15 | 8 september 2026, 20:15 | correct |

### skoda: AI interpretation of fetched sections

| Field | Expected | Observed | Status |
|---|---|---|---|
| registrationNumber | Unknown | Unknown | correct |
| make | Skoda | Skoda | correct |
| model | Roomster | Roomster | correct |
| modelYear | 2008 | 2008 | correct |
| vin | TMBTHB5J185009503 | TMBTHB5J185009503 | correct |
| priceSek | 29500 | 29500 | correct |
| odometerKilometres | 129090 | 129090 | correct |
| sellerType | Unknown | Unknown | correct |
| locality | Norrköping | Norrköping | correct |
| county | Unknown | Unknown | correct |
| publishedDate | Unknown | Unknown | correct |
| updatedDate | 2026-09-08 | 2026-09-08 | correct |
| fuelTypes | ["petrol"] | ["petrol"] | correct |
| transmission | manual | manual | correct |
| drivetrain | frontWheelDrive | frontWheelDrive | correct |
| bodyType | van | van | correct |
| colour | Vit | Vit | correct |
| horsepower | 69 | 69 | correct |
| engineDisplacementCubicCentimetres | 1200 | 1200 | correct |
| energyConsumptions | Unknown | Unknown | correct |
| annualVehicleTaxSek | Unknown | Unknown | correct |
| ownerCount | 2 | 2 | correct |
| firstRegistrationDate | 2007-12-05 | 2007-12-05 | correct |
| lastInspectionDate | 2026-04-21 | 2026-04-21 | correct |
| nextInspectionDate | 2027-06-30 | 2027-06-30 | correct |
| towBar | true | true | correct |
| equipment[1] | ABS | ABS | correct |
| equipment[2] | Sidoairbags | Sidoairbags | correct |
| equipment[3] | Antispinn | Antispinn | correct |
| equipment[4] | ESC | ESC | correct |
| equipment[5] | Takrails | Takrails | correct |
| equipment[6] | Servostyrning | Servostyrning | correct |
| equipment[7] | Startspärr | Startspärr | correct |
| equipment[8] | Airbag fram | Airbag fram | correct |
| equipment[9] | Uppvärmda säten, fram | Uppvärmda säten, fram | correct |
| equipment[10] | Dragkrok, fast | Dragkrok, fast | correct |
| equipment[11] | Centrallås | Centrallås | correct |
| equipment[12] | 12V-uttag | 12V-uttag | correct |
| equipment[13] | Klimatanläggning | Klimatanläggning | correct |
| equipment[14] | AUX-ingång | AUX-ingång | correct |
| equipment[15] | CD-spelare | CD-spelare | correct |
| details.title | Skoda Roomster | Skoda Roomster | correct |
| details.subtitle | Praktik 1.2 12v Manuell | Praktik 1.2 12v Manuell | correct |
| details.description | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | correct |
| details.listingId | 26434732 | 26434732 | correct |
| details.seats | 2 | 2 | correct |
| details.doors | 4 | 4 | correct |
| details.luggageLitres | Unknown | Unknown | correct |
| details.weightKilograms | 1240 | 1240 | correct |
| details.weightLabel | Vikt | Vikt | correct |
| details.weightCategory | unspecified | unspecified | correct |
| details.trailerWeightKilograms | Unknown | Unknown | correct |
| details.trailerWeightLabel | Unknown | Unknown | correct |
| details.trailerWeightCategory | Unknown | Unknown | correct |
| details.postalCode | 60209 | 60209 | correct |
| details.country | Sverige | Sverige | correct |
| details.feeClass | Transportbil | Transportbil | correct |
| details.saleForm | Begagnad bil till salu | Begagnad bil till salu | correct |
| details.updatedLocalDateTime | 2026-09-08T20:15:00 | 2026-09-08T20:15:00 | correct |
| details.updatedTimeZone | Unknown | Unknown | correct |
| details.updatedUtcOffsetMinutes | Unknown | Unknown | correct |
| details.sellerAnswers | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | correct |
| equipment.exactList | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | ["ABS", "Sidoairbags", "Antispinn", "ESC", "Takrails", "Servostyrning", "Startspärr", "Airbag fram", "Uppvärmda säten, fram", "Dragkrok, fast", "Centrallås", "12V-uttag", "Klimatanläggning", "AUX-ingång", "CD-spelare"] | correct |
