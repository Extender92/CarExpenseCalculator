import {
  AlertTriangle,
  ChevronDown,
  ExternalLink,
  LoaderCircle,
  Plus,
  RefreshCw,
  Trash2,
} from "lucide-react";
import { useId, useRef, useState, type ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  createEnergyEntry,
  createStringEntry,
  deriveMissingFields,
  editCollection,
  editScalarField,
  type CollectionDraft,
  type EnergyConsumptionDraft,
  type ListingReviewDraft,
  type ListingWorkspaceItem,
  type ScalarFieldName,
  type StringCollectionEntry,
} from "./review-model";
import {
  advertisementFields,
  energyUnitOptions,
  formatDateTime,
  formatMoneyInput,
  fuelOptions,
  historyFields,
  identityFields,
  inputClassName,
  missingFieldLabels,
  provenanceLabel,
  technicalFields,
  textareaClassName,
  type ScalarFieldDefinition,
} from "./presentation";
import { normalizeScalarInput, parseLocalizedNumber, validateReviewDraft } from "./validation";

interface ListingReviewCardProps {
  item: ListingWorkspaceItem;
  onChange: (draft: ListingReviewDraft, errors?: Record<string, string>) => void;
  onRetry: () => void;
  onRemove: () => void;
}

const phaseLabels: Record<ListingWorkspaceItem["phase"], string> = {
  queued: "Väntar",
  analyzing: "Analyserar",
  retrying: "Analyserar igen",
  complete: "Komplett extraktion",
  partial: "Delvis extraktion",
  unavailable: "Ingen verifierad extraktion",
  failed: "Analysen misslyckades",
};

