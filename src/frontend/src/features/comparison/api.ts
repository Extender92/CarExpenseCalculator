import type { components } from "@/api/schema";
import {
  parseExact,
  stringifyExact,
  type Exact,
  type Numeric,
} from "@/features/household/numbers";

export type Schema<K extends keyof components["schemas"]> = Exact<
  components["schemas"][K]
>;
export type Baseline = Schema<"ComparisonBaselineResponse">;
export type Rules = Schema<"RuleProfileInput">;
export type RuleResponse = Schema<"RuleProfileResponse">;
export type FactResponse = Schema<"VehicleFactsResponse">;
export type FactWrite = Schema<"VehicleFactsWrite">;
export type FactEdits = Schema<"VehicleFactEdits">;
export type Candidate = Schema<"ComparisonCandidateRequest">;
export type ComparisonRequest = Schema<"CompleteComparisonRequest">;
export type ComparisonResponse = Schema<"CompleteComparisonResponse">;
export type View = Schema<"ComparisonPreviewResponse">;
export type Result = Schema<"ComparisonCandidateResult">;
export type ComparisonError = Schema<"ComparisonFieldError">;

const messages: Record<string, string> = {
  comparisonBaselineConflict:
    "Jämförelseunderlaget har ändrats. Läs aktuella uppgifter och granska skillnaderna innan du beräknar igen.",
  ruleProfileRevisionConflict:
    "Köpkraven har ändrats i ett annat fönster. Dina ändringar finns kvar.",
  profileRevisionConflict: "Hushållsprofilen har ändrats i ett annat fönster.",
  vehicleRevisionConflict: "Bilens underlag har ändrats sedan det öppnades.",
  listingVersionConflict:
    "Annonsen har ändrats. Granska den aktuella annonsversionen igen.",
  comparisonIdentityMismatch:
    "Bilens identitet stämmer inte med det lästa underlaget.",
  comparisonBusy:
    "Servern beräknar redan andra jämförelser. Försök igen med Beräkna nu.",
  comparisonTimedOut:
    "Jämförelsen hann inte bli färdig. Dina ändringar finns kvar; du kan försöka igen.",
  vehicleNotFound: "Bilen finns inte längre. Uppdatera jämförelseunderlaget.",
  ruleProfileNotFound: "Inga köpkrav har sparats ännu.",
  invalidResponse:
    "Ett komplett jämförelsesvar kunde inte läsas. Föregående resultat är inaktuella.",
  networkError: "Anslutningen misslyckades. Dina ändringar finns kvar.",
  payloadTooLarge:
    "Underlaget är större än serverns tillåtna begäran. Inga bilar har utelämnats eller sparats automatiskt.",
};

export class ComparisonApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly problem: Partial<
      Schema<"ComparisonProblemDetails"> &
        Schema<"ComparisonValidationProblemDetails">
    > = {},
  ) {
    super(
      messages[code] ??
        (status === 400
          ? "Kontrollera de angivna uppgifterna."
          : status === 503
            ? "Sparade jämförelseunderlag kan inte läsas just nu. Dina ändringar finns kvar."
            : "Åtgärden kunde inte genomföras. Dina ändringar finns kvar."),
    );
  }
}

export const jsonBytes = (json: string) =>
  new TextEncoder().encode(json).byteLength;
export const writeLimit = 2 * 1024 * 1024;

async function request<T>(
  route: string,
  method: string,
  body?: unknown,
  signal?: AbortSignal,
  complete = false,
): Promise<T> {
  const json = body === undefined ? undefined : stringifyExact(body);
  // preview-all has a deployment-configurable limit. Do not impose the default
  // 32 MiB in the browser, which cannot know whether the operator increased it.
  if (json !== undefined && !complete && jsonBytes(json) > writeLimit)
    throw new ComparisonApiError(413, "payloadTooLarge");
  let response: Response;
  try {
    response = await fetch(route, {
      method,
      signal,
      body: json,
      headers:
        json === undefined ? undefined : { "Content-Type": "application/json" },
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new ComparisonApiError(0, "networkError");
  }
  let raw: string;
  try {
    raw = await response.text();
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new ComparisonApiError(response.status, "invalidResponse");
  }
  if (signal?.aborted) throw new DOMException("Avbrutet", "AbortError");
  let parsed: unknown;
  try {
    parsed = parseExact(raw);
  } catch {
    throw new ComparisonApiError(response.status, "invalidResponse");
  }
  if (!response.ok) {
    const problem = parsed as ComparisonApiError["problem"];
    throw new ComparisonApiError(
      response.status,
      problem?.code ?? "invalidResponse",
      problem,
    );
  }
  return parsed as T;
}

export const comparisonApi = {
  baseline: (signal?: AbortSignal) =>
    request<Baseline>("/api/comparisons/baseline", "GET", undefined, signal),
  preview: (body: ComparisonRequest, signal?: AbortSignal) =>
    request<ComparisonResponse>(
      "/api/comparisons/preview-all",
      "POST",
      body,
      signal,
      true,
    ),
  rules: (signal?: AbortSignal) =>
    request<RuleResponse>("/api/rule-profile", "GET", undefined, signal),
  saveRules: (input: Rules, expectedRevision: Numeric) =>
    request<RuleResponse>("/api/rule-profile", "PUT", {
      input,
      expectedRevision,
    }),
  facts: (id: string, signal?: AbortSignal) =>
    request<FactResponse>(
      `/api/vehicle-facts/${encodeURIComponent(id)}`,
      "GET",
      undefined,
      signal,
    ),
  saveFacts: (id: string, input: FactWrite, expectedRevision: Numeric) =>
    request<FactResponse>(
      `/api/vehicle-facts/${encodeURIComponent(id)}`,
      "PUT",
      { input, expectedRevision },
    ),
};
