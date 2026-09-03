import { afterEach, describe, expect, it, vi } from "vitest";
import {
  analyzeListing,
  createSavedCostScenario,
  deleteSavedCostScenario,
  getSavedCostScenario,
  getSavedCostScenarioByRegistration,
  listSavedCostScenarios,
  ListingAnalysisApiError,
  replaceSavedCostScenario,
  SavedCostScenarioApiError,
  type ManualCalculationRequest,
  type SavedCostScenarioResponse,
} from "./client";
import { completeManualCalculationResult } from "@/test/manual-calculation-result";
import { completeListingAnalysisResponse } from "@/test/listing-analysis";

const scenario: ManualCalculationRequest = {
  vehicleLabel: "Volvo V70",
  calculationPeriodMonths: 12,
  purchasePriceSek: 20_000,
  expectedResidualValueSek: 15_000,
  annualDistanceKilometres: 0,
  financing: null,
  energySources: [],
  vehicleTax: null,
  insurance: null,
  maintenanceAndRepairs: null,
  otherRecurringCosts: [],
  otherOneTimeCosts: [],
};

const savedResponse: SavedCostScenarioResponse = {
  vehicleId: "0194f7a8-5c33-7f43-b516-d5c2f94dcd31",
  registrationNumber: "ABC123",
  revision: 1,
  calculationVersion: 1,
  resultSchemaVersion: 1,
  createdAtUtc: "2026-08-30T08:00:00Z",
  updatedAtUtc: "2026-08-30T08:00:00Z",
  calculatedAtUtc: "2026-08-30T08:00:00Z",
  scenario,
  result: completeManualCalculationResult,
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("saved cost scenario API client", () => {
  it("calls list and both lookup endpoints with typed responses", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([]))
      .mockResolvedValueOnce(jsonResponse(savedResponse))
      .mockResolvedValueOnce(jsonResponse(savedResponse));
    vi.stubGlobal("fetch", fetchMock);

    await expect(listSavedCostScenarios()).resolves.toEqual([]);
    await expect(getSavedCostScenario(savedResponse.vehicleId)).resolves.toEqual(savedResponse);
    await expect(getSavedCostScenarioByRegistration("ABC-123")).resolves.toEqual(savedResponse);

    expect(requestUrl(fetchMock.mock.calls[0][0])).toContain("/api/saved-cost-scenarios");
    expect(requestUrl(fetchMock.mock.calls[1][0])).toContain(savedResponse.vehicleId);
    expect(decodeURIComponent(requestUrl(fetchMock.mock.calls[2][0]))).toContain("ABC-123");
  });

  it("creates, replaces, and deletes through the generated routes", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse(savedResponse, 201))
      .mockResolvedValueOnce(jsonResponse({ ...savedResponse, revision: 2 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await createSavedCostScenario({ registrationNumber: "ABC123", scenario });
    await replaceSavedCostScenario(savedResponse.vehicleId, { expectedRevision: 1, scenario });
    await deleteSavedCostScenario(savedResponse.vehicleId, 2);

    expect(requestMethod(fetchMock.mock.calls[0][0])).toBe("POST");
    expect(await requestBody(fetchMock.mock.calls[0][0])).toMatchObject({ registrationNumber: "ABC123" });
    expect(requestMethod(fetchMock.mock.calls[1][0])).toBe("PUT");
    expect(await requestBody(fetchMock.mock.calls[1][0])).toMatchObject({ expectedRevision: 1 });
    expect(requestMethod(fetchMock.mock.calls[2][0])).toBe("DELETE");
    expect(requestUrl(fetchMock.mock.calls[2][0])).toContain("expectedRevision=2");
  });

  it("exposes validation and saved-problem metadata", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({
        title: "Validation failed",
        status: 400,
        errors: { "scenario.purchasePriceSek": ["Value must be at least 0."] },
      }, 400, "application/problem+json"))
      .mockResolvedValueOnce(jsonResponse({
        title: "Conflict",
        status: 409,
        code: "registrationNumberConflict",
        existingVehicleId: savedResponse.vehicleId,
      }, 409, "application/problem+json"));
    vi.stubGlobal("fetch", fetchMock);

    const validationError = await createSavedCostScenario({ registrationNumber: "ABC123", scenario })
      .catch((error: unknown) => error);
    expect(validationError).toBeInstanceOf(SavedCostScenarioApiError);
    expect((validationError as SavedCostScenarioApiError).validationProblem?.errors)
      .toHaveProperty("scenario.purchasePriceSek");

    const conflict = await createSavedCostScenario({ registrationNumber: "ABC123", scenario })
      .catch((error: unknown) => error);
    expect(conflict).toBeInstanceOf(SavedCostScenarioApiError);
    expect((conflict as SavedCostScenarioApiError).problem).toMatchObject({
      code: "registrationNumberConflict",
      existingVehicleId: savedResponse.vehicleId,
    });
  });

  it("surfaces unsupported versions, missing resources, and network failures", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({
        status: 409,
        code: "unsupportedSavedScenarioVersion",
        calculationVersion: 99,
        resultSchemaVersion: 99,
      }, 409, "application/problem+json"))
      .mockResolvedValueOnce(jsonResponse({
        status: 404,
        code: "savedCostScenarioNotFound",
      }, 404, "application/problem+json"))
      .mockRejectedValueOnce(new TypeError("Failed to fetch"));
    vi.stubGlobal("fetch", fetchMock);

    await expect(listSavedCostScenarios()).rejects.toMatchObject({
      problem: { code: "unsupportedSavedScenarioVersion" },
    });
    await expect(getSavedCostScenario(savedResponse.vehicleId)).rejects.toMatchObject({
      status: 404,
      problem: { code: "savedCostScenarioNotFound" },
    });
    await expect(listSavedCostScenarios()).rejects.toBeInstanceOf(TypeError);
  });
});

