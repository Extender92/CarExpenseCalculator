import { execFileSync } from "node:child_process";
import {
  expect,
  test,
  type APIRequestContext,
  type Locator,
  type Page,
} from "@playwright/test";
import type { components } from "../src/api/schema";

type Schema<K extends keyof components["schemas"]> = components["schemas"][K];
type Profile = Schema<"HouseholdProfileInput">;
type Vehicle = Schema<"VehicleCostInput">;
type SavedVehicle = Schema<"VehicleCostInputResponse">;
type Preview = Schema<"HouseholdPreviewResponse">;
const owned = new Set<string>();
const zero = () => ({ isIncluded: false, items: [] });
const profile = (overrides: Profile = {}): Profile => ({
  startMonth: { year: 2026, month: 1 },
  periodMonths: 12,
  annualDistanceKilometres: 0,
  purchaseCashSek: 100000,
  activeSensitivityMode: "baseline",
  energyPrices: [
    { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } },
  ],
  loanTerms: {
    termMonths: 10,
    annualNominalInterestRatePercent: { single: 0 },
    setupFeeSek: 0,
    monthlyFeeSek: 0,
  },
  ...overrides,
});
const purchase = (overrides: Partial<Vehicle> = {}): Vehicle => ({
  candidateKey: "fictional",
  acquisitionType: "purchase",
  priceSek: 0,
  residual: { mode: "fixedAmount", value: { single: 0 }, periodMonths: 12 },
  energySources: [],
  tax: zero(),
  insurance: zero(),
  service: zero(),
  repairs: zero(),
  customCosts: zero(),
  additionalRepairAllowancePerMonthSek: { single: 0 },
  ...overrides,
});
const cost = (
  key: string,
  amount: number,
  cadence: Schema<"HouseholdCostCadence">,
  monthOffset?: number,
  dueMonthOfYear?: number,
) => ({
  isIncluded: false,
  items: [
    {
      key,
      label: `Fiktiv ${key}`,
      amountSek: { single: amount },
      cadence,
      monthOffset,
      dueMonthOfYear,
    },
  ],
});
const resultRegion = (page: Page) =>
  page.getByRole("region", { name: "Beräkningsresultat för vald bil" });

test.beforeEach(async ({ request }) => {
  await clearDraft(request);
  await saveProfile(request, profile());
});
test.afterEach(async ({ request }) => {
  // Track registrations before writes so cleanup also covers a failed assertion.
  const failures: string[] = [];
  try {
    const response = await request.get("/api/vehicle-cost-inputs");
    expect(response.ok()).toBe(true);
    for (const car of (await response.json()) as Schema<"VehicleCostInputSummary">[]) {
      if (!owned.has(car.registrationNumber)) continue;
      const deleted = await request.delete(
        `/api/vehicle-cost-inputs/${car.vehicleId}?expectedRevision=${car.revision}`,
      );
      if (deleted.status() !== 204)
        failures.push(`${car.registrationNumber}: ${deleted.status()}`);
    }
  } finally {
    owned.clear();
    await clearDraft(request);
  }
  expect(failures).toEqual([]);
});

test("A1 and A2 allocate cash per alternative and reconcile the exact ownership cost with payments", async ({
  page,
  request,
}) => {
  await saveProfile(
    request,
    profile({
      purchaseCashSek: 30000,
      startupBudgetSek: 500,
      loanTerms: {
        termMonths: 10,
        annualNominalInterestRatePercent: { single: 0 },
        setupFeeSek: 500,
        monthlyFeeSek: 25,
      },
    }),
  );
  const cars = [];
  for (const [index, price] of [25000, 30000, 80000].entries())
    cars.push(
      await create(
        request,
        `SAA00${index + 1}`,
        purchase({
          priceSek: price,
          residual: {
            mode: "fixedAmount",
            value: { single: price === 80000 ? 60000 : price },
            periodMonths: 12,
          },
        }),
      ),
    );
  await open(page, cars[2]);
  const preview = await calculate(page);
  expect(preview.calculationVersion).toBe(2);
  expect(preview.resultSchemaVersion).toBe(2);
  for (const [index, car] of cars.entries()) {
    const allocation = sections(preview, car).financingDetails!.allocation!;
    expect(allocation.cashAppliedSek).toBe([25000, 30000, 30000][index]);
    expect(allocation.principalSek).toBe([0, 0, 50000][index]);
    expect(allocation.unusedPurchaseCashSek).toBe([5000, 0, 0][index]);
  }
  const s = sections(preview, cars[2]);
  expect(s.totals.ownershipCost.completeTotalSek).toBe(20750);
  expect(s.payments.externalOutflow.completeTotalSek).toBe(80750);
  expect(s.reconciliation.reconciledOwnershipCost.completeTotalSek).toBe(20750);
  expect(s.financingDetails!.loan!.principalRepaidSek).toBe(50000);
  expect(
    s.payments.months.slice(11).map((month) => month.outflow.completeTotalSek),
  ).toEqual([0, 0]);
  await money(resultRegion(page), "Ägandekostnad för perioden", "20 750,00");
  await expandResults(page);
  await money(resultRegion(page), "Externa utbetalningar", "80 750,00");
  await money(resultRegion(page), "Kvarvarande skuld", "0,00");
  const calendar = page.getByRole("table", { name: "Betalningar per månad" });
  await expect(
    calendar
      .getByRole("row")
      .filter({ hasText: "2026-11" })
      .getByRole("cell")
      .first(),
  ).toHaveText(/0,00\s*kr/);
  await page.getByLabel("Kontanter till bilköpet (kr)").fill("10000");
  const changed = await calculate(page);
  for (const [index, car] of cars.entries())
    expect(
      sections(changed, car).financingDetails!.allocation!.principalSek,
    ).toBe([15000, 20000, 70000][index]);
});

