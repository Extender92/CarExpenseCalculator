import { AlertTriangle, Link2, ListPlus, LoaderCircle, Sparkles } from "lucide-react";
import { useEffect, useEffectEvent, useId, useRef, useState, type ReactNode, type Ref } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  analyzeListing,
  createSavedListing,
  deleteSavedListing,
  getSavedListing,
  listSavedListings,
  ListingAnalysisApiError,
  replaceSavedListing,
  SavedListingApiError,
  type SavedListingResponse,
  type SavedListingSummary,
} from "@/api/client";
import { newCarWorkflow, prepareCar, saveCarCostsAndFacts, workflowPending } from "@/features/url-analysis/batch-workflow";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ListingComparisonPanel } from "@/features/url-analysis/ListingComparisonPanel";
import { ListingReviewCard } from "@/features/url-analysis/ListingReviewCard";
import { FifoRequestScheduler } from "@/features/url-analysis/request-scheduler";
import {
  analysisResponseToContext,
  analysisResponseToDraft,
  createEmptyReviewDraft,
  type ListingReviewDraft,
  type ListingWorkspaceItem,
} from "@/features/url-analysis/review-model";
import {
  allComparisonChoicesSelected,
  buildSavedListingRequest,
  buildReviewedListingInput,
  compareListingDrafts,
  createManualReviewContext,
  mergeListingComparison,
  savedListingToReviewState,
  savedListingValidationErrors,
  type ComparisonChoice,
  type ComparisonFieldName,
  type ListingComparisonDifference,
} from "@/features/url-analysis/saved-listings";
import {
  SavedListingsPanel,
  type SavedListingListState,
} from "@/features/url-analysis/SavedListingsPanel";
import { textareaClassName } from "@/features/url-analysis/presentation";
import { validateListingUrlList, type NormalizedListingUrl } from "@/features/url-analysis/urls";
import { useSystemStatus } from "@/hooks/use-system-status";
import { householdApi, HouseholdApiError } from "@/features/household/api";
import { validateVehicle } from "@/features/household/form-model";
import { vehicleStateLabels } from "@/features/household/labels";
import { useOptionalWorkspace } from "@/features/household/use-workspace";
import { readListingForHouseholdDraft } from "@/features/household/listing-read";
import {canonicalNumber,n,shiftDecimal,fromOrdinary,stringifyExact} from "@/features/household/numbers";
import { listingNumberText } from "@/features/url-analysis/exact";
import { vehicleChanged } from "@/lib/vehicle-events";
import { useReviewWorkspace } from "@/features/url-analysis/review-workspace";
import { reviewDraftApi, type ReviewDraftResponse } from "@/features/url-analysis/review-drafts-api";
import { reviewDraftToItem } from "@/features/url-analysis/review-draft-state";
import { validateReviewDraft } from "@/features/url-analysis/validation";
import { factErrors } from "@/features/comparison/preview";
import { useOptionalComparison } from "@/features/comparison/use-comparison";
import { costResource, factsResource } from "@/components/editing/resources";
import { WorkflowNavigationGuard } from "@/features/url-analysis/WorkflowNavigationGuard";
import { EditorDialog } from "@/components/editing/EditorDialog";

type BatchMode = "analyze" | "manual";

interface PendingBatch {
  mode: BatchMode;
  urls: NormalizedListingUrl[];
}

interface PendingReload {
  itemId: string;
  vehicleId: string;
}

interface PendingClose {
  itemId: string;
}

interface PendingDelete {
  vehicleId: string;
  registrationNumber: string;
  expectedRevision: import("@/features/url-analysis/exact").ListingNumber;
  hasSavedCostScenario: boolean;
}

interface PendingAttach {
  itemId: string;
  vehicleId: string;
  registrationNumber: string;
  expectedRevision: import("@/features/url-analysis/exact").ListingNumber;
}

interface ComparisonState {
  itemId: string;
  existing: SavedListingResponse;
  differences: ListingComparisonDifference[];
  choices: Partial<Record<ComparisonFieldName, ComparisonChoice>>;
  busy: boolean;
  stale: boolean;
}

interface PageNotice {
  tone: "success" | "error";
  message: string;
}

let workspaceId = 0;

