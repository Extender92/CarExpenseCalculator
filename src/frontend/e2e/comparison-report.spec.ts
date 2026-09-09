import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";
import { mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const owned: string[] = [];
let previous: { profile: unknown; rules: unknown };
const zero = () => ({ isIncluded: false, items: [] });
const profile = () => ({
  periodMonths: 12,
  startMonth: { year: 2026, month: 1 },
  annualDistanceKilometres: 12000,
  purchaseCashSek: 100000,
  activeSensitivityMode: "baseline",
  energyPrices: [
    { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 0 } },
  ],
});
const cost = (price = 40000) => ({
  candidateKey: "report",
  acquisitionType: "purchase",
  priceSek: price,
  residual: { mode: "fixedAmount", value: { single: 20000 }, periodMonths: 12 },
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
const rules = () => ({
  preferences: [
    {
      criterionKey: "purchasePriceSek",
      minimumEvidence: "userConfirmed",
      weight: 3,
      zeroPoint: 100000,
      fullPoint: 20000,
    },
    {
      criterionKey: "transmission",
      minimumEvidence: "userConfirmed",
      weight: 2,
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
  index = 0,
  input: unknown = cost(),
  confirmed = true,
) {
  const created = await request.post("/api/vehicle-cost-inputs", {
    data: { registrationNumber: `PAA${100 + index}`, cost: { input } },
  });
  expect(created.status(), await created.text()).toBe(201);
  const car = await created.json();
  owned.push(car.vehicleId);
  if (confirmed) {
    const saved = await request.put(`/api/vehicle-facts/${car.vehicleId}`, {
      data: {
        expectedRevision: car.revision,
        input: {
          costConfirmation: "confirm",
          edits: {
            transmission: { kind: "manual", manual: { value: "automatic" } },
            serviceNotes: {
              kind: "manual",
              manual: { value: "Årlig översyn hos Ängens verkstad." },
            },
          },
        },
      },
    });
    expect(saved.status(), await saved.text()).toBe(200);
  }
  return car;
}
const main = (page: Page) =>
  page.getByRole("table", { name: "Huvudjämförelse", exact: true });
async function openReport(page: Page) {
  const button = page.getByRole("button", {
    name: "Öppna rapport",
    exact: true,
  });
  await expect(button).toBeEnabled({ timeout: 30000 });
  await button.click();
  await expect(page).toHaveURL(/\/search\/report$/);
  await expect(
    page.getByRole("button", { name: "Skriv ut / Spara som PDF" }),
  ).toBeEnabled({ timeout: 30000 });
}
test.beforeEach(async ({ request, page }) => {
  const baseline = await (
    await request.get("/api/comparisons/baseline")
  ).json();
  previous = { profile: baseline.profile ?? {}, rules: baseline.rules ?? {} };
  await shared(request, "household-profile", profile());
  await shared(request, "rule-profile", rules());
  page.on("dialog", (dialog) => void dialog.accept());
});
test.afterEach(async ({ request, page }) => {
  await page.goto("about:blank");
  try {
    for (const id of owned) {
      const current = await request.get(`/api/vehicle-cost-inputs/${id}`);
      if (current.ok())
        expect(
          (
            await request.delete(
              `/api/vehicle-cost-inputs/${id}?expectedRevision=${(await current.json()).revision}`,
            )
          ).status(),
        ).toBe(204);
      else expect(current.status()).toBe(404);
    }
  } finally {
    owned.length = 0;
    await shared(request, "household-profile", previous.profile);
    await shared(request, "rule-profile", previous.rules);
  }
});

test("B1/B2: exports a frozen full/partial report, sources and dirty assumptions without print-triggered requests", async ({
  request,
  page,
}) => {
  await create(request);
  const partial = await create(request, 1, { ...cost(), residual: null });
  const latest = await (
    await request.get(`/api/vehicle-facts/${partial.vehicleId}`)
  ).json();
  expect(
    (
      await request.put(`/api/vehicle-facts/${partial.vehicleId}`, {
        data: {
          expectedRevision: latest.revision,
          input: { edits: { transmission: { kind: "unknown" } } },
        },
      })
    ).status(),
  ).toBe(200);
  await page.goto("/search");
  await expect(main(page)).toContainText("[85,00, 85,00]");
  await page
    .getByText("Hushållsprofil – visa och redigera", { exact: true })
    .click();
  await page
    .locator('[data-field-path="profile.purchaseCashSek"]')
    .fill("100001,1234567890123456789");
  await openReport(page);
  await expect(main(page)).toContainText("[85,00, 85,00]");
  await expect(main(page)).toContainText("[45,00, 85,00]");
  await expect(main(page)).toContainText("känd del");
  await expect(page.getByRole("article")).toContainText(
    "Årlig översyn hos Ängens verkstad.",
  );
  await expect(page.getByRole("article")).toContainText(
    "100001,1234567890123456789",
  );
  await expect(page.getByRole("article")).toContainText(
    "Osparade antaganden ingår",
  );
  const before = await page.getByRole("article").innerText();
  const calls: string[] = [];
  page.on("request", (r) => {
    if (new URL(r.url()).pathname.startsWith("/api/")) calls.push(r.url());
  });
  // A second client changes the server. The already captured report stays intact.
  await shared(request, "rule-profile", {});
  await page.evaluate(() => {
    window.print = () => window.dispatchEvent(new Event("afterprint"));
    window.dispatchEvent(new Event("focus"));
  });
  await page.getByRole("button", { name: "Skriv ut / Spara som PDF" }).click();
  await page.getByRole("button", { name: "Skriv ut / Spara som PDF" }).click();
  await page.waitForTimeout(650);
  expect(calls).toEqual([]);
  expect(await page.getByRole("article").innerText()).toBe(before);
  const directory = await mkdtemp(join(tmpdir(), "car-report-66-"));
  try {
    const pdf = await page.pdf({ preferCSSPageSize: true });
    await writeFile(join(directory, "comparison.pdf"), pdf);
    expect(pdf.byteLength).toBeGreaterThan(20000);
    // Actual page/glyph/fragmentation inspection complements this transport check.
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(
    page.locator('[data-field-path="profile.purchaseCashSek"]'),
  ).toHaveValue("100001,1234567890123456789");
  await expect(
    page.getByText("Serverunderlaget har ändrats.", { exact: false }),
  ).toBeVisible();
});

test("all 250 cars and collapsed details are exported from page two using the server's full order", async ({
  request,
  page,
}) => {
  test.setTimeout(180000);
  for (let i = 0; i < 250; i++)
    await create(
      request,
      i,
      {
        candidateKey: "partial",
        acquisitionType: "purchase",
        priceSek: 40000 + i,
      },
      false,
    );
  await page.goto("/search");
  await expect(main(page).locator("tbody tr")).toHaveCount(50, {
    timeout: 30000,
  });
  await page.getByRole("button", { name: "Nästa sida", exact: true }).click();
  let costOrder: string[] = [];
  // Read through APIRequestContext: Chromium's inspector evicts very large
  // response bodies even though the application receives and parses them.
  await page.route("**/api/comparisons/preview-all", async (route) => {
    const upstream = await route.fetch();
    costOrder = (await upstream.json()).views.baseline.costOrder;
    await route.fulfill({ response: upstream });
  });
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
  await openReport(page);
  await expect(main(page).locator("tbody tr")).toHaveCount(250);
  await expect(page.locator("[data-report-details]")).toHaveCount(250);
  expect(
    await main(page)
      .locator("[data-report-vehicle]")
      .evaluateAll((rows) =>
        rows.map((r) => r.getAttribute("data-report-vehicle")),
      ),
  ).toEqual(costOrder);
  await expect(
    page.getByRole("table", {
      name: "PAA349 – Fordonsfakta och källor",
      exact: true,
    }),
  ).toBeAttached();
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(page).toHaveURL(/\/search$/, { timeout: 15000 });
  await expect(
    page.getByRole("heading", { name: "Jämförelse", exact: true }),
  ).toBeVisible({ timeout: 30000 });
  await expect(page.getByText("Sida 2 av 5", { exact: false })).toBeVisible();
});

test("report works in explicit manual mode without storage and remains usable at 390px", async ({
  page,
}) => {
  await page.route("**/api/comparisons/baseline", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/problem+json",
      body: '{"code":"storageUnavailable"}',
    }),
  );
  await page.goto("/search");
  await page
    .getByLabel("Jämförelseläge", { exact: true })
    .selectOption("manual");
  await page.getByRole("button", { name: "Lägg till manuell bil" }).click();
  await page.getByLabel("Registreringsnummer", { exact: true }).fill("PDF123");
  await openReport(page);
  await expect(main(page)).toContainText("PDF123");
  await expect(page.getByRole("article")).toContainText(
    "Fristående manuellt underlag utan kontroll mot lagringen",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await expect(
    page.getByRole("heading", { name: "Förhandsvisning inför PDF" }),
  ).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("button", { name: "Skriv ut / Spara som PDF" }),
  ).toBeFocused();
  const storage = await page.evaluate(() => ({
    local: { ...localStorage },
    session: { ...sessionStorage },
  }));
  expect(JSON.stringify(storage)).not.toContain("PDF123");
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Rapportunderlaget finns inte kvar" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Tillbaka till jämförelsen" }),
  ).toBeVisible();
});

test("observed full deletion invalidates the report instead of changing its ranked membership", async ({
  request,
  page,
}) => {
  const car = await create(request);
  await page.goto("/search");
  await openReport(page);
  const saved = await (
    await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)
  ).json();
  expect(
    (
      await request.delete(
        `/api/vehicle-cost-inputs/${car.vehicleId}?expectedRevision=${saved.revision}`,
      )
    ).status(),
  ).toBe(204);
  await page.evaluate(
    (id) =>
      window.dispatchEvent(new CustomEvent("vehicle-deleted", { detail: id })),
    car.vehicleId,
  );
  await expect(
    page.getByRole("heading", { name: "Rapportunderlaget finns inte kvar" }),
  ).toBeVisible();
  await expect(
    page.getByText("En bil i rapporten har raderats.", { exact: false }),
  ).toBeVisible();
  await expect(page.getByRole("article")).toHaveCount(0);
});

