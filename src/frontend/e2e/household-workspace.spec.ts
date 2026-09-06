import { expect, test, type APIRequestContext } from "@playwright/test";

const owned = new Set<string>();
const zero = () => ({ isIncluded: false, items: [] });
const profile = () => ({
  startMonth: { year: 2026, month: 1 },
  periodMonths: 12,
  annualDistanceKilometres: 12000,
  purchaseCashSek: 100000,
  energyPrices: [
    { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 0 } },
  ],
  activeSensitivityMode: "baseline",
  startupBudgetSek: 9000,
  monthlyBudgetSek: 2250,
});
const purchase = () => ({
  candidateKey: "car",
  acquisitionType: "purchase",
  priceSek: 50000,
  residual: { mode: "fixedAmount", value: { single: 40000 }, periodMonths: 12 },
  energySources: [
    {
      key: "fuel",
      fuel: "petrol",
      unit: "litre",
      consumptionBasis: "wholeDistance",
      consumptionPer100Kilometres: { single: 8 },
    },
  ],
  tax: zero(),
  insurance: zero(),
  service: zero(),
  repairs: zero(),
  customCosts: zero(),
  additionalRepairAllowancePerMonthSek: { single: 0 },
});

test.beforeEach(async ({ request }) => {
  await clearDraft(request);
  const current = await request.get("/api/household-profile");
  const revision =
    current.status() === 404 ? 0 : (await current.json()).revision;
  const saved = await request.put("/api/household-profile", {
    data: { expectedRevision: revision, input: profile() },
  });
  expect(saved.status()).toBe(200);
});
test.afterEach(async ({ request }) => {
  try {
    const response = await request.get("/api/vehicle-cost-inputs");
    if (response.ok())
      for (const car of await response.json())
        if (owned.has(car.registrationNumber)) {
          const deleted = await request.delete(
            `/api/vehicle-cost-inputs/${car.vehicleId}?expectedRevision=${car.revision}`,
          );
          expect(deleted.status()).toBe(204);
        }
  } finally {
    owned.clear();
    await clearDraft(request);
  }
});

test("edits exact numbers, keeps unsaved navigation state and explicitly saves separate resources", async ({
  page,
}) => {
  let ai = 0;
  page.on("request", (request) => {
    if (request.url().includes("/api/listing-analyses")) ai++;
  });
  page.on("dialog", (dialog) => void dialog.accept());
  await page.goto("/manual");
  await expect(page.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
    "100000",
  );
  await page
    .getByLabel("Kontanter till bilköpet (kr)")
    .fill("123,1234567890123456789");
  await page.getByLabel("Årlig körsträcka (mil)").fill("1,1234567890123456789");
  await page.getByLabel("Registreringsnummer", { exact: true }).fill("HHH100");
  owned.add("HHH100");
  await page
    .getByLabel("Inköpspris (kr)", { exact: true })
    .fill("987,1234567890123456789");
  await page
    .getByRole("link", { name: "Granska äldre underlag", exact: true })
    .click();
  await page
    .getByRole("link", { name: "Till hushållskalkylen", exact: true })
    .click();
  await expect(page.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue(
    "987,1234567890123456789",
  );
  const saveProfile = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/household-profile") &&
      response.request().method() === "PUT",
  );
  await page
    .getByRole("button", { name: "Spara hushållsprofil", exact: true })
    .click();
  const savedProfile = await saveProfile;
  expect(savedProfile.status()).toBe(200);
  expect(savedProfile.request().postData()).toContain(
    '"annualDistanceKilometres":11.234567890123456789',
  );
  expect(await savedProfile.text()).toContain("123.1234567890123456789");
  await expect(
    page.getByText("Osparade biländringar", { exact: true }),
  ).toBeVisible();
  const saveCar = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/vehicle-cost-inputs") &&
      response.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Spara bilunderlag", exact: true })
    .click();
  const savedCar = await saveCar;
  expect(savedCar.status()).toBe(201);
  expect(await savedCar.text()).toContain("987.1234567890123456789");
  expect(ai).toBe(0);
});

test("renders complete purchase, fixed residual mismatch and keyboard-accessible errors", async ({
  page,
  request,
}) => {
  const car = await create(request, "HHH101", purchase());
  await page.goto(`/manual?vehicleId=${car.vehicleId}`);
  const results = page.getByRole("region", {
    name: "Beräkningsresultat för vald bil",
  });
  await expect(results.getByText(/10\s000,00\s*kr/).first()).toBeVisible();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("24");
  await expect(
    page.getByLabel("Period för fast restvärde (månader)"),
  ).toHaveValue("12");
  await expect(
    results.getByText(/Fast restvärde gäller en annan period/).first(),
  ).toBeVisible();
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("12");
  await expect(
    results.getByText("Periodens tillämpliga kostnader är kompletta."),
  ).toBeVisible();
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("fel");
  await page
    .getByRole("button", { name: "Spara bilunderlag", exact: true })
    .click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "Kontrollera uppgifterna" })
      .first(),
  ).toBeFocused();
  await page
    .getByRole("button", { name: /Inköpspris.*Ange ett tal/ })
    .first()
    .click();
  await expect(
    page.getByLabel("Inköpspris (kr)", { exact: true }),
  ).toBeFocused();
});