export function UrlAnalysisPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const householdWorkspace = useOptionalWorkspace();
  const comparisonWorkspace = useOptionalComparison()?.workspace;
  const carWrites = useRef(new Set<string>());
  const allowNavigation = useRef(false);
  const [calculationStatuses, setCalculationStatuses] = useState<Record<string, string>>({});
  const systemStatus = useSystemStatus();
  const [savingBatch, setSavingBatch] = useState(false);
  const [urlInput, setUrlInput] = useState("");
  const [urlErrors, setUrlErrors] = useState<Record<string, string>>({});
  const { items, setItems } = useReviewWorkspace();
  const itemsRef = useRef(items);
  useEffect(() => { itemsRef.current = items; }, [items]);
  const [reviewDrafts, setReviewDrafts] = useState<ReviewDraftResponse[]>([]);
  const [reviewDraftError, setReviewDraftError] = useState<string | null>(null);
  const [draftConflict, setDraftConflict] = useState<{ itemId: string; existing: ReviewDraftResponse } | null>(null);
  const [deleteDraft, setDeleteDraft] = useState<ReviewDraftResponse | null>(null);
  const [pendingBatch, setPendingBatch] = useState<PendingBatch | null>(null);
  const [pendingReload, setPendingReload] = useState<PendingReload | null>(null);
  const [pendingClose, setPendingClose] = useState<PendingClose | null>(null);
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);
  const [pendingAttach, setPendingAttach] = useState<PendingAttach | null>(null);
  const [comparison, setComparison] = useState<ComparisonState | null>(null);
  const [savedListings, setSavedListings] = useState<SavedListingSummary[]>([]);
  useEffect(() => {
    if (!householdWorkspace) return;
    let controller = new AbortController();
    const read = () => {
      controller.abort();
      controller = new AbortController();
      const signal = controller.signal;
      void householdApi.list(signal).then(vehicles => {
        if (!signal.aborted) {
          setCalculationStatuses(Object.fromEntries(vehicles.map(vehicle => [
            vehicle.vehicleId,
            `${vehicleStateLabels[vehicle.state]}${vehicle.needsListingReview ? " · Annonsgranskning behövs" : ""}`,
          ])));
        }
      }).catch(() => {
        if (!signal.aborted) setCalculationStatuses({});
      });
    };
    read();
    window.addEventListener("focus", read);
    return () => {
      controller.abort();
      window.removeEventListener("focus", read);
    };
  }, [householdWorkspace, savedListings]);
  const [savedListState, setSavedListState] = useState<SavedListingListState>("loading");
  const [savedListError, setSavedListError] = useState<string | null>(null);
  const [busyVehicleId, setBusyVehicleId] = useState<string | null>(null);
  const [pageNotice, setPageNotice] = useState<PageNotice | null>(null);
  const schedulerRef = useRef(new FifoRequestScheduler(1));
  const [pausedQueue, setPausedQueue] = useState<{message: string; until: number} | null>(null);
  const controllersRef = useRef(new Map<string, AbortController>());
  const urlErrorRef = useRef<HTMLDivElement>(null);
  const actionRef = useRef<HTMLDivElement>(null);
  const pendingCardFocusRef = useRef<string | null>(null);

  useEffect(() => {
    void refreshSavedListings();
    void refreshReviewDrafts();
    const controllers = controllersRef.current;
    return () => {
      controllers.forEach((controller) => controller.abort());
      controllers.clear();
      setItems(current => current.map(item => item.controller ? { ...item, controller: null,
        phase: "failed", error: "Analysen avbröts när du lämnade sidan. Befintliga uppgifter finns kvar." } : item));
    };
  }, [setItems]);

  useEffect(() => {
    if (items.some(item => item.saved && !item.workflow))
      setItems(current => current.map(item => item.saved && !item.workflow ? { ...item,
        workflow: { ...newCarWorkflow(), selected: false, existing: true, costsSaved: true, factsSaved: true } } : item));
  }, [items, setItems]);

  const preparingItems = useRef(new Set<string>());
  const prepareUnopened = useEffectEvent(prepareItem);
  useEffect(() => {
    for (const item of items) {
      if (!item.saved && !item.controller && !preparingItems.current.has(item.id) && !item.workflow?.writing && !item.workflow?.error &&
          (!item.workflow || item.workflow.preparedListing !== JSON.stringify(item.draft)) &&
          ["complete", "partial"].includes(item.phase) && !Object.keys(validateReviewDraft(item.draft)).length)
        void prepareUnopened(item);
    }
  }, [items]);

  const deepLink = searchParams.get("reviewDraftId") ?? searchParams.get("vehicleId") ?? searchParams.get("analysisId");
  const deepLinkKind = searchParams.has("reviewDraftId") ? "draft" : searchParams.has("vehicleId") ? "vehicle" : "analysis";
  useEffect(() => {
    if (!deepLink) return;
    let live = true;
    async function open() {
      try {
        if (deepLinkKind === "analysis") {
          setItems(current => current.map(item => item.id === deepLink ? { ...item, editorRequest: Date.now() } : item));
          return;
        }
        const loaded: ListingWorkspaceItem = deepLinkKind === "draft"
          ? reviewDraftToItem(await reviewDraftApi.read(deepLink!))
          : { ...createWorkspaceItem({ submitted: "", normalized: "" } as NormalizedListingUrl, "manual"),
            ...savedListingToReviewState(await getSavedListing(deepLink!)) };
        if (!live) return;
        setItems(current => {
          const existing = current.find(item => deepLinkKind === "draft" ? item.reviewDraft?.id === deepLink : item.saved?.vehicleId === deepLink);
          return existing ? current.map(item => item === existing ? { ...item, editorRequest: Date.now() } : item)
            : [...current, { ...loaded, baseline: loaded.draft, editorRequest: Date.now() }];
        });
      } catch (error) { if (live) setPageNotice({ tone: "error", message: savedErrorMessage(error) }); }
    }
    void open();
    return () => { live = false; };
  }, [deepLink, deepLinkKind, setItems]);

  useEffect(() => {
    const id = pendingCardFocusRef.current;
    if (!id) return;
    const card = document.getElementById(`workspace-${id}`);
    if (!card) return;
    card.focus();
    pendingCardFocusRef.current = null;
  }, [items]);

  const extractorConfigured = systemStatus.phase === "loaded"
    ? systemStatus.data.integrations.codexListingExtractionConfigured
    : undefined;

  async function refreshSavedListings() {
    setSavedListState("loading");
    setSavedListError(null);
    try {
      setSavedListings(await listSavedListings());
      setSavedListState("ready");
    } catch (error) {
      setSavedListError(savedErrorMessage(error));
      setSavedListState("error");
    }
  }

  async function refreshReviewDrafts() {
    try { setReviewDrafts(await reviewDraftApi.list()); setReviewDraftError(null); }
    catch (error) { setReviewDraftError(savedErrorMessage(error)); }
  }

  async function prepareItem(item: ListingWorkspaceItem) {
    if (preparingItems.current.has(item.id)) return;
    preparingItems.current.add(item.id);
    updateItem(item.id, current => ({ ...current, workflow: { ...(current.workflow ?? newCarWorkflow()), preparing: true, error: undefined } }));
    try {
      const workflow = await prepareCar(item);
      updateItem(item.id, current => ({ ...current, workflow: JSON.stringify(current.draft) !== JSON.stringify(item.draft)
        ? { ...current.workflow!, preparing: false }
        : { ...workflow, selected: current.workflow?.selected ?? workflow.selected } }));
    } catch (error) {
      updateItem(item.id, current => ({ ...current, workflow: { ...(current.workflow ?? newCarWorkflow()), preparing: false, error: (error as Error).message } }));
    } finally { preparingItems.current.delete(item.id); }
  }

  function openReviewDraft(value: ReviewDraftResponse) {
    const loaded = reviewDraftToItem(value);
    setItems(current => {
      const existing = current.find(item => item.reviewDraft?.id === value.id);
      return existing ? current.map(item => item === existing ? { ...item, editorRequest: Date.now() } : item)
        : [...current, { ...loaded, editorRequest: Date.now() }];
    });
  }

  function requestBatch(mode: BatchMode) {
    const validation = validateListingUrlList(urlInput);
    setUrlErrors(validation.errors);
    if (Object.keys(validation.errors).length > 0) {
      queueMicrotask(() => urlErrorRef.current?.focus());
      return;
    }

    if (items.length > 0) {
      setPendingBatch({ mode, urls: validation.urls });
      focusAction();
      return;
    }
    startBatch(mode, validation.urls);
  }

  function startBatch(mode: BatchMode, urls: NormalizedListingUrl[]) {
    controllersRef.current.forEach((controller) => controller.abort());
    controllersRef.current.clear();
    setPendingBatch(null);
    setUrlErrors({});
    setComparison(null);
    setPendingAttach(null);

    const nextItems = urls.map((url) => createWorkspaceItem(url, mode));
    setItems(nextItems);
    if (mode === "analyze") {
      nextItems.forEach((item, index) => runAnalysis(item.id, item.submittedUrl, false, index));
    }
  }

  function runAnalysis(id: string, url: string, retry: boolean, lineIndex?: number) {
    controllersRef.current.get(id)?.abort();
    const controller = new AbortController();
    controllersRef.current.set(id, controller);
    updateItem(id, (item) => ({
      ...item,
      phase: retry ? "retrying" : "queued",
      error: null,
      persistenceNotice: null,
      controller,
    }));

    void schedulerRef.current.schedule(async () => {
      updateItem(id, (item) => ({ ...item, phase: retry ? "retrying" : "analyzing" }));
      try {
        return await analyzeListing(url, controller.signal);
      } catch (error) {
        // Pause before the scheduler releases capacity and starts another queued URL.
        if (!controller.signal.aborted && controllersRef.current.get(id) === controller && error instanceof ListingAnalysisApiError &&
            (error.code === "listingSourceRateLimited" || error.code === "listingSourceBlocked")) {
          schedulerRef.current.pause();
          setPausedQueue({message: error.message, until: Date.now() + (error.retryAfterSeconds ?? 0) * 1000});
        }
        throw error;
      }
    }, controller.signal).then(async (response) => {
      if (controllersRef.current.get(id) !== controller) return;
      controllersRef.current.delete(id);
      updateItem(id, (item) => {
        const draft = analyzedDraftForItem(response, item);
        return ({
        ...item,
        submittedUrl: response.submittedUrl,
        normalizedUrl: response.normalizedUrl,
        phase: response.status,
        workflow: !item.saved && ["complete", "partial"].includes(response.status) ? { ...newCarWorkflow(), preparing: true } : item.workflow,
        context: analysisResponseToContext(response),
        draft,
        baseline: item.saved || item.reviewDraft ? item.baseline : draft,
        dirty: item.saved !== null || item.reviewDraft !== undefined,
        error: null,
        persistenceNotice: item.saved
          ? { tone: "warning", message: "Den nya analysen är inte sparad ännu." }
          : null,
        validationErrors: {},
        controller: null,
      }); });
      // Prefill starts from committed workspace state in the effect above.
      // A paint callback can run before React publishes a completed analysis.
    }).catch((error: unknown) => {
      if (controllersRef.current.get(id) !== controller) return;
      controllersRef.current.delete(id);
      if (isAbortError(error)) return;

      const message = analysisErrorMessage(error);
      if (error instanceof ListingAnalysisApiError && error.validationProblem?.errors?.url) {
        setUrlErrors((current) => ({
          ...current,
          [`server-${id}`]: `${lineIndex === undefined ? "URL" : `Rad ${lineIndex + 1}`}: ${error.validationProblem!.errors!.url.join(" ")}`,
        }));
        queueMicrotask(() => urlErrorRef.current?.focus());
      }
      updateItem(id, (item) => ({
        ...item,
        phase: "failed",
        error: message,
        controller: null,
      }));
    });
  }

  function updateDraft(id: string, draft: ListingReviewDraft, validationErrors?: Record<string, string>) {
    updateItem(id, (item) => ({
      ...item,
      draft,
      dirty: JSON.stringify(draft) !== JSON.stringify(item.baseline ?? createEmptyReviewDraft()),
      persistenceNotice: null,
      validationErrors: validationErrors ?? item.validationErrors,
    }));
  }

  function retryItem(item: ListingWorkspaceItem) {
    runAnalysis(item.id, item.normalizedUrl, true);
  }

  function requestClose(item: ListingWorkspaceItem) {
    if (item.dirty || workflowPending(item)) {
      setPendingClose({ itemId: item.id });
      focusAction();
      return;
    }
    closeItem(item.id);
  }

  function closeItem(id: string) {
    controllersRef.current.get(id)?.abort();
    controllersRef.current.delete(id);
    setItems((current) => current.filter((item) => item.id !== id));
    setPendingClose(null);
    setComparison((current) => current?.itemId === id ? null : current);
    setPendingAttach((current) => current?.itemId === id ? null : current);
  }

  async function openSavedListing(summary: SavedListingSummary, forceReload = false) {
    const openItem = items.find((item) => item.saved?.vehicleId === summary.vehicleId);
    if (openItem && !forceReload) {
      if (openItem.dirty) {
        setPendingReload({ itemId: openItem.id, vehicleId: summary.vehicleId });
        focusAction();
      } else {
        focusCard(openItem.id);
      }
      return;
    }

    setPendingReload(null);
    setBusyVehicleId(summary.vehicleId);
    setPageNotice(null);
    try {
      const state = householdWorkspace
        ? await readListingForHouseholdDraft(summary.vehicleId)
        : savedListingToReviewState(await getSavedListing(summary.vehicleId));
      if (openItem) {
        updateItem(openItem.id, (item) => ({
          ...item,
          ...state,
          baseline: state.draft,
          dirty: false,
          error: null,
          persistenceNotice: null,
          saving: false,
          validationErrors: {},
          controller: null,
        }));
        focusCard(openItem.id);
      } else {
        const item: ListingWorkspaceItem = {
          id: `saved-${++workspaceId}`,
          ...state,
          baseline: state.draft,
          dirty: false,
          error: null,
          persistenceNotice: null,
          saving: false,
          validationErrors: {},
          controller: null,
        };
        setItems((current) => [...current, item]);
        focusCard(item.id);
      }
    } catch (error) {
      await handleSavedReadError(error, openItem?.id);
    } finally {
      setBusyVehicleId(null);
    }
  }

  async function saveItem(item: ListingWorkspaceItem): Promise<boolean> {
    if (item.reviewDraft || (!item.saved && !item.draft.fields.registrationNumber.input)) return saveReviewDraft(item);
    const built = buildSavedListingRequest(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
    if (!built.request || !built.registrationNumber) {
      updateItem(item.id, (current) => ({ ...current, validationErrors: built.errors }));
      focusCard(item.id);
      return false;
    }

    updateItem(item.id, (current) => ({
      ...current,
      saving: true,
      error: null,
      persistenceNotice: null,
      validationErrors: {},
    }));
    if (item.saved) setBusyVehicleId(item.saved.vehicleId);
    try {
      const saved = item.saved
        ? await replaceSavedListing(item.saved.vehicleId, {
            expectedRevision: item.saved.revision,
            listing: built.request.listing,
          })
        : await createSavedListing(built.request);
      acceptSavedResponse(item.id, saved, item.saved ? "Ändringarna har sparats." : "Bilen har sparats.", item.draft);
      if (item.saved) householdWorkspace?.acknowledgeListingWrite(fromOrdinary(saved), n(listingNumberText(item.saved.revision)));
      vehicleChanged(saved.vehicleId);
      await refreshSavedListings();
      return true;
    } catch (error) {
      await handleSaveError(item.id, built.registrationNumber, error);
      return false;
    } finally {
      updateItem(item.id, (current) => ({ ...current, saving: false }));
      if (item.saved) setBusyVehicleId(null);
    }
  }

  async function saveReviewDraft(item: ListingWorkspaceItem, existing?: ReviewDraftResponse): Promise<boolean> {
    const errors = validateReviewDraft(item.draft);
    if (Object.keys(errors).length) {
      updateItem(item.id, current => ({ ...current, validationErrors: errors })); return false;
    }
    updateItem(item.id, current => ({ ...current, saving: true, persistenceNotice: null }));
    try {
      const input = buildReviewedListingInput(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
      const target = existing ?? item.reviewDraft;
      const saved = target ? await reviewDraftApi.replace(target.id, target.revision, input) : await reviewDraftApi.create(input);
      const state = reviewDraftToItem(saved);
      updateItem(item.id, current => ({ ...current, reviewDraft: state.reviewDraft,
        baseline: item.draft, dirty: JSON.stringify(current.draft) !== JSON.stringify(item.draft),
        saving: false, validationErrors: {}, persistenceNotice: { tone: "success", message: "Annonsutkastet har sparats. Bilen ingår ännu inte i jämförelsen." } }));
      setDraftConflict(null);
      await refreshReviewDrafts();
      return true;
    } catch (error) {
      if (error instanceof HouseholdApiError && error.reviewDraftId &&
          ["reviewDraftAlreadyExists", "reviewDraftRevisionConflict"].includes(error.code)) {
        try { setDraftConflict({ itemId: item.id, existing: await reviewDraftApi.read(error.reviewDraftId) }); }
        catch (readError) { setItemPersistenceError(item.id, savedErrorMessage(readError)); }
      }
      setItemPersistenceError(item.id, savedErrorMessage(error));
      return false;
    } finally { updateItem(item.id, current => ({ ...current, saving: false })); }
  }

  async function adoptReviewDraft(item: ListingWorkspaceItem, existing?: { vehicleId: string; revision: import("@/features/url-analysis/exact").ListingNumber }, replacement?: ListingReviewDraft): Promise<boolean> {
    if (!item.reviewDraft) return false;
    const draft = replacement ?? item.draft;
    const built = buildSavedListingRequest(item.submittedUrl, item.normalizedUrl, item.context, draft);
    if (!built.request) { updateItem(item.id, current => ({ ...current, validationErrors: built.errors })); return false; }
    updateItem(item.id, current => ({ ...current, saving: true }));
    try {
      let revision = item.reviewDraft.revision;
      if (item.dirty || replacement) {
        const saved = await reviewDraftApi.replace(item.reviewDraft.id, revision, built.request.listing);
        revision = saved.revision;
        updateItem(item.id, current => ({ ...current, reviewDraft: { id: saved.id, revision }, baseline: draft,
          dirty: JSON.stringify(current.draft) !== JSON.stringify(draft) }));
      }
      const saved = await reviewDraftApi.adopt(item.reviewDraft.id, revision, existing?.vehicleId, existing?.revision);
      acceptSavedResponse(item.id, saved, "Bilen har lagts till. Annonsutkastet har förbrukats.", item.draft);
      if (existing) updateItem(item.id, current => ({ ...current, workflow: undefined }));
      if (existing) householdWorkspace?.acknowledgeListingWrite(fromOrdinary(saved), n(listingNumberText(existing.revision)));
      vehicleChanged(saved.vehicleId);
      setComparison(null); setPendingAttach(null);
      await refreshReviewDrafts(); await refreshSavedListings();
      return true;
    } catch (error) {
      if (error instanceof HouseholdApiError && error.code === "registrationNumberConflict" && error.vehicleId)
        await prepareDuplicateResolution(item.id, built.registrationNumber!, error.vehicleId, error.actualRevision ?? n(1));
      else setItemPersistenceError(item.id, savedErrorMessage(error));
      return false;
    } finally { updateItem(item.id, current => ({ ...current, saving: false })); }
  }

  async function handleSaveError(itemId: string, registrationNumber: string, error: unknown) {
    if (!(error instanceof SavedListingApiError)) {
      setItemPersistenceError(itemId, savedErrorMessage(error));
      return;
    }
    if (error.validationProblem) {
      updateItem(itemId, (item) => ({
        ...item,
        validationErrors: savedListingValidationErrors(error.validationProblem!),
        persistenceNotice: { tone: "error", message: error.message },
      }));
      focusCard(itemId);
      return;
    }
    if (error.problem?.code === "registrationNumberConflict" && error.problem.existingVehicleId) {
      await prepareDuplicateResolution(
        itemId,
        registrationNumber,
        error.problem.existingVehicleId,
        error.problem.actualRevision ?? 1,
      );
      return;
    }
    if (error.problem?.code === "revisionConflict") {
      updateItem(itemId, (item) => ({
        ...item,
        persistenceNotice: {
          tone: "error",
          message: "Bilen har ändrats sedan den öppnades. Ditt utkast finns kvar. Välj Jämför med senaste innan du sparar igen.",
          action: "compareLatest",
        },
      }));
      focusCard(itemId);
      return;
    }
    if (error.problem?.code === "savedListingNotFound") {
      convertToUnsavedDraft(itemId, "Den sparade bilen finns inte längre. Utkastet finns kvar och kan sparas på nytt.");
      await refreshSavedListings();
      return;
    }
    setItemPersistenceError(itemId, error.message);
  }

  async function prepareDuplicateResolution(
    itemId: string,
    registrationNumber: string,
    vehicleId: string,
    expectedRevision: import("@/features/url-analysis/exact").ListingNumber,
  ) {
    try {
      const existing = await getSavedListing(vehicleId);
      beginComparison(itemId, existing);
    } catch (error) {
      if (error instanceof SavedListingApiError && error.problem?.code === "savedListingNotFound") {
        setPendingAttach({ itemId, vehicleId, registrationNumber, expectedRevision });
        focusAction();
        return;
      }
      setItemPersistenceError(itemId, savedErrorMessage(error));
    }
  }

  function beginComparison(
    itemId: string,
    existing: SavedListingResponse,
    preservedChoices: Partial<Record<ComparisonFieldName, ComparisonChoice>> = {},
  ) {
    const candidate = items.find((item) => item.id === itemId);
    if (!candidate) return;
    const differences = compareListingDrafts(savedListingToReviewState(existing).draft, candidate.draft);
    const choices = Object.fromEntries(
      differences
        .filter((difference) => preservedChoices[difference.key] !== undefined)
        .map((difference) => [difference.key, preservedChoices[difference.key]]),
    ) as Partial<Record<ComparisonFieldName, ComparisonChoice>>;
    setComparison({
      itemId,
      existing,
      differences,
      choices,
      busy: false,
      stale: false,
    });
    setPendingAttach(null);
    focusAction();
  }

  async function compareWithLatest(itemId: string, vehicleId: string) {
    try {
      const preservedChoices = comparison?.itemId === itemId ? comparison.choices : {};
      beginComparison(itemId, await getSavedListing(vehicleId), preservedChoices);
    } catch (error) {
      if (error instanceof SavedListingApiError && error.problem?.code === "savedListingNotFound") {
        convertToUnsavedDraft(itemId, "Den sparade bilen finns inte längre. Utkastet finns kvar och kan sparas på nytt.");
        await refreshSavedListings();
      } else {
        setItemPersistenceError(itemId, savedErrorMessage(error));
      }
    }
  }

  async function replaceFromComparison() {
    if (!comparison || comparison.stale || !allComparisonChoicesSelected(comparison.differences, comparison.choices)) return;
    const candidate = items.find((item) => item.id === comparison.itemId);
    if (!candidate) {
      setComparison(null);
      return;
    }
    const merged = mergeListingComparison(
      savedListingToReviewState(comparison.existing).draft,
      candidate.draft,
      candidate.normalizedUrl,
      comparison.differences,
      comparison.choices,
    );
    const built = buildSavedListingRequest(candidate.submittedUrl, candidate.normalizedUrl, candidate.context, merged);
    if (candidate.reviewDraft) {
      await adoptReviewDraft(candidate, comparison.existing, merged); return;
    }
    if (!built.request) {
      updateItem(candidate.id, (item) => ({ ...item, draft: merged, validationErrors: built.errors }));
      setComparison(null);
      focusCard(candidate.id);
      return;
    }

    setComparison((current) => current ? { ...current, busy: true } : current);
    setBusyVehicleId(comparison.existing.vehicleId);
    try {
      const saved = await replaceSavedListing(comparison.existing.vehicleId, {
        expectedRevision: comparison.existing.revision,
        listing: built.request.listing,
      });
      acceptSavedResponse(candidate.id, saved, "Den sparade bilen har ersatts med dina val.", candidate.draft);
      updateItem(candidate.id, current => ({ ...current, workflow: undefined }));
      householdWorkspace?.acknowledgeListingWrite(fromOrdinary(saved), n(listingNumberText(comparison.existing.revision)));
      vehicleChanged(saved.vehicleId);
      setComparison(null);
      await refreshSavedListings();
    } catch (error) {
      if (error instanceof SavedListingApiError && error.problem?.code === "revisionConflict") {
        setComparison((current) => current ? { ...current, busy: false, stale: true } : current);
        updateItem(candidate.id, (item) => ({
          ...item,
          persistenceNotice: {
            tone: "error",
            message: "Bilen ändrades igen. Dina val och utkastet finns kvar. Hämta den senaste versionen innan du försöker igen.",
            action: "compareLatest",
          },
          saving: false,
        }));
      } else if (error instanceof SavedListingApiError && error.problem?.code === "savedListingNotFound") {
        setComparison(null);
        convertToUnsavedDraft(candidate.id, "Den sparade bilen finns inte längre. Ditt utkast finns kvar.");
        await refreshSavedListings();
      } else {
        setComparison((current) => current ? { ...current, busy: false } : current);
        setItemPersistenceError(candidate.id, savedErrorMessage(error));
      }
    } finally {
      setBusyVehicleId(null);
    }
  }

  async function attachToScenarioOnly() {
    if (!pendingAttach) return;
    const item = items.find((candidate) => candidate.id === pendingAttach.itemId);
    if (!item) {
      setPendingAttach(null);
      return;
    }
    if (item.reviewDraft) {
      await adoptReviewDraft(item, { vehicleId: pendingAttach.vehicleId, revision: pendingAttach.expectedRevision }); return;
    }
    const built = buildSavedListingRequest(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
    if (!built.request) {
      updateItem(item.id, (current) => ({ ...current, validationErrors: built.errors }));
      setPendingAttach(null);
      return;
    }
    updateItem(item.id, (current) => ({ ...current, saving: true }));
    setBusyVehicleId(pendingAttach.vehicleId);
    try {
      const saved = await replaceSavedListing(pendingAttach.vehicleId, {
        expectedRevision: pendingAttach.expectedRevision,
        listing: built.request.listing,
      });
      acceptSavedResponse(item.id, saved, "Annonsen har kopplats till bilen. Den sparade kalkylen finns kvar.", item.draft);
      updateItem(item.id, current => ({ ...current, workflow: undefined }));
      householdWorkspace?.acknowledgeListingWrite(fromOrdinary(saved), n(listingNumberText(pendingAttach.expectedRevision)));
      vehicleChanged(saved.vehicleId);
      setPendingAttach(null);
      await refreshSavedListings();
    } catch (error) {
      await handleSaveError(item.id, pendingAttach.registrationNumber, error);
      setPendingAttach(null);
    } finally {
      updateItem(item.id, (current) => ({ ...current, saving: false }));
      setBusyVehicleId(null);
    }
  }

  function requestDelete(summary: SavedListingSummary) {
    const open = items.find((item) => item.saved?.vehicleId === summary.vehicleId);
    setPendingDelete({
      vehicleId: summary.vehicleId,
      registrationNumber: summary.registrationNumber,
      expectedRevision: open?.saved?.revision ?? summary.revision,
      hasSavedCostScenario: open?.saved?.hasSavedCostScenario ?? summary.hasSavedCostScenario,
    });
    focusAction();
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setBusyVehicleId(pendingDelete.vehicleId);
    setPageNotice(null);
    try {
      await deleteSavedListing(pendingDelete.vehicleId, pendingDelete.expectedRevision);
      const open = items.find((item) => item.saved?.vehicleId === pendingDelete.vehicleId);
      if (open) {
        closeItem(open.id);
      }
      setPendingDelete(null);
      setPageNotice({ tone: "success", message: `Bilen ${pendingDelete.registrationNumber} har raderats permanent.` });
      await refreshSavedListings();
    } catch (error) {
      setPendingDelete(null);
      if (error instanceof SavedListingApiError && error.problem?.code === "revisionConflict") {
        setPageNotice({
          tone: "error",
          message: "Bilen ändrades innan den kunde raderas. Listan har uppdaterats; kontrollera bilen och bekräfta borttagningen igen.",
        });
        await refreshSavedListings();
      } else if (error instanceof SavedListingApiError && error.problem?.code === "savedListingNotFound") {
        const open = items.find((item) => item.saved?.vehicleId === pendingDelete.vehicleId);
        if (open) closeItem(open.id);
        setPageNotice({ tone: "error", message: "Bilen fanns inte längre. Listan har uppdaterats." });
        await refreshSavedListings();
      } else {
        setPageNotice({ tone: "error", message: savedErrorMessage(error) });
      }
    } finally {
      setBusyVehicleId(null);
    }
  }

  function acceptSavedResponse(itemId: string, saved: SavedListingResponse, message: string, submitted?: ListingReviewDraft) {
    const state = savedListingToReviewState(saved);
    updateItem(itemId, (item) => ({
      ...item,
      ...state,
      householdBaseRevision: undefined,
      baseline: state.draft,
      reviewDraft: undefined,
      draft: submitted && JSON.stringify(item.draft) !== JSON.stringify(submitted) ? item.draft : state.draft,
      dirty: !!submitted && JSON.stringify(item.draft) !== JSON.stringify(submitted),
      error: null,
      persistenceNotice: { tone: "success", message },
      saving: false,
      validationErrors: {},
      controller: null,
    }));
  }

  function convertToUnsavedDraft(itemId: string, message: string) {
    updateItem(itemId, (item) => ({
      ...item,
      saved: null,
      dirty: true,
      persistenceNotice: { tone: "warning", message },
      saving: false,
    }));
  }

  function setItemPersistenceError(itemId: string, message: string) {
    updateItem(itemId, (item) => ({
      ...item,
      persistenceNotice: { tone: "error", message },
      saving: false,
    }));
    focusCard(itemId);
  }

  async function handleSavedReadError(error: unknown, itemId?: string) {
    if (error instanceof SavedListingApiError && error.problem?.code === "savedListingNotFound") {
      if (itemId) {
        convertToUnsavedDraft(itemId, "Den sparade bilen finns inte längre. Utkastet finns kvar lokalt.");
      } else {
        setPageNotice({ tone: "error", message: "Den sparade bilen finns inte längre. Listan har uppdaterats." });
      }
      await refreshSavedListings();
      return;
    }
    setPageNotice({ tone: "error", message: savedErrorMessage(error) });
  }

  function updateItem(id: string, updater: (item: ListingWorkspaceItem) => ListingWorkspaceItem) {
    setItems((current) => {
      const next = current.map((item) => item.id === id ? updater(item) : item);
      itemsRef.current = next;
      return next;
    });
  }

  function focusCard(id: string) {
    pendingCardFocusRef.current = id;
    queueMicrotask(() => {
      const card = document.getElementById(`workspace-${id}`);
      if (!card) return;
      card.focus();
      pendingCardFocusRef.current = null;
    });
  }

  function focusAction() {
    queueMicrotask(() => actionRef.current?.focus());
  }

  async function saveCar(initial: ListingWorkspaceItem): Promise<boolean> {
    if (carWrites.current.has(initial.id) || initial.workflow?.preparing) return false;
    carWrites.current.add(initial.id);
    updateItem(initial.id, current => ({ ...current, workflow: current.workflow && { ...current.workflow, writing: true } }));
    try { return await saveCarWork(initial); }
    finally {
      carWrites.current.delete(initial.id);
      updateItem(initial.id, current => ({ ...current, workflow: current.workflow && { ...current.workflow, writing: false } }));
    }
  }

  async function saveCarWork(initial: ListingWorkspaceItem): Promise<boolean> {
    if (initial.saved && (initial.workflow?.existing || initial.workflow?.costsSaved && initial.workflow?.factsSaved)) {
      const id = initial.saved.vehicleId;
      const resources = [
        householdWorkspace?.state.active.vehicleId === id ? costResource(householdWorkspace) : null,
        comparisonWorkspace?.state.facts[id] ? factsResource(comparisonWorkspace, id) : null,
      ];
      for (const resource of resources) {
        if (resource?.dirty && !await resource.save()) return false;
        await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      }
      const current = itemsRef.current.find(item => item.id === initial.id);
      return !!current && (!current.dirty || await saveItem(current));
    }
    if (!initial.workflow) return initial.reviewDraft && initial.draft.fields.registrationNumber.input
      ? adoptReviewDraft(initial) : saveItem(initial);
    let item = initial;
    if (item.reviewDraft && !item.dirty && !item.draft.fields.registrationNumber.input) return true;
    if (!item.saved || item.dirty) {
      updateItem(item.id, current => ({ ...current, workflow: current.workflow && { ...current.workflow, stage: "listing" } }));
      const ok = item.reviewDraft && item.draft.fields.registrationNumber.input
        ? await adoptReviewDraft(item) : await saveItem(item);
      if (!ok) return false;
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      item = itemsRef.current.find(value => value.id === initial.id)!;
    }
    if (!item.saved) return true;
    const capturedCost = stringifyExact(item.workflow?.cost);
    const capturedFacts = stringifyExact(item.workflow?.facts);
    try {
      await saveCarCostsAndFacts(item, (workflow, revision) => updateItem(initial.id, current => {
        const laterCost = current.workflow && stringifyExact(current.workflow.cost) !== capturedCost &&
          stringifyExact(current.workflow.cost) !== stringifyExact(workflow.cost);
        const laterFacts = current.workflow && stringifyExact(current.workflow.facts) !== capturedFacts &&
          stringifyExact(current.workflow.facts) !== stringifyExact(workflow.facts);
        return { ...current, workflow: { ...workflow, selected: current.workflow?.selected ?? true,
          ...(laterCost ? { cost: current.workflow!.cost, costsSaved: false } : {}),
          ...(laterFacts ? { facts: current.workflow!.facts, factsSaved: false } : {}) },
          saved: current.saved && { ...current.saved, revision } };
      }));
      return true;
    } catch { return false; }
  }

  async function saveBatch() {
    if (savingBatch) return;
    setSavingBatch(true); setPageNotice(null);
    const chosen = itemsRef.current.filter(item => item.workflow?.selected);
    let failed = false;
    try {
      for (const initial of chosen) {
        const item = itemsRef.current.find(value => value.id === initial.id);
        if (!item || !await saveCar(item)) failed = true;
      }
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      const laterEdits = chosen.some(original => {
        const current = itemsRef.current.find(item => item.id === original.id);
        return !current || current.dirty || workflowPending(current) ||
          current.saved?.vehicleId === householdWorkspace?.state.active.vehicleId && !!householdWorkspace?.state.active.dirty ||
          !!current.saved && !!comparisonWorkspace?.state.facts[current.saved.vehicleId]?.dirty;
      });
      if (failed || laterEdits) {
        setPageNotice({ tone: "error", message: "Det valda arbetet är inte helt sparat. Varje bil visar vad som sparades; återstående ändringar finns kvar." });
      } else if (chosen.some(original => itemsRef.current.find(item => item.id === original.id)?.saved)) { allowNavigation.current = true; navigate("/search"); }
      else setPageNotice({ tone: "success", message: "Annonsutkasten är sparade. Komplettera registreringsnummer när du har dem." });
    } finally { setSavingBatch(false); }
  }

  const analyzing = items.filter((item) => ["queued", "analyzing", "retrying"].includes(item.phase)).length;
  const selected = items.filter(item => item.workflow?.selected);
  const selectedCars = selected.filter(item => item.draft.fields.registrationNumber.input.trim()).length;
  const batchBlocked = savingBatch || analyzing > 0 || !selected.length || selected.some(item => item.saving || item.workflow?.writing || item.workflow?.preparing ||
    (!item.saved && !!item.workflow?.error) || Object.keys(validateReviewDraft(item.draft)).length > 0 || !!item.draft.fields.registrationNumber.input && !!item.workflow && !item.workflow.existing && (Object.keys(validateVehicle(item.workflow.cost)).length > 0 || Object.keys(factErrors(item.workflow.facts, "facts")).length > 0));
  const analysisDisabled = extractorConfigured === false || savingBatch;
  const openVehicleIds = new Set(items.flatMap((item) => item.saved ? [item.saved.vehicleId] : []));

  return (
    <div className="space-y-8">
      <WorkflowNavigationGuard items={items} save={saveCar} allow={allowNavigation} discard={ids => setItems(current => current
        .filter(item => !ids.includes(item.id) || !!item.saved || !!item.reviewDraft)
        .map(item => ids.includes(item.id) ? { ...item, draft: item.baseline ?? item.draft, dirty: false, validationErrors: {},
          workflow: item.workflow && { ...item.workflow, cost: item.workflow.costBaseline ?? item.workflow.cost,
            facts: item.workflow.factsBaseline ?? item.workflow.facts, costsSaved: !!item.workflow.costBaselineSaved, factsSaved: !!item.workflow.factsBaselineSaved } } : item))} />
      <header className="border-b border-slate-800 pb-8">
        <h1 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Lägg till bil</h1>
        <p className="mt-4 max-w-3xl text-base leading-7 text-slate-400">
          Klistra in upp till tio Blocket-annonser. En annons hämtas och tolkas åt gången.
          Tillgängliga uppgifter följer med till jämförelsen. Du kan komplettera senare.
        </p>
      </header>

      {pageNotice && (
        <div role={pageNotice.tone === "error" ? "alert" : "status"} className={pageNoticeClass(pageNotice.tone)}>
          {pageNotice.message}
        </div>
      )}
      {deepLinkKind === "analysis" && deepLink && !items.some(item => item.id === deepLink) && <p role="alert">
        Det osparade annonsunderlaget finns inte kvar efter omladdningen. Öppna ett sparat utkast eller analysera annonsen igen.
      </p>}
      <details open={window.location.hash === "#review-drafts" || undefined} id="review-drafts" className="space-y-3 rounded-xl border border-slate-700 p-4"><summary className="cursor-pointer">Fortsätt med sparade annonsutkast ({reviewDrafts.length})</summary><section aria-label="Sparade annonsutkast">
        <h2 className="text-lg font-semibold">Sparade annonsutkast</h2>
        <p className="text-sm text-slate-400">Utkast ingår i jämförelsen först efter att du kompletterat registreringsnummer och valt Lägg till bil.</p>
        {reviewDraftError && <p role="alert">{reviewDraftError}</p>}
        {!reviewDraftError && !reviewDrafts.length && <p>Inga sparade annonsutkast.</p>}
        {reviewDrafts.map(value => <div key={value.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-800 p-3">
          <span className="min-w-0 flex-1 break-all">{[value.input.draft.make?.value, value.input.draft.model?.value].filter(Boolean).join(" ") || value.listingReference}</span>
          <Button type="button" onClick={() => openReviewDraft(value)}>Öppna utkast</Button>
          <Button type="button" variant="ghost" onClick={() => setDeleteDraft(value)}>Radera utkast</Button>
        </div>)}
      </section></details>
      {deleteDraft && <EditorDialog open title="Radera annonsutkast?" onClose={() => setDeleteDraft(null)} actions={<Button type="button" onClick={() => {
        void reviewDraftApi.remove(deleteDraft.id, deleteDraft.revision).then(async () => {
          setItems(current => current.filter(item => item.reviewDraft?.id !== deleteDraft.id)); setDeleteDraft(null); await refreshReviewDrafts();
        }).catch(error => { setPageNotice({ tone: "error", message: savedErrorMessage(error) }); setDeleteDraft(null); });
      }}>Radera detta utkast</Button>}><p>Hela utkastet tas bort. Sparade bilar påverkas inte.</p></EditorDialog>}
      {draftConflict && <EditorDialog open title="Granska sparat annonsutkast" onClose={() => setDraftConflict(null)}>
        <p>Det finns redan ett utkast för samma annons. Kontrollera skillnaderna innan du ersätter det.</p>
        {(() => {
          const candidate = items.find(item => item.id === draftConflict.itemId);
          if (!candidate) return null;
          const previous = reviewDraftToItem(draftConflict.existing);
          const changes = compareListingDrafts(previous.draft, candidate.draft);
          return <><dl className="space-y-3">{[{ key: "registrationNumber", label: "Registreringsnummer", existingValue: previous.draft.fields.registrationNumber.input || "Saknas",
            candidateValue: candidate.draft.fields.registrationNumber.input || "Saknas" }, ...changes].map(change => <div key={change.key}>
            <dt className="font-semibold">{change.label}</dt><dd className="whitespace-pre-wrap break-words">Sparat: {change.existingValue}</dd>
            <dd className="whitespace-pre-wrap break-words">Nytt: {change.candidateValue}</dd></div>)}</dl>
            <div className="mt-4 flex flex-wrap gap-3"><Button type="button" disabled={candidate.saving} onClick={() => void saveReviewDraft(candidate, draftConflict.existing)}>Ersätt utkastet med de nya uppgifterna</Button>
              <Button type="button" variant="secondary" onClick={() => { openReviewDraft(draftConflict.existing); setDraftConflict(null); }}>Öppna sparat utkast</Button></div></>;
        })()}
      </EditorDialog>}
      {pausedQueue && <div role="alert" className="rounded-lg border p-4 space-y-2">
        <p>{pausedQueue.message}</p>
        <Button type="button" onClick={() => {
          if (Date.now() < pausedQueue.until) {
            setPageNotice({tone: "error", message: `Vänta minst ${Math.ceil((pausedQueue.until - Date.now()) / 1000)} sekunder innan kön återupptas.`});
            return;
          }
          setPausedQueue(null);
          schedulerRef.current.resume();
        }}>Fortsätt kön</Button>
      </div>}

      <details><summary className="cursor-pointer">Sparade bilar</summary><SavedListingsPanel
        state={savedListState}
        listings={savedListings}
        calculationStatuses={householdWorkspace ? calculationStatuses : undefined}
        error={savedListError}
        openVehicleIds={openVehicleIds}
        busyVehicleId={busyVehicleId}
        onRetry={() => void refreshSavedListings()}
        onOpen={(listing) => void openSavedListing(listing)}
        onCalculate={(listing) => navigate(`/manual?listingVehicleId=${listing.vehicleId}`)}
        onDelete={requestDelete}
      /></details>

      {extractorConfigured === false && (
        <div role="status" className="flex gap-3 rounded-xl border border-amber-400/30 bg-amber-400/10 p-4 text-sm leading-6 text-amber-100">
          <AlertTriangle size={19} className="mt-0.5 shrink-0" />
          <span>Codex-extraktionen är inte konfigurerad. Du kan fortfarande skapa manuella utkast och hantera sparade bilar.</span>
        </div>
      )}
      {systemStatus.phase === "error" && (
        <div role="status" className="rounded-xl border border-slate-700 bg-slate-900/60 p-4 text-sm text-slate-300">
          Extraktionsstatus kunde inte kontrolleras. Du kan fortfarande försöka analysera eller arbeta manuellt.
        </div>
      )}

      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-xl bg-blue-400/10 text-blue-300"><Link2 size={22} /></span>
            <div>
              <CardTitle>Annonslänkar</CardTitle>
              <CardDescription>En fullständig annonslänk per rad. Automatisk hämtning stöder Blockets bilannonser via HTTPS. Samma annonssida får bara anges en gång.</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); requestBatch("analyze"); }}>
            <label htmlFor="listing-urls" className="text-sm font-medium text-slate-300">URL:er
              <textarea
                id="listing-urls"
                value={urlInput}
                rows={5}
                maxLength={20_489}
                className={textareaClassName}
                aria-invalid={Object.keys(urlErrors).length > 0}
                aria-describedby="listing-url-help listing-url-errors"
                placeholder={"https://www.blocket.se/mobility/item/123\nhttps://www.blocket.se/mobility/item/456"}
                onChange={(event) => { setUrlInput(event.target.value); setUrlErrors({}); }}
              />
            </label>
            <p id="listing-url-help" className="text-xs leading-5 text-slate-500">
              URL:erna skickas endast till appens eget API. Webbläsaren hämtar aldrig annonssidorna direkt.
            </p>
            {Object.keys(urlErrors).length > 0 && (
              <div id="listing-url-errors" ref={urlErrorRef} tabIndex={-1} role="alert" className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4 outline-none focus:ring-2 focus:ring-rose-300">
                <p className="font-semibold text-rose-200">Kontrollera URL:erna</p>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-rose-100/80">
                  {Object.entries(urlErrors).map(([path, error]) => <li key={path}>{error}</li>)}
                </ul>
              </div>
            )}
            <div className="flex flex-wrap gap-3">
              <Button type="submit" size="lg" disabled={analysisDisabled}>
                {analyzing > 0 ? <LoaderCircle className="animate-spin" size={18} /> : <Sparkles size={18} />}
                Hämta annonser
              </Button>
              <Button type="button" size="lg" variant="secondary" onClick={() => requestBatch("manual")}>
                <ListPlus size={18} /> Skapa manuella utkast
              </Button>
              <Link to="/manual?newCar=1" className="self-center text-cyan-300 underline">Lägg till bil manuellt utan annons</Link>
            </div>
          </form>
        </CardContent>
      </Card>

      {pendingBatch && (
        <PendingActionPanel ref={actionRef} title="Ersätt den öppna arbetsytan?" onClose={() => setPendingBatch(null)}>
          <p>Den nya listan stänger alla öppna kort. Osparade ändringar försvinner.</p>
          <ActionButtons confirmLabel="Ersätt arbetsytan" onConfirm={() => startBatch(pendingBatch.mode, pendingBatch.urls)} onCancel={() => setPendingBatch(null)} />
        </PendingActionPanel>
      )}

      {pendingReload && (
        <PendingActionPanel ref={actionRef} title="Läs in den sparade bilen igen?" onClose={() => setPendingReload(null)}>
          <p>Lokala ändringar i det öppna kortet ersätts med den sparade versionen.</p>
          <ActionButtons
            confirmLabel="Läs in sparad version"
            onConfirm={() => {
              const summary = savedListings.find((entry) => entry.vehicleId === pendingReload.vehicleId);
              if (summary) void openSavedListing(summary, true);
              else setPendingReload(null);
            }}
            onCancel={() => setPendingReload(null)}
          />
        </PendingActionPanel>
      )}

      {pendingClose && (
        <PendingActionPanel ref={actionRef} title="Spara ändringarna innan du stänger kortet?" onClose={() => setPendingClose(null)}>
          <p>Annonsens osparade ändringar finns kvar tills du väljer hur de ska hanteras.</p>
          <div className="mt-4 flex flex-wrap gap-2">
            <Button disabled={items.find(item => item.id === pendingClose.itemId)?.saving} onClick={async () => {
              const item = itemsRef.current.find(item => item.id === pendingClose.itemId);
              if (!item || !await saveCar(item)) return;
              await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
              if (!itemsRef.current.find(current => current.id === item.id)?.dirty) closeItem(item.id);
            }}>Spara och stäng</Button>
            <Button variant="secondary" disabled={items.find(item => item.id === pendingClose.itemId)?.saving} onClick={() => closeItem(pendingClose.itemId)}>Kasta ändringar</Button>
            <Button variant="ghost" onClick={() => setPendingClose(null)}>Fortsätt redigera</Button>
          </div>
        </PendingActionPanel>
      )}

      {pendingAttach && (
        <PendingActionPanel ref={actionRef} title={`Bilen ${pendingAttach.registrationNumber} har redan en sparad kalkyl`} onClose={() => setPendingAttach(null)}>
          <p>Det finns ingen sparad annons att jämföra med. Du kan koppla den nya annonsen till bilen utan att ändra eller ta bort kalkylen.</p>
          <ActionButtons
            confirmLabel="Koppla annons till befintlig bil"
            busy={busyVehicleId === pendingAttach.vehicleId}
            onConfirm={() => void attachToScenarioOnly()}
            onCancel={() => setPendingAttach(null)}
          />
        </PendingActionPanel>
      )}

      {pendingDelete && (
        <PendingActionPanel ref={actionRef} title={`Radera ${pendingDelete.registrationNumber} permanent?`} onClose={() => setPendingDelete(null)} danger>
          <p>Hela bilen och den sparade annonsen tas bort permanent.</p>
          {pendingDelete.hasSavedCostScenario && (
            <p className="mt-2 font-semibold text-rose-100">Bilen har också en sparad kalkyl som raderas samtidigt.</p>
          )}
          <ActionButtons
            confirmLabel="Radera bilen permanent"
            danger
            busy={busyVehicleId === pendingDelete.vehicleId}
            onConfirm={() => void confirmDelete()}
            onCancel={() => setPendingDelete(null)}
          />
        </PendingActionPanel>
      )}

      {comparison && (
        <EditorDialog open title="Granska skillnader mot sparad bil" onClose={() => { if (!comparison.busy) setComparison(null); }}>
        <ListingComparisonPanel
          ref={actionRef}
          registrationNumber={comparison.existing.registrationNumber}
          differences={comparison.differences}
          choices={comparison.choices}
          busy={comparison.busy}
          stale={comparison.stale}
          onChoice={(field, choice) => setComparison((current) => current ? { ...current, choices: { ...current.choices, [field]: choice } } : current)}
          onReplace={() => void replaceFromComparison()}
          onCompareLatest={() => void compareWithLatest(comparison.itemId, comparison.existing.vehicleId)}
          onOpenExisting={() => {
            const summary = summaryFromSaved(comparison.existing);
            setComparison(null);
            void openSavedListing(summary);
          }}
          onCancel={() => setComparison(null)}
        />
        </EditorDialog>
      )}

      <section aria-labelledby="analysis-results" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-cyan-400">Arbetsyta</p>
            <h2 id="analysis-results" className="mt-1 text-2xl font-bold">Annonsunderlag</h2>
          </div>
          {items.length > 0 && <span role="status" className="text-sm text-slate-400">{items.length} underlag, {analyzing} pågående</span>}
        </div>
        {items.length > 0 && <div className="sticky top-16 z-20 flex flex-wrap items-center gap-3 rounded-xl border border-cyan-900 bg-slate-950 p-4 lg:top-0">
          <Button disabled={batchBlocked} onClick={() => void saveBatch()}>Spara och jämför ({selectedCars} {selectedCars === 1 ? "bil" : "bilar"}, {selected.length - selectedCars} utkast)</Button>
          {analyzing > 0 && <p role="status">Vänta tills alla annonser har hämtats.</p>}
          <p className="text-sm text-slate-400">Uppgifterna sparas obekräftade. Saknade uppgifter kan kompletteras senare.</p>
        </div>}
        {items.length === 0
          ? <div className="rounded-2xl border border-dashed border-slate-700 p-8 text-center text-sm text-slate-500">Inga annonsunderlag är öppna ännu.</div>
          : items.map((item) => (
              <div id={`workspace-${item.id}`} key={item.id} tabIndex={-1} className="scroll-mt-6 outline-none focus:ring-2 focus:ring-cyan-400/50">
                {item.workflow && <label className="mb-2 flex items-center gap-2"><input type="checkbox" checked={item.workflow.selected} disabled={savingBatch}
                  onChange={event => updateItem(item.id, current => ({ ...current, workflow: current.workflow && { ...current.workflow, selected: event.target.checked } }))} />Ta med i samlad sparning</label>}
                <ListingReviewCard
                  item={item}
                  calculationStatus={householdWorkspace && item.saved ? calculationStatuses[item.saved.vehicleId] ?? "Kalkylstatus kunde inte läsas" : undefined}
                  onChange={(draft, errors) => updateDraft(item.id, draft, errors)}
                  onRetry={() => retryItem(item)}
                  onPrepare={() => void prepareItem(item)}
                  onSave={() => saveCar(item)}
                  onAdopt={item.reviewDraft ? () => saveCar(item) : undefined}
                  onDiscard={() => updateItem(item.id, current => ({ ...current, draft: current.baseline ?? createEmptyReviewDraft(), dirty: false, validationErrors: {},
                    workflow: current.workflow && { ...current.workflow,
                      cost: current.workflow.costBaseline ?? current.workflow.cost,
                      facts: current.workflow.factsBaseline ?? current.workflow.facts,
                      costsSaved: current.saved ? !!current.workflow.costBaselineSaved : current.workflow.costsSaved,
                      factsSaved: current.saved ? !!current.workflow.factsBaselineSaved : current.workflow.factsSaved } }))}
                  onCalculate={item.saved
                    ? () => navigate(`/manual?listingVehicleId=${item.saved!.vehicleId}`)
                    : undefined}
                  onClose={() => requestClose(item)}
                  onDelete={item.saved ? () => requestDelete(summaryFromItem(item)) : undefined}
                  onCompareLatest={item.saved ? () => void compareWithLatest(item.id, item.saved!.vehicleId) : undefined}
                />
              </div>
            ))}
      </section>
    </div>
  );
}

