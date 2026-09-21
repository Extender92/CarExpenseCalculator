import { expect, test, type APIRequestContext } from "@playwright/test";
import { completeListingAnalysisResponse } from "../src/test/listing-analysis";
import { removePresentationListings } from "./listing-presentation-fixtures";
import { closeEditor } from "./editor-helpers";

function listing(pageId: string, registration?: string) {
  const analysis = structuredClone(completeListingAnalysisResponse);
  const url = `https://cars.example/item/issue95-${pageId}`;
  const provenance = { origin: "listing", extractionMethod: "html", verification: "unverified", sourceUrl: url } as const;
  for (const field of Object.values(analysis.listing)) if (field && "provenance" in field) field.provenance = provenance;
  return { submittedUrl: url, analyzedAtUtc: analysis.analyzedAtUtc, requestedModel: analysis.requestedModel,
    promptVersion: 4, schemaVersion: 3, sources: [url], draft: { ...analysis.listing,
      registrationNumber: registration ? { value: registration, provenance } : null,
      fuelTypes: { values: ["petrol"], provenance }, ownerCount: { value: 0, provenance }, annualVehicleTaxSek: { value: 0, provenance },
      energyConsumptions: { values: [{ label: "NEDC", unit: "litre", consumptionPer100Kilometres: 8.5 }], provenance },
      details: { description: { value: "Årlig översyn.\n\nOriginalbeskrivning som bevaras i utkastet.", provenance } } } };
}
async function clearDraft(request: APIRequestContext, id: string) {
  const current = await request.get(`/api/listing-review-drafts/${id}`);
  if (current.status() === 404) return;
  expect(current.ok()).toBe(true);
  const { revision } = await current.json();
  expect((await request.delete(`/api/listing-review-drafts/${id}?expectedRevision=${revision}`)).status()).toBe(204);
}

test("registration-free drafts survive reload, stay outside comparison and adopt explicitly", async ({ page, request }) => {
  const drafts: string[] = [], vehicles: string[] = [];
  try {
    for (const name of ["first", "second"]) {
      const response = await request.post("/api/listing-review-drafts", { data: { input: listing(`${name}-${Date.now()}`) } });
      expect(response.status(), await response.text()).toBe(201); drafts.push((await response.json()).id);
    }
    const before = await (await request.get("/api/vehicle-cost-inputs")).json();
    await page.goto(`/analyze-urls?reviewDraftId=${drafts[0]}`);
    const dialog = page.getByRole("dialog", { name: /Redigera bil/ });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel("Registreringsnummer", { exact: true })).toHaveValue("");
    await expect(dialog.getByText("Saknas för att lägga till i jämförelsen", { exact: true })).toBeVisible();
    await dialog.getByLabel("Märke", { exact: true }).fill("Ändrat utkast");
    await dialog.getByRole("button", { name: "Spara utkast", exact: true }).click();
    await expect.poll(async () => (await (await request.get(`/api/listing-review-drafts/${drafts[0]}`)).json()).revision).toBe(2);
    await page.reload();
    await expect(dialog.getByLabel("Märke", { exact: true })).toHaveValue("Ändrat utkast");
    await expect(dialog.getByLabel("Märke", { exact: true }).locator("..")).toContainText("Obekräftad");
    expect((await (await request.get("/api/vehicle-cost-inputs")).json()).length).toBe(before.length);
    await dialog.getByLabel("Registreringsnummer", { exact: true }).fill("TWF101");
    const adopted = page.waitForResponse(response => response.url().endsWith(`/listing-review-drafts/${drafts[0]}/adopt`) && response.request().method() === "POST");
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    const response = await adopted; expect(response.ok(), await response.text()).toBe(true);
    const saved = await response.json(); vehicles.push(saved.vehicleId);
    await expect(page.getByText("Annons: sparad · Kostnader: sparade · Jämförelsefakta: sparade", { exact: true })).toBeVisible();
    await dialog.getByRole("tab", { name: "Kostnader", exact: true }).click();
    await expect(dialog.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue("20000");
    expect(saved.listing.registrationNumber.provenance.verification).toBe("unverified");
    expect(saved.listing.details.description.value).toContain("Originalbeskrivning");
    expect((await request.get(`/api/listing-review-drafts/${drafts[0]}`)).status()).toBe(404);
    expect((await request.get(`/api/listing-review-drafts/${drafts[1]}`)).status()).toBe(200);
  } finally { for (const id of drafts) await clearDraft(request, id); await removePresentationListings(request, vehicles); }
});

