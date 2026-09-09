import {
  expect,
  test,
  type APIRequestContext,
  type Page,
  type Locator,
} from "@playwright/test";
import { corruptOwnedLegacyResults } from "./legacy-test-data";
import type { components } from "../src/api/schema";

type Schema<K extends keyof components["schemas"]> = components["schemas"][K];
type Complete = Schema<"CompleteComparisonResponse">;
const owned = new Set<string>();
let previous: {
  profile: Schema<"HouseholdProfileInput">;
  rules: Schema<"RuleProfileInput">;
};
const date = "2026-09-09";
const zero = () => ({ isIncluded: false, items: [] });
const profile = (): Schema<"HouseholdProfileInput"> => ({
  periodMonths: 12,
  startMonth: { year: 2026, month: 1 },
  annualDistanceKilometres: 12000,
  purchaseCashSek: 100000,
  activeSensitivityMode: "baseline",
  startupBudgetSek: 0,
  monthlyBudgetSek: 2000,
  energyPrices: [
    { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 0 } },
  ],
});
const cost = (price = 40000, residual = 20000): Schema<"VehicleCostInput"> => ({
  candidateKey: "acceptance",
  acquisitionType: "purchase",
  priceSek: price,
  residual: {
    mode: "fixedAmount",
    value: { single: residual },
    periodMonths: 12,
  },
  energySources: [
    {
      key: "petrol",
      fuel: "petrol",
      unit: "litre",
      consumptionBasis: "wholeDistance",
      consumptionPer100Kilometres: { single: 1 },
    },
  ],
  tax: zero(),
  insurance: zero(),
  service: zero(),
  repairs: zero(),
  customCosts: zero(),
  additionalRepairAllowancePerMonthSek: { single: 0 },
});
const b1Rules = (): Schema<"RuleProfileInput"> => ({
  preferences: [
    {
      criterionKey: "purchasePriceSek",
      weight: 3,
      minimumEvidence: "userConfirmed",
      zeroPoint: 100000,
      fullPoint: 20000,
    },
    {
      criterionKey: "transmission",
      weight: 2,
      minimumEvidence: "userConfirmed",
      preferredValues: [{ transmission: "automatic" }],
    },
  ],
});
async function shared(
  request: APIRequestContext,
  route: string,
  input: unknown,
) {
  const current = await request.get(`/api/${route}`);
  const expectedRevision =
    current.status() === 404 ? 0 : (await current.json()).revision;
  const saved = await request.put(`/api/${route}`, {
    data: { expectedRevision, input },
  });
  expect(saved.status(), await saved.text()).toBe(200);
}
async function create(
  request: APIRequestContext,
  registrationNumber: string,
  input = cost(),
  edits: Schema<"VehicleFactEdits"> = {},
) {
  const response = await request.post("/api/vehicle-cost-inputs", {
    data: { registrationNumber, cost: { input } },
  });
  const car = await response.json();
  if (response.status() === 201) owned.add(car.vehicleId);
  expect(response.status(), JSON.stringify(car)).toBe(201);
  await facts(request, car.vehicleId, { edits, costConfirmation: "confirm" });
  return car as Schema<"VehicleCostInputResponse">;
}
async function facts(
  request: APIRequestContext,
  id: string,
  input: Schema<"VehicleFactsWrite">,
) {
  const current = await (await request.get(`/api/vehicle-facts/${id}`)).json();
  const saved = await request.put(`/api/vehicle-facts/${id}`, {
    data: { expectedRevision: current.revision, input },
  });
  expect(saved.status(), await saved.text()).toBe(200);
  return (await saved.json()) as Schema<"VehicleFactsResponse">;
}
const main = (page: Page) =>
  page.getByRole("table", { name: "Huvudjämförelse", exact: true });
const row = (page: Page, id: string) =>
  main(page).locator(`[data-vehicle-id="${id}"]`);
