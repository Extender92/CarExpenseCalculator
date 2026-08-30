import { expect, test } from "@playwright/test";

test("calculates the documented manual ownership scenario through the single origin", async ({ page }) => {
  await page.goto("/manual");

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

  const responsePromise = page.waitForResponse((response) =>
    response.url().endsWith("/api/manual-calculations") && response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Beräkna kostnad" }).click();
  const response = await responsePromise;

  expect(response.status()).toBe(200);
  await expect(page.getByText(/64\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText(/49\s000,00\s*kr/).first()).toBeVisible();
  await expect(page.getByText("1 200 liter")).toBeVisible();
  await expect(page.getByText("Alla standardvärden är kända.")).toBeVisible();
});
