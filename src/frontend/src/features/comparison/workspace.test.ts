import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HouseholdWorkspace } from "@/features/household/workspace";
import { householdApi } from "@/features/household/api";
import { cloneExact, n } from "@/features/household/numbers";
import { deferred, savedVehicle } from "@/features/household/test-fixtures";
import { ComparisonWorkspace, localDate } from "./workspace";
import {
  comparisonApi,
  ComparisonApiError,
  type ComparisonResponse,
  type FactResponse,
  type RuleResponse,
} from "./api";
import {
  baseline,
  candidate,
  facts,
  response,
  vehicleId,
} from "./test-fixtures";

let household: HouseholdWorkspace;
let workspace: ComparisonWorkspace;
beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  household = new HouseholdWorkspace();
  household.setCalculationActive(false);
  workspace = new ComparisonWorkspace(household);
  workspace.connect();
  vi.spyOn(comparisonApi, "baseline").mockResolvedValue(baseline());
  vi.spyOn(comparisonApi, "facts").mockImplementation(async (id) =>
    facts(Number(id.slice(-12)) - 1),
  );
  vi.spyOn(comparisonApi, "preview").mockImplementation(async (request) =>
    response(request),
  );
  vi.spyOn(householdApi, "vehicle").mockImplementation(async (id) =>
    savedVehicle(id, "TAA100"),
  );
});
afterEach(() => {
  workspace.dispose();
  household.dispose();
  vi.useRealTimers();
  vi.restoreAllMocks();
});
async function stored() {
  await workspace.refresh();
  await workspace.calculate();
}
function manual() {
  workspace.setMode("manual");
  workspace.start();
  return workspace.addManual();
}