async function openDetails(section: Locator) {
  if (!(await section.evaluate((node) => (node as HTMLDetailsElement).open)))
    await section.locator(":scope > summary").click();
}
async function enter(page: Page) {
  await page.goto("/search");
  await page.getByLabel("Utvärderingsdatum", { exact: true }).fill(date);
  await expect(
    page.getByRole("button", { name: "Öppna rapport", exact: true }),
  ).toBeEnabled();
}
async function calculate(page: Page): Promise<Complete> {
  const pending = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/comparisons/preview-all") &&
      r.request().method() === "POST" &&
      r.status() === 200,
  );
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
  const response = await pending;
  const result = (await response.json()) as Complete;
  expect(result.views.baseline.asOfDate).toBe(date);
  await expect(
    page.getByText("Resultaten är inaktuella.", { exact: false }),
  ).not.toBeVisible();
  return result;
}
async function report(page: Page) {
  const open = page.getByRole("button", { name: "Öppna rapport", exact: true });
  await expect(open).toBeEnabled({ timeout: 30000 });
  await open.click();
  await expect(page).toHaveURL(/\/search\/report$/);
  await expect(
    page.getByRole("button", { name: "Skriv ut / Spara som PDF" }),
  ).toBeEnabled({ timeout: 30000 });
}
async function saveFacts(page: Page) {
  const saved = page.waitForResponse(
    (r) =>
      r.url().includes("/api/vehicle-facts/") && r.request().method() === "PUT",
  );
  await page
    .getByRole("button", { name: "Spara biluppgifter", exact: true })
    .click();
  const response = await saved;
  expect(response.status(), await response.text()).toBe(200);
}
test.beforeEach(async ({ request, page }) => {
  const baseline = await (
    await request.get("/api/comparisons/baseline")
  ).json();
  previous = { profile: baseline.profile ?? {}, rules: baseline.rules ?? {} };
  await shared(request, "household-profile", profile());
  await shared(request, "rule-profile", {});
  page.on("dialog", (dialog) => void dialog.accept());
});
test.afterEach(async ({ request, page }) => {
  await page.goto("about:blank");
  const failures: string[] = [];
  for (const id of owned) {
    try {
      const current = await request.get(`/api/vehicle-cost-inputs/${id}`);
      if (current.status() === 404) continue;
      expect(current.status(), await current.text()).toBe(200);
      const deleted = await request.delete(
        `/api/vehicle-cost-inputs/${id}?expectedRevision=${(await current.json()).revision}`,
      );
      expect(deleted.status(), await deleted.text()).toBe(204);
    } catch (error) {
      failures.push(`${id}: ${String(error)}`);
    }
  }
  owned.clear();
  for (const [route, input] of [
    ["household-profile", previous.profile],
    ["rule-profile", previous.rules],
  ] as const) {
    try {
      await shared(request, route, input);
    } catch (error) {
      failures.push(`${route}: ${String(error)}`);
    }
  }
  expect(
    failures,
    "Every fixture cleanup is attempted, even after another cleanup fails",
  ).toEqual([]);
});

