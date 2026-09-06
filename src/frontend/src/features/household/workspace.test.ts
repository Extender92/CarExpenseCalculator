import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  householdApi,
  HouseholdApiError,
  type DraftResponse,
  type PreviewResponse,
  type ProfileResponse,
  type VehicleResponse,
} from "./api";
import { HouseholdWorkspace } from "./workspace";
import { cloneExact, n } from "./numbers";
import {
  deferred,
  id1,
  id2,
  previewResponse,
  profile,
  savedVehicle,
  summary,
} from "./test-fixtures";

let workspace: HouseholdWorkspace;
beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  workspace = new HouseholdWorkspace();
  vi.spyOn(householdApi, "profile").mockResolvedValue({
    input: null,
    revision: n(0),
  });
  vi.spyOn(householdApi, "list").mockResolvedValue([]);
  vi.spyOn(householdApi, "draft").mockResolvedValue({
    input: null,
    revision: n(0),
  });
  vi.spyOn(householdApi, "preview").mockImplementation(async (request) =>
    previewResponse(request),
  );
});
afterEach(() => {
  workspace.dispose();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("household workspace generations and separate save baselines", () => {
  it("does not replace a newer registration entered while create is pending", async () => {
    const pending = deferred<VehicleResponse>();
    vi.spyOn(householdApi, "create").mockReturnValue(pending.promise);
    workspace.editActive({ registrationNumber: "ABC123" });
    const operation = workspace.saveVehicle();
    workspace.editActive({ registrationNumber: "DEF456" });
    pending.resolve(savedVehicle());
    await operation;
    expect(workspace.state.active.registrationNumber).toBe("DEF456");
    expect(workspace.state.active.vehicleId).toBeNull();
    expect(workspace.state.active.dirty).toBe(true);
    expect(workspace.state.vehicles[id1]).toBeDefined();
  });
  it("debounces by 500 ms and never autosaves", async () => {
    const save = vi.spyOn(householdApi, "saveProfile");
    workspace.editProfile(profile());
    workspace.editProfile({ ...profile(), periodMonths: n(24) });
    await vi.advanceTimersByTimeAsync(499);
    expect(householdApi.preview).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(householdApi.preview).toHaveBeenCalledOnce();
    expect(save).not.toHaveBeenCalled();
  });
  it("ignores an older response even if its transport does not honor cancellation", async () => {
    const older = deferred<PreviewResponse>();
    vi.mocked(householdApi.preview).mockImplementationOnce(() => older.promise);
    const first = workspace.calculate();
    const firstRequest = vi.mocked(householdApi.preview).mock.calls[0][0];
    workspace.editProfile(profile());
    await workspace.calculate();
    older.resolve(previewResponse(firstRequest, "999"));
    await first;
    expect(
      workspace.state.preview.results.manual.result?.sections.totals
        .ownershipCost.completeTotalSek?.text,
    ).toBe("100");
  });
  it("keeps old results explicitly stale throughout the next generation", async () => {
    await workspace.calculate();
    const old = workspace.state.preview;
    const pending = deferred<PreviewResponse>();
    vi.mocked(householdApi.preview).mockImplementationOnce(
      () => pending.promise,
    );
    workspace.editProfile(profile());
    const operation = workspace.calculate();
    expect(workspace.state.stale).toBe(true);
    expect(workspace.state.preview).toBe(old);
    pending.resolve(
      previewResponse(
        vi.mocked(householdApi.preview).mock.calls.at(-1)![0],
        "250",
      ),
    );
    await operation;
    expect(workspace.state.stale).toBe(false);
    expect(
      workspace.state.preview.results.manual.result?.sections.totals
        .ownershipCost.completeTotalSek?.text,
    ).toBe("250");
  });
  it("keeps a later profile edit when a save returns, while advancing its revision", async () => {
    const pending = deferred<ProfileResponse>();
    const api = vi
      .spyOn(householdApi, "saveProfile")
      .mockReturnValue(pending.promise);
    workspace.editProfile(profile());
    const operation = workspace.saveProfile();
    workspace.editProfile({
      ...profile(),
      purchaseCashSek: n("123.1234567890123456789"),
    });
    pending.resolve({ input: profile(), revision: n("9007199254740993") });
    await operation;
    expect(workspace.state.profile.purchaseCashSek?.text).toBe(
      "123.1234567890123456789",
    );
    expect(workspace.state.profileDirty).toBe(true);
    expect(workspace.state.savedProfile.revision.text).toBe("9007199254740993");
    expect(api.mock.calls[0][1].text).toBe("0");
  });
  it("preserves edited car fields after create returns and does not require a valid profile", async () => {
    const pending = deferred<VehicleResponse>();
    vi.spyOn(householdApi, "create").mockReturnValue(pending.promise);
    workspace.editProfile({ ...profile(), purchaseCashSek: n("invalid") });
    workspace.editActive({ registrationNumber: "abc-123" });
    const operation = workspace.saveVehicle();
    workspace.editActive({
      cost: {
        ...workspace.state.active.cost,
        input: { candidateKey: "manual", priceSek: n(999) },
      },
    });
    pending.resolve(savedVehicle());
    await operation;
    expect(workspace.state.active.cost.input.priceSek?.text).toBe("999");
    expect(workspace.state.active.dirty).toBe(true);
    expect(workspace.state.active.vehicleId).toBe(id1);
  });
  it("does not replace unsaved profile data during focus reloads", async () => {
    workspace.editProfile(profile());
    vi.mocked(householdApi.profile).mockResolvedValue({
      input: { ...profile(), purchaseCashSek: n(1) },
      revision: n(5),
    });
    await workspace.refresh();
    expect(workspace.state.profile.purchaseCashSek?.text).toBe("100000");
    expect(workspace.state.savedProfile.revision.text).toBe("0");
    expect(workspace.state.remoteProfile?.revision.text).toBe("5");
  });
  it("exposes conflicts without retry or local data loss", async () => {
    const save = vi
      .spyOn(householdApi, "saveProfile")
      .mockRejectedValue(
        new HouseholdApiError(409, "profileRevisionConflict", [], n(2)),
      );
    workspace.editProfile(profile());
    await workspace.saveProfile();
    expect(save).toHaveBeenCalledOnce();
    expect(workspace.state.profileDirty).toBe(true);
    expect(workspace.state.notice).toMatch(/annat fönster/);
  });
  it("maps profile save field errors to the profile editor", async () => {
    vi.spyOn(householdApi, "saveProfile").mockRejectedValue(
      new HouseholdApiError(400, "invalidHouseholdInput", [
        {
          path: "input.purchaseCashSek",
          code: "outOfRange",
          message: "English",
        },
      ]),
    );
    workspace.editProfile(profile());
    await workspace.saveProfile();
    expect(workspace.state.errors["profile.purchaseCashSek"]).toBeDefined();
  });
  it("requires a choice before replacing dirty car editing", async () => {
    const read = vi.spyOn(householdApi, "vehicle");
    workspace.editActive({ registrationNumber: "ABC123" });
    vi.mocked(window.confirm).mockReturnValue(false);
    expect(await workspace.openVehicle(id2)).toBe(false);
    expect(read).not.toHaveBeenCalled();
    expect(workspace.state.active.registrationNumber).toBe("ABC123");
  });
  it("ignores a late open response after the user starts a different manual car", async () => {
    const pending = deferred<VehicleResponse>();
    vi.spyOn(householdApi, "vehicle").mockReturnValue(pending.promise);
    const operation = workspace.openVehicle(id1);
    workspace.newVehicle();
    workspace.editActive({ registrationNumber: "DEF456" });
    pending.resolve(savedVehicle());
    await operation;
    expect(workspace.state.active.registrationNumber).toBe("DEF456");
    expect(workspace.state.active.vehicleId).toBeNull();
  });
  it("reads all cars with four details at a time", async () => {
    const cars = Array.from({ length: 8 }, (_, index) =>
      savedVehicle(
        `10000000-0000-0000-0000-${String(index + 1).padStart(12, "0")}`,
        `ABC10${index}`,
      ),
    );
    vi.mocked(householdApi.list).mockResolvedValue(cars.map(summary));
    const pending = cars.map(() => deferred<VehicleResponse>());
    const read = vi
      .spyOn(householdApi, "vehicle")
      .mockImplementation(
        (id) => pending[cars.findIndex((car) => car.vehicleId === id)].promise,
      );
    const operation = workspace.refresh();
    await Promise.resolve();
    await Promise.resolve();
    expect(read).toHaveBeenCalledTimes(4);
    pending[0].resolve(cars[0]);
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    expect(read).toHaveBeenCalledTimes(5);
    pending.forEach((item, index) => item.resolve(cars[index]));
    await operation;
    expect(workspace.state.summaries).toHaveLength(8);
  });
  it("supports a pure preview when the database is unavailable", async () => {
    vi.mocked(householdApi.profile).mockRejectedValue(
      new HouseholdApiError(503, "householdStorageUnavailable"),
    );
    vi.mocked(householdApi.list).mockRejectedValue(new Error("DB"));
    vi.mocked(householdApi.draft).mockRejectedValue(new Error("DB"));
    await workspace.refresh();
    workspace.editProfile(profile());
    await workspace.calculate();
    expect(workspace.state.preview.results.manual.result).toBeDefined();
    expect(workspace.state.storageNotice).toMatch(/Sparade uppgifter/);
  });
});

describe("shared draft and deletion", () => {
  it("saves an existing car origin with exact revisions and explicitly replaces a different registration", async () => {
    vi.mocked(householdApi.draft).mockResolvedValue({
      revision: n("9007199254740993"),
      input: {
        registrationNumber: "DEF456",
        cost: { input: { candidateKey: "other" } },
      },
    });
    await workspace.refresh();
    vi.spyOn(householdApi, "vehicle").mockResolvedValue(
      savedVehicle(id1, "ABC123", "9007199254740995"),
    );
    await workspace.openVehicle(id1);
    const save = vi
      .spyOn(householdApi, "saveDraft")
      .mockResolvedValue({ input: null, revision: n("9007199254740994") });
    await workspace.saveDraft();
    expect(save.mock.calls[0][0].baseVehicleRevision?.text).toBe(
      "9007199254740995",
    );
    expect(save.mock.calls[0][1].text).toBe("9007199254740993");
    expect(save.mock.calls[0][2]).toBe(true);
  });
  it("opens without consuming, requires saved edits before adoption, and reads the cleared slot revision", async () => {
    const draft: DraftResponse = {
      revision: n(3),
      input: {
        registrationNumber: "ABC123",
        cost: { input: { candidateKey: "ABC123", priceSek: n(1000) } },
      },
    };
    vi.mocked(householdApi.draft).mockResolvedValue(draft);
    const adopt = vi
      .spyOn(householdApi, "adopt")
      .mockResolvedValue(savedVehicle());
    await workspace.openDraft();
    expect(adopt).not.toHaveBeenCalled();
    expect(workspace.state.draft?.input).not.toBeNull();
    workspace.editActive({
      cost: {
        ...workspace.state.active.cost,
        input: { candidateKey: "ABC123", priceSek: n(2000) },
      },
    });
    await workspace.adoptDraft();
    expect(adopt).not.toHaveBeenCalled();
    vi.spyOn(householdApi, "saveDraft").mockImplementation(async (input) => ({
      input,
      revision: n(4),
    }));
    await workspace.saveDraft();
    vi.mocked(householdApi.draft).mockResolvedValue({
      revision: n(5),
      input: null,
    });
    await workspace.adoptDraft();
    expect(adopt.mock.calls[0][0].text).toBe("4");
    expect(workspace.state.draft).toEqual({ revision: n(5), input: null });
  });
  it("preserves the recovery draft on failed adoption", async () => {
    const input = {
      registrationNumber: "ABC123",
      cost: { input: { candidateKey: "ABC123" } },
    };
    vi.mocked(householdApi.draft).mockResolvedValue({ revision: n(1), input });
    vi.spyOn(householdApi, "adopt").mockRejectedValue(
      new HouseholdApiError(409, "vehicleRevisionConflict"),
    );
    await workspace.openDraft();
    await workspace.adoptDraft();
    expect(workspace.state.draft?.input).toEqual(input);
    expect(workspace.state.active.fromDraft).toBe(true);
  });
  it("does not attach a later registration edit to the car returned by pending adoption", async () => {
    vi.mocked(householdApi.draft).mockResolvedValue({
      revision: n(1),
      input: {
        registrationNumber: "ABC123",
        cost: { input: { candidateKey: "ABC123" } },
      },
    });
    await workspace.openDraft();
    const pending = deferred<VehicleResponse>();
    vi.spyOn(householdApi, "adopt").mockReturnValue(pending.promise);
    const operation = workspace.adoptDraft();
    workspace.editActive({ registrationNumber: "DEF456" });
    vi.mocked(householdApi.draft).mockResolvedValue({
      input: null,
      revision: n(2),
    });
    pending.resolve(savedVehicle());
    await operation;
    expect(workspace.state.active.registrationNumber).toBe("DEF456");
    expect(workspace.state.active.vehicleId).toBeNull();
    expect(workspace.state.active.baseRevision).toBeNull();
    expect(workspace.state.active.fromDraft).toBe(false);
    expect(workspace.state.active.dirty).toBe(true);
    expect(workspace.state.draft?.revision.text).toBe("2");
  });
  it("does not let an old vehicle load resurrect a deleted car", async () => {
    const pending = deferred<VehicleResponse>();
    vi.spyOn(householdApi, "vehicle").mockReturnValue(pending.promise);
    workspace.editProfile(profile());
    const operation = workspace.openVehicle(id1);
    workspace.forgetVehicle(id1);
    pending.resolve(savedVehicle());
    await operation;
    expect(workspace.state.active.vehicleId).toBeNull();
    expect(workspace.state.profile.purchaseCashSek?.text).toBe("100000");
  });
  it("does not overwrite a newer car editor when draft deletion finishes", async () => {
    vi.mocked(householdApi.draft).mockResolvedValue({
      revision: n(1),
      input: {
        registrationNumber: "ABC123",
        cost: { input: { candidateKey: "ABC123" } },
      },
    });
    await workspace.openDraft();
    const pending = deferred<DraftResponse>();
    vi.spyOn(householdApi, "deleteDraft").mockReturnValue(pending.promise);
    const operation = workspace.deleteDraft();
    workspace.newVehicle();
    workspace.editActive({ registrationNumber: "DEF456" });
    pending.resolve({ revision: n(2), input: null });
    await operation;
    expect(workspace.state.active.registrationNumber).toBe("DEF456");
    expect(workspace.state.draft?.revision.text).toBe("2");
  });
});

describe("legacy transition", () => {
  it("deleting one car preserves another car's review edits without silently advancing transition revision", async () => {
    const vehicles = [savedVehicle(), savedVehicle(id2, "DEF456")];
    vi.spyOn(householdApi, "transition").mockResolvedValue({
      profile: { input: null, revision: n(0) },
      revision: n(7),
      vehicles,
    });
    vi.mocked(householdApi.list).mockResolvedValue([summary(vehicles[1])]);
    vi.spyOn(householdApi, "vehicle").mockResolvedValue(vehicles[1]);
    await workspace.loadTransition();
    workspace.editTransition(id2, {
      input: { candidateKey: "DEF456", priceSek: n(12345) },
    });
    workspace.forgetVehicle(id1);
    await workspace.refresh();
    expect(workspace.state.transition?.costs[id2].input.priceSek?.text).toBe(
      "12345",
    );
    expect(workspace.state.transition?.costs[id1]).toBeUndefined();
    expect(
      workspace.state.transition?.snapshot.vehicles.map(
        (vehicle) => vehicle.vehicleId,
      ),
    ).toEqual([id2]);
    expect(workspace.state.transition?.snapshot.revision.text).toBe("7");
    expect(workspace.state.transition?.dirty).toBe(true);
  });
  it("submits the complete set and exact revisions atomically, without adopting old household assumptions", async () => {
    const old = savedVehicle();
    old.state = "legacyPending";
    old.input = null;
    const snapshot = {
      profile: { input: null, revision: n(0) },
      revision: n("9007199254740993"),
      vehicles: [old, savedVehicle(id2, "DEF456", "9")],
    };
    vi.spyOn(householdApi, "transition").mockResolvedValue(snapshot);
    const confirm = vi
      .spyOn(householdApi, "confirmTransition")
      .mockResolvedValue({
        profile: { input: profile(), revision: n(1) },
        revision: n("9007199254740994"),
        vehicles: [],
      });
    await workspace.loadTransition();
    expect(workspace.state.profile.purchaseCashSek).toBeUndefined();
    workspace.editProfile(profile());
    await workspace.confirmTransition();
    expect(confirm).toHaveBeenCalledOnce();
    expect(
      confirm.mock.calls[0][0].vehicles.map((vehicle) => vehicle.vehicleId),
    ).toEqual([id1, id2]);
    expect(confirm.mock.calls[0][0].expectedTransitionRevision.text).toBe(
      "9007199254740993",
    );
  });
  it("keeps review edits when a transition fails and never resubmits automatically", async () => {
    const old = savedVehicle();
    vi.mocked(householdApi.list).mockResolvedValue([summary(old)]);
    vi.spyOn(householdApi, "vehicle").mockResolvedValue(old);
    const snapshot = {
      profile: { input: null, revision: n(0) },
      revision: n(1),
      vehicles: [old],
    };
    vi.spyOn(householdApi, "transition").mockResolvedValue(snapshot);
    const confirm = vi
      .spyOn(householdApi, "confirmTransition")
      .mockRejectedValue(
        new HouseholdApiError(409, "transitionRevisionConflict"),
      );
    await workspace.loadTransition();
    workspace.editTransition(id1, {
      input: { candidateKey: "ABC123", priceSek: n(1) },
      legacyDecisions: [],
    });
    const editing = cloneExact(workspace.state.transition);
    await workspace.confirmTransition();
    expect(workspace.state.transition).toEqual(editing);
    expect(confirm).toHaveBeenCalledOnce();
  });
});
