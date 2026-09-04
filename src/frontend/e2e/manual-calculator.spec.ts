import { expect, test, type Page, type Response } from "@playwright/test";

test("calculates the documented manual ownership scenario through the single origin", async ({ page }) => {
  await page.goto("/manual");
  await fillDocumentedScenario(page);

  const responsePromise = page.waitForResponse((response) =>
    response.url().endsWith("/api/manual-calculations") && response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Beräkna kostnad" }).click();
  const response = await responsePromise;

  expect(response.status()).toBe(200);
  expectSameOrigin(page, response);
  await expect(page.getByText(/64\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText(/49\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText("1 200 liter")).toBeVisible();
  await expect(page.getByText("Alla standardvärden är kända.")).toBeVisible();
});

test("saves, reopens, replaces, and deletes a vehicle through PostgreSQL", async ({ page }) => {
  const registrationNumber = randomRegistrationNumber();
  const vehicleName = `Volvo V70 E2E ${registrationNumber}`;
  await page.goto("/manual");
  await fillDocumentedScenario(page);
  await page.getByLabel("Bilens namn").fill(vehicleName);
  await page.getByLabel("Registreringsnummer").fill(registrationNumber);

  const createResponsePromise = page.waitForResponse((response) =>
    response.url().endsWith("/api/saved-cost-scenarios") && response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Spara bil" }).click();
  const createResponse = await createResponsePromise;
  expect(createResponse.status()).toBe(201);
  expectSameOrigin(page, createResponse);

  await expect(page.getByText("Bilen har sparats.")).toBeVisible();
  await expect(page.getByText(`${vehicleName} (${registrationNumber})`)).toBeVisible();
  await expect(page.getByText(/64\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText(/49\s000,00\s*kr/).first()).toBeVisible();

  await page.getByLabel(/Inköpspris/).fill("21000");
  const replaceResponsePromise = page.waitForResponse((response) =>
    response.url().includes("/api/saved-cost-scenarios/") && response.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Spara ändringar" }).click();
  const replaceResponse = await replaceResponsePromise;
  expect(replaceResponse.status()).toBe(200);
  expectSameOrigin(page, replaceResponse);
  await expect(page.getByText("Sparad revision 2")).toBeVisible();

  await page.reload();
  const savedHeading = page.getByText(`${vehicleName} (${registrationNumber})`);
  await expect(savedHeading).toBeVisible();
  const savedCard = savedHeading.locator("xpath=ancestor::li");
  await savedCard.getByRole("button", { name: "Öppna" }).click();
  await expect(page.getByLabel(/Inköpspris/)).toHaveValue("21000");
  await expect(page.getByLabel("Registreringsnummer")).toHaveAttribute("readonly", "");

  await savedCard.getByRole("button", { name: new RegExp(`Ta bort ${vehicleName}`) }).click();
  const deleteResponsePromise = page.waitForResponse((response) =>
    response.url().includes("/api/saved-cost-scenarios/") && response.request().method() === "DELETE",
  );
  await page.getByRole("button", { name: "Ta bort permanent" }).click();
  const deleteResponse = await deleteResponsePromise;
  expect(deleteResponse.status()).toBe(204);
  expectSameOrigin(page, deleteResponse);

  await expect(page.getByText(/finns kvar som en osparad kalkyl/)).toBeVisible();
  await expect(page.getByLabel(/Inköpspris/)).toHaveValue("21000");
  await expect(page.getByLabel("Registreringsnummer")).toBeEnabled();
  await expect(page.getByText(`${vehicleName} (${registrationNumber})`)).not.toBeVisible();
});

test("links a saved listing, detects listing drift, and reviews the current version", async ({ page }) => {
  const registrationNumber = "LNK123";
  await removeVehicleIfPresent(page, registrationNumber);

  await page.goto("/analyze-urls");
  await page.getByLabel("URL:er").fill("https://cars.example/item/complete");
  await page.getByRole("button", { name: "Analysera URL:er" }).click();
  const listingCard = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "complete" });
  await expect(listingCard.getByText("Volvo V70 2.4")).toBeVisible();
  await listingCard.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await listingCard.getByLabel("Registreringsnummer").fill(registrationNumber);
  const listingCreate = page.waitForResponse((response) =>
    response.url().endsWith("/api/saved-listings") && response.request().method() === "POST",
  );
  await listingCard.getByRole("button", { name: "Spara bil" }).click();
  expect((await listingCreate).status()).toBe(201);

  await listingCard.getByRole("button", { name: "Skapa kalkyl" }).click();
  await expect(page).toHaveURL(/\/manual\?listingVehicleId=/);
  await expect(page.getByRole("heading", { name: "Annonsuppgifter för kalkylen" })).toBeVisible();
  await expect(page.getByLabel("Registreringsnummer")).toHaveValue(registrationNumber);
  await expect(page.getByLabel(/Inköpspris/)).toHaveValue("20000");
  await fillLinkedScenarioAssumptions(page);

  const scenarioCreate = page.waitForResponse((response) =>
    response.url().includes("/api/saved-cost-scenarios/")
      && response.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Spara kalkyl" }).click();
  expect((await scenarioCreate).status()).toBe(200);
  await expect(page.getByText("Kopplad till aktuell annons")).toBeVisible();
  await expect(page.getByText(/64\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText(/49\s000,00\s*kr/).first()).toBeVisible();

  await page.goto("/analyze-urls");
  const summary = page.getByText(registrationNumber, { exact: true }).first().locator("xpath=ancestor::li");
  await expect(summary.getByText("Kalkyl aktuell")).toBeVisible();
  await summary.getByRole("button", { name: "Öppna", exact: true }).click();
  const opened = page.locator('[data-testid^="listing-card-"]').filter({ hasText: "Volvo V70 2.4" });
  await opened.getByRole("button", { name: "Granska och komplettera alla uppgifter" }).click();
  await opened.getByLabel("Annonspris").fill("21000");
  const listingReplace = page.waitForResponse((response) =>
    response.url().includes("/api/saved-listings/") && response.request().method() === "PUT",
  );
  await opened.getByRole("button", { name: "Spara ändringar" }).click();
  expect((await listingReplace).status()).toBe(200);
  await expect(opened.getByText("Kalkyl inaktuell")).toBeVisible();

  await opened.getByRole("button", { name: "Öppna kalkyl" }).click();
  await expect(page).toHaveURL(/\/manual\?listingVehicleId=/);
  await expect(page.getByText("Tidigare kalkyl är inaktuell")).toBeVisible();
  await expect(page.getByLabel(/Inköpspris/)).toHaveValue("20000");
  const listingPanel = page.getByRole("heading", { name: "Annonsuppgifter för kalkylen" })
    .locator("xpath=ancestor::div[contains(@class,'rounded-2xl')][1]");
  await expect(listingPanel.getByText(/21\s000,00\s*kr/)).toBeVisible();
  await listingPanel.getByRole("button", { name: "Använd annonsvärdet" }).first().click();
  await expect(page.getByLabel(/Inköpspris/)).toHaveValue("21000");

  const reviewSave = page.waitForResponse((response) =>
    response.url().includes("/api/saved-cost-scenarios/")
      && response.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Spara ändringar" }).click();
  expect((await reviewSave).status()).toBe(200);
  await expect(page.getByText("Kopplad till aktuell annons")).toBeVisible();

  await removeVehicleIfPresent(page, registrationNumber);
});

async function fillDocumentedScenario(page: Page) {
  await page.getByLabel(/Inköpspris/).fill("20000");
  await page.getByLabel(/Årlig körsträcka/).fill("1500");

  await page.getByRole("group", { name: "Förväntat restvärde" }).getByRole("radio", { name: "Känt", exact: true }).check();
  await page.getByLabel(/Förväntat restvärde/).fill("15000");

  await page.getByRole("radio", { name: "Finansiering" }).check();
  await page.getByLabel(/Kontantinsats/).fill("5000");
  await page.getByLabel(/Nominell årsränta/).fill("0");
  await page.getByLabel(/Lånets löptid/).fill("12");

  await page.getByRole("button", { name: "Lägg till energikälla" }).click();
  await page.locator("#manual-energySources-0-label").fill("Bensin");
  await page.locator("#manual-energySources-0-unit").selectOption("litre");
  await page.locator("#manual-energySources-0-consumptionPer100Kilometres").fill("8");
  await page.locator("#manual-energySources-0-pricePerUnitSek").fill("20");

  await page.locator('input[name="vehicleTax.known"][value="known"]').check();
  await page.locator("#manual-vehicleTax-amountSek").fill("2400");
  await page.locator("#manual-vehicleTax-cadence").selectOption("annual");
  await page.locator('input[name="insurance.known"][value="known"]').check();
  await page.locator("#manual-insurance-amountSek").fill("500");
  await page.locator("#manual-insurance-cadence").selectOption("monthly");
  await page.locator('input[name="maintenanceAndRepairs.known"][value="known"]').check();
  await page.locator("#manual-maintenanceAndRepairs-amountSek").fill("6000");
  await page.locator("#manual-maintenanceAndRepairs-cadence").selectOption("annual");

  await page.getByRole("button", { name: "Lägg till återkommande" }).click();
  await page.locator("#manual-otherRecurringCosts-0-label").fill("Övrigt");
  await page.locator("#manual-otherRecurringCosts-0-amountSek").fill("300");
  await page.locator("#manual-otherRecurringCosts-0-cadence").selectOption("monthly");
  await page.getByRole("button", { name: "Lägg till engångskostnad" }).click();
  await page.locator("#manual-otherOneTimeCosts-0-label").fill("Leverans");
  await page.locator("#manual-otherOneTimeCosts-0-amountSek").fill("2000");
}

async function fillLinkedScenarioAssumptions(page: Page) {
  await page.getByLabel(/Årlig körsträcka/).fill("1500");
  await page.getByRole("group", { name: "Förväntat restvärde" })
    .getByRole("radio", { name: "Känt", exact: true }).check();
  await page.getByLabel(/Förväntat restvärde/).fill("15000");

  await page.getByRole("radio", { name: "Finansiering" }).check();
  await page.getByLabel(/Kontantinsats/).fill("5000");
  await page.getByLabel(/Nominell årsränta/).fill("0");
  await page.getByLabel(/Lånets löptid/).fill("12");

  await page.locator("#manual-energySources-0-pricePerUnitSek").fill("20");
  await page.locator("#manual-energySources-0-distanceSharePercent").fill("100");
  await page.locator('input[name="insurance.known"][value="known"]').check();
  await page.locator("#manual-insurance-amountSek").fill("500");
  await page.locator("#manual-insurance-cadence").selectOption("monthly");
  await page.locator('input[name="maintenanceAndRepairs.known"][value="known"]').check();
  await page.locator("#manual-maintenanceAndRepairs-amountSek").fill("6000");
  await page.locator("#manual-maintenanceAndRepairs-cadence").selectOption("annual");

  await page.getByRole("button", { name: "Lägg till återkommande" }).click();
  await page.locator("#manual-otherRecurringCosts-0-label").fill("Övrigt");
  await page.locator("#manual-otherRecurringCosts-0-amountSek").fill("300");
  await page.locator("#manual-otherRecurringCosts-0-cadence").selectOption("monthly");
  await page.getByRole("button", { name: "Lägg till engångskostnad" }).click();
  await page.locator("#manual-otherOneTimeCosts-0-label").fill("Leverans");
  await page.locator("#manual-otherOneTimeCosts-0-amountSek").fill("2000");
}

async function removeVehicleIfPresent(page: Page, registrationNumber: string) {
  const listing = await page.request.get(`/api/saved-listings/by-registration/${registrationNumber}`);
  if (listing.ok()) {
    const resource = await listing.json() as { vehicleId: string; revision: number };
    await page.request.delete(
      `/api/saved-listings/${resource.vehicleId}?expectedRevision=${resource.revision}`,
    );
    return;
  }

  const scenario = await page.request.get(
    `/api/saved-cost-scenarios/by-registration/${registrationNumber}`,
  );
  if (!scenario.ok()) return;
  const resource = await scenario.json() as { vehicleId: string; revision: number };
  await page.request.delete(
    `/api/saved-cost-scenarios/${resource.vehicleId}?expectedRevision=${resource.revision}`,
  );
}

function randomRegistrationNumber() {
  return `TST${Math.floor(Math.random() * 1_000).toString().padStart(3, "0")}`;
}

function expectSameOrigin(page: Page, response: Response) {
  expect(new URL(response.url()).origin).toBe(new URL(page.url()).origin);
}