test("all twenty criteria use explicit editor goals, evidence and authoritative HTTP outcomes", async ({
  request,
  page,
}) => {
  test.setTimeout(150000);
  const car = await create(request, "TAA100", cost(12000, 0), {
    odometerKilometres: { kind: "manual", manual: { value: 200000 } },
    ownerCount: { kind: "manual", manual: { value: 3 } },
    towBar: { kind: "manual", manual: { value: false } },
    transmission: { kind: "manual", manual: { value: "automatic" } },
    seats: { kind: "manual", manual: { value: 5 } },
    modelYear: { kind: "manual", manual: { value: 2010 } },
    fuelTypes: { kind: "manual", manual: { value: ["petrol", "electricity"] } },
    bodyType: { kind: "manual", manual: { value: "wagon" } },
    drivetrain: { kind: "manual", manual: { value: "allWheelDrive" } },
    locality: { kind: "manual", manual: { value: "Örebro" } },
    county: { kind: "manual", manual: { value: "Örebro län" } },
    towingCapacityKilograms: { kind: "manual", manual: { value: 1500 } },
    inspectionValidThrough: { kind: "manual", manual: { value: "2026-10-09" } },
    serviceDocumentation: { kind: "manual", manual: { value: "documented" } },
  });
  await enter(page);
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).click();
  // Fixed independent examples: ten numeric criteria score 50, eight choices 100.
  const numeric = [
    ["purchasePriceSek", "12000", "0", "24000", 12000],
    ["odometerKilometres", "20000", "0", "40000", 200000],
    ["ownerCount", "3", "0", "6", 3],
    ["seats", "5", "2", "8", 5],
    ["modelYear", "2010", "2000", "2020", 2010],
    ["towingCapacityKilograms", "1500", "0", "3000", 1500],
    ["inspectionValidThrough", "30", "0", "60", 30],
    ["netCostSek", "12000", "0", "24000", 12000],
    ["costPerMonthSek", "1000", "0", "2000", 1000],
    ["costPerMilSek", "10", "0", "20", 10],
  ] as const;
  for (const [key, limit, zeroPoint, fullPoint, actual] of numeric) {
    const section = page.locator(`[data-criterion="${key}"]`);
    await openDetails(section);
    await section
      .getByRole("button", { name: "Lägg till krav", exact: true })
      .click();
    await section
      .getByLabel("Kravets verifiering")
      .selectOption("userConfirmed");
    await section
      .getByLabel(
        key === "inspectionValidThrough"
          ? "Minst antal återstående dagar"
          : "Lägsta tillåtna värde",
      )
      .fill(limit);
    await section
      .getByRole("button", { name: "Lägg till prioritering", exact: true })
      .click();
    await section
      .getByLabel("Prioriteringens verifiering")
      .selectOption("userConfirmed");
    await section.getByLabel("Värde som ger 0 poäng").fill(zeroPoint);
    await section.getByLabel("Värde som ger 100 poäng").fill(fullPoint);
    await section.getByLabel("Vikt (0–5)").fill("1");
    const result = (await calculate(page)).views.baseline.candidates[0];
    const hard = result.hardRules.find((r) => r.rule.criterionKey === key)!;
    expect(hard.state, key).toBe("pass");
    expect(hard.assessment.actual!.number, key).toBe(actual);
    expect(
      result.contributions.find((c) => c.preference.criterionKey === key)!
        .range,
      key,
    ).toEqual({ lower: 50, upper: 50 });
  }
  const choices = [
    ["towBar", "Nej"],
    ["transmission", "Automat"],
    ["fuelTypes", "El"],
    ["bodyType", "Kombi"],
    ["drivetrain", "Fyrhjulsdrift"],
    ["locality", " O\u0308REBRO "],
    ["county", " ÖREBRO LÄN "],
    ["serviceDocumentation", "Dokumenterat"],
  ] as const;
  for (const [key, selected] of choices) {
    const section = page.locator(`[data-criterion="${key}"]`);
    await openDetails(section);
    await section
      .getByRole("button", { name: "Lägg till krav", exact: true })
      .click();
    await section
      .getByLabel("Kravets verifiering")
      .selectOption("userConfirmed");
    await section
      .getByRole("button", { name: "Lägg till prioritering", exact: true })
      .click();
    await section
      .getByLabel("Prioriteringens verifiering")
      .selectOption("userConfirmed");
    for (const legend of ["Hårt krav", "Prioritering"]) {
      const group = section.getByRole("group", { name: legend, exact: true });
      if (key === "locality" || key === "county")
        await group
          .getByLabel("Önskade värden, separerade med semikolon")
          .fill(selected);
      else
        await group
          .getByRole("checkbox", { name: selected, exact: true })
          .check();
    }
    await section.getByLabel("Vikt (0–5)").fill("1");
    const result = (await calculate(page)).views.baseline.candidates[0];
    expect(
      result.hardRules.find((r) => r.rule.criterionKey === key)!.state,
      key,
    ).toBe("pass");
    expect(
      result.contributions.find((c) => c.preference.criterionKey === key)!
        .range,
      key,
    ).toEqual({ lower: 100, upper: 100 });
  }
  for (const key of ["startupBudget", "monthlyBudget"]) {
    const section = page.locator(`[data-criterion="${key}"]`);
    await openDetails(section);
    await section
      .getByRole("button", { name: "Lägg till krav", exact: true })
      .click();
    await section
      .getByLabel("Kravets verifiering")
      .selectOption("userConfirmed");
    await expect(
      section.getByRole("button", { name: "Lägg till prioritering" }),
    ).toHaveCount(0);
  }
  const complete = await calculate(page);
  for (const view of Object.values(complete.views)) {
    const result = view.candidates[0];
    expect(result.vehicleId).toBe(car.vehicleId);
    expect(result.hardRules).toHaveLength(20);
    expect(result.hardRules.every((r) => r.state === "pass")).toBe(true);
    expect(result.contributions).toHaveLength(18);
    expect(result.score).toEqual({ lower: 72.22, upper: 72.22 });
    expect(result.coveragePercent).toBe(100);
  }
  await expect(row(page, car.vehicleId)).toContainText("[72,22, 72,22]");
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  const details = page.getByRole("table", {
    name: "Krav, prioriteringar och källor",
    exact: true,
  });
  await expect(details.getByText(/ · Uppfyllt$/, { exact: false })).toHaveCount(
    20,
  );
  await page
    .getByRole("button", {
      name: "Spara köpkrav och prioriteringar",
      exact: true,
    })
    .click();
  await expect
    .poll(
      async () =>
        (await (await request.get("/api/rule-profile")).json()).input.hardRules
          .length,
    )
    .toBe(20);
  await report(page);
  await expect(main(page)).toContainText("[72,22, 72,22]");
  await expect(page.getByRole("article")).toContainText("Örebro");
});

