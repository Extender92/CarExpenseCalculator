import { stringifyExact } from "@/features/household/numbers";
import createClient from "openapi-fetch";
import { listingRequest, listingNumberText, type ListingNumber, type Preserved } from "@/features/url-analysis/exact";
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
export type ListingAnalysisRequest = components["schemas"]["ListingAnalysisRequest"];
export type ListingAnalysisResponse = Preserved<components["schemas"]["ListingAnalysisResponse"]>;
export type ListingAnalysisProblemDetails = components["schemas"]["ListingAnalysisProblemDetails"];
export type ListingDraftResponse = Preserved<components["schemas"]["ListingDraftResponse"]>;
export type ListingAnalysisSource = components["schemas"]["ListingAnalysisSourceResponse"];
export type FieldProvenance = components["schemas"]["FieldProvenanceResponse"];
export type ListingFieldCode = components["schemas"]["ListingFieldCode"];
export type ListingAnalysisStatus = components["schemas"]["ListingAnalysisStatus"];
export type SellerType = components["schemas"]["SellerType"];
export type FuelType = components["schemas"]["FuelType"];
export type Transmission = components["schemas"]["Transmission"];
export type Drivetrain = components["schemas"]["Drivetrain"];
export type BodyType = components["schemas"]["BodyType"];
export type EnergyUnit = components["schemas"]["EnergyUnit"];
export type CreateSavedListingRequest = Preserved<components["schemas"]["CreateSavedListingRequest"]>;
export type ReplaceSavedListingRequest = Preserved<components["schemas"]["ReplaceSavedListingRequest"]>;
export type ReviewedListingInput = Preserved<components["schemas"]["ReviewedListingInput"]>;
export type ListingDraftInput = Preserved<components["schemas"]["ListingDraftInput"]>;
export type SavedListingProblemDetails = components["schemas"]["SavedListingProblemDetails"];
export type SavedListingResponse = Preserved<components["schemas"]["SavedListingResponse"]>;
export type SavedListingSummary = Preserved<components["schemas"]["SavedListingSummaryResponse"]>;

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

export class ListingAnalysisApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
    public readonly code?: string,
    public readonly validationProblem?: ValidationProblemDetails,
    public readonly retryAfterSeconds?: number,
  ) {
    super(message);
    this.name = "ListingAnalysisApiError";
  }
}

