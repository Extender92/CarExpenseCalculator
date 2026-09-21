import { expect, type Locator, type Page } from "@playwright/test";

/** Native dialogs make the background inert, but role locators can still match it. */
export async function editingScope(page: Page): Promise<Page | Locator> {
  return page.locator("dialog[open]:not(:has(dialog[open])), body:not(:has(dialog[open]))").last();
}

export async function openListingEditor(card: Locator) {
  await card.getByRole("button", { name: "Redigera bil", exact: true }).click();
  const dialog = card.getByRole("dialog", { name: /Redigera bil/ });
  await expect(dialog).toBeVisible();
  // Other tabs keep their forms mounted. Their hidden summaries must not be
  // clicked when background cost loading happens to finish before this loop.
  const listing = dialog.getByRole("tabpanel", { name: "Biluppgifter", exact: true });
  await expect(listing).toBeVisible();
  for (const summary of await listing.locator("summary").all()) {
    if (!await summary.evaluate(element => element.parentElement?.hasAttribute("open"))) await summary.click();
  }
  return dialog;
}

export async function saveListingCard(card: Locator) {
  const dialog = card.locator("dialog[open]");
  if (await dialog.count()) await dialog.getByRole("button", { name: /^(Spara bil|Lägg till bil)$/, exact: true }).click();
  else await card.getByRole("button", { name: /^(Lägg till bil|Spara ändringar)$/, exact: true }).click();
}

export async function closeEditor(page: Page, decision: "save" | "discard" = "save") {
  const dialog = page.locator("dialog[open]").last();
  await expect(dialog).toBeVisible();
  const count = await page.locator("dialog[open]").count();
  await dialog.getByRole("button", { name: "Stäng", exact: true }).first().click();
  const question = dialog.getByRole("button", { name: "Spara och stäng", exact: true });
  await expect.poll(async () => await page.locator("dialog[open]").count() < count || await question.isVisible()).toBe(true);
  if (await question.isVisible()) await dialog.getByRole("button", {
    name: decision === "save" ? "Spara och stäng" : "Kasta ändringar", exact: true,
  }).click();
  await expect(page.locator("dialog[open]")).toHaveCount(count - 1);
}

export async function openHouseholdProfile(page: Page) {
  const settings = page.locator("summary").filter({ hasText: /^Gemensamma uppgifter och köpkrav$/ });
  if (await settings.count() && !await settings.evaluate(element => element.parentElement?.hasAttribute("open"))) await settings.click();
  const current = await editingScope(page);
  await expect(current.getByLabel("Kontanter till bilköpet (kr)").or(current.getByRole("button", { name: "Redigera hushållsprofil", exact: true }))).toBeVisible();
  if (await current.getByLabel("Kontanter till bilköpet (kr)").isVisible()) return;
  await current.getByRole("button", { name: "Redigera hushållsprofil", exact: true }).click();
  await expect(page.locator("dialog[open]").last().getByLabel("Kontanter till bilköpet (kr)")).toBeVisible();
}

export async function showHouseholdResults(page: Page) {
  const scope = await editingScope(page);
  const summary = scope.locator("summary").filter({ hasText: /^Detaljerat kalkylresultat$/ });
  await expect(summary).toHaveCount(1);
  if (!await summary.evaluate(element => element.parentElement?.hasAttribute("open"))) await summary.click();
}

export async function expandListingSection(dialog: Locator, title: string) {
  const summary = dialog.locator("summary").filter({ hasText: title });
  if (!await summary.evaluate(element => element.parentElement?.hasAttribute("open"))) await summary.click();
}

/** The overview keeps optional settings collapsed until deliberately requested. */
export async function openComparisonSettings(page: Page) {
  const summary = page.locator("summary").filter({ hasText: /^Gemensamma uppgifter och köpkrav$/ });
  await expect(summary).toBeVisible();
  if (!await summary.evaluate(element => element.parentElement?.hasAttribute("open"))) await summary.click();
}