test("inspection date boundaries and stronger evidence survive editing, persistence and reporting", async ({
  request,
  page,
}) => {
  await shared(request, "rule-profile", {
    hardRules: [
      {
        criterionKey: "inspectionValidThrough",
        operator: "minimumRemainingDays",
        minimumEvidence: "userConfirmed",
        minimum: 30,
      },
    ],
    preferences: [
      {
        criterionKey: "inspectionValidThrough",
        minimumEvidence: "userConfirmed",
        weight: 1,
        zeroPoint: 0,
        fullPoint: 100,
      },
    ],
    signals: [{ key: "inspectionValidity", shortInspectionDays: 30 }],
  });
  const car = await create(request, "TAA101");
  await enter(page);
  await row(page, car.vehicleId).getByRole("button").click();
  const fact = page.locator('[data-fact="inspectionValidThrough"]');
  await openDetails(fact);
  await fact
    .getByLabel("Åtgärd för Besiktningsgiltighet (återstående dagar)")
    .selectOption("manual");
  for (const [value, days, state, score] of [
    ["2026-09-08", -1, "fail", 0],
    ["2026-10-08", 29, "fail", 29],
    ["2026-10-09", 30, "pass", 30],
    ["2026-10-10", 31, "pass", 31],
  ] as const) {
    await fact.getByLabel("Besiktningen giltig till och med").fill(value);
    const result = (await calculate(page)).views.baseline.candidates[0];
    expect(result.hardRules[0].assessment.actual!.number).toBe(days);
    expect(result.hardRules[0].state).toBe(state);
    expect(result.score).toEqual({ lower: score, upper: score });
    expect(result.signals.some((s) => s.kind === "warning")).toBe(days < 30);
    await expect(row(page, car.vehicleId)).toContainText(
      state === "pass" ? "Godkänd" : "Bortvald",
    );
  }
  await saveFacts(page);
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).click();
  const rule = page.locator('[data-criterion="inspectionValidThrough"]');
  await openDetails(rule);
  await rule.getByLabel("Kravets verifiering").selectOption("registryVerified");
  await rule
    .getByLabel("Prioriteringens verifiering")
    .selectOption("registryVerified");
  const result = (await calculate(page)).views.baseline.candidates[0];
  expect(result.hardRules[0].state).toBe("needsVerification");
  expect(result.score).toEqual({ lower: 0, upper: 100 });
  expect(result.coveragePercent).toBe(0);
  expect(result.isCheapestEligibleComplete).toBe(false);
  await report(page);
  await expect(main(page)).toContainText("Behöver verifieras");
  await expect(main(page)).toContainText("[0,00, 100,00]");
});

