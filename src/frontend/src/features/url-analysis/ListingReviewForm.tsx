import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { AlertTriangle, ExternalLink, Plus, Trash2 } from "lucide-react";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { canonicalNumber, formatNumeric, n, shiftDecimal } from "@/features/household/numbers";
import { ListingDetailsEditor } from "./ListingDetailsEditor";
import { emptyDetails } from "./details";
import { createEnergyEntry, createStringEntry, deriveMissingFields, editCollection, editScalarField, manualProvenance,
  type CollectionDraft, type EnergyConsumptionDraft, type ListingReviewDraft, type ListingWorkspaceItem,
  type ScalarFieldName, type StringCollectionEntry } from "./review-model";
import { advertisementFields, energyUnitOptions, fuelOptions, historyFields, identityFields, inputClassName,
  missingFieldLabels, provenanceLabel, technicalFields, textareaClassName, type ScalarFieldDefinition } from "./presentation";
import { normalizeScalarInput, parseLocalizedNumber, validateReviewDraft } from "./validation";

export function ListingReviewForm({ item, onChange }: {
  item: ListingWorkspaceItem;
  onChange: (draft: ListingReviewDraft, errors?: Record<string, string>) => void;
}) {
  const [reviewed, setReviewed] = useState("");
  const [confirming, setConfirming] = useState(false);
  const [selectedConfirmations, setSelectedConfirmations] = useState<string[]>([]);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const formRef = useRef<HTMLDivElement>(null);
  const busy = ["queued", "analyzing", "retrying"].includes(item.phase);
  const missing = deriveMissingFields(item.draft);
  const fields = item.draft.fields;
  const hasServerValidation = item.persistenceNotice?.tone === "error" && Object.keys(item.validationErrors).length > 0;
  const displayedValidationMessage = validationMessage ?? (hasServerValidation ? "Rätta fälten och försök igen." : null);
  useEffect(() => {
    if (!hasServerValidation) return;
    queueMicrotask(() => errorSummaryRef.current?.focus());
  }, [hasServerValidation]);

  function updateDraft(draft: ListingReviewDraft) {
    setValidationMessage(null);
    onChange(draft, validateReviewDraft(draft));
  }

  function updateScalar(name: ScalarFieldName, input: string) {
    updateDraft(editScalarField(item.draft, name, input, item.normalizedUrl));
  }
  function focusReviewField(path: string) {
    const targets = [...(formRef.current?.querySelectorAll<HTMLElement>("[data-review-path]") ?? [])];
    const target = targets.find(element => element.dataset.reviewPath === path) ??
      targets.find(element => path.startsWith(`${element.dataset.reviewPath}.`) || path.startsWith(`${element.dataset.reviewPath}[`));
    for (let parent = target?.parentElement; parent; parent = parent.parentElement)
      if (parent instanceof HTMLDetailsElement) parent.open = true;
    const control = target?.matches("input,select,textarea") ? target : target?.querySelector<HTMLElement>("input,select,textarea");
    control?.focus(); control?.scrollIntoView?.({ block: "center" });
  }

  function normalizeScalar(name: ScalarFieldName) {
    const current = item.draft.fields[name].input;
    const normalized = normalizeScalarInput(name, current);
    if (normalized !== current) updateDraft({ ...item.draft, fields: { ...item.draft.fields,
      [name]: { ...item.draft.fields[name], input: normalized } } });
  }

  function validateAndFocus() {
    const errors = validateReviewDraft(item.draft);
    onChange(item.draft, errors);
    if (Object.keys(errors).length > 0) {
      setValidationMessage("Rätta fälten nedan innan underlaget används vidare.");
      queueMicrotask(() => errorSummaryRef.current?.focus());
    } else {
      setValidationMessage(item.saved && !item.dirty
        ? "Alla ifyllda uppgifter har giltigt format och annonsen är sparad."
        : "Alla ifyllda uppgifter har giltigt format.");
    }
  }

  return (
          <div ref={formRef} className="space-y-6">
            <Button variant="secondary" disabled={busy} onClick={() => { setSelectedConfirmations([]); setReviewed(JSON.stringify(item.draft)); setConfirming(true); }}>Bekräfta uppgifter</Button>
            {confirming && <EditorDialog open title="Bekräfta uppgifter" onClose={() => setConfirming(false)} actions={
              <Button disabled={!selectedConfirmations.length || reviewed !== JSON.stringify(item.draft)} onClick={() => {
                let draft = item.draft;
                for (const key of selectedConfirmations) {
                  if (key in draft.fields) draft = confirmScalar(draft, key as ScalarFieldName, item.normalizedUrl);
                  else if (key === "fuelTypes") draft = { ...draft, fuelTypes: confirmedCollection(draft.fuelTypes, item.normalizedUrl) };
                  else if (key === "equipment") draft = { ...draft, equipment: confirmedCollection(draft.equipment, item.normalizedUrl) };
                  else if (key === "conditionNotes") draft = { ...draft, conditionNotes: confirmedCollection(draft.conditionNotes, item.normalizedUrl) };
                }
                updateDraft(draft); setConfirming(false);
              }}>Bekräfta valda värden</Button>}>
              <p className="mb-4">Välj bara värden som du själv har kontrollerat. Inget är förvalt. Bekräftelsen sparas med bilen och ger ingen registerverifiering.</p>
              {reviewed !== JSON.stringify(item.draft) && <p role="alert">Underlaget har ändrats. Öppna bekräftelsen igen och granska aktuella värden.</p>}
              {[...allFields.filter(f => item.draft.fields[f.name].input).map(f => ({ key: f.name, label: f.suffix ? `${f.label} (${f.suffix})` : f.label, value: f.options?.find(option => option.value === item.draft.fields[f.name].input)?.label ?? (f.kind === "boolean" ? item.draft.fields[f.name].input === "true" ? "Ja" : "Nej" : item.draft.fields[f.name].input) })),
                ...(["fuelTypes", "equipment", "conditionNotes"] as const).filter(key => item.draft[key].mode !== "unknown").map(key => ({ key,
                  label: key === "fuelTypes" ? "Drivmedel" : key === "equipment" ? "Utrustning" : "Skickuppgifter", value: item.draft[key].mode === "empty" ? "Inga" :
                    item.draft[key].values.map(v => typeof v === "string" ? fuelOptions.find(option => option.value === v)?.label ?? v : v.value).join(", ") }))].map(option => <label key={option.key} className="my-3 flex items-start gap-2">
                  <input type="checkbox" checked={selectedConfirmations.includes(option.key)} onChange={event => setSelectedConfirmations(current => event.target.checked ? [...current, option.key] : current.filter(key => key !== option.key))} />
                  <span>{option.label}: {option.value}</span></label>)}
            </EditorDialog>}
            <p className="rounded-xl border border-cyan-400/20 bg-cyan-400/5 p-4 text-sm leading-6 text-slate-300">
              Ändrade uppgifter är manuella och obekräftade. Bekräfta ett värde först när du har kontrollerat det.
              Sparning innebär ingen bekräftelse eller registerkontroll.
            </p>

            {Object.keys(item.validationErrors).length > 0 && (
              <div ref={errorSummaryRef} tabIndex={-1} role="alert" className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4 outline-none focus:ring-2 focus:ring-rose-300">
                <p className="font-semibold text-rose-200">Några uppgifter behöver rättas</p>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-rose-100/80">
                  {Object.entries(item.validationErrors).map(([path, message]) => <li key={path}><button type="button" className="text-left underline" onClick={() => focusReviewField(path)}>{message}</button></li>)}
                </ul>
              </div>
            )}

            <FieldSection title="Viktiga uppgifter" expanded>
              <ScalarFields definitions={essentialFields} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar}
                onConfirm={name => updateDraft(confirmScalar(item.draft, name, item.normalizedUrl))} />
              <FuelTypesEditor draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
            </FieldSection>

            <FieldSection title="Beskrivning och originalspecifikationer">
              <ListingDetailsEditor value={item.draft.details ?? emptyDetails()} url={item.normalizedUrl} disabled={busy}
                errors={item.validationErrors} onChange={details => updateDraft({...item.draft, details})} />
            </FieldSection>
            <FieldSection title="Övrig identitet">
              <ScalarFields definitions={secondary(identityFields)} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar}
                onConfirm={name => updateDraft(confirmScalar(item.draft, name, item.normalizedUrl))} />
            </FieldSection>

            <FieldSection title="Annons">
              <ScalarFields definitions={secondary(advertisementFields)} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar}
                onConfirm={name => updateDraft(confirmScalar(item.draft, name, item.normalizedUrl))} />
              {fields.odometerKilometres.input && !parseLocalizedNumber(fields.odometerKilometres.input).error && (
                <p className="text-xs text-slate-500">
                  Motsvarar {formatNumeric(n(shiftDecimal(canonicalNumber(fields.odometerKilometres.input),1)),3)} km.
                </p>
              )}
            </FieldSection>

            <FieldSection title="Tekniska uppgifter">
              <ScalarFields definitions={secondary(technicalFields)} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar}
                onConfirm={name => updateDraft(confirmScalar(item.draft, name, item.normalizedUrl))} />
              <EnergyEditor draft={item.draft} normalizedUrl={item.normalizedUrl} disabled={busy} onChange={updateDraft} errors={item.validationErrors} />
            </FieldSection>

            <FieldSection title="Historik och besiktning">
              <ScalarFields definitions={secondary(historyFields)} item={item} disabled={busy} onInput={updateScalar} onBlur={normalizeScalar}
                onConfirm={name => updateDraft(confirmScalar(item.draft, name, item.normalizedUrl))} />
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
              {item.context.sources.length
                ? <ul className="space-y-2">
                    {item.context.sources.map((source) => (
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
              {displayedValidationMessage && <p role="status" className="text-sm text-slate-300">{displayedValidationMessage}</p>}
            </div>
          </div>
  );
}

function ScalarFields({ definitions, item, disabled, onInput, onBlur }: {
  definitions: ScalarFieldDefinition[];
  item: ListingWorkspaceItem;
  disabled: boolean;
  onInput: (name: ScalarFieldName, value: string) => void;
  onBlur: (name: ScalarFieldName) => void;
  onConfirm: (name: ScalarFieldName) => void;
}) {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {definitions.map((definition) => {
        const field = item.draft.fields[definition.name];
        const error = item.validationErrors[definition.name];
        const id = `${item.id}-${definition.name}`;
        const readOnly = definition.name === "registrationNumber" && item.saved !== null;
        return (
          <div key={definition.name} data-review-path={definition.name} className="block text-sm font-medium text-slate-300">
            <label htmlFor={id}>{definition.label}{definition.suffix ? ` (${definition.suffix})` : ""}</label>
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
                readOnly={readOnly}
                aria-invalid={Boolean(error)}
                aria-describedby={error ? `${id}-error` : `${id}-source`}
                className={inputClassName}
                onChange={(event) => onInput(definition.name, event.target.value)}
                onBlur={() => onBlur(definition.name)}
              />
            )}
            {readOnly && <span className="mt-1 block text-xs text-cyan-300">Registreringsnumret kan inte ändras för en sparad bil.</span>}
            {!field.input && <span className="mt-1 flex items-center gap-1 text-xs text-rose-300"><AlertTriangle aria-hidden="true" size={13} />
              {definition.name === "registrationNumber" ? "Saknas för att lägga till i jämförelsen" : "Saknas – kan kompletteras senare"}</span>}
            {error
              ? <span id={`${id}-error`} className="mt-1 block text-xs text-rose-300">{error}</span>
              : <details className="mt-1 text-xs text-slate-400"><summary>Källa och verifiering</summary><span id={`${id}-source`} className="break-all">
                  {provenanceLabel(field.provenance)}{field.provenance ? ` · ${field.provenance.sourceUrl}` : ""}</span></details>}

          </div>
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
    <CollectionFrame path="fuelTypes" label="Bränsletyper" mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors.fuelTypes}
      onConfirm={() => onChange({ ...draft, fuelTypes: confirmedCollection(collection, normalizedUrl) })}>
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
    <CollectionFrame path="energyConsumptions" label="Energiförbrukning" mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors.energyConsumptions}
      onConfirm={() => onChange({ ...draft, energyConsumptions: confirmedCollection(collection, normalizedUrl) })}>
      <div className="space-y-3">
        {collection.values.map((entry, index) => {
          const base = `energyConsumptions.values[${index}]`;
          return (
            <div key={entry.id} className="grid gap-3 rounded-xl border border-slate-800 p-3 md:grid-cols-[1fr_0.8fr_1fr_auto]">
              <SmallInput
                path={`${base}.label`} id={`${entry.id}-label`}
                label="Etikett"
                value={entry.label}
                disabled={disabled}
                error={errors[`${base}.label`]}
                onChange={(value) => patch(entry.id, { label: value })}
                onBlur={() => patch(entry.id, { label: normalizeCollectionText(entry.label) })}
              />
              <label className="text-xs text-slate-400">Enhet
                <select data-review-path={`${base}.unit`} aria-label={`Enhet ${index + 1}`} className={inputClassName} value={entry.unit} disabled={disabled} onChange={(event) => patch(entry.id, { unit: event.target.value as EnergyConsumptionDraft["unit"] })}>
                  {energyUnitOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </select>
              </label>
              <SmallInput
                path={`${base}.consumptionPer100Kilometres`} id={`${entry.id}-consumption`}
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
    <CollectionFrame path={name} label={label} mode={collection.mode} provenance={collection.provenance} disabled={disabled} onMode={setMode} error={errors[name]}
      onConfirm={() => onChange({ ...draft, [name]: confirmedCollection(collection, normalizedUrl) })}>
      <div className="space-y-2">
        {collection.values.map((entry, index) => (
          <div key={entry.id} className="flex items-start gap-2">
            {multiline
              ? <textarea data-review-path={`${name}.values[${index}]`} aria-label={`${label} ${index + 1}`} className={textareaClassName} value={entry.value} disabled={disabled} aria-invalid={Boolean(errors[`${name}.values[${index}]`])} aria-describedby={errors[`${name}.values[${index}]`] ? `${entry.id}-error` : undefined} onChange={(event) => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: event.target.value } : value))} onBlur={() => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: normalizeCollectionText(value.value) } : value))} />
              : <input data-review-path={`${name}.values[${index}]`} aria-label={`${label} ${index + 1}`} className={inputClassName} value={entry.value} disabled={disabled} aria-invalid={Boolean(errors[`${name}.values[${index}]`])} aria-describedby={errors[`${name}.values[${index}]`] ? `${entry.id}-error` : undefined} onChange={(event) => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: event.target.value } : value))} onBlur={() => commit("values", collection.values.map((value) => value.id === entry.id ? { ...value, value: normalizeCollectionText(value.value) } : value))} />}
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

function confirmedCollection<T>(collection: CollectionDraft<T>, url: string): CollectionDraft<T> {
  return { ...collection, provenance: { ...manualProvenance(collection.provenance?.sourceUrl ?? url), verification: "userConfirmed" } };
}

function CollectionFrame({ path, label, mode, provenance, disabled, onMode, onConfirm, error, children }: {
  path: string;
  label: string;
  mode: CollectionDraft<unknown>["mode"];
  provenance: CollectionDraft<unknown>["provenance"];
  disabled: boolean;
  onMode: (mode: CollectionDraft<unknown>["mode"]) => void;
  onConfirm: () => void;
  error?: string;
  children: ReactNode;
}) {
  const errorId = useId();
  return (
    <div data-review-path={path} className="rounded-xl border border-slate-800 bg-slate-950/30 p-4">
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
      {mode !== "unknown" && provenance?.verification !== "userConfirmed" && <Button type="button" variant="ghost" disabled={disabled || !!error}
        onClick={onConfirm}>Bekräfta {label.toLocaleLowerCase("sv-SE")}</Button>}
    </div>
  );
}

function FieldSection({ title, children, expanded = false }: { title: string; children: ReactNode; expanded?: boolean }) {
  return (
    <details open={expanded || undefined} className="space-y-4 rounded-2xl border border-slate-800 bg-slate-900/40 p-4 sm:p-5">
      <summary className="cursor-pointer text-base font-semibold text-white">{title}</summary>
      {children}
    </details>
  );
}

const essentialNames: ScalarFieldName[] = ["registrationNumber", "make", "model", "modelYear", "priceSek", "odometerKilometres", "transmission", "ownerCount", "towBar"];
const allFields = [...identityFields, ...advertisementFields, ...technicalFields, ...historyFields];
const essentialFields = essentialNames.map(name => allFields.find(field => field.name === name)!);
const secondary = (definitions: ScalarFieldDefinition[]) => definitions.filter(field => !essentialNames.includes(field.name));
function confirmScalar(draft: ListingReviewDraft, name: ScalarFieldName, url: string): ListingReviewDraft {
  const field = draft.fields[name];
  return { ...draft, fields: { ...draft.fields, [name]: { ...field,
    provenance: { ...manualProvenance(field.provenance?.sourceUrl ?? url), verification: "userConfirmed" } } } };
}

function SmallInput({ path, id, label, value, disabled, error, inputMode, onChange, onBlur }: {
  path: string;
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
      <input data-review-path={path} id={id} aria-label={label} className={inputClassName} value={value} disabled={disabled} inputMode={inputMode} aria-invalid={Boolean(error)} aria-describedby={error ? `${id}-error` : undefined} onChange={(event) => onChange(event.target.value)} onBlur={onBlur} />
      {error && <span id={`${id}-error`} className="mt-1 block text-xs text-rose-300">{error}</span>}
    </label>
  );
}


function normalizeCollectionText(value: string) { return value.trim().normalize("NFC"); }
