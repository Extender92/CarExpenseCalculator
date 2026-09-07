import {
  normalizeRegistrationNumber,
  validateRegistrationNumber,
} from "@/features/manual-calculator/saved-scenarios";
import { vehicleDeleted } from "@/lib/vehicle-events";
import {
  householdApi,
  HouseholdApiError,
  emptyProfileResponse,
  type Candidate,
  type CostWrite,
  type DraftResponse,
  type DraftInput,
  type LegacyReview,
  type ListingResponse,
  type ProfileInput,
  type ProfileResponse,
  type ReviewedListing,
  type TransitionResponse,
  type VehicleResponse,
  type VehicleSummary,
} from "./api";
import {
  initialProfile,
  initialVehicle,
  recoveredCost,
  remainingReviews,
  reviewSet,
  profileFields,
  validateFields,
  validateCostWrite,
  type FormErrors,
} from "./form-model";
import {
  cloneExact,
  sameNumber,
  stringifyExact,
  type Numeric,
} from "./numbers";
import {
  calculateGeneration,
  limitedMap,
  toFormErrors,
  type PreviewGeneration,
} from "./preview";

export interface ActiveVehicle {
  token: number;
  edit: number;
  dirty: boolean;
  vehicleId: string | null;
  baseRevision: Numeric | null;
  registrationNumber: string;
  cost: CostWrite;
  reviews: LegacyReview[];
  listing: ReviewedListing | null;
  listingSource: ListingResponse | null;
  fromDraft: boolean;
  costIncluded: boolean;
  draftRevision: Numeric | null;
  state: VehicleResponse["state"] | "new";
}
export interface TransitionEditing {
  snapshot: TransitionResponse;
  costs: Record<string, CostWrite>;
  edit: number;
  dirty: boolean;
}
export interface WorkspaceState {
  profile: ProfileInput;
  savedProfile: ProfileResponse;
  remoteProfile: ProfileResponse | null;
  profileEdit: number;
  profileDirty: boolean;
  summaries: VehicleSummary[];
  vehicles: Record<string, VehicleResponse>;
  readErrors: Record<string, string>;
  active: ActiveVehicle;
  draft: DraftResponse | null;
  transition: TransitionEditing | null;
  preview: PreviewGeneration;
  stale: boolean;
  calculating: boolean;
  loading: boolean;
  busy: "profile" | "vehicle" | "draft" | "transition" | null;
  notice: string | null;
  storageNotice: string | null;
  errors: FormErrors;
}

function removeTransitionVehicles(
  transition: TransitionEditing | null,
  removed: Set<string>,
): TransitionEditing | null {
  if (
    !transition ||
    !transition.snapshot.vehicles.some((vehicle) =>
      removed.has(vehicle.vehicleId),
    )
  )
    return transition;
  const vehicles = transition.snapshot.vehicles.filter(
    (vehicle) => !removed.has(vehicle.vehicleId),
  );
  if (!vehicles.length) return null;
  return {
    ...transition,
    // Keep the original revision: deletion requires an explicit server review
    // before confirmation, but must not discard another car's local decisions.
    snapshot: { ...transition.snapshot, vehicles },
    costs: Object.fromEntries(
      Object.entries(transition.costs).filter(([id]) => !removed.has(id)),
    ),
    edit: transition.edit + 1,
    dirty: true,
  };
}