export class SavedListingApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
    public readonly validationProblem?: ValidationProblemDetails,
    public readonly problem?: SavedListingProblemDetails,
  ) {
    super(message);
    this.name = "SavedListingApiError";
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
  signal?: AbortSignal,
): Promise<ManualCalculationResult> {
  const { data, error, response } = await api.POST("/api/manual-calculations", {
    body: request,
    signal,
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

export async function analyzeListing(
  url: string,
  signal?: AbortSignal,
): Promise<ListingAnalysisResponse> {
  const postListingAnalysis = () => listingRequest<ListingAnalysisResponse>("POST", "/api/listing-analyses", {url}, signal);
  let result: Awaited<ReturnType<typeof postListingAnalysis>>;
  try {
    result = await postListingAnalysis();
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    if (signal?.aborted) throw new DOMException("The operation was aborted.", "AbortError");
    throw new ListingAnalysisApiError(
      "URL-analysen kunde inte genomföras. Kontrollera anslutningen och försök igen.",
      undefined,
      "listingAnalysisNetworkError",
    );
  }

  const { data, error, response } = result;

  if (data !== undefined) {
    return data;
  }

  if (response.status === 400 && !(error as ListingAnalysisProblemDetails | undefined)?.code) {
    throw new ListingAnalysisApiError(
      "URL:en kunde inte godkännas.",
      response.status,
      undefined,
      error as ValidationProblemDetails,
    );
  }

  const problem = error as ListingAnalysisProblemDetails | undefined;
  const messages: Record<string, string> = {
    listingSourceUnsupported: "Automatisk hämtning stöder just nu endast Blockets bilannonser på /mobility/item/.",
    listingSourceRateLimited: "Blocket har tillfälligt begränsat hämtningen. Kön är pausad. Vänta innan du fortsätter.",
    listingSourceBlocked: "Blocket nekade åtkomst till sidan. Kön är pausad och inga automatiska omförsök görs.",
    listingSourceUnavailable: "Annonsen finns inte längre eller kunde inte nås. Kontrollera adressen.",
    listingSourceInvalidContent: "Annonsens innehåll kunde inte läsas. Sidans struktur, innehållstyp eller storlek stöds inte.",
    listingAnalysisRateLimited: "URL-analysen är tillfälligt begränsad. Försök igen senare eller fyll i uppgifterna manuellt.",
    listingAnalysisNotConfigured: "Codex-extraktionen är inte konfigurerad. Du kan fortfarande fylla i uppgifterna manuellt.",
    listingAnalysisTimedOut: "URL-analysen tog för lång tid. Försök igen eller fyll i uppgifterna manuellt.",
    listingAnalysisProviderUnavailable: "URL-analysen är inte tillgänglig just nu. Du kan fortfarande fylla i uppgifterna manuellt.",
    listingAnalysisInvalidProviderResponse: "URL-analysen gav inget användbart svar. Du kan fortfarande fylla i uppgifterna manuellt.",
  };

  throw new ListingAnalysisApiError(
    (problem?.code && messages[problem.code])
      || "URL-analysen kunde inte genomföras. Kontrollera anslutningen och försök igen.",
    response.status,
    problem?.code,
    undefined,
    response.headers.has("Retry-After") ? Math.max(1, Number(response.headers.get("Retry-After")) || 60) : undefined,
  );
}

export async function listSavedListings(): Promise<SavedListingSummary[]> {
  return runSavedListingRequest(async () => {
    const { data, error, response } = await listingRequest<SavedListingSummary[]>("GET", "/api/saved-listings");
    if (data !== undefined) return data;
    throw createSavedListingError(response.status, error);
  });
}

export async function getSavedListing(vehicleId: string): Promise<SavedListingResponse> {
  return runSavedListingRequest(async () => {
    const { data, error, response } = await listingRequest<SavedListingResponse>("GET", `/api/saved-listings/${encodeURIComponent(vehicleId)}`);
    if (data !== undefined) return data;
    throw createSavedListingError(response.status, error);
  });
}

export async function getSavedListingByRegistration(
  registrationNumber: string,
): Promise<SavedListingResponse> {
  return runSavedListingRequest(async () => {
    const { data, error, response } = await listingRequest<SavedListingResponse>("GET", `/api/saved-listings/by-registration/${encodeURIComponent(registrationNumber)}`);
    if (data !== undefined) return data;
    throw createSavedListingError(response.status, error);
  });
}

export async function createSavedListing(
  request: CreateSavedListingRequest,
): Promise<SavedListingResponse> {
  return runSavedListingRequest(async () => {
    const { data, error, response } = await listingRequest<SavedListingResponse>("POST", "/api/saved-listings", request);
    if (data !== undefined) return data;
    throw createSavedListingError(response.status, error);
  });
}

export async function replaceSavedListing(
  vehicleId: string,
  request: ReplaceSavedListingRequest,
): Promise<SavedListingResponse> {
  return runSavedListingRequest(async () => {
    const { data, error, response } = await listingRequest<SavedListingResponse>("PUT", `/api/saved-listings/${encodeURIComponent(vehicleId)}`, request);
    if (data !== undefined) return data;
    throw createSavedListingError(response.status, error);
  });
}

export async function deleteSavedListing(
  vehicleId: string,
  expectedRevision: ListingNumber,
): Promise<void> {
  return runSavedListingRequest(async () => {
    const { error, response } = await listingRequest<never>("DELETE", `/api/saved-listings/${encodeURIComponent(vehicleId)}?expectedRevision=${listingNumberText(expectedRevision)}`);
    if (response.status === 204) {
      window.dispatchEvent(new CustomEvent("vehicle-deleted", { detail: vehicleId }));
      return;
    }
    throw createSavedListingError(response.status, error);
  });
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
  request: Omit<ReplaceSavedCostScenarioRequest, "expectedRevision"> & {expectedRevision: ListingNumber},
): Promise<SavedCostScenarioResponse> {
  const response = await fetch(new Request(new URL(`/api/saved-cost-scenarios/${encodeURIComponent(vehicleId)}`, window.location.origin), {
    method: "PUT", headers: {"Content-Type": "application/json"}, body: stringifyExact(request),
  }));
  const payload = await response.json();
  const data: SavedCostScenarioResponse | undefined = response.ok ? payload : undefined;
  const error = response.ok ? undefined : payload;

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
    window.dispatchEvent(new CustomEvent("vehicle-deleted", { detail: vehicleId }));
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
    listingLinkUnavailable: "Bilen har ingen sparad annons att koppla kalkylen till.",
  };

  return new SavedCostScenarioApiError(
    (problem?.code && messages[problem.code]) || "Sparade bilar kunde inte hanteras just nu.",
    status,
    undefined,
    problem,
  );
}

async function runSavedListingRequest<T>(request: () => Promise<T>): Promise<T> {
  try {
    return await request();
  } catch (error) {
    if (error instanceof SavedListingApiError) throw error;
    throw new SavedListingApiError(
      "Sparade annonser kunde inte hanteras just nu. Kontrollera anslutningen och försök igen.",
    );
  }
}

function createSavedListingError(status: number, error: unknown) {
  if (status === 400) {
    return new SavedListingApiError(
      "Annonsuppgifterna innehåller värden som inte kunde godkännas.",
      status,
      error as ValidationProblemDetails,
    );
  }

  const problem = error as SavedListingProblemDetails | undefined;
  const messages: Record<string, string> = {
    savedListingNotFound: "Den sparade annonsen finns inte längre.",
    registrationNumberConflict: "Det finns redan en sparad bil med registreringsnumret.",
    revisionConflict: "Den sparade bilen har ändrats sedan den öppnades.",
    unsupportedSavedListingVersion: "Den sparade annonsen har en version som inte kan visas.",
  };

  return new SavedListingApiError(
    (problem?.code && messages[problem.code]) || "Sparade annonser kunde inte hanteras just nu.",
    status,
    undefined,
    problem,
  );
}
