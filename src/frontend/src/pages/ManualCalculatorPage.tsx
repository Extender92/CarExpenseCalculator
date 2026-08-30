import {
  Banknote,
  Calculator,
  CarFront,
  Fuel,
  LoaderCircle,
  Plus,
  ReceiptText,
  Trash2,
} from "lucide-react";
import { forwardRef, useRef, useState, type FormEvent, type ReactNode } from "react";
import {
  calculateManualScenario,
  ManualCalculationApiError,
  type ManualCalculationResult,
} from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  createEnergySource,
  createInitialManualCalculationForm,
  createNamedRecurringCost,
  createOneTimeCost,
  type ManualCalculationForm,
  type OptionalRecurringCostDraft,
} from "@/features/manual-calculator/form-model";
import {
  ResultDetails,
  ResultSummary,
} from "@/features/manual-calculator/ManualCalculationResultView";
import {
  fieldDomId,
  fieldErrorId,
  fieldLabel,
  formatQuantity,
  validationProblemToErrors,
} from "@/features/manual-calculator/presentation";
import {
  convertMilToKilometres,
  validateManualCalculationForm,
  type ValidationErrors,
} from "@/features/manual-calculator/validation";
import { cn } from "@/lib/utils";

type RequestState = "idle" | "submitting" | "success" | "error";

