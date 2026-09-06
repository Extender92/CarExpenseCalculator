import { request as httpRequest } from "node:http";
import { expect, test } from "@playwright/test";

const preview = {
  requestId: "browser-generation",
  profile: { periodMonths: 12, annualDistanceKilometres: 0, purchaseCashSek: 50000 },
  vehicles: [{
    registrationNumber: null,
    unresolvedLegacyItems: [],
    input: {
      candidateKey: "manual",
      priceSek: 50000,
      residual: { mode: "fixedAmount", value: { single: 40000 }, periodMonths: 12 },
      energySources: [],
      tax: { isIncluded: false, items: [] },
      insurance: { isIncluded: false, items: [] },
      service: { isIncluded: false, items: [] },
      repairs: { isIncluded: false, items: [] },
      additionalRepairAllowancePerMonthSek: { single: 0 },
      customCosts: { isIncluded: false, items: [] },
    },
  }],
};

test("browser receives partial household sections through the shared origin", async ({ page }) => {
  await page.goto("/manual");
  const result = await page.evaluate(async (body) => {
    const response = await fetch("/api/household-calculations/preview", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
    });
    return { status: response.status, body: await response.json() };
  }, preview);
  expect(result.status).toBe(200);
  expect(result.body).toMatchObject({ requestId: "browser-generation", calculationVersion: 2, resultSchemaVersion: 2 });
  expect(result.body.vehicles[0].sections.totals).toMatchObject({
    ownershipCost: { state: "complete", completeTotalSek: 10000 },
    costPerMil: { completeTotalSek: null, missingComponents: ["zeroDistance"] },
  });
});

for (const chunked of [false, true]) {
  test(`Nginx enforces the household 2 MiB boundary (${chunked ? "chunked" : "known length"})`, async ({ request, baseURL }) => {
    for (const extra of [0, 1]) {
      const body = JSON.stringify(preview).padEnd(2 * 1024 * 1024 + extra);
      const url = new URL("/api/household-calculations/preview", baseURL);
      const status = chunked
        ? await chunkedPost(url, body)
        : (await request.post(url.toString(), { data: body, headers: { "Content-Type": "application/json" } })).status();
      expect(status).toBe(extra === 0 ? 200 : 413);
    }
  });
}

function chunkedPost(url: URL, body: string): Promise<number> {
  return new Promise((resolve, reject) => {
    // Docker Desktop's IPv6 localhost forwarding can stall large chunked writes.
    // Use its IPv4 listener for local tests; native Linux forwarding was checked too.
    const req = httpRequest(url, { method: "POST", agent: false,
      family: url.hostname === "localhost" ? 4 : undefined,
      headers: { "Content-Type": "application/json" } }, (res) => {
      res.resume();
      res.on("end", () => resolve(res.statusCode ?? 0));
    });
    req.on("error", reject);
    req.setTimeout(15000, () => req.destroy(new Error("Household request timed out")));
    // Node uses Transfer-Encoding: chunked when writing without Content-Length.
    req.write(body.slice(0, 65536));
    req.end(body.slice(65536));
  });
}
