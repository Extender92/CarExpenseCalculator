import { useState } from "react";
import type { components } from "@/api/schema";
import { Button } from "@/components/ui/button";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { useOptionalComparison } from "@/features/comparison/use-comparison";
import { labelFor } from "@/features/comparison/catalogue";
import { Evidence } from "@/features/comparison/FactsEditor";
import type { FactEdits, FactWrite } from "@/features/comparison/api";
import { request } from "./api";
import { formatMoney, cloneExact, stringifyExact, type Exact } from "./numbers";
import { fuels, units } from "./form-model";
import { useWorkspace } from "./use-workspace";

type Preview = Exact<components["schemas"]["ListingReusePreviewResponse"]>;
interface Loaded { preview: Preview; token: number; edit: number; factFingerprint: string; }
const warnings: Record<string, string> = {
  electricityBasisRequired: "Elförbrukningen saknar uppgift om den avser batteriet eller inköpt el. Välj mätpunkt i kalkylen.",
  consumptionPairingRequired: "Förbrukningen kan inte kopplas entydigt till ett drivmedel och en körsträcka. Originaluppgifterna finns kvar i annonsen.",
};

export function ListingReusePanel() {
  const { workspace, state } = useWorkspace();
  const comparison = useOptionalComparison();
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [replace, setReplace] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const active = state.active;
  const source = active.listingSource;
  const factEditing = active.vehicleId ? comparison?.state.facts[active.vehicleId] : undefined;
  const factFingerprint = stringifyExact(factEditing?.input ?? {});
  const stale = !!loaded && (loaded.token !== active.token || loaded.edit !== active.edit || loaded.factFingerprint !== factFingerprint);
  if (!source && !active.listing) return null;

  async function preview() {
    setBusy(true); setMessage(null);
    const token = active.token, edit = active.edit;
    try {
      if (active.vehicleId && comparison) await comparison.workspace.select(active.vehicleId);
      if (workspace.state.active.token !== token || workspace.state.active.edit !== edit) return;
      const facts = active.vehicleId ? comparison?.workspace.state.facts[active.vehicleId] : undefined;
      const result = await request<components["schemas"]["ListingReusePreviewResponse"]>("/api/listing-reuse/preview", "POST", {
        ...(source ? { vehicleId: source.vehicleId, expectedVehicleRevision: workspace.state.active.baseRevision,
          expectedListingVersion: source.listingVersion } : { unsavedListing: active.listing }),
        target: active.cost.input, factEdits: facts?.input.edits,
      });
      if (workspace.state.active.token !== token || workspace.state.active.edit !== edit) return;
      setLoaded({ preview: result, token, edit, factFingerprint: stringifyExact(facts?.input ?? {}) });
      setSelected([]); setReplace([]);
    } catch (error) { setMessage((error as Error).message); }
    finally { setBusy(false); }
  }
  const proposal = loaded?.preview;
  const canApplyFacts = !!(source && factEditing && comparison);
  const priceFact = proposal?.factTargets.find(target => target.field === "purchasePriceSek");
  const priceAlreadyApplied = !!proposal?.purchasePrice?.alreadyApplied && (!canApplyFacts || !!priceFact?.alreadyApplied);
  const needsReplacement = proposal ? [
    ...(proposal.purchasePrice && (proposal.purchasePrice.requiresReplacement || (canApplyFacts && priceFact?.requiresReplacement)) && !priceAlreadyApplied ? ["price"] : []),
    ...(proposal.annualTax?.requiresReplacement && !proposal.annualTax.alreadyApplied ? ["tax"] : []),
    ...proposal.energySources.filter(s => s.requiresReplacement && !s.alreadyApplied).map(s => `energy:${s.key}`),
    ...proposal.factTargets.filter(t => t.requiresReplacement && !t.alreadyApplied).map(t => `fact:${t.field}`),
    ...(proposal.facts.conditionNotes && (factEditing?.base.input?.conditionNotes != null || factEditing?.input.edits?.conditionNotes != null) ? ["notes"] : []),
  ] : [];
  const unapprovedReplacement = selected.some(key => needsReplacement.includes(key) && !replace.includes(key));
  function choice(key: string, label: string, replacement: boolean, applied: boolean, disabled = false) {
    return <div key={key} className="space-y-2 rounded-lg border border-slate-700 p-3">
      <label className="flex items-start gap-2"><input type="checkbox" disabled={applied || disabled || stale}
        checked={selected.includes(key)} onChange={event => setSelected(current => event.target.checked ? [...current, key] : current.filter(k => k !== key))} />
        <span>{label}{applied ? " · Redan tillämpat" : ""}</span></label>
      {replacement && !applied && <label className="flex items-start gap-2 text-sm text-amber-200"><input type="checkbox" disabled={disabled || stale}
        checked={replace.includes(key)} onChange={event => setReplace(current => event.target.checked ? [...current, key] : current.filter(k => k !== key))} />
        Jag vill ersätta det befintliga värdet för detta förslag</label>}
    </div>;
  }
  function apply() {
    if (!loaded || stale || unapprovedReplacement) return;
    const p = loaded.preview;
    const accepted = (key: string, requiresReplacement: boolean) => selected.includes(key) && (!requiresReplacement || replace.includes(key));
    let input = cloneExact(active.cost.input);
    const facts: FactWrite = cloneExact(factEditing?.input ?? {});
    const edits: FactEdits = { ...facts.edits };
    const priceFact = p.factTargets.find(f => f.field === "purchasePriceSek");
    if (p.purchasePrice && accepted("price", p.purchasePrice.requiresReplacement || !!priceFact?.requiresReplacement)) {
      input = { ...input, priceSek: p.purchasePrice.value, priceSource: p.purchasePrice.source };
      if (canApplyFacts) edits.purchasePriceSek = { kind: "listing" };
    }
    if (p.annualTax && accepted("tax", p.annualTax.requiresReplacement))
      input = { ...input, tax: { isIncluded: false, items: [p.annualTax.value] } };
    for (const suggestion of p.energySources) {
      if (!accepted(`energy:${suggestion.key}`, suggestion.requiresReplacement)) continue;
      const existing = input.energySources ?? [];
      input = { ...input, energySources: [...existing.filter(item => item.key !== suggestion.key), suggestion.value] };
    }
    if (canApplyFacts) for (const target of p.factTargets) {
      if (accepted(`fact:${target.field}`, target.requiresReplacement))
        Object.assign(edits, { [target.field]: { kind: "listing" } });
    }
    if (canApplyFacts && p.facts.conditionNotes && accepted("notes", factEditing?.base.input?.conditionNotes != null || facts.edits?.conditionNotes != null))
      edits.conditionNotes = p.facts.conditionNotes.map(() => ({ kind: "listing" }));
    if (stringifyExact(input) !== stringifyExact(active.cost.input)) workspace.editActive({ cost: { ...active.cost, input } });
    if (canApplyFacts && stringifyExact(edits) !== stringifyExact(facts.edits ?? {})) comparison!.workspace.editFacts(active.vehicleId!, {
      ...facts, edits, expectedListingVersion: source!.listingVersion,
    });
    setLoaded(null); setMessage("Valda annonsuppgifter har lagts till i det osparade underlaget. Inget värde har bekräftats.");
  }
  return <div className="space-y-3 rounded-xl border border-cyan-900 p-4">
    <Button type="button" variant="secondary" disabled={busy} onClick={() => void preview()}>Använd tillgängliga annonsuppgifter</Button>
    {message && <p role="status">{message}</p>}
    {proposal && <EditorDialog open title="Välj annonsuppgifter att återanvända" onClose={() => setLoaded(null)} actions={
      <Button type="button" disabled={stale || !selected.length || unapprovedReplacement} onClick={apply}>Tillämpa valda förslag</Button>}>
      <p className="mb-4">Förslagen ändrar endast det osparade underlaget. Saknade kostnader förblir okända. Befintliga värden kräver ett separat ersättningsval.</p>
      {stale && <p role="alert" className="text-amber-200">Underlaget ändrades efter förhandsgranskningen. Stäng och hämta förslagen på nytt.</p>}
      <div className="space-y-3">
        {proposal.purchasePrice && choice("price", `Inköpspris: ${formatMoney(proposal.purchasePrice.value)}${canApplyFacts ? " samt motsvarande prisfakta" : ""}`,
          proposal.purchasePrice.requiresReplacement || (canApplyFacts && !!priceFact?.requiresReplacement), priceAlreadyApplied)}
        {proposal.annualTax && choice("tax", `Årlig fordonsskatt: ${formatMoney(proposal.annualTax.value.amountSek?.single)}`, proposal.annualTax.requiresReplacement, proposal.annualTax.alreadyApplied)}
        {proposal.energySources.map(s => choice(`energy:${s.key}`, `${fuels.find(([key]) => key === s.value.fuel)?.[1] ?? "Drivmedel"}${s.value.consumptionPer100Kilometres
          ? `: ${s.value.consumptionPer100Kilometres.single?.text.replace(".", ",") ?? "Okänt"} ${units.find(([key]) => key === s.value.unit)?.[1]} / 100 km · ${s.value.consumptionLabel ?? ""}` : " · Förbrukning saknas eller behöver kopplas manuellt"}`,
          s.requiresReplacement, s.alreadyApplied))}
        {proposal.factTargets.filter(t => t.field !== "purchasePriceSek").map(target => <div key={target.field}>
          {choice(`fact:${target.field}`, `Jämförelsefakta: ${labelFor(target.field)}`, target.requiresReplacement, target.alreadyApplied, !canApplyFacts)}
          {proposal.facts.facts[target.field as keyof typeof proposal.facts.facts]?.observations.map((o, index) => <div key={index} className="px-3">
            <p className="break-words text-sm">{displayValue(o.value)}</p><Evidence evidence={o.evidence} version={o.sourceListingVersion} />
          </div>)}
        </div>)}
        {proposal.facts.conditionNotes && choice("notes", "Skickuppgifter från annonsen", factEditing?.base.input?.conditionNotes != null || factEditing?.input.edits?.conditionNotes != null, false, !canApplyFacts)}
        {proposal.facts.conditionNotes?.map((note, index) => <p key={index} className="whitespace-pre-wrap break-words text-sm">{note.observations.map(o => o.value).join(" · ")}</p>)}
        {unapprovedReplacement && <p role="alert" className="text-amber-200">Välj uttryckligen vilka befintliga värden som får ersättas, eller avmarkera förslagen.</p>}
        {!canApplyFacts && <p className="text-sm text-slate-400">Jämförelsefakta kan väljas efter att annonsen lagts till som bil. Kostnadsförslagen kan användas i utkastet nu.</p>}
        {proposal.warnings.map(warning => <p role="note" key={warning} className="text-amber-200">{warnings[warning] ?? "Kontrollera annonsens uppgifter innan de används."}</p>)}
      </div>
    </EditorDialog>}
  </div>;
}
function displayValue(value: unknown): string {
  if (value === false) return "Nej";
  if (value === true) return "Ja";
  if (value == null) return "Saknas";
  if (Array.isArray(value)) return value.length ? value.map(displayValue).join(", ") : "Uttryckligen tom samling";
  if (typeof value === "object" && "text" in value) return String(value.text);
  return String(value);
}