const listing = (transmission: "automatic" | "manual") => {
  const url = "https://cars.example/issue67";
  const empty = Object.fromEntries(
    "registrationNumber make model variant modelYear vin vehicleLabel priceSek odometerKilometres sellerType locality county publishedDate updatedDate imageCount fuelTypes transmission drivetrain bodyType colour horsepower engineDisplacementCubicCentimetres energyConsumptions annualVehicleTaxSek ownerCount firstRegistrationDate lastInspectionDate nextInspectionDate towBar equipment sellerClaims conditionNotes"
      .split(" ")
      .map((key) => [key, null]),
  );
  const provenance = {
    origin: "listing",
    extractionMethod: "ai",
    verification: "unverified",
    sourceUrl: url,
  };
  return {
    submittedUrl: url,
    analyzedAtUtc: "2026-09-09T12:00:00Z",
    requestedModel: "fixture",
    promptVersion: 2,
    schemaVersion: 2,
    sources: [url],
    draft: {
      ...empty,
      priceSek: { value: 40000, provenance },
      transmission: { value: transmission, provenance },
    },
  };
};
test("listing adoption, economic changes, explicit confirmation and frozen report share one vehicle lifecycle", async ({
  request,
  page,
}) => {
  test.setTimeout(90000);
  const rules = b1Rules();
  rules.preferences![1].minimumEvidence = "advertised";
  await shared(request, "rule-profile", rules);
  const added = await request.post("/api/saved-listings", {
    data: { registrationNumber: "TAA102", listing: listing("automatic") },
  });
  const car = await added.json();
  if (added.status() === 201) owned.add(car.vehicleId);
  expect(added.status(), JSON.stringify(car)).toBe(201);
  const savedCost = await request.put(
    `/api/vehicle-cost-inputs/${car.vehicleId}`,
    { data: { expectedRevision: car.revision, cost: { input: cost() } } },
  );
  expect(savedCost.status(), await savedCost.text()).toBe(200);
  await facts(request, car.vehicleId, { costConfirmation: "confirm" });
  await enter(page);
  await row(page, car.vehicleId).getByRole("button").click();
  const gear = page.locator('[data-fact="transmission"]');
  await openDetails(gear);
  await expect(gear).toContainText("Annonsförslag: Automat");
  await expect(row(page, car.vehicleId)).toContainText("[45,00, 85,00]");
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("listing");
  await saveFacts(page);
  await expect(row(page, car.vehicleId)).toContainText("[85,00, 85,00]");
  let source = await (
    await request.get(`/api/vehicle-facts/${car.vehicleId}`)
  ).json();
  expect(
    source.input.facts.transmission.observations[0].sourceListingVersion,
  ).toBe(1);
  expect(
    source.input.facts.transmission.observations[0].evidence.verification,
  ).toBe("unverified");
  for (const price of ["35000", ""]) {
    await page
      .getByRole("link", { name: "Redigera kalkylpriset", exact: true })
      .click();
    const priceField = page.getByLabel("Inköpspris (kr)", { exact: true });
    await expect(priceField).toBeFocused();
    await priceField.fill(price);
    const saving = page.waitForResponse(
      (r) =>
        r.url().endsWith(`/api/vehicle-cost-inputs/${car.vehicleId}`) &&
        r.request().method() === "PUT",
    );
    await page
      .getByRole("button", { name: "Spara bilunderlag", exact: true })
      .click();
    const saved = await saving;
    expect(saved.status(), await saved.text()).toBe(200);
    source = await (
      await request.get(`/api/vehicle-facts/${car.vehicleId}`)
    ).json();
    expect(source.costConfirmedAt).toBeNull();
    await page
      .getByRole("link", { name: "Tillbaka till jämförelsen", exact: true })
      .click();
    await expect(row(page, car.vehicleId)).toContainText("[40,00, 100,00]");
    let current = (await calculate(page)).views.baseline.candidates[0];
    expect(current.effectiveCostInput!.priceSek).toBe(price ? 35000 : null);
    if (price) {
      await page
        .getByRole("button", {
          name: "Bekräfta sparat kostnadsunderlag",
          exact: true,
        })
        .click();
      await expect(row(page, car.vehicleId)).toContainText("[88,75, 88,75]");
      current = (await calculate(page)).views.baseline.candidates[0];
      expect(current.costConfirmedAt).not.toBeNull();
      expect(current.contributions[0].assessment.actual!.number).toBe(35000);
    } else expect(current.contributions[0].assessment.actual).toBeNull();
  }
  const oldListing = await (
    await request.get(`/api/saved-listings/${car.vehicleId}`)
  ).json();
  const replaced = await request.put(`/api/saved-listings/${car.vehicleId}`, {
    data: { expectedRevision: oldListing.revision, listing: listing("manual") },
  });
  expect(replaced.status(), await replaced.text()).toBe(200);
  await page
    .getByRole("button", { name: "Läs aktuellt serverunderlag", exact: true })
    .click();
  await expect(gear).toContainText("Annonsförslag: Manuell");
  const current = (await calculate(page)).views.baseline.candidates[0];
  expect(current.needsListingReview).toBe(true);
  expect(
    current.effectiveFacts.facts.transmission!.observations[0]
      .sourceListingVersion,
  ).toBe(1);
  expect(current.effectiveFacts.facts.transmission!.observations[0].value).toBe(
    "automatic",
  );
  expect(current.effectiveCostInput!.priceSek).toBeNull();
  await report(page);
  const captured = await page.getByRole("article").innerText();
  const calls: string[] = [];
  page.on("request", (r) => {
    if (new URL(r.url()).pathname.startsWith("/api/")) calls.push(r.url());
  });
  await facts(request, car.vehicleId, {
    edits: { transmission: { kind: "manual", manual: { value: "manual" } } },
  });
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  expect(await page.getByRole("article").innerText()).toBe(captured);
  expect(calls).toEqual([]);
  await expect(main(page)).toContainText("känd del");
  await expect(main(page)).toContainText("[40,00, 100,00]");
});