export function ListingReviewCard({ item, onChange, onRetry, onRemove }: ListingReviewCardProps) {
  const [reviewOpen, setReviewOpen] = useState(item.phase === "unavailable" || item.phase === "failed");
  const [retryConfirmation, setRetryConfirmation] = useState(false);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const busy = item.phase === "queued" || item.phase === "analyzing" || item.phase === "retrying";
  const missing = deriveMissingFields(item.draft);
  const fields = item.draft.fields;
  const heading = fields.vehicleLabel.input
    || [fields.make.input, fields.model.input, fields.variant.input].filter(Boolean).join(" ")
    || "Tillfälligt annonsutkast";

  function updateDraft(draft: ListingReviewDraft) {
    setValidationMessage(null);
    onChange(draft, validateReviewDraft(draft));
  }

  function updateScalar(name: ScalarFieldName, input: string) {
    updateDraft(editScalarField(item.draft, name, input, item.normalizedUrl));
  }

  function normalizeScalar(name: ScalarFieldName) {
    const current = item.draft.fields[name].input;
    const normalized = normalizeScalarInput(name, current);
    if (normalized !== current) updateScalar(name, normalized);
  }

  function validateAndFocus() {
    const errors = validateReviewDraft(item.draft);
    onChange(item.draft, errors);
    if (Object.keys(errors).length > 0) {
      setValidationMessage("Rätta fälten nedan innan underlaget används vidare.");
      queueMicrotask(() => errorSummaryRef.current?.focus());
    } else {
      setValidationMessage("Alla ifyllda uppgifter har giltigt format. Utkastet är fortfarande inte sparat.");
    }
  }

  function requestRetry() {
    if (item.dirty) {
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
              <Badge variant="muted">{missing.length} okända fält</Badge>
              <Badge variant="warning">Osparat utkast</Badge>
            </div>
            <CardTitle className="mt-3 break-words">{heading}</CardTitle>
            <dl className="mt-2 space-y-1 text-xs text-slate-400">
              <div className="flex flex-col gap-1 sm:flex-row">
                <dt className="font-semibold text-slate-500">Inskickad URL:</dt>
                <dd className="break-all">{item.submittedUrl}</dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row">
                <dt className="font-semibold text-slate-500">Normaliserad URL:</dt>
                <dd className="break-all">{item.normalizedUrl}</dd>
              </div>
            </dl>
          </div>
          <div className="flex flex-wrap gap-2">
            {!busy && (
              <Button type="button" variant="secondary" size="sm" onClick={requestRetry}>
                <RefreshCw size={15} /> Analysera igen
              </Button>
            )}
            <Button type="button" variant="ghost" size="sm" onClick={onRemove}>
              <Trash2 size={15} /> Ta bort
            </Button>
          </div>
        </div>

        {busy && (
          <p role="status" className="flex items-center gap-2 text-sm text-cyan-300">
            <LoaderCircle className="animate-spin" size={17} /> {phaseLabels[item.phase]}…
          </p>
        )}
        {item.error && (
          <Notice tone="error">
            <strong>{item.error}</strong> Dina befintliga uppgifter finns kvar och kan kompletteras manuellt.
          </Notice>
        )}
        {item.phase === "unavailable" && !item.error && item.analysis && (
          <Notice tone="warning">
            Den inskickade annonssidan kunde inte bekräftas som källa. AI-värden har därför inte använts.
          </Notice>
        )}
        {item.phase === "unavailable" && !item.analysis && (
          <Notice tone="warning">
            Utkastet skapades utan automatisk extraktion och kan fyllas i helt manuellt.
          </Notice>
        )}
        {item.analysis && (
          <p className="text-xs leading-5 text-slate-500">
            Analyserad {formatDateTime(item.analysis.analyzedAtUtc)} med begärd modell {item.analysis.requestedModel}.
            Modellnamnet visar konfigurationen och bevisar inte leverantörens faktiska routning.
          </p>
        )}
      </CardHeader>

      <CardContent className="space-y-5">
        <Summary draft={item.draft} />

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
          aria-expanded={reviewOpen}
          onClick={() => setReviewOpen((open) => !open)}
        >
          Granska och komplettera alla uppgifter
          <ChevronDown size={18} className={reviewOpen ? "rotate-180 transition" : "transition"} />
        </button>

        {reviewOpen && (
          <div className="space-y-6">
            <p className="rounded-xl border border-cyan-400/20 bg-cyan-400/5 p-4 text-sm leading-6 text-slate-300">
              Fälten finns bara i minnet tills du lämnar eller laddar om sidan. Ett ändrat värde markeras som
              manuellt och användarbekräftat. Det går ännu inte att spara annonsunderlag.
            </p>

            {Object.keys(item.validationErrors).length > 0 && (
              <div ref={errorSummaryRef} tabIndex={-1} role="alert" className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4 outline-none focus:ring-2 focus:ring-rose-300">
                <p className="font-semibold text-rose-200">Några uppgifter behöver rättas</p>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-rose-100/80">
                  {Object.entries(item.validationErrors).map(([path, message]) => <li key={path}>{message}</li>)}
                </ul>
              </div>
            )}

            <FieldSection title="Identitet">
              <ScalarFields definitions={identityFields} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar} />
            </FieldSection>

            <FieldSection title="Annons">
              <ScalarFields definitions={advertisementFields} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar} />
              {fields.odometerKilometres.input && !parseLocalizedNumber(fields.odometerKilometres.input).error && (
                <p className="text-xs text-slate-500">
                  Motsvarar {(parseLocalizedNumber(fields.odometerKilometres.input).value! * 10).toLocaleString("sv-SE", { maximumFractionDigits: 3 })} km.
                </p>
              )}
            </FieldSection>

            <FieldSection title="Tekniska uppgifter">
              <FuelTypesEditor draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
              <ScalarFields definitions={technicalFields} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar} />
              <EnergyEditor draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
            </FieldSection>

            <FieldSection title="Historik och besiktning">
              <ScalarFields definitions={historyFields} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar} />
            </FieldSection>

            <FieldSection title="Utrustning och uppgifter från säljaren">
              <StringCollectionEditor name="equipment" label="Utrustning" maximum={100} draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
              <StringCollectionEditor name="sellerClaims" label="Säljarens påståenden" maximum={20} draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
              <p className="text-xs leading-5 text-amber-300">Säljarens uppgifter är påståenden från annonsen och ska inte tolkas som verifierade fakta.</p>
              <StringCollectionEditor name="conditionNotes" label="Korta skicknoteringar" maximum={10} draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} multiline />
            </FieldSection>

            <FieldSection title="Saknade uppgifter">
              {missing.length === 0
                ? <p className="text-sm text-emerald-300">Inga fält är markerade som okända.</p>
                : <ul className="grid gap-2 text-sm text-slate-300 sm:grid-cols-2">
                    {missing.map((code) => <li key={code} className="rounded-lg bg-slate-950/50 px-3 py-2">{missingFieldLabels[code]}</li>)}
                  </ul>}
            </FieldSection>

            <FieldSection title="Källor och proveniens">
              {item.analysis?.sources.length
                ? <ul className="space-y-2">
                    {item.analysis.sources.map((source) => (
                      <li key={source.url} className="flex flex-col gap-2 rounded-xl border border-slate-800 bg-slate-950/50 p-3 sm:flex-row sm:items-center sm:justify-between">
                        <a href={source.url} target="_blank" rel="noopener noreferrer" className="break-all text-sm text-cyan-300 hover:underline">
                          {source.url} <ExternalLink className="inline" size={13} />
                        </a>
                        <Badge variant={source.matchesSubmittedUrl ? "success" : "muted"}>
                          {source.matchesSubmittedUrl ? "Matchar annonsen" : "Kompletterande källa"}
                        </Badge>
                      </li>
                    ))}
                  </ul>
                : <p className="text-sm text-slate-400">Inga öppnade webbkällor kunde styrkas.</p>}
            </FieldSection>

            <div className="flex flex-wrap items-center gap-3">
              <Button type="button" variant="secondary" onClick={validateAndFocus}>Kontrollera uppgifter</Button>
              {validationMessage && <p role="status" className="text-sm text-slate-300">{validationMessage}</p>}
            </div>
          </div>
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
  ];
  return (
    <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {summaries.map(([label, value]) => (
        <div key={label} className="rounded-xl border border-slate-800 bg-slate-950/50 p-3">
          <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
          <dd className="mt-1 text-sm font-semibold text-slate-100">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function ScalarFields({ definitions, item, disabled, onInput, onBlur }: {
  definitions: ScalarFieldDefinition[];
  item: ListingWorkspaceItem;
  disabled: boolean;
  onInput: (name: ScalarFieldName, value: string) => void;
  onBlur: (name: ScalarFieldName) => void;
}) {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {definitions.map((definition) => {
        const field = item.draft.fields[definition.name];
        const error = item.validationErrors[definition.name];
        const id = `${item.id}-${definition.name}`;
        return (
          <label key={definition.name} htmlFor={id} className="block text-sm font-medium text-slate-300">
            {definition.label}{definition.suffix ? ` (${definition.suffix})` : ""}
            {definition.kind === "select" || definition.kind === "boolean" ? (
              <select id={id} aria-label={definition.label} value={field.input} disabled={disabled} aria-invalid={Boolean(error)} aria-describedby={error ? `${id}-error` : `${id}-source`} className={inputClassName} onChange={(event) => onInput(definition.name, event.target.value)}>
                <option value="">Okänt</option>
                {(definition.kind === "boolean"
                  ? [{ value: "true", label: "Ja" }, { value: "false", label: "Nej" }]
                  : definition.options ?? []).map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            ) : (
              <input
                id={id}
                aria-label={definition.label}
                type={definition.kind === "date" ? "date" : "text"}
                inputMode={definition.kind === "decimal" ? "decimal" : definition.kind === "integer" ? "numeric" : undefined}
                value={field.input}
                disabled={disabled}
                aria-invalid={Boolean(error)}
                aria-describedby={error ? `${id}-error` : `${id}-source`}
                className={inputClassName}
                onChange={(event) => onInput(definition.name, event.target.value)}
                onBlur={() => onBlur(definition.name)}
              />
            )}
            {error
              ? <span id={`${id}-error`} className="mt-1 block text-xs text-rose-300">{error}</span>
              : <span id={`${id}-source`} className="mt-1 block break-all text-xs font-normal text-slate-500">
                  {provenanceLabel(field.provenance)}{field.provenance ? ` · ${field.provenance.sourceUrl}` : ""}
                </span>}
          </label>
        );
      })}
    </div>
  );
}

function FuelTypesEditor({ draft, normalizedUrl, disabled, onChange, errors }: EditorProps) {
  const collection = draft.fuelTypes;
  function setMode(mode: CollectionDraft<string>["mode"]) {
    onChange({ ...draft, fuelTypes: editCollection(collection, mode, collection.values, normalizedUrl) });
  }
  function toggle(value: string) {
    const values = collection.values.includes(value)
      ? collection.values.filter((entry) => entry !== value)
      : [...collection.values, value];
    onChange({ ...draft, fuelTypes: editCollection(collection, "values", values, normalizedUrl) });
  }
  return (
    <CollectionFrame label="Bränsletyper" mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors.fuelTypes}>
      <div className="flex flex-wrap gap-2">
        {fuelOptions.map((option) => (
          <label key={option.value} className="flex items-center gap-2 rounded-lg border border-slate-700 px-3 py-2 text-sm">
            <input type="checkbox" checked={collection.values.includes(option.value)} disabled={disabled} onChange={() => toggle(option.value)} />
            {option.label}
          </label>
        ))}
      </div>
    </CollectionFrame>
  );
}

function EnergyEditor({ draft, normalizedUrl, disabled, onChange, errors }: EditorProps) {
  const collection = draft.energyConsumptions;
  function commit(mode: CollectionDraft<EnergyConsumptionDraft>["mode"], values = collection.values) {
    onChange({ ...draft, energyConsumptions: editCollection(collection, mode, values, normalizedUrl) });
  }
  function setMode(mode: CollectionDraft<EnergyConsumptionDraft>["mode"]) {
    commit(mode, mode === "values" && collection.values.length === 0 ? [createEnergyEntry()] : collection.values);
  }
  function patch(id: string, update: Partial<EnergyConsumptionDraft>) {
    commit("values", collection.values.map((entry) => entry.id === id ? { ...entry, ...update } : entry));
  }
  return (
    <CollectionFrame label="Energiförbrukning" mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors.energyConsumptions}>
      <div className="space-y-3">
        {collection.values.map((entry, index) => {
          const base = `energyConsumptions.values[${index}]`;
          return (
            <div key={entry.id} className="grid gap-3 rounded-xl border border-slate-800 p-3 md:grid-cols-[1fr_0.8fr_1fr_auto]">
              <SmallInput
                id={`${entry.id}-label`}
                label="Etikett"
                value={entry.label}
                disabled={disabled}
                error={errors[`${base}.label`]}
                onChange={(value) => patch(entry.id, { label: value })}
                onBlur={() => patch(entry.id, { label: normalizeCollectionText(entry.label) })}
              />
              <label className="text-xs text-slate-400">Enhet
                <select aria-label={`Enhet ${index + 1}`} className={inputClassName} value={entry.unit} disabled={disabled} onChange={(event) => patch(entry.id, { unit: event.target.value as EnergyConsumptionDraft["unit"] })}>
                  {energyUnitOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </select>
              </label>
              <SmallInput
                id={`${entry.id}-consumption`}
                label="Per 100 km"
                inputMode="decimal"
                value={entry.consumptionPer100Kilometres}
                disabled={disabled}
                error={errors[`${base}.consumptionPer100Kilometres`]}
                onChange={(value) => patch(entry.id, { consumptionPer100Kilometres: value })}
                onBlur={() => patch(entry.id, { consumptionPer100Kilometres: entry.consumptionPer100Kilometres.trim() })}
              />
              <Button type="button" variant="ghost" size="sm" className="self-end" disabled={disabled} aria-label={`Ta bort energiförbrukning ${index + 1}`} onClick={() => commit("values", collection.values.filter((value) => value.id !== entry.id))}><Trash2 size={15} /></Button>
            </div>
          );
        })}
        <Button type="button" variant="secondary" size="sm" disabled={disabled || collection.values.length >= 2} onClick={() => commit("values", [...collection.values, createEnergyEntry()])}><Plus size={15} /> Lägg till förbrukning</Button>
      </div>
    </CollectionFrame>
  );
}

type StringCollectionName = "equipment" | "sellerClaims" | "conditionNotes";

function StringCollectionEditor({ name, label, maximum, draft, normalizedUrl, disabled, onChange, errors, multiline = false }: EditorProps & {
  name: StringCollectionName;
  label: string;
  maximum: number;
  multiline?: boolean;
}) {
  const collection = draft[name];
  function commit(mode: CollectionDraft<StringCollectionEntry>["mode"], values = collection.values) {
    onChange({ ...draft, [name]: editCollection(collection, mode, values, normalizedUrl) });
  }
  function setMode(mode: CollectionDraft<StringCollectionEntry>["mode"]) {
    commit(mode, mode === "values" && collection.values.length === 0 ? [createStringEntry()] : collection.values);
  }
  return (
    <CollectionFrame label={label} mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors[name]}>
      <div className="space-y-2">
        {collection.values.map((entry, index) => (
          <div key={entry.id} className="flex items-start gap-2">
            {multiline
              ? <textarea aria-label={`${label} ${index + 1}`} className={textareaClassName} value={entry.value} disabled={disabled} aria-invalid={Boolean(errors[`${name}.values[${index}]`])} aria-describedby={errors[`${name}.values[${index}]`] ? `${entry.id}-error` : undefined} onChange={(event) => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: event.target.value } : value))} onBlur={() => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: normalizeCollectionText(value.value) } : value))} />
              : <input aria-label={`${label} ${index + 1}`} className={inputClassName} value={entry.value} disabled={disabled} aria-invalid={Boolean(errors[`${name}.values[${index}]`])} aria-describedby={errors[`${name}.values[${index}]`] ? `${entry.id}-error` : undefined} onChange={(event) => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: event.target.value } : value))} onBlur={() => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: normalizeCollectionText(value.value) } : value))} />}
            <Button type="button" variant="ghost" size="sm" className="mt-2 shrink-0" disabled={disabled} aria-label={`Ta bort ${label.toLocaleLowerCase("sv-SE")} ${index + 1}`} onClick={() => commit("values", collection.values.filter((value) => value.id !== entry.id))}><Trash2 size={15} /></Button>
            {errors[`${name}.values[${index}]`] && <span id={`${entry.id}-error`} className="sr-only">{errors[`${name}.values[${index}]`]}</span>}
          </div>
        ))}
        {Object.entries(errors).filter(([path]) => path.startsWith(`${name}.values`)).map(([path, error]) => <p key={path} className="text-xs text-rose-300">{error}</p>)}
        <Button type="button" variant="secondary" size="sm" disabled={disabled || collection.values.length >= maximum} onClick={() => commit("values", [...collection.values, createStringEntry()])}><Plus size={15} /> Lägg till</Button>
      </div>
    </CollectionFrame>
  );
}