function createWorkspaceItem(url: NormalizedListingUrl, mode: BatchMode): ListingWorkspaceItem {
  workspaceId += 1;
  return {
    id: `listing-${workspaceId}`,
    submittedUrl: url.submitted,
    normalizedUrl: url.normalized,
    phase: mode === "analyze" ? "queued" : "unavailable",
    context: createManualReviewContext(),
    draft: createEmptyReviewDraft(),
    saved: null,
    dirty: false,
    error: null,
    persistenceNotice: null,
    saving: false,
    validationErrors: {},
    controller: null,
  };
}

function analyzedDraftForItem(
  response: Parameters<typeof analysisResponseToDraft>[0],
  item: ListingWorkspaceItem,
) {
  const draft = analysisResponseToDraft(response);
  if (!item.saved) return draft;
  return {
    ...draft,
    fields: {
      ...draft.fields,
      registrationNumber: {
        input: item.saved.registrationNumber,
        provenance: item.draft.fields.registrationNumber.provenance
          ? { ...item.draft.fields.registrationNumber.provenance }
          : null,
      },
    },
  };
}

function summaryFromSaved(saved: SavedListingResponse): SavedListingSummary {
  return {
    vehicleId: saved.vehicleId,
    registrationNumber: saved.registrationNumber,
    vehicleLabel: saved.listing.vehicleLabel?.value ?? null,
    revision: saved.revision,
    listingVersion: saved.listingVersion,
    listingSchemaVersion: saved.listingSchemaVersion,
    make: saved.listing.make?.value ?? null,
    model: saved.listing.model?.value ?? null,
    modelYear: saved.listing.modelYear?.value ?? null,
    priceSek: saved.listing.priceSek?.value ?? null,
    odometerKilometres: saved.listing.odometerKilometres?.value ?? null,
    status: saved.status,
    missingFieldCount: saved.missingFields.length,
    hasSavedCostScenario: saved.hasSavedCostScenario,
    savedCostScenarioSourceListingVersion: saved.savedCostScenarioSourceListingVersion,
    savedCostScenarioOutdated: saved.savedCostScenarioOutdated,
    updatedAtUtc: saved.updatedAtUtc,
  };
}