test("a malformed or stale next generation cannot be exported; direct report navigation has no stored snapshot", async ({
  request,
  page,
}) => {
  await create(request);
  await page.goto("/search/report");
  await expect(
    page.getByRole("heading", { name: "Rapportunderlaget finns inte kvar" }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(
    page.getByRole("button", { name: "Öppna rapport", exact: true }),
  ).toBeEnabled();
  await page.route("**/api/comparisons/preview-all", async (route) => {
    const upstream = await route.fetch();
    const value = await upstream.json();
    delete value.views.cautious;
    await route.fulfill({ response: upstream, json: value });
  });
  try {
    await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
    // Busy state alone is insufficient: wait until the malformed response was
    // actually read and rejected before checking that export stays blocked.
    await expect(
      page.getByText(/Ett komplett jämförelsesvar kunde inte läsas/),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Öppna rapport", exact: true }),
    ).toBeDisabled();
    await expect(
      page.getByText("Resultaten är inaktuella.", { exact: false }),
    ).toBeVisible();
  } finally {
    await page.unrouteAll({ behavior: "wait" });
  }
});

test("A2/A8: report separates ownership cost, cash outflow and internal repair saving", async ({
  request,
  page,
}) => {
  await shared(request, "household-profile", {
    ...profile(),
    annualDistanceKilometres: 0,
    purchaseCashSek: 30000,
    loanTerms: {
      termMonths: 10,
      annualNominalInterestRatePercent: { single: 0 },
      setupFeeSek: 500,
      monthlyFeeSek: 25,
    },
  });
  await create(request, 0, {
    ...cost(80000),
    energySources: [],
    residual: {
      mode: "fixedAmount",
      value: { single: 60000 },
      periodMonths: 12,
    },
  });
  await create(request, 1, {
    ...cost(0),
    energySources: [],
    residual: { mode: "fixedAmount", value: { single: 0 }, periodMonths: 12 },
    service: {
      isIncluded: false,
      items: [
        {
          key: "service",
          label: "Årlig service",
          amountSek: { single: 1200 },
          cadence: "annual",
          dueMonthOfYear: 3,
        },
      ],
    },
    repairs: {
      isIncluded: false,
      items: [
        {
          key: "repair",
          label: "Känd reparation",
          amountSek: { single: 2400 },
          cadence: "once",
          monthOffset: 6,
        },
      ],
    },
    additionalRepairAllowancePerMonthSek: { single: 300 },
  });
  await page.goto("/search");
  await openReport(page);
  await expect(main(page)).toContainText("20 750,00 kr");
  await expect(main(page)).toContainText("7 200,00 kr");
  const payment = page.getByRole("table", {
    name: "PAA100 – Betalningsunderlag",
    exact: true,
  });
  await expect(
    payment.locator('[data-report-path="externalOutflow"]'),
  ).toContainText("80 750,00 kr");
  const repairs = page.getByRole("table", {
    name: "PAA101 – Betalningsunderlag",
    exact: true,
  });
  await expect(
    repairs.locator('[data-report-path="externalOutflow"]'),
  ).toContainText("3 600,00 kr");
  await expect(
    repairs.locator('[data-report-path="internalSaving"]'),
  ).toContainText("3 600,00 kr");
  const calendar = page.getByRole("table", {
    name: "PAA100 – Betalningskalender",
    exact: true,
  });
  await expect(
    calendar.locator('[data-report-month="11"]').getByRole("cell").first(),
  ).toHaveText("0,00 kr");
});

test("A5/A6: prints hybrid energy amounts without weighting or charging-loss recalculation", async ({
  request,
  page,
}) => {
  await shared(request, "household-profile", {
    ...profile(),
    energyPrices: [
      { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } },
    ],
    electricDrivingSharePercent: { single: 60 },
    homeChargingSharePercent: { single: 80 },
    chargingLossPercent: { single: 10 },
    homeChargingPricePerKilowattHourSek: { single: 2 },
    publicChargingPricePerKilowattHourSek: { single: 5 },
  });
  for (const whole of [false, true])
    await create(request, whole ? 1 : 0, {
      ...cost(),
      energySources: [
        {
          key: "electric",
          fuel: "electricity",
          unit: "kilowattHour",
          consumptionBasis: whole ? "wholeDistance" : "drivingMode",
          electricityBasis: whole ? "metered" : "battery",
          consumptionPer100Kilometres: { single: whole ? 10 : 18 },
        },
        {
          key: "petrol",
          fuel: "petrol",
          unit: "litre",
          consumptionBasis: whole ? "wholeDistance" : "drivingMode",
          consumptionPer100Kilometres: { single: whole ? 3 : 6 },
        },
      ],
    });
  await page.goto("/search");
  await openReport(page);
  await expect(
    page
      .getByRole("table", {
        name: "PAA100 – Energi, skatt och försäkring",
        exact: true,
      })
      .locator('[data-report-path="energy.cost"]'),
  ).toContainText("9 504,00 kr");
  await expect(
    page
      .getByRole("table", {
        name: "PAA101 – Energi, skatt och försäkring",
        exact: true,
      })
      .locator('[data-report-path="energy.cost"]'),
  ).toContainText("10 320,00 kr");
});

test("A9: prints the lease deposit, refund and authoritative startup/average budgets separately", async ({
  request,
  page,
}) => {
  await shared(request, "household-profile", {
    ...profile(),
    periodMonths: 24,
    annualDistanceKilometres: 15000,
    startupBudgetSek: 9000,
    monthlyBudgetSek: 2250,
  });
  await create(request, 0, {
    candidateKey: "lease",
    acquisitionType: "lease",
    lease: {
      termMonths: 24,
      upfrontNonRefundableSek: 6000,
      refundableDepositSek: 3000,
      depositRefundSek: { single: 3000 },
      includedDistanceKilometres: 24000,
      excessDistancePricePerKilometreSek: { single: 1 },
      priceBasis: "quoted",
      energyIncluded: true,
      monthlyPayments: Array.from({ length: 24 }, (_, i) => ({
        monthOffset: i + 1,
        amountSek: 2000,
      })),
      endFees: [],
      otherPayments: [],
    },
    tax: zero(),
    insurance: zero(),
    service: zero(),
    repairs: zero(),
    customCosts: zero(),
    additionalRepairAllowancePerMonthSek: { single: 0 },
  });
  await page.goto("/search");
  await openReport(page);
  await expect(main(page)).toContainText("60 000,00 kr");
  const p = page.getByRole("table", {
    name: "PAA100 – Betalningsunderlag",
    exact: true,
  });
  await expect(p.locator('[data-report-path="externalOutflow"]')).toContainText(
    "63 000,00 kr",
  );
  await expect(p.locator('[data-report-path="externalInflow"]')).toContainText(
    "3 000,00 kr",
  );
  const budgets = page.getByRole("table", {
    name: "PAA100 – Kostnadsavstämning och budgetar",
    exact: true,
  });
  await expect(
    budgets.locator('[data-report-path="startupBudget.fundingRequired"]'),
  ).toContainText("9 000,00 kr");
  await expect(
    budgets.locator('[data-report-path="monthlyBudget.fundingRequired"]'),
  ).toContainText("2 250,00 kr");
  await expect(
    budgets.locator('[data-report-path="monthlyBudget.status"]'),
  ).toContainText("Inom budget");
});
