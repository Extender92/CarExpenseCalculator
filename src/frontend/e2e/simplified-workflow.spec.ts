import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { removePresentationListings } from "./listing-presentation-fixtures";

test("three primary clicks save two complete listings with costs and unconfirmed facts", async ({ page, request }) => {
  const owned: string[] = [];
  const registrations = ["TSF101", "TSF102"];
  try {
    await page.addInitScript(() => localStorage.setItem("car-expense:first-start-dismissed", "yes"));
    await page.goto("/");
    await page.getByRole("main").getByRole("link", { name: "Lägg till bil", exact: true }).click();
    await page.getByLabel("URL:er").fill(registrations.map(reg => `https://cars.example/item/complete-workflow-${reg}`).join("\n"));
    await page.getByRole("button", { name: "Hämta annonser", exact: true }).click();
    const save = page.getByRole("button", { name: "Spara och jämför (2 bilar, 0 utkast)", exact: true });
    await expect(save).toBeEnabled();
    await expect(page.getByText("Tillgängliga annonsuppgifter har förifyllts. Övriga kostnader är okända.")).toHaveCount(2);
    await save.click();
    await expect(page).toHaveURL(/\/search$/);
    for (const registration of registrations) {
      const list = await (await request.get("/api/vehicle-cost-inputs")).json();
      const row = list.find((value: { registrationNumber: string }) => value.registrationNumber === registration);
      expect(row).toBeTruthy(); owned.push(row.vehicleId);
      const cost = await (await request.get(`/api/vehicle-cost-inputs/${row.vehicleId}`)).json();
      expect(cost.input.priceSek).toBe(20000);
      expect(cost.input.tax.items[0].amountSek.single).toBe(2400);
      expect(cost.input.energySources[0].fuel).toBe("petrol");
      expect(cost.input.energySources[0].consumptionPer100Kilometres.single).toBe(8);
      expect(cost.input.priceSource.listingVersion).toBe(1);
      const facts = await (await request.get(`/api/vehicle-facts/${row.vehicleId}`)).json();
      expect(facts.input.facts.towBar.observations[0].value).toBe(false);
      expect(facts.input.facts.towBar.observations[0].evidence.verification).toBe("unverified");
      expect(facts.costConfirmedAt).toBeNull();
      await expect(page.getByRole("table", { name: "Huvudjämförelse" }).getByText(registration, { exact: true })).toBeVisible();
    }
  } finally {
    const list = await (await request.get("/api/vehicle-cost-inputs")).json();
    for (const row of list) if (registrations.includes(row.registrationNumber) && !owned.includes(row.vehicleId)) owned.push(row.vehicleId);
    await removePresentationListings(request, owned);
  }
});

async function start(page: Page, urls: string[]) {
  page.on("dialog", dialog => void dialog.accept());
  await page.addInitScript(() => localStorage.setItem("car-expense:first-start-dismissed", "yes"));
  await page.goto("/analyze-urls");
  await page.getByLabel("URL:er").fill(urls.join("\n"));
  await page.getByRole("button", { name: "Hämta annonser", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Spara och jämför/ })).toBeEnabled();
}
async function cleanup(request: APIRequestContext, registrations: string[], urls: string[]) {
  const inventory = await (await request.get("/api/vehicle-cost-inputs")).json();
  await removePresentationListings(request, inventory.filter((row: { registrationNumber: string }) => registrations.includes(row.registrationNumber)).map((row: { vehicleId: string }) => row.vehicleId));
  const drafts = await (await request.get("/api/listing-review-drafts")).json();
  for (const draft of drafts) if (urls.includes(draft.listingReference) || urls.includes(draft.input?.submittedUrl)) {
    const current = await (await request.get(`/api/listing-review-drafts/${draft.id}`)).json();
    expect((await request.delete(`/api/listing-review-drafts/${draft.id}?expectedRevision=${current.revision}`)).status()).toBe(204);
  }
}

