import { request as httpRequest } from "node:http";
import { expect, test } from "@playwright/test";

const route = "/api/comparisons/preview-all";
function manual(count: number) {
  return {
    mode: "manual", requestId: "complete-browser", profile: {}, asOfDate: "2026-09-08",
    rules: { preferences: [{ criterionKey: "purchasePriceSek", weight: 1, minimumEvidence: "userConfirmed", zeroPoint: 100000, fullPoint: 20000 }] },
    candidates: Array.from({ length: count }, (_, i) => ({
      vehicleId: `09088500-0000-4000-8000-${String(i + 1).padStart(12, "0")}`,
      registrationNumber: `TAA${String(i + 100)}`,
      facts: { edits: { purchasePriceSek: { kind: "manual", manual: { value: 40000 } } } },
    })),
  };
}

test("101 manual cars produce one complete three-view comparison through browser and Nginx", async ({ page }) => {
  await page.goto("/manual");
  const result = await page.evaluate(async ({ route, body }) => {
    const response = await fetch(route, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
    return { status: response.status, body: await response.json() };
  }, { route, body: manual(101) });
  expect(result.status).toBe(200);
  expect(result.body).toMatchObject({ mode: "manual", candidateCount: 101, transportVersion: 2, baselineToken: null, listings: [] });
  for (const key of ["baseline", "favorable", "cautious"]) {
    const view = result.body.views[key];
    expect(view.storageChecked).toBe(false);
    expect(view.candidates).toHaveLength(101);
    expect(view.costOrder).toHaveLength(101);
    expect(view.scoreOrder).toEqual(manual(101).candidates.map(x => x.vehicleId));
    expect(view.candidates.every((x: { isDefinitePreferenceWinner: boolean }) => !x.isDefinitePreferenceWinner)).toBe(true);
  }
});

test("stored all-car comparison detects another client's deletion without removing other candidates", async ({ request, browser, baseURL }) => {
  const other = await browser.newContext({ baseURL });
  const created: string[] = [];
  try {
    const before = await (await request.get("/api/comparisons/baseline")).json();
    for (const candidate of manual(101).candidates) {
      const response = await request.post("/api/vehicle-cost-inputs", { data: {
        registrationNumber: candidate.registrationNumber, cost: { input: { candidateKey: candidate.registrationNumber, priceSek: 40000 } },
      } });
      expect(response.status()).toBe(201);
      created.push((await response.json()).vehicleId);
    }
    const baseline = await (await request.get("/api/comparisons/baseline")).json();
    const body = { mode: "stored", requestId: "whole-saved", profile: {}, rules: {}, asOfDate: "2026-09-08",
      storedBase: { baselineToken: baseline.baselineToken, householdProfileRevision: baseline.householdProfileRevision, ruleProfileRevision: baseline.ruleProfileRevision } };
    const response = await request.post(route, { data: body });
    expect(response.status()).toBe(200);
    const result = await response.json();
    expect(result.candidateCount).toBe(before.candidateCount + 101);
    for (const key of ["baseline", "favorable", "cautious"]) {
      expect(result.views[key].costOrder).toEqual(expect.arrayContaining(created));
      expect(new Set(result.views[key].costOrder).size).toBe(result.candidateCount);
    }
    const current = await (await other.request.get(`/api/vehicle-cost-inputs/${created[100]}`)).json();
    expect((await other.request.delete(`/api/vehicle-cost-inputs/${created[100]}?expectedRevision=${current.revision}`)).status()).toBe(204);
    const conflict = await request.post(route, { data: body });
    expect(conflict.status()).toBe(409);
    expect((await conflict.json()).code).toBe("comparisonBaselineConflict");
  } finally {
    for (const id of created) {
      const current = await request.get(`/api/vehicle-cost-inputs/${id}`);
      if (current.ok()) expect((await request.delete(`/api/vehicle-cost-inputs/${id}?expectedRevision=${(await current.json()).revision}`)).status()).toBe(204);
      else expect(current.status()).toBe(404);
    }
    await other.close();
  }
});

for (const chunked of [false, true]) {
  test(`complete comparison accepts exactly 32 MiB through Nginx (${chunked ? "chunked" : "length"})`, async ({ request, baseURL }) => {
    const input = manual(0); input.requestId = "räkna-åäö";
    const json = JSON.stringify(input);
    const maximum = 32 * 1024 * 1024;
    for (const extra of [0, 1]) {
      const body = json + " ".repeat(maximum - Buffer.byteLength(json, "utf8") + extra);
      const response = chunked ? await chunkedPost(new URL(route, baseURL), body)
        : await request.post(route, { data: body, headers: { "Content-Type": "application/json" } }).then(async r => ({ status: r.status(), body: await r.json() }));
      expect(response.status).toBe(extra ? 413 : 200);
      if (extra) expect(response.body).toMatchObject({ code: "payloadTooLarge", maximumRequestBytes: maximum });
      else expect(response.body).toMatchObject({ candidateCount: 0, transportVersion: 2 });
    }
  });
}

function chunkedPost(url: URL, body: string): Promise<{ status: number; body: unknown }> {
  return new Promise((resolve, reject) => {
    const req = httpRequest(url, { method: "POST", agent: false, family: url.hostname === "localhost" ? 4 : undefined,
      headers: { "Content-Type": "application/json" } }, res => {
      let text = "";
      res.setEncoding("utf8"); res.on("data", chunk => { text += chunk; });
      res.on("end", () => {
        try { resolve({ status: res.statusCode ?? 0, body: JSON.parse(text) }); }
        catch (error) { reject(error); }
      });
      res.on("error", reject);
    });
    req.on("error", reject);
    req.setTimeout(30000, () => req.destroy(new Error("Complete comparison request timed out")));
    req.write(body.slice(0, 65536)); req.end(body.slice(65536));
  });
}