test("two browsers recover a saved-rule conflict and capture only reviewed current assumptions", async ({
  request,
  page,
  browser,
  baseURL,
}) => {
  await shared(request, "rule-profile", b1Rules());
  const car = await create(request, "TAA103", cost(), {
    transmission: { kind: "manual", manual: { value: "automatic" } },
  });
  await enter(page);
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).click();
  const rule = page.locator('[data-criterion="purchasePriceSek"]');
  await openDetails(rule);
  await rule.getByLabel("Vikt (0–5)").fill("1");
  const other = await browser.newContext({ baseURL });
  try {
    const second = await other.newPage();
    await enter(second);
    await second
      .getByText("Köpkrav och prioriteringar", { exact: true })
      .click();
    const secondRule = second.locator('[data-criterion="purchasePriceSek"]');
    await openDetails(secondRule);
    await secondRule.getByLabel("Vikt (0–5)").fill("5");
    const save = second.waitForResponse(
      (r) =>
        r.url().endsWith("/api/rule-profile") && r.request().method() === "PUT",
    );
    await second
      .getByRole("button", {
        name: "Spara köpkrav och prioriteringar",
        exact: true,
      })
      .click();
    expect((await save).status()).toBe(200);
    const conflict = page.waitForResponse(
      (r) =>
        r.url().endsWith("/api/rule-profile") && r.request().method() === "PUT",
    );
    await page
      .getByRole("button", {
        name: "Spara köpkrav och prioriteringar",
        exact: true,
      })
      .click();
    expect((await conflict).status()).toBe(409);
    await expect(rule.getByLabel("Vikt (0–5)")).toHaveValue("1");
    await expect(
      page.getByRole("button", { name: "Öppna rapport", exact: true }),
    ).toBeDisabled();
    expect(
      (await (await request.get("/api/rule-profile")).json()).input
        .preferences[0].weight,
    ).toBe(5);
    await page
      .getByRole("button", { name: "Läs aktuellt serverunderlag", exact: true })
      .click();
    await expect(
      page.getByRole("heading", { name: "Serverunderlaget har ändrats" }),
    ).toBeVisible();
    await page
      .getByRole("button", {
        name: "Behåll granskade lokala ändringar",
        exact: true,
      })
      .click();
    await expect(row(page, car.vehicleId)).toContainText("[91,67, 91,67]");
    const reviewed = (await calculate(page)).views.baseline.candidates[0];
    expect(reviewed.unsaved.rules).toBe(true);
    await report(page);
    await expect(page.getByRole("article")).toContainText(
      "Osparade antaganden ingår",
    );
    await expect(main(page)).toContainText("[91,67, 91,67]");
    await page
      .getByRole("link", { name: "Tillbaka till jämförelsen", exact: true })
      .click();
    await expect(rule.getByLabel("Vikt (0–5)")).toHaveValue("1");
    const saved = page.waitForResponse(
      (r) =>
        r.url().endsWith("/api/rule-profile") && r.request().method() === "PUT",
    );
    await page
      .getByRole("button", {
        name: "Spara köpkrav och prioriteringar",
        exact: true,
      })
      .click();
    expect((await saved).status()).toBe(200);
    await second.reload();
    await expect(row(second, car.vehicleId)).toContainText("[91,67, 91,67]");
  } finally {
    await other.close();
  }
});