test("reuse populates selected unsaved costs and facts once, then saves resources with current revisions", async ({ page, request }) => {
  const owned: string[] = [];
  try {
    const created = await request.post("/api/saved-listings", { data: { registrationNumber: "TWF102", listing: listing(`reuse-${Date.now()}`, "TWF102") } });
    expect(created.status(), await created.text()).toBe(201);
    const saved = await created.json(); owned.push(saved.vehicleId);
    await page.goto(`/search?vehicleId=${saved.vehicleId}&tab=cost`);
    const dialog = page.getByRole("dialog", { name: /Redigera bil/ });
    await dialog.getByRole("button", { name: "Använd tillgängliga annonsuppgifter", exact: true }).click();
    const preview = page.getByRole("dialog", { name: "Välj annonsuppgifter att återanvända" });
    await expect(preview).toBeVisible();
    await expect(preview.getByRole("checkbox", { name: /Mätarställning \(mil\)/ })).toBeVisible();
    await expect(preview.getByText("16710", { exact: true })).toBeVisible();
    await expect(preview.getByText("Bensin", { exact: true })).toBeVisible();
    await preview.getByRole("checkbox", { name: /Inköpspris:/ }).check();
    await preview.getByRole("checkbox", { name: /Årlig fordonsskatt:/ }).check();
    await preview.getByRole("checkbox", { name: /Bensin:.*NEDC/ }).check();
    await preview.getByRole("checkbox", { name: /Jämförelsefakta: Ägarantal/ }).check();
    await preview.getByRole("button", { name: "Tillämpa valda förslag" }).click();
    const unchanged = await (await request.get(`/api/vehicle-cost-inputs/${saved.vehicleId}`)).json();
    expect(unchanged.revision).toBe(1); expect(unchanged.input).toBeNull();
    await dialog.getByRole("tab", { name: "Biluppgifter", exact: true }).click();
    await dialog.getByLabel("Märke", { exact: true }).fill("Granskad testbil");
    await dialog.getByRole("button", { name: "Stäng", exact: true }).first().click();
    await dialog.getByRole("button", { name: "Spara och stäng" }).click();
    await expect(dialog).toHaveCount(0);
    const cost = await (await request.get(`/api/vehicle-cost-inputs/${saved.vehicleId}`)).json();
    const facts = await (await request.get(`/api/vehicle-facts/${saved.vehicleId}`)).json();
    expect(cost.revision).toBe(4);
    expect(cost.input.priceSek).toBe(20000); expect(cost.input.priceSource.listingVersion).toBe(1);
    expect(cost.input.energySources).toHaveLength(1);
    expect(cost.input.energySources[0].consumptionLabel).toBe("NEDC");
    expect(cost.input.tax.items[0].amountSek.single).toBe(0);
    expect(facts.input.facts.ownerCount.observations[0].value).toBe(0);
    expect(facts.input.facts.ownerCount.observations[0].evidence.verification).toBe("unverified");
    expect(facts.costConfirmedAt).toBeNull();
    expect(facts.currentListingVersion).toBe(2);
    // Reusing selected facts preserves their source version without claiming that
    // the entire listing has been reviewed.
    expect(facts.factsReviewedListingVersion).toBeNull();
    expect(facts.input.facts.ownerCount.observations[0].sourceListingVersion).toBe(1);
  } finally { await removePresentationListings(request, owned); }
});

