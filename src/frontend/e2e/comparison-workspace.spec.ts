import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";

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
const cost = (price = 40000, residual = 20000) => ({
  candidateKey: "car",
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
const preferences = () => ({
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
  route: "household-profile" | "rule-profile",
  input: unknown,
) {
  const current = await request.get(`/api/${route}`);
  const expectedRevision =
    current.status() === 404 ? 0 : (await current.json()).revision;
  expect(
    (
      await request.put(`/api/${route}`, { data: { expectedRevision, input } })
    ).status(),
  ).toBe(200);
}
async function create(
  request: APIRequestContext,
  index = 0,
  input: unknown = cost(),
  edits: unknown = {
    transmission: { kind: "manual", manual: { value: "automatic" } },
  },
) {
  const created = await request.post("/api/vehicle-cost-inputs", {
    data: { registrationNumber: `CAA${100 + index}`, cost: { input } },
  });
  expect(created.status(), await created.text()).toBe(201);
  const car = await created.json();
  owned.push(car.vehicleId);
  const facts = await request.put(`/api/vehicle-facts/${car.vehicleId}`, {
    data: {
      expectedRevision: car.revision,
      input: { edits, costConfirmation: "confirm" },
    },
  });
  expect(facts.status(), await facts.text()).toBe(200);
  return { ...car, revision: (await facts.json()).revision };
}
const main = (page: Page) =>
  page.getByRole("table", { name: "Huvudjämförelse", exact: true });
const row = (page: Page, registration: string) =>
  main(page).getByRole("row").filter({ hasText: registration });
async function settled(page: Page) {
  await expect(
    page.getByText("Resultaten är inaktuella.", { exact: false }),
  ).not.toBeVisible();
}
test.beforeEach(async ({ request, page }) => {
  const b = await (await request.get("/api/comparisons/baseline")).json();
  previous = { profile: b.profile ?? {}, rules: b.rules ?? {} };
  await shared(request, "household-profile", profile());
  await shared(request, "rule-profile", preferences());
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

test("B1-B4: edits common priorities and facts with exact intervals, coverage and explicit saves", async ({
  request,
  page,
}, testInfo) => {
  const car = await create(request);
  await create(request, 1, cost(60000, 40000), {});
  const writes: string[] = [];
  page.on("request", (r) => {
    if (r.method() !== "GET" && !r.url().endsWith("/preview-all"))
      writes.push(r.url());
  });
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
  await expect(row(page, "CAA100")).toContainText("100,00 %");
  await page
    .getByRole("button", { name: "CAA100", exact: true })
    .first()
    .click();
  const gear = page.locator('[data-fact="transmission"]');
  await gear.locator("summary").click();
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("unknown");
  await expect(row(page, "CAA100")).toContainText("[45,00, 85,00]");
  await expect(row(page, "CAA100")).toContainText("60,00 %");
  expect(writes).toEqual([]);
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).click();
  const gearRule = page.locator('[data-criterion="transmission"]');
  await gearRule.locator("summary").click();
  await gearRule.getByLabel("Vikt (0–5)").fill("0");
  await expect(row(page, "CAA100")).toContainText("[75,00, 75,00]");
  const priceRule = page.locator('[data-criterion="purchasePriceSek"]');
  await priceRule.locator("summary").click();
  await priceRule.getByLabel("Vikt (0–5)").fill("0");
  await expect(row(page, "CAA100")).toContainText("Ingen poäng");
  await priceRule.getByLabel("Vikt (0–5)").fill("1");
  await gearRule.getByLabel("Vikt (0–5)").fill("4");
  await expect(row(page, "CAA100")).toContainText("[15,00, 95,00]");
  // A confirmed unwanted gearbox has a known zero contribution (B4).
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("manual");
  await gear.getByLabel("Värde", { exact: true }).selectOption("manual");
  await expect(row(page, "CAA100")).toContainText("[15,00, 15,00]");
  expect(writes).toEqual([]);
  await page
    .getByRole("button", { name: "Spara biluppgifter", exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (
          await (
            await request.get(`/api/vehicle-facts/${car.vehicleId}`)
          ).json()
        ).input.facts.transmission.observations[0].value,
    )
    .toBe("manual");
  await page
    .getByRole("button", {
      name: "Spara köpkrav och prioriteringar",
      exact: true,
    })
    .click();
  await expect
    .poll(
      async () =>
        (await (await request.get("/api/rule-profile")).json()).input
          .preferences[1].weight,
    )
    .toBe(4);
  await settled(page);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: testInfo.outputPath("comparison-desktop.png"),
  });
});

test("A2: ownership cost, financing and calendar remain separate, with focused economic links", async ({
  request,
  page,
}) => {
  await shared(request, "household-profile", {
    ...profile(),
    purchaseCashSek: 30000,
    loanTerms: {
      termMonths: 10,
      annualNominalInterestRatePercent: { single: 0 },
      setupFeeSek: 250,
      monthlyFeeSek: 50,
    },
  });
  const car = await create(request, 0, cost(80000, 60000));
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText(/20\s750,00/);
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  await expect(
    page.getByRole("table", { name: "Betalningar och budgetar", exact: true }),
  ).toContainText(/80\s750,00/);
  await row(page, "CAA100")
    .getByRole("link", { name: "Ekonomiskt underlag" })
    .click();
  await expect(page).toHaveURL(new RegExp(`vehicleId=${car.vehicleId}`));
  await expect(
    page.getByLabel("Inköpspris (kr)", { exact: true }),
  ).toBeFocused();
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("75000");
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(row(page, "CAA100")).toContainText(/15\s750,00/);
  expect(
    (
      await (
        await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)
      ).json()
    ).input.priceSek,
  ).toBe(80000);
});

test("101 cars share pagination and global scores; details never narrow the calculation", async ({
  request,
  page,
}) => {
  test.setTimeout(120000);
  for (let i = 0; i < 101; i++) await create(request, i);
  const bodies: Record<string, unknown>[] = [];
  page.on("request", (r) => {
    if (r.url().endsWith("/preview-all")) bodies.push(r.postDataJSON());
  });
  await page.goto("/search");
  await expect(main(page).getByRole("row")).toHaveCount(51);
  expect(bodies[0].overrides).toEqual([]);
  expect(bodies[0].candidates).toBeUndefined();
  await page.getByRole("button", { name: "Nästa sida" }).click();
  await expect(main(page).getByRole("row")).toHaveCount(51);
  await expect(main(page)).toContainText("CAA150");
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  await expect(
    page
      .getByRole("table", { name: "Känslighetsanalys", exact: true })
      .getByRole("row"),
  ).toHaveCount(51);
  await page.getByRole("button", { name: "Nästa sida" }).click();
  await expect(main(page).getByRole("row")).toHaveCount(2);
  await expect(main(page)).toContainText("CAA200");
  await expect(row(page, "CAA200")).toContainText("[85,00, 85,00]");
  expect(bodies).toHaveLength(1);
});

test("two browsers preserve local priorities until a changed baseline is explicitly reviewed", async ({
  request,
  page,
  browser,
  baseURL,
}) => {
  await create(request);
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).click();
  const price = page.locator('[data-criterion="purchasePriceSek"]');
  await price.locator("summary").click();
  await price.getByLabel("Vikt (0–5)").fill("1");
  const other = await browser.newContext({ baseURL });
  try {
    await shared(other.request, "household-profile", {
      ...profile(),
      purchaseCashSek: 99999,
    });
    await page
      .getByRole("button", { name: "Läs aktuellt serverunderlag" })
      .click();
    await expect(
      page.getByRole("heading", { name: "Serverunderlaget har ändrats" }),
    ).toBeVisible();
    await expect(price.getByLabel("Vikt (0–5)")).toHaveValue("1");
    await page
      .getByRole("button", { name: "Behåll granskade lokala ändringar" })
      .click();
    await expect(row(page, "CAA100")).toContainText("[91,67, 91,67]");
    await settled(page);
    expect(
      (await (await request.get("/api/rule-profile")).json()).input
        .preferences[0].weight,
    ).toBe(3);
  } finally {
    await other.close();
  }
});

test("explicit manual mode works without storage and retains economic edits through navigation", async ({
  page,
}, testInfo) => {
  const calls: string[] = [];
  await page.route("**/api/comparisons/baseline", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({ code: "comparisonStorageUnavailable" }),
    }),
  );
  await page.goto("/search");
  await page.getByLabel("Jämförelseläge").selectOption("manual");
  page.on("request", (r) => {
    if (r.url().includes("/api/") && !r.url().endsWith("/preview-all"))
      calls.push(r.url());
  });
  await page.getByRole("button", { name: "Lägg till manuell bil" }).click();
  await page.getByLabel("Registreringsnummer", { exact: true }).fill("CAA100");
  await page
    .getByRole("link", { name: "Redigera ekonomiskt underlag", exact: true })
    .click();
  await page
    .getByLabel("Inköpspris (kr)", { exact: true })
    .fill("123,1234567890123456789");
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(row(page, "CAA100")).toBeVisible();
  await settled(page);
  await page
    .getByRole("link", { name: "Redigera ekonomiskt underlag", exact: true })
    .click();
  await expect(page.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue(
    "123,1234567890123456789",
  );
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  expect(calls).toEqual([]);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: testInfo.outputPath("comparison-mobile.png") });
});

