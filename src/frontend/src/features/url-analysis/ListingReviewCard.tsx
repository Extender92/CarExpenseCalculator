import { AlertTriangle, Calculator, ChevronDown, LoaderCircle, RefreshCw, Save, Trash2, X } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { editScalarField, type ListingReviewDraft, type ListingWorkspaceItem } from "./review-model";
import { formatDateTime, formatMoneyInput, fuelOptions, technicalFields, inputClassName } from "./presentation";
import { validateReviewDraft } from "./validation";
import { reuseWarnings } from "@/features/household/apply-listing-reuse";
import { listingReuseValue } from "@/features/household/reuse-value";
import { CarEditor, type CarEditorTab } from "@/components/editing/CarEditor";

interface ListingReviewCardProps {
  item: ListingWorkspaceItem;
  onChange: (draft: ListingReviewDraft, errors?: Record<string, string>) => void;
  onRetry: () => void;
  onPrepare?: () => void;
  onSave: () => Promise<boolean> | void;
  onDiscard?: () => void;
  onAdopt?: () => Promise<boolean>;
  onCalculate?: () => void;
  calculationStatus?: string;
  onClose: () => void;
  onDelete?: () => void;
  onCompareLatest?: () => void;
}

const phaseLabels: Record<ListingWorkspaceItem["phase"], string> = {
  queued: "Väntar",
  analyzing: "Analyserar",
  retrying: "Analyserar igen",
  complete: "Grunduppgifter kompletta",
  partial: "Delvis extraktion",
  unavailable: "Inga användbara annonsuppgifter",
  failed: "Analysen misslyckades",
};