export function ManualCalculatorPage() {
  const [form, setForm] = useState(createInitialManualCalculationForm);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [hasValidated, setHasValidated] = useState(false);
  const [requestState, setRequestState] = useState<RequestState>("idle");
  const [requestError, setRequestError] = useState<string | null>(null);
  const [result, setResult] = useState<ManualCalculationResult | null>(null);
  const [resultIsStale, setResultIsStale] = useState(false);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const resultSummaryRef = useRef<HTMLDivElement>(null);

  function updateForm(updater: (current: ManualCalculationForm) => ManualCalculationForm) {
    const next = updater(form);
    setForm(next);
    setRequestError(null);
    if (hasValidated) {
      setErrors(validateManualCalculationForm(next).errors);
    }
    if (result) {
      setResultIsStale(true);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setHasValidated(true);
    setRequestError(null);

    const validation = validateManualCalculationForm(form);
    setErrors(validation.errors);
    if (!validation.request) {
      setRequestState("idle");
      focusAfterRender(() => errorSummaryRef.current);
      return;
    }

    setRequestState("submitting");
    try {
      const nextResult = await calculateManualScenario(validation.request);
      setResult(nextResult);
      setResultIsStale(false);
      setErrors({});
      setRequestState("success");
      focusAfterRender(() => resultSummaryRef.current);
    } catch (error) {
      setRequestState("error");
      if (error instanceof ManualCalculationApiError && error.problem) {
        const serverErrors = validationProblemToErrors(error.problem);
        setErrors(serverErrors);
        setRequestError(
          Object.keys(serverErrors).length > 0
            ? "Servern hittade värden som behöver rättas."
            : error.message,
        );
        focusAfterRender(() => errorSummaryRef.current);
      } else {
        setRequestError("Kalkylen kunde inte beräknas just nu. Kontrollera anslutningen och försök igen.");
        focusAfterRender(() => errorSummaryRef.current);
      }
    }
  }

  function updateEnergySource(
    id: string,
    patch: Partial<ManualCalculationForm["energySources"][number]>,
  ) {
    updateForm((current) => ({
      ...current,
      energySources: current.energySources.map((source) =>
        source.id === id ? { ...source, ...patch } : source,
      ),
    }));
  }

  function updateRecurringCost(
    id: string,
    patch: Partial<ManualCalculationForm["otherRecurringCosts"][number]>,
  ) {
    updateForm((current) => ({
      ...current,
      otherRecurringCosts: current.otherRecurringCosts.map((cost) =>
        cost.id === id ? { ...cost, ...patch } : cost,
      ),
    }));
  }

  function updateOneTimeCost(
    id: string,
    patch: Partial<ManualCalculationForm["otherOneTimeCosts"][number]>,
  ) {
    updateForm((current) => ({
      ...current,
      otherOneTimeCosts: current.otherOneTimeCosts.map((cost) =>
        cost.id === id ? { ...cost, ...patch } : cost,
      ),
    }));
  }

  const totalDistanceKilometres = convertMilToKilometres(form.annualDistanceMil);

  return (
    <div className="space-y-10">
      <header className="border-b border-slate-800 pb-8">
        <Badge variant="success">Tillgänglig</Badge>
        <h1 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Manuell kalkyl</h1>
        <p className="mt-4 max-w-3xl text-base leading-7 text-slate-400">
          Räkna på bilens kassaflöde och ägandekostnad med dina egna antaganden. Inget sparas och
          beräkningen fungerar utan externa datakällor eller AI.
        </p>
      </header>

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1.6fr)_minmax(20rem,0.8fr)]">
        <form onSubmit={handleSubmit} noValidate aria-busy={requestState === "submitting"} className="space-y-6">
          <ErrorSummary ref={errorSummaryRef} errors={errors} requestError={requestError} />

          <fieldset disabled={requestState === "submitting"} className="contents">
            <FormSection
              icon={CarFront}
              title="Scenario"
              description="Grunduppgifter för bilen och perioden du vill räkna på."
            >
              <div className="grid gap-5 sm:grid-cols-2">
                <TextField
                  label="Bilens namn"
                  path="vehicleLabel"
                  value={form.vehicleLabel}
                  onChange={(value) => updateForm((current) => ({ ...current, vehicleLabel: value }))}
                  errors={errors}
                  help="Valfritt, till exempel modell eller registreringsnummer."
                  maxLength={121}
                />
                <TextField
                  label="Beräkningsperiod"
                  path="calculationPeriodMonths"
                  value={form.calculationPeriodMonths}
                  onChange={(value) => updateForm((current) => ({ ...current, calculationPeriodMonths: value }))}
                  errors={errors}
                  inputMode="numeric"
                  suffix="månader"
                  required
                />
                <TextField
                  label="Inköpspris"
                  path="purchasePriceSek"
                  value={form.purchasePriceSek}
                  onChange={(value) => updateForm((current) => ({ ...current, purchasePriceSek: value }))}
                  errors={errors}
                  inputMode="decimal"
                  suffix="kr"
                  required
                />
                <TextField
                  label="Årlig körsträcka"
                  path="annualDistanceKilometres"
                  value={form.annualDistanceMil}
                  onChange={(value) => updateForm((current) => ({ ...current, annualDistanceMil: value }))}
                  errors={errors}
                  inputMode="decimal"
                  suffix="mil/år"
                  help={
                    totalDistanceKilometres === null
                      ? "1 svensk mil motsvarar exakt 10 kilometer."
                      : `Motsvarar ${formatQuantity(totalDistanceKilometres)} km per år.`
                  }
                  required
                />
              </div>

              <ChoiceGroup
                legend="Förväntat restvärde"
                path="residualValueKnown"
                value={form.residualValueKnown ? "known" : "unknown"}
                options={[
                  { value: "unknown", label: "Okänt" },
                  { value: "known", label: "Känt" },
                ]}
                onChange={(value) => updateForm((current) => ({
                  ...current,
                  residualValueKnown: value === "known",
                }))}
                help="Utan restvärde kan kassaflödet fortfarande beräknas, men inte nettoägandekostnaden."
              />
              {form.residualValueKnown && (
                <div className="max-w-sm">
                  <TextField
                    label="Förväntat restvärde"
                    path="expectedResidualValueSek"
                    value={form.expectedResidualValueSek}
                    onChange={(value) => updateForm((current) => ({
                      ...current,
                      expectedResidualValueSek: value,
                    }))}
                    errors={errors}
                    inputMode="decimal"
                    suffix="kr"
                    required
                  />
                </div>
              )}
            </FormSection>

            <FormSection
              icon={Banknote}
              title="Köp och finansiering"
              description="Välj kontantköp eller fyll i villkoren för ett annuitetslån."
              path="financing"
              errors={errors}
            >
              <ChoiceGroup
                legend="Betalsätt"
                path="financingMethod"
                value={form.financing.enabled ? "financing" : "cash"}
                options={[
                  { value: "cash", label: "Kontantköp" },
                  { value: "financing", label: "Finansiering" },
                ]}
                onChange={(value) => updateForm((current) => ({
                  ...current,
                  financing: { ...current.financing, enabled: value === "financing" },
                }))}
              />

              {form.financing.enabled && (
                <div className="grid gap-5 sm:grid-cols-3">
                  <TextField
                    label="Kontantinsats"
                    path="financing.downPaymentSek"
                    value={form.financing.downPaymentSek}
                    onChange={(value) => updateForm((current) => ({
                      ...current,
                      financing: { ...current.financing, downPaymentSek: value },
                    }))}
                    errors={errors}
                    inputMode="decimal"
                    suffix="kr"
                    required
                  />
                  <TextField
                    label="Nominell årsränta"
                    path="financing.annualNominalInterestRatePercent"
                    value={form.financing.annualNominalInterestRatePercent}
                    onChange={(value) => updateForm((current) => ({
                      ...current,
                      financing: {
                        ...current.financing,
                        annualNominalInterestRatePercent: value,
                      },
                    }))}
                    errors={errors}
                    inputMode="decimal"
                    suffix="%"
                    required
                  />
                  <TextField
                    label="Lånets löptid"
                    path="financing.termMonths"
                    value={form.financing.termMonths}
                    onChange={(value) => updateForm((current) => ({
                      ...current,
                      financing: { ...current.financing, termMonths: value },
                    }))}
                    errors={errors}
                    inputMode="numeric"
                    suffix="månader"
                    required
                  />
                </div>
              )}
            </FormSection>

            <FormSection
              icon={Fuel}
              title="Energi"
              description="Ange bränsle, el eller annan energi. Vid körning måste andelarna tillsammans vara 100 %."
              path="energySources"
              errors={errors}
              action={
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  disabled={form.energySources.length >= 2}
                  onClick={() => updateForm((current) => ({
                    ...current,
                    energySources: [
                      ...current.energySources,
                      createEnergySource(current.energySources.length === 0 ? "100" : ""),
                    ],
                  }))}
                >
                  <Plus size={16} /> Lägg till energikälla
                </Button>
              }
            >
              {form.energySources.length === 0 ? (
                <EmptyCollection>
                  Ingen energikälla har lagts till. Det är giltigt när körsträckan är 0 mil.
                </EmptyCollection>
              ) : (
                <div className="space-y-4">
                  {form.energySources.map((source, index) => {
                    const path = `energySources[${index}]`;
                    return (
                      <div key={source.id} className="rounded-xl border border-slate-800 bg-slate-950/35 p-4 sm:p-5">
                        <div className="mb-5 flex items-center justify-between gap-3">
                          <h3 className="font-semibold text-slate-200">Energikälla {index + 1}</h3>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            aria-label={`Ta bort energikälla ${index + 1}`}
                            onClick={() => updateForm((current) => ({
                              ...current,
                              energySources: current.energySources.filter((item) => item.id !== source.id),
                            }))}
                          >
                            <Trash2 size={16} /> Ta bort
                          </Button>
                        </div>
                        <div className="grid gap-5 sm:grid-cols-2">
                          <TextField
                            label="Namn"
                            path={`${path}.label`}
                            value={source.label}
                            onChange={(value) => updateEnergySource(source.id, { label: value })}
                            errors={errors}
                            placeholder="Till exempel bensin eller el"
                            required
                          />
                          <SelectField
                            label="Enhet"
                            path={`${path}.unit`}
                            value={source.unit}
                            onChange={(value) => updateEnergySource(source.id, { unit: value as typeof source.unit })}
                            errors={errors}
                            options={energyUnitOptions}
                            required
                          />
                          <TextField
                            label="Förbrukning per 100 km"
                            path={`${path}.consumptionPer100Kilometres`}
                            value={source.consumptionPer100Kilometres}
                            onChange={(value) => updateEnergySource(source.id, { consumptionPer100Kilometres: value })}
                            errors={errors}
                            inputMode="decimal"
                            required
                          />
                          <TextField
                            label="Pris per enhet"
                            path={`${path}.pricePerUnitSek`}
                            value={source.pricePerUnitSek}
                            onChange={(value) => updateEnergySource(source.id, { pricePerUnitSek: value })}
                            errors={errors}
                            inputMode="decimal"
                            suffix="kr"
                            required
                          />
                          <TextField
                            label="Andel av körsträckan"
                            path={`${path}.distanceSharePercent`}
                            value={source.distanceSharePercent}
                            onChange={(value) => updateEnergySource(source.id, { distanceSharePercent: value })}
                            errors={errors}
                            inputMode="decimal"
                            suffix="%"
                            required
                          />
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </FormSection>

            <FormSection
              icon={ReceiptText}
              title="Standardkostnader"
              description="Markera varje kostnad som känd eller okänd. Noll kronor betyder att kostnaden är känd och 0 kr."
            >
              <div className="grid gap-4 lg:grid-cols-3">
                <OptionalCostFields
                  title="Fordonsskatt"
                  path="vehicleTax"
                  value={form.vehicleTax}
                  errors={errors}
                  onChange={(value) => updateForm((current) => ({ ...current, vehicleTax: value }))}
                />
                <OptionalCostFields
                  title="Försäkring"
                  path="insurance"
                  value={form.insurance}
                  errors={errors}
                  onChange={(value) => updateForm((current) => ({ ...current, insurance: value }))}
                />
                <OptionalCostFields
                  title="Underhåll och reparationer"
                  path="maintenanceAndRepairs"
                  value={form.maintenanceAndRepairs}
                  errors={errors}
                  onChange={(value) => updateForm((current) => ({ ...current, maintenanceAndRepairs: value }))}
                />
              </div>
            </FormSection>

            <FormSection
              icon={Plus}
              title="Övriga kostnader"
              description="Lägg till egna återkommande kostnader eller engångskostnader."
              path="otherRecurringCosts"
              errors={errors}
            >
              <CustomCostSection
                title="Återkommande kostnader"
                addLabel="Lägg till återkommande"
                count={form.otherRecurringCosts.length}
                onAdd={() => updateForm((current) => ({
                  ...current,
                  otherRecurringCosts: [...current.otherRecurringCosts, createNamedRecurringCost()],
                }))}
              >
                {form.otherRecurringCosts.map((cost, index) => {
                  const path = `otherRecurringCosts[${index}]`;
                  return (
                    <div key={cost.id} className="grid gap-4 rounded-xl border border-slate-800 bg-slate-950/35 p-4 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
                      <TextField label="Namn" path={`${path}.label`} value={cost.label} onChange={(value) => updateRecurringCost(cost.id, { label: value })} errors={errors} required />
                      <TextField label="Belopp" path={`${path}.amountSek`} value={cost.amountSek} onChange={(value) => updateRecurringCost(cost.id, { amountSek: value })} errors={errors} inputMode="decimal" suffix="kr" required />
                      <SelectField label="Intervall" path={`${path}.cadence`} value={cost.cadence} onChange={(value) => updateRecurringCost(cost.id, { cadence: value as typeof cost.cadence })} errors={errors} options={cadenceOptions} required />
                      <Button type="button" variant="ghost" size="sm" aria-label={`Ta bort återkommande kostnad ${index + 1}`} onClick={() => updateForm((current) => ({ ...current, otherRecurringCosts: current.otherRecurringCosts.filter((item) => item.id !== cost.id) }))}><Trash2 size={16} /> Ta bort</Button>
                    </div>
                  );
                })}
              </CustomCostSection>

              <CustomCostSection
                title="Engångskostnader"
                addLabel="Lägg till engångskostnad"
                count={form.otherOneTimeCosts.length}
                onAdd={() => updateForm((current) => ({
                  ...current,
                  otherOneTimeCosts: [...current.otherOneTimeCosts, createOneTimeCost()],
                }))}
              >
                {form.otherOneTimeCosts.map((cost, index) => {
                  const path = `otherOneTimeCosts[${index}]`;
                  return (
                    <div key={cost.id} className="grid gap-4 rounded-xl border border-slate-800 bg-slate-950/35 p-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
                      <TextField label="Namn" path={`${path}.label`} value={cost.label} onChange={(value) => updateOneTimeCost(cost.id, { label: value })} errors={errors} required />
                      <TextField label="Belopp" path={`${path}.amountSek`} value={cost.amountSek} onChange={(value) => updateOneTimeCost(cost.id, { amountSek: value })} errors={errors} inputMode="decimal" suffix="kr" required />
                      <Button type="button" variant="ghost" size="sm" aria-label={`Ta bort engångskostnad ${index + 1}`} onClick={() => updateForm((current) => ({ ...current, otherOneTimeCosts: current.otherOneTimeCosts.filter((item) => item.id !== cost.id) }))}><Trash2 size={16} /> Ta bort</Button>
                    </div>
                  );
                })}
              </CustomCostSection>
            </FormSection>

            <Card className="border-cyan-400/20 bg-cyan-400/5">
              <CardContent className="flex flex-col items-start justify-between gap-4 pt-6 sm:flex-row sm:items-center">
                <div>
                  <p className="font-semibold text-slate-100">Redo att räkna?</p>
                  <p className="mt-1 text-sm text-slate-400">Beräkningen sparas inte och kan köras igen med nya värden.</p>
                </div>
                <Button type="submit" size="lg" disabled={requestState === "submitting"}>
                  {requestState === "submitting" ? <LoaderCircle className="animate-spin" size={18} /> : <Calculator size={18} />}
                  {requestState === "submitting" ? "Beräknar…" : result ? "Beräkna igen" : "Beräkna kostnad"}
                </Button>
              </CardContent>
            </Card>
          </fieldset>

          <p className="sr-only" role="status" aria-live="polite">
            {requestState === "submitting"
              ? "Beräkningen pågår."
              : requestState === "success"
                ? "Beräkningen är klar."
                : ""}
          </p>
        </form>

        <aside className="xl:sticky xl:top-8">
          {result ? (
            <ResultSummary result={result} isStale={resultIsStale} summaryRef={resultSummaryRef} />
          ) : (
            <Card>
              <CardHeader>
                <Badge variant="muted">Resultat</Badge>
                <CardTitle className="pt-2">Dina kostnader visas här</CardTitle>
                <CardDescription>
                  Fyll i scenariot och välj Beräkna kostnad. Kalkylen visar kassaflöde även när vissa standardkostnader är okända.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3 text-sm leading-6 text-slate-400">
                  <li>• Kassaflöde under vald period</li>
                  <li>• Månadssnitt och årssnitt</li>
                  <li>• Energi- och finansieringsdetaljer</li>
                  <li>• Nettoägandekostnad när restvärde finns</li>
                </ul>
              </CardContent>
            </Card>
          )}
        </aside>
      </div>

      {result && <ResultDetails result={result} isStale={resultIsStale} />}
    </div>
  );
}

function FormSection({ icon: Icon, title, description, children, action, path, errors }: { icon: typeof CarFront; title: string; description: string; children: ReactNode; action?: ReactNode; path?: string; errors?: ValidationErrors }) {
  const sectionErrors = path ? errors?.[path] : undefined;
  return (
    <Card id={path ? fieldDomId(path) : undefined}>
      <CardHeader>
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
          <div className="flex items-start gap-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-400/10 text-cyan-300"><Icon size={20} /></span>
            <div><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></div>
          </div>
          {action}
        </div>
        {sectionErrors && <FieldErrors path={path!} errors={sectionErrors} />}
      </CardHeader>
      <CardContent className="space-y-5">{children}</CardContent>
    </Card>
  );
}

function TextField({ label, path, value, onChange, errors, help, suffix, inputMode, placeholder, required, maxLength }: { label: string; path: string; value: string; onChange: (value: string) => void; errors: ValidationErrors; help?: string; suffix?: string; inputMode?: "decimal" | "numeric"; placeholder?: string; required?: boolean; maxLength?: number }) {
  const fieldErrors = errors[path];
  const describedBy = [help ? `${fieldDomId(path)}-help` : null, fieldErrors ? fieldErrorId(path) : null]
    .filter(Boolean)
    .join(" ") || undefined;
  return (
    <div>
      <label htmlFor={fieldDomId(path)} className="mb-2 block text-sm font-medium text-slate-200">
        {label}{required && <span className="ml-1 text-cyan-400" aria-hidden="true">*</span>}
      </label>
      <div className="relative">
        <input id={fieldDomId(path)} name={path} type="text" inputMode={inputMode} value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} maxLength={maxLength} aria-invalid={fieldErrors ? true : undefined} aria-describedby={describedBy} aria-required={required} className={cn(inputClass, suffix && "pr-20", fieldErrors && errorInputClass)} />
        {suffix && <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs font-medium text-slate-500">{suffix}</span>}
      </div>
      {help && <p id={`${fieldDomId(path)}-help`} className="mt-2 text-xs leading-5 text-slate-500">{help}</p>}
      {fieldErrors && <FieldErrors path={path} errors={fieldErrors} />}
    </div>
  );
}

function SelectField({ label, path, value, onChange, errors, options, required }: { label: string; path: string; value: string; onChange: (value: string) => void; errors: ValidationErrors; options: ReadonlyArray<{ value: string; label: string }>; required?: boolean }) {
  const fieldErrors = errors[path];
  return (
    <div>
      <label htmlFor={fieldDomId(path)} className="mb-2 block text-sm font-medium text-slate-200">
        {label}{required && <span className="ml-1 text-cyan-400" aria-hidden="true">*</span>}
      </label>
      <select id={fieldDomId(path)} name={path} value={value} onChange={(event) => onChange(event.target.value)} aria-invalid={fieldErrors ? true : undefined} aria-describedby={fieldErrors ? fieldErrorId(path) : undefined} aria-required={required} className={cn(inputClass, fieldErrors && errorInputClass)}>
        <option value="">Välj…</option>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
      {fieldErrors && <FieldErrors path={path} errors={fieldErrors} />}
    </div>
  );
}

function ChoiceGroup({ legend, path, value, options, onChange, help }: { legend: string; path: string; value: string; options: ReadonlyArray<{ value: string; label: string }>; onChange: (value: string) => void; help?: string }) {
  return (
    <fieldset id={fieldDomId(path)}>
      <legend className="mb-2 text-sm font-medium text-slate-200">{legend}</legend>
      <div className="flex flex-wrap gap-3">
        {options.map((option) => (
          <label key={option.value} className={cn("flex cursor-pointer items-center gap-2 rounded-xl border px-4 py-2.5 text-sm transition", value === option.value ? "border-cyan-400/40 bg-cyan-400/10 text-cyan-200" : "border-slate-700 bg-slate-950/30 text-slate-300 hover:border-slate-600")}>
            <input type="radio" name={path} value={option.value} checked={value === option.value} onChange={() => onChange(option.value)} className="accent-cyan-400" />
            {option.label}
          </label>
        ))}
      </div>
      {help && <p className="mt-2 text-xs leading-5 text-slate-500">{help}</p>}
    </fieldset>
  );
}

function OptionalCostFields({ title, path, value, errors, onChange }: { title: string; path: string; value: OptionalRecurringCostDraft; errors: ValidationErrors; onChange: (value: OptionalRecurringCostDraft) => void }) {
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/35 p-4">
      <ChoiceGroup legend={title} path={`${path}.known`} value={value.isKnown ? "known" : "unknown"} options={[{ value: "unknown", label: "Okänd" }, { value: "known", label: "Känd" }]} onChange={(choice) => onChange({ ...value, isKnown: choice === "known" })} />
      {value.isKnown && (
        <div className="mt-4 space-y-4">
          <TextField label="Belopp" path={`${path}.amountSek`} value={value.amountSek} onChange={(amountSek) => onChange({ ...value, amountSek })} errors={errors} inputMode="decimal" suffix="kr" required />
          <SelectField label="Intervall" path={`${path}.cadence`} value={value.cadence} onChange={(cadence) => onChange({ ...value, cadence: cadence as OptionalRecurringCostDraft["cadence"] })} errors={errors} options={cadenceOptions} required />
        </div>
      )}
    </div>
  );
}

function CustomCostSection({ title, addLabel, count, onAdd, children }: { title: string; addLabel: string; count: number; onAdd: () => void; children: ReactNode }) {
  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><h3 className="font-semibold text-slate-200">{title}</h3><p className="mt-1 text-xs text-slate-500">{count} av 50 tillagda</p></div>
        <Button type="button" variant="secondary" size="sm" disabled={count >= 50} onClick={onAdd}><Plus size={16} /> {addLabel}</Button>
      </div>
      {count === 0 ? <EmptyCollection>Inga kostnader har lagts till.</EmptyCollection> : children}
    </section>
  );
}

function EmptyCollection({ children }: { children: ReactNode }) {
  return <p className="rounded-xl border border-dashed border-slate-800 bg-slate-950/20 px-4 py-5 text-sm text-slate-500">{children}</p>;
}

function FieldErrors({ path, errors }: { path: string; errors: string[] }) {
  return <div id={fieldErrorId(path)} className="mt-2 space-y-1" role="alert">{errors.map((error, index) => <p key={`${error}-${index}`} className="text-xs leading-5 text-rose-300">{error}</p>)}</div>;
}

const ErrorSummary = forwardRef<HTMLDivElement, { errors: ValidationErrors; requestError: string | null }>(function ErrorSummary({ errors, requestError }, ref) {
  const entries = Object.entries(errors);
  if (!requestError && entries.length === 0) return null;
  return (
    <div ref={ref} tabIndex={-1} role="alert" aria-labelledby="manual-error-title" className="scroll-mt-24 rounded-2xl border border-rose-400/30 bg-rose-400/10 p-5 outline-none focus-visible:ring-2 focus-visible:ring-rose-300">
      <h2 id="manual-error-title" className="font-semibold text-rose-200">Kontrollera formuläret</h2>
      {requestError && <p className="mt-2 text-sm leading-6 text-rose-100">{requestError}</p>}
      {entries.length > 0 && <ul className="mt-3 space-y-2 text-sm text-rose-100">{entries.flatMap(([path, messages]) => messages.map((message, index) => <li key={`${path}-${index}`}>{path === "form" ? <span>{message}</span> : <a className="underline decoration-rose-300/50 underline-offset-4 hover:text-white" href={`#${fieldDomId(path)}`}><span className="font-semibold">{fieldLabel(path)}:</span> {message}</a>}</li>))}</ul>}
    </div>
  );
});

const cadenceOptions = [
  { value: "monthly", label: "Månadsvis" },
  { value: "annual", label: "Årlig" },
] as const;
const energyUnitOptions = [
  { value: "litre", label: "Liter" },
  { value: "kilowattHour", label: "kWh" },
  { value: "kilogram", label: "Kilogram" },
] as const;
const inputClass = "h-11 w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 text-sm text-slate-100 outline-none transition placeholder:text-slate-600 focus:border-cyan-400/70 focus:ring-2 focus:ring-cyan-400/20 disabled:cursor-not-allowed disabled:opacity-60";
const errorInputClass = "border-rose-400/60 focus:border-rose-300 focus:ring-rose-400/20";

function focusAfterRender(getElement: () => HTMLElement | null) {
  window.setTimeout(() => getElement()?.focus(), 0);
}
