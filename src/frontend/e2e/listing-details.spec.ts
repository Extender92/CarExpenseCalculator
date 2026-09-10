import {expect, test} from "@playwright/test";
import {readFile} from "node:fs/promises";

test("complete reference listings survive review, draft adoption, comparison and a frozen PDF", async ({page,request}, testInfo) => {
  const ids: string[] = [];
  const slotBefore = await (await request.get("/api/vehicle-draft")).json();
  expect(slotBefore.input).toBeNull();
  const refs = await Promise.all(["audi-a4","skoda-roomster"].map(async name => ({name,
    ...(JSON.parse(await readFile(new URL(`../../../tests/fixtures/listings/${name}.json`,import.meta.url),"utf8")))})));
  try {
    await page.goto("/analyze-urls");
    await page.getByLabel("URL:er").fill(refs.map(x=>`https://cars.example/item/${x.name}`).join("\n"));
    await page.getByRole("button",{name:"Analysera URL:er"}).click();
    const cards = page.locator('[data-testid^="listing-card-"]');
    await expect(cards).toHaveCount(2);
    for (const [i,reference] of refs.entries()) {
      const card = cards.nth(i);
      await expect(card.getByText("Delvis extraktion")).toBeVisible();
      await card.getByRole("button",{name:"Granska och komplettera alla uppgifter"}).click();
      await expect(card.getByLabel("Hela beskrivningen")).toHaveValue(reference.draft.details.description);
      await expect(card.getByLabel("Sittplatser",{exact:true})).toHaveValue(String(reference.draft.details.seats));
      await expect(card.getByLabel("Registreringsnummer",{exact:true})).toHaveValue("");
      await expect(card.getByText(/Metadata om öppnad sida saknas/)).toBeVisible();
      if (i===1) await expect(card.getByLabel("Svar 1",{exact:true})).toHaveValue("Nej");
      // Fictional identities exist only in this disposable acceptance database.
      await card.getByLabel("Registreringsnummer",{exact:true}).fill(`ULA10${i}`);
      if (i===0) {
        const save = page.waitForResponse(r=>r.url().endsWith("/api/saved-listings") && r.request().method()==="POST");
        await card.getByRole("button",{name:"Spara bil",exact:true}).click();
        const result=await save; expect(result.status(),await result.text()).toBe(201);
        const body=await result.json(); ids.push(body.vehicleId);
      } else {
        const save=page.waitForResponse(r=>r.url().endsWith("/api/vehicle-draft") && r.request().method()==="PUT");
        await card.getByRole("button",{name:"Spara gemensamt annonsutkast"}).click();
        const saved=await save; expect(saved.status(),await saved.text()).toBe(200);
        const slot=await saved.json();
        expect(slot.input.listing.draft.details.sellerAnswers[0].value.answer).toBe("Nej");
        const adopted=await request.post("/api/vehicle-draft/adopt",{data:{expectedRevision:slot.revision}});
        expect(adopted.status(),await adopted.text()).toBe(200); ids.push((await adopted.json()).vehicleId);
      }
      const persisted=await (await request.get(`/api/saved-listings/${ids[i]}`)).json();
      expect(persisted.listing.details.description.value).toBe(reference.draft.details.description);
      expect(persisted.listing.equipment.values).toEqual(reference.draft.equipment);
      expect(persisted.listing.details.seats.provenance.verification).toBe("unverified");
      expect(persisted.sourcePageObserved).toBe(false);
    }
    // A real snapshot response carries both complete listings exactly once.
    const comparison=page.waitForResponse(r=>r.url().endsWith("/api/comparisons/preview-all") && r.status()===200);
    await page.goto("/search");
    const result=await (await comparison).json();
    expect(result.transportVersion).toBe(2); expect(result.listings).toHaveLength(2);
    for (const mode of ["favorable","baseline","cautious"]) expect(result.views[mode].candidates).toHaveLength(2);
    await expect(page.getByRole("button",{name:"Öppna rapport",exact:true})).toBeEnabled();
    await page.getByRole("button",{name:"Öppna rapport",exact:true}).click();
    await expect(page.getByRole("button",{name:"Skriv ut / Spara som PDF",exact:true})).toBeEnabled();
    const report=page.getByRole("article");
    for (const reference of refs) {
      await expect(report).toContainText(reference.draft.details.title);
      await expect(report).toContainText(reference.draft.vin);
      await expect(report).toContainText(reference.draft.firstRegistrationDate);
    }
    await expect(report).toContainText("Har bilen några skulder?");
    const before=await report.innerText();
    const old=await (await request.get(`/api/saved-listings/${ids[0]}`)).json();
    const replaced=await request.put(`/api/saved-listings/${ids[0]}`,{data:{expectedRevision:old.revision,listing:{
      submittedUrl:old.submittedUrl,analyzedAtUtc:old.analyzedAtUtc,requestedModel:old.requestedModel,
      promptVersion:old.promptVersion,schemaVersion:old.schemaVersion,sources:[],
      draft:{...old.listing,details:{...old.listing.details,description:{...old.listing.details.description,value:"Senare ändrad annons"}}}}}});
    expect(replaced.status(),await replaced.text()).toBe(200);
    let browserRequests=0; page.on("request",()=>browserRequests++);
    const pdf=await page.pdf({preferCSSPageSize:true});
    await testInfo.attach("complete-listings.pdf",{body:pdf,contentType:"application/pdf"});
    expect(await report.innerText()).toBe(before);
    expect(browserRequests).toBe(0);
  } finally {
    for (const id of ids) {
      const current=await request.get(`/api/vehicle-cost-inputs/${id}`);
      if(current.ok()) expect((await request.delete(`/api/vehicle-cost-inputs/${id}?expectedRevision=${(await current.json()).revision}`)).status()).toBe(204);
    }
    const slot=await (await request.get("/api/vehicle-draft")).json();
    if(slot.input?.registrationNumber?.startsWith("ULA10")) await request.delete(`/api/vehicle-draft?expectedRevision=${slot.revision}`);
  }
});
