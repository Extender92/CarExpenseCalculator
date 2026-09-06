import type { components, paths } from "@/api/schema";
import {
  type Exact,
  type Numeric,
  n,
  parseExact,
  stringifyExact,
} from "./numbers";

type Schema<K extends keyof components["schemas"]> = Exact<
  components["schemas"][K]
>;
export type ProfileInput = Schema<"HouseholdProfileInput">;
export type VehicleInput = Schema<"VehicleCostInput">;
export type CostWrite = Schema<"VehicleCostWrite">;
export type VehicleResponse = Schema<"VehicleCostInputResponse">;
export type VehicleSummary = Schema<"VehicleCostInputSummary">;
export type ProfileResponse = Schema<"HouseholdProfileResponse">;
export type DraftInput = Schema<"VehicleDraftInput">;
export type DraftResponse = Schema<"VehicleDraftResponse">;
export type TransitionResponse = Schema<"HouseholdTransitionResponse">;
export type TransitionRequest = Schema<"ConfirmHouseholdTransitionRequest">;
export type PreviewRequest = Schema<"HouseholdPreviewRequest">;
export type PreviewResponse = Schema<"HouseholdPreviewResponse">;
export type Candidate = Schema<"HouseholdPreviewCandidate">;
export type VehiclePreview = Schema<"HouseholdVehiclePreview">;
export type SectionResult = Schema<"CostSectionResult">;
export type LegacyReview = Schema<"LegacyReviewResponse">;
export type LegacyDecision = Schema<"LegacyItemDecision">;
export type InputError = Schema<"HouseholdInputError">;
export type Sensitivity = Schema<"SensitivityValue">;
export type CostItem = Schema<"HouseholdCostItem">;
export type LeaseCharge = Schema<"HouseholdLeaseCharge">;
export type ListingResponse = Schema<"SavedListingResponse">;
export type ReviewedListing = Schema<"ReviewedListingInput">;

export const maximumRequestBytes = 2 * 1024 * 1024;
export function bodyBytes(body: unknown) {
  return new TextEncoder().encode(stringifyExact(body)).byteLength;
}

export class HouseholdApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly fields: InputError[] = [],
    public readonly actualRevision?: Numeric | null,
    public readonly vehicleId?: string | null,
    public readonly recoveryRoute?: string | null,
  ) {
    super(
      messages[code] ??
        (status === 400
          ? "Kontrollera de angivna uppgifterna."
          : "Anropet kunde inte genomföras. Dina ändringar finns kvar."),
    );
  }
}

const messages: Record<string, string> = {
  profileNotFound: "Ingen hushållsprofil har sparats ännu.",
  vehicleNotFound:
    "Bilen finns inte längre. Öppna ett nytt underlag för att lägga till en bil.",
  draftEmpty: "Det sparade utkastet finns inte längre.",
  householdStorageUnavailable:
    "Sparade uppgifter kan inte läsas just nu. Du kan fortfarande göra en manuell förhandsvisning.",
  networkError:
    "Anslutningen misslyckades. Dina ändringar finns kvar. Kontrollera serverläget innan du försöker spara igen.",
  profileRevisionConflict:
    "Hushållsprofilen har ändrats i ett annat fönster. Läs aktuella uppgifter och granska skillnaderna.",
  vehicleRevisionConflict:
    "Bilen har ändrats sedan den öppnades. Läs aktuella uppgifter och granska skillnaderna.",
  draftRevisionConflict:
    "Det gemensamma utkastet har ändrats. Läs det aktuella utkastet innan du gör ett nytt val.",
  transitionRevisionConflict:
    "Underlaget för övergången har ändrats. Läs det aktuella beståndet och granska det igen.",
  transitionSetConflict:
    "Beståndet av äldre kalkyler har ändrats. Övergången har inte genomförts.",
  registrationNumberConflict:
    "Registreringsnumret tillhör redan en sparad bil. Öppna den bilen och granska underlaget.",
  householdTransitionRequired:
    "Äldre kalkyler behöver granskas i hushållsflödet innan denna ändring kan sparas.",
  draftReplacementRequired:
    "Det finns ett utkast för en annan bil. Välj uttryckligen om det ska ersättas.",
  draftIdentityMismatch:
    "Utkastets bil stämmer inte längre med det sparade underlaget. Öppna och granska bilen igen.",
  listingRequired: "Bilen saknar den annons som du vill bekräfta.",
  listingLabelRequiresReview:
    "Bilens namn behöver ändras genom granskning av annonsen.",
  unsupportedHouseholdInputVersion:
    "Underlaget har ett lagringsformat som denna version inte kan läsa.",
  payloadTooLarge:
    "Underlaget är större än 2 MiB. Det har inte skickats eller delats automatiskt. Minska underlaget och försök igen.",
};

