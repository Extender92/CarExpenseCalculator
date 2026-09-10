import { cloneExact, n } from "@/features/household/numbers";
import { previewVehicle, profile } from "@/features/household/test-fixtures";
import { criteria } from "./catalogue";
import type {
  Baseline,
  Candidate,
  ComparisonRequest,
  ComparisonResponse,
  FactResponse,
  Result,
  Schema,
  View,
} from "./api";

export const vehicleId = (index = 0) =>
  `65000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`;
export const candidate = (index = 0): Candidate => ({
  vehicleId: vehicleId(index),
  registrationNumber: `TAA${String(index + 100)}`,
  facts: {},
});
export const baseline = (count = 1, token = "a", revision = "1"): Baseline => ({
  profile: profile(),
  householdProfileRevision: n(revision),
  rules: { hardRules: [], preferences: [], signals: [] },
  ruleProfileRevision: n(revision),
  candidateCount: n(count),
  baselineToken: `v1:${token.repeat(64)}`,
  transportVersion: n(1),
});
export function unknownFacts(): Schema<"ComparisonFactSet"> {
  const keys = [
    ...criteria.flatMap((c) => (c.fact ? [c.fact] : [])),
    "lastServiceDate",
    "lastServiceOdometerKilometres",
    "serviceNotes",
  ];
  return {
    facts: Object.fromEntries(
      keys.map((key) => [key, { state: "unknown", observations: [] }]),
    ) as unknown as Schema<"VehicleComparisonFacts">,
    conditionNotes: null,
  };
}
export function facts(index = 0, revision = "1"): FactResponse {
  return {
    vehicleId: vehicleId(index),
    registrationNumber: candidate(index).registrationNumber,
    revision: n(revision),
    input: unknownFacts(),
    listingProposal: null,
    currentListingVersion: null,
    factsReviewedListingVersion: null,
    costReviewedListingVersion: null,
    needsListingReview: false,
    costConfirmedAt: null,
    storageVersion: n(1),
  };
}
export function result(c = candidate(), amount = "100.00"): Result {
  const input = c.costInput ?? {
    candidateKey: c.registrationNumber,
    acquisitionType: "purchase",
    priceSek: n(40000),
  };
  return {
    vehicleId: c.vehicleId,
    registrationNumber: c.registrationNumber,
    effectiveFacts: unknownFacts(),
    effectiveCostInput: c.costInput ?? null,
    costConfirmedAt: null,
    unresolvedLegacyItems: [],
    sourceRevisions: {
      householdProfile: n(1),
      ruleProfile: n(1),
      vehicle: n(1),
      listing: null,
      factsReviewedListing: null,
      costReviewedListing: null,
    },
    needsListingReview: false,
    storedInputState: "current",
    unsaved: {
      profile: false,
      rules: false,
      facts: false,
      costs: false,
      reviewDecisions: false,
      costConfirmation: false,
      listingReview: false,
    },
    cost: previewVehicle(
      {
        input,
        registrationNumber: c.registrationNumber,
        unresolvedLegacyItems: [],
      },
      amount,
    ).sections,
    errors: [],
    hardRules: [],
    eligibility: "eligible",
    contributions: [],
    score: { lower: n(85), upper: n(85) },
    coveragePercent: n(100),
    scoreUnavailableReason: null,
    signals: [],
    isCheapestEligibleComplete: false,
    isDefinitePreferenceWinner: false,
  };
}
export function response(
  request: ComparisonRequest,
  count = request.candidates?.length ?? 1,
  amount = "100.00",
): ComparisonResponse {
  const candidates =
    request.candidates ?? Array.from({ length: count }, (_, i) => candidate(i));
  const results = candidates.map((c) => result(c, amount));
  if (request.mode === "stored")
    results.forEach((r) => {
      r.sourceRevisions.householdProfile =
        request.storedBase!.householdProfileRevision;
      r.sourceRevisions.ruleProfile = request.storedBase!.ruleProfileRevision;
    });
  const view = (mode: "baseline" | "favorable" | "cautious"): View => ({
    requestId: request.requestId,
    mode: request.mode,
    storageChecked: request.mode === "stored",
    profile: { ...cloneExact(request.profile), activeSensitivityMode: mode },
    rules: cloneExact(request.rules),
    asOfDate: request.asOfDate,
    ruleVersion: n(1),
    resultSchemaVersion: n(1),
    calculationVersion: n(2),
    householdResultSchemaVersion: n(2),
    profileErrors: [],
    candidates: cloneExact(results),
    costOrder: results.map((r) => r.vehicleId),
    scoreOrder: results.map((r) => r.vehicleId),
    preferenceRecommendationReason: "overlapOrTie",
  });
  return {
    requestId: request.requestId,
    generationId: "65000000-0000-4000-9000-000000000000",
    mode: request.mode,
    baselineToken: request.storedBase?.baselineToken ?? null,
    candidateCount: n(count),
    activeSensitivityMode: request.profile.activeSensitivityMode ?? "baseline",
    views: {
      baseline: view("baseline"),
      favorable: view("favorable"),
      cautious: view("cautious"),
    },
    transportVersion: n(2),
    listings: [],
  };
}
export const manualRequest = (count = 1): ComparisonRequest => ({
  mode: "manual",
  requestId: "fixture",
  profile: profile(),
  rules: {},
  asOfDate: "2026-09-08",
  candidates: Array.from({ length: count }, (_, i) => candidate(i)),
});