test("atomic legacy review retains all 50+50 sources through comparison, report and later explicit mapping", async ({
  request,
  page,
}) => {
  test.setTimeout(90000);
  const ids: string[] = [];
  for (const [index, registrationNumber] of ["TAA104", "TAA105"].entries()) {
    const created = await request.post("/api/saved-cost-scenarios", {
      data: {
        registrationNumber,
        scenario: {
          vehicleLabel: "Fiktiv äldre slutkontroll",
          calculationPeriodMonths: 24,
          purchasePriceSek: 20000,
          expectedResidualValueSek: 15000,
          annualDistanceKilometres: index ? 22222 : 11111,
          financing: null,
          energySources: [
            {
              label: "Äldre bränsle",
              unit: "litre",
              consumptionPer100Kilometres: 8,
              pricePerUnitSek: 20,
              distanceSharePercent: 100,
            },
          ],
          vehicleTax: null,
          insurance: null,
          maintenanceAndRepairs: { amountSek: 1000, cadence: "annual" },
          otherRecurringCosts: Array.from(
            { length: index ? 0 : 50 },
            (_, i) => ({
              label: `Återkommande ${i}`,
              amountSek: i + 1,
              cadence: "monthly",
            }),
          ),
          otherOneTimeCosts: Array.from({ length: index ? 0 : 50 }, (_, i) => ({
            label: `Engång ${i}`,
            amountSek: i + 1,
          })),
        },
      },
    });
    const car = await created.json();
    if (created.status() === 201) {
      owned.add(car.vehicleId);
      ids.push(car.vehicleId);
    }
    expect(created.status(), JSON.stringify(car)).toBe(201);
  }
  corruptOwnedLegacyResults(ids);
  await shared(request, "rule-profile", {
    preferences: [
      {
        criterionKey: "netCostSek",
        minimumEvidence: "userConfirmed",
        weight: 1,
        zeroPoint: 100000,
        fullPoint: 0,
      },
    ],
    hardRules: [
      {
        criterionKey: "monthlyBudget",
        operator: "withinBudget",
        minimumEvidence: "userConfirmed",
      },
    ],
  });
  await page.goto("/manual/transition");
  for (const [index, id] of ids.entries()) {
    const section = page.locator(`[data-vehicle-id="${id}"]`);
    await section
      .getByText("Originaluppgifter från äldre kalkyl", { exact: true })
      .click();
    await expect(
      section.getByText("Årskörsträcka (km)", { exact: true }).locator(".."),
    ).toContainText(index ? "22222" : "11111");
  }
  const first = page.locator(`[data-vehicle-id="${ids[0]}"]`);
  await expect(
    first.getByLabel("Beslut för Återkommande 49", { exact: true }),
  ).toHaveValue("map");
  await expect(
    first.getByLabel("Beslut för Engång 49", { exact: true }),
  ).toHaveValue("keepForReview");
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("24");
  await page.getByLabel("Årlig körsträcka (mil)").fill("1200");
  const transition = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/household-transition") &&
      r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Bekräfta hela övergången", exact: true })
    .click();
  const accepted = await transition;
  expect(accepted.status(), await accepted.text()).toBe(200);
  expect(accepted.request().postDataJSON().vehicles).toHaveLength(2);
  const current = (await (
    await request.get(`/api/vehicle-cost-inputs/${ids[0]}`)
  ).json()) as Schema<"VehicleCostInputResponse">;
  expect(current.input!.customCosts!.items).toHaveLength(50);
  const oneTime = current.unresolvedLegacyItems.filter(
    (r) => r.input.kind === "oneTime",
  );
  expect(oneTime).toHaveLength(50);
  expect(new Set(oneTime.map((r) => r.input.key)).size).toBe(50);
  await facts(request, ids[0], { costConfirmation: "confirm" });
  await enter(page);
  const preview = await calculate(page);
  const partial = preview.views.baseline.candidates.find(
    (c) => c.vehicleId === ids[0],
  )!;
  // (1 + ... + 50) × 24 = 30,600, independently of unresolved one-time amounts.
  expect(partial.cost.customCosts.cost.knownSubtotalSek).toBe(30600);
  expect(partial.cost.totals.ownershipCost.completeTotalSek).toBeNull();
  expect(partial.cost.monthlyBudget.status).not.toBe("withinLimit");
  expect(partial.score).toEqual({ lower: 0, upper: 100 });
  expect(partial.isCheapestEligibleComplete).toBe(false);
  await report(page);
  const article = page.getByRole("article");
  for (const i of [0, 25, 49]) {
    await expect(article).toContainText(`Återkommande ${i}`);
    await expect(article).toContainText(`Engång ${i}`);
  }
  for (const item of oneTime)
    await expect(article).toContainText(item.input.key);
  await page
    .getByRole("link", { name: "Tillbaka till jämförelsen", exact: true })
    .click();
  await row(page, ids[0])
    .getByRole("link", { name: "Ekonomiskt underlag", exact: true })
    .click();
  const review = page.getByLabel("Beslut för Engång 0", { exact: true });
  // Map into the separate repairs collection: the 50-entry custom collection is full.
  await expect(review).toBeVisible();
  await review.selectOption("map");
  const mapArea = review.locator("xpath=ancestor::fieldset[1]");
  await mapArea
    .getByLabel("Skapa mål för Engång 0", { exact: true })
    .selectOption("repairs");
  await mapArea
    .getByRole("button", { name: "Skapa från originalposten" })
    .click();
  const once = page.locator(
    '[data-field-path="input.repairs.items[0].monthOffset"]',
  );
  const repairSection = once.locator("xpath=ancestor::details[1]");
  await openDetails(repairSection);
  await once.fill("1");
  const save = page.waitForResponse(
    (r) =>
      r.url().endsWith(`/api/vehicle-cost-inputs/${ids[0]}`) &&
      r.request().method() === "PUT",
  );
  await page
    .getByRole("button", { name: "Spara bilunderlag", exact: true })
    .click();
  expect((await save).status()).toBe(200);
  await page
    .getByRole("link", { name: "Tillbaka till jämförelsen", exact: true })
    .click();
  const mapped = (await calculate(page)).views.baseline.candidates.find(
    (c) => c.vehicleId === ids[0],
  )!;
  expect(
    mapped.unresolvedLegacyItems.filter((r) => r.input.kind === "oneTime"),
  ).toHaveLength(49);
  expect(mapped.cost.repairs.cost.knownSubtotalSek).toBe(1);
  expect(mapped.cost.customCosts.cost.knownSubtotalSek).toBe(30600);
  expect(mapped.cost.totals.ownershipCost.completeTotalSek).toBeNull();
});

