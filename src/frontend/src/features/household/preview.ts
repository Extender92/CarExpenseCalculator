import {
  bodyBytes,
  householdApi,
  HouseholdApiError,
  maximumRequestBytes,
  type Candidate,
  type InputError,
  type PreviewRequest,
  type ProfileInput,
  type VehiclePreview,
} from "./api";
import {
  validateFields,
  profileFields,
  validateVehicle,
  type FormErrors,
} from "./form-model";
import {
  normalizeRegistrationNumber,
  validateRegistrationNumber,
} from "@/features/manual-calculator/saved-scenarios";

export interface PreviewOutcome {
  result?: VehiclePreview;
  error?: string;
  fields?: FormErrors;
}
export interface PreviewGeneration {
  results: Record<string, PreviewOutcome>;
  profileErrors: FormErrors;
}

export async function limitedMap<T, R>(
  items: readonly T[],
  limit: number,
  action: (item: T, index: number) => Promise<R>,
  signal?: AbortSignal,
): Promise<R[]> {
  const results: R[] = new Array(items.length);
  let index = 0;
  await Promise.all(
    Array.from({ length: Math.min(limit, items.length) }, async () => {
      while (index < items.length) {
        if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
        const current = index++;
        results[current] = await action(items[current], current);
      }
    }),
  );
  return results;
}

export function translatedFieldError(error: InputError) {
  const messages: Record<string, string> = {
    outOfRange: "Värdet ligger utanför det tillåtna intervallet.",
    duplicateKey:
      "Postens nyckel används redan. Varje underlag måste ha en egen nyckel.",
    missingItem: "Posten saknar ett giltigt underlag.",
    invalidSensitivity:
      "Ange ett fast värde eller samtliga tre osäkerhetsvärden.",
    invalidRegistrationNumber:
      "Ange ett vanligt svenskt registreringsnummer, till exempel ABC123.",
    residualHorizonMismatch:
      "Fast restvärde gäller en annan period – ange ett nytt belopp för den valda perioden.",
    leaseHorizonMismatch:
      "Jämförelseperioden matchar inte leasingavtalets längd.",
    calculationOutOfRange: "Beloppet är för stort för beräkningen.",
    invalidLegacyTarget:
      "Välj en egen befintlig målpost för varje mappat underlag.",
  };
  return (
    messages[error.code] ??
    "Kontrollera uppgiften och dess angivna enhet eller underlag."
  );
}

export function toFormErrors(
  errors: InputError[],
  pathMap: (path: string) => string = (path) => path,
): FormErrors {
  const result: FormErrors = {};
  for (const error of errors)
    (result[pathMap(error.path)] ??= []).push(translatedFieldError(error));
  return result;
}

export function previewBatches(
  profile: ProfileInput,
  candidates: Candidate[],
  requestId: string,
): { batches: PreviewRequest[]; errors: Record<string, PreviewOutcome> } {
  const batches: PreviewRequest[] = [];
  const errors: Record<string, PreviewOutcome> = {};
  let current: Candidate[] = [];
  const make = (vehicles: Candidate[]): PreviewRequest => ({
    requestId,
    profile,
    vehicles,
  });
  const flush = () => {
    if (current.length) batches.push(make(current));
    current = [];
  };
  const identities = candidates.flatMap((candidate) => [
    candidate.input.candidateKey,
    ...(candidate.registrationNumber
      ? [
          `registration:${normalizeRegistrationNumber(candidate.registrationNumber)}`,
        ]
      : []),
  ]);
  const counts = new Map<string, number>();
  for (const identity of identities)
    counts.set(identity, (counts.get(identity) ?? 0) + 1);
  for (const candidate of candidates) {
    const key = candidate.input.candidateKey;
    const fields = validateVehicle(candidate.input);
    const registration =
      candidate.registrationNumber == null
        ? null
        : normalizeRegistrationNumber(candidate.registrationNumber);
    if (registration && validateRegistrationNumber(registration).error)
      fields.registrationNumber = ["Ange ett giltigt registreringsnummer."];
    if (Object.keys(fields).length) {
      errors[key] = {
        fields,
        error:
          "Kontrollera bilens formulär. Tidigare resultat är inte aktuella.",
      };
      continue;
    }
    if (
      (counts.get(key) ?? 0) > 1 ||
      (registration && (counts.get(`registration:${registration}`) ?? 0) > 1)
    ) {
      errors[key] = {
        error:
          "Bilen förekommer flera gånger. Öppna det befintliga alternativet.",
      };
      continue;
    }
    const input = { ...candidate, registrationNumber: registration };
    try {
      if (bodyBytes(make([input])) > maximumRequestBytes) {
        errors[key] = {
          error:
            "Bilens underlag är större än 2 MiB och kan inte förhandsvisas.",
        };
        continue;
      }
    } catch {
      errors[key] = {
        error:
          "Bilens granskningsunderlag innehåller ett tal som inte kan överföras exakt.",
      };
      continue;
    }
    if (registration == null) {
      flush();
      batches.push(make([input]));
      continue;
    }
    if (
      current.length === 100 ||
      bodyBytes(make([...current, input])) > maximumRequestBytes
    )
      flush();
    current.push(input);
  }
  flush();
  if (!candidates.length) batches.push(make([]));
  return { batches, errors };
}

export async function calculateGeneration(
  profile: ProfileInput,
  candidates: Candidate[],
  id: string,
  signal: AbortSignal,
): Promise<PreviewGeneration> {
  const profileErrors = validateFields(
    profile as Record<string, unknown>,
    profileFields,
    "profile",
  );
  if (Object.keys(profileErrors).length)
    return {
      profileErrors,
      results: Object.fromEntries(
        candidates.map((item) => [
          item.input.candidateKey,
          { error: "Profilen innehåller uppgifter som behöver rättas." },
        ]),
      ),
    };
  const { batches, errors } = previewBatches(profile, candidates, id);
  const results = { ...errors };
  await limitedMap(
    batches,
    2,
    async (batch) => {
      try {
        const response = await householdApi.preview(batch, signal);
        if (
          response.requestId !== id ||
          response.vehicles.length !== batch.vehicles.length ||
          response.vehicles.some(
            (vehicle, index) =>
              vehicle.candidateKey !== batch.vehicles[index].input.candidateKey,
          )
        )
          throw new Error("Förhandsvisningen matchar inte de begärda bilarna.");
        Object.assign(profileErrors, toFormErrors(response.profileErrors));
        for (let index = 0; index < response.vehicles.length; index++) {
          const vehicle = response.vehicles[index];
          results[vehicle.candidateKey] = {
            result: vehicle,
            fields: toFormErrors(vehicle.sections.inputErrors, inputPath),
          };
        }
      } catch (error) {
        if (signal.aborted) throw error;
        for (let index = 0; index < batch.vehicles.length; index++) {
          const fields =
            error instanceof HouseholdApiError
              ? toFormErrors(
                  error.fields.filter((field) =>
                    field.path.startsWith(`vehicles[${index}]`),
                  ),
                  inputPath,
                )
              : {};
          results[batch.vehicles[index].input.candidateKey] = {
            error: (error as Error).message,
            fields,
          };
        }
      }
    },
    signal,
  );
  return { results, profileErrors };
}

function inputPath(path: string) {
  return path
    .replace(/^vehicles\[\d+\]\.(input\.)?/, "input.")
    .replace(/(lease\.(endFees|otherPayments))\.items\[/, "$1[");
}
