import { createServer } from "node:http";

const port = 8080;
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
  location: null,
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
      promptVersion: 1,
      schemaVersion: 1,
    });
  }
  if (request.method !== "POST" || request.url !== "/internal/listing-extractions") {
    return json(response, 404, { code: "notFound" });
  }

  const payload = JSON.parse(await readBody(request));
  if (payload.normalizedUrl.includes("failure")) {
    return json(response, 503, { code: "codexProviderUnavailable" });
  }

  const unavailable = payload.normalizedUrl.includes("unavailable");
  const partial = payload.normalizedUrl.includes("partial");
  const draft = unavailable
    ? { ...nullDraft }
    : {
        ...nullDraft,
        registrationNumber: partial ? null : "ABC123",
        make: "Volvo",
        model: partial ? null : "V70",
        variant: "2.4",
        modelYear: partial ? null : 2008,
        vin: "YV1TEST123",
        priceSek: partial ? null : 20000,
        odometerKilometres: partial ? null : 167100,
        sellerType: "private",
        location: "Tenhult",
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

  return json(response, 200, {
    requestedModel: "gpt-5.6-luna",
    promptVersion: 1,
    schemaVersion: 1,
    analyzedAtUtc: "2026-09-03T08:00:00Z",
    sources: unavailable ? [] : [payload.normalizedUrl, "https://manufacturer.example/model"],
    draft,
  });
});

server.listen(port, "0.0.0.0");

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
