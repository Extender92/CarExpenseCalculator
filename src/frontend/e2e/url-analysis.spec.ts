import { expect, test, type Request } from "@playwright/test";

test("analyzes independent URLs through the same-origin proxy and keeps review drafts in memory", async ({ page }) => {
  const listingRequests: Request[] = [];
  const externalBrowserRequests: string[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/listing-analyses") listingRequests.push(request);
    if (!["localhost", "127.0.0.1"].includes(url.hostname)) externalBrowserRequests.push(request.url());
  });

  await page.goto("/analyze-urls");
  await expect(page.getByRole("heading", { name: "Analysera URL:er" })).toBeVisible();
  await page.getByLabel("URL:er").fill([
    "https://cars.example/item/complete",
    "https://cars.example/item/unavailable",
    "https://cars.example/item/failure",
  ].join("\n"));
  await page.getByRole("button", { name: "Analysera URL:er" }).click();

  await expect(page.getByText("Komplett extraktion")).toBeVisible();
  await expect(page.getByText("Ingen verifierad extraktion")).toBeVisible();
  await expect(page.getByText("Analysen misslyckades")).toBeVisible();
  await expect.poll(() => listingRequests.length).toBe(3);
  for (const request of listingRequests) {
    expect(new URL(request.url()).origin).toBe(new URL(page.url()).origin);
    expect(request.method()).toBe("POST");
  }
  expect(externalBrowserRequests).toEqual([]);

  const completeCard = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "complete" });
  await expect(completeCard.getByText("20 000 kr")).toBeVisible();
  await expect(completeCard.getByText("16710 mil")).toBeVisible();
  await expect(completeCard.getByText("Nej", { exact: true }).first()).toBeVisible();
  await completeCard.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await expect(completeCard.getByLabel("Ort eller stad")).toHaveValue("Tenhult");
  await expect(completeCard.getByLabel("Län")).toHaveValue("Jönköpings län");
  await completeCard.getByLabel("Län").fill("Östergötlands län");
  await expect(completeCard.getByLabel("Ort eller stad")).toHaveValue("Tenhult");
  await expect(completeCard.getByText("Matchar annonsen")).toBeVisible();
  await expect(completeCard.getByLabel("Utrustning")).toHaveValue("empty");
  await completeCard.getByLabel("Märke").fill("Saab");
  await completeCard.getByLabel("Annonspris").fill("0");
  await expect(completeCard.getByText("0 kr")).toBeVisible();
  await expect(completeCard.getByText(/Användare · Manuell · Bekräftad/).first()).toBeVisible();

  const unavailableCard = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "unavailable" });
  await unavailableCard.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await unavailableCard.getByLabel("Registreringsnummer").fill("ABC123");
  await expect(unavailableCard.getByText(/Användare · Manuell · Bekräftad/)).toBeVisible();

  await page.reload();
  await expect(page.getByText("Inga annonsunderlag är öppna ännu.")).toBeVisible();
});

test("creates, compares, reopens, replaces, and permanently deletes a saved listing", async ({ page }) => {
  await removeSavedListingIfPresent(page, "ABC123");
  await page.goto("/analyze-urls");
  await page.getByLabel("URL:er").fill("https://cars.example/item/complete");
  await page.getByRole("button", { name: "Analysera URL:er" }).click();
  const draft = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "complete" });
  await expect(draft.getByText("Volvo V70 2.4")).toBeVisible();

  const createPromise = page.waitForResponse((response) =>
    response.url().endsWith("/api/saved-listings") && response.request().method() === "POST",
  );
  await draft.getByRole("button", { name: "Spara bil" }).click();
  const created = await createPromise;
  expect(created.status()).toBe(201);
  expectSameOrigin(page, created);
  await expect(draft.getByText("Sparad", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Sparade bilar" })).toBeVisible();

  await page.reload();
  const savedSummary = page.getByText("ABC123", { exact: true }).first().locator("xpath=ancestor::li");
  await expect(savedSummary).toContainText("Volvo V70 2008");
  await savedSummary.getByRole("button", { name: "Öppna", exact: true }).click();
  const opened = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "Volvo V70 2.4" });
  await opened.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await expect(opened.getByLabel("Registreringsnummer")).toHaveAttribute("readonly", "");
  await opened.getByRole("button", { name: "Stäng kort" }).click();

  await page.getByLabel("URL:er").fill("https://cars.example/item/complete?new=1");
  await page.getByRole("button", { name: "Analysera URL:er" }).click();
  const candidate = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "complete" });
  await candidate.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await candidate.getByLabel("Märke").fill("Saab");
  await candidate.getByRole("button", { name: "Spara bil" }).click();

  const comparison = page.getByRole("alertdialog", { name: /ABC123 finns redan/ });
  await expect(comparison).toBeVisible();
  await expect(comparison.getByRole("button", { name: "Ersätt sparad bil" })).toBeDisabled();
  await comparison.getByRole("radio", { name: /Använd ny uppgift.*Saab/ }).check();
  const replacePromise = page.waitForResponse((response) =>
    response.url().includes("/api/saved-listings/") && response.request().method() === "PUT",
  );
  await comparison.getByRole("button", { name: "Ersätt sparad bil" }).click();
  const replaced = await replacePromise;
  expect(replaced.status()).toBe(200);
  expectSameOrigin(page, replaced);
  await expect(candidate.getByText("Sparad", { exact: true })).toBeVisible();

  await page.reload();
  const updatedSummary = page.getByText("ABC123", { exact: true }).first().locator("xpath=ancestor::li");
  await expect(updatedSummary).toContainText("Saab V70 2008");
  await updatedSummary.getByRole("button", { name: "Öppna", exact: true }).click();
  const updatedCard = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "Saab V70 2.4" });
  await updatedCard.getByRole("button", { name: "Radera bilen" }).click();
  await expect(page.getByRole("alertdialog", { name: /Radera ABC123 permanent/ })).toContainText("permanent");
  const deletePromise = page.waitForResponse((response) =>
    response.url().includes("/api/saved-listings/") && response.request().method() === "DELETE",
  );
  await page.getByRole("button", { name: "Radera bilen permanent" }).click();
  const deleted = await deletePromise;
  expect(deleted.status()).toBe(204);
  expectSameOrigin(page, deleted);
  await expect(updatedCard.getByText("Osparat utkast", { exact: true })).toBeVisible();
  await expect(updatedCard.getByText(/ligger kvar här som ett osparat utkast/)).toBeVisible();
  const missing = await page.request.get("/api/saved-listings/by-registration/ABC123");
  expect(missing.status()).toBe(404);
});

