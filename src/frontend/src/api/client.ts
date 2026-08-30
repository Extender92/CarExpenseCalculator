import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type SystemStatus = components["schemas"]["SystemStatusResponse"];
export type ManualCalculationRequest = components["schemas"]["ManualCalculationRequest"];
export type ManualCalculationResult = components["schemas"]["ManualCalculationResult"];
export type ValidationProblemDetails = components["schemas"]["ValidationProblemDetails"];

export class ManualCalculationApiError extends Error {
  constructor(
    message: string,
    public readonly problem?: ValidationProblemDetails,
  ) {
    super(message);
    this.name = "ManualCalculationApiError";
  }
}

const api = createClient<paths>({ baseUrl: "" });

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
