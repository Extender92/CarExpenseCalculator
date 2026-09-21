import { afterEach, describe, expect, it, vi } from "vitest";
import { householdApi } from "@/features/household/api";
import { comparisonApi } from "@/features/comparison/api";
import { facts } from "@/features/comparison/test-fixtures";
import { savedVehicle } from "@/features/household/test-fixtures";
import { cloneExact, n } from "@/features/household/numbers";
import { savedListingResponse } from "@/test/listing-analysis";
import { savedListingToReviewState } from "./saved-listings";
import { newCarWorkflow, saveCarCostsAndFacts } from "./batch-workflow";
import type { ListingWorkspaceItem } from "./review-model";

function item(): ListingWorkspaceItem {
  return { ...savedListingToReviewState(savedListingResponse), id: "one", dirty: false, error: null,
    persistenceNotice: null, saving: false, validationErrors: {}, controller: null,
    workflow: { ...newCarWorkflow(), cost: { candidateKey: "manual", acquisitionType: "purchase", priceSek: n(20000) },
      facts: { edits: { towBar: { kind: "listing" } } } } };
}
afterEach(() => vi.restoreAllMocks());
describe("per-car batch writes", () => {
  it("uses the acknowledged cost revision for facts and never confirms costs", async () => {
    const car = item();
    const costs = vi.spyOn(householdApi, "replace").mockResolvedValue(savedVehicle(car.saved!.vehicleId, "ABC123", "2"));
    const factWrite = vi.spyOn(comparisonApi, "saveFacts").mockResolvedValue(facts(0, "3"));
    const checkpoint = vi.fn();
    await saveCarCostsAndFacts(car, checkpoint);
    expect(costs).toHaveBeenCalledTimes(1);
    expect(factWrite.mock.calls[0][2].text).toBe("2");
    expect(factWrite.mock.calls[0][1]).toMatchObject({ costConfirmation: "preserve", edits: { towBar: { kind: "listing" } } });
    expect(checkpoint.mock.lastCall![0]).toMatchObject({ stage: "done", costsSaved: true, factsSaved: true });
    expect(car.workflow!.costsSaved).toBeUndefined();
  });
  it("keeps a successful cost write on fact failure and resumes only the unfinished step on explicit retry", async () => {
    const car = item();
    const costs = vi.spyOn(householdApi, "replace").mockResolvedValue(savedVehicle(car.saved!.vehicleId, "ABC123", "2"));
    const factWrite = vi.spyOn(comparisonApi, "saveFacts").mockRejectedValueOnce(new Error("Konflikt"));
    const checkpoint = vi.fn((workflow, revision) => { car.workflow = cloneExact(workflow); car.saved!.revision = revision; });
    await expect(saveCarCostsAndFacts(cloneExact(car), checkpoint)).rejects.toThrow("Konflikt");
    expect(costs).toHaveBeenCalledTimes(1); expect(factWrite).toHaveBeenCalledTimes(1);
    expect(car.workflow).toMatchObject({ costsSaved: true, error: "Konflikt", stage: "facts" });
    factWrite.mockResolvedValueOnce(facts(0, "3"));
    await saveCarCostsAndFacts(cloneExact(car), checkpoint);
    expect(costs).toHaveBeenCalledTimes(1); expect(factWrite).toHaveBeenCalledTimes(2);
    expect(car.workflow).toMatchObject({ stage: "done", factsSaved: true });
  });
  it("does not write facts after a failed cost step or write anything for a registration-free draft", async () => {
    const costs = vi.spyOn(householdApi, "replace").mockRejectedValue(new Error("Kostnadsfel"));
    const factWrite = vi.spyOn(comparisonApi, "saveFacts");
    await expect(saveCarCostsAndFacts(item(), vi.fn())).rejects.toThrow("Kostnadsfel");
    expect(factWrite).not.toHaveBeenCalled();
    const draft = item(); draft.saved = null;
    await saveCarCostsAndFacts(draft, vi.fn());
    expect(costs).toHaveBeenCalledTimes(1);
  });
});