test("attaches a manual listing to a scenario-only vehicle and warns before deleting both", async ({ page }) => {
  const registrationNumber = randomRegistrationNumber();
  const scenarioResponse = await page.request.post("/api/saved-cost-scenarios", {
    data: {
      registrationNumber,
      scenario: {
        vehicleLabel: "Scenariofordon",
        calculationPeriodMonths: 12,
        purchasePriceSek: 20000,
        expectedResidualValueSek: null,
        annualDistanceKilometres: 0,
        financing: null,
        energySources: [],
        vehicleTax: null,
        insurance: null,
        maintenanceAndRepairs: null,
        otherRecurringCosts: [],
        otherOneTimeCosts: [],
      },
    },
  });
  expect(scenarioResponse.status()).toBe(201);

  await page.goto("/analyze-urls");
  await page.getByLabel("URL:er").fill(`https://cars.example/item/manual-${registrationNumber}`);
  await page.getByRole("button", { name: "Skapa manuella utkast" }).click();
  const draft = page.locator('[data-testid^="listing-card-"]');
  await draft.getByLabel("Registreringsnummer").fill(registrationNumber);
  await draft.getByLabel("Märke").fill("Volvo");
  await draft.getByRole("button", { name: "Spara bil" }).click();

  const attach = page.getByRole("alertdialog", { name: /har redan en sparad kalkyl/ });
  await expect(attach).toBeVisible();
  const attachPromise = page.waitForResponse((response) =>
    response.url().includes("/api/saved-listings/") && response.request().method() === "PUT",
  );
  await attach.getByRole("button", { name: "Koppla annons till befintlig bil" }).click();
  expect((await attachPromise).status()).toBe(200);
  await expect(draft.getByText(/sparade kalkylen finns kvar/)).toBeVisible();

  await draft.getByRole("button", { name: "Radera bilen" }).click();
  const deletion = page.getByRole("alertdialog", { name: new RegExp(`Radera ${registrationNumber} permanent`) });
  await expect(deletion).toContainText("sparad kalkyl som raderas samtidigt");
  await deletion.getByRole("button", { name: "Radera bilen permanent" }).click();
  await expect(draft.getByText("Osparat utkast", { exact: true })).toBeVisible();
});

async function removeSavedListingIfPresent(page: import("@playwright/test").Page, registrationNumber: string) {
  const existing = await page.request.get(`/api/saved-listings/by-registration/${registrationNumber}`);
  if (!existing.ok()) return;
  const resource = await existing.json() as { vehicleId: string; revision: number };
  await page.request.delete(`/api/saved-listings/${resource.vehicleId}?expectedRevision=${resource.revision}`);
}

function randomRegistrationNumber() {
  const letters = "ABCDEFGHJKLMNPRSTUWXYZ";
  const suffix = Math.floor(100 + Math.random() * 900);
  return `${letters[Math.floor(Math.random() * letters.length)]}${letters[Math.floor(Math.random() * letters.length)]}${letters[Math.floor(Math.random() * letters.length)]}${suffix}`;
}

function expectSameOrigin(page: import("@playwright/test").Page, response: import("@playwright/test").Response) {
  expect(new URL(response.url()).origin).toBe(new URL(page.url()).origin);
}