test("positive-rate financing matches an independent reference after HTTP and Swedish presentation", async ({
  page,
  request,
}) => {
  await saveProfile(
    request,
    profile({
      purchaseCashSek: 0,
      loanTerms: {
        termMonths: 24,
        annualNominalInterestRatePercent: { single: 6 },
        setupFeeSek: 0,
        monthlyFeeSek: 0,
      },
    }),
  );
  const car = await create(request, "SAA004", purchase({ priceSek: 50000 }));
  await open(page, car);
  const s = sections(await calculate(page), car);
  // Closed-form 50-digit reference is independent of the production installment loop.
  expect(s.financingDetails!.loan).toMatchObject({
    monthlyInstallmentSek: 2216.03,
    principalRepaidSek: 24252.09,
    remainingPrincipalSek: 25747.91,
    interestPaidSek: 2340.27,
    paymentsDuringPeriodSek: 26592.37,
  });
  expect(s.totals.endEquity.completeTotalSek).toBe(-25747.91);
  await expandResults(page);
  await money(resultRegion(page), "Kvarvarande skuld", "25 747,91");
  await money(
    resultRegion(page),
    "Lånebetalning per månad, utan avgift",
    "2 216,03",
  );
});

test("A3 and A4 preserve fixed horizons while percentage residuals follow profile edits", async ({
  page,
  request,
}) => {
  const percent = await create(
    request,
    "SAA005",
    purchase({
      priceSek: 100000,
      residual: { mode: "annualPercentage", value: { single: 10 } },
    }),
  );
  const fixed = await create(
    request,
    "SAA006",
    purchase({
      priceSek: 80000,
      residual: {
        mode: "fixedAmount",
        value: { single: 60000 },
        periodMonths: 24,
      },
      insurance: cost("insurance", 100, "monthly"),
    }),
  );
  await open(page, percent);
  for (const [months, residual, displayed] of [
    [12, 90000, "90 000,00"],
    [24, 81000, "81 000,00"],
    [6, 94868.33, "94 868,33"],
  ] as const) {
    await page.getByLabel("Ägandeperiod (månader, 1–120)").fill(String(months));
    expect(
      sections(await calculate(page), percent).depreciation.residualValueSek,
    ).toBe(residual);
    await expandResults(page);
    await money(resultRegion(page), "Restvärde", displayed);
  }
  await page.getByRole("button", { name: "Öppna SAA006", exact: true }).click();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("36");
  const mismatch = sections(await calculate(page), fixed);
  expect(mismatch.totals.ownershipCost.completeTotalSek).toBeNull();
  expect(mismatch.depreciation.cost.errors).toEqual(
    expect.arrayContaining([
      expect.objectContaining({ code: "residualHorizonMismatch" }),
    ]),
  );
  expect(mismatch.insurance.cost.completeTotalSek).toBe(3600);
  await expect(
    page.getByLabel("Period för fast restvärde (månader)"),
  ).toHaveValue("24");
  await expect(
    resultRegion(page)
      .getByText(/Fast restvärde gäller en annan/)
      .first(),
  ).toBeVisible();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("24");
  expect(
    sections(await calculate(page), fixed).totals.ownershipCost
      .completeTotalSek,
  ).toBe(22400);
});