/** A tab-local workspace. Only explicit methods below write to the server. */
export class HouseholdWorkspace {
  private listeners = new Set<() => void>();
  private token = 0;
  private readSequence = 0;
  private writeEpoch = 0;
  private previewSequence = 0;
  private openSequence = 0;
  private transitionSequence = 0;
  private readAbort: AbortController | null = null;
  private previewAbort: AbortController | null = null;
  private openAbort: AbortController | null = null;
  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private tombstones = new Set<string>();
  private started = false;
  state: WorkspaceState = {
    profile: initialProfile(),
    savedProfile: emptyProfileResponse(),
    remoteProfile: null,
    profileEdit: 0,
    profileDirty: false,
    summaries: [],
    vehicles: {},
    readErrors: {},
    active: this.newActive(),
    draft: null,
    transition: null,
    preview: { results: {}, profileErrors: {} },
    stale: true,
    calculating: false,
    loading: false,
    busy: null,
    notice: null,
    storageNotice: null,
    errors: {},
  };
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  snapshot = () => this.state;
  private set(patch: Partial<WorkspaceState>) {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) listener();
  }
  private newActive(): ActiveVehicle {
    return {
      token: ++this.token,
      edit: 0,
      dirty: false,
      vehicleId: null,
      baseRevision: null,
      registrationNumber: "",
      cost: {
        input: initialVehicle(),
        vehicleLabel: null,
        listingLinkMode: "preserve",
      },
      reviews: [],
      listing: null,
      listingSource: null,
      fromDraft: false,
      costIncluded: true,
      draftRevision: null,
      state: "new",
    };
  }
  start() {
    if (!this.started) {
      this.started = true;
      void this.refresh();
      this.schedule();
    }
  }
  onFocus() {
    if (this.started) void this.refresh();
  }
  dispose() {
    this.readAbort?.abort();
    this.previewAbort?.abort();
    this.openAbort?.abort();
    if (this.previewTimer) clearTimeout(this.previewTimer);
    this.started = false;
  }
  isDirty() {
    return (
      this.state.profileDirty ||
      this.state.active.dirty ||
      this.state.transition?.dirty === true
    );
  }
  setNotice(notice: string | null) {
    this.set({ notice });
  }
  editProfile(profile: ProfileInput) {
    this.set({
      profile,
      profileEdit: this.state.profileEdit + 1,
      profileDirty: true,
      errors: {},
      notice: null,
    });
    this.schedule();
  }
  editActive(patch: Partial<ActiveVehicle>) {
    const transition = this.state.transition;
    const id = this.state.active.vehicleId;
    this.set({
      ...(patch.cost && id && transition?.costs[id]
        ? {
            transition: {
              ...transition,
              costs: { ...transition.costs, [id]: cloneExact(patch.cost) },
              edit: transition.edit + 1,
              dirty: true,
            },
          }
        : {}),
      active: {
        ...this.state.active,
        ...patch,
        ...(patch.cost ? { costIncluded: true } : {}),
        edit: this.state.active.edit + 1,
        dirty: true,
      },
      errors: {},
      notice: null,
    });
    this.schedule();
  }
  newVehicle() {
    if (!this.allowReplace()) return false;
    this.cancelOpen();
    this.set({
      active: {
        ...this.newActive(),
        draftRevision: this.state.draft?.revision ?? null,
      },
      errors: {},
      notice: null,
    });
    this.schedule();
    return true;
  }
  private allowReplace() {
    return (
      !this.state.active.dirty ||
      window.confirm(
        "Du har osparade ändringar i bilen. Lämna dem och öppna ett annat underlag?",
      )
    );
  }
  private cancelOpen() {
    this.openSequence++;
    this.openAbort?.abort();
  }
  private schedule() {
    this.previewSequence++;
    this.previewAbort?.abort();
    if (this.previewTimer) clearTimeout(this.previewTimer);
    this.set({ stale: true, calculating: false });
    this.previewTimer = setTimeout(() => {
      this.previewTimer = null;
      void this.calculate();
    }, 500);
  }
  private candidates(): Candidate[] {
    const active = this.state.active;
    const all = new Map(
      this.state.summaries.map((summary) => [
        summary.vehicleId,
        this.state.vehicles[summary.vehicleId],
      ]),
    );
    for (const saved of this.state.transition?.snapshot.vehicles ?? [])
      all.set(saved.vehicleId, saved);
    const candidates: Candidate[] = [...all.values()]
      .filter(
        (saved): saved is VehicleResponse =>
          !!saved && !this.tombstones.has(saved.vehicleId),
      )
      .sort((left, right) =>
        left.registrationNumber.localeCompare(
          right.registrationNumber,
          "sv-SE",
        ),
      )
      .flatMap((saved) => {
        if (
          !saved ||
          (active.vehicleId === saved.vehicleId &&
            !this.state.transition?.costs[saved.vehicleId])
        )
          return [];
        const cost =
          this.state.transition?.costs[saved.vehicleId] ?? recoveredCost(saved);
        return [
          {
            registrationNumber: saved.registrationNumber,
            input: { ...cost.input, candidateKey: saved.registrationNumber },
            unresolvedLegacyItems: remainingReviews(
              reviewSet(saved),
              cost.legacyDecisions,
              cost.input,
            ).map((item) => item.input),
          },
        ];
      });
    if (
      !active.vehicleId ||
      (!this.tombstones.has(active.vehicleId) &&
        !this.state.transition?.costs[active.vehicleId])
    ) {
      const registration = active.registrationNumber.trim()
        ? normalizeRegistrationNumber(active.registrationNumber)
        : null;
      candidates.push({
        registrationNumber: registration,
        input: {
          ...active.cost.input,
          candidateKey: active.vehicleId ? registration! : "manual",
        },
        unresolvedLegacyItems: remainingReviews(
          active.reviews,
          active.cost.legacyDecisions,
          active.cost.input,
        ).map((item) => item.input),
      });
    }
    return candidates;
  }
  async calculate() {
    if (this.previewTimer) clearTimeout(this.previewTimer);
    this.previewTimer = null;
    this.previewAbort?.abort();
    const controller = new AbortController();
    this.previewAbort = controller;
    const generation = ++this.previewSequence;
    this.set({ calculating: true, stale: true });
    try {
      const preview = await calculateGeneration(
        cloneExact(this.state.profile),
        cloneExact(this.candidates()),
        `household-${generation}`,
        controller.signal,
      );
      if (!controller.signal.aborted && generation === this.previewSequence)
        this.set({ preview, stale: false, calculating: false });
    } catch (error) {
      if (!controller.signal.aborted && generation === this.previewSequence)
        this.set({ calculating: false, notice: (error as Error).message });
    }
  }
  async refresh() {
    this.readAbort?.abort();
    const controller = new AbortController();
    this.readAbort = controller;
    const sequence = ++this.readSequence;
    const writeEpoch = this.writeEpoch;
    this.set({ loading: true });
    const [profile, list, draft] = await Promise.allSettled([
      householdApi.profile(controller.signal).catch((error) => {
        if (
          error instanceof HouseholdApiError &&
          error.code === "profileNotFound"
        )
          return emptyProfileResponse();
        throw error;
      }),
      householdApi.list(controller.signal).then(async (summaries) => ({
        summaries,
        details: await limitedMap(
          summaries,
          4,
          async (summary) => {
            try {
              return {
                id: summary.vehicleId,
                value: await householdApi.vehicle(
                  summary.vehicleId,
                  controller.signal,
                ),
              };
            } catch (error) {
              return { id: summary.vehicleId, error: (error as Error).message };
            }
          },
          controller.signal,
        ),
      })),
      householdApi.draft(controller.signal),
    ]);
    if (
      controller.signal.aborted ||
      sequence !== this.readSequence ||
      writeEpoch !== this.writeEpoch
    )
      return;
    const patch: Partial<WorkspaceState> = {
      loading: false,
      storageNotice: null,
    };
    const failures: string[] = [];
    if (profile.status === "fulfilled") {
      if (!this.state.profileDirty) {
        patch.savedProfile = profile.value;
        patch.profile = cloneExact(profile.value.input ?? initialProfile());
        patch.remoteProfile = null;
      } else if (
        !sameNumber(profile.value.revision, this.state.savedProfile.revision)
      )
        patch.remoteProfile = profile.value;
    } else failures.push((profile.reason as Error).message);
    if (list.status === "fulfilled") {
      patch.summaries = list.value.summaries
        .filter((item) => !this.tombstones.has(item.vehicleId))
        .sort((a, b) =>
          a.registrationNumber.localeCompare(b.registrationNumber, "sv-SE"),
        );
      patch.vehicles = {};
      patch.readErrors = {};
      for (const detail of list.value.details) {
        if (detail.value) patch.vehicles[detail.id] = detail.value;
        else
          patch.readErrors[detail.id] =
            detail.error ?? "Underlaget kunde inte läsas.";
      }
      const registered = new Set(
        list.value.summaries.map((vehicle) => vehicle.vehicleId),
      );
      patch.transition = removeTransitionVehicles(
        this.state.transition,
        new Set(
          this.state.transition?.snapshot.vehicles
            .filter((vehicle) => !registered.has(vehicle.vehicleId))
            .map((vehicle) => vehicle.vehicleId),
        ),
      );
      const active = this.state.active;
      if (
        active.vehicleId &&
        !list.value.summaries.some(
          (item) => item.vehicleId === active.vehicleId,
        )
      ) {
        this.tombstones.add(active.vehicleId);
        this.cancelOpen();
        patch.active = this.newActive();
        patch.notice =
          "Den öppna bilen har raderats. Dess lokala underlag har rensats.";
      }
    } else failures.push((list.reason as Error).message);
    if (draft.status === "fulfilled") {
      patch.draft = draft.value;
      if (!this.state.active.draftRevision)
        patch.active = {
          ...(patch.active ?? this.state.active),
          draftRevision: draft.value.revision,
        };
    } else failures.push((draft.reason as Error).message);
    if (failures.length) patch.storageNotice = [...new Set(failures)].join(" ");
    this.set(patch);
    this.schedule();
  }
  async openVehicle(id: string, listingRequested = false, force = false) {
    if (!force && !this.allowReplace()) return false;
    if (this.tombstones.has(id)) {
      this.set({ notice: "Bilen har raderats." });
      return false;
    }
    this.cancelOpen();
    const sequence = this.openSequence;
    const controller = new AbortController();
    this.openAbort = controller;
    const activeToken = this.state.active.token;
    const activeEdit = this.state.active.edit;
    try {
      const vehicle = await householdApi.vehicle(id, controller.signal);
      const listing =
        vehicle.currentListingVersion != null || listingRequested
          ? await householdApi.listing(id, controller.signal)
          : null;
      if (
        controller.signal.aborted ||
        sequence !== this.openSequence ||
        this.tombstones.has(id) ||
        this.state.active.token !== activeToken ||
        this.state.active.edit !== activeEdit
      )
        return false;
      const cost = this.state.transition?.costs[id] ?? recoveredCost(vehicle);
      this.set({
        active: {
          ...this.newActive(),
          vehicleId: id,
          baseRevision: vehicle.revision,
          registrationNumber: vehicle.registrationNumber,
          cost: cloneExact(cost),
          reviews: reviewSet(vehicle),
          state: vehicle.state,
          listingSource: listing,
          draftRevision: this.state.draft?.revision ?? null,
        },
        vehicles: { ...this.state.vehicles, [id]: vehicle },
        errors: {},
        notice: null,
      });
      this.schedule();
      return true;
    } catch (error) {
      if (!controller.signal.aborted) this.failure(error);
      return false;
    }
  }
  acceptRemoteProfile() {
    const remote = this.state.remoteProfile;
    if (
      !remote ||
      !window.confirm(
        "Ersätta dina profiländringar med de aktuella serveruppgifterna?",
      )
    )
      return;
    this.set({
      profile: cloneExact(remote.input ?? initialProfile()),
      savedProfile: remote,
      remoteProfile: null,
      profileDirty: false,
      profileEdit: this.state.profileEdit + 1,
    });
    this.schedule();
  }
  keepProfileAgainstRemote() {
    const remote = this.state.remoteProfile;
    if (
      !remote ||
      !window.confirm(
        "Behålla din profilredigering efter granskningen? Nästa uttryckliga sparning ersätter den visade serverprofilen.",
      )
    )
      return;
    this.set({ savedProfile: remote, remoteProfile: null, profileDirty: true });
  }
  keepVehicleAgainstRemote() {
    const active = this.state.active;
    const remote = active.vehicleId
      ? this.state.vehicles[active.vehicleId]
      : null;
    if (
      !remote ||
      !window.confirm(
        "Behålla din bilredigering efter granskningen? Nästa uttryckliga sparning ersätter det visade serverunderlaget.",
      )
    )
      return;
    this.set({
      active: {
        ...active,
        baseRevision: remote.revision,
        reviews: reviewSet(remote),
        state: remote.state,
        cost: { ...active.cost, listingLinkMode: "preserve" },
        dirty: true,
        edit: active.edit + 1,
      },
    });
    this.schedule();
    if (remote.currentListingVersion != null) void this.readLatestListing();
  }
  async readLatestListing() {
    const active = this.state.active;
    if (!active.vehicleId) return;
    try {
      const listing = await householdApi.listing(active.vehicleId);
      if (
        this.state.active.token === active.token &&
        !this.tombstones.has(active.vehicleId)
      )
        this.set({ active: { ...this.state.active, listingSource: listing } });
    } catch (error) {
      this.failure(error);
    }
  }
  acceptDraftRevision() {
    if (
      !this.state.draft ||
      !window.confirm(
        "Använda den visade utkastplatsens aktuella revision för din redigering? En ny uttrycklig sparning kan ersätta innehållet på platsen.",
      )
    )
      return;
    this.set({
      active: {
        ...this.state.active,
        draftRevision: this.state.draft.revision,
      },
    });
  }
  private failure(error: unknown, prefix?: string) {
    this.set({
      notice: (error as Error).message,
      errors:
        error instanceof HouseholdApiError
          ? toFormErrors(error.fields, (path) =>
              prefix ? path.replace(prefix, "input") : path,
            )
          : {},
    });
  }
  private begin(kind: NonNullable<WorkspaceState["busy"]>) {
    if (this.state.busy) return false;
    this.writeEpoch++;
    this.set({ busy: kind, notice: null, errors: {}, loading: false });
    return true;
  }
  private end() {
    const refreshInvalidated = this.state.loading;
    this.writeEpoch++;
    this.set({ busy: null, loading: false });
    // Reads started during a write cannot publish across its final epoch. In
    // particular, deletion refreshes the cleared draft slot before reaching end.
    if (refreshInvalidated) void this.refresh();
  }
  async saveProfile() {
    const errors = validateFields(
      this.state.profile as Record<string, unknown>,
      profileFields,
      "profile",
    );
    if (Object.keys(errors).length) {
      this.set({ errors });
      return;
    }
    if (!this.begin("profile")) return;
    const input = cloneExact(this.state.profile);
    const edit = this.state.profileEdit;
    try {
      const saved = await householdApi.saveProfile(
        input,
        this.state.savedProfile.revision,
      );
      this.set({
        savedProfile: saved,
        remoteProfile: null,
        profileDirty: edit !== this.state.profileEdit,
        ...(edit === this.state.profileEdit
          ? { profile: cloneExact(saved.input ?? initialProfile()) }
          : {}),
        notice: "Hushållsprofilen har sparats.",
      });
    } catch (error) {
      this.failure(error);
      this.set({
        errors:
          error instanceof HouseholdApiError
            ? toFormErrors(error.fields, (path) =>
                path.replace(/^input(?=\.|$)/, "profile"),
              )
            : {},
      });
    } finally {
      this.end();
    }
  }
  private validateActive() {
    const errors = validateCostWrite(
      this.state.active.cost,
      this.state.active.reviews,
    );
    const registration = validateRegistrationNumber(
      this.state.active.registrationNumber,
    );
    if (registration.error) errors.registrationNumber = [registration.error];
    this.set({ errors });
    return Object.keys(errors).length === 0;
  }
  private writeActive(): CostWrite {
    return {
      ...cloneExact(this.state.active.cost),
      input: {
        ...cloneExact(this.state.active.cost.input),
        candidateKey: normalizeRegistrationNumber(
          this.state.active.registrationNumber,
        ),
      },
    };
  }
  async saveVehicle() {
    if (this.state.active.fromDraft) {
      this.set({
        notice: "Spara utkastet och använd sedan Ta utkastet i bruk.",
      });
      return;
    }
    if (!this.validateActive() || !this.begin("vehicle")) return;
    const active = this.state.active;
    try {
      const cost = this.writeActive();
      const saved = active.vehicleId
        ? await householdApi.replace(
            active.vehicleId,
            active.baseRevision!,
            cost,
          )
        : await householdApi.create(
            normalizeRegistrationNumber(active.registrationNumber),
            cost,
          );
      if (
        !this.tombstones.has(saved.vehicleId) &&
        this.state.active.token === active.token
      ) {
        const current = this.state.active;
        if (
          !active.vehicleId &&
          normalizeRegistrationNumber(current.registrationNumber) !==
            saved.registrationNumber
        ) {
          this.set({
            notice: `${saved.registrationNumber} har sparats. Din senare redigering för ett annat registreringsnummer finns kvar som nytt underlag.`,
          });
        } else
          this.set({
            active: {
              ...current,
              vehicleId: saved.vehicleId,
              baseRevision: saved.revision,
              state: saved.state,
              registrationNumber: saved.registrationNumber,
              dirty: current.edit !== active.edit,
              reviews: saved.unresolvedLegacyItems,
              ...(current.edit === active.edit
                ? { cost: recoveredCost(saved) }
                : {}),
            },
            notice: "Bilunderlaget har sparats.",
          });
      }
      this.putVehicle(saved);
    } catch (error) {
      this.failure(error, "cost.input");
    } finally {
      this.end();
    }
  }
  private putVehicle(saved: VehicleResponse) {
    if (this.tombstones.has(saved.vehicleId)) return;
    const summary: VehicleSummary = {
      vehicleId: saved.vehicleId,
      registrationNumber: saved.registrationNumber,
      vehicleLabel: saved.vehicleLabel,
      revision: saved.revision,
      state: saved.state,
      reviewItems: reviewSet(saved),
      sourceListingVersion: saved.sourceListingVersion,
      currentListingVersion: saved.currentListingVersion,
      needsListingReview: saved.needsListingReview,
      updatedAtUtc: saved.updatedAtUtc,
    };
    this.set({
      vehicles: { ...this.state.vehicles, [saved.vehicleId]: saved },
      summaries: [
        ...this.state.summaries.filter(
          (item) => item.vehicleId !== saved.vehicleId,
        ),
        summary,
      ].sort((a, b) =>
        a.registrationNumber.localeCompare(b.registrationNumber, "sv-SE"),
      ),
    });
    this.schedule();
  }
  async saveDraft(replaceExisting = false) {
    if (!this.validateActive()) return;
    const active = this.state.active;
    if (!active.draftRevision) {
      this.set({
        notice:
          "Läs det gemensamma utkastets aktuella revision innan du sparar.",
      });
      return;
    }
    const other = this.state.draft?.input?.registrationNumber;
    if (
      other &&
      other !== normalizeRegistrationNumber(active.registrationNumber) &&
      !replaceExisting
    ) {
      if (
        !window.confirm(
          `Ersätta det sparade utkastet för ${other} med ${normalizeRegistrationNumber(active.registrationNumber)}?`,
        )
      )
        return;
      replaceExisting = true;
    }
    if (
      !active.fromDraft &&
      other === normalizeRegistrationNumber(active.registrationNumber) &&
      !window.confirm(
        `Ersätta det sparade utkastet för ${other} med de öppna uppgifterna?`,
      )
    )
      return;
    if (!this.begin("draft")) return;
    try {
      const saved = await householdApi.saveDraft(
        {
          registrationNumber: normalizeRegistrationNumber(
            active.registrationNumber,
          ),
          cost: active.costIncluded ? this.writeActive() : null,
          listing: active.listing,
          baseVehicleId: active.vehicleId,
          baseVehicleRevision: active.baseRevision,
        },
        active.draftRevision,
        replaceExisting,
      );
      this.set({ draft: saved });
      if (this.state.active.token === active.token)
        this.set({
          active: {
            ...this.state.active,
            fromDraft:
              normalizeRegistrationNumber(
                this.state.active.registrationNumber,
              ) === normalizeRegistrationNumber(active.registrationNumber),
            draftRevision: saved.revision,
            dirty: this.state.active.edit !== active.edit,
          },
          notice:
            "Utkastet har sparats. Bilunderlaget ändras först när utkastet tas i bruk.",
        });
    } catch (error) {
      this.failure(error, "input.cost.input");
    } finally {
      this.end();
    }
  }
  async saveListingDraft(
    input: DraftInput,
    expectedRevision: Numeric,
    replaceExisting: boolean,
  ) {
    if (!this.begin("draft")) return;
    try {
      const draft = await householdApi.saveDraft(
        input,
        expectedRevision,
        replaceExisting,
      );
      this.set({
        draft,
        notice: "Annonsutkastet har sparats i den gemensamma utkastplatsen.",
      });
      return draft;
    } catch (error) {
      this.failure(error);
      throw error;
    } finally {
      this.end();
    }
  }
  async openDraft() {
    if (!this.allowReplace()) return;
    this.cancelOpen();
    const sequence = this.openSequence;
    const token = this.state.active.token;
    const edit = this.state.active.edit;
    try {
      const draft = await householdApi.draft();
      if (
        sequence !== this.openSequence ||
        token !== this.state.active.token ||
        edit !== this.state.active.edit
      )
        return;
      const input = draft.input;
      if (!input) {
        this.set({ draft, notice: "Det finns inget sparat utkast." });
        return;
      }
      const vehicle = input.baseVehicleId
        ? await householdApi.vehicle(input.baseVehicleId)
        : null;
      if (
        sequence !== this.openSequence ||
        token !== this.state.active.token ||
        edit !== this.state.active.edit ||
        (input.baseVehicleId && this.tombstones.has(input.baseVehicleId))
      )
        return;
      this.set({
        draft,
        active: {
          ...this.newActive(),
          vehicleId: input.baseVehicleId ?? null,
          baseRevision: input.baseVehicleRevision ?? null,
          registrationNumber: input.registrationNumber,
          cost: cloneExact(
            input.cost ??
              (vehicle
                ? recoveredCost(vehicle)
                : { input: initialVehicle(), listingLinkMode: "preserve" }),
          ),
          costIncluded: input.cost != null,
          listing: cloneExact(input.listing ?? null),
          reviews: vehicle ? reviewSet(vehicle) : [],
          state: vehicle?.state ?? "new",
          fromDraft: true,
          draftRevision: draft.revision,
        },
        notice: "Utkastet har öppnats och finns kvar på servern.",
      });
      this.schedule();
    } catch (error) {
      this.failure(error);
    }
  }
  async adoptDraft() {
    const active = this.state.active;
    if (!active.fromDraft || active.dirty || !active.draftRevision) {
      this.set({ notice: "Spara först ändringarna i det öppnade utkastet." });
      return;
    }
    if (!this.begin("draft")) return;
    try {
      const saved = await householdApi.adopt(active.draftRevision);
      this.putVehicle(saved);
      if (
        this.state.active.token === active.token &&
        !this.tombstones.has(saved.vehicleId)
      ) {
        const changedRegistration =
          !active.vehicleId &&
          normalizeRegistrationNumber(this.state.active.registrationNumber) !==
            saved.registrationNumber;
        this.set({
          active: {
            ...this.state.active,
            vehicleId: changedRegistration ? null : saved.vehicleId,
            baseRevision: changedRegistration ? null : saved.revision,
            fromDraft: false,
            state: changedRegistration ? "new" : saved.state,
            dirty: this.state.active.edit !== active.edit,
            ...(this.state.active.edit === active.edit
              ? { cost: recoveredCost(saved), listing: null }
              : {}),
            reviews: changedRegistration ? [] : saved.unresolvedLegacyItems,
          },
          notice: changedRegistration
            ? `${saved.registrationNumber} har tagits i bruk. Din senare redigering för ett annat registreringsnummer finns kvar som nytt underlag.`
            : "Utkastet har tagits i bruk och platsen är nu tom.",
        });
      }
      const draft = await householdApi.draft();
      this.set({ draft });
      if (this.state.active.token === active.token)
        this.set({
          active: { ...this.state.active, draftRevision: draft.revision },
        });
    } catch (error) {
      this.failure(error);
    } finally {
      this.end();
    }
  }
  async deleteDraft() {
    if (
      !this.state.draft ||
      !window.confirm(
        "Radera det gemensamma utkastet permanent? Bilunderlag och hushållsprofil påverkas inte.",
      )
    )
      return;
    if (!this.begin("draft")) return;
    const token = this.state.active.token;
    const edit = this.state.active.edit;
    try {
      const deleted = await householdApi.deleteDraft(this.state.draft.revision);
      this.set({
        draft: deleted,
        ...(this.state.active.fromDraft &&
        this.state.active.token === token &&
        this.state.active.edit === edit
          ? { active: { ...this.newActive(), draftRevision: deleted.revision } }
          : {}),
        notice: "Det sparade utkastet har raderats.",
      });
      this.schedule();
    } catch (error) {
      this.failure(error);
    } finally {
      this.end();
    }
  }
  async deleteVehicle(vehicle: VehicleSummary) {
    if (
      !window.confirm(
        `Radera ${vehicle.registrationNumber} permanent, inklusive annons, kalkyl och tillhörande utkast?`,
      )
    )
      return;
    if (!this.begin("vehicle")) return;
    try {
      await householdApi.delete(vehicle.vehicleId, vehicle.revision);
      this.forgetVehicle(vehicle.vehicleId);
      vehicleDeleted(vehicle.vehicleId);
      this.set({
        notice: "Bilen och dess tillhörande uppgifter har raderats.",
      });
    } catch (error) {
      this.failure(error);
    } finally {
      this.end();
    }
  }
  forgetVehicle(id: string) {
    this.tombstones.add(id);
    this.writeEpoch++;
    this.cancelOpen();
    const vehicles = { ...this.state.vehicles };
    delete vehicles[id];
    const registration = this.state.summaries.find(
      (item) => item.vehicleId === id,
    )?.registrationNumber;
    const draftMatches =
      this.state.draft?.input?.baseVehicleId === id ||
      (registration &&
        this.state.draft?.input?.registrationNumber === registration);
    this.set({
      vehicles,
      summaries: this.state.summaries.filter((item) => item.vehicleId !== id),
      ...(this.state.active.vehicleId === id
        ? { active: this.newActive() }
        : {}),
      ...(draftMatches ? { draft: null } : {}),
      transition: removeTransitionVehicles(
        this.state.transition,
        new Set([id]),
      ),
    });
    this.schedule();
    void this.refresh();
  }
  async loadTransition(replace = false) {
    if (this.state.transition?.dirty && !replace) {
      this.set({
        notice:
          "Dina övergångsändringar finns kvar. Välj Läs om övergången om du vill ersätta dem.",
      });
      return;
    }
    if (
      replace &&
      !window.confirm(
        "Ersätta de lokala granskningsbesluten med aktuellt övergångsunderlag?",
      )
    )
      return;
    const edit = this.state.transition?.edit;
    const sequence = ++this.transitionSequence;
    const epoch = this.writeEpoch;
    try {
      const snapshot = await householdApi.transition();
      if (
        this.state.transition?.edit !== edit ||
        sequence !== this.transitionSequence ||
        epoch !== this.writeEpoch
      )
        return;
      this.set({
        transition: {
          snapshot,
          costs: Object.fromEntries(
            snapshot.vehicles.map((vehicle) => [
              vehicle.vehicleId,
              recoveredCost(vehicle),
            ]),
          ),
          edit: 0,
          dirty: false,
        },
        notice: null,
      });
      this.schedule();
    } catch (error) {
      this.failure(error);
    }
  }
  editTransition(id: string, cost: CostWrite) {
    const transition = this.state.transition;
    if (!transition) return;
    this.set({
      ...(this.state.active.vehicleId === id
        ? {
            active: {
              ...this.state.active,
              cost: cloneExact(cost),
              edit: this.state.active.edit + 1,
              dirty: true,
            },
          }
        : {}),
      transition: {
        ...transition,
        costs: { ...transition.costs, [id]: cost },
        edit: transition.edit + 1,
        dirty: true,
      },
    });
    this.schedule();
  }
  async confirmTransition() {
    const transition = this.state.transition;
    if (!transition) return;
    const errors = validateFields(
      this.state.profile as Record<string, unknown>,
      profileFields,
      "profile",
    );
    for (const [id, cost] of Object.entries(transition.costs))
      for (const [path, messages] of Object.entries(
        validateCostWrite(
          cost,
          reviewSet(
            transition.snapshot.vehicles.find(
              (vehicle) => vehicle.vehicleId === id,
            )!,
          ),
        ),
      ))
        errors[`transition.${id}.${path}`] = messages;
    if (Object.keys(errors).length) {
      this.set({ errors });
      return;
    }
    if (
      !window.confirm(
        `Bekräfta övergången för samtliga ${transition.snapshot.vehicles.length} äldre bilar? Profilen sparas och äldre hushållsantaganden/resultat tas bort. Kvarvarande granskningsposter behålls.`,
      )
    )
      return;
    if (!this.begin("transition")) return;
    const profileEdit = this.state.profileEdit;
    const profile = cloneExact(this.state.profile);
    const activeToken = this.state.active.token;
    const activeEdit = this.state.active.edit;
    try {
      const result = await householdApi.confirmTransition({
        profile,
        expectedProfileRevision: transition.snapshot.profile.revision,
        expectedTransitionRevision: transition.snapshot.revision,
        vehicles: transition.snapshot.vehicles.map((vehicle) => ({
          vehicleId: vehicle.vehicleId,
          expectedRevision: vehicle.revision,
          cost: cloneExact(transition.costs[vehicle.vehicleId]),
        })),
      });
      this.set({
        savedProfile: result.profile,
        profileDirty: this.state.profileEdit !== profileEdit,
        transition:
          this.state.transition?.edit === transition.edit
            ? null
            : this.state.transition,
        notice:
          "Övergången är genomförd. Kvarvarande granskningsposter visas på respektive bil. Eventuella senare ändringar finns kvar för granskning.",
      });
      // Changed local editors remain recoverable; their old revisions must not be silently rebased.
      if (
        this.state.active.state === "legacyPending" &&
        this.state.active.token === activeToken &&
        this.state.active.edit === activeEdit
      )
        this.set({ active: this.newActive() });
    } catch (error) {
      this.failure(error);
    } finally {
      this.end();
    }
    await this.refresh();
  }
  async confirmListingVersion() {
    const active = this.state.active;
    if (!active.listingSource) return;
    if (
      !window.confirm(
        "Bekräfta att du har granskat den visade annonsversionen? Kostnadsfält ändras bara genom deras egna användningsknappar.",
      )
    )
      return;
    this.editActive({ cost: { ...active.cost, listingLinkMode: "current" } });
  }
  dataForDraft(): DraftInput {
    return {
      registrationNumber: normalizeRegistrationNumber(
        this.state.active.registrationNumber,
      ),
      cost: this.state.active.costIncluded ? this.writeActive() : null,
      listing: this.state.active.listing,
      baseVehicleId: this.state.active.vehicleId,
      baseVehicleRevision: this.state.active.baseRevision,
    };
  }
}

export function changedValue(left: unknown, right: unknown) {
  try {
    return stringifyExact(left) !== stringifyExact(right);
  } catch {
    return true;
  }
}