function summaryFromItem(item: ListingWorkspaceItem): SavedListingSummary {
  const saved = item.saved!;
  return {
    vehicleId: saved.vehicleId,
    registrationNumber: saved.registrationNumber,
    vehicleLabel: item.draft.fields.vehicleLabel.input || null,
    revision: saved.revision,
    listingVersion: saved.listingVersion,
    listingSchemaVersion: saved.listingSchemaVersion,
    make: item.draft.fields.make.input || null,
    model: item.draft.fields.model.input || null,
    modelYear: numberOrNull(item.draft.fields.modelYear.input),
    priceSek: numberOrNull(item.draft.fields.priceSek.input),
    odometerKilometres: item.draft.fields.odometerKilometres.input
      ? numberOrNull(item.draft.fields.odometerKilometres.input, 1)
      : null,
    status: item.phase === "failed" || item.phase === "queued" || item.phase === "analyzing" || item.phase === "retrying"
      ? "unavailable"
      : item.phase,
    missingFieldCount: 0,
    hasSavedCostScenario: saved.hasSavedCostScenario,
    savedCostScenarioSourceListingVersion: saved.savedCostScenarioSourceListingVersion,
    savedCostScenarioOutdated: saved.savedCostScenarioOutdated,
    updatedAtUtc: saved.updatedAtUtc,
  };
}

