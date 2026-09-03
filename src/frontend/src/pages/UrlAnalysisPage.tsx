import { AlertTriangle, Link2, ListPlus, LoaderCircle, Sparkles } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import {
  analyzeListing,
  ListingAnalysisApiError,
} from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ListingReviewCard } from "@/features/url-analysis/ListingReviewCard";
import { FifoRequestScheduler } from "@/features/url-analysis/request-scheduler";
import {
  analysisResponseToDraft,
  createEmptyReviewDraft,
  type ListingReviewDraft,
  type ListingWorkspaceItem,
} from "@/features/url-analysis/review-model";
import { textareaClassName } from "@/features/url-analysis/presentation";
import { validateListingUrlList, type NormalizedListingUrl } from "@/features/url-analysis/urls";
import { useSystemStatus } from "@/hooks/use-system-status";

type BatchMode = "analyze" | "manual";

interface PendingBatch {
  mode: BatchMode;
  urls: NormalizedListingUrl[];
}

let workspaceId = 0;

export function UrlAnalysisPage() {
  const systemStatus = useSystemStatus();
  const [urlInput, setUrlInput] = useState("");
  const [urlErrors, setUrlErrors] = useState<Record<string, string>>({});
  const [items, setItems] = useState<ListingWorkspaceItem[]>([]);
  const [pendingBatch, setPendingBatch] = useState<PendingBatch | null>(null);
  const schedulerRef = useRef(new FifoRequestScheduler(2));
  const controllersRef = useRef(new Map<string, AbortController>());
  const urlErrorRef = useRef<HTMLDivElement>(null);

  useEffect(() => () => {
    controllersRef.current.forEach((controller) => controller.abort());
    controllersRef.current.clear();
  }, []);

  const extractorConfigured = systemStatus.phase === "loaded"
    ? systemStatus.data.integrations.codexListingExtractionConfigured
    : undefined;

  function requestBatch(mode: BatchMode) {
    const validation = validateListingUrlList(urlInput);
    setUrlErrors(validation.errors);
    if (Object.keys(validation.errors).length > 0) {
      queueMicrotask(() => urlErrorRef.current?.focus());
      return;
    }

    if (items.some((item) => item.dirty)) {
      setPendingBatch({ mode, urls: validation.urls });
      return;
    }
    startBatch(mode, validation.urls);
  }

  function startBatch(mode: BatchMode, urls: NormalizedListingUrl[]) {
    controllersRef.current.forEach((controller) => controller.abort());
    controllersRef.current.clear();
    setPendingBatch(null);
    setUrlErrors({});

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
        analysis: response,
        draft: analysisResponseToDraft(response),
        dirty: false,
        error: null,
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
      validationErrors: validationErrors ?? item.validationErrors,
    }));
  }

  function retryItem(item: ListingWorkspaceItem) {
    runAnalysis(item.id, item.normalizedUrl, true);
  }

  function removeItem(id: string) {
    controllersRef.current.get(id)?.abort();
    controllersRef.current.delete(id);
    setItems((current) => current.filter((item) => item.id !== id));
  }

  function updateItem(id: string, updater: (item: ListingWorkspaceItem) => ListingWorkspaceItem) {
    setItems((current) => current.map((item) => item.id === id ? updater(item) : item));
  }

  const analyzing = items.filter((item) => ["queued", "analyzing", "retrying"].includes(item.phase)).length;
  const analysisDisabled = extractorConfigured === false;

  return (
    <div className="space-y-8">
      <header className="border-b border-slate-800 pb-8">
        <Badge variant="success">Tillgänglig</Badge>
        <h1 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Analysera URL:er</h1>
        <p className="mt-4 max-w-3xl text-base leading-7 text-slate-400">
          Klistra in upp till tio publika bilannonser. Varje sida analyseras separat och du kan granska,
          rätta eller fylla i alla uppgifter manuellt. Underlagen sparas inte ännu.
        </p>
      </header>

      {extractorConfigured === false && (
        <div role="status" className="flex gap-3 rounded-xl border border-amber-400/30 bg-amber-400/10 p-4 text-sm leading-6 text-amber-100">
          <AlertTriangle size={19} className="mt-0.5 shrink-0" />
          <span>Codex-extraktionen är inte konfigurerad. Du kan fortfarande skapa manuella utkast för URL:erna.</span>
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
        <div role="alertdialog" aria-labelledby="replace-workspace-title" className="rounded-xl border border-amber-400/30 bg-amber-400/10 p-5">
          <h2 id="replace-workspace-title" className="font-semibold text-amber-200">Ersätt redigerade utkast?</h2>
          <p className="mt-2 text-sm text-amber-100/80">Den nya listan ersätter alla nuvarande resultat. Manuella ändringar sparas inte.</p>
          <div className="mt-4 flex gap-2">
            <Button type="button" onClick={() => startBatch(pendingBatch.mode, pendingBatch.urls)}>Ersätt utkasten</Button>
            <Button type="button" variant="ghost" onClick={() => setPendingBatch(null)}>Avbryt</Button>
          </div>
        </div>
      )}

      <section aria-labelledby="analysis-results" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-cyan-400">Tillfällig arbetsyta</p>
            <h2 id="analysis-results" className="mt-1 text-2xl font-bold">Annonsunderlag</h2>
          </div>
          {items.length > 0 && <span role="status" className="text-sm text-slate-400">{items.length} underlag, {analyzing} pågående</span>}
        </div>
        {items.length === 0
          ? <div className="rounded-2xl border border-dashed border-slate-700 p-8 text-center text-sm text-slate-500">Inga URL:er har lagts till ännu.</div>
          : items.map((item) => (
              <ListingReviewCard
                key={item.id}
                item={item}
                onChange={(draft, errors) => updateDraft(item.id, draft, errors)}
                onRetry={() => retryItem(item)}
                onRemove={() => removeItem(item.id)}
              />
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
    analysis: null,
    draft: createEmptyReviewDraft(),
    dirty: false,
    error: null,
    validationErrors: {},
    controller: null,
  };
}

function analysisErrorMessage(error: unknown) {
  if (error instanceof ListingAnalysisApiError) return error.message;
  return "URL-analysen kunde inte genomföras. Kontrollera anslutningen och försök igen.";
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === "AbortError";
}
