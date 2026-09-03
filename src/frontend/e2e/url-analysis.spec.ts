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
  await expect(page.getByText("Inga URL:er har lagts till ännu.")).toBeVisible();
});
