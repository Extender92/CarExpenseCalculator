import { AlertTriangle, Link2, ListPlus, LoaderCircle, Sparkles } from "lucide-react";
import { useEffect, useId, useRef, useState, type ReactNode, type Ref } from "react";
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
import { Badge } from "@/components/ui/badge";
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
  expectedRevision: number;
  hasSavedCostScenario: boolean;
}

interface PendingAttach {
  itemId: string;
  vehicleId: string;
  registrationNumber: string;
  expectedRevision: number;
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
  const systemStatus = useSystemStatus();
  const [urlInput, setUrlInput] = useState("");
  const [urlErrors, setUrlErrors] = useState<Record<string, string>>({});
  const [items, setItems] = useState<ListingWorkspaceItem[]>([]);
  const [pendingBatch, setPendingBatch] = useState<PendingBatch | null>(null);
  const [pendingReload, setPendingReload] = useState<PendingReload | null>(null);
  const [pendingClose, setPendingClose] = useState<PendingClose | null>(null);
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);
  const [pendingAttach, setPendingAttach] = useState<PendingAttach | null>(null);
  const [comparison, setComparison] = useState<ComparisonState | null>(null);
  const [savedListings, setSavedListings] = useState<SavedListingSummary[]>([]);
  const [savedListState, setSavedListState] = useState<SavedListingListState>("loading");
  const [savedListError, setSavedListError] = useState<string | null>(null);
  const [busyVehicleId, setBusyVehicleId] = useState<string | null>(null);
  const [pageNotice, setPageNotice] = useState<PageNotice | null>(null);
  const schedulerRef = useRef(new FifoRequestScheduler(2));
  const controllersRef = useRef(new Map<string, AbortController>());
  const urlErrorRef = useRef<HTMLDivElement>(null);
  const actionRef = useRef<HTMLDivElement>(null);
  const pendingCardFocusRef = useRef<string | null>(null);

  useEffect(() => {
    void refreshSavedListings();
    const controllers = controllersRef.current;
    return () => {
      controllers.forEach((controller) => controller.abort());
      controllers.clear();
    };
  }, []);

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
      return analyzeListing(url, controller.signal);
    }, controller.signal).then((response) => {
      if (controllersRef.current.get(id) !== controller) return;
      controllersRef.current.delete(id);
      updateItem(id, (item) => ({
        ...item,
        submittedUrl: response.submittedUrl,
        normalizedUrl: response.normalizedUrl,
        phase: response.status,
        context: analysisResponseToContext(response),
        draft: analyzedDraftForItem(response, item),
        dirty: item.saved !== null,
        error: null,
        persistenceNotice: item.saved
          ? { tone: "warning", message: "Den nya analysen är inte sparad ännu." }
          : null,
        validationErrors: {},
        controller: null,
      }));
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
      dirty: true,
      persistenceNotice: null,
      validationErrors: validationErrors ?? item.validationErrors,
    }));
  }

  function retryItem(item: ListingWorkspaceItem) {
    runAnalysis(item.id, item.normalizedUrl, true);
  }

  function requestClose(item: ListingWorkspaceItem) {
    if (item.dirty) {
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
      const saved = await getSavedListing(summary.vehicleId);
      const state = savedListingToReviewState(saved);
      if (openItem) {
        updateItem(openItem.id, (item) => ({
          ...item,
          ...state,
          dirty: false,
          error: null,
          persistenceNotice: null,
          saving: false,
          validationErrors: {},
          controller: null,
        }));
        focusCard(openItem.id);
      } else {
        const item = savedResponseToWorkspace(saved);
        setItems((current) => [...current, item]);
        focusCard(item.id);
      }
    } catch (error) {
      await handleSavedReadError(error, openItem?.id);
    } finally {
      setBusyVehicleId(null);
    }
  }

  async function saveItem(item: ListingWorkspaceItem) {
    const built = buildSavedListingRequest(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
    if (!built.request || !built.registrationNumber) {
      updateItem(item.id, (current) => ({ ...current, validationErrors: built.errors }));
      focusCard(item.id);
      return;
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
      acceptSavedResponse(item.id, saved, item.saved ? "Ändringarna har sparats." : "Bilen har sparats.");
      await refreshSavedListings();
    } catch (error) {
      await handleSaveError(item.id, built.registrationNumber, error);
    } finally {
      updateItem(item.id, (current) => ({ ...current, saving: false }));
      if (item.saved) setBusyVehicleId(null);
    }
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
    expectedRevision: number,
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
      acceptSavedResponse(candidate.id, saved, "Den sparade bilen har ersatts med dina val.");
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
      acceptSavedResponse(item.id, saved, "Annonsen har kopplats till bilen. Den sparade kalkylen finns kvar.");
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
        convertToUnsavedDraft(open.id, "Bilen har raderats permanent. Uppgifterna ligger kvar här som ett osparat utkast.");
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
        if (open) convertToUnsavedDraft(open.id, "Den sparade bilen finns inte längre. Utkastet finns kvar lokalt.");
        setPageNotice({ tone: "error", message: "Bilen fanns inte längre. Listan har uppdaterats." });
        await refreshSavedListings();
      } else {
        setPageNotice({ tone: "error", message: savedErrorMessage(error) });
      }
    } finally {
      setBusyVehicleId(null);
    }
  }

  function acceptSavedResponse(itemId: string, saved: SavedListingResponse, message: string) {
    const state = savedListingToReviewState(saved);
    updateItem(itemId, (item) => ({
      ...item,
      ...state,
      dirty: false,
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
    setItems((current) => current.map((item) => item.id === id ? updater(item) : item));
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

  const analyzing = items.filter((item) => ["queued", "analyzing", "retrying"].includes(item.phase)).length;
  const analysisDisabled = extractorConfigured === false;
  const openVehicleIds = new Set(items.flatMap((item) => item.saved ? [item.saved.vehicleId] : []));

  return (
    <div className="space-y-8">
      <header className="border-b border-slate-800 pb-8">
        <Badge variant="success">Tillgänglig</Badge>
        <h1 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Analysera URL:er</h1>
        <p className="mt-4 max-w-3xl text-base leading-7 text-slate-400">
          Klistra in upp till tio publika bilannonser, granska uppgifterna och spara bilens aktuella annons.
          Sparade och tillfälliga underlag kan vara öppna samtidigt.
        </p>
      </header>

      {pageNotice && (
        <div role={pageNotice.tone === "error" ? "alert" : "status"} className={pageNoticeClass(pageNotice.tone)}>
          {pageNotice.message}
        </div>
      )}

      <SavedListingsPanel
        state={savedListState}
        listings={savedListings}
        error={savedListError}
        openVehicleIds={openVehicleIds}
        busyVehicleId={busyVehicleId}
        onRetry={() => void refreshSavedListings()}
        onOpen={(listing) => void openSavedListing(listing)}
        onDelete={requestDelete}
      />

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
              <CardDescription>En fullständig HTTP- eller HTTPS-URL per rad. Samma annonssida får bara anges en gång.</CardDescription>
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
                placeholder={"https://www.example.se/annons/123\nhttps://www.example.se/annons/456"}
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
                Analysera URL:er
              </Button>
              <Button type="button" size="lg" variant="secondary" onClick={() => requestBatch("manual")}>
                <ListPlus size={18} /> Skapa manuella utkast
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {pendingBatch && (
        <PendingActionPanel ref={actionRef} title="Ersätt den öppna arbetsytan?">
          <p>Den nya listan stänger alla öppna kort. Osparade ändringar försvinner.</p>
          <ActionButtons confirmLabel="Ersätt arbetsytan" onConfirm={() => startBatch(pendingBatch.mode, pendingBatch.urls)} onCancel={() => setPendingBatch(null)} />
        </PendingActionPanel>
      )}

      {pendingReload && (
        <PendingActionPanel ref={actionRef} title="Läs in den sparade bilen igen?">
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
        <PendingActionPanel ref={actionRef} title="Stäng kortet utan att spara?">
          <p>De osparade ändringarna tas bort från arbetsytan. Den sparade bilen i databasen påverkas inte.</p>
          <ActionButtons confirmLabel="Stäng kort" onConfirm={() => closeItem(pendingClose.itemId)} onCancel={() => setPendingClose(null)} />
        </PendingActionPanel>
      )}

      {pendingAttach && (
        <PendingActionPanel ref={actionRef} title={`Bilen ${pendingAttach.registrationNumber} har redan en sparad kalkyl`}>
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
        <PendingActionPanel ref={actionRef} title={`Radera ${pendingDelete.registrationNumber} permanent?`} danger>
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
      )}

      <section aria-labelledby="analysis-results" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-cyan-400">Arbetsyta</p>
            <h2 id="analysis-results" className="mt-1 text-2xl font-bold">Annonsunderlag</h2>
          </div>
          {items.length > 0 && <span role="status" className="text-sm text-slate-400">{items.length} underlag, {analyzing} pågående</span>}
        </div>
        {items.length === 0
          ? <div className="rounded-2xl border border-dashed border-slate-700 p-8 text-center text-sm text-slate-500">Inga annonsunderlag är öppna ännu.</div>
          : items.map((item) => (
              <div id={`workspace-${item.id}`} key={item.id} tabIndex={-1} className="scroll-mt-6 outline-none focus:ring-2 focus:ring-cyan-400/50">
                <ListingReviewCard
                  item={item}
                  onChange={(draft, errors) => updateDraft(item.id, draft, errors)}
                  onRetry={() => retryItem(item)}
                  onSave={() => void saveItem(item)}
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

function savedResponseToWorkspace(saved: SavedListingResponse): ListingWorkspaceItem {
  workspaceId += 1;
  const state = savedListingToReviewState(saved);
  return {
    id: `listing-${workspaceId}`,
    ...state,
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
      ? (numberOrNull(item.draft.fields.odometerKilometres.input) ?? 0) * 10
      : null,
    status: item.phase === "failed" || item.phase === "queued" || item.phase === "analyzing" || item.phase === "retrying"
      ? "unavailable"
      : item.phase,
    missingFieldCount: 0,
    hasSavedCostScenario: saved.hasSavedCostScenario,
    updatedAtUtc: saved.updatedAtUtc,
  };
}

function numberOrNull(value: string) {
  if (!value) return null;
  const number = Number(value.replace(",", "."));
  return Number.isFinite(number) ? number : null;
}

function analysisErrorMessage(error: unknown) {
  if (error instanceof ListingAnalysisApiError) return error.message;
  return "URL-analysen kunde inte genomföras. Kontrollera anslutningen och försök igen.";
}

function savedErrorMessage(error: unknown) {
  if (error instanceof SavedListingApiError) return error.message;
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

function PendingActionPanel({ ref, title, danger = false, children }: {
  ref: Ref<HTMLDivElement>;
  title: string;
  danger?: boolean;
  children: ReactNode;
}) {
  const titleId = useId();
  useEffect(() => focusLater(ref), [ref]);
  return (
    <div
      ref={ref}
      tabIndex={-1}
      role="alertdialog"
      aria-labelledby={titleId}
      className={`rounded-xl border p-5 outline-none focus:ring-2 ${danger ? "border-rose-400/30 bg-rose-400/10 focus:ring-rose-300" : "border-amber-400/30 bg-amber-400/10 focus:ring-amber-300"}`}
    >
      <h2 id={titleId} className={`font-semibold ${danger ? "text-rose-100" : "text-amber-100"}`}>{title}</h2>
      <div className={`mt-2 text-sm leading-6 ${danger ? "text-rose-100/80" : "text-amber-100/80"}`}>{children}</div>
    </div>
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