test("mixed batch checkpoints a cost failure and saves registration-free drafts only once", async ({ page, request }) => {
  const urls = ["https://cars.example/item/complete-workflow-TSF103", "https://cars.example/item/partial-simplified-draft"];
  let listingWrites = 0, draftWrites = 0, costWrites = 0;
  page.on("request", r => {
    if (r.method() === "POST" && r.url().endsWith("/api/saved-listings")) listingWrites++;
    if (["POST", "PUT"].includes(r.method()) && r.url().includes("/api/listing-review-drafts")) draftWrites++;
  });
  await page.route("**/api/vehicle-cost-inputs/*", async route => {
    if (route.request().method() !== "PUT") return route.continue();
    costWrites++;
    if (costWrites === 1) return route.fulfill({ status: 503, contentType: "application/problem+json", body: JSON.stringify({ code: "testFailure" }) });
    await route.continue();
  });
  try {
    await start(page, urls);
    const save = page.getByRole("button", { name: "Spara och jämför (1 bil, 1 utkast)", exact: true });
    await save.click();
    await expect(page.getByText(/Det valda arbetet är inte helt sparat/)).toBeVisible();
    expect(listingWrites).toBe(1); expect(draftWrites).toBe(1); expect(costWrites).toBe(1);
    const inventory = await (await request.get("/api/vehicle-cost-inputs")).json();
    expect(inventory.filter((row: { registrationNumber: string }) => row.registrationNumber === "TSF103")).toHaveLength(1);
    await save.click();
    await expect(page).toHaveURL(/\/search$/);
    expect(listingWrites).toBe(1); expect(draftWrites).toBe(1); expect(costWrites).toBe(2);
  } finally { await cleanup(request, ["TSF103"], urls); }
});

