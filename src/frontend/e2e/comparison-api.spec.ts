import { request as httpRequest } from "node:http";
import { expect, test } from "@playwright/test";

const preview = {
  mode: "manual", requestId: "comparison-browser", profile: {}, rules: {
    preferences: [
      { criterionKey: "purchasePriceSek", weight: 3, minimumEvidence: "userConfirmed", zeroPoint: 100000, fullPoint: 20000 },
      { criterionKey: "transmission", weight: 2, minimumEvidence: "userConfirmed", preferredValues: [{ transmission: "automatic" }] },
    ],
  }, asOfDate: "2026-09-08", candidates: [{
    vehicleId: "09086400-0000-4000-8000-000000000001", registrationNumber: "TST064", facts: { edits: {
      purchasePriceSek: { kind: "manual", manual: { value: 40000 } },
      transmission: { kind: "manual", manual: { value: "automatic" } },
    } },
  }],
};

test("comparison B1 travels through browser and Nginx without saving", async ({ page }) => {
  await page.goto("/manual");
  const result = await page.evaluate(async (body) => {
    const response = await fetch("/api/comparisons/preview", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
    });
    return { status: response.status, body: await response.json() };
  }, preview);
  expect(result.status).toBe(200);
  expect(result.body).toMatchObject({ mode: "manual", storageChecked: false, ruleVersion: 1, calculationVersion: 2 });
  expect(result.body.candidates[0]).toMatchObject({ score: { lower: 85, upper: 85 }, coveragePercent: 100 });
});

test("comparison facts share vehicle revisions and disappear with the vehicle", async ({ request }) => {
  let id: string | undefined;
  try {
    const created = await request.post("/api/vehicle-cost-inputs", { data: {
      registrationNumber: "TST064", cost: { input: { candidateKey: "TST064", priceSek: 40000 } },
    } });
    expect(created.status()).toBe(201);
    const vehicle = await created.json(); id = vehicle.vehicleId;
    const saved = await request.put(`/api/vehicle-facts/${id}`, { data: {
      expectedRevision: vehicle.revision, input: { ...preview.candidates[0].facts, costConfirmation: "confirm" },
    } });
    expect(saved.status()).toBe(200);
    const facts = await saved.json();
    expect(facts.revision).toBe(vehicle.revision + 1);
    expect(facts.costConfirmedAt).not.toBeNull();
    const stale = await request.put(`/api/vehicle-facts/${id}`, { data: { expectedRevision: vehicle.revision, input: {} } });
    expect(stale.status()).toBe(409);
    const removed = await request.delete(`/api/vehicle-cost-inputs/${id}?expectedRevision=${facts.revision}`);
    expect(removed.status()).toBe(204);
    expect((await request.get(`/api/vehicle-facts/${id}`)).status()).toBe(404);
  } finally {
    if (id) {
      const current = await request.get(`/api/vehicle-cost-inputs/${id}`);
      if (current.ok()) {
        const vehicle = await current.json();
        expect((await request.delete(`/api/vehicle-cost-inputs/${id}?expectedRevision=${vehicle.revision}`)).status()).toBe(204);
      } else expect(current.status()).toBe(404);
    }
  }
});

for (const chunked of [false, true]) {
  test(`Nginx comparison limit is exactly 2 MiB (${chunked ? "chunked" : "known length"})`, async ({ request, baseURL }) => {
    for (const extra of [0, 1]) {
      const body = JSON.stringify(preview).padEnd(2 * 1024 * 1024 + extra);
      const url = new URL("/api/comparisons/preview", baseURL);
      const status = chunked ? await chunkedPost(url, body)
        : (await request.post(url.toString(), { data: body, headers: { "Content-Type": "application/json" } })).status();
      expect(status).toBe(extra === 0 ? 200 : 413);
    }
  });
}

function chunkedPost(url: URL, body: string): Promise<number> {
  return new Promise((resolve, reject) => {
    const req = httpRequest(url, { method: "POST", agent: false,
      family: url.hostname === "localhost" ? 4 : undefined,
      headers: { "Content-Type": "application/json" } }, (res) => {
      res.resume(); res.on("end", () => resolve(res.statusCode ?? 0));
    });
    req.on("error", reject);
    req.setTimeout(15000, () => req.destroy(new Error("Comparison request timed out")));
    req.write(body.slice(0, 65536)); req.end(body.slice(65536));
  });
}