test("shows lease payments, refunds and average budget without extending beyond the agreement", async ({
  page,
  request,
}) => {
  const current = await (await request.get("/api/household-profile")).json();
  expect(
    (
      await request.put("/api/household-profile", {
        data: {
          expectedRevision: current.revision,
          input: { ...profile(), periodMonths: 24 },
        },
      })
    ).status(),
  ).toBe(200);
  const { priceSek: _price, residual: _residual, ...shared } = purchase();
  void _price;
  void _residual;
  const car = await create(request, "HHH102", {
    ...shared,
    acquisitionType: "lease",
    lease: {
      termMonths: 24,
      upfrontNonRefundableSek: 6000,
      refundableDepositSek: 3000,
      depositRefundSek: { single: 3000 },
      includedDistanceKilometres: 18000,
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
  await page.goto(`/manual?vehicleId=${car.vehicleId}`);
  const results = page.getByRole("region", {
    name: "Beräkningsresultat för vald bil",
  });
  await expect(results.getByText(/60\s000,00\s*kr/).first()).toBeVisible();
  await results.getByText("Betalningskalender", { exact: true }).click();
  await expect(results.getByText(/63\s000,00\s*kr/).first()).toBeVisible();
  await expect(results.getByText(/3\s000,00\s*kr/).first()).toBeVisible();
  await expect(results.getByText("Inom budget")).toHaveCount(2);
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("36");
  await expect(
    results.getByText(/Jämförelseperioden matchar inte/).first(),
  ).toBeVisible();
  await expect(results.getByText("Kan inte bedömas ännu")).toBeVisible();
});

test("restores a draft after reload, requires saving its edits and atomically adopts it", async ({
  page,
}) => {
  page.on("dialog", (dialog) => void dialog.accept());
  await page.goto("/manual");
  await expect(page.getByText("Utkastplatsen är tom.")).toBeVisible();
  await page.getByLabel("Registreringsnummer", { exact: true }).fill("HHH103");
  owned.add("HHH103");
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("15000");
  await page.getByRole("button", { name: "Spara utkast", exact: true }).click();
  await expect(page.getByText(/Utkastet har sparats\./).first()).toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "Öppna sparat utkast" }).click();
  await expect(page.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue(
    "15000",
  );
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("16000");
  await expect(
    page.getByRole("button", { name: "Ta utkastet i bruk" }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Spara utkast", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Ta utkastet i bruk" }),
  ).toBeEnabled();
  await page.getByRole("button", { name: "Ta utkastet i bruk" }).click();
  await expect(page.getByText("Utkastplatsen är tom.")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Öppna HHH103" }),
  ).toBeVisible();
  await expect(page.getByLabel("Inköpspris (kr)", { exact: true })).toHaveValue(
    "16000",
  );
});

test("conflicts between browser contexts preserve local edits and deletion clears the other workspace", async ({
  browser,
  page,
  request,
  baseURL,
}) => {
  const car = await create(request, "HHH104", purchase());
  const secondContext = await browser.newContext({ baseURL });
  try {
    const second = await secondContext.newPage();
    page.on("dialog", (dialog) => void dialog.accept());
    second.on("dialog", (dialog) => void dialog.accept());
    await page.goto(`/manual?vehicleId=${car.vehicleId}`);
    await second.goto(`/manual?vehicleId=${car.vehicleId}`);
    await expect(
      page.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("50000");
    await expect(
      second.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("50000");
    await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("51000");
    await second.getByLabel("Inköpspris (kr)", { exact: true }).fill("52000");
    await page
      .getByRole("button", { name: "Spara bilunderlag", exact: true })
      .click();
    await expect(page.getByText("Bilunderlaget har sparats.")).toBeVisible();
    await second
      .getByRole("button", { name: "Spara bilunderlag", exact: true })
      .click();
    await expect(
      second.getByText(/Bilen har ändrats sedan den öppnades/),
    ).toBeVisible();
    await expect(
      second.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("52000");
    await second.getByRole("button", { name: "Uppdatera serverläget" }).click();
    await expect(
      second.getByText(/Bilen har ändrats på servern/),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Radera HHH104", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Öppna HHH104" }),
    ).toHaveCount(0);
    await second.getByRole("button", { name: "Uppdatera serverläget" }).click();
    await expect(
      second.getByLabel("Inköpspris (kr)", { exact: true }),
    ).toHaveValue("");
    await expect(second.getByLabel("Kontanter till bilköpet (kr)")).toHaveValue(
      "100000",
    );
  } finally {
    await secondContext.close();
  }
});

test("reviews all 50+50 legacy posts and preserves unresolved inputs through the atomic transition", async ({
  page,
  request,
}) => {
  test.setTimeout(60000);
  owned.add("HHH105");
  const created = await request.post("/api/saved-cost-scenarios", {
    data: {
      registrationNumber: "HHH105",
      scenario: {
        vehicleLabel: "Äldre bil",
        calculationPeriodMonths: 24,
        purchasePriceSek: 20000,
        expectedResidualValueSek: 15000,
        annualDistanceKilometres: 9999,
        financing: null,
        energySources: [
          {
            label: "Äldre bensin",
            unit: "litre",
            consumptionPer100Kilometres: 8,
            pricePerUnitSek: 20,
            distanceSharePercent: 100,
          },
        ],
        vehicleTax: null,
        insurance: null,
        maintenanceAndRepairs: { amountSek: 1000, cadence: "annual" },
        otherRecurringCosts: Array.from({ length: 50 }, (_, index) => ({
          label: `Återkommande ${index}`,
          amountSek: index,
          cadence: "monthly",
        })),
        otherOneTimeCosts: Array.from({ length: 50 }, (_, index) => ({
          label: `Engång ${index}`,
          amountSek: index,
        })),
      },
    },
  });
  expect(created.status()).toBe(201);
  page.on("dialog", (dialog) => void dialog.accept());
  await page.goto("/manual/transition");
  await expect(page.getByLabel("Årlig körsträcka (mil)")).toHaveValue("1200");
  await expect(
    page.getByLabel("Period för fast restvärde (månader)"),
  ).toHaveValue("24");
  await expect(
    page.getByLabel("Beslut för Återkommande 49", { exact: true }),
  ).toHaveValue("map");
  await expect(
    page.getByLabel("Beslut för Engång 49", { exact: true }),
  ).toHaveValue("keepForReview");
  await page
    .getByLabel("Beslut för Engång 0", { exact: true })
    .selectOption("discard");
  const confirmation = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/household-transition") &&
      response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Bekräfta hela övergången" }).click();
  expect((await confirmation).status()).toBe(200);
  const snapshot = await (await request.get("/api/vehicle-cost-inputs")).json();
  const car = snapshot.find(
    (item: { registrationNumber: string }) =>
      item.registrationNumber === "HHH105",
  );
  const current = await (
    await request.get(`/api/vehicle-cost-inputs/${car.vehicleId}`)
  ).json();
  expect(current.state).toBe("current");
  expect(
    current.unresolvedLegacyItems.filter(
      (item: { input: { kind: string } }) => item.input.kind === "oneTime",
    ),
  ).toHaveLength(49);
  expect(current.input.customCosts.items).toHaveLength(50);
  await page.goto(`/manual?vehicleId=${car.vehicleId}`);
  await expect(
    page.getByText("Kalkylen är ofullständig. Kända delkostnader visas nedan."),
  ).toBeVisible();
});

test("previews without storage and keeps the mobile workspace inside the viewport", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route(
    /\/api\/(household-profile|vehicle-cost-inputs|vehicle-draft)$/,
    (route) =>
      route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: '{"code":"householdStorageUnavailable"}',
      }),
  );
  await page.goto("/manual");
  await page.getByLabel("Ägandeperiod (månader, 1–120)").fill("12");
  await page.getByLabel("Kontanter till bilköpet (kr)").fill("50000");
  await page.getByLabel("Inköpspris (kr)", { exact: true }).fill("50000");
  const response = page.waitForResponse((response) =>
    response.url().endsWith("/api/household-calculations/preview"),
  );
  await page.getByRole("button", { name: "Beräkna nu", exact: true }).click();
  expect((await response).status()).toBe(200);
  await expect(
    page.getByRole("region", { name: "Beräkningsresultat för vald bil" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});

for (const example of [
  {
    registration: "HHH107",
    fuels: ["electricity"],
    total: /12\s400,00\s*kr/,
    sources: [
      {
        key: "electricity",
        fuel: "electricity",
        unit: "kilowattHour",
        consumptionBasis: "wholeDistance",
        electricityBasis: "metered",
        consumptionPer100Kilometres: { single: 20 },
      },
    ],
    charging: {
      homeChargingSharePercent: { single: 100 },
      homeChargingPricePerKilowattHourSek: { single: 1 },
    },
  },
  {
    registration: "HHH108",
    fuels: ["electricity", "petrol"],
    total: /17\s200,00\s*kr/,
    sources: [
      {
        key: "electricity",
        fuel: "electricity",
        unit: "kilowattHour",
        consumptionBasis: "drivingMode",
        electricityBasis: "battery",
        consumptionPer100Kilometres: { single: 20 },
      },
      {
        key: "petrol",
        fuel: "petrol",
        unit: "litre",
        consumptionBasis: "drivingMode",
        consumptionPer100Kilometres: { single: 5 },
      },
    ],
    charging: {
      electricDrivingSharePercent: { single: 60 },
      homeChargingSharePercent: { single: 75 },
      homeChargingPricePerKilowattHourSek: { single: 1 },
      publicChargingPricePerKilowattHourSek: { single: 3 },
      chargingLossPercent: { single: 10 },
    },
  },
  {
    registration: "HHH109",
    fuels: ["biogas"],
    total: /14\s320,00\s*kr/,
    sources: [
      {
        key: "gas",
        fuel: "biogas",
        unit: "kilogram",
        consumptionBasis: "wholeDistance",
        consumptionPer100Kilometres: { single: 1.2 },
      },
    ],
    charging: {},
  },
]) {
  test(`preserves energy units and presents the server calculation for ${example.registration}`, async ({
    page,
    request,
  }) => {
    const current = await (await request.get("/api/household-profile")).json();
    const updated = await request.put("/api/household-profile", {
      data: {
        expectedRevision: current.revision,
        input: {
          ...profile(),
          ...example.charging,
          energyPrices: [
            { fuel: "petrol", unit: "litre", pricePerUnitSek: { single: 20 } },
            {
              fuel: "biogas",
              unit: "kilogram",
              pricePerUnitSek: { single: 30 },
            },
          ],
        },
      },
    });
    expect(updated.status()).toBe(200);
    const car = await create(request, example.registration, {
      ...purchase(),
      energySources: example.sources,
    });
    await page.goto(`/manual?vehicleId=${car.vehicleId}`);
    const results = page.getByRole("region", {
      name: "Beräkningsresultat för vald bil",
    });
    await expect(results.getByText(example.total).first()).toBeVisible();
    const editor = page.getByRole("region", { name: "Bilredigering" });
    await editor.getByText("Energi", { exact: true }).click();
    for (let index = 0; index < example.fuels.length; index++)
      await expect(
        editor.getByLabel("Drivmedel", { exact: true }).nth(index),
      ).toHaveValue(example.fuels[index]);
  });
}

test("saves an explicit reviewed URL draft in the same slot and adopts only its listing", async ({
  page,
  request,
}) => {
  owned.add("HHH106");
  await page.goto("/analyze-urls");
  await page
    .getByLabel("URL:er")
    .fill("https://cars.example/item/shared-draft");
  await page.getByRole("button", { name: "Skapa manuella utkast" }).click();
  const card = page.locator('[data-testid^="listing-card-"]');
  await card.getByLabel("Registreringsnummer").fill("HHH106");
  await card.getByLabel("Annonspris").fill("123,1234567890123456789");
  const saved = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/vehicle-draft") &&
      response.request().method() === "PUT",
  );
  await card
    .getByRole("button", { name: "Spara gemensamt annonsutkast" })
    .click();
  const response = await saved;
  expect(response.status()).toBe(200);
  expect(await response.text()).toContain("123.1234567890123456789");
  await page.goto("/manual");
  await page.getByRole("button", { name: "Öppna sparat utkast" }).click();
  await page.getByRole("button", { name: "Ta utkastet i bruk" }).click();
  await expect(page.getByText("Utkastplatsen är tom.")).toBeVisible();
  const vehicles = await (await request.get("/api/vehicle-cost-inputs")).json();
  const vehicle = vehicles.find(
    (value: { registrationNumber: string }) =>
      value.registrationNumber === "HHH106",
  );
  expect(vehicle.state).toBe("listingOnly");
  const listing = await request.get(`/api/saved-listings/${vehicle.vehicleId}`);
  expect(await listing.text()).toContain("123.1234567890123456789");
});

async function create(
  request: APIRequestContext,
  registrationNumber: string,
  input: unknown,
) {
  owned.add(registrationNumber);
  const result = await request.post("/api/vehicle-cost-inputs", {
    data: { registrationNumber, cost: { input } },
  });
  expect(result.status()).toBe(201);
  return result.json();
}
async function clearDraft(request: APIRequestContext) {
  const read = await request.get("/api/vehicle-draft");
  expect(read.status()).toBe(200);
  const slot = await read.json();
  if (slot.input != null)
    expect(
      (
        await request.delete(
          `/api/vehicle-draft?expectedRevision=${slot.revision}`,
        )
      ).status(),
    ).toBe(200);
}
