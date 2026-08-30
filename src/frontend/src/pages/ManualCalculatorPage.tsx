import {
  Banknote,
  Calculator,
  CarFront,
  Fuel,
  LoaderCircle,
  Plus,
  ReceiptText,
  RotateCcw,
  Save,
  Trash2,
} from "lucide-react";
import {
  forwardRef,
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import {
  calculateManualScenario,
  createSavedCostScenario,
  deleteSavedCostScenario,
  getSavedCostScenario,
  getSavedCostScenarioByRegistration,
  listSavedCostScenarios,
  ManualCalculationApiError,
  replaceSavedCostScenario,
  SavedCostScenarioApiError,
  type ManualCalculationRequest,
  type ManualCalculationResult,
  type SavedCostScenarioResponse,
  type SavedCostScenarioSummary,
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
  SavedScenariosPanel,
  type SavedListState,
} from "@/features/manual-calculator/SavedScenariosPanel";
import {
  fieldDomId,
  fieldErrorId,
  fieldLabel,
  formatQuantity,
  savedValidationProblemToErrors,
  validationProblemToErrors,
} from "@/features/manual-calculator/presentation";
import {
  normalizeRegistrationNumber,
  savedScenarioMetadata,
  savedScenarioToForm,
  validateRegistrationNumber,
  type OpenedSavedScenario,
} from "@/features/manual-calculator/saved-scenarios";
import {
  convertMilToKilometres,
  validateManualCalculationForm,
  type ValidationErrors,
} from "@/features/manual-calculator/validation";
import { cn } from "@/lib/utils";

type RequestState = "idle" | "submitting" | "success" | "error";
type PersistenceOperation = "idle" | "opening" | "saving" | "deleting";
type NoticeTone = "success" | "warning" | "error";

type PendingConfirmation =
  | { kind: "new" }
  | { kind: "open"; scenario: SavedCostScenarioSummary }
  | { kind: "delete"; scenario: SavedCostScenarioSummary }
  | {
      kind: "duplicate";
      existing: SavedCostScenarioResponse;
      request: ManualCalculationRequest;
    }
  | { kind: "reload"; vehicleId: string };

export function ManualCalculatorPage() {
  const [form, setForm] = useState(createInitialManualCalculationForm);
  const [registrationNumber, setRegistrationNumber] = useState("");
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [hasValidated, setHasValidated] = useState(false);
  const [hasValidatedForSave, setHasValidatedForSave] = useState(false);
  const [requestState, setRequestState] = useState<RequestState>("idle");
  const [requestError, setRequestError] = useState<string | null>(null);
  const [result, setResult] = useState<ManualCalculationResult | null>(null);
  const [resultIsStale, setResultIsStale] = useState(false);
  const [resultIsPersisted, setResultIsPersisted] = useState(false);
  const [savedScenarios, setSavedScenarios] = useState<SavedCostScenarioSummary[]>([]);
  const [savedListState, setSavedListState] = useState<SavedListState>("loading");
  const [savedListError, setSavedListError] = useState<string | null>(null);
  const [currentSaved, setCurrentSaved] = useState<OpenedSavedScenario | null>(null);
  const [draftIsDirty, setDraftIsDirty] = useState(false);
  const [persistenceOperation, setPersistenceOperation] = useState<PersistenceOperation>("idle");
  const [persistenceNotice, setPersistenceNotice] = useState<{
    tone: NoticeTone;
    message: string;
  } | null>(null);
  const [pendingConfirmation, setPendingConfirmation] = useState<PendingConfirmation | null>(null);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const resultSummaryRef = useRef<HTMLDivElement>(null);
  const confirmationRef = useRef<HTMLDivElement>(null);

  const refreshSavedScenarios = useCallback(async () => {
    setSavedListState("loading");
    setSavedListError(null);
    try {
      setSavedScenarios(await listSavedCostScenarios());
      setSavedListState("ready");
    } catch (error) {
      setSavedListState("error");
      setSavedListError(savedOperationMessage(error, "Sparade bilar kunde inte hämtas. Kontrollera databasen och försök igen."));
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    listSavedCostScenarios()
      .then((scenarios) => {
        if (!cancelled) {
          setSavedScenarios(scenarios);
          setSavedListState("ready");
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setSavedListState("error");
          setSavedListError(savedOperationMessage(
            error,
            "Sparade bilar kunde inte hämtas. Kontrollera databasen och försök igen.",
          ));
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function updateForm(updater: (current: ManualCalculationForm) => ManualCalculationForm) {
    const next = updater(form);
    setForm(next);
    setRequestError(null);
    setPersistenceNotice(null);
    setPendingConfirmation(null);
    setDraftIsDirty(true);
    if (hasValidated) {
      setErrors(validatePage(next, registrationNumber, hasValidatedForSave).errors);
    }
    if (result) {
      setResultIsStale(true);
    }
  }

  function updateRegistrationNumber(value: string) {
    setRegistrationNumber(value);
    setPersistenceNotice(null);
    setPendingConfirmation(null);
    setDraftIsDirty(true);
    if (hasValidatedForSave) {
      setErrors(validatePage(form, value, true).errors);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setHasValidated(true);
    setHasValidatedForSave(false);
    setRequestError(null);
    setPersistenceNotice(null);

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
      setResultIsPersisted(false);
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

  function applySavedScenario(saved: SavedCostScenarioResponse, message: string) {
    setForm(savedScenarioToForm(saved));
    setRegistrationNumber(saved.registrationNumber);
    setCurrentSaved(savedScenarioMetadata(saved));
    setResult(saved.result);
    setResultIsStale(false);
    setResultIsPersisted(true);
    setDraftIsDirty(false);
    setErrors({});
    setHasValidated(false);
    setHasValidatedForSave(false);
    setRequestError(null);
    setPendingConfirmation(null);
    setPersistenceNotice({ tone: "success", message });
    focusAfterRender(() => resultSummaryRef.current);
  }

  function resetToNewDraft() {
    setForm(createInitialManualCalculationForm());
    setRegistrationNumber("");
    setCurrentSaved(null);
    setResult(null);
    setResultIsStale(false);
    setResultIsPersisted(false);
    setDraftIsDirty(false);
    setErrors({});
    setHasValidated(false);
    setHasValidatedForSave(false);
    setRequestError(null);
    setPersistenceNotice(null);
    setPendingConfirmation(null);
  }

  function requestNewDraft() {
    if (currentSaved || draftIsDirty) {
      showConfirmation({ kind: "new" });
      return;
    }
    resetToNewDraft();
  }

  function requestOpenScenario(scenario: SavedCostScenarioSummary) {
    if (scenario.vehicleId === currentSaved?.vehicleId) {
      return;
    }
    if (currentSaved || draftIsDirty) {
      showConfirmation({ kind: "open", scenario });
      return;
    }
    void openScenario(scenario.vehicleId);
  }

  async function openScenario(vehicleId: string) {
    setPersistenceOperation("opening");
    setPersistenceNotice(null);
    setPendingConfirmation(null);
    try {
      const saved = await getSavedCostScenario(vehicleId);
      applySavedScenario(saved, "Den sparade bilen har öppnats.");
    } catch (error) {
      if (isSavedProblem(error, "savedCostScenarioNotFound")) {
        if (currentSaved?.vehicleId === vehicleId) {
          keepCurrentAsUnsavedDraft();
        }
        setPersistenceNotice({
          tone: "warning",
          message: "Bilen finns inte längre. Listan har uppdaterats och dina nuvarande uppgifter har behållits.",
        });
        void refreshSavedScenarios();
      } else {
        setPersistenceNotice({
          tone: "error",
          message: savedOperationMessage(error, "Den sparade bilen kunde inte öppnas. Försök igen."),
        });
      }
    } finally {
      setPersistenceOperation("idle");
    }
  }

  async function handleSave() {
    setHasValidated(true);
    setHasValidatedForSave(true);
    setRequestError(null);
    setPersistenceNotice(null);
    setPendingConfirmation(null);

    const validation = validatePage(form, registrationNumber, true);
    setErrors(validation.errors);
    if (!validation.request || !validation.normalizedRegistrationNumber) {
      focusAfterRender(() => errorSummaryRef.current);
      return;
    }

    setPersistenceOperation("saving");
    try {
      if (currentSaved) {
        const saved = await replaceSavedCostScenario(currentSaved.vehicleId, {
          expectedRevision: currentSaved.revision,
          scenario: validation.request,
        });
        applySavedScenario(saved, "Ändringarna har sparats.");
      } else {
        const saved = await createSavedCostScenario({
          registrationNumber: validation.normalizedRegistrationNumber,
          scenario: validation.request,
        });
        applySavedScenario(saved, "Bilen har sparats.");
      }
      await refreshSavedScenarios();
    } catch (error) {
      await handleSaveFailure(
        error,
        validation.request,
        validation.normalizedRegistrationNumber,
        currentSaved?.vehicleId,
      );
    } finally {
      setPersistenceOperation("idle");
    }
  }

  async function handleSaveFailure(
    error: unknown,
    request: ManualCalculationRequest,
    normalizedRegistration: string,
    conflictVehicleId?: string,
  ) {
    if (error instanceof SavedCostScenarioApiError && error.validationProblem) {
      const serverErrors = savedValidationProblemToErrors(error.validationProblem);
      setErrors(serverErrors);
      setRequestError(
        Object.keys(serverErrors).length > 0
          ? "Servern hittade värden som behöver rättas innan bilen kan sparas."
          : error.message,
      );
      focusAfterRender(() => errorSummaryRef.current);
      return;
    }

    if (isSavedProblem(error, "registrationNumberConflict")) {
      try {
        const existing = error.problem?.existingVehicleId
          ? await getSavedCostScenario(error.problem.existingVehicleId)
          : await getSavedCostScenarioByRegistration(normalizedRegistration);
        showConfirmation({ kind: "duplicate", existing, request });
      } catch (lookupError) {
        setPersistenceNotice({
          tone: "error",
          message: savedOperationMessage(
            lookupError,
            "Bilen finns redan, men den befintliga kalkylen kunde inte hämtas.",
          ),
        });
      }
      return;
    }

    const affectedVehicleId = currentSaved?.vehicleId ?? conflictVehicleId;
    if (isSavedProblem(error, "revisionConflict") && affectedVehicleId) {
      setPersistenceNotice({
        tone: "warning",
        message: "Bilen har ändrats sedan du öppnade den. Dina ändringar är kvar och inget har skrivits över.",
      });
      showConfirmation({ kind: "reload", vehicleId: affectedVehicleId });
      return;
    }

    if (isSavedProblem(error, "savedCostScenarioNotFound") && affectedVehicleId) {
      if (currentSaved?.vehicleId === affectedVehicleId) {
        keepCurrentAsUnsavedDraft();
      }
      setPersistenceNotice({
        tone: "warning",
        message: "Den sparade bilen finns inte längre. Dina uppgifter är kvar som en osparad kalkyl.",
      });
      void refreshSavedScenarios();
      return;
    }

    setPersistenceNotice({
      tone: "error",
      message: savedOperationMessage(error, "Bilen kunde inte sparas. Dina uppgifter finns kvar."),
    });
  }

  async function replaceDuplicate() {
    if (pendingConfirmation?.kind !== "duplicate") {
      return;
    }
    const { existing, request } = pendingConfirmation;
    setPendingConfirmation(null);
    setPersistenceOperation("saving");
    try {
      const saved = await replaceSavedCostScenario(existing.vehicleId, {
        expectedRevision: existing.revision,
        scenario: request,
      });
      applySavedScenario(saved, "Den tidigare sparade kalkylen har ersatts.");
      await refreshSavedScenarios();
    } catch (error) {
      await handleSaveFailure(error, request, existing.registrationNumber, existing.vehicleId);
    } finally {
      setPersistenceOperation("idle");
    }
  }

  function requestDeleteScenario(scenario: SavedCostScenarioSummary) {
    showConfirmation({ kind: "delete", scenario });
  }

  async function deleteScenario(scenario: SavedCostScenarioSummary) {
    setPendingConfirmation(null);
    setPersistenceOperation("deleting");
    const deletingCurrent = scenario.vehicleId === currentSaved?.vehicleId;
    const expectedRevision = deletingCurrent ? currentSaved.revision : scenario.revision;
    try {
      await deleteSavedCostScenario(scenario.vehicleId, expectedRevision);
      if (deletingCurrent) {
        keepCurrentAsUnsavedDraft();
        setPersistenceNotice({
          tone: "success",
          message: "Bilen har tagits bort permanent. Formuläret och resultatet finns kvar som en osparad kalkyl.",
        });
      } else {
        setPersistenceNotice({ tone: "success", message: "Bilen har tagits bort permanent." });
      }
      setSavedScenarios((current) => current.filter((item) => item.vehicleId !== scenario.vehicleId));
      await refreshSavedScenarios();
    } catch (error) {
      if (isSavedProblem(error, "savedCostScenarioNotFound")) {
        if (deletingCurrent) {
          keepCurrentAsUnsavedDraft();
        }
        setPersistenceNotice({
          tone: "warning",
          message: "Bilen var redan borttagen. Listan har uppdaterats och dina formulärvärden har behållits.",
        });
        await refreshSavedScenarios();
      } else if (isSavedProblem(error, "revisionConflict")) {
        setPersistenceNotice({
          tone: "warning",
          message: "Bilen har ändrats och kunde därför inte tas bort. Inget har raderats.",
        });
        if (deletingCurrent) {
          showConfirmation({ kind: "reload", vehicleId: scenario.vehicleId });
        }
        await refreshSavedScenarios();
      } else {
        setPersistenceNotice({
          tone: "error",
          message: savedOperationMessage(error, "Bilen kunde inte tas bort. Försök igen."),
        });
      }
    } finally {
      setPersistenceOperation("idle");
    }
  }

  function keepCurrentAsUnsavedDraft() {
    setCurrentSaved(null);
    setDraftIsDirty(true);
    setResultIsPersisted(false);
    setRegistrationNumber((current) => normalizeRegistrationNumber(current));
  }

  function showConfirmation(confirmation: PendingConfirmation) {
    setPendingConfirmation(confirmation);
    focusAfterRender(() => confirmationRef.current);
  }

  function confirmPendingAction() {
    const confirmation = pendingConfirmation;
    if (!confirmation) {
      return;
    }

    if (confirmation.kind === "new") {
      resetToNewDraft();
    } else if (confirmation.kind === "open") {
      void openScenario(confirmation.scenario.vehicleId);
    } else if (confirmation.kind === "delete") {
      void deleteScenario(confirmation.scenario);
    } else if (confirmation.kind === "duplicate") {
      void replaceDuplicate();
    } else {
      void openScenario(confirmation.vehicleId);
    }
  }

  function openDuplicateInstead() {
    if (pendingConfirmation?.kind !== "duplicate") {
      return;
    }
    applySavedScenario(pendingConfirmation.existing, "Den befintliga sparade bilen har öppnats.");
    void refreshSavedScenarios();
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
  const persistenceBusy = persistenceOperation !== "idle";
  const formBusy = requestState === "submitting"
    || persistenceOperation === "saving"
    || persistenceOperation === "opening";

  return (
    <div className="space-y-10">
      <header className="border-b border-slate-800 pb-8">
        <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-start">
          <div>
            <Badge variant="success">Tillgänglig</Badge>
            <h1 className="mt-4 text-3xl font-bold tracking-tight text-white sm:text-4xl">Manuell kalkyl</h1>
            <p className="mt-4 max-w-3xl text-base leading-7 text-slate-400">
              Räkna på bilens kassaflöde och ägandekostnad med dina egna antaganden. Förhandsvisning
              fungerar utan databasen, och färdiga bilscenarier kan sparas lokalt.
            </p>
          </div>
          <Button type="button" variant="secondary" disabled={formBusy} onClick={requestNewDraft}>
            <RotateCcw size={17} /> Ny kalkyl
          </Button>
        </div>
      </header>

      <SavedScenariosPanel
        state={savedListState}
        scenarios={savedScenarios}
        error={savedListError}
        currentVehicleId={currentSaved?.vehicleId ?? null}
        busy={persistenceBusy}
        onRetry={() => void refreshSavedScenarios()}
        onOpen={requestOpenScenario}
        onDelete={requestDeleteScenario}
      />

      {persistenceNotice && (
        <PersistenceNotice tone={persistenceNotice.tone} message={persistenceNotice.message} />
      )}

      {pendingConfirmation && (
        <ConfirmationPanel
          ref={confirmationRef}
          confirmation={pendingConfirmation}
          busy={persistenceBusy}
          onConfirm={confirmPendingAction}
          onOpenExisting={pendingConfirmation.kind === "duplicate" ? openDuplicateInstead : undefined}
          onCancel={() => setPendingConfirmation(null)}
        />
      )}

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1.6fr)_minmax(20rem,0.8fr)]">
        <form onSubmit={handleSubmit} noValidate aria-busy={formBusy} className="space-y-6">
          <ErrorSummary ref={errorSummaryRef} errors={errors} requestError={requestError} />

          <fieldset disabled={formBusy} className="contents">
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
                  help="Valfritt, till exempel modell eller ett eget smeknamn."
                  maxLength={121}
                />
                <TextField
                  label="Registreringsnummer"
                  path="registrationNumber"
                  value={registrationNumber}
                  onChange={updateRegistrationNumber}
                  onBlur={() => {
                    if (!currentSaved && registrationNumber.trim()) {
                      updateRegistrationNumber(normalizeRegistrationNumber(registrationNumber));
                    }
                  }}
                  errors={errors}
                  help={currentSaved
                    ? "Registreringsnumret kan inte ändras. Ta bort bilen och skapa den igen för att rätta det."
                    : "Krävs endast när bilen sparas, till exempel ABC123 eller ABC12D."}
                  readOnly={Boolean(currentSaved)}
                  maxLength={12}
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
              <CardContent className="flex flex-col items-start justify-between gap-5 pt-6 lg:flex-row lg:items-center">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold text-slate-100">
                      {currentSaved ? `${currentSaved.registrationNumber} är öppnad` : "Osparad kalkyl"}
                    </p>
                    {currentSaved && (
                      <Badge variant={draftIsDirty ? "warning" : "success"}>
                        {draftIsDirty ? "Osparade ändringar" : `Sparad revision ${currentSaved.revision}`}
                      </Badge>
                    )}
                  </div>
                  <p className="mt-1 text-sm text-slate-400">
                    Förhandsvisning sparar ingenting. Spara bil kräver ett registreringsnummer och
                    skriver bilens aktuella kalkyl till PostgreSQL.
                  </p>
                </div>
                <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row">
                  <Button type="submit" size="lg" variant="secondary" disabled={formBusy}>
                    {requestState === "submitting" ? <LoaderCircle className="animate-spin" size={18} /> : <Calculator size={18} />}
                    {requestState === "submitting" ? "Beräknar…" : result ? "Beräkna igen" : "Beräkna kostnad"}
                  </Button>
                  <Button type="button" size="lg" disabled={formBusy} onClick={() => void handleSave()}>
                    {persistenceOperation === "saving" ? <LoaderCircle className="animate-spin" size={18} /> : <Save size={18} />}
                    {persistenceOperation === "saving"
                      ? "Sparar…"
                      : currentSaved
                        ? "Spara ändringar"
                        : "Spara bil"}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </fieldset>

          <p className="sr-only" role="status" aria-live="polite">
            {requestState === "submitting"
              ? "Beräkningen pågår."
              : persistenceOperation === "saving"
                ? "Bilen sparas."
                : persistenceOperation === "opening"
                  ? "Den sparade bilen öppnas."
                  : persistenceOperation === "deleting"
                    ? "Den sparade bilen tas bort."
              : requestState === "success"
                ? "Beräkningen är klar."
                : ""}
          </p>
        </form>

        <aside className="xl:sticky xl:top-8">
          {result ? (
            <ResultSummary result={result} isStale={resultIsStale} isPersisted={resultIsPersisted} summaryRef={resultSummaryRef} />
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

      {result && <ResultDetails result={result} isStale={resultIsStale} isPersisted={resultIsPersisted} />}
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

function TextField({ label, path, value, onChange, onBlur, errors, help, suffix, inputMode, placeholder, required, maxLength, disabled, readOnly }: { label: string; path: string; value: string; onChange: (value: string) => void; onBlur?: () => void; errors: ValidationErrors; help?: string; suffix?: string; inputMode?: "decimal" | "numeric"; placeholder?: string; required?: boolean; maxLength?: number; disabled?: boolean; readOnly?: boolean }) {
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
        <input id={fieldDomId(path)} name={path} type="text" inputMode={inputMode} value={value} onChange={(event) => onChange(event.target.value)} onBlur={onBlur} placeholder={placeholder} maxLength={maxLength} disabled={disabled} readOnly={readOnly} aria-invalid={fieldErrors ? true : undefined} aria-describedby={describedBy} aria-required={required} className={cn(inputClass, readOnly && "cursor-not-allowed bg-slate-900/80 text-slate-400", suffix && "pr-20", fieldErrors && errorInputClass)} />
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

function PersistenceNotice({ tone, message }: { tone: NoticeTone; message: string }) {
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={cn(
        "rounded-2xl border p-4 text-sm leading-6",
        tone === "success" && "border-emerald-400/30 bg-emerald-400/10 text-emerald-100",
        tone === "warning" && "border-amber-400/30 bg-amber-400/10 text-amber-100",
        tone === "error" && "border-rose-400/30 bg-rose-400/10 text-rose-100",
      )}
    >
      {message}
    </div>
  );
}

const ConfirmationPanel = forwardRef<HTMLDivElement, {
  confirmation: PendingConfirmation;
  busy: boolean;
  onConfirm: () => void;
  onOpenExisting?: () => void;
  onCancel: () => void;
}>(function ConfirmationPanel(
  { confirmation, busy, onConfirm, onOpenExisting, onCancel },
  ref,
) {
  const copy = confirmationCopy(confirmation);
  return (
    <div
      ref={ref}
      tabIndex={-1}
      role="alertdialog"
      aria-labelledby="saved-confirmation-title"
      aria-describedby="saved-confirmation-description"
      className="scroll-mt-24 rounded-2xl border border-amber-400/30 bg-amber-400/10 p-5 outline-none focus-visible:ring-2 focus-visible:ring-amber-300"
    >
      <h2 id="saved-confirmation-title" className="font-semibold text-amber-100">{copy.title}</h2>
      <p id="saved-confirmation-description" className="mt-2 text-sm leading-6 text-amber-50/90">
        {copy.description}
      </p>
      <div className="mt-4 flex flex-wrap gap-3">
        <Button type="button" disabled={busy} onClick={onConfirm}>{copy.confirmLabel}</Button>
        {onOpenExisting && (
          <Button type="button" variant="secondary" disabled={busy} onClick={onOpenExisting}>
            Öppna sparad bil
          </Button>
        )}
        <Button type="button" variant="ghost" disabled={busy} onClick={onCancel}>Avbryt</Button>
      </div>
    </div>
  );
});

function confirmationCopy(confirmation: PendingConfirmation) {
  if (confirmation.kind === "new") {
    return {
      title: "Börja med en ny kalkyl?",
      description: "Det öppna formuläret och resultatet ersätts. En redan sparad bil ligger kvar i databasen, men osparade ändringar försvinner.",
      confirmLabel: "Skapa ny kalkyl",
    };
  }
  if (confirmation.kind === "open") {
    return {
      title: `Öppna ${savedScenarioName(confirmation.scenario)}?`,
      description: "Det nuvarande formuläret och resultatet ersätts. Osparade ändringar försvinner.",
      confirmLabel: "Öppna bilen",
    };
  }
  if (confirmation.kind === "delete") {
    const isNamed = confirmation.scenario.vehicleLabel
      ? `${confirmation.scenario.vehicleLabel} (${confirmation.scenario.registrationNumber})`
      : confirmation.scenario.registrationNumber;
    return {
      title: `Ta bort ${isNamed} permanent?`,
      description: "Bilen och hela dess sparade kalkyl tas bort från databasen. Åtgärden kan inte ångras.",
      confirmLabel: "Ta bort permanent",
    };
  }
  if (confirmation.kind === "duplicate") {
    return {
      title: `${confirmation.existing.registrationNumber} är redan sparad`,
      description: "Välj Ersätt sparad bil för att skriva över den befintliga kalkylen med formuläret, eller öppna den sparade bilen utan att ersätta den.",
      confirmLabel: "Ersätt sparad bil",
    };
  }
  return {
    title: "En nyare version finns",
    description: "Hämta senaste versionen för att fortsätta. Dina osparade ändringar ersätts inte förrän du väljer att hämta den.",
    confirmLabel: "Hämta senaste",
  };
}

function savedScenarioName(scenario: SavedCostScenarioSummary) {
  return scenario.vehicleLabel
    ? `${scenario.vehicleLabel} (${scenario.registrationNumber})`
    : scenario.registrationNumber;
}

function validatePage(
  form: ManualCalculationForm,
  registrationNumber: string,
  requireRegistration: boolean,
): {
  errors: ValidationErrors;
  request?: ManualCalculationRequest;
  normalizedRegistrationNumber?: string;
} {
  const manualValidation = validateManualCalculationForm(form);
  const errors = { ...manualValidation.errors };
  let normalizedRegistrationNumber: string | undefined;

  if (requireRegistration) {
    const registrationValidation = validateRegistrationNumber(registrationNumber);
    normalizedRegistrationNumber = registrationValidation.normalized;
    if (registrationValidation.error) {
      errors.registrationNumber = [registrationValidation.error];
    }
  }

  if (Object.keys(errors).length > 0) {
    return { errors };
  }

  return {
    errors,
    request: manualValidation.request,
    normalizedRegistrationNumber,
  };
}

function isSavedProblem(error: unknown, code: string): error is SavedCostScenarioApiError {
  return error instanceof SavedCostScenarioApiError && error.problem?.code === code;
}

function savedOperationMessage(error: unknown, fallback: string) {
  if (error instanceof SavedCostScenarioApiError) {
    return error.message;
  }
  return fallback;
}
