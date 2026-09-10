# Live listing reference matrix

The reference is the user-supplied text, independently opened in Chromium on 2026-09-10. It was never sent to the extraction model. `correct` means the returned field matches the reference; `missing` means a supplied reference value was omitted; `incorrect` means a different value or an unsupported assertion. An expected unknown returned as null is correct. All observations remain unconfirmed listing information.

Two final independent API/Nginx requests were made for each URL. Entire descriptions are compared including paragraph separators, and equipment is checked entry by entry. Original normalization of make/model/variant is not required to reproduce the subtitle twice. Supplementary name/value duplicates are reviewed separately below.

## audi-a4: 26427275

[Source advertisement](https://www.blocket.se/mobility/item/26427275?ci=3) · [Exact fixture](../tests/fixtures/listings/audi-a4.json)

| Field | Expected | Final 1 | Final 2 |
|---|---|---|---|
| registrationNumber | Not supplied (must remain unknown) | correct | correct |
| make | Audi | correct | correct |
| model | A4 | correct | correct |
| modelYear | 2000 | correct | correct |
| vin | WAUZZZ8DZYA044660 | correct | correct |
| priceSek | 28888 | correct | correct |
| odometerKilometres | 130000 | correct | correct |
| sellerType | Not supplied (must remain unknown) | correct | correct |
| locality | Västerhaninge | correct | correct |
| county | Not supplied (must remain unknown) | correct | correct |
| publishedDate | Not supplied (must remain unknown) | correct | correct |
| updatedDate | 2026-09-08 | correct | correct |
| fuelTypes | ["petrol"] | correct | correct |
| transmission | manual | correct | correct |
| drivetrain | frontWheelDrive | correct | correct |
| bodyType | sedan | correct | correct |
| colour | Röd | correct | correct |
| horsepower | 125 | correct | correct |
| engineDisplacementCubicCentimetres | 1800 | correct | correct |
| energyConsumptions | [{"label": "Bränsleförbrukning (NEDC)", "unit": "litre", "consumptionPer100Kilometres": "8.5"}] | correct | correct |
| annualVehicleTaxSek | Not supplied (must remain unknown) | correct | correct |
| ownerCount | 4 | missing | missing |
| firstRegistrationDate | 1999-11-24 | missing | missing |
| lastInspectionDate | 2026-08-14 | correct | correct |
| nextInspectionDate | 2027-09-14 | correct | correct |
| towBar | true | correct | correct |
| equipment[1] | ABS | correct | correct |
| equipment[2] | Sidoairbags | correct | correct |
| equipment[3] | El-sidospeglar m. värme | correct | correct |
| equipment[4] | ESC | correct | correct |
| equipment[5] | Servostyrning | correct | correct |
| equipment[6] | Startspärr | correct | correct |
| equipment[7] | Airbag fram | correct | correct |
| equipment[8] | Aircondition | correct | correct |
| equipment[9] | Uppvärmda säten, fram | correct | correct |
| equipment[10] | Fällbart baksäte | correct | correct |
| equipment[11] | Dragkrok, fast | correct | correct |
| equipment[12] | Centrallås | correct | correct |
| equipment[13] | Färddator | correct | correct |
| equipment[14] | 12V-uttag | correct | correct |
| equipment[15] | Mörktonade rutor | correct | correct |
| equipment[16] | Justerbart svankstöd | correct | correct |
| equipment[17] | Klimatanläggning | correct | correct |
| equipment[18] | Elektriska fönster | correct | correct |
| details.title | Audi A4 | correct | correct |
| details.subtitle | Sedan 1.8 Manuell | correct | correct |
| details.description | Säljer min Audi A4 som varit mitt lilla retroprojekt. Bilen är i väldigt fint skick och har endast gått ca 13 000 mil.<br><br>All service är gjord och mycket har bytts ut under tiden jag haft bilen. Servicebok finns.<br><br>Bilen kommer med 2 uppsättningar hjul.<br><br>Nyligen besiktigad och gick igenom utan några som helst problem.<br><br>En riktigt fin och välskött bil för sin ålder, perfekt för någon som uppskattar äldre Audi och vill ha ett fint retroprojekt. | correct | correct |
| details.listingId | 26427275 | correct | correct |
| details.seats | 5 | correct | correct |
| details.doors | 4 | correct | correct |
| details.luggageLitres | 440 | correct | correct |
| details.weightKilograms | 1370 | correct | correct |
| details.weightLabel | Vikt | correct | correct |
| details.weightCategory | unspecified | correct | correct |
| details.trailerWeightKilograms | 1300 | correct | correct |
| details.trailerWeightLabel | Max trailervikt | correct | correct |
| details.trailerWeightCategory | unspecified | correct | correct |
| details.postalCode | 13738 | correct | correct |
| details.country | Sverige | correct | correct |
| details.feeClass | Personbil | correct | correct |
| details.saleForm | Begagnad bil till salu | missing | missing |
| details.updatedLocalDateTime | 2026-09-08T16:35:00 | correct | correct |
| details.updatedTimeZone | Not supplied (must remain unknown) | correct | correct |
| details.updatedUtcOffsetMinutes | Not supplied (must remain unknown) | correct | correct |
| details.sellerAnswers | Not supplied (must remain unknown) | correct | correct |

### Additional normalized/generated fields

- Final 1: variant `1.8`, image count `10`, supplementary specifications: [{"name": "Avgiftsklass", "value": "Personbil"}].
  Seller summary: ["Säljaren uppger att bilen är i väldigt fint skick.", "Säljaren uppger att all service är gjord och att mycket har bytts ut.", "Säljaren uppger att servicebok finns.", "Säljaren uppger att bilen har två uppsättningar hjul.", "Säljaren uppger att bilen nyligen besiktigades utan problem."]. Condition summary: ["Retroprojekt", "Välskött bil för sin ålder"].
- Final 2: variant `1.8`, image count `10`, supplementary specifications: [{"name": "Bilens plats", "value": "Sverige"}, {"name": "Avgiftsklass", "value": "Personbil"}].
  Seller summary: ["Bilen är i väldigt fint skick.", "All service är gjord.", "Mycket har bytts ut under tiden säljaren haft bilen.", "Servicebok finns.", "Bilen kommer med 2 uppsättningar hjul.", "Nyligen besiktigad och gick igenom utan några som helst problem."]. Condition summary: ["Retroprojekt", "Välskött bil för sin ålder"].

Equipment order/count and source flags are checked separately from membership:
- Final 1: 18 entries; exact sequence match: True; sourcePageObserved: False; status: partial; {"correct": 62, "missing": 3, "incorrect": 0}.
- Final 2: 18 entries; exact sequence match: True; sourcePageObserved: False; status: partial; {"correct": 62, "missing": 3, "incorrect": 0}.

## skoda-roomster: 26434732

[Source advertisement](https://www.blocket.se/mobility/item/26434732?ci=3) · [Exact fixture](../tests/fixtures/listings/skoda-roomster.json)

| Field | Expected | Final 1 | Final 2 |
|---|---|---|---|
| registrationNumber | Not supplied (must remain unknown) | correct | correct |
| make | Skoda | correct | missing |
| model | Roomster | correct | missing |
| modelYear | 2008 | correct | missing |
| vin | TMBTHB5J185009503 | correct | missing |
| priceSek | 29500 | correct | missing |
| odometerKilometres | 129090 | correct | missing |
| sellerType | Not supplied (must remain unknown) | correct | correct |
| locality | Norrköping | correct | missing |
| county | Not supplied (must remain unknown) | correct | correct |
| publishedDate | Not supplied (must remain unknown) | correct | correct |
| updatedDate | 2026-09-08 | correct | missing |
| fuelTypes | ["petrol"] | correct | missing |
| transmission | manual | correct | missing |
| drivetrain | frontWheelDrive | correct | missing |
| bodyType | van | correct | missing |
| colour | Vit | correct | missing |
| horsepower | 69 | correct | missing |
| engineDisplacementCubicCentimetres | 1200 | correct | missing |
| energyConsumptions | Not supplied (must remain unknown) | correct | correct |
| annualVehicleTaxSek | Not supplied (must remain unknown) | correct | correct |
| ownerCount | 2 | correct | missing |
| firstRegistrationDate | 2007-12-05 | correct | missing |
| lastInspectionDate | 2026-04-21 | correct | missing |
| nextInspectionDate | 2027-06-30 | correct | missing |
| towBar | true | correct | missing |
| equipment[1] | ABS | correct | missing |
| equipment[2] | Sidoairbags | correct | missing |
| equipment[3] | Antispinn | correct | missing |
| equipment[4] | ESC | correct | missing |
| equipment[5] | Takrails | correct | missing |
| equipment[6] | Servostyrning | correct | missing |
| equipment[7] | Startspärr | correct | missing |
| equipment[8] | Airbag fram | correct | missing |
| equipment[9] | Uppvärmda säten, fram | correct | missing |
| equipment[10] | Dragkrok, fast | correct | missing |
| equipment[11] | Centrallås | correct | missing |
| equipment[12] | 12V-uttag | correct | missing |
| equipment[13] | Klimatanläggning | correct | missing |
| equipment[14] | AUX-ingång | correct | missing |
| equipment[15] | CD-spelare | correct | missing |
| details.title | Skoda Roomster | correct | missing |
| details.subtitle | Praktik 1.2 12v Manuell | correct | missing |
| details.description | Praktik 1.2, Drag, AC, Vinterdäck på ALU fälg ingår, 2 ägare, Servad i egen verkstad varje år | correct | missing |
| details.listingId | 26434732 | correct | correct |
| details.seats | 2 | correct | missing |
| details.doors | 4 | correct | missing |
| details.luggageLitres | Not supplied (must remain unknown) | correct | correct |
| details.weightKilograms | 1240 | correct | missing |
| details.weightLabel | Vikt | correct | missing |
| details.weightCategory | unspecified | correct | missing |
| details.trailerWeightKilograms | Not supplied (must remain unknown) | correct | correct |
| details.trailerWeightLabel | Not supplied (must remain unknown) | correct | correct |
| details.trailerWeightCategory | Not supplied (must remain unknown) | correct | correct |
| details.postalCode | 60209 | correct | missing |
| details.country | Sverige | correct | missing |
| details.feeClass | Transportbil | correct | missing |
| details.saleForm | Begagnad bil till salu | missing | missing |
| details.updatedLocalDateTime | 2026-09-08T20:15:00 | correct | missing |
| details.updatedTimeZone | Not supplied (must remain unknown) | correct | correct |
| details.updatedUtcOffsetMinutes | Not supplied (must remain unknown) | correct | correct |
| details.sellerAnswers | [{"question": "Har bilen några skulder?", "answer": "Nej"}] | correct | missing |

### Additional normalized/generated fields

- Final 1: variant `Praktik 1.2 12v Manuell`, image count `7`, supplementary specifications: Not supplied (must remain unknown).
  Seller summary: ["2 ägare", "Servad i egen verkstad varje år"]. Condition summary: Not supplied (must remain unknown).
- Final 2: variant `None`, image count `None`, supplementary specifications: Not supplied (must remain unknown).
  Seller summary: Not supplied (must remain unknown). Condition summary: Not supplied (must remain unknown).

Equipment order/count and source flags are checked separately from membership:
- Final 1: 15 entries; exact sequence match: True; sourcePageObserved: False; status: partial; {"correct": 61, "missing": 1, "incorrect": 0}.
- Final 2: 0 entries; exact sequence match: False; sourcePageObserved: False; status: partial; {"correct": 13, "missing": 49, "incorrect": 0}.
