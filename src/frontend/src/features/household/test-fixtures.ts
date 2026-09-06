import type {
  Candidate,
  PreviewRequest,
  PreviewResponse,
  SectionResult,
  VehiclePreview,
  VehicleResponse,
  VehicleSummary,
} from "./api";
import { initialProfile, initialVehicle } from "./form-model";
import { n } from "./numbers";

export const id1 = "10000000-0000-0000-0000-000000000001";
export const id2 = "10000000-0000-0000-0000-000000000002";
export function savedVehicle(
  id = id1,
  registration = "ABC123",
  revision = "1",
): VehicleResponse {
  return {
    vehicleId: id,
    registrationNumber: registration,
    vehicleLabel: null,
    revision: n(revision),
    state: "current",
    input: {
      ...initialVehicle(),
      candidateKey: registration,
      priceSek: n(100000),
    },
    legacy: null,
    unresolvedLegacyItems: [],
    sourceListingVersion: null,
    currentListingVersion: null,
    needsListingReview: false,
    createdAtUtc: "2026-09-01T00:00:00Z",
    updatedAtUtc: "2026-09-01T00:00:00Z",
  };
}
export function summary(vehicle: VehicleResponse): VehicleSummary {
  return { ...vehicle, reviewItems: vehicle.unresolvedLegacyItems };
}
export function candidate(index: number): Candidate {
  const registration = `ABC${String(index).padStart(3, "0")}`;
  return {
    registrationNumber: registration,
    input: {
      candidateKey: registration,
      acquisitionType: "purchase",
      priceSek: n(index),
    },
    unresolvedLegacyItems: [],
  };
}
export function section(amount: string | number = 0): SectionResult {
  return {
    state: "complete",
    completeTotalSek: n(amount),
    knownSubtotalSek: n(amount),
    missingComponents: [],
    errors: [],
  };
}
export function previewVehicle(
  candidate: Candidate,
  amount = "100",
): VehiclePreview {
  const category = () => ({ isIncluded: false, cost: section(), items: [] });
  return {
    candidateKey: candidate.input.candidateKey,
    registrationNumber: candidate.registrationNumber ?? null,
    input: candidate.input,
    unresolvedLegacyItems: [],
    isCostComparable: true,
    sections: {
      candidateKey: candidate.input.candidateKey,
      acquisitionType: candidate.input.acquisitionType ?? "purchase",
      financingDetails: null,
      financing: section(),
      depreciation: { cost: section(), residualValueSek: n(1000) },
      energy: { cost: section(), isIncluded: false, sources: [] },
      tax: category(),
      insurance: category(),
      service: category(),
      repairs: category(),
      customCosts: category(),
      repairAllowance: section(),
      totals: {
        distanceKilometres: n(1000),
        ownershipCost: section(amount),
        monthlyCost: section(),
        costPerMil: section(),
        endEquity: section(),
      },
      inputErrors: [],
      lease: {
        cost: section(),
        termMonths: null,
        coveredMonths: null,
        isEstimate: false,
        excessDistanceKilometres: null,
        depositWithheld: section(),
      },
      payments: {
        requestedMonths: n(12),
        coveredMonths: n(12),
        calendarStatus: section(),
        months: [],
        sources: [],
        externalOutflow: section(),
        externalInflow: section(),
        netExternalCashFlow: section(),
        internalSaving: section(),
      },
      startupBudget: {
        status: "notConfigured",
        limitSek: null,
        fundingRequired: section(),
      },
      monthlyBudget: {
        status: "withinLimit",
        limitSek: n(100),
        fundingRequired: section("100.00"),
      },
      reconciliation: {
        purchaseCash: section(),
        principalRepaid: section(),
        depreciation: section(),
        accruedOperatingCosts: section(),
        paidOperatingCosts: section(),
        depositPaid: section(),
        depositRefund: section(),
        depositWithheld: section(),
        repairAllowance: section(),
        reconciledOwnershipCost: section(),
      },
    },
  };
}
export function previewResponse(
  request: PreviewRequest,
  amount = "100",
): PreviewResponse {
  return {
    requestId: request.requestId,
    profile: request.profile,
    currency: "SEK",
    calculationVersion: n(2),
    resultSchemaVersion: n(2),
    activeSensitivityMode: request.profile.activeSensitivityMode ?? "baseline",
    profileErrors: [],
    vehicles: request.vehicles.map((vehicle) =>
      previewVehicle(vehicle, amount),
    ),
  };
}
export const profile = () => ({
  ...initialProfile(),
  periodMonths: n(12),
  annualDistanceKilometres: n(15000),
  purchaseCashSek: n(100000),
});
export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((ok, fail) => {
    resolve = ok;
    reject = fail;
  });
  return { promise, resolve, reject };
}