test("A5 and A6 use the stated energy bases and keep all candidates coherent through unsaved profile edits", async ({
  page,
  request,
}) => {
  await saveProfile(
    request,
    profile({
      annualDistanceKilometres: 12000,
      electricDrivingSharePercent: { single: 60 },
      homeChargingSharePercent: { single: 80 },
      homeChargingPricePerKilowattHourSek: { single: 2 },
      publicChargingPricePerKilowattHourSek: { single: 5 },
      chargingLossPercent: { single: 10 },
    }),
  );
  const hybrid = await create(
    request,
    "SAA007",
    purchase({
      energySources: [
        {
          key: "electric",
          fuel: "electricity",
          unit: "kilowattHour",
          consumptionBasis: "drivingMode",
          electricityBasis: "battery",
          consumptionPer100Kilometres: { single: 18 },
        },
        {
          key: "petrol",
          fuel: "petrol",
          unit: "litre",
          consumptionBasis: "drivingMode",
          consumptionPer100Kilometres: { single: 6 },
        },
      ],
    }),
  );
  const whole = await create(
    request,
    "SAA008",
    purchase({
      energySources: [
        {
          key: "electric",
          fuel: "electricity",
          unit: "kilowattHour",
          consumptionBasis: "wholeDistance",
          electricityBasis: "metered",
          consumptionPer100Kilometres: { single: 10 },
        },
        {
          key: "petrol",
          fuel: "petrol",
          unit: "litre",
          consumptionBasis: "wholeDistance",
          consumptionPer100Kilometres: { single: 3 },
        },
      ],
    }),
  );
  const battery = await create(
    request,
    "SAA009",
    purchase({
      energySources: [
        {
          key: "electric",
          fuel: "electricity",
          unit: "kilowattHour",
          consumptionBasis: "wholeDistance",
          electricityBasis: "metered",
          consumptionPer100Kilometres: { single: 20 },
        },
      ],
      additionalRepairAllowancePerMonthSek: {
        favorable: 100,
        baseline: 200,
        cautious: 300,
      },
    }),
  );
  const partial = await create(
    request,
    "SAA010",
    purchase({ insurance: null, tax: cost("tax", 100, "monthly") }),
  );
  await open(page, hybrid);
  let preview = await calculate(page);
  expect(sections(preview, hybrid).energy.sources).toEqual(
    expect.arrayContaining([
      expect.objectContaining({
        baseQuantity: 1296,
        purchasedQuantity: 1440,
        cost: expect.objectContaining({ completeTotalSek: 3744 }),
      }),
      expect.objectContaining({
        purchasedQuantity: 288,
        cost: expect.objectContaining({ completeTotalSek: 5760 }),
      }),
    ]),
  );
  expect(sections(preview, hybrid).energy.cost.completeTotalSek).toBe(9504);
  expect(sections(preview, whole).energy.cost.completeTotalSek).toBe(10320);
  await money(resultRegion(page), "Ägandekostnad för perioden", "9 504,00");
  await expect(
    page.getByRole("article").filter({ hasText: "SAA008" }),
  ).toContainText(/10\s320,00/);
  await page.getByLabel("Årlig körsträcka (mil)").fill("2400");
  preview = await calculate(page);
  expect(sections(preview, hybrid).energy.cost.completeTotalSek).toBe(19008);
  expect(sections(preview, whole).energy.cost.completeTotalSek).toBe(20640);
  // An energy-price edit changes both bases, without saving the shared profile.
  await page
    .locator(
      '[data-field-path="profile.homeChargingPricePerKilowattHourSek.single"]',
    )
    .fill("3");
  preview = await calculate(page);
  expect(sections(preview, hybrid).energy.cost.completeTotalSek).toBe(21312);
  expect(sections(preview, whole).energy.cost.completeTotalSek).toBe(22560);
  for (const [mode, reserve] of [
    ["favorable", 1200],
    ["baseline", 2400],
    ["cautious", 3600],
  ] as const) {
    await page.getByLabel("Aktivt osäkerhetsläge").selectOption(mode);
    preview = await calculate(page);
    expect(preview.activeSensitivityMode).toBe(mode);
    expect(sections(preview, battery).repairAllowance.completeTotalSek).toBe(
      reserve,
    );
    expect(
      sections(preview, partial).totals.ownershipCost.completeTotalSek,
    ).toBeNull();
    expect(sections(preview, partial).tax.cost.completeTotalSek).toBe(1200);
  }
  const incomplete = await request.put(
    `/api/vehicle-cost-inputs/${battery.vehicleId}`,
    {
      data: {
        expectedRevision: battery.revision,
        cost: {
          input: {
            ...battery.input,
              additionalRepairAllowancePerMonthSek: null,
          },
        },
      },
    },
  );
  expect(incomplete.status(), await incomplete.text()).toBe(200);
  await page
    .getByRole("button", { name: "Uppdatera serverläget", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Uppdatera serverläget", exact: true }),
  ).toBeVisible();
  preview = await calculate(page);
  expect(
    sections(preview, battery).repairAllowance.completeTotalSek,
  ).toBeNull();
  expect(sections(preview, battery).energy.cost.completeTotalSek).toBe(16320);
  expect(
    sections(preview, battery).totals.ownershipCost.completeTotalSek,
  ).toBeNull();
  await page.getByLabel("Årlig körsträcka (mil)").fill("0");
  preview = await calculate(page);
  expect(sections(preview, hybrid).energy.cost.completeTotalSek).toBe(0);
  expect(
    sections(preview, hybrid).totals.costPerMil.missingComponents,
  ).toContain("zeroDistance");
  const saved = await (await request.get("/api/household-profile")).json();
  expect(saved.input.annualDistanceKilometres).toBe(12000);
  expect(saved.input.activeSensitivityMode).toBe("baseline");
  expect(saved.input.homeChargingPricePerKilowattHourSek.single).toBe(2);
});

test("A7, A8 and A11 separate accrual, annual bills, repair saving and strict average-budget limits", async ({
  page,
  request,
}) => {
  await saveProfile(
    request,
    profile({ periodMonths: 6, monthlyBudgetSek: 200, startupBudgetSek: 500 }),
  );
  const tax = await create(
    request,
    "SAA011",
    purchase({
      residual: { mode: "annualPercentage", value: { single: 0 } },
      tax: cost("tax", 1200, "annual", undefined, 3),
    }),
  );
  const repair = await create(
    request,
    "SAA012",
    purchase({
      service: cost("service", 1200, "once", 4),
      repairs: cost("repair", 2400, "once", 2),
      additionalRepairAllowancePerMonthSek: { single: 300 },
    }),
  );
  const spike = await create(
    request,
    "SAA013",
    purchase({
      repairs: cost("spike", 2400, "once", 2),
      customCosts: cost("startup", 500, "once", 0),
    }),
  );
  await open(page, tax);
  let s = sections(await calculate(page), tax);
  expect(s.tax.cost.completeTotalSek).toBe(600);
  expect(s.payments.months[3].outflow.completeTotalSek).toBe(1200);
  expect(s.monthlyBudget.status).toBe("withinLimit");
  await expandResults(page);
  await money(resultRegion(page), "Fordonsskatt", "600,00");
  await money(
    resultRegion(page),
    "Genomsnittligt finansieringsbehov",
    "200,00",
  );
  await expect(
    page
      .getByRole("table", { name: "Betalningar per månad" })
      .getByRole("row")
      .filter({ hasText: "2026-03" }),
  ).toContainText(/1\s200,00/);
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("12");
  await page.getByRole("button", { name: "Öppna SAA012", exact: true }).click();
  s = sections(await calculate(page), repair);
  expect(s.totals.ownershipCost.completeTotalSek).toBe(7200);
  expect(s.payments.externalOutflow.completeTotalSek).toBe(3600);
  expect(s.payments.internalSaving.completeTotalSek).toBe(3600);
  await expandResults(page);
  await money(resultRegion(page), "Ägandekostnad för perioden", "7 200,00");
  await money(resultRegion(page), "Internt reparationssparande", "3 600,00");
  await page.getByRole("button", { name: "Öppna SAA013", exact: true }).click();
  s = sections(await calculate(page), spike);
  expect(s.monthlyBudget).toMatchObject({
    status: "withinLimit",
    fundingRequired: { completeTotalSek: 200 },
  });
  expect(s.startupBudget.fundingRequired.completeTotalSek).toBe(500);
  await expect(resultRegion(page).getByText("Inom budget")).toHaveCount(2);
  await page.getByLabel("Löpande månadsbudget (kr)").fill("199");
  expect(sections(await calculate(page), spike).monthlyBudget.status).toBe(
    "exceeded",
  );
  await expect(
    resultRegion(page).getByText("Budgeten överskrids"),
  ).toBeVisible();
});

test("A9 and A10 display the exact lease contract at matching, shorter and longer horizons", async ({
  page,
  request,
}) => {
  await saveProfile(
    request,
    profile({
      periodMonths: 24,
      annualDistanceKilometres: 15000,
      startupBudgetSek: 9000,
      monthlyBudgetSek: 2250,
    }),
  );
  const car = await create(
    request,
    "SAA014",
    purchase({
      acquisitionType: "lease",
      priceSek: null,
      residual: null,
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
    }),
  );
  await open(page, car);
  let s = sections(await calculate(page), car);
  expect(s.lease.excessDistanceKilometres).toBe(6000);
  expect(s.totals.ownershipCost.completeTotalSek).toBe(60000);
  expect(s.totals.monthlyCost.completeTotalSek).toBe(2500);
  expect(s.payments.externalOutflow.completeTotalSek).toBe(63000);
  expect(s.payments.externalInflow.completeTotalSek).toBe(3000);
  expect(s.startupBudget.fundingRequired.completeTotalSek).toBe(9000);
  expect(s.monthlyBudget.fundingRequired.completeTotalSek).toBe(2250);
  await expandResults(page);
  await money(resultRegion(page), "Ägandekostnad för perioden", "60 000,00");
  await money(resultRegion(page), "Externa utbetalningar", "63 000,00");
  await money(resultRegion(page), "Återbetalningar", "3 000,00");
  for (const [period, outflow, refund] of [
    [12, 33000, 0],
    [36, 63000, 3000],
  ]) {
    await page.getByLabel("Ägandeperiod (månader, 1–120)").fill(String(period));
    s = sections(await calculate(page), car);
    expect(s.totals.ownershipCost.completeTotalSek).toBeNull();
    expect(s.payments.externalOutflow.knownSubtotalSek).toBe(outflow);
    expect(s.payments.externalInflow.knownSubtotalSek).toBe(refund);
    expect(s.payments.coveredMonths).toBe(Math.min(period, 24));
    await expect(
      resultRegion(page)
        .getByText(/Jämförelseperioden matchar inte/)
        .first(),
    ).toBeVisible();
    if (period === 36) expect(s.monthlyBudget.status).toBe("unknown");
  }
});

test("two browsers share explicit saves while later edits and unsaved reloads remain separate", async ({
  browser,
  page,
  request,
  baseURL,
}) => {
  const car = await create(request, "SAA015", purchase({ priceSek: 25000 }));
  await open(page, car);
  const otherContext = await browser.newContext({ baseURL });
  let releaseSave!: () => void;
  const release = new Promise<void>((resolve) => {
    releaseSave = resolve;
  });
  let acknowledge!: () => void;
  const serverSaved = new Promise<void>((resolve) => {
    acknowledge = resolve;
  });
  try {
    await page.route("**/api/household-profile", async (route) => {
      if (route.request().method() !== "PUT") return route.continue();
      const response = await route.fetch();
      acknowledge();
      await release;
      await route.fulfill({ response });
    });
    await page.getByLabel("Kontanter till bilköpet (kr)").fill("20000");
    await page
      .getByRole("button", { name: "Spara hushållsprofil", exact: true })
      .click();
    await serverSaved;
    await page.getByLabel("Kontanter till bilköpet (kr)").fill("30000");
    releaseSave();
    await expect(page.getByText("Hushållsprofilen har sparats.")).toBeVisible();
    await expect(page.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "30000",
    );
    await expect(page.getByText("Osparade profiländringar")).toBeVisible();
    const other = await otherContext.newPage();
    await open(other, car);
    await expect(other.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "20000",
    );
    await other.getByLabel("Kontanter till bilköpet (kr)").fill("40000");
    await other
      .getByRole("button", { name: "Spara hushållsprofil", exact: true })
      .click();
    await expect(
      other.getByText("Hushållsprofilen har sparats."),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Spara hushållsprofil", exact: true })
      .click();
    await expect(
      page.getByText(/Hushållsprofilen har ändrats i ett annat fönster/),
    ).toBeVisible();
    await expect(page.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "30000",
    );
    await page
      .getByRole("button", { name: "Uppdatera serverläget", exact: true })
      .click();
    await expect(
      page.getByText(
        "Profilen har ändrats på servern. Din redigering är bevarad.",
      ),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Använd serverns profil", exact: true })
      .click();
    await expect(page.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "40000",
    );
    await page.getByLabel("Kontanter till bilköpet (kr)").fill("50000");
    await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("26000");
    await page
      .getByRole("button", { name: "Spara bilunderlag", exact: true })
      .click();
    await expect(page.getByText("Bilunderlaget har sparats.")).toBeVisible();
    expect(
      (await (await request.get("/api/household-profile")).json()).input
        .purchaseCashSek,
    ).toBe(40000);
    await page.reload();
    await expect(page.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "40000",
    );
    await expect(
      page.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("26000");
  } finally {
    releaseSave();
    await page.unrouteAll({ behavior: "wait" });
    await otherContext.close();
  }
});

test("two-browser draft conflicts and failed adoption preserve recovery until explicit replacement and deletion", async ({
  browser,
  page,
  request,
  baseURL,
}) => {
  const car = await create(request, "SAA016", purchase({ priceSek: 20000 }));
  await open(page, car);
  await page.getByRole("button", { name: "Spara utkast", exact: true }).click();
  await expect(page.getByText(/Utkastet har sparats/).first()).toBeVisible();
  const initial = await (await request.get("/api/vehicle-draft")).json();
  const otherContext = await browser.newContext({ baseURL });
  try {
    const other = await otherContext.newPage();
    other.on("dialog", (dialog) => void dialog.accept());
    await other.goto("/manual");
    await other
      .getByRole("button", { name: "Öppna sparat utkast", exact: true })
      .click();
    await expect(
      other.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("20000");
    expect(
      (await (await request.get("/api/vehicle-draft")).json()).revision,
    ).toBe(initial.revision);
    await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("21000");
    await page
      .getByRole("button", { name: "Spara utkast", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Ta utkastet i bruk" }),
    ).toBeEnabled();
    await other.getByLabel("Inköpspris (kr)", { exact: true }).fill("22000");
    await other
      .getByRole("button", { name: "Spara utkast", exact: true })
      .click();
    await expect(
      other.getByText(/Det gemensamma utkastet har ändrats/),
    ).toBeVisible();
    await expect(
      other.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("22000");
    const updated = await request.put(
      `/api/vehicle-cost-inputs/${car.vehicleId}`,
      {
        data: {
          expectedRevision: car.revision,
          cost: { input: purchase({ priceSek: 23000 }) },
        },
      },
    );
    expect(updated.status()).toBe(200);
    await page.getByRole("button", { name: "Ta utkastet i bruk" }).click();
    await expect(
      page.getByText(/Bilen har ändrats sedan den öppnades/),
    ).toBeVisible();
    const preserved = await (await request.get("/api/vehicle-draft")).json();
    expect(preserved.input.cost.input.priceSek).toBe(21000);
    // Opening the saved new draft after a reload must still retain its stale base.
    await page.reload();
    await page
      .getByRole("button", { name: "Öppna sparat utkast", exact: true })
      .click();
    await expect(
      page.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("21000");
    // Replace with a new registered draft through the explicit UI choice.
    await page.getByRole("button", { name: "Ny bil", exact: true }).click();
    owned.add("SAA017");
    await page
      .getByLabel("Registreringsnummer", { exact: true })
      .fill("SAA017");
    await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("17000");
    await page
      .getByRole("button", { name: "Spara utkast", exact: true })
      .click();
    await expect(page.getByText(/Sparat utkast: SAA017/)).toBeVisible();
    const replacement = await (await request.get("/api/vehicle-draft")).json();
    expect(replacement.revision).toBeGreaterThan(preserved.revision);
    await page.getByRole("button", { name: "Ta utkastet i bruk" }).click();
    await expect(
      page.getByRole("button", { name: "Öppna SAA017", exact: true }),
    ).toBeVisible();
    await expect(page.getByText("Utkastplatsen är tom.")).toBeVisible();
    const empty = await (await request.get("/api/vehicle-draft")).json();
    expect(empty.revision).toBeGreaterThan(replacement.revision);
    const stale = await request.put("/api/vehicle-draft", {
      data: {
        expectedRevision: replacement.revision,
        input: replacement.input,
        replaceExisting: true,
      },
    });
    expect(stale.status()).toBe(409);
    await page
      .getByRole("button", { name: "Spara utkast", exact: true })
      .click();
    await expect(page.getByText(/Sparat utkast: SAA017/)).toBeVisible();
    await page
      .getByRole("button", { name: "Radera SAA017", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Öppna SAA017", exact: true }),
    ).toHaveCount(0);
    await expect(page.getByText("Utkastplatsen är tom.")).toBeVisible();
    expect(
      (await (await request.get("/api/household-profile")).json()).input
        .purchaseCashSek,
    ).toBe(100000);
  } finally {
    await otherContext.close();
  }
});

test("atomic two-car legacy review recovers corrupt results and preserves every 50+50 source", async ({
  page,
  request,
}) => {
  test.setTimeout(60000);
  const ids: string[] = [];
  for (const [index, registration] of ["SAA018", "SAA019"].entries()) {
    owned.add(registration);
    const created = await request.post("/api/saved-cost-scenarios", {
      data: {
        registrationNumber: registration,
        scenario: {
          vehicleLabel: "Fiktiv äldre acceptansbil",
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
            (_, row) => ({
              label: `Återkommande ${row}`,
              amountSek: row + 1,
              cadence: "monthly",
            }),
          ),
          otherOneTimeCosts: Array.from(
            { length: index ? 0 : 50 },
            (_, row) => ({ label: `Engång ${row}`, amountSek: row + 1 }),
          ),
        },
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    ids.push((await created.json()).vehicleId);
  }
  // Deliberately unreadable derived data is created only in the fake-only Compose
  // database, for our UUIDs. Never accept an arbitrary SQL target or public endpoint.
  const project = process.env.COMPOSE_PROJECT_NAME ?? "car-expense-e2e";
  expect(project).toBe("car-expense-e2e");
  const docker = process.platform === "win32" ? "docker.exe" : "docker";
  const compose = [
    "compose",
    "-p",
    project,
    "-f",
    "compose.yaml",
    "-f",
    "compose.e2e.yaml",
  ];
  const services = execFileSync(
    docker,
    [...compose, "ps", "--services", "--status", "running"],
    { cwd: "../..", encoding: "utf8" },
  );
  expect(services.split(/\r?\n/)).toContain("fake-codex-extractor");
  expect(services.split(/\r?\n/)).not.toContain("codex-extractor");
  for (const id of ids) expect(id).toMatch(/^[0-9a-f-]{36}$/);
  execFileSync(
    docker,
    [
      ...compose,
      "exec",
      "-T",
      "postgres",
      "psql",
      "-U",
      "car_expense_app",
      "-d",
      "car_expense_calculator",
      "-v",
      "ON_ERROR_STOP=1",
      "-c",
      `UPDATE saved_cost_scenarios SET result_schema_version=999, result_snapshot='{}'::jsonb WHERE vehicle_id IN ('${ids[0]}','${ids[1]}')`,
    ],
    { cwd: "../..", encoding: "utf8" },
  );
  page.on("dialog", (dialog) => void dialog.accept());
  await page.goto("/manual/transition");
  await expect(page.getByLabel("Årlig körsträcka (mil)")).toHaveValue("0");
  const first = page.locator(`[data-vehicle-id="${ids[0]}"]`);
  const second = page.locator(`[data-vehicle-id="${ids[1]}"]`);
  for (const [section, original] of [
    [first, "11111"],
    [second, "22222"],
  ] as const) {
    await section
      .getByText("Originaluppgifter från äldre kalkyl", { exact: true })
      .click();
    await expect(
      section.getByText("Årskörsträcka (km)", { exact: true }).locator(".."),
    ).toContainText(original);
    await expect(
      section.getByLabel("Period för fast restvärde (månader)"),
    ).toHaveValue("24");
  }
  await expect(
    first.getByLabel("Beslut för Återkommande 49", { exact: true }),
  ).toHaveValue("map");
  await expect(
    first.getByLabel("Beslut för Engång 49", { exact: true }),
  ).toHaveValue("keepForReview");
  await first
    .getByLabel("Beslut för Engång 0", { exact: true })
    .selectOption("discard");
  await page.getByLabel("Årlig körsträcka (mil)").fill("1200");
  const confirmation = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/household-transition") &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Bekräfta hela övergången", exact: true })
    .click();
  const confirmed = await confirmation;
  expect(confirmed.status(), await confirmed.text()).toBe(200);
  expect(confirmed.request().postDataJSON().vehicles).toHaveLength(2);
  for (const id of ids) {
    const current: SavedVehicle = await (
      await request.get(`/api/vehicle-cost-inputs/${id}`)
    ).json();
    expect(current.state).toBe("current");
    expect(current.legacy).toBeNull();
    expect(current.input!.priceSek).toBe(20000);
    expect(current.input!.residual).toMatchObject({
      periodMonths: 24,
      value: { single: 15000 },
    });
    expect(
      (await request.get(`/api/saved-cost-scenarios/${id}`)).status(),
    ).toBe(404);
    if (id === ids[0]) {
      expect(current.input!.customCosts!.items).toHaveLength(50);
      expect(
        current.unresolvedLegacyItems.filter(
          (item) => item.input.kind === "oneTime",
        ),
      ).toHaveLength(49);
      expect(
        current.unresolvedLegacyItems.filter(
          (item) => item.input.kind === "maintenance",
        ),
      ).toHaveLength(1);
      expect(
        current.unresolvedLegacyItems.filter(
          (item) => item.input.kind === "energy",
        ),
      ).toHaveLength(1);
      await open(page, current);
      // Vehicle detail and the shared profile load independently after navigation.
      await expect(page.getByLabel("Ägandeperiod (månader, 1–120)")).toHaveValue("12");
      await expect(page.getByLabel("Årlig körsträcka (mil)")).toHaveValue("1200");
      const s = sections(await calculate(page), current);
      expect(s.totals.ownershipCost.completeTotalSek).toBeNull();
      expect(s.customCosts.cost.knownSubtotalSek).toBe(15300);
      expect(s.monthlyBudget.status).toBe("notConfigured");
    }
  }
  expect(
    (await (await request.get("/api/household-profile")).json()).input
      .annualDistanceKilometres,
  ).toBe(12000);
});

async function saveProfile(request: APIRequestContext, input: Profile) {
  const current = await request.get("/api/household-profile");
  expect([200, 404]).toContain(current.status());
  const revision =
    current.status() === 404 ? 0 : (await current.json()).revision;
  const saved = await request.put("/api/household-profile", {
    data: { expectedRevision: revision, input },
  });
  expect(saved.status(), await saved.text()).toBe(200);
}
async function create(
  request: APIRequestContext,
  registrationNumber: string,
  input: Vehicle,
): Promise<SavedVehicle> {
  owned.add(registrationNumber);
  const response = await request.post("/api/vehicle-cost-inputs", {
    data: {
      registrationNumber,
      cost: { vehicleLabel: "Fiktiv acceptansbil", input },
    },
  });
  expect(response.status(), await response.text()).toBe(201);
  return response.json();
}
async function clearDraft(request: APIRequestContext) {
  const response = await request.get("/api/vehicle-draft");
  expect(response.status()).toBe(200);
  const slot = await response.json();
  if (slot.input)
    expect(
      (
        await request.delete(
          `/api/vehicle-draft?expectedRevision=${slot.revision}`,
        )
      ).status(),
    ).toBe(200);
}
async function open(page: Page, car: SavedVehicle) {
  page.on("dialog", (dialog) => void dialog.accept());
  const profileRead = page.waitForResponse(response =>
    response.url().endsWith("/api/household-profile") && response.request().method() === "GET");
  await page.goto(`/manual?vehicleId=${car.vehicleId}`);
  const loadedProfile = await (await profileRead).json();
  await expect(page.getByLabel("Ägandeperiod (månader, 1–120)"))
    .toHaveValue(String(loadedProfile.input.periodMonths ?? ""));
  await expect(
    page.getByRole("heading", {
      name: `Redigera ${car.registrationNumber}`,
      exact: true,
    }),
  ).toBeVisible();
}
async function calculate(page: Page): Promise<Preview> {
  const response = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/household-calculations/preview") &&
      response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
  const result = await response;
  expect(result.status(), await result.text()).toBe(200);
  await expect(
    page.getByText(
      "Förhandsvisningen gäller nuvarande uppgifter, inklusive osparade ändringar.",
      { exact: true },
    ),
  ).toBeVisible();
  return result.json();
}
function sections(preview: Preview, car: SavedVehicle) {
  const candidate = preview.vehicles.find(
    (value) => value.registrationNumber === car.registrationNumber,
  );
  expect(candidate, car.registrationNumber).toBeDefined();
  return candidate!.sections;
}
async function expandResults(page: Page) {
  for (const summary of await resultRegion(page)
    .locator(":scope > details > summary")
    .all()) {
    if ((await summary.locator("..").getAttribute("open")) === null)
      await summary.click();
  }
}
async function money(scope: Locator, label: string, expected: string) {
  await expect(
    scope
      .getByText(label, { exact: true })
      .and(scope.locator("span, dt"))
      .locator(".."),
  ).toContainText(new RegExp(expected.replaceAll(" ", "\\s") + "\\s*kr"));
}