function numberOrNull(value: string, shift = 0) {
  if (!value) return null;
  try { return n(shiftDecimal(canonicalNumber(value), shift)); } catch { return null; }
}

function analysisErrorMessage(error: unknown) {
  if (error instanceof ListingAnalysisApiError) return error.message;
  return "URL-analysen kunde inte genomföras. Kontrollera anslutningen och försök igen.";
}

function savedErrorMessage(error: unknown) {
  if (error instanceof SavedListingApiError || error instanceof HouseholdApiError) return error.message;
  return "Sparade annonser kunde inte hanteras just nu. Kontrollera anslutningen och försök igen.";
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === "AbortError";
}

function focusLater(ref: Ref<HTMLDivElement>) {
  if (typeof ref === "object" && ref !== null && "current" in ref) {
    queueMicrotask(() => ref.current?.focus());
  }
}

function PendingActionPanel({ ref, title, danger = false, children, onClose }: {
  onClose: () => void;
  ref: Ref<HTMLDivElement>;
  title: string;
  danger?: boolean;
  children: ReactNode;
}) {
  const titleId = useId();
  useEffect(() => focusLater(ref), [ref]);
  return (
    <EditorDialog open title={title} onClose={onClose}><div
      ref={ref}
      tabIndex={-1}
      role="alertdialog"
      aria-labelledby={titleId}
      className={`rounded-xl border p-5 outline-none focus:ring-2 ${danger ? "border-rose-400/30 bg-rose-400/10 focus:ring-rose-300" : "border-amber-400/30 bg-amber-400/10 focus:ring-amber-300"}`}
    >
      <h2 id={titleId} className={`font-semibold ${danger ? "text-rose-100" : "text-amber-100"}`}>{title}</h2>
      <div className={`mt-2 text-sm leading-6 ${danger ? "text-rose-100/80" : "text-amber-100/80"}`}>{children}</div>
    </div></EditorDialog>
  );
}

function ActionButtons({ confirmLabel, danger = false, busy = false, onConfirm, onCancel }: {
  confirmLabel: string;
  danger?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="mt-4 flex flex-wrap gap-2">
      <Button
        type="button"
        variant="default"
        className={danger ? "bg-rose-600 text-white hover:bg-rose-500" : undefined}
        disabled={busy}
        onClick={onConfirm}
      >
        {confirmLabel}
      </Button>
      <Button type="button" variant="ghost" disabled={busy} onClick={onCancel}>Avbryt</Button>
    </div>
  );
}

function pageNoticeClass(tone: PageNotice["tone"]) {
  return tone === "error"
    ? "rounded-xl border border-rose-400/30 bg-rose-400/10 p-4 text-sm text-rose-100"
    : "rounded-xl border border-emerald-400/30 bg-emerald-400/10 p-4 text-sm text-emerald-100";
}