test("URL deletion clears compared facts, costs and the matching draft without resurrecting the car", async ({
  request,
  page,
}) => {
  await shared(request, "rule-profile", b1Rules());
  const car = await create(request, "TAA106", cost(), {
    transmission: { kind: "manual", manual: { value: "automatic" } },
  });
  const current = await (
    await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)
  ).json();
  const attached = await request.put(`/api/saved-listings/${car.vehicleId}`, {
    data: { expectedRevision: current.revision, listing: listing("automatic") },
  });
  expect(attached.status(), await attached.text()).toBe(200);
  const attachedCar = await attached.json();
  const emptySlot = await (await request.get("/api/vehicle-draft")).json();
  expect(emptySlot.input).toBeNull();
  const draft = await request.put("/api/vehicle-draft", {
    data: {
      expectedRevision: emptySlot.revision,
      input: {
        registrationNumber: "TAA106",
        baseVehicleId: car.vehicleId,
        baseVehicleRevision: attachedCar.revision,
        cost: { input: cost() },
      },
    },
  });
  expect(draft.status(), await draft.text()).toBe(200);
  const draftRevision = (await draft.json()).revision;
  const beforeProfile = await (
    await request.get("/api/household-profile")
  ).json();
  const beforeRules = await (await request.get("/api/rule-profile")).json();
  await enter(page);
  await expect(row(page, car.vehicleId)).toContainText("[85,00, 85,00]");
  await report(page);
  await page
    .getByRole("link", { name: "Tillbaka till jämförelsen", exact: true })
    .click();
  await page.getByRole("link", { name: "URL-analys", exact: true }).click();
  const summary = page
    .getByText("TAA106", { exact: true })
    .first()
    .locator("xpath=ancestor::li");
  await summary.getByRole("button", { name: "Öppna", exact: true }).click();
  await page
    .locator('[data-testid^="listing-card-"]')
    .getByRole("button", { name: "Radera bilen", exact: true })
    .click();
  const deleting = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === `/api/saved-listings/${car.vehicleId}` &&
      r.request().method() === "DELETE",
  );
  await page
    .getByRole("button", { name: "Radera bilen permanent", exact: true })
    .click();
  expect((await deleting).status()).toBe(204);
  for (const resource of [
    "vehicle-cost-inputs",
    "vehicle-facts",
    "saved-listings",
    "saved-cost-scenarios",
  ])
    expect(
      (await request.get(`/api/${resource}/${car.vehicleId}`)).status(),
      resource,
    ).toBe(404);
  const cleared = await (await request.get("/api/vehicle-draft")).json();
  expect(cleared.input).toBeNull();
  expect(cleared.revision).toBeGreaterThan(draftRevision);
  expect(
    (
      await request.post("/api/vehicle-draft/adopt", {
        data: { expectedRevision: draftRevision },
      })
    ).status(),
  ).toBe(409);
  expect(await (await request.get("/api/household-profile")).json()).toEqual(
    beforeProfile,
  );
  expect(await (await request.get("/api/rule-profile")).json()).toEqual(
    beforeRules,
  );
  await page.getByRole("link", { name: "Jämförelse", exact: true }).click();
  await expect(row(page, car.vehicleId)).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Öppna rapport", exact: true }),
  ).toBeDisabled();
  expect(
    (await (await request.get("/api/comparisons/baseline")).json())
      .candidateCount,
  ).toBe(0);
});
