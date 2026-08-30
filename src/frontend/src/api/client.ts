import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type SystemStatus = components["schemas"]["SystemStatusResponse"];

const api = createClient<paths>({ baseUrl: "" });

export async function getSystemStatus(): Promise<SystemStatus> {
  const { data, error } = await api.GET("/api/system/status");

  if (error || !data) {
    throw new Error("API-status kunde inte hämtas.");
  }

  return data;
}
