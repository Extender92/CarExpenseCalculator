import type { VehicleInput } from "@/features/household/api";
import { householdApi, request } from "@/features/household/api";
import { comparisonApi, type FactWrite } from "@/features/comparison/api";
import { applyListingReuse, bindReuseSources, type ReusePreview } from "@/features/household/apply-listing-reuse";
import { cloneExact, fromOrdinary, n, stringifyExact, type Numeric } from "@/features/household/numbers";
import { buildReviewedListingInput } from "./saved-listings";
import type { ListingWorkspaceItem } from "./review-model";
import { listingNumberText } from "./exact";
import { acknowledgeRevision, vehicleChanged } from "@/lib/vehicle-events";

export interface CarWorkflow {
  selected: boolean;
  existing?: boolean;
  writing?: boolean;
  preparedListing?: string;
  costBaseline?: VehicleInput;
  costBaselineSaved?: boolean;
  factsBaseline?: FactWrite;
  factsBaselineSaved?: boolean;
  preparing?: boolean;
  error?: string;
  cost: VehicleInput;
  facts: FactWrite;
  proposal?: ReusePreview["facts"];
  applied: string[];
  warnings: string[];
  costsSaved?: boolean;
  factsSaved?: boolean;
  stage?: "listing" | "cost" | "facts" | "done";
}
export const newCarWorkflow = (): CarWorkflow => ({ selected: true,
  cost: { candidateKey: "manual", acquisitionType: "purchase" }, facts: {}, applied: [], warnings: [] });

export async function prepareCar(item: ListingWorkspaceItem): Promise<CarWorkflow> {
  const current = item.workflow ?? newCarWorkflow();
  const listing = buildReviewedListingInput(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
  const preview = await request<import("@/api/schema").components["schemas"]["ListingReusePreviewResponse"]>(
    "/api/listing-reuse/preview", "POST", { unsavedListing: fromOrdinary(listing), target: current.cost, factEdits: current.facts.edits });
  const applied = current.preparedListing
    ? reconcileReuseSources(current.cost, current.facts, preview as ReusePreview)
    : applyListingReuse(current.cost, current.facts, preview as ReusePreview);
  return { ...current, cost: applied.cost, facts: applied.facts, proposal: preview.facts, applied: current.preparedListing ? current.applied : applied.applied,
    warnings: preview.warnings, preparing: false, error: undefined,
    preparedListing: JSON.stringify(item.draft),
    costBaseline: current.costBaseline ?? cloneExact(applied.cost),
    factsBaseline: current.factsBaseline ?? cloneExact(applied.facts) };
}

/** Each successful write is checkpointed immediately. No retries, rollback, or shared editor mutation. */
export async function saveCarCostsAndFacts(item: ListingWorkspaceItem, checkpoint: (workflow: CarWorkflow, revision: Numeric) => void) {
  if (!item.saved || !item.workflow || item.workflow.existing) return;
  const saved = item.saved;
  let workflow = cloneExact(item.workflow);
  let revision = n(listingNumberText(saved.revision));
  const version = n(listingNumberText(saved.listingVersion));
  const publish = (next: Numeric) => {
    acknowledgeRevision({ vehicleId: saved.vehicleId, previous: revision, current: next });
    revision = next; checkpoint(workflow, revision);
  };
  try {
    if (!workflow.costsSaved) {
      workflow.stage = "cost"; checkpoint(workflow, revision);
      const cost = bindReuseSources({ ...workflow.cost, candidateKey: saved.registrationNumber }, version);
      const response = await householdApi.replace(saved.vehicleId, revision, { input: cost, listingLinkMode: "current" });
      workflow = { ...workflow, cost, costBaseline: cloneExact(cost), costBaselineSaved: true, costsSaved: true }; publish(response.revision);
    }
    if (!workflow.factsSaved) {
      workflow.stage = "facts"; checkpoint(workflow, revision);
      const response = await comparisonApi.saveFacts(saved.vehicleId, {
        ...workflow.facts, expectedListingVersion: version, costConfirmation: "preserve",
      }, revision);
      workflow = { ...workflow, factsBaseline: cloneExact(workflow.facts), factsBaselineSaved: true, factsSaved: true }; publish(response.revision);
    }
    workflow = { ...workflow, stage: "done", error: undefined }; checkpoint(workflow, revision);
    vehicleChanged(saved.vehicleId);
  } catch (error) {
    checkpoint({ ...workflow, error: (error as Error).message }, revision);
    throw error;
  }
}

/** Rechecking an edited listing never fills cleared targets or changes chosen costs.
 * A source claim is removed when the edited advertisement no longer supports it. */
export function reconcileReuseSources(input: VehicleInput, facts: FactWrite, preview: ReusePreview) {
  const cost = cloneExact(input);
  const nextFacts = cloneExact(facts);
  const equal = (a: unknown, b: unknown) => stringifyExact(a) === stringifyExact(b);
  if (cost.priceSource && !equal(cost.priceSek, preview.purchasePrice?.value)) cost.priceSource = null;
  if (nextFacts.edits?.purchasePriceSek?.kind === "listing" && cost.priceSek != null &&
      !equal(cost.priceSek, preview.purchasePrice?.value))
    nextFacts.edits.purchasePriceSek = { kind: "editManual", manual: { value: cost.priceSek } };
  cost.energySources = cost.energySources?.map(row => {
    const proposal = preview.energySources.find(candidate => candidate.key === row.key)?.value;
    return { ...row,
      fuelSource: row.fuelSource && (!proposal || row.fuel !== proposal.fuel || row.unit !== proposal.unit) ? null : row.fuelSource,
      consumptionSource: row.consumptionSource && (!proposal || !equal(row.consumptionPer100Kilometres, proposal.consumptionPer100Kilometres) ||
        row.unit !== proposal.unit || row.consumptionLabel !== proposal.consumptionLabel ||
        row.consumptionBasis !== proposal.consumptionBasis || row.electricityBasis !== proposal.electricityBasis)
        ? null : row.consumptionSource };
  });
  if (cost.tax) cost.tax.items = cost.tax.items?.map(row => ({ ...row,
    listingSource: row.listingSource && (!preview.annualTax || !equal(row.amountSek, preview.annualTax.value.amountSek) ||
      row.cadence !== preview.annualTax.value.cadence) ? null : row.listingSource }));
  return { cost, facts: nextFacts, applied: [] as string[] };
}

export function workflowPending(item: ListingWorkspaceItem) {
  return !!item.saved && !!item.workflow && !item.workflow.existing &&
    (!item.workflow.costsSaved || !item.workflow.factsSaved);
}