interface EditorProps {
  draft: ListingReviewDraft;
  normalizedUrl: string;
  disabled: boolean;
  onChange: (draft: ListingReviewDraft) => void;
  errors: Record<string, string>;
}

function CollectionFrame({ label, mode, provenance, disabled, onMode, error, children }: {
  label: string;
  mode: CollectionDraft<unknown>["mode"];
  provenance: CollectionDraft<unknown>["provenance"];
  disabled: boolean;
  onMode: (mode: CollectionDraft<unknown>["mode"]) => void;
  error?: string;
  children: ReactNode;
}) {
  const errorId = useId();
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/30 p-4">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
        <label className="text-sm font-medium text-slate-300">{label}
          <select aria-label={label} value={mode} disabled={disabled} className={`${inputClassName} sm:w-52`} aria-invalid={Boolean(error)} aria-describedby={error ? errorId : undefined} onChange={(event) => onMode(event.target.value as CollectionDraft<unknown>["mode"])}>
            <option value="unknown">Okänt</option>
            <option value="empty">Inga</option>
            <option value="values">Ange värden</option>
          </select>
        </label>
        <span className="break-all text-xs text-slate-500">
          {provenanceLabel(provenance)}{provenance ? ` · ${provenance.sourceUrl}` : ""}
        </span>
      </div>
      {error && <p id={errorId} className="mt-2 text-xs text-rose-300">{error}</p>}
      {mode === "values" && <div className="mt-4">{children}</div>}
    </div>
  );
}

function FieldSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4 rounded-2xl border border-slate-800 bg-slate-900/40 p-4 sm:p-5">
      <h4 className="text-base font-semibold text-white">{title}</h4>
      {children}
    </section>
  );
}

function SmallInput({ id, label, value, disabled, error, inputMode, onChange, onBlur }: {
  id: string;
  label: string;
  value: string;
  disabled: boolean;
  error?: string;
  inputMode?: "decimal";
  onChange: (value: string) => void;
  onBlur?: () => void;
}) {
  return (
    <label htmlFor={id} className="text-xs text-slate-400">{label}
      <input id={id} aria-label={label} className={inputClassName} value={value} disabled={disabled} inputMode={inputMode} aria-invalid={Boolean(error)} aria-describedby={error ? `${id}-error` : undefined} onChange={(event) => onChange(event.target.value)} onBlur={onBlur} />
      {error && <span id={`${id}-error`} className="mt-1 block text-xs text-rose-300">{error}</span>}
    </label>
  );
}

function Notice({ tone, children }: { tone: "warning" | "error"; children: ReactNode }) {
  return (
    <div role={tone === "error" ? "alert" : "status"} className={`flex gap-3 rounded-xl border p-4 text-sm leading-6 ${tone === "error" ? "border-rose-400/30 bg-rose-400/10 text-rose-100" : "border-amber-400/30 bg-amber-400/10 text-amber-100"}`}>
      <AlertTriangle size={18} className="mt-0.5 shrink-0" /> <span>{children}</span>
    </div>
  );
}

function normalizeCollectionText(value: string) {
  return value.trim().normalize("NFC");
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
