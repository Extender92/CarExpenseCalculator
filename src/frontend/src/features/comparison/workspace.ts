import { HouseholdWorkspace } from "@/features/household/workspace";
import {
  householdApi,
  type ProfileResponse,
  type VehicleInput,
} from "@/features/household/api";
import {
  initialVehicle,
  validateCostWrite,
  validateVehicle,
  type FormErrors,
} from "@/features/household/form-model";
import {
  cloneExact,
  n,
  sameNumber,
  stringifyExact,
  type Numeric,
} from "@/features/household/numbers";
import {
  normalizeRegistrationNumber,
  validateRegistrationNumber,
} from "@/features/manual-calculator/saved-scenarios";
import { vehicleChanged, vehicleDeleted } from "@/lib/vehicle-events";
import {
  comparisonApi,
  ComparisonApiError,
  jsonBytes,
  type Baseline,
  type Candidate,
  type ComparisonRequest,
  type ComparisonResponse,
  type FactResponse,
  type FactWrite,
  type Result,
  type Rules,
  type RuleResponse,
} from "./api";
import {
  factErrors,
  mappedErrors,
  numberErrors,
  ruleErrors,
  validResponse,
} from "./preview";

export const emptyRules = (): Rules => ({
  hardRules: [],
  preferences: [],
  signals: [],
});
export function localDate(now = new Date()): string {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}
export function newIdentity() {
  // getRandomValues also works on the trusted LAN's HTTP origin, unlike randomUUID.
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join(
    "",
  );
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
interface EditingFacts {
  base: FactResponse;
  remote?: FactResponse;
  input: FactWrite;
  edit: number;
  dirty: boolean;
  confirmedCost?: string;
}
interface ManualVehicle {
  candidate: Candidate;
  edit: number;
  confirmedCost?: string;
}
export interface ComparisonState {
  mode: "stored" | "manual";
  date: string;
  baseline: Baseline | null;
  remote: Baseline | null;
  rules: Rules;
  savedRules: RuleResponse;
  rulesDirty: boolean;
  rulesEdit: number;
  facts: Record<string, EditingFacts>;
  manual: ManualVehicle[];
  selected: string | null;
  response: ComparisonResponse | null;
  stale: boolean;
  calculating: boolean;
  loading: boolean;
  errors: FormErrors;
  notice: string | null;
  problem: ComparisonApiError | null;
  busy: string | null;
  sort: "cost" | "score";
  page: number;
  expanded: string[];
  requestBytes: number;
}
const fingerprint = (value: unknown) => {
  try {
    return stringifyExact(value);
  } catch {
    return "invalid";
  }
};
const profileResponse = (b: Baseline): ProfileResponse => ({
  input: b.profile,
  revision: b.householdProfileRevision,
});

/** One tab's unsaved comparison. Responses never become writable evidence. */
export class ComparisonWorkspace {
  state: ComparisonState = {
    mode: "stored",
    date: localDate(),
    baseline: null,
    remote: null,
    rules: emptyRules(),
    savedRules: { input: null, revision: n(0), storageVersion: n(1) },
    rulesDirty: false,
    rulesEdit: 0,
    facts: {},
    manual: [],
    selected: null,
    response: null,
    stale: true,
    calculating: false,
    loading: false,
    errors: {},
    notice: null,
    problem: null,
    busy: null,
    sort: "cost",
    page: 1,
    expanded: [],
    requestBytes: 0,
  };
  private listeners = new Set<() => void>();
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private activeRequests = new Set<AbortController>();
  private queued = false;
  private readAbort: AbortController | null = null;
  private readVersion = 0;
  private openVersion = 0;
  private detailActive = 0;
  private detailQueue: (() => void)[] = [];
  private started = false;
  private disposed = false;
  private syncing = false;
  private tombstones = new Set<string>();
  private unsubscribe: (() => void) | null = null;
  private unsubscribeWrites: (() => void) | null = null;
  private expectedVehicles: Map<string, string> | null = null;
  private rememberWrite(
    id?: string,
    revision?: Numeric,
    listing?: Numeric | null,
    registration?: string,
    deleted = false,
  ) {
    if (!this.expectedVehicles && this.state.response?.mode === "stored")
      this.expectedVehicles = new Map(
        this.state.response.views.baseline.candidates.map((c) => [
          c.vehicleId,
          fingerprint([
            c.registrationNumber,
            c.sourceRevisions.vehicle,
            c.sourceRevisions.listing,
          ]),
        ]),
      );
    if (id && deleted) this.expectedVehicles?.delete(id);
    else if (id && revision)
      this.expectedVehicles?.set(
        id,
        fingerprint([registration, revision, listing]),
      );
  }
  constructor(public readonly household: HouseholdWorkspace) {}
  connect() {
    if (this.unsubscribe) return;
    this.disposed = false;
    const household = this.household;
    let last = household.snapshot();
    this.unsubscribe = household.subscribe(() => {
      const next = household.snapshot();
      const changed =
        next.profile !== last.profile ||
        next.savedProfile !== last.savedProfile ||
        next.active !== last.active ||
        next.transition !== last.transition;
      last = next;
      if (!this.syncing && changed && this.started) this.schedule();
    });
    this.unsubscribeWrites = household.subscribeWrites(
      (saved, previousRevision) => {
        if (!this.started || this.state.mode !== "stored") return;
        this.rememberWrite(
          saved?.vehicleId,
          saved?.revision,
          saved?.currentListingVersion,
          saved?.registrationNumber,
        );
        if (saved) {
          const editing = this.state.facts[saved.vehicleId];
          // Economic writes cannot edit facts. Only advance the exact revision
          // acknowledged by this tab; listing references still need fresh review.
          if (
            editing &&
            sameNumber(editing.base.revision, previousRevision) &&
            sameNumber(
              editing.base.currentListingVersion,
              saved.currentListingVersion,
            )
          ) {
            this.set({
              facts: {
                ...this.state.facts,
                [saved.vehicleId]: {
                  ...editing,
                  base: {
                    ...editing.base,
                    revision: saved.revision,
                    costConfirmedAt: null,
                  },
                },
              },
            });
          }
        }
        void this.refresh(true);
      },
    );
  }
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  snapshot = () => this.state;
  private set(patch: Partial<ComparisonState>) {
    this.state = { ...this.state, ...patch };
    for (const l of this.listeners) l();
  }
  start() {
    this.connect();
    if (!this.started) {
      this.started = true;
      if (this.state.mode === "stored") void this.refresh();
      else this.schedule();
    }
  }
  enter() {
    if (this.started) this.onFocus();
    else this.start();
  }
  onFocus() {
    if (this.started && !this.state.busy) void this.refresh();
  }
  dispose() {
    this.disposed = true;
    this.generation++;
    this.readAbort?.abort();
    this.activeRequests.forEach((c) => c.abort());
    if (this.timer) clearTimeout(this.timer);
    this.unsubscribe?.();
    this.unsubscribe = null;
    this.started = false;
    this.unsubscribeWrites?.();
    this.unsubscribeWrites = null;
  }
  isDirty() {
    return (
      this.state.rulesDirty ||
      Object.values(this.state.facts).some((f) => f.dirty) ||
      this.state.manual.length > 0
    );
  }
  editRules(rules: Rules) {
    this.set({ rules, rulesDirty: true, rulesEdit: this.state.rulesEdit + 1 });
    this.schedule();
  }
  editDate(date: string) {
    this.set({ date });
    this.schedule();
  }
  setSort(sort: ComparisonState["sort"]) {
    this.set({ sort, page: 1 });
  }
  setPage(page: number) {
    this.set({
      page: Math.max(
        1,
        Math.min(page, Math.ceil(this.results().length / 50) || 1),
      ),
    });
  }
  setExpanded(expanded: string[]) {
    this.set({ expanded });
  }
  setMode(mode: ComparisonState["mode"]) {
    if (mode === this.state.mode) return;
    if (
      mode === "manual" &&
      !window.confirm(
        "Starta fristående manuell jämförelse? De visade hushållsvärdena och köpkraven används som manuella antaganden. Sparade bilfakta och verifieringar kopieras inte. Dina sparade bilars osparade ändringar finns kvar när du återgår.",
      )
    )
      return;
    this.readVersion++;
    this.readAbort?.abort();
    this.openVersion++;
    this.expectedVehicles = null;
    this.set({
      mode,
      selected: null,
      response: null,
      remote: null,
      loading: false,
      page: 1,
      notice: null,
    });
    if (mode === "stored") void this.refresh();
    this.schedule();
  }
  results(): Result[] {
    const response = this.state.response;
    if (!response || response.mode !== this.state.mode) return [];
    const mode =
      this.household.state.profile.activeSensitivityMode ?? "baseline";
    const view = response.views[mode];
    const byId = new Map(
      view.candidates
        .filter((v) => !this.tombstones.has(v.vehicleId))
        .map((v) => [v.vehicleId, v]),
    );
    return (
      this.state.sort === "cost" ? view.costOrder : view.scoreOrder
    ).flatMap((id) => (byId.has(id) ? [byId.get(id)!] : []));
  }
  result(id: string) {
    return this.results().find((r) => r.vehicleId === id);
  }
  private hasStoredEdits() {
    return (
      this.state.rulesDirty ||
      this.household.state.profileDirty ||
      Object.values(this.state.facts).some((f) => f.dirty) ||
      !!(
        this.household.state.active.vehicleId &&
        this.household.state.active.dirty
      ) ||
      this.household.state.transition?.dirty === true
    );
  }
  private installBaseline(baseline: Baseline, replaceRules = false) {
    this.syncing = true;
    try {
      this.household.receiveProfile(profileResponse(baseline));
    } finally {
      this.syncing = false;
    }
    this.set({
      baseline,
      remote: null,
      ...(!this.state.rulesDirty || replaceRules
        ? {
            rules: cloneExact(baseline.rules ?? emptyRules()),
            rulesDirty: false,
            rulesEdit: this.state.rulesEdit + 1,
          }
        : {}),
      savedRules: {
        input: baseline.rules,
        revision: baseline.ruleProfileRevision,
        storageVersion: n(1),
      },
    });
  }
  async refresh(ownWrite = this.expectedVehicles !== null) {
    if (this.state.mode !== "stored" || this.disposed) return;
    this.readAbort?.abort();
    const controller = new AbortController();
    this.readAbort = controller;
    const version = ++this.readVersion;
    this.set({ loading: true, notice: null, problem: null });
    try {
      const next = await comparisonApi.baseline(controller.signal);
      if (
        controller.signal.aborted ||
        version !== this.readVersion ||
        this.disposed
      )
        return;
      const changed = this.state.baseline?.baselineToken !== next.baselineToken;
      // Own writes only authorize the revisions returned by that write. Any
      // changed shared revision or independently edited car still needs review.
      let safeOwn =
        ownWrite &&
        sameNumber(
          next.householdProfileRevision,
          this.household.state.savedProfile.revision,
        ) &&
        sameNumber(next.ruleProfileRevision, this.state.savedRules.revision);
      if (safeOwn) {
        const dirty = Object.values(this.state.facts).filter((f) => f.dirty);
        for (const f of dirty) {
          const latest = await this.readFacts(
            f.base.vehicleId,
            controller.signal,
          );
          if (!sameNumber(latest.revision, f.base.revision)) safeOwn = false;
        }
        const active = this.household.state.active;
        if (active.vehicleId && active.dirty) {
          const latest = await this.readFacts(
            active.vehicleId,
            controller.signal,
          );
          if (!sameNumber(latest.revision, active.baseRevision))
            safeOwn = false;
        }
      }
      if (
        controller.signal.aborted ||
        version !== this.readVersion ||
        this.disposed
      )
        return;
      const initialProfileConflict =
        !this.state.baseline &&
        this.household.state.profileDirty &&
        !sameNumber(
          next.householdProfileRevision,
          this.household.state.savedProfile.revision,
        );
      if (
        initialProfileConflict ||
        (changed && this.state.baseline && this.hasStoredEdits() && !safeOwn)
      ) {
        this.invalidate();
        this.set({
          remote: next,
          notice:
            "Serverunderlaget har ändrats. Granska skillnaderna; dina lokala ändringar finns kvar.",
        });
      } else {
        if (changed) {
          const facts = Object.fromEntries(
            Object.entries(this.state.facts).filter(
              ([id, f]) => f.dirty || id === this.state.selected,
            ),
          );
          this.set({ facts });
        }
        this.installBaseline(next);
        this.schedule();
        if (
          this.state.selected &&
          !this.state.facts[this.state.selected]?.dirty
        )
          void this.select(this.state.selected, true);
      }
    } catch (error) {
      if (!controller.signal.aborted && version === this.readVersion)
        this.failure(error);
    } finally {
      if (version === this.readVersion) this.set({ loading: false });
    }
  }
  async acceptRemote(keep: boolean) {
    const remote = this.state.remote;
    if (!remote || this.state.busy) return;
    if (
      !window.confirm(
        keep
          ? "Behåll dina ändringar efter granskning mot det aktuella underlaget? Annonsval och konfliktval behöver göras om när deras version har ändrats."
          : "Ersätt osparade regler, fakta och hushållsprofil med serverunderlaget? Ekonomisk bilredigering behålls och granskas separat på Manuell kalkyl.",
      )
    )
      return;
    this.set({ busy: "review", errors: {} });
    try {
      const facts: Record<string, EditingFacts> = {};
      for (const [id, edit] of Object.entries(this.state.facts)) {
        const latest = await this.readFacts(id);
        if (keep && edit.dirty) {
          if (
            !sameNumber(latest.revision, edit.base.revision) &&
            hasReferences(edit.input)
          )
            throw new Error(
              "Annons- eller konfliktval hänvisar till äldre uppgifter. Läs aktuella biluppgifter och gör dessa val på nytt.",
            );
          facts[id] = { ...edit, base: latest };
        } else facts[id] = { base: latest, input: {}, edit: 0, dirty: false };
      }
      const h = this.household.state;
      if (h.active.vehicleId && h.active.dirty) {
        // Use the existing explicit household review for economic edits; never
        // quietly attach the comparison's refreshed revision to those edits.
        const latest = await householdApi.vehicle(h.active.vehicleId);
        if (!sameNumber(latest.revision, h.active.baseRevision))
          throw new Error(
            "Bilens ekonomiska underlag har ändrats. Granska serverunderlaget på Manuell kalkyl innan jämförelsens baslinje byts.",
          );
      }
      this.syncing = true;
      if (!keep) this.household.receiveProfile(profileResponse(remote), true);
      else if (this.household.state.profileDirty) {
        this.household.receiveProfile(profileResponse(remote));
        if (this.household.state.remoteProfile)
          this.household.keepProfileAgainstRemote();
      }
      this.syncing = false;
      if (this.household.state.remoteProfile) return;
      this.set({ facts });
      this.installBaseline(remote, !keep);
      this.schedule();
      this.expectedVehicles = null;
    } catch (error) {
      this.failure(error);
    } finally {
      this.syncing = false;
      this.set({ busy: null });
    }
  }
  private async readFacts(id: string, signal?: AbortSignal) {
    if (this.detailActive >= 4)
      await new Promise<void>((resolve) => this.detailQueue.push(resolve));
    else this.detailActive++;
    try {
      if (signal?.aborted || this.disposed)
        throw new DOMException("Avbrutet", "AbortError");
      return await comparisonApi.facts(id, signal);
    } finally {
      const next = this.detailQueue.shift();
      if (next) next();
      else this.detailActive--;
    }
  }
  async select(id: string, reload = false) {
    const version = ++this.openVersion;
    this.set({ selected: id, errors: {}, notice: null });
    if (this.state.mode === "manual" || (this.state.facts[id] && !reload))
      return;
    try {
      const base = await this.readFacts(id);
      if (
        version !== this.openVersion ||
        this.tombstones.has(id) ||
        this.disposed
      )
        return;
      if (reload && this.state.facts[id]?.dirty) {
        this.set({
          facts: {
            ...this.state.facts,
            [id]: { ...this.state.facts[id], remote: base },
          },
        });
        return;
      }
      this.set({
        facts: {
          ...this.state.facts,
          [id]: { base, input: {}, dirty: false, edit: 0 },
        },
      });
    } catch (error) {
      if (version === this.openVersion) this.failure(error);
    }
  }
  reviewFacts(id: string, keep: boolean) {
    const f = this.state.facts[id];
    if (!f?.remote) return;
    if (
      keep &&
      hasReferences(f.input) &&
      !sameNumber(f.base.revision, f.remote.revision)
    ) {
      this.set({
        notice:
          "Annons- och konfliktval gäller äldre underlag. Anteckna önskade ändringar och använd aktuella biluppgifter innan du gör dessa val på nytt.",
      });
      return;
    }
    this.set({
      facts: {
        ...this.state.facts,
        [id]: {
          ...f,
          base: f.remote,
          remote: undefined,
          input: keep ? f.input : {},
          dirty: keep && f.dirty,
          edit: f.edit + 1,
        },
      },
    });
    this.schedule();
  }
  editFacts(id: string, input: FactWrite) {
    if (this.state.mode === "manual") {
      this.set({
        manual: this.state.manual.map((m) =>
          m.candidate.vehicleId === id
            ? {
                ...m,
                candidate: { ...m.candidate, facts: input },
                edit: m.edit + 1,
              }
            : m,
        ),
      });
    } else {
      const old = this.state.facts[id];
      if (!old) return;
      this.set({
        facts: {
          ...this.state.facts,
          [id]: { ...old, input, dirty: true, edit: old.edit + 1 },
        },
      });
    }
    this.schedule();
  }
  addManual() {
    const id = newIdentity();
    this.set({
      manual: [
        ...this.state.manual,
        {
          candidate: { vehicleId: id, registrationNumber: "", facts: {} },
          edit: 0,
        },
      ],
      selected: id,
    });
    this.schedule();
    return id;
  }
  editManual(id: string, patch: Partial<Candidate>) {
    this.set({
      manual: this.state.manual.map((m) =>
        m.candidate.vehicleId === id
          ? {
              ...m,
              edit: m.edit + 1,
              candidate: {
                ...m.candidate,
                ...patch,
                vehicleId: id,
                storedBase: undefined,
                legacyDecisions: undefined,
              },
            }
          : m,
      ),
    });
    this.schedule();
  }
  manualCost(id: string): VehicleInput | null {
    return (
      this.state.manual.find((m) => m.candidate.vehicleId === id)?.candidate
        .costInput ?? null
    );
  }
  ensureManualCost(id: string) {
    if (!this.manualCost(id))
      this.editManual(id, { costInput: initialVehicle() });
  }
  private costFor(id: string): VehicleInput | null {
    if (this.state.mode === "manual") return this.manualCost(id);
    const active = this.household.state.active;
    return active.vehicleId === id && active.dirty
      ? active.cost.input
      : (this.result(id)?.effectiveCostInput ?? null);
  }
  confirmPreview(id: string, action: "confirm" | "clear") {
    if (action === "confirm" && !this.costFor(id)) {
      this.set({ notice: "Ange ett kostnadsunderlag innan det bekräftas." });
      return;
    }
    const current =
      this.state.mode === "manual"
        ? this.state.manual.find((m) => m.candidate.vehicleId === id)?.candidate
            .facts
        : this.state.facts[id]?.input;
    if (!current) return;
    this.editFacts(id, { ...current, costConfirmation: action });
    const confirmedCost = fingerprint(this.costFor(id));
    if (this.state.mode === "manual")
      this.set({
        manual: this.state.manual.map((m) =>
          m.candidate.vehicleId === id ? { ...m, confirmedCost } : m,
        ),
      });
    else
      this.set({
        facts: {
          ...this.state.facts,
          [id]: { ...this.state.facts[id], confirmedCost },
        },
      });
  }
  private effectiveFacts(
    input: FactWrite | undefined,
    confirmedCost: string | undefined,
    cost: VehicleInput | null,
  ): FactWrite {
    return {
      ...cloneExact(input ?? {}),
      ...(input?.costConfirmation === "confirm" &&
      confirmedCost !== fingerprint(cost)
        ? { costConfirmation: "preserve" as const }
        : {}),
    };
  }
  private capture(): {
    request: ComparisonRequest;
    expectedCount: number;
    errors: FormErrors;
  } {
    const h = this.household.state;
    const errors: FormErrors = {
      ...ruleErrors(this.state.rules),
      ...numberErrors(h.profile, "profile"),
    };
    if (!/^\d{4}-\d{2}-\d{2}$/.test(this.state.date))
      errors.asOfDate = ["Ange ett giltigt datum."];
    const base = this.state.baseline;
    if (this.state.mode === "stored" && (!base || this.state.remote))
      errors.baseline = [
        "Läs och granska det aktuella serverunderlaget först.",
      ];
    const request: ComparisonRequest = {
      mode: this.state.mode,
      requestId: `comparison-${this.generation}`,
      profile: cloneExact(h.profile),
      rules: cloneExact(this.state.rules),
      asOfDate: this.state.date,
    };
    if (request.mode === "stored" && base) {
      request.storedBase = {
        baselineToken: base.baselineToken,
        householdProfileRevision: h.savedProfile.revision,
        ruleProfileRevision: this.state.savedRules.revision,
      };
      const overrides = new Map<string, Candidate>();
      for (const [id, f] of Object.entries(this.state.facts))
        if (f.dirty && !this.tombstones.has(id)) {
          if (f.remote)
            errors[`vehicle.${id}.facts`] = [
              "Granska de ändrade biluppgifterna innan jämförelsen beräknas igen.",
            ];
          overrides.set(id, {
            vehicleId: id,
            registrationNumber: f.base.registrationNumber,
            storedBase: {
              vehicleRevision: f.base.revision,
              listing: { version: f.base.currentListingVersion },
            },
            facts: this.effectiveFacts(
              f.input,
              f.confirmedCost,
              this.costFor(id),
            ),
          });
        }
      const edits = new Map<
        string,
        {
          input: VehicleInput;
          revision: Numeric;
          registration: string;
          listing: Numeric | null;
          decisions: Candidate["legacyDecisions"];
        }
      >();
      for (const vehicle of h.transition?.snapshot.vehicles ?? []) {
        const cost = h.transition?.costs[vehicle.vehicleId];
        if (cost && h.transition?.dirty)
          edits.set(vehicle.vehicleId, {
            input: cost.input,
            revision: vehicle.revision,
            registration: vehicle.registrationNumber,
            listing: vehicle.currentListingVersion,
            decisions: cost.legacyDecisions,
          });
      }
      const active = h.active;
      if (
        active.vehicleId &&
        active.dirty &&
        active.baseRevision &&
        !this.tombstones.has(active.vehicleId)
      ) {
        const result = this.result(active.vehicleId);
        const listing =
          h.vehicles[active.vehicleId]?.currentListingVersion ??
          result?.sourceRevisions.listing ??
          null;
        edits.set(active.vehicleId, {
          input: active.cost.input,
          revision: active.baseRevision,
          registration: active.registrationNumber,
          listing,
          decisions: active.cost.legacyDecisions,
        });
        Object.assign(
          errors,
          prefixErrors(
            validateCostWrite(active.cost, active.reviews),
            `vehicle.${active.vehicleId}.costInput`,
          ),
        );
      }
      for (const [id, cost] of edits) {
        const old = overrides.get(id);
        if (old && !sameNumber(old.storedBase?.vehicleRevision, cost.revision))
          errors[`vehicle.${id}`] = [
            "Fakta och ekonomiska ändringar har olika basrevisioner. Granska aktuella underlag.",
          ];
        overrides.set(id, {
          ...old,
          vehicleId: id,
          registrationNumber: cost.registration,
          storedBase: {
            vehicleRevision: cost.revision,
            listing: { version: cost.listing },
          },
          facts: old?.facts ?? {},
          costInput: cloneExact(cost.input),
          legacyDecisions: cloneExact(cost.decisions),
        });
      }
      request.overrides = [...overrides.values()];
    } else if (request.mode === "manual") {
      request.candidates = this.state.manual.map((m) => ({
        ...cloneExact(m.candidate),
        facts: this.effectiveFacts(
          m.candidate.facts,
          m.confirmedCost,
          m.candidate.costInput ?? null,
        ),
      }));
    }
    const seen = new Set<string>();
    for (const c of request.candidates ?? request.overrides ?? []) {
      const prefix = `vehicle.${c.vehicleId}`;
      const reg = validateRegistrationNumber(c.registrationNumber);
      if (reg.error) errors[`${prefix}.registrationNumber`] = [reg.error];
      const normalized = normalizeRegistrationNumber(c.registrationNumber);
      if (seen.has(normalized))
        errors[`${prefix}.registrationNumber`] = [
          "Registreringsnumret finns redan i jämförelsen.",
        ];
      seen.add(normalized);
      Object.assign(errors, factErrors(c.facts ?? {}, `${prefix}.facts`));
      if (c.costInput)
        Object.assign(
          errors,
          prefixErrors(validateVehicle(c.costInput), `${prefix}.costInput`),
        );
    }
    return {
      request,
      errors,
      expectedCount:
        request.mode === "manual"
          ? this.state.manual.length
          : Number(base?.candidateCount.text ?? "0"),
    };
  }
  private invalidate() {
    this.generation++;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    this.activeRequests.forEach((c) => c.abort());
    this.queued = false;
    this.set({ stale: true, calculating: false });
  }
  schedule() {
    this.invalidate();
    if (!this.started || this.disposed) return;
    this.timer = setTimeout(() => {
      this.timer = null;
      void this.calculate();
    }, 500);
  }
  async calculate() {
    if (this.disposed) return;
    this.invalidate();
    this.set({ errors: {}, notice: null, problem: null });
    if (this.activeRequests.size >= 2) {
      this.queued = true;
      this.set({ calculating: true });
      return;
    }
    const { request, errors, expectedCount } = this.capture();
    if (Object.keys(errors).length) {
      this.set({ errors });
      return;
    }
    const current = this.generation;
    const controller = new AbortController();
    this.activeRequests.add(controller);
    try {
      this.set({
        calculating: true,
        requestBytes: jsonBytes(stringifyExact(request)),
      });
      const response = await comparisonApi.preview(request, controller.signal);
      if (
        controller.signal.aborted ||
        current !== this.generation ||
        this.disposed
      )
        return;
      validResponse(request, response, expectedCount);
      if (
        request.mode === "stored" &&
        this.expectedVehicles &&
        (response.views.baseline.candidates.length !==
          this.expectedVehicles.size ||
          response.views.baseline.candidates.some(
            (c) =>
              this.expectedVehicles!.get(c.vehicleId) !==
              fingerprint([
                c.registrationNumber,
                c.sourceRevisions.vehicle,
                c.sourceRevisions.listing,
              ]),
          ))
      ) {
        this.set({ remote: this.state.baseline });
        throw new ComparisonApiError(409, "comparisonBaselineConflict");
      }
      this.expectedVehicles = null;
      this.set({
        response,
        stale: false,
        page: Math.min(this.state.page, Math.ceil(expectedCount / 50) || 1),
      });
    } catch (error) {
      if (
        !controller.signal.aborted &&
        current === this.generation &&
        !this.disposed
      ) {
        this.failure(error);
        if (error instanceof ComparisonApiError && error.problem.fieldErrors)
          this.set({
            errors: mappedErrors(error.problem.fieldErrors, request),
          });
      }
    } finally {
      this.activeRequests.delete(controller);
      if (current === this.generation && !this.disposed)
        this.set({ calculating: false });
      if (this.queued && !this.disposed) {
        this.queued = false;
        void this.calculate();
      }
    }
  }
  private failure(error: unknown) {
    this.invalidate();
    this.set({
      notice: (error as Error).message,
      problem: error instanceof ComparisonApiError ? error : null,
    });
  }
  private startWrite(key: string) {
    if (this.state.busy) return false;
    this.readVersion++;
    this.readAbort?.abort();
    this.invalidate();
    this.set({ busy: key, loading: false, notice: null, errors: {} });
    return true;
  }
  async saveRules() {
    const errors = ruleErrors(this.state.rules);
    this.set({ errors });
    if (Object.keys(errors).length || !this.startWrite("rules")) return;
    const input = cloneExact(this.state.rules);
    const edit = this.state.rulesEdit;
    try {
      const saved = await comparisonApi.saveRules(
        input,
        this.state.savedRules.revision,
      );
      this.rememberWrite();
      this.set({
        savedRules: saved,
        rulesDirty: edit !== this.state.rulesEdit,
        ...(edit === this.state.rulesEdit
          ? { rules: cloneExact(saved.input ?? emptyRules()) }
          : {}),
        notice: "Köpkrav och prioriteringar har sparats.",
      });
      await this.refresh(true);
    } catch (error) {
      this.failure(error);
      this.writeErrors(error, "rules");
    } finally {
      this.set({ busy: null });
    }
  }
  async saveFacts(id: string, confirmation?: "confirm" | "clear") {
    const f = this.state.facts[id];
    if (!f || this.state.mode !== "stored") return;
    if (f.remote) {
      this.set({ notice: "Granska aktuella biluppgifter innan de sparas." });
      return;
    }
    if (
      confirmation === "confirm" &&
      this.household.state.active.vehicleId === id &&
      this.household.state.active.dirty
    ) {
      this.set({
        notice:
          "Spara bilens ekonomiska underlag innan det bekräftas beständigt.",
      });
      return;
    }
    if (!this.startWrite(id)) return;
    const input = confirmation
      ? { costConfirmation: confirmation }
      : { ...cloneExact(f.input), costConfirmation: "preserve" as const };
    try {
      const saved = await this.household.coordinateVehicleWrite(() =>
        comparisonApi.saveFacts(id, input, f.base.revision),
      );
      if (this.tombstones.has(id) || this.disposed) return;
      this.rememberWrite(
        id,
        saved.revision,
        saved.currentListingVersion,
        saved.registrationNumber,
      );
      const current = this.state.facts[id];
      const pending = confirmation
        ? { ...current.input }
        : remainingFactEdits(f.input, current.input);
      if (confirmation && current.edit === f.edit)
        delete pending.costConfirmation;
      const changedReference =
        !confirmation &&
        current.edit !== f.edit &&
        (hasReferences(pending) ||
          Object.keys(f.input.edits ?? {}).some(
            (key) => !(key in (current.input.edits ?? {})),
          ));
      this.set({
        facts: {
          ...this.state.facts,
          [id]: {
            ...current,
            base: changedReference ? f.base : saved,
            remote: changedReference ? saved : undefined,
            input: changedReference ? current.input : pending,
            dirty: changedReference || Object.keys(pending).length > 0,
          },
        },
        notice: confirmation
          ? "Kostnadsbekräftelsen har uppdaterats."
          : "Biluppgifterna har sparats.",
      });
      this.household.acknowledgeFactRevision(
        id,
        f.base.revision,
        saved.revision,
      );
      vehicleChanged(id);
      await this.refresh(true);
    } catch (error) {
      this.failure(error);
      this.writeErrors(error, `vehicle.${id}.facts`);
    } finally {
      this.set({ busy: null });
    }
  }
  private writeErrors(error: unknown, prefix: string) {
    if (error instanceof ComparisonApiError && error.problem.fieldErrors)
      this.set({
        errors: Object.fromEntries(
          error.problem.fieldErrors.map((e) => [
            e.path.replace(/^input(?=\.|$)/, prefix),
            [
              e.code === "required"
                ? "Uppgiften behöver anges."
                : "Kontrollera uppgiften och dess tillåtna värden.",
            ],
          ]),
        ),
      });
  }
  async remove(id: string) {
    if (this.state.mode === "manual") {
      if (!window.confirm("Ta bort den manuella bilen från arbetsytan?"))
        return;
      this.set({
        manual: this.state.manual.filter((m) => m.candidate.vehicleId !== id),
        selected: null,
        response: null,
      });
      this.schedule();
      return;
    }
    const r = this.result(id);
    if (
      !r ||
      !window.confirm(
        `Radera ${r.registrationNumber} permanent, inklusive annons, kalkyl, biluppgifter och matchande utkast?`,
      )
    )
      return;
    if (!this.startWrite(id)) return;
    try {
      await this.household.coordinateVehicleWrite(() =>
        householdApi.delete(
          id,
          this.state.facts[id]?.base.revision ?? r.sourceRevisions.vehicle!,
        ),
      );
      this.rememberWrite(id, undefined, undefined, undefined, true);
      this.household.forgetVehicle(id);
      this.forget(id);
      vehicleDeleted(id);
      await this.refresh(true);
    } catch (error) {
      this.failure(error);
    } finally {
      this.set({ busy: null });
    }
  }
  forget(id: string) {
    this.tombstones.add(id);
    this.openVersion++;
    const facts = { ...this.state.facts };
    delete facts[id];
    this.set({
      facts,
      selected: this.state.selected === id ? null : this.state.selected,
      response: null,
    });
    this.schedule();
  }
}
function prefixErrors(errors: FormErrors, prefix: string): FormErrors {
  return Object.fromEntries(
    Object.entries(errors).map(([path, messages]) => [
      path.replace(/^input(?=\.|$)/, prefix),
      messages,
    ]),
  );
}
function hasReferences(input: FactWrite) {
  return (
    input.reviewCurrentListing ||
    input.expectedListingVersion != null ||
    Object.values(input.edits ?? {})
      .flat()
      .some(
        (edit) =>
          edit &&
          (edit.kind === "listing" ||
            edit.kind === "resolve" ||
            edit.kind === "conflict"),
      )
  );
}

/** Remove only actions acknowledged by the save; confirmation has its own button. */
function remainingFactEdits(sent: FactWrite, current: FactWrite): FactWrite {
  const pending: FactWrite = {};
  const edits = Object.fromEntries(
    Object.entries(current.edits ?? {}).filter(
      ([key, value]) =>
        fingerprint(value) !==
        fingerprint(sent.edits?.[key as keyof typeof sent.edits]),
    ),
  );
  if (Object.keys(edits).length) pending.edits = edits;
  if (current.costConfirmation && current.costConfirmation !== "preserve")
    pending.costConfirmation = current.costConfirmation;
  if (current.reviewCurrentListing !== sent.reviewCurrentListing)
    pending.reviewCurrentListing = current.reviewCurrentListing;
  if (hasReferences(pending))
    pending.expectedListingVersion = current.expectedListingVersion;
  return pending;
}