test("mobile editor preserves dirty work across Escape, navigation cancellation and discard", async ({ page, request }) => {
  const owned: string[] = [];
  try {
    const created = await request.post("/api/saved-listings", { data: { registrationNumber: "TWF103", listing: listing(`mobile-${Date.now()}`, "TWF103") } });
    expect(created.status(), await created.text()).toBe(201);
    const saved = await created.json(); owned.push(saved.vehicleId);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/search");
    await page.getByRole("table", { name: "Huvudjämförelse", exact: true }).getByRole("button", { name: "TWF103", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: /Redigera bil/ });
    await dialog.getByRole("tab", { name: "Biluppgifter", exact: true }).click();
    await dialog.getByLabel("Märke", { exact: true }).fill("Mobil ändring");
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
    const dimensions = await dialog.boundingBox(); expect(dimensions!.width).toBeLessThanOrEqual(390);
    for (const key of ["Tab", "Tab", "Shift+Tab", "Shift+Tab"]) {
      await page.keyboard.press(key);
      expect(await dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);
    }
    const saveBounds = await dialog.getByRole("button", { name: "Spara bil", exact: true }).boundingBox();
    expect(saveBounds!.y + saveBounds!.height).toBeLessThanOrEqual(844);
    await page.keyboard.press("Escape");
    await dialog.getByRole("button", { name: "Fortsätt redigera" }).click();
    await expect(dialog.getByLabel("Märke", { exact: true })).toHaveValue("Mobil ändring");
    await page.goBack();
    await expect(dialog.getByRole("button", { name: "Spara och stäng" })).toBeVisible();
    await dialog.getByRole("button", { name: "Kasta ändringar" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(/\/search$/);
    expect((await (await request.get(`/api/saved-listings/${saved.vehicleId}`)).json()).listing.make.value).toBe("Volvo");
    await page.goForward();
    await expect(page).toHaveURL(new RegExp(`vehicleId=${saved.vehicleId}`));
    await expect(dialog).toBeVisible();
    await dialog.getByRole("tab", { name: "Biluppgifter", exact: true }).click();
    await expect(dialog.getByLabel("Märke", { exact: true })).toHaveValue("Volvo");
  } finally { await removePresentationListings(request, owned); }
});

test("saved car listing conflicts require reviewing current values before replacing them", async ({ page, request }) => {
  const source = listing("car-conflict", "TWF106");
  const created = await request.post("/api/saved-listings", { data: { registrationNumber: "TWF106", listing: source } });
  expect(created.status()).toBe(201);
  const car = await created.json();
  try {
    await page.goto(`/search?vehicleId=${car.vehicleId}&tab=listing`);
    const dialog = page.locator("dialog[open]");
    await dialog.getByLabel("Antal ägare", { exact: true }).fill("2");
    const remote = structuredClone(source);
    remote.draft.ownerCount.value = 3;
    const replaced = await request.put(`/api/saved-listings/${car.vehicleId}`, { data: { expectedRevision: car.revision, listing: remote } });
    expect(replaced.status()).toBe(200);
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    await dialog.getByRole("button", { name: "Jämför med senaste annons", exact: true }).click();
    await expect(dialog.getByText("Sparat: 3", { exact: true })).toBeVisible();
    await expect(dialog.getByText("Ditt underlag: 2", { exact: true })).toBeVisible();
    expect((await (await request.get(`/api/saved-listings/${car.vehicleId}`)).json()).listing.ownerCount.value).toBe(3);
    await dialog.getByRole("button", { name: "Behåll mina annonsändringar efter granskning", exact: true }).click();
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    await expect.poll(async () => (await (await request.get(`/api/saved-listings/${car.vehicleId}`)).json()).listing.ownerCount.value).toBe(2);
    const saved = await (await request.get(`/api/saved-listings/${car.vehicleId}`)).json();
    expect(saved.listing.ownerCount.provenance.verification).toBe("unverified");
    await dialog.getByLabel("Antal ägare", { exact: true }).fill("4");
    const removed = await request.delete(`/api/vehicle-cost-inputs/${car.vehicleId}?expectedRevision=${saved.revision}`);
    expect(removed.status()).toBe(204);
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    await expect(dialog.getByText("Den sparade annonsen finns inte längre.", { exact: true })).toBeVisible();
    await expect(dialog.getByLabel("Antal ägare", { exact: true })).toHaveValue("4");
    expect((await request.get(`/api/saved-listings/${car.vehicleId}`)).status()).toBe(404);
  } finally { await removePresentationListings(request, [car.vehicleId]); }
});

test("two hybrids retain individual electric shares in saved costs and a frozen multipage report", async ({ page, request }, testInfo) => {
  const owned: string[] = [];
  const before = await (await request.get("/api/comparisons/baseline")).json();
  async function shared(route: string, input: unknown) {
    const current = await request.get(`/api/${route}`);
    const expectedRevision = current.status() === 404 ? 0 : (await current.json()).revision;
    const response = await request.put(`/api/${route}`, { data: { expectedRevision, input } });
    expect(response.status(), await response.text()).toBe(200);
  }
  const zero = () => ({ isIncluded: false, items: [] });
  try {
    await shared("household-profile", { periodMonths: 12, startMonth: { year: 2026, month: 1 },
      annualDistanceKilometres: 10000, purchaseCashSek: 100000, activeSensitivityMode: "baseline",
      electricDrivingSharePercent: { single: 50 }, homeChargingSharePercent: { single: 100 },
      homeChargingPricePerKilowattHourSek: { single: 1 },
      energyPrices: [{ fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } }] });
    await shared("rule-profile", {});
    for (const [registration, share] of [["TWF104", 20], ["TWF105", 80]] as const) {
      const source = listing(`hybrid-${registration}`, registration);
      source.draft.details.description.value = Array.from({ length: 36 }, (_, i) => `Stycke ${i + 1}: Årlig översyn och skötsel. Originalunderlag med svenska tecken för ${registration}.`).join("\n\n");
      const created = await request.post("/api/saved-listings", { data: { registrationNumber: registration, listing: source } });
      expect(created.status(), await created.text()).toBe(201);
      const car = await created.json(); owned.push(car.vehicleId);
      const cost = await request.put(`/api/vehicle-cost-inputs/${car.vehicleId}`, { data: { expectedRevision: car.revision,
        cost: { input: { candidateKey: registration, acquisitionType: "purchase", priceSek: 0,
          residual: { mode: "fixedAmount", value: { single: 0 }, periodMonths: 12 },
          tax: zero(), insurance: zero(), service: zero(), repairs: zero(), customCosts: zero(),
          additionalRepairAllowancePerMonthSek: { single: 0 },
          energySources: [
            { key: "petrol", fuel: "petrol", unit: "litre", consumptionBasis: "drivingMode", consumptionPer100Kilometres: { single: 5 } },
            { key: "electric", fuel: "electricity", unit: "kilowattHour", consumptionBasis: "drivingMode", electricityBasis: "metered", consumptionPer100Kilometres: { single: 20 } },
          ] } } } });
      expect(cost.status(), await cost.text()).toBe(200);
      await page.goto(`/search?vehicleId=${car.vehicleId}&tab=cost`);
      const dialog = page.locator("dialog[open]");
      await dialog.locator("summary").filter({ hasText: /^Energi$/ }).click();
      await dialog.getByLabel("Elandel för denna bil", { exact: true }).selectOption("override");
      await dialog.getByLabel("Typ av värde för Bilens elandel av körsträckan (%)", { exact: true }).selectOption("single");
      await dialog.getByLabel("Bilens elandel av körsträckan (%)", { exact: true }).fill(String(share));
      await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
      await expect.poll(async () => (await (await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)).json()).input.electricDrivingShare)
        .toEqual({ mode: "override", value: { single: share, favorable: null, baseline: null, cautious: null } });
      await closeEditor(page);
    }
    await page.reload();
    const table = page.getByRole("table", { name: "Huvudjämförelse", exact: true });
    await expect(table.getByRole("row").filter({ hasText: "TWF104" })).toContainText(/8\s400,00/);
    await expect(table.getByRole("row").filter({ hasText: "TWF105" })).toContainText(/3\s600,00/);
    const report = page.getByRole("button", { name: "Öppna rapport", exact: true });
    await expect(report).toBeEnabled();
    await page.getByLabel("Rapportinnehåll").selectOption("full");
    await report.click();
    await expect(page).toHaveURL(/\/search\/report$/);
    const article = page.getByRole("article");
    await expect(article).toContainText("Elandel: 20 % · Bilens eget val");
    await expect(article).toContainText("Elandel: 80 % · Bilens eget val");
    await expect(article).toContainText("Stycke 36:");
    const captured = await article.innerText();
    const calls: string[] = [];
    page.on("request", r => { if (new URL(r.url()).pathname.startsWith("/api/")) calls.push(r.url()); });
    const current = await (await request.get(`/api/vehicle-cost-inputs/${owned[0]}`)).json();
    const changed = await request.put(`/api/vehicle-cost-inputs/${owned[0]}`, { data: { expectedRevision: current.revision,
      cost: { input: { ...current.input, electricDrivingShare: { mode: "inherit" } } } } });
    expect(changed.status()).toBe(200);
    await page.emulateMedia({ media: "print" });
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).backgroundColor)).toBe("rgb(255, 255, 255)");
    const pdf = await page.pdf({ path: testInfo.outputPath("two-hybrid-report.pdf"), preferCSSPageSize: true, printBackground: true });
    expect(pdf.byteLength).toBeGreaterThan(25000);
    expect(await article.innerText()).toBe(captured);
    expect(calls).toEqual([]);
  } finally {
    await removePresentationListings(request, owned);
    await shared("household-profile", before.profile ?? {});
    await shared("rule-profile", before.rules ?? {});
  }
});