test("mobile cards preserve a cleared price through listing edits and keyboard navigation", async ({ page, request }) => {
  const urls = ["https://cars.example/item/complete-workflow-TSF104"];
  await page.setViewportSize({ width: 390, height: 844 });
  try {
    await start(page, urls);
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const card = page.getByTestId(/listing-card/);
    await card.getByRole("button", { name: "Redigera bil", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: /Redigera bil/ });
    await dialog.getByRole("tab", { name: "Kostnader", exact: true }).click();
    const section = dialog.getByRole("tabpanel", { name: "Kostnader", exact: true }).locator("summary").filter({ hasText: /^Anskaffning och restvärde$/ });
    if (await section.count() && !await section.evaluate(el => el.parentElement?.hasAttribute("open"))) await section.click();
    await dialog.getByLabel("Inköpspris (kr)", { exact: true }).fill("0");
    await dialog.getByRole("tab", { name: "Biluppgifter", exact: true }).click();
    await dialog.getByLabel("Märke", { exact: true }).fill("Manuellt ändrad");
    await expect(page.getByText("Förbereder tillgängliga kostnadsuppgifter…")).not.toBeVisible();
    await dialog.getByRole("tab", { name: "Kostnader", exact: true }).click();
    await expect(dialog.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue("0");
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    await expect.poll(async () => {
      const list = await (await request.get("/api/vehicle-cost-inputs")).json();
      const row = list.find((v: { registrationNumber: string }) => v.registrationNumber === "TSF104");
      return row ? (await (await request.get(`/api/vehicle-cost-inputs/${row.vehicleId}`)).json()).input?.priceSek : undefined;
    }).toBe(0);
    await dialog.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(card.getByRole("button", { name: "Redigera bil", exact: true })).toBeFocused();
    await page.getByRole("button", { name: /^Spara och jämför/ }).click();
    await expect(page).toHaveURL(/\/search$/);
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  } finally { await cleanup(request, ["TSF104"], urls); }
});

test("Back and Forward protect unsaved card edits and can retain them for continued editing", async ({ page, request }) => {
  const urls = ["https://cars.example/item/complete-workflow-TSF105"];
  try {
    await page.addInitScript(() => localStorage.setItem("car-expense:first-start-dismissed", "yes"));
    await page.goto("/");
    await page.getByRole("main").getByRole("link", { name: "Lägg till bil", exact: true }).click();
    await page.getByLabel("URL:er").fill(urls[0]);
    await page.getByRole("button", { name: "Hämta annonser", exact: true }).click();
    await expect(page.getByRole("button", { name: /^Spara och jämför/ })).toBeEnabled();
    await page.getByLabel("Komplettera registreringsnummer").fill("TSF106");
    await page.goBack();
    const question = page.getByRole("dialog", { name: "Osparade bilar" });
    await expect(question).toBeVisible();
    await question.getByRole("button", { name: "Fortsätt redigera", exact: true }).click();
    await expect(page).toHaveURL(/analyze-urls/);
    await expect(page.getByLabel("Komplettera registreringsnummer")).toHaveValue("TSF106");
    await page.goBack();
    await question.getByRole("button", { name: "Kasta ändringar", exact: true }).click();
    await expect(page).toHaveURL(/\/$/);
    await page.goForward();
    await expect(page).toHaveURL(/analyze-urls/);
    await expect(page.getByTestId(/listing-card/)).toHaveCount(0);
  } finally { await cleanup(request, ["TSF105", "TSF106"], urls); }
});

test("duplicate registrations in a batch require review without replacing the first saved car", async ({ page, request }) => {
  const urls = ["https://cars.example/item/complete-workflow-TSF107-first", "https://cars.example/item/complete-workflow-TSF107-second"];
  try {
    await start(page, urls);
    await page.getByRole("button", { name: /^Spara och jämför/ }).click();
    await expect(page.getByRole("alertdialog", { name: "Bilen TSF107 finns redan" })).toBeVisible();
    const list = await (await request.get("/api/vehicle-cost-inputs")).json();
    const cars = list.filter((row: { registrationNumber: string }) => row.registrationNumber === "TSF107");
    expect(cars).toHaveLength(1);
    const saved = await (await request.get(`/api/saved-listings/${cars[0].vehicleId}`)).json();
    expect(saved.listingVersion).toBe(1);
    expect(saved.normalizedUrl).toBe(urls[0]);
    expect((await (await request.get(`/api/vehicle-cost-inputs/${cars[0].vehicleId}`)).json()).input.priceSek).toBe(20000);
    await expect(page).toHaveURL(/analyze-urls/);
  } finally { await cleanup(request, ["TSF107"], urls); }
});

test("later cost edits survive an in-flight batch write and prevent automatic navigation", async ({ page, request }) => {
  const urls = ["https://cars.example/item/complete-workflow-TSF108"];
  let release: () => void = () => {};
  const pending = new Promise<void>(resolve => { release = resolve; });
  let costWrites = 0, factsWrites = 0;
  page.on("request", r => { if (r.method() === "PUT" && r.url().includes("/api/vehicle-facts/")) factsWrites++; });
  await page.route("**/api/vehicle-cost-inputs/*", async route => {
    if (route.request().method() === "PUT" && ++costWrites === 1) await pending;
    await route.continue();
  });
  try {
    await start(page, urls);
    await page.getByRole("button", { name: /^Spara och jämför/ }).click();
    await expect.poll(() => costWrites).toBe(1);
    const card = page.getByTestId(/listing-card/);
    await card.getByRole("button", { name: "Redigera bil", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: /Redigera bil/ });
    await dialog.getByRole("tab", { name: "Kostnader", exact: true }).click();
    const section = dialog.getByRole("tabpanel", { name: "Kostnader", exact: true }).locator("summary").filter({ hasText: /^Anskaffning och restvärde$/ });
    if (await section.count() && !await section.evaluate(el => el.parentElement?.hasAttribute("open"))) await section.click();
    await dialog.getByLabel("Inköpspris (kr)", { exact: true }).fill("0");
    release();
    await expect.poll(() => factsWrites).toBe(1);
    await expect(dialog.getByRole("button", { name: "Spara bil", exact: true })).toBeEnabled();
    await expect(page).toHaveURL(/analyze-urls/);
    await expect(dialog.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue("0");
    const secondSave = page.waitForResponse(response => response.url().includes("/api/vehicle-cost-inputs/") && response.request().method() === "PUT");
    await dialog.getByRole("button", { name: "Spara bil", exact: true }).click();
    expect((await secondSave).status()).toBe(200);
    await expect(dialog.getByRole("button", { name: "Spara bil", exact: true })).toBeDisabled();
    expect(costWrites).toBe(2); expect(factsWrites).toBe(1);
    const list = await (await request.get("/api/vehicle-cost-inputs")).json();
    const row = list.find((v: { registrationNumber: string }) => v.registrationNumber === "TSF108");
    const saved = await (await request.get(`/api/vehicle-cost-inputs/${row.vehicleId}`)).json();
    expect(saved.input.priceSek).toBe(0); expect(saved.input.priceSource).toBeNull();
  } finally { release(); await page.unrouteAll({ behavior: "wait" }); await cleanup(request, ["TSF108"], urls); }
});
