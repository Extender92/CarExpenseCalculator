import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type SystemStatus = components["schemas"]["SystemStatusResponse"];
export type ManualCalculationRequest = components["schemas"]["ManualCalculationRequest"];
export type ManualCalculationResult = components["schemas"]["ManualCalculationResult"];
export type ValidationProblemDetails = components["schemas"]["ValidationProblemDetails"];
export type CreateSavedCostScenarioRequest = components["schemas"]["CreateSavedCostScenarioRequest"];
export type ReplaceSavedCostScenarioRequest = components["schemas"]["ReplaceSavedCostScenarioRequest"];
export type SavedCostScenarioProblemDetails = components["schemas"]["SavedCostScenarioProblemDetails"];
export type SavedCostScenarioResponse = components["schemas"]["SavedCostScenarioResponse"];
export type SavedCostScenarioSummary = components["schemas"]["SavedCostScenarioSummaryResponse"];

export class ManualCalculationApiError extends Error {
  constructor(
    message: string,
    public readonly problem?: ValidationProblemDetails,
  ) {
    super(message);
    this.name = "ManualCalculationApiError";
  }
}

export class SavedCostScenarioApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly validationProblem?: ValidationProblemDetails,
    public readonly problem?: SavedCostScenarioProblemDetails,
  ) {
    super(message);
    this.name = "SavedCostScenarioApiError";
  }
}

const api = createClient<paths>({
  baseUrl: typeof window === "undefined" ? "http://localhost" : window.location.origin,
  fetch: (request) => globalThis.fetch(request),
});

export async function getSystemStatus(): Promise<SystemStatus> {
  const { data, error } = await api.GET("/api/system/status");

  if (error || !data) {
    throw new Error("API-status kunde inte hämtas.");
  }

  return data;
}

export async function calculateManualScenario(
  request: ManualCalculationRequest,
): Promise<ManualCalculationResult> {
  const { data, error, response } = await api.POST("/api/manual-calculations", {
    body: request,
  });

  if (data) {
    return data;
  }

  if (response.status === 400) {
    throw new ManualCalculationApiError(
      "Kalkylen innehåller värden som inte kunde godkännas.",
      error,
    );
  }

  throw new ManualCalculationApiError("Kalkylen kunde inte beräknas just nu.");
}

export async function listSavedCostScenarios(): Promise<SavedCostScenarioSummary[]> {
  const { data, error, response } = await api.GET("/api/saved-cost-scenarios");

  if (data !== undefined) {
    return data;
  }

  throw createSavedScenarioError(response.status, error);
}

export async function getSavedCostScenario(vehicleId: string): Promise<SavedCostScenarioResponse> {
  const { data, error, response } = await api.GET("/api/saved-cost-scenarios/{vehicleId}", {
    params: { path: { vehicleId } },
  });

  if (data !== undefined) {
    return data;
  }

  throw createSavedScenarioError(response.status, error);
}

export async function getSavedCostScenarioByRegistration(
  registrationNumber: string,
): Promise<SavedCostScenarioResponse> {
  const { data, error, response } = await api.GET(
    "/api/saved-cost-scenarios/by-registration/{registrationNumber}",
    { params: { path: { registrationNumber } } },
  );

  if (data !== undefined) {
    return data;
  }

  throw createSavedScenarioError(response.status, error);
}

export async function createSavedCostScenario(
  request: CreateSavedCostScenarioRequest,
): Promise<SavedCostScenarioResponse> {
  const { data, error, response } = await api.POST("/api/saved-cost-scenarios", {
    body: request,
  });

  if (data !== undefined) {
    return data;
  }

  throw createSavedScenarioError(response.status, error);
}

export async function replaceSavedCostScenario(
  vehicleId: string,
  request: ReplaceSavedCostScenarioRequest,
): Promise<SavedCostScenarioResponse> {
  const { data, error, response } = await api.PUT("/api/saved-cost-scenarios/{vehicleId}", {
    params: { path: { vehicleId } },
    body: request,
  });

  if (data !== undefined) {
    return data;
  }

  throw createSavedScenarioError(response.status, error);
}

export async function deleteSavedCostScenario(
  vehicleId: string,
  expectedRevision: number,
): Promise<void> {
  const { error, response } = await api.DELETE("/api/saved-cost-scenarios/{vehicleId}", {
    params: {
      path: { vehicleId },
      query: { expectedRevision },
    },
  });

  if (response.status === 204) {
    return;
  }

  throw createSavedScenarioError(response.status, error);
}

function createSavedScenarioError(status: number, error: unknown) {
  if (status === 400) {
    return new SavedCostScenarioApiError(
      "Uppgifterna innehåller värden som inte kunde godkännas.",
      status,
      error as ValidationProblemDetails,
    );
  }

  const problem = error as SavedCostScenarioProblemDetails | undefined;
  const messages: Record<string, string> = {
    savedCostScenarioNotFound: "Den sparade bilen finns inte längre.",
    registrationNumberConflict: "Det finns redan en sparad bil med registreringsnumret.",
    revisionConflict: "Den sparade bilen har ändrats sedan den öppnades.",
    unsupportedSavedScenarioVersion: "Den sparade bilen har en version som inte kan visas.",
  };

  return new SavedCostScenarioApiError(
    (problem?.code && messages[problem.code]) || "Sparade bilar kunde inte hanteras just nu.",
    status,
    undefined,
    problem,
  );
}
