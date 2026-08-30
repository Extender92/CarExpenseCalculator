import { expect, test } from "@playwright/test";

test("serves the dashboard and API through one origin", async ({ page, request }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: /ett bättre beslutsunderlag/i })).toBeVisible();
  await expect(page.getByText("Systemet är friskt")).toBeVisible();

  const response = await request.get("/api/system/status");
  expect(response.ok()).toBeTruthy();
  await expect(response.json()).resolves.toMatchObject({
    status: "healthy",
    database: "available",
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
