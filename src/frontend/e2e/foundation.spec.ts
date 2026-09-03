import { expect, test } from "@playwright/test";

test("serves the dashboard and API through one origin", async ({ page }) => {
  const responsePromise = page.waitForResponse((response) =>
    new URL(response.url()).pathname === "/api/system/status"
      && response.request().method() === "GET",
  );
  await page.goto("/");
  const response = await responsePromise;

  await expect(page.getByRole("heading", { name: /ett bättre beslutsunderlag/i })).toBeVisible();
  await expect(page.getByText("Systemet är friskt")).toBeVisible();

  expect(response.status()).toBe(200);
  expect(new URL(response.url()).origin).toBe(new URL(page.url()).origin);
  await expect(response.json()).resolves.toMatchObject({
    status: "healthy",
    database: "available",
    features: {
      ruleBasedSearch: false,
      urlAnalysis: true,
      manualCalculator: true,
      aiReview: false,
    },
  });
});

test("navigates to all three foundation routes", async ({ page }) => {
  await page.goto("/search");
  await expect(page.getByRole("heading", { name: "Regelsökning" })).toBeVisible();

  await page.goto("/analyze-urls");
  await expect(page.getByRole("heading", { name: "Analysera URL:er" })).toBeVisible();

  await page.goto("/manual");
  await expect(page.getByRole("heading", { name: "Manuell kalkyl" })).toBeVisible();
});