describe("comparison workspace", () => {
  it("does not silently accept an unrelated inventory change concurrent with an own save", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), { edits: { seats: { kind: "unknown" } } });
    vi.spyOn(comparisonApi, "saveRules").mockResolvedValue({
      input: {},
      revision: n(2),
      storageVersion: n(1),
    });
    vi.mocked(comparisonApi.baseline).mockResolvedValue({
      ...baseline(2, "b"),
      ruleProfileRevision: n(2),
    });
    vi.mocked(comparisonApi.preview).mockImplementation(async (request) =>
      response(request, 2),
    );
    await workspace.saveRules();
    await workspace.calculate();
    expect(workspace.state.stale).toBe(true);
    expect(workspace.state.problem?.code).toBe("comparisonBaselineConflict");
    expect(workspace.state.response?.candidateCount.text).toBe("1");
    expect(workspace.state.remote?.candidateCount.text).toBe("2");
  });
  it("bounds lazy fact reads to four even when selection changes rapidly", async () => {
    const calls: ReturnType<typeof deferred<FactResponse>>[] = [];
    vi.mocked(comparisonApi.facts).mockImplementation(() => {
      const d = deferred<FactResponse>();
      calls.push(d);
      return d.promise;
    });
    const selections = Array.from({ length: 9 }, (_, i) =>
      workspace.select(vehicleId(i)),
    );
    expect(calls).toHaveLength(4);
    for (let i = 0; i < 9; i++) {
      calls[i].resolve(facts(i));
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
    }
    await Promise.all(selections);
    expect(workspace.state.selected).toBe(vehicleId(8));
    expect(Object.keys(workspace.state.facts)).toEqual([vehicleId(8)]);
  });
  it("requires review when the initial baseline conflicts with an already edited household profile", async () => {
    household.editProfile({
      periodMonths: n(24),
      activeSensitivityMode: "baseline",
    });
    await workspace.refresh();
    expect(workspace.state.remote).not.toBeNull();
    expect(household.state.profile.periodMonths?.text).toBe("24");
  });
  it("shows refreshed fact values before replacing dirty actions", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "manual", manual: { value: n(7) } } },
    });
    vi.mocked(comparisonApi.facts).mockResolvedValue(facts(0, "2"));
    await workspace.select(vehicleId(), true);
    expect(workspace.state.facts[vehicleId()].base.revision.text).toBe("1");
    expect(workspace.state.facts[vehicleId()].remote?.revision.text).toBe("2");
    workspace.reviewFacts(vehicleId(), false);
    expect(workspace.state.facts[vehicleId()].base.revision.text).toBe("2");
    expect(workspace.state.facts[vehicleId()].dirty).toBe(false);
  });
  it("does not discard an explicit preview confirmation when saving independent facts", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), {
      costConfirmation: "confirm",
      edits: { seats: { kind: "manual", manual: { value: n(7) } } },
    });
    vi.spyOn(comparisonApi, "saveFacts").mockResolvedValue(facts(0, "2"));
    vi.mocked(comparisonApi.facts).mockResolvedValue(facts(0, "2"));
    await workspace.saveFacts(vehicleId());
    expect(workspace.state.facts[vehicleId()].input).toEqual({
      costConfirmation: "confirm",
    });
    expect(workspace.state.facts[vehicleId()].dirty).toBe(true);
    expect(
      vi.mocked(comparisonApi.saveFacts).mock.calls[0][1].costConfirmation,
    ).toBe("preserve");
  });
  it("does not move changed reference actions to the revision returned by an in-flight save", async () => {
    await stored();
    await workspace.select(vehicleId());
    const pending = deferred<FactResponse>();
    vi.spyOn(comparisonApi, "saveFacts").mockReturnValue(pending.promise);
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "manual", manual: { value: n(5) } } },
    });
    const saving = workspace.saveFacts(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: {
        seats: {
          kind: "conflict",
          observations: [
            { kind: "current", observationIndex: n(0) },
            { kind: "manual", manual: { value: n(7) } },
          ],
        },
      },
    });
    pending.resolve(facts(0, "2"));
    await saving;
    expect(workspace.state.facts[vehicleId()].remote?.revision.text).toBe("2");
    expect(workspace.state.facts[vehicleId()].base.revision.text).toBe("1");
    await workspace.calculate();
    expect(workspace.state.errors).toHaveProperty(
      `vehicle.${vehicleId()}.facts`,
    );
  });
  it("loads only the baseline for a complete stored request, not unchanged car payloads", async () => {
    await stored();
    const request = vi.mocked(comparisonApi.preview).mock.calls[0][0];
    expect(request.overrides).toEqual([]);
    expect(request.candidates).toBeUndefined();
    expect(comparisonApi.facts).not.toHaveBeenCalled();
    expect(householdApi.vehicle).not.toHaveBeenCalled();
    expect(workspace.state.stale).toBe(false);
  });
  it("starts empty, keeps an explicit local date and never inserts example rules", () => {
    expect(workspace.state.rules).toEqual({
      hardRules: [],
      preferences: [],
      signals: [],
    });
    expect(localDate(new Date(2026, 0, 1, 23, 59))).toBe("2026-01-01");
    workspace.editDate("2020-01-01");
    expect(workspace.state.date).toBe("2020-01-01");
  });
  it("debounces shared profile edits without starting household calculation or storage in manual mode", async () => {
    const preview = vi.spyOn(householdApi, "preview");
    const save = vi.spyOn(householdApi, "saveProfile");
    const id = manual();
    workspace.editManual(id, { registrationNumber: "TAA100" });
    household.editProfile({ ...household.state.profile, periodMonths: n(24) });
    await vi.advanceTimersByTimeAsync(499);
    expect(comparisonApi.preview).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(comparisonApi.preview).toHaveBeenCalledOnce();
    expect(preview).not.toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
    expect(comparisonApi.baseline).not.toHaveBeenCalled();
  });
  it("ignores reversed responses and retains old results as stale during new work", async () => {
    await stored();
    const old = workspace.state.response;
    const pending = deferred<ComparisonResponse>();
    vi.mocked(comparisonApi.preview).mockImplementationOnce(
      () => pending.promise,
    );
    const first = workspace.calculate();
    const request = vi.mocked(comparisonApi.preview).mock.calls.at(-1)![0];
    expect(workspace.state.response).toBe(old);
    expect(workspace.state.stale).toBe(true);
    workspace.editDate("2026-09-09");
    await workspace.calculate();
    const fresh = workspace.state.response;
    pending.resolve(response(request, 1, "999"));
    await first;
    expect(workspace.state.response).toBe(fresh);
    expect(fresh?.views.baseline.asOfDate).toBe("2026-09-09");
  });
  it("keeps only the latest queued generation and caps transports at two", async () => {
    await stored();
    vi.mocked(comparisonApi.preview).mockClear();
    const a = deferred<ComparisonResponse>();
    const b = deferred<ComparisonResponse>();
    vi.mocked(comparisonApi.preview)
      .mockImplementationOnce(() => a.promise)
      .mockImplementationOnce(() => b.promise);
    const first = workspace.calculate();
    const firstRequest = vi.mocked(comparisonApi.preview).mock.calls[0][0];
    const second = workspace.calculate();
    const secondRequest = vi.mocked(comparisonApi.preview).mock.calls[1][0];
    workspace.editDate("2027-01-01");
    await workspace.calculate();
    workspace.editDate("2028-01-01");
    await workspace.calculate();
    expect(comparisonApi.preview).toHaveBeenCalledTimes(2);
    a.resolve(response(firstRequest));
    await first;
    await Promise.resolve();
    expect(comparisonApi.preview).toHaveBeenCalledTimes(3);
    expect(vi.mocked(comparisonApi.preview).mock.calls[2][0].asOfDate).toBe(
      "2028-01-01",
    );
    b.resolve(response(secondRequest));
    await second;
  });
  it("does not omit a locally invalid manual candidate to recommend the remaining one", async () => {
    const a = manual();
    workspace.editManual(a, { registrationNumber: "TAA100" });
    const b = workspace.addManual();
    workspace.editManual(b, {
      registrationNumber: "TAA101",
      costInput: { candidateKey: "TAA101", priceSek: n("unfinished") },
    });
    await workspace.calculate();
    expect(comparisonApi.preview).not.toHaveBeenCalled();
    expect(workspace.state.errors).toHaveProperty(
      `vehicle.${b}.costInput.priceSek`,
    );
    expect(workspace.state.manual).toHaveLength(2);
  });
  it("forwards interpretable numeric domain errors and retains every candidate", async () => {
    const id = manual();
    workspace.editManual(id, {
      registrationNumber: "TAA100",
      costInput: { candidateKey: "TAA100", priceSek: n(-1) },
    });
    await workspace.calculate();
    expect(
      vi.mocked(comparisonApi.preview).mock.calls[0][0].candidates?.[0]
        .costInput?.priceSek?.text,
    ).toBe("-1");
  });
  it("keeps dirty rule edits when a newer server baseline is discovered", async () => {
    await stored();
    workspace.editRules({
      hardRules: [],
      preferences: [],
      signals: [{ key: "costCompleteness" }],
    });
    vi.mocked(comparisonApi.baseline).mockResolvedValue(baseline(2, "b", "2"));
    await workspace.refresh();
    expect(workspace.state.remote?.candidateCount.text).toBe("2");
    expect(workspace.state.rules.signals).toHaveLength(1);
    expect(workspace.state.baseline?.baselineToken).toBe(
      baseline().baselineToken,
    );
    await workspace.calculate();
    expect(comparisonApi.preview).toHaveBeenCalledOnce();
  });
  it("does not silently switch modes or retry after a baseline conflict", async () => {
    await stored();
    vi.mocked(comparisonApi.preview).mockRejectedValue(
      new ComparisonApiError(409, "comparisonBaselineConflict"),
    );
    await workspace.calculate();
    expect(workspace.state.mode).toBe("stored");
    expect(workspace.state.stale).toBe(true);
    expect(comparisonApi.preview).toHaveBeenCalledTimes(2);
  });
  it("keeps later rule edits when a save advances the revision", async () => {
    await stored();
    const pending = deferred<RuleResponse>();
    vi.spyOn(comparisonApi, "saveRules").mockReturnValue(pending.promise);
    const first = {
      hardRules: [],
      preferences: [],
      signals: [{ key: "costCompleteness" as const }],
    };
    workspace.editRules(first);
    const saving = workspace.saveRules();
    workspace.editRules({ ...first, signals: [{ key: "monthlyBudget" }] });
    vi.mocked(comparisonApi.baseline).mockResolvedValue({
      ...baseline(1, "b"),
      ruleProfileRevision: n(2),
      rules: first,
    });
    pending.resolve({ input: first, revision: n(2), storageVersion: n(1) });
    await saving;
    expect(workspace.state.savedRules.revision.text).toBe("2");
    expect(workspace.state.rules.signals?.[0].key).toBe("monthlyBudget");
    expect(workspace.state.rulesDirty).toBe(true);
    expect(workspace.state.remote).toBeNull();
  });
  it("keeps per-car fact editing across selection and navigation", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "manual", manual: { value: n(5) } } },
    });
    await workspace.select(vehicleId(1));
    await workspace.select(vehicleId());
    expect(
      workspace.state.facts[vehicleId()].input.edits?.seats?.manual?.value.text,
    ).toBe("5");
    expect(comparisonApi.facts).toHaveBeenCalledTimes(2);
  });
  it("does not transfer a preview confirmation to changed cost assumptions", async () => {
    const id = manual();
    workspace.editManual(id, {
      registrationNumber: "TAA100",
      costInput: { candidateKey: "TAA100", priceSek: n(40000) },
    });
    workspace.confirmPreview(id, "confirm");
    await workspace.calculate();
    expect(
      vi.mocked(comparisonApi.preview).mock.calls.at(-1)![0].candidates?.[0]
        .facts?.costConfirmation,
    ).toBe("confirm");
    workspace.editManual(id, {
      costInput: { candidateKey: "TAA100", priceSek: n(35000) },
    });
    await workspace.calculate();
    expect(
      vi.mocked(comparisonApi.preview).mock.calls.at(-1)![0].candidates?.[0]
        .facts?.costConfirmation,
    ).toBe("preserve");
  });
  it("keeps later fact edits after save, while advancing the shared vehicle revision", async () => {
    await stored();
    await workspace.select(vehicleId());
    const pending = deferred<FactResponse>();
    vi.spyOn(comparisonApi, "saveFacts").mockReturnValue(pending.promise);
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "manual", manual: { value: n(5) } } },
    });
    const saving = workspace.saveFacts(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "manual", manual: { value: n(7) } } },
    });
    vi.mocked(comparisonApi.facts).mockResolvedValue(facts(0, "2"));
    vi.mocked(comparisonApi.baseline).mockResolvedValue(baseline(1, "b"));
    pending.resolve(facts(0, "2"));
    await saving;
    expect(workspace.state.facts[vehicleId()].base.revision.text).toBe("2");
    expect(
      workspace.state.facts[vehicleId()].input.edits?.seats?.manual?.value.text,
    ).toBe("7");
    expect(workspace.state.facts[vehicleId()].dirty).toBe(true);
  });
  it("rejects reference transfer to a changed fact version during explicit refresh", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: { seats: { kind: "listing" } },
      expectedListingVersion: n(1),
    });
    vi.mocked(comparisonApi.baseline).mockResolvedValue(baseline(1, "b"));
    await workspace.refresh();
    vi.mocked(comparisonApi.facts).mockResolvedValue(facts(0, "2"));
    await workspace.acceptRemote(true);
    expect(workspace.state.remote).not.toBeNull();
    expect(workspace.state.notice).toContain("Annons- eller konfliktval");
  });
  it("deletion prevents an older response or detail read from restoring the car", async () => {
    await stored();
    const pending = deferred<ComparisonResponse>();
    vi.mocked(comparisonApi.preview).mockImplementationOnce(
      () => pending.promise,
    );
    const calculating = workspace.calculate();
    const request = vi.mocked(comparisonApi.preview).mock.calls.at(-1)![0];
    workspace.forget(vehicleId());
    pending.resolve(response(request));
    await calculating;
    expect(workspace.results()).toEqual([]);
    expect(workspace.state.facts[vehicleId()]).toBeUndefined();
  });
  it("preserves server order, pagination, and expansion over a normal recalculation", async () => {
    vi.mocked(comparisonApi.baseline).mockResolvedValue(baseline(101));
    vi.mocked(comparisonApi.preview).mockImplementation(async (request) =>
      response(request, 101),
    );
    await stored();
    workspace.setPage(3);
    workspace.setExpanded(["Känslighetsanalys"]);
    await workspace.calculate();
    expect(workspace.state.page).toBe(3);
    expect(workspace.state.expanded).toEqual(["Känslighetsanalys"]);
    workspace.setSort("score");
    expect(workspace.state.page).toBe(1);
    expect(workspace.results().map((r) => r.vehicleId)).toEqual(
      Array.from({ length: 101 }, (_, i) => candidate(i).vehicleId),
    );
  });
  it("keeps the saved-mode inputs when an explicit manual workspace is opened", async () => {
    await stored();
    await workspace.select(vehicleId());
    workspace.editFacts(vehicleId(), {
      edits: { towBar: { kind: "manual", manual: { value: false } } },
    });
    const before = cloneExact(workspace.state.facts);
    workspace.setMode("manual");
    expect(workspace.state.manual).toEqual([]);
    expect(workspace.state.facts).toEqual(before);
  });
});
