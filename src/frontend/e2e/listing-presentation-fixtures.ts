import {expect, type APIRequestContext} from "@playwright/test";
import {completeListingAnalysisResponse} from "../src/test/listing-analysis";
import type {components} from "../src/api/schema";

export const presentationDescription = Array.from({length: 18}, (_, i) =>
  `Stycke ${i + 1}: Årlig översyn, vinterdäck och originaltext med dealer och private.`).join("\n\n");
export const presentationEquipment = Array.from({length: 30}, (_, i) => `Utrustning ${i + 1}: Sätesvärme och säkerhet`);
export const presentationSource = `https://cars.example/item/${"lang-annonsadress-".repeat(35)}slut`;

// Only API-created fictional cars in the disposable stack. Register ownership
// immediately so a later assertion cannot leave a partially created fixture.
export async function seedPresentationListings(request: APIRequestContext, owned: string[]) {
  for (const [i, sellerType] of (["dealer", "private"] as const).entries()) {
    const analysis = structuredClone(completeListingAnalysisResponse);
    const provenance = {origin: "listing", extractionMethod: "html", verification: "unverified", sourceUrl: presentationSource} as const;
    for (const field of Object.values(analysis.listing)) {
      if (field && "provenance" in field) field.provenance.sourceUrl = presentationSource;
    }
    const value = <T,>(item: T) => ({value: item, provenance});
    const draft = {...analysis.listing,
      registrationNumber: value(`LPR10${i}`), sellerType: value(sellerType),
      equipment: {values: presentationEquipment, provenance},
      details: {description: value(presentationDescription), title: value(`Fiktiv rapportbil ${i + 1}`),
        specifications: [{value: {name: "Originalbeteckning", value: "dealer"}, provenance}],
        sellerAnswers: [{value: {question: "Har bilen några skulder?", answer: "Nej"}, provenance}]},
    };
    if (i === 1) { draft.priceSek = null; draft.vin = null; draft.ownerCount = null; }
    const data: components["schemas"]["CreateSavedListingRequest"] = {registrationNumber: `LPR10${i}`, listing: {
      submittedUrl: presentationSource, analyzedAtUtc: analysis.analyzedAtUtc,
      requestedModel: analysis.requestedModel, promptVersion: 4, schemaVersion: 3,
      sources: [presentationSource], draft,
    }};
    const created = await request.post("/api/saved-listings", {data});
    expect(created.status(), await created.text()).toBe(201);
    const saved = await created.json(); owned.push(saved.vehicleId);
    expect(saved.status).toBe(i === 0 ? "complete" : "partial");
    expect(saved.listing.sellerType.provenance).toEqual(provenance);
  }
}

export async function removePresentationListings(request: APIRequestContext, owned: string[]) {
  for (const id of owned) {
    const current = await request.get(`/api/vehicle-cost-inputs/${id}`);
    if (current.status() === 404) continue;
    expect(current.status(), await current.text()).toBe(200);
    const revision = (await current.json()).revision;
    expect((await request.delete(`/api/vehicle-cost-inputs/${id}?expectedRevision=${revision}`)).status()).toBe(204);
  }
}