async function request<T>(
  route: keyof paths,
  method: string,
  body?: unknown,
  signal?: AbortSignal,
  suffix = "",
): Promise<Exact<T>> {
  const json = body === undefined ? undefined : stringifyExact(body);
  if (json && new TextEncoder().encode(json).byteLength > maximumRequestBytes)
    throw new HouseholdApiError(413, "payloadTooLarge");
  let response: Response;
  try {
    response = await fetch(`${route}${suffix}`, {
      method,
      signal,
      headers:
        json === undefined ? undefined : { "Content-Type": "application/json" },
      body: json,
    });
  } catch (error) {
    if (
      signal?.aborted ||
      (error instanceof DOMException && error.name === "AbortError")
    )
      throw error;
    throw new HouseholdApiError(0, "networkError");
  }
  if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
  if (response.status === 204) return undefined as Exact<T>;
  const raw = await response.text();
  if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
  if (!response.ok) {
    let problem: Schema<"HouseholdProblemDetails"> &
      Partial<Schema<"HouseholdValidationProblemDetails">>;
    try {
      problem = parseExact(raw);
    } catch {
      throw new HouseholdApiError(response.status, "invalidResponse");
    }
    throw new HouseholdApiError(
      response.status,
      problem.code ?? "invalidResponse",
      problem.fieldErrors ?? [],
      problem.actualRevision,
      problem.vehicleId,
      problem.recoveryRoute,
    );
  }
  try {
    return parseExact<T>(raw);
  } catch {
    throw new HouseholdApiError(503, "invalidResponse");
  }
}

export const householdApi = {
  preview: (body: PreviewRequest, signal?: AbortSignal) =>
    request<components["schemas"]["HouseholdPreviewResponse"]>(
      "/api/household-calculations/preview",
      "POST",
      body,
      signal,
    ),
  profile: (signal?: AbortSignal) =>
    request<components["schemas"]["HouseholdProfileResponse"]>(
      "/api/household-profile",
      "GET",
      undefined,
      signal,
    ),
  saveProfile: (input: ProfileInput, expectedRevision: Numeric) =>
    request<components["schemas"]["HouseholdProfileResponse"]>(
      "/api/household-profile",
      "PUT",
      { input, expectedRevision },
    ),
  list: (signal?: AbortSignal) =>
    request<components["schemas"]["VehicleCostInputSummary"][]>(
      "/api/vehicle-cost-inputs",
      "GET",
      undefined,
      signal,
    ),
  vehicle: (id: string, signal?: AbortSignal) =>
    vehicleRequest<components["schemas"]["VehicleCostInputResponse"]>(
      id,
      "GET",
      undefined,
      signal,
    ),
  create: (registrationNumber: string, cost: CostWrite) =>
    request<components["schemas"]["VehicleCostInputResponse"]>(
      "/api/vehicle-cost-inputs",
      "POST",
      { registrationNumber, cost },
    ),
  replace: (id: string, expectedRevision: Numeric, cost: CostWrite) =>
    vehicleRequest<components["schemas"]["VehicleCostInputResponse"]>(
      id,
      "PUT",
      { expectedRevision, cost },
    ),
  delete: (id: string, expectedRevision: Numeric) =>
    vehicleRequest<void>(
      id,
      "DELETE",
      undefined,
      undefined,
      `?expectedRevision=${encodeURIComponent(expectedRevision.text)}`,
    ),
  draft: (signal?: AbortSignal) =>
    request<components["schemas"]["VehicleDraftResponse"]>(
      "/api/vehicle-draft",
      "GET",
      undefined,
      signal,
    ),
  saveDraft: (
    input: DraftInput,
    expectedRevision: Numeric,
    replaceExisting = false,
  ) =>
    request<components["schemas"]["VehicleDraftResponse"]>(
      "/api/vehicle-draft",
      "PUT",
      { input, expectedRevision, replaceExisting },
    ),
  deleteDraft: (revision: Numeric) =>
    request<components["schemas"]["VehicleDraftResponse"]>(
      "/api/vehicle-draft",
      "DELETE",
      undefined,
      undefined,
      `?expectedRevision=${encodeURIComponent(revision.text)}`,
    ),
  adopt: (expectedRevision: Numeric) =>
    request<components["schemas"]["VehicleCostInputResponse"]>(
      "/api/vehicle-draft/adopt",
      "POST",
      { expectedRevision },
    ),
  transition: (signal?: AbortSignal) =>
    request<components["schemas"]["HouseholdTransitionResponse"]>(
      "/api/household-transition",
      "GET",
      undefined,
      signal,
    ),
  confirmTransition: (body: TransitionRequest) =>
    request<components["schemas"]["HouseholdTransitionResponse"]>(
      "/api/household-transition",
      "POST",
      body,
    ),
  listing: async (
    id: string,
    signal?: AbortSignal,
  ): Promise<ListingResponse> => {
    validateId(id);
    // The same exact codec is used when listing values enter household editing.
    return request<components["schemas"]["SavedListingResponse"]>(
      `/api/saved-listings/${id}` as keyof paths,
      "GET",
      undefined,
      signal,
    );
  },
};

function validateId(id: string) {
  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  )
    throw new HouseholdApiError(400, "invalidVehicleId");
}
function vehicleRequest<T>(
  id: string,
  method: string,
  body?: unknown,
  signal?: AbortSignal,
  suffix = "",
) {
  validateId(id);
  return request<T>(
    `/api/vehicle-cost-inputs/${id}` as keyof paths,
    method,
    body,
    signal,
    suffix,
  );
}
export const emptyProfileResponse = (): ProfileResponse => ({
  input: null,
  revision: n(0),
});