test("whole-car deletion removes facts and preserves both shared profiles", async ({
  request,
  page,
}) => {
  const car = await create(request);
  const before = await (await request.get("/api/comparisons/baseline")).json();
  await page.goto("/search");
  await expect(row(page, "CAA100")).toBeVisible();
  await page
    .getByRole("button", { name: "CAA100", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Radera bilen permanent" }).click();
  await expect(row(page, "CAA100")).toHaveCount(0);
  expect(
    (await request.get(`/api/vehicle-facts/${car.vehicleId}`)).status(),
  ).toBe(404);
  const after = await (await request.get("/api/comparisons/baseline")).json();
  expect(after.profile).toEqual(before.profile);
  expect(after.rules).toEqual(before.rules);
});

test("B5-B8: overlap, hard requirements, partial totals and fixed goals use the server's outcomes", async ({
  request,
  page,
}) => {
  const manual = (value: unknown) => ({ kind: "manual", manual: { value } });
  await shared(request, "rule-profile", {
    preferences: [
      preferences().preferences[0],
      { ...preferences().preferences[1], weight: 1 },
      {
        criterionKey: "serviceDocumentation",
        minimumEvidence: "userConfirmed",
        weight: 1,
        preferredValues: [{ serviceDocumentation: "documented" }],
      },
    ],
  });
  await create(request, 0, cost(40000), {});
  await create(request, 1, cost(20000, 0), {
    serviceDocumentation: manual("absent"),
  });
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[45,00, 85,00]");
  await expect(row(page, "CAA101")).toContainText("[60,00, 80,00]");
  await page.getByLabel("Sortering", { exact: true }).selectOption("score");
  await expect(main(page).getByRole("row").nth(1)).toContainText("CAA101");
  await expect(page.getByText(/Poängintervallen överlappar/)).toBeVisible();
  await expect(
    page.getByText("Säker preferensvinnare", { exact: true }),
  ).toHaveCount(0);
  // Independent fixed goals never normalize against the other cars (B8).
  await create(request, 2, cost(1000, 0), {
    transmission: manual("automatic"),
  });
  await page
    .getByRole("button", { name: "Läs aktuellt serverunderlag" })
    .click();
  await expect(row(page, "CAA100")).toContainText("[45,00, 85,00]");
  // B7's inclusive limits, unknown owners, and a confirmed missing tow bar.
  const car = await (await request.get("/api/vehicle-cost-inputs")).json();
  const b = car.find(
    (c: { registrationNumber: string }) => c.registrationNumber === "CAA101",
  );
  const written = await request.put(`/api/vehicle-facts/${b.vehicleId}`, {
    data: {
      expectedRevision: b.revision,
      input: {
        edits: { odometerKilometres: manual(200000), towBar: manual(false) },
      },
    },
  });
  expect(written.ok()).toBe(true);
  await shared(request, "rule-profile", {
    hardRules: [
      {
        criterionKey: "purchasePriceSek",
        operator: "inclusiveRange",
        maximum: 20000,
        minimumEvidence: "userConfirmed",
      },
      {
        criterionKey: "odometerKilometres",
        operator: "inclusiveRange",
        maximum: 200000,
        minimumEvidence: "userConfirmed",
      },
      {
        criterionKey: "ownerCount",
        operator: "inclusiveRange",
        maximum: 6,
        minimumEvidence: "userConfirmed",
      },
      {
        criterionKey: "towBar",
        operator: "equals",
        allowedValues: [{ boolean: true }],
        minimumEvidence: "userConfirmed",
      },
    ],
  });
  await page
    .getByRole("button", { name: "Läs aktuellt serverunderlag" })
    .click();
  await expect(row(page, "CAA101")).toContainText("Bortvald");
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  const details = page
    .getByRole("table", {
      name: "Krav, prioriteringar och källor",
      exact: true,
    })
    .getByRole("row")
    .filter({ hasText: "CAA101" });
  await expect(details).toContainText("Ägarantal");
  await expect(details).toContainText("Behöver verifieras");
  await expect(details).toContainText("Dragkrok");
  await details
    .getByRole("button", { name: "Granska Ägarantal", exact: true })
    .click();
  await expect(
    page.locator('[data-fact="ownerCount"]').getByLabel("Åtgärd för Ägarantal"),
  ).toBeFocused();
});

test("B6 cost order keeps incomplete and rejected alternatives while marking only the eligible cheapest", async ({
  request,
  page,
}) => {
  await shared(request, "rule-profile", {
    hardRules: [
      {
        criterionKey: "towBar",
        operator: "equals",
        allowedValues: [{ boolean: true }],
        minimumEvidence: "userConfirmed",
      },
    ],
  });
  const tow = (value: boolean) => ({
    towBar: { kind: "manual", manual: { value } },
  });
  await create(request, 0, cost(40000, 10000), tow(true));
  await create(request, 1, cost(40000, 15000), tow(true));
  await create(
    request,
    2,
    { ...cost(40000, 30000), insurance: null },
    tow(true),
  );
  await create(request, 3, cost(40000, 20000), tow(false));
  await page.goto("/search");
  await expect(main(page).getByRole("row")).toHaveCount(5);
  for (const [index, registration] of [
    "CAA101",
    "CAA100",
    "CAA102",
    "CAA103",
  ].entries())
    await expect(
      main(page)
        .getByRole("row")
        .nth(index + 1),
    ).toContainText(registration);
  await expect(row(page, "CAA101")).toContainText(
    "Billigast bland godkända kompletta alternativ",
  );
  await expect(row(page, "CAA102")).toContainText("känd del");
  await expect(row(page, "CAA103")).toContainText("Bortvald");
});

test("250 cars include an off-page preference winner and share every detail page", async ({
  request,
  page,
}) => {
  test.setTimeout(180000);
  for (let i = 0; i < 250; i++)
    await create(request, i, i === 249 ? cost(20000, 0) : cost(40000, 30000));
  await page.goto("/search");
  await expect(main(page).getByRole("row")).toHaveCount(51);
  await expect(main(page)).not.toContainText("CAA349");
  const recommendations = page.getByRole("region", {
    name: "Rekommendationer för hela beståndet",
  });
  await expect(recommendations).toContainText("CAA349: Säker preferensvinnare");
  await recommendations
    .getByRole("button", { name: "CAA349", exact: true })
    .click();
  await expect(
    page.getByText("Sida 5 av 5 · 50 bilar per sida", { exact: true }),
  ).toBeVisible();
  await expect(row(page, "CAA349")).toContainText("[100,00, 100,00]");
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  await expect(
    page
      .getByRole("table", { name: "Känslighetsanalys", exact: true })
      .getByRole("row"),
  ).toHaveCount(51);
});

test("A5-A8 and A11 retain independent energy, maintenance, accrual and strict budget results", async ({
  request,
  page,
}) => {
  const item = (
    key: string,
    amount: number,
    cadence = "once",
    monthOffset = 2,
    dueMonthOfYear?: number,
  ) => ({
    isIncluded: false,
    items: [
      {
        key,
        label: key,
        amountSek: { single: amount },
        cadence,
        monthOffset,
        dueMonthOfYear,
      },
    ],
  });
  await shared(request, "household-profile", {
    ...profile(),
    electricDrivingSharePercent: { single: 60 },
    homeChargingSharePercent: { single: 80 },
    homeChargingPricePerKilowattHourSek: { single: 2 },
    publicChargingPricePerKilowattHourSek: { single: 5 },
    chargingLossPercent: { single: 10 },
    energyPrices: [
      { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } },
      { fuel: "diesel", unit: "litre", pricePerUnitSek: { single: 0 } },
    ],
    monthlyBudgetSek: 200,
  });
  const sources = (
    basis: string,
    electric: number,
    petrol: number,
    electricityBasis: string,
  ) => [
    {
      key: "electric",
      fuel: "electricity",
      unit: "kilowattHour",
      consumptionBasis: basis,
      electricityBasis,
      consumptionPer100Kilometres: { single: electric },
    },
    {
      key: "petrol",
      fuel: "petrol",
      unit: "litre",
      consumptionBasis: basis,
      consumptionPer100Kilometres: { single: petrol },
    },
  ];
  await create(request, 0, {
    ...cost(0, 0),
    energySources: sources("drivingMode", 18, 6, "battery"),
  });
  await create(request, 1, {
    ...cost(0, 0),
    energySources: sources("wholeDistance", 10, 3, "metered"),
  });
  // Explicit zero-priced diesel keeps these payment examples independent of the hybrid's energy use.
  const free = {
    ...cost(0, 0),
    energySources: [{ ...cost().energySources[0], fuel: "diesel" }],
    residual: { mode: "annualPercentage", value: { single: 0 } },
  };
  await create(request, 2, {
    ...free,
    service: item("service", 1200),
    repairs: item("repair", 2400),
    additionalRepairAllowancePerMonthSek: {
      favorable: 100,
      baseline: 300,
      cautious: 500,
    },
  });
  await create(request, 3, {
    ...free,
    tax: item("tax", 1200, "annual", undefined, 3),
  });
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText(/9\s504,00/);
  await expect(row(page, "CAA101")).toContainText(/10\s320,00/);
  await expect(row(page, "CAA102")).toContainText(/7\s200,00/);
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  const sensitivity = page
    .getByRole("table", { name: "Känslighetsanalys", exact: true })
    .getByRole("row")
    .filter({ hasText: "CAA102" });
  await expect(sensitivity).toContainText(/4\s800,00/);
  await expect(sensitivity).toContainText(/9\s600,00/);
  const payments = page.getByRole("table", {
    name: "Betalningar och budgetar",
    exact: true,
  });
  await expect(
    payments
      .getByRole("row")
      .filter({ hasText: "CAA102" })
      .getByRole("cell")
      .first(),
  ).toContainText(/3\s600,00/);
  await page
    .getByText("Hushållsprofil – visa och redigera", { exact: true })
    .click();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("6");
  await expect(row(page, "CAA103")).toContainText("600,00");
  await expect(
    payments.getByRole("row").filter({ hasText: "CAA103" }),
  ).toContainText("Inom budget");
  await page.getByLabel("Löpande månadsbudget (kr)").fill("199");
  await expect(
    payments.getByRole("row").filter({ hasText: "CAA103" }),
  ).toContainText("Budgeten överskrids");
  await expect(row(page, "CAA100")).toContainText("känd del"); // Fixed residual retains its original horizon.
});

test("A9-A10 display leasing coverage and distinguish outflows, refunds and budgets", async ({
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
    ...cost(),
    acquisitionType: "lease",
    priceSek: null,
    residual: null,
    energySources: [],
    lease: {
      termMonths: 24,
      upfrontNonRefundableSek: 6000,
      refundableDepositSek: 3000,
      depositRefundSek: { single: 3000 },
      includedDistanceKilometres: 24000,
      excessDistancePricePerKilometreSek: { single: 1 },
      priceBasis: "quoted",
      energyIncluded: true,
      monthlyPayments: Array.from({ length: 24 }, (_, index) => ({
        monthOffset: index + 1,
        amountSek: 2000,
      })),
      endFees: [],
      otherPayments: [],
    },
  });
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText(/60\s000,00/);
  await page.getByRole("button", { name: "Öppna alla", exact: true }).click();
  const payments = page.getByRole("table", {
    name: "Betalningar och budgetar",
    exact: true,
  });
  await expect(payments).toContainText(/63\s000,00/);
  await expect(payments).toContainText(/3\s000,00/);
  await expect(payments).toContainText(/2\s250,00/);
  await page
    .getByText("Hushållsprofil – visa och redigera", { exact: true })
    .click();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("12");
  await expect(payments).toContainText(/33\s000,00/);
  await expect(row(page, "CAA100")).toContainText("känd del");
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("36");
  await expect(payments).toContainText(/63\s000,00/);
  await expect(payments).toContainText("Kan inte bedömas ännu");
});

