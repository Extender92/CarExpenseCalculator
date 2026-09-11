import {expect, test} from "@playwright/test";
import {presentationDescription, presentationEquipment, presentationSource,
  seedPresentationListings, removePresentationListings} from "./listing-presentation-fixtures";

for (const view of ["comparison", "report"] as const) {
  test(`saved listing ${view} keeps mobile scrolling local and seller kinds Swedish`, async ({page, request}, testInfo) => {
    const owned: string[] = [];
    try {
      await seedPresentationListings(request, owned);
      await page.setViewportSize({width: 390, height: 844});
      await page.goto("/search");
      const open = page.getByRole("button", {name: "Öppna rapport", exact: true});
      await expect(open).toBeEnabled();
      if (view === "report") {
        await open.click();
        await expect(page.getByRole("button", {name: "Skriv ut / Spara som PDF", exact: true})).toBeEnabled();
      } else await page.locator("summary").filter({hasText: /^Annonsunderlag$/}).click();

      // This fails on the former unwrapped 700px report table (document: 724px).
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
      for (const [i, label] of ["Handlare", "Privat"].entries()) {
        const title = `LPR10${i} – Annonsunderlag`;
        const table = page.getByRole("table", {name: title, exact: true});
        const scroll = page.getByRole("region", {name: `${title} – rullbar tabell`, exact: true});
        await expect(scroll).toBeVisible();
        await scroll.focus();
        await expect(scroll).toBeFocused();
        expect(await scroll.evaluate(el => el.scrollWidth > el.clientWidth)).toBe(true);
        await page.keyboard.press("ArrowRight");
        await expect.poll(() => scroll.evaluate(el => el.scrollLeft)).toBeGreaterThan(0);
        await scroll.evaluate(el => { el.scrollLeft = el.scrollWidth; });
        expect(await scroll.evaluate(el => el.scrollLeft + el.clientWidth >= el.scrollWidth - 1)).toBe(true);
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
        const seller = table.getByRole("row").filter({has: page.getByRole("rowheader", {name: "Annons · Säljartyp", exact: true})});
        await expect(seller.getByRole("cell")).toContainText(label);
        await expect(seller).toContainText("Obekräftat");
        await expect(table).toContainText(presentationDescription);
        await expect(table).toContainText(presentationSource);
        for (const equipment of presentationEquipment) await expect(table).toContainText(equipment);
        await expect(table).toContainText("Värde: dealer");
      }
      if (view === "report") {
        const article = page.getByRole("article");
        const before = await article.innerText();
        const current = await (await request.get(`/api/saved-listings/${owned[0]}`)).json();
        const updated = await request.put(`/api/saved-listings/${owned[0]}`, {data: {
          expectedRevision: current.revision, listing: {submittedUrl: current.submittedUrl,
            analyzedAtUtc: current.analyzedAtUtc, requestedModel: current.requestedModel,
            promptVersion: current.promptVersion, schemaVersion: current.schemaVersion, sources: current.sources.map((s: {url: string}) => s.url),
            draft: {...current.listing, sellerType: {...current.listing.sellerType, value: "private"}}}}});
        expect(updated.status(), await updated.text()).toBe(200);
        let requests = 0; page.on("request", () => requests++);
        await page.emulateMedia({media: "print"});
        for (const scroll of await page.locator(".listing-content [tabindex='0']").all()) {
          expect(await scroll.evaluate(el => getComputedStyle(el).overflowX)).toBe("visible");
          expect(await scroll.evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true);
        }
        const pdf = await page.pdf({preferCSSPageSize: true});
        await testInfo.attach("saved-listing-presentation.pdf", {body: pdf, contentType: "application/pdf"});
        expect(await article.innerText()).toBe(before);
        expect(requests).toBe(0);
      }
    } finally { await removePresentationListings(request, owned); }
  });
}
