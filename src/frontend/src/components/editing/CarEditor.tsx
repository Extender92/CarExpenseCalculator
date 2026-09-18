import { useEffect, useId, useRef, useState, useSyncExternalStore } from "react";
import { useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { getSavedListing, replaceSavedListing, SavedListingApiError, type SavedListingResponse } from "@/api/client";
import { vehicleChanged } from "@/lib/vehicle-events";
import { useOptionalWorkspace } from "@/features/household/use-workspace";
import { useOptionalComparison } from "@/features/comparison/use-comparison";
import { VehicleCostEditor } from "@/features/household/VehicleCostEditor";
import { ListingDraftAction } from "@/features/household/ListingDraftAction";
import { fromOrdinary, n } from "@/features/household/numbers";
import { useReviewWorkspace } from "@/features/url-analysis/review-workspace";
import { savedListingToReviewState, buildSavedListingRequest, savedListingValidationErrors, compareListingDrafts } from "@/features/url-analysis/saved-listings";
import { ListingReviewForm } from "@/features/url-analysis/ListingReviewForm";
import { validateReviewDraft } from "@/features/url-analysis/validation";
import type { ListingReviewDraft, ListingWorkspaceItem } from "@/features/url-analysis/review-model";
import { listingNumberText } from "@/features/url-analysis/exact";
import { FactsEditor } from "@/features/comparison/FactsEditor";
import { CostConfirmation } from "@/features/comparison/CostConfirmation";
import { ComparisonEditorActions } from "@/features/comparison/ComparisonEditorActions";
import { focusField } from "@/features/comparison/navigation";
import { EditorDialog, type EditingResource } from "./EditorDialog";
import { costResource, factsResource } from "./resources";

export interface ControlledListingEditor {
  item: ListingWorkspaceItem;
  onChange: (draft: ListingReviewDraft, errors?: Record<string, string>) => void;
  save: () => Promise<boolean>;
  discard: () => void;
  adopt?: () => Promise<boolean>;
}
export type CarEditorTab = "listing" | "cost" | "facts";
const tabs: [CarEditorTab, string][] = [["listing", "Annons"], ["cost", "Kalkyl"], ["facts", "Jämförelsefakta"]];
const noopSubscribe = () => () => {};
const noSnapshot = () => null;

export function CarEditor({ open, vehicleId, listingEditor, initialTab = "listing", field, onClose }: {
  open: boolean; vehicleId: string | null; listingEditor?: ControlledListingEditor;
  initialTab?: CarEditorTab; field?: string | null; onClose: (navigating?: boolean) => void;
}) {
  const household = useOptionalWorkspace();
  const location = useLocation();
  const h = useSyncExternalStore(household?.subscribe ?? noopSubscribe, household?.snapshot ?? noSnapshot);
  const comparison = useOptionalComparison();
  const { items, setItems } = useReviewWorkspace();
  const [tab, setTab] = useState<CarEditorTab>(initialTab);
  const [notice, setNotice] = useState<string | null>(null);
  const [listingConflict, setListingConflict] = useState(false);
  const [latestListing, setLatestListing] = useState<SavedListingResponse | null>(null);
  const id = useId();
  const requested = useRef<string | null>(null);
  const focused = useRef<string | null>(null);
  const existing = items.find(item => item.saved?.vehicleId === vehicleId);
  const item = listingEditor?.item ?? existing;
  const costReady = !!h && (vehicleId ? h.active.vehicleId === vehicleId : !listingEditor);
  const source = costReady ? h?.active.listingSource : null;
  const f = vehicleId ? comparison?.state.facts[vehicleId] : undefined;

  useEffect(() => {
    if (!open || !vehicleId || !household || requested.current === vehicleId) return;
    requested.current = vehicleId;
    void (async () => {
      if (household.state.active.vehicleId !== vehicleId) await household.openVehicle(vehicleId);
      await comparison?.workspace.select(vehicleId);
    })();
  }, [open, vehicleId, household, comparison?.workspace]);
  useEffect(() => {
    if (!source || listingEditor || existing) return;
    const state = savedListingToReviewState(source);
    setItems(current => current.some(x => x.saved?.vehicleId === source.vehicleId) ? current : [...current, {
      ...state, id: `saved-${source.vehicleId}`, baseline: state.draft, dirty: false,
      error: null, saving: false, persistenceNotice: null, validationErrors: {}, controller: null,
    }]);
  }, [source, listingEditor, existing, setItems]);
  useEffect(() => {
    const target = `${vehicleId}:${tab}:${field}`;
    if (field && focused.current !== target && (tab !== "cost" || costReady) && (tab !== "facts" || f)) {
      focused.current = target;
      requestAnimationFrame(() => focusField(field, household?.state.active.cost.input));
    }
  }, [field, tab, costReady, household, vehicleId, f]);

  function update(draft: ListingReviewDraft, errors = validateReviewDraft(draft)) {
    if (listingEditor) return listingEditor.onChange(draft, errors);
    if (item) setItems(current => current.map(x => x.id === item.id ? { ...x, draft, validationErrors: errors,
      dirty: JSON.stringify(draft) !== JSON.stringify(x.baseline), persistenceNotice: null } : x));
  }
  async function saveListing() {
    if (listingEditor) return listingEditor.save();
    if (!item?.saved) return false;
    const built = buildSavedListingRequest(item.submittedUrl, item.normalizedUrl, item.context, item.draft);
    if (!built.request) { update(item.draft, built.errors); return false; }
    setItems(current => current.map(x => x.id === item.id ? { ...x, saving: true } : x));
    try {
      const saved: SavedListingResponse = await replaceSavedListing(item.saved.vehicleId, {
        expectedRevision: item.saved.revision, listing: built.request.listing,
      });
      const state = savedListingToReviewState(saved);
      household?.acknowledgeListingWrite(fromOrdinary(saved), n(listingNumberText(item.saved.revision)));
      setItems(current => current.map(x => x.id === item.id ? { ...x, ...state, baseline: state.draft,
        draft: JSON.stringify(x.draft) === JSON.stringify(item.draft) ? state.draft : x.draft,
        dirty: JSON.stringify(x.draft) !== JSON.stringify(item.draft), saving: false } : x));
      vehicleChanged(saved.vehicleId);
      setListingConflict(false); setLatestListing(null);
      setNotice("Annonsen har sparats. Kalkyl och granskade fakta har behållits.");
      return true;
    } catch (error) {
      setNotice((error as Error).message);
      if (error instanceof SavedListingApiError && error.problem?.code === "revisionConflict") setListingConflict(true);
      if (error && typeof error === "object" && "validationProblem" in error && error.validationProblem)
        update(item.draft, savedListingValidationErrors(error.validationProblem));
      return false;
    } finally { setItems(current => current.map(x => x.id === item.id ? { ...x, saving: false } : x)); }
  }
  async function reviewLatestListing() {
    if (!item?.saved) return;
    try { setLatestListing(await getSavedListing(item.saved.vehicleId)); }
    catch (error) { setNotice((error as Error).message); }
  }
  function acceptLatestListing(keepEdits: boolean) {
    if (!latestListing || !item) return;
    const remote = savedListingToReviewState(latestListing);
    setItems(current => current.map(x => x.id === item.id ? { ...x, ...remote,
      draft: keepEdits ? x.draft : remote.draft, baseline: remote.draft,
      dirty: keepEdits && JSON.stringify(x.draft) !== JSON.stringify(remote.draft),
      validationErrors: keepEdits ? validateReviewDraft(x.draft) : {},
    } : x));
    setListingConflict(false); setLatestListing(null);
    setNotice(keepEdits ? "Dina annonsändringar finns kvar. Spara för att ersätta det granskade underlaget. Kalkyl och fakta sparas separat." : "Den senaste sparade annonsen har öppnats.");
  }
  const listingResource: EditingResource | null = item ? { key: "listing", label: "Annons", dirty: item.dirty,
    busy: item.saving, save: saveListing, discard: () => {
      if (listingEditor) listingEditor.discard();
      else setItems(current => current.map(x => x.id === item.id && x.baseline ? { ...x, draft: x.baseline, dirty: false, validationErrors: {} } : x));
    } } : null;
  // Listing changes come last: pending fact choices retain the exact listing version
  // the user reviewed, and every step uses acknowledged vehicle revisions.
  const resources = [costReady && household ? costResource(household) : null,
    f && comparison && vehicleId ? factsResource(comparison.workspace, vehicleId) : null, listingResource]
    .filter((x): x is EditingResource => x !== null);
  return <EditorDialog open={open} title={`Redigera bil${item?.draft.fields.make.input ? ` – ${item.draft.fields.make.input} ${item.draft.fields.model.input}` : h?.active.registrationNumber && costReady ? ` – ${h.active.registrationNumber}` : ""}`}
    onClose={onClose} resources={resources} activeResource={tab === "listing" && item && !item.saved ? undefined : tab} actions={tab === "listing" && item && !item.saved ? <>
      <Button type="button" disabled={item.saving} onClick={() => void saveListing()}>{item.reviewDraft || !item.draft.fields.registrationNumber.input ? "Spara utkast" : "Lägg till bil"}</Button>
      {item.reviewDraft && listingEditor?.adopt && <Button type="button" disabled={item.saving || !item.draft.fields.registrationNumber.input}
        onClick={() => void listingEditor.adopt!()}>Lägg till bil</Button>}
    </> : undefined}>
    <div role="tablist" aria-label="Bilens underlag" className="mb-5 flex gap-1 border-b border-slate-700" onKeyDown={event => {
      const index = tabs.findIndex(([value]) => value === tab);
      const next = event.key === "ArrowRight" ? (index + 1) % tabs.length : event.key === "ArrowLeft" ? (index + tabs.length - 1) % tabs.length
        : event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 : null;
      if (next == null) return; event.preventDefault(); setTab(tabs[next][0]); document.getElementById(`${id}-${tabs[next][0]}`)?.focus();
    }}>{tabs.map(([value, label]) => <button key={value} id={`${id}-${value}`} type="button" role="tab" aria-selected={tab === value}
      aria-controls={`${id}-panel-${value}`} tabIndex={tab === value ? 0 : -1} onClick={() => setTab(value)}
      className={`min-w-0 flex-1 border-b-2 px-2 py-3 text-sm ${tab === value ? "border-cyan-300 text-cyan-200" : "border-transparent text-slate-300"}`}>{label}</button>)}</div>
    {notice && <p role="alert" className="mb-4 text-amber-200">{notice}</p>}
    {h?.notice && (location.pathname !== "/manual" || tab !== "cost") && <p role="status" className="mb-4 text-amber-200">{h.notice}</p>}
    {comparison?.state.notice && <p role="status" className="mb-4 text-amber-200">{comparison.state.notice}</p>}
    <div role="tabpanel" id={`${id}-panel-listing`} aria-labelledby={`${id}-listing`} hidden={tab !== "listing"}>
      {listingConflict && <div role="alert" className="mb-4 space-y-3 rounded-lg border border-amber-400 p-4">
        <p>Annonsen har ändrats sedan den öppnades. Dina ändringar finns kvar.</p>
        <Button type="button" variant="secondary" onClick={() => void reviewLatestListing()}>Jämför med senaste annons</Button>
        {latestListing && item && <>
          <dl className="space-y-3">{compareListingDrafts(savedListingToReviewState(latestListing).draft, item.draft).map(change =>
            <div key={change.key}><dt className="font-semibold">{change.label}</dt>
              <dd className="whitespace-pre-wrap break-words">Sparat: {change.existingValue}</dd>
              <dd className="whitespace-pre-wrap break-words">Ditt underlag: {change.candidateValue}</dd></div>)}</dl>
          <div className="flex flex-wrap gap-2"><Button type="button" onClick={() => acceptLatestListing(false)}>Använd senaste annonsen</Button>
            <Button type="button" variant="secondary" onClick={() => acceptLatestListing(true)}>Behåll mina annonsändringar efter granskning</Button></div>
        </>}
      </div>}
      {item ? <><ListingReviewForm item={item} onChange={update} />
        <details className="mt-4"><summary>Gemensamt kalkylutkast</summary><ListingDraftAction item={item} /></details>
      </> : <p>Denna bil saknar ett öppet annonsunderlag.</p>}
    </div>
    <div role="tabpanel" id={`${id}-panel-cost`} aria-labelledby={`${id}-cost`} hidden={tab !== "cost"}>
      {costReady ? <VehicleCostEditor /> : <p>{vehicleId ? "Öppnar bilens kalkyl…" : "Lägg till bilen med registreringsnummer innan ett separat kalkylunderlag sparas."}</p>}
    </div>
    <div role="tabpanel" id={`${id}-panel-facts`} aria-labelledby={`${id}-facts`} hidden={tab !== "facts"}>
      {f && comparison && vehicleId ? <><p className="mb-3 text-sm">Aktuell annonsversion: {f.base.currentListingVersion?.text ?? "Saknas"}. Fakta granskade mot version {f.base.factsReviewedListingVersion?.text ?? "Saknas"}.</p>
        {f.remote && <div role="alert"><p>Underlaget har ändrats. Granska de aktuella uppgifterna innan du sparar.</p>
          <FactsEditor readOnly input={{}} value={f.remote.input} proposal={f.remote.listingProposal} manualMode={false} prefix={`vehicle.${vehicleId}.remote`} errors={{}} onChange={() => {}} />
          <Button onClick={() => comparison.workspace.reviewFacts(vehicleId, false)}>Använd aktuella fakta</Button>
          <Button onClick={() => comparison.workspace.reviewFacts(vehicleId, true)}>Behåll mina val efter granskning</Button></div>}
        <FactsEditor input={f.input} value={f.base.input} proposal={f.base.listingProposal} listingVersion={f.base.currentListingVersion}
          manualMode={false} prefix={`vehicle.${vehicleId}.facts`} errors={comparison.state.errors} onChange={input => comparison.workspace.editFacts(vehicleId, input)} />
        <CostConfirmation id={vehicleId} />
        <div className="mt-4 flex flex-wrap gap-3">
          <Button variant="secondary" disabled={!!comparison.state.busy} onClick={() => void comparison.workspace.select(vehicleId, true)}>Läs aktuella biluppgifter</Button>
          <Button variant="secondary" disabled={!!comparison.state.busy || resources.some(resource => resource.dirty || resource.busy)}
            onClick={async () => { await comparison.workspace.remove(vehicleId); if (!comparison.workspace.state.selected) onClose(); }}>Radera bilen permanent</Button>
        </div>
        {resources.some(resource => resource.dirty) && <p className="mt-2 text-sm">Spara eller kasta ändringarna innan bilen raderas.</p>}
      </> : <p>{vehicleId ? "Öppnar jämförelsefakta…" : "Registreringsnummer behövs för att lägga till bilen i jämförelsen."}</p>}
    </div>
    <ComparisonEditorActions />
  </EditorDialog>;
}