export function ListingReviewCard({
  item,
  onChange,
  onRetry,
  onPrepare,
  onSave,
  onDiscard,
  onAdopt,
  onCalculate,
  calculationStatus,
  onClose,
  onDelete,
  onCompareLatest,
}: ListingReviewCardProps) {
  const [editorTab, setEditorTab] = useState<CarEditorTab>("listing");
  const [reviewOpen, setReviewOpen] = useState(false);
  const [retryConfirmation, setRetryConfirmation] = useState(false);
  const [closedRequest, setClosedRequest] = useState<number | undefined>();
  const busy = ["queued", "analyzing", "retrying"].includes(item.phase) || item.saving || item.workflow?.writing || item.workflow?.preparing;
  const fields = item.draft.fields;
  const heading = fields.vehicleLabel.input || [fields.make.input, fields.model.input, fields.variant.input].filter(Boolean).join(" ") || "Tillfälligt annonsutkast";
  const isReviewOpen = reviewOpen || (item.editorRequest !== undefined && item.editorRequest !== closedRequest);

  async function requestSave(): Promise<boolean> {
    const errors = validateReviewDraft(item.draft);
    onChange(item.draft, errors);
    if (Object.keys(errors).length > 0) {
      setReviewOpen(true);
      return false;
    }
    return (await onSave()) ?? false;
  }

  function requestRetry() {
    if (item.dirty || item.workflow && (JSON.stringify(item.workflow.cost) !== JSON.stringify(item.workflow.costBaseline) || JSON.stringify(item.workflow.facts) !== JSON.stringify(item.workflow.factsBaseline))) {
      setRetryConfirmation(true);
    } else {
      onRetry();
    }
  }

  return (
    <Card className="overflow-hidden" data-testid={`listing-card-${item.id}`}>
      <div className={`h-1 ${phaseColour(item.phase)}`} />
      <CardHeader className="gap-4">
        <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={phaseBadge(item.phase)}>{phaseLabels[item.phase]}</Badge>
              <Badge variant={item.saved && !item.dirty ? "success" : "warning"}>
                {item.saved || item.reviewDraft ? (item.dirty ? "Ändrad sedan sparning" : item.reviewDraft ? "Sparat annonsutkast" : "Sparad") : "Osparat utkast"}
              </Badge>
              {calculationStatus ? <Badge variant="muted">{calculationStatus}</Badge> : item.saved?.hasSavedCostScenario && (item.saved.savedCostScenarioOutdated
                ? <Badge variant="warning">Kalkyl inaktuell</Badge>
                : item.saved.savedCostScenarioSourceListingVersion !== null
                  ? <Badge variant="success">Kalkyl aktuell</Badge>
                  : <Badge variant="muted">Manuell kalkyl</Badge>)}
            </div>
            <CardTitle className="mt-3 break-words">{heading}</CardTitle>

          </div>
          <div className="flex flex-wrap gap-2">
            {!busy && (!item.saved || item.dirty) && (
              <Button type="button" size="sm" onClick={requestSave}>
                <Save size={15} /> {item.saved ? "Spara ändringar" : item.reviewDraft || !fields.registrationNumber.input ? "Spara utkast" : "Lägg till bil"}
              </Button>
            )}
            {!busy && (
              <Button type="button" variant="secondary" size="sm" onClick={requestRetry}>
                <RefreshCw size={15} /> Analysera igen
              </Button>
            )}
            {!busy && item.saved && onCompareLatest && item.persistenceNotice?.action === "compareLatest" && (
              <Button type="button" variant="secondary" size="sm" onClick={onCompareLatest}>
                <RefreshCw size={15} /> Jämför med senaste
              </Button>
            )}
            {!busy && item.saved && onDelete && (
              <Button type="button" variant="ghost" size="sm" className="text-rose-200" onClick={onDelete}>
                <Trash2 size={15} /> Radera bilen
              </Button>
            )}
            {!busy && item.saved && onCalculate && (
              <Button type="button" variant="secondary" size="sm" onClick={onCalculate}>
                <Calculator size={15} /> {calculationStatus ? "Öppna hushållskalkyl" : item.saved.hasSavedCostScenario ? "Öppna kalkyl" : "Skapa kalkyl"}
              </Button>
            )}

            <Button type="button" variant="ghost" size="sm" onClick={onClose}>
              {item.saved ? <X size={15} /> : <Trash2 size={15} />}
              {item.saved ? "Stäng kort" : "Ta bort utkast"}
            </Button>
          </div>
        </div>

        {busy && (
          <p role="status" className="flex items-center gap-2 text-sm text-cyan-300">
            <LoaderCircle className="animate-spin" size={17} /> {item.workflow?.writing ? "Sparar bilens underlag" : item.workflow?.preparing ? "Förbereder kalkyl" : phaseLabels[item.phase]}…
          </p>
        )}
        {item.error && (
          <Notice tone="error">
            <strong>{item.error}</strong> Dina befintliga uppgifter finns kvar och kan kompletteras manuellt.
          </Notice>
        )}
        {item.persistenceNotice && (
          <Notice tone={item.persistenceNotice.tone}>
            {item.persistenceNotice.message}
          </Notice>
        )}
        {item.phase === "unavailable" && !item.error && item.context.requestedModel && (
          <Notice tone="warning">
            Analysen avslutades utan användbara fordonsuppgifter. Underlaget kan kompletteras manuellt.
          </Notice>
        )}

        {item.phase === "unavailable" && !item.context.requestedModel && (
          <Notice tone="warning">
            Utkastet skapades utan automatisk extraktion och kan fyllas i helt manuellt.
          </Notice>
        )}
        {item.saving && (
          <p role="status" className="flex items-center gap-2 text-sm text-cyan-300">
            <LoaderCircle className="animate-spin" size={17} /> Sparar bilen…
          </p>
        )}
      </CardHeader>

      <CardContent className="space-y-5">
        {item.context.requestedModel && <p className="text-sm" role="note">Obekräftade annonsuppgifter. Fältens källor visar direkt hämtat eller AI-tolkat innehåll.
          {!item.context.sources.some(s => s.matchesSubmittedUrl) && " Metadata om öppnad sida saknas. Annonsadressen är en referens; granska uppgifterna."}
        </p>}
        <Summary draft={item.draft} />
        {!item.saved && <label className="block text-sm font-medium">Komplettera registreringsnummer
          <input className={`${inputClassName} mt-2`} value={fields.registrationNumber.input} disabled={busy}
            onChange={event => onChange(editScalarField(item.draft, "registrationNumber", event.target.value, item.normalizedUrl))} />
        </label>}
        {item.workflow && <div className="space-y-2 text-sm">
          {item.workflow.preparing ? <p role="status">Förbereder tillgängliga kostnadsuppgifter…</p> :
            <p>{item.workflow.applied.length ? "Tillgängliga annonsuppgifter har förifyllts. Övriga kostnader är okända." : "Inga ytterligare kostnadsuppgifter har fyllts i."}</p>}
          {!item.workflow.existing && !item.workflow.preparing && <details><summary>Uppgifter till kalkylen</summary>
            <p>Inköpspris: {listingReuseValue(item.workflow.cost.priceSek, "purchasePriceSek")} kr.</p>
            <p>Årlig skatt: {listingReuseValue(item.workflow.cost.tax?.items?.[0]?.amountSek?.single, "annualVehicleTaxSek")} kr.</p>
            {item.workflow.cost.energySources?.map(source => <p key={source.key}>
              {listingReuseValue(source.fuel, "fuelTypes")}: {listingReuseValue(source.consumptionPer100Kilometres?.single, "consumption")} {source.unit === "kilowattHour" ? "kWh" : source.unit === "kilogram" ? "kg" : "liter"}/100 km
              {source.consumptionLabel ? ` · ${source.consumptionLabel}` : ""}.
            </p>)}
            <p>Jämförelsefakta följer annonsen och sparas obekräftade.</p>
          </details>}
          {item.workflow.warnings.map(warning => <p key={warning} className="text-amber-200">{reuseWarnings[warning] ?? warning}
            <button className="ml-2 text-cyan-300 underline" onClick={() => { setEditorTab("cost"); setReviewOpen(true); }}>Komplettera kostnader</button></p>)}
          {item.workflow.stage && !item.draft.fields.registrationNumber.input ? <p role="status">Annonsutkast: {item.reviewDraft && !item.dirty ? "sparat" : "kvar att spara"}. Ingår inte i jämförelsen.</p> : item.workflow.stage && <p role="status">Annons: {item.saved || item.reviewDraft ? "sparad" : "kvar att spara"} · Kostnader: {item.workflow.costsSaved ? "sparade" : "kvar att spara"} · Jämförelsefakta: {item.workflow.factsSaved ? "sparade" : "kvar att spara"}</p>}
          {item.workflow.error && <div role="alert" className="text-rose-300"><p>{item.workflow.error}</p>{!item.saved && <Button variant="secondary" onClick={onPrepare}>Försök förifylla igen</Button>}</div>}
        </div>}
        <details><summary>Källor och analysinformation</summary>            <dl className="mt-2 space-y-1 text-xs text-slate-400">
              <div className="flex flex-col gap-1 sm:flex-row">
                <dt className="font-semibold text-slate-500">Inskickad URL:</dt>
                <dd className="break-all">{item.submittedUrl}</dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row">
                <dt className="font-semibold text-slate-500">Normaliserad URL:</dt>
                <dd className="break-all">{item.normalizedUrl}</dd>
              </div>
            </dl>        {item.context.requestedModel && (
          <p className="text-xs leading-5 text-slate-500">
            Analyserad {formatDateTime(item.context.analyzedAtUtc)} med begärd modell {item.context.requestedModel}.
            Modellnamnet visar konfigurationen och bevisar inte leverantörens faktiska routning.
          </p>
        )}
</details>
        {!fields.registrationNumber.input && <p className="flex items-center gap-2 text-sm text-rose-300">
          <AlertTriangle aria-hidden="true" size={16} /> Registreringsnummer saknas för att lägga till i jämförelsen. Du kan spara utkastet.
        </p>}

        {retryConfirmation && (
          <div role="alertdialog" aria-labelledby={`retry-title-${item.id}`} className="rounded-xl border border-amber-400/30 bg-amber-400/10 p-4">
            <h4 id={`retry-title-${item.id}`} className="font-semibold text-amber-200">Ersätt manuella ändringar?</h4>
            <p className="mt-1 text-sm text-amber-100/80">En lyckad ny analys ersätter hela det redigerade utkastet.</p>
            <div className="mt-3 flex gap-2">
              <Button type="button" size="sm" onClick={() => { setRetryConfirmation(false); onRetry(); }}>Analysera och ersätt</Button>
              <Button type="button" size="sm" variant="ghost" onClick={() => setRetryConfirmation(false)}>Avbryt</Button>
            </div>
          </div>
        )}

        <button
          type="button"
          className="flex w-full items-center justify-between rounded-xl border border-slate-700 bg-slate-950/50 px-4 py-3 text-left text-sm font-semibold text-slate-200 hover:border-slate-600"
          aria-expanded={isReviewOpen}
          onClick={() => setReviewOpen((open) => !open)}
        >
          Redigera bil
          <ChevronDown size={18} className={isReviewOpen ? "rotate-180 transition" : "transition"} />
        </button>

        {isReviewOpen && (
          <CarEditor initialTab={editorTab} open vehicleId={item.saved?.vehicleId ?? null} onClose={() => { setReviewOpen(false); setClosedRequest(item.editorRequest); }}
            listingEditor={{ item, onChange, save: requestSave, discard: () => onDiscard?.(), adopt: onAdopt }} />
        )}
      </CardContent>
    </Card>
  );
}