test("economic saving advances the shared revision and clears old confirmation without losing fact edits", async ({
  request,
  page,
}) => {
  const car = await create(request);
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
  await row(page, "CAA100").getByRole("button", { name: "CAA100" }).click();
  const gear = page.locator('[data-fact="transmission"]');
  await gear.locator("summary").click();
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("unknown");
  let release!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    `**/api/vehicle-cost-inputs/${car.vehicleId}`,
    async (route) => {
      if (route.request().method() === "GET") await gate;
      await route.continue();
    },
  );
  try {
    await page
      .getByRole("link", { name: "Redigera ekonomiskt underlag", exact: true })
      .click();
    await expect(
      page.getByRole("heading", { name: "Öppnar bilens ekonomiska underlag" }),
    ).toBeVisible();
    await expect(
      page.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveCount(0);
  } finally {
    release();
  }
  await expect(page.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue(
    "40000",
  );
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("35000");
  await page
    .getByRole("button", { name: "Spara bilunderlag", exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (
          await (
            await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)
          ).json()
        ).input.priceSek,
    )
    .toBe(35000);
  await page.getByRole("link", { name: "Tillbaka till jämförelsen" }).click();
  await expect(row(page, "CAA100")).toContainText("[0,00, 100,00]");
  await settled(page);
  await expect(gear.getByLabel("Åtgärd för Växellåda")).toHaveValue("unknown");
  await page
    .getByRole("button", {
      name: "Bekräfta sparat kostnadsunderlag",
      exact: true,
    })
    .click();
  await expect(row(page, "CAA100")).toContainText("[48,75, 88,75]");
  await settled(page);
  expect(
    (await (await request.get(`/api/vehicle-facts/${car.vehicleId}`)).json())
      .input.facts.transmission.state,
  ).toBe("known");
});

test("listing adoption preserves source versions and requires explicit conflict resolution", async ({
  request,
  page,
}) => {
  const url = "https://cars.example/issue65";
  const emptyDraft = Object.fromEntries(
    "registrationNumber make model variant modelYear vin vehicleLabel priceSek odometerKilometres sellerType locality county publishedDate updatedDate imageCount fuelTypes transmission drivetrain bodyType colour horsepower engineDisplacementCubicCentimetres energyConsumptions annualVehicleTaxSek ownerCount firstRegistrationDate lastInspectionDate nextInspectionDate towBar equipment sellerClaims conditionNotes"
      .split(" ")
      .map((key) => [key, null]),
  );
  const listing = (transmission: string) => ({
    submittedUrl: url,
    analyzedAtUtc: "2026-09-08T12:00:00Z",
    requestedModel: "fixture",
    promptVersion: 2,
    schemaVersion: 2,
    sources: [url],
    draft: {
      ...emptyDraft,
      transmission: {
        value: transmission,
        provenance: {
          origin: "listing",
          extractionMethod: "ai",
          verification: "unverified",
          sourceUrl: url,
        },
      },
    },
  });
  const added = await request.post("/api/saved-listings", {
    data: { registrationNumber: "CAA100", listing: listing("manual") },
  });
  expect(added.status(), await added.text()).toBe(201);
  const car = await added.json();
  owned.push(car.vehicleId);
  const economic = await request.put(
    `/api/vehicle-cost-inputs/${car.vehicleId}`,
    { data: { expectedRevision: car.revision, cost: { input: cost() } } },
  );
  expect(economic.status(), await economic.text()).toBe(200);
  const initialFacts = await request.put(
    `/api/vehicle-facts/${car.vehicleId}`,
    {
      data: {
        expectedRevision: (await economic.json()).revision,
        input: {
          costConfirmation: "confirm",
          edits: {
            transmission: { kind: "manual", manual: { value: "automatic" } },
          },
        },
      },
    },
  );
  expect(initialFacts.status(), await initialFacts.text()).toBe(200);
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
  await row(page, "CAA100").getByRole("button").click();
  const gear = page.locator('[data-fact="transmission"]');
  await gear.locator("summary").click();
  await expect(gear).toContainText("Annonsförslag: Manuell");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]"); // Reading is not adoption.
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("listing");
  await expect(row(page, "CAA100")).toContainText("[45,00, 85,00]");
  await page
    .getByRole("button", { name: "Spara biluppgifter", exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (
          await (
            await request.get(`/api/vehicle-facts/${car.vehicleId}`)
          ).json()
        ).input.facts.transmission.observations[0].sourceListingVersion,
    )
    .toBe(1);
  const saved = await (
    await request.get(`/api/saved-listings/${car.vehicleId}`)
  ).json();
  const replaced = await request.put(`/api/saved-listings/${car.vehicleId}`, {
    data: { expectedRevision: saved.revision, listing: listing("automatic") },
  });
  expect(replaced.status(), await replaced.text()).toBe(200);
  await page
    .getByRole("button", { name: "Läs aktuellt serverunderlag" })
    .click();
  await expect(gear).toContainText("Annonsförslag: Automat");
  await expect(gear).toContainText("Annonsversion 1");
  await expect(gear).toContainText("Annonsversion 2");
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("conflict");
  await gear.getByLabel("Källa för observation 1").selectOption("current-0");
  await gear.getByLabel("Källa för observation 2").selectOption("listing");
  await page
    .getByRole("button", { name: "Spara biluppgifter", exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (
          await (
            await request.get(`/api/vehicle-facts/${car.vehicleId}`)
          ).json()
        ).input.facts.transmission.state,
    )
    .toBe("conflicting");
  await expect(gear.locator("summary")).toContainText("Motstridigt");
  await gear.getByLabel("Åtgärd för Växellåda").selectOption("resolve");
  await gear.getByLabel("Värde", { exact: true }).selectOption("automatic");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
});

test("keyboard errors and incomplete responses never publish a false recommendation", async ({
  request,
  page,
}) => {
  await create(request);
  await page.goto("/search");
  await expect(row(page, "CAA100")).toContainText("[85,00, 85,00]");
  await page.route("**/api/comparisons/preview-all", async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    delete body.views.cautious;
    await route.fulfill({ response, json: body });
  });
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).focus();
  await page.keyboard.press("Enter");
  await expect(
    page.getByText(/Ett komplett jämförelsesvar kunde inte läsas/),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Rekommendationer för hela beståndet" }),
  ).toHaveCount(0);
  await expect(row(page, "CAA100")).not.toContainText("Säker preferensvinnare");
  await page.unroute("**/api/comparisons/preview-all");
  await page.getByText("Köpkrav och prioriteringar", { exact: true }).focus();
  await page.keyboard.press("Enter");
  const price = page.locator('[data-criterion="purchasePriceSek"]');
  await price.locator("summary").focus();
  await page.keyboard.press("Enter");
  await price.getByLabel("Vikt (0–5)").fill("fel");
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
  await expect(page.getByRole("alert")).toBeFocused();
  await page.getByRole("alert").getByRole("button").first().click();
  await expect(price.getByLabel("Vikt (0–5)")).toBeFocused();
});