describe("listing analysis API client", () => {
  it("posts one URL through the generated same-origin route", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(completeListingAnalysisResponse));
    vi.stubGlobal("fetch", fetchMock);

    await expect(analyzeListing("https://cars.example/item/1")).resolves.toEqual(completeListingAnalysisResponse);
    expect(new URL(requestUrl(fetchMock.mock.calls[0][0])).pathname).toBe("/api/listing-analyses");
    expect(requestMethod(fetchMock.mock.calls[0][0])).toBe("POST");
    expect(await requestBody(fetchMock.mock.calls[0][0])).toEqual({ url: "https://cars.example/item/1" });
  });

  it("exposes validation details for HTTP 400", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({
      title: "Validation failed",
      status: 400,
      errors: { url: ["URL must be public."] },
    }, 400, "application/problem+json")));

    const error = await analyzeListing("http://localhost").catch((reason: unknown) => reason);
    expect(error).toBeInstanceOf(ListingAnalysisApiError);
    expect(error).toMatchObject({ status: 400, validationProblem: { errors: { url: ["URL must be public."] } } });
  });

  it.each([
    [429, "listingAnalysisRateLimited", "begränsad"],
    [503, "listingAnalysisNotConfigured", "inte konfigurerad"],
    [503, "listingAnalysisTimedOut", "för lång tid"],
    [503, "listingAnalysisProviderUnavailable", "inte tillgänglig"],
    [503, "listingAnalysisInvalidProviderResponse", "inget användbart svar"],
  ])("maps %s %s to a typed safe error", async (status, code, message) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ status, code }, status, "application/problem+json")));

    await expect(analyzeListing("https://cars.example/item/1")).rejects.toMatchObject({
      status,
      code,
      message: expect.stringContaining(message),
    });
  });

  it("wraps network errors without exposing their contents", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("secret upstream details")));

    const error = await analyzeListing("https://cars.example/item/1").catch((reason: unknown) => reason);
    expect(error).toMatchObject({ code: "listingAnalysisNetworkError" });
    expect((error as Error).message).not.toContain("secret");
  });

  it("propagates request cancellation", async () => {
    const controller = new AbortController();
    vi.stubGlobal("fetch", vi.fn().mockImplementation((request: Request) => new Promise((_resolve, reject) => {
      request.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError")));
    })));

    const request = analyzeListing("https://cars.example/item/1", controller.signal);
    controller.abort();
    await expect(request).rejects.toMatchObject({ name: "AbortError" });
  });
});

function jsonResponse(body: unknown, status = 200, contentType = "application/json") {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": contentType },
  });
}

function requestUrl(input: RequestInfo | URL) {
  return input instanceof Request ? input.url : String(input);
}

function requestMethod(input: RequestInfo | URL) {
  return input instanceof Request ? input.method : "GET";
}

async function requestBody(input: RequestInfo | URL) {
  return input instanceof Request ? input.clone().json() : undefined;
}