function Summary({ draft }: { draft: ListingReviewDraft }) {
  const fields = draft.fields;
  const summaries = [
    ["Registrering", fields.registrationNumber.input || "Okänt"],
    ["Pris", fields.priceSek.input ? formatMoneyInput(fields.priceSek.input) : "Okänt"],
    ["Mätarställning", fields.odometerKilometres.input ? `${fields.odometerKilometres.input} mil` : "Okänt"],
    ["Modellår", fields.modelYear.input || "Okänt"],
    ["Ägare", fields.ownerCount.input || "Okänt"],
    ["Dragkrok", fields.towBar.input === "true" ? "Ja" : fields.towBar.input === "false" ? "Nej" : "Okänt"],
    ["Drivmedel", draft.fuelTypes.mode === "values" ? draft.fuelTypes.values.map(value => fuelOptions.find(option => option.value === value)?.label ?? value).join(", ") : "Okänt"],
    ["Växellåda", technicalFields.find(f => f.name === "transmission")?.options?.find(option => option.value === fields.transmission.input)?.label ?? "Okänt"],
  ];
  return (
    <dl className="grid grid-cols-2 gap-3 xl:grid-cols-3">
      {summaries.map(([label, value]) => (
        <div key={label} className="min-w-0 break-words rounded-xl border border-slate-800 bg-slate-950/50 p-3">
          <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
          <dd className="mt-1 text-sm font-semibold text-slate-100">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function Notice({ tone, children }: { tone: "success" | "warning" | "error"; children: ReactNode }) {
  const classes = tone === "error"
    ? "border-rose-400/30 bg-rose-400/10 text-rose-100"
    : tone === "success"
      ? "border-emerald-400/30 bg-emerald-400/10 text-emerald-100"
      : "border-amber-400/30 bg-amber-400/10 text-amber-100";
  return (
    <div role={tone === "error" ? "alert" : "status"} className={`flex gap-3 rounded-xl border p-4 text-sm leading-6 ${classes}`}>
      <AlertTriangle size={18} className="mt-0.5 shrink-0" /> <span>{children}</span>
    </div>
  );
}

function phaseBadge(phase: ListingWorkspaceItem["phase"]): "success" | "warning" | "muted" | "default" {
  if (phase === "complete") return "success";
  if (phase === "partial" || phase === "unavailable" || phase === "failed") return "warning";
  if (phase === "queued") return "muted";
  return "default";
}

function phaseColour(phase: ListingWorkspaceItem["phase"]) {
  if (phase === "complete") return "bg-emerald-400";
  if (phase === "partial" || phase === "unavailable" || phase === "failed") return "bg-amber-400";
  return "bg-cyan-400";
}
