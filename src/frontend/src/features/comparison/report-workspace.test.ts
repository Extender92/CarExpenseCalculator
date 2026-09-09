import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HouseholdWorkspace } from "@/features/household/workspace";
import { householdApi } from "@/features/household/api";
import { n, stringifyExact } from "@/features/household/numbers";
import { deferred } from "@/features/household/test-fixtures";
import { ComparisonWorkspace } from "./workspace";
import {
  comparisonApi,
  ComparisonApiError,
  type ComparisonResponse,
} from "./api";
import { baseline, response, vehicleId } from "./test-fixtures";

let household: HouseholdWorkspace;
let workspace: ComparisonWorkspace;
beforeEach(() => {
  vi.useFakeTimers();
  household = new HouseholdWorkspace();
  household.setCalculationActive(false);
  workspace = new ComparisonWorkspace(household);
  workspace.connect();
  vi.spyOn(comparisonApi, "baseline").mockResolvedValue(baseline());
  vi.spyOn(comparisonApi, "preview").mockImplementation(async (request) =>
    response(request),
  );
});
afterEach(() => {
  workspace.dispose();
  household.dispose();
  vi.useRealTimers();
  vi.restoreAllMocks();
});
async function current() {
  workspace.start();
  await vi.advanceTimersByTimeAsync(501);
}

describe("report capture boundary", () => {
  it("blocks absent, empty, pending and locally malformed generations", async () => {
    expect(workspace.openReport()).toBe(false);
    await current();
    expect(workspace.openReport()).toBe(true);
    workspace.editDate("2026-09-10");
    expect(workspace.openReport()).toBe(false);
    await vi.advanceTimersByTimeAsync(501);
    household.editProfile({
      ...household.state.profile,
      purchaseCashSek: n("not a number"),
    });
    expect(workspace.openReport()).toBe(false);
    await vi.advanceTimersByTimeAsync(501);
    expect(workspace.openReport()).toBe(false);
    household.editProfile({
      ...household.state.profile,
      purchaseCashSek: n(0),
    });
    vi.mocked(comparisonApi.baseline).mockResolvedValue(baseline(0, "empty"));
    vi.mocked(comparisonApi.preview).mockImplementation(async (request) =>
      response(request, 0),
    );
    workspace.state.rulesDirty = false;
    household.state.profileDirty = false;
    await workspace.refresh();
    await vi.advanceTimersByTimeAsync(501);
    expect(workspace.reportBlockReason()).toMatch(/Lägg till bilar/);
    expect(workspace.openReport()).toBe(false);
  });
  it("blocks queued recalculations and ignores late responses without changing an existing report", async () => {
    await current();
    workspace.openReport();
    const captured = stringifyExact(workspace.state.report);
    const old = deferred<ComparisonResponse>();
    const newer = deferred<ComparisonResponse>();
    const oldRequest = vi.mocked(comparisonApi.preview).mock.calls[0][0];
    vi.mocked(comparisonApi.preview)
      .mockReturnValueOnce(old.promise)
      .mockReturnValueOnce(newer.promise);
    const a = workspace.calculate();
    const b = workspace.calculate();
    await workspace.calculate();
    expect(workspace.openReport()).toBe(false);
    old.resolve(response(oldRequest));
    newer.resolve(response(oldRequest));
    await Promise.all([a, b]);
    expect(stringifyExact(workspace.state.report)).toBe(captured);
  });
  it("blocks relevant writes and server conflicts but permits independent partial result errors", async () => {
    await current();
    const saved =
      deferred<Awaited<ReturnType<typeof householdApi.saveProfile>>>();
    vi.spyOn(householdApi, "saveProfile").mockReturnValue(saved.promise);
    const pending = household.saveProfile();
    expect(workspace.openReport()).toBe(false);
    saved.resolve({ input: household.state.profile, revision: n(2) });
    await pending;
    vi.mocked(comparisonApi.preview).mockRejectedValueOnce(
      new ComparisonApiError(409, "comparisonBaselineConflict"),
    );
    await workspace.calculate();
    expect(workspace.openReport()).toBe(false);
    vi.mocked(comparisonApi.preview).mockImplementation(async (request) => {
      const r = response(request);
      r.views.baseline.candidates[0].cost.totals.ownershipCost = {
        state: "invalid",
        completeTotalSek: null,
        knownSubtotalSek: n(50),
        errors: [
          { path: "priceSek", code: "outOfRange", message: "Invalid price" },
        ],
        missingComponents: [],
      };
      return r;
    });
    await workspace.calculate();
    expect(workspace.openReport()).toBe(true);
  });
  it("pauses automatic work, retains unsaved editing and clears only the report on return", async () => {
    await current();
    workspace.setPage(1);
    workspace.setExpanded(["Leasing"]);
    workspace.openReport();
    const frozen = workspace.state.report;
    workspace.setReportActive(true);
    household.setReportActive(true);
    vi.mocked(comparisonApi.baseline).mockClear();
    vi.mocked(comparisonApi.preview).mockClear();
    workspace.editRules({ preferences: [] });
    household.editProfile({
      ...household.state.profile,
      purchaseCashSek: n(12345),
    });
    workspace.onFocus();
    household.onFocus();
    await vi.advanceTimersByTimeAsync(1000);
    expect(comparisonApi.preview).not.toHaveBeenCalled();
    expect(comparisonApi.baseline).not.toHaveBeenCalled();
    expect(workspace.state.report).toBe(frozen);
    workspace.setReportActive(false);
    household.setReportActive(false);
    expect(workspace.state.report).toBeNull();
    expect(household.state.profile.purchaseCashSek?.text).toBe("12345");
    expect(workspace.state.rulesDirty).toBe(true);
    expect(workspace.state.expanded).toEqual(["Leasing"]);
    expect(comparisonApi.baseline).toHaveBeenCalledTimes(1);
  });
  it("invalidates the entire report on an observed member deletion, but not an unrelated deletion", async () => {
    await current();
    workspace.openReport();
    workspace.setReportActive(true);
    workspace.forget(vehicleId(9));
    expect(workspace.state.report).not.toBeNull();
    workspace.forget(vehicleId());
    expect(workspace.state.report).toBeNull();
    expect(workspace.state.reportInvalidated).toBe(true);
  });
});
