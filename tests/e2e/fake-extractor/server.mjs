import { createServer } from "node:http";

const port = 8080;
const maximumCapacity = 2;
const invocationCounts = new Map();
const outcomeCounts = new Map();
const waiters = [];
let activeOperations = 0;
let maximumConcurrentOperations = 0;

const nullDraft = {
  registrationNumber: null,
  make: null,
  model: null,
  variant: null,
  modelYear: null,
  vin: null,
  priceSek: null,
  odometerKilometres: null,
  sellerType: null,
  locality: null,
  county: null,
  publishedDate: null,
  updatedDate: null,
  imageCount: null,
  fuelTypes: null,
  transmission: null,
  drivetrain: null,
  bodyType: null,
  colour: null,
  horsepower: null,
  engineDisplacementCubicCentimetres: null,
  energyConsumptions: null,
  annualVehicleTaxSek: null,
  ownerCount: null,
  firstRegistrationDate: null,
  lastInspectionDate: null,
  nextInspectionDate: null,
  towBar: null,
  equipment: null,
  sellerClaims: null,
  conditionNotes: null,
};

const completeDraft = {
  ...nullDraft,
  registrationNumber: "ABC123",
  make: "Volvo",
  model: "V70",
  variant: "2.4",
  modelYear: 2008,
  vin: "YV1TEST123",
  priceSek: 20000,
  odometerKilometres: 167100,
  sellerType: "private",
  locality: "Tenhult",
  county: "Jönköpings län",
  publishedDate: "2026-08-20",
  updatedDate: "2026-08-27",
  imageCount: 8,
  fuelTypes: ["petrol"],
  transmission: "manual",
  drivetrain: "frontWheelDrive",
  bodyType: "wagon",
  colour: "Röd",
  horsepower: 140,
  engineDisplacementCubicCentimetres: 2435,
  energyConsumptions: [{ label: "Bensin", unit: "litre", consumptionPer100Kilometres: 8 }],
  annualVehicleTaxSek: 2400,
  ownerCount: 4,
  firstRegistrationDate: "2008-01-15",
  lastInspectionDate: "2026-08-24",
  nextInspectionDate: "2027-10-31",
  towBar: false,
  equipment: [],
  sellerClaims: ["Motor och växellåda fungerar bra"],
  conditionNotes: [],
};

const server = createServer(async (request, response) => {
  if (request.method === "GET" && request.url === "/health/live") {
    return json(response, 200, { status: "healthy" });
  }
  if (request.method === "GET" && request.url === "/internal/status") {
    return json(response, 200, {
      configured: true,
      requestedModel: "gpt-5.6-luna",
      reasoningEffort: "medium",
      codexCliVersion: "0.153.0",
      promptVersion: 2,
      schemaVersion: 2,
    });
  }
  if (request.method === "GET" && request.url === "/internal/test-state") {
    return json(response, 200, {
      activeOperations,
      maximumConcurrentOperations,
      maximumCapacity,
      invocationCounts: Object.fromEntries(invocationCounts),
      outcomeCounts: Object.fromEntries(outcomeCounts),
    });
  }
  if (request.method !== "POST" || request.url !== "/internal/listing-extractions") {
    return json(response, 404, { code: "notFound" });
  }

  let payload;
  try {
    payload = JSON.parse(await readBody(request));
  } catch {
    return json(response, 400, { code: "invalidListingExtractionRequest" });
  }

  const testCase = classify(payload.normalizedUrl);
  const invocation = increment(invocationCounts, testCase.identifier);
  await acquireCapacity();
  increment(outcomeCounts, testCase.outcome);

  try {
    if (testCase.outcome === "slow") {
      await delay(250);
    }
    if (testCase.outcome === "rateLimited") {
      return json(response, 429, { code: "codexRateLimited" });
    }
    if (testCase.outcome === "timedOut") {
      return json(response, 503, { code: "codexTimedOut" });
    }
    if (testCase.outcome === "providerUnavailable") {
      return json(response, 503, { code: "codexProviderUnavailable" });
    }
    if (testCase.outcome === "invalidOutput") {
      return json(response, 200, { requestedModel: "gpt-5.6-luna", unexpected: true });
    }
    if (testCase.outcome === "retryOnce" && invocation === 1) {
      return json(response, 503, { code: "codexProviderUnavailable" });
    }

    const partial = testCase.outcome === "partial";
    const unavailable = testCase.outcome === "unavailable";
    const unmatched = testCase.outcome === "unmatchedSource";
    const draft = unavailable
      ? { ...nullDraft }
      : partial
        ? { ...nullDraft, make: "Volvo", locality: "Tenhult" }
        : { ...completeDraft };
    const sources = unavailable
      ? []
      : unmatched
        ? ["https://unmatched.example/another-listing"]
        : [payload.normalizedUrl, "https://manufacturer.example/model"];

    return json(response, 200, {
      requestedModel: "gpt-5.6-luna",
      promptVersion: 2,
      schemaVersion: 2,
      analyzedAtUtc: "2026-09-03T08:00:00Z",
      sources,
      draft,
    });
  } finally {
    releaseCapacity();
  }
});

server.listen(port, "0.0.0.0");

function classify(normalizedUrl) {
  let pathname = "/complete";
  try {
    pathname = new URL(normalizedUrl).pathname.toLowerCase();
  } catch {
    // The API validates URLs before invoking this test service.
  }

  const identifier = pathname.split("/").filter(Boolean).at(-1)?.replace(/[^a-z0-9-]/g, "-").slice(0, 100)
    ?? "unnamed";
  const cases = [
    ["retry-once", "retryOnce"],
    ["rate-limited", "rateLimited"],
    ["timed-out", "timedOut"],
    ["provider-unavailable", "providerUnavailable"],
    ["failure", "providerUnavailable"],
    ["invalid-output", "invalidOutput"],
    ["unmatched-source", "unmatchedSource"],
    ["unavailable", "unavailable"],
    ["partial", "partial"],
    ["slow", "slow"],
  ];
  const match = cases.find(([needle]) => pathname.includes(needle));
  return { identifier, outcome: match?.[1] ?? "complete" };
}

async function acquireCapacity() {
  if (activeOperations >= maximumCapacity) {
    await new Promise((resolve) => waiters.push(resolve));
  }
  activeOperations += 1;
  maximumConcurrentOperations = Math.max(maximumConcurrentOperations, activeOperations);
}

function releaseCapacity() {
  activeOperations -= 1;
  waiters.shift()?.();
}

function increment(map, key) {
  const next = (map.get(key) ?? 0) + 1;
  map.set(key, next);
  return next;
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function json(response, status, payload) {
  response.writeHead(status, { "content-type": "application/json" });
  response.end(JSON.stringify(payload));
}

async function readBody(request) {
  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (body.length > 16_384) throw new Error("Request too large");
  }
  return body;
}
