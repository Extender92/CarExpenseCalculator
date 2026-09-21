import { expect, test, type APIRequestContext } from "@playwright/test";
import { presentationDescription, presentationEquipment, presentationSource,
  seedPresentationListings, removePresentationListings } from "./listing-presentation-fixtures";

async function shared(request: APIRequestContext, route: string, input: unknown) {
  const current = await request.get(`/api/${route}`);
  const expectedRevision = current.status() === 404 ? 0 : (await current.json()).revision;
  const saved = await request.put(`/api/${route}`, { data: { expectedRevision, input } });
  expect(saved.status(), await saved.text()).toBe(200);
}

for (const mode of ["summary", "full"] as const) {
  test(`${mode} PDF freezes all cars, sources, gaps and individual electric shares`, async ({ page, request }, testInfo) => {
    const owned: string[] = [];
    const before = await (await request.get("/api/comparisons/baseline")).json();
    try {
      await shared(request, "household-profile", { periodMonths: 24, startMonth: { year: 2026, month: 1 },
        annualDistanceKilometres: 12000, purchaseCashSek: 100000, activeSensitivityMode: "baseline",
        homeChargingSharePercent: { single: 100 }, homeChargingPricePerKilowattHourSek: { single: 2 },
        energyPrices: [{ fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } }] });
      await shared(request, "rule-profile", {});
      await seedPresentationListings(request, owned);
      for (const [i, id] of owned.entries()) {
        const car = await (await request.get(`/api/vehicle-cost-inputs/${id}`)).json();
        const saved = await request.put(`/api/vehicle-cost-inputs/${id}`, { data: { expectedRevision: car.revision,
          cost: { input: { candidateKey: car.registrationNumber, acquisitionType: "purchase", priceSek: 60000,
            electricDrivingShare: { mode: "override", value: { single: i === 0 ? 20 : 80 } },
            energySources: [
              { key: "petrol", fuel: "petrol", unit: "litre", consumptionBasis: "drivingMode", consumptionPer100Kilometres: { single: 5 } },
              { key: "electric", fuel: "electricity", unit: "kilowattHour", consumptionBasis: "drivingMode", electricityBasis: "metered", consumptionPer100Kilometres: { single: 20 } },
            ] } } } });
        expect(saved.status(), await saved.text()).toBe(200);
      }
      await page.goto("/search");
      await expect(page.getByLabel("Rapportinnehåll")).toHaveValue("summary");
      await page.getByLabel("Rapportinnehåll").selectOption(mode);
      const open = page.getByRole("button", { name: "Öppna rapport", exact: true });
      await expect(open).toBeEnabled();
      const calls: string[] = [];
      page.on("request", r => { if (new URL(r.url()).pathname.startsWith("/api/")) calls.push(r.url()); });
      await open.click();
      const print = page.getByRole("button", { name: "Skriv ut / Spara som PDF" });
      await expect(print).toBeEnabled();
      const article = page.getByRole("article");
      await expect(article).toContainText("LPR100");
      await expect(article).toContainText("LPR101");
      await expect(article).toContainText("Elandel: 20 % · Bilens eget val");
      await expect(article).toContainText("Elandel: 80 % · Bilens eget val");
      await expect(article).toContainText("känd del");
      await expect(article).toContainText(presentationSource);
      if (mode === "full") {
        await expect(article).toContainText(presentationDescription);
        await expect(article).toContainText(presentationEquipment.at(-1)!);
        await expect(article).toContainText("Handlare");
        await expect(article).toContainText("Privat");
      } else {
        await expect(article).not.toContainText(presentationDescription);
        await expect(article).toContainText("Sammanfattning");
        await expect(article).toContainText("Inga köpkrav valda");
      }
      const frozen = await article.innerText();
      // An independent client changes shared data; the report remains frozen.
      await shared(request, "household-profile", { ...before.profile, activeSensitivityMode: "baseline", periodMonths: 6 });
      await page.emulateMedia({ media: "print" });
      await page.pdf({ path: testInfo.outputPath(`${mode}.pdf`), preferCSSPageSize: true, printBackground: true });
      await page.emulateMedia({ media: "screen" });
      await page.setViewportSize({ width: 390, height: 844 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
      await page.screenshot({ path: testInfo.outputPath(`${mode}-mobile.png`), fullPage: true });
      expect(await article.innerText()).toBe(frozen);
      expect(calls).toEqual([]);
    } finally {
      await removePresentationListings(request, owned);
      await shared(request, "household-profile", before.profile ?? {});
      await shared(request, "rule-profile", before.rules ?? {});
    }
  });
}
