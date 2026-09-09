import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { ProfileFields, panelClass } from "@/features/household/Fields";
import { InputComparison } from "@/features/household/Review";
import { formatNumeric } from "@/features/household/numbers";
import { useWorkspace } from "@/features/household/use-workspace";
import { useComparison } from "@/features/comparison/use-comparison";
import { localDate } from "@/features/comparison/workspace";
import { RulesEditor, RulesSummary } from "@/features/comparison/RulesEditor";
import { FactsEditor } from "@/features/comparison/FactsEditor";
import { ComparisonTables } from "@/features/comparison/Tables";
import { SelectField, TextField } from "@/features/comparison/controls";
import { labelFor, reasonText } from "@/features/comparison/catalogue";
import { economicLink, focusField } from "@/features/comparison/navigation";
import {
  profileFields,
  type FormErrors,
} from "@/features/household/form-model";

const linkClass = "text-cyan-300 underline underline-offset-4";
export function ComparisonPage() {
  const { workspace, state } = useComparison();
  const { workspace: household, state: h } = useWorkspace();
  const navigate = useNavigate();
  const [focusErrors, setFocusErrors] = useState(false);
  const selectedRef = useRef<HTMLElement>(null);
  useEffect(() => {
    workspace.enter();
  }, [workspace]);
  const rows = workspace.results();
  const cheapestCount = rows.filter((r) => r.isCheapestEligibleComplete).length;
  const pageRows = rows.slice((state.page - 1) * 50, state.page * 50);
  const selected = state.selected;
  const edit = selected ? state.facts[selected] : undefined;
  const manual = state.manual.find((m) => m.candidate.vehicleId === selected);
  const selectedResult = selected ? workspace.result(selected) : undefined;
  const isManual = state.mode === "manual";
  const profileErrors = {
    ...h.errors,
    ...state.errors,
    ...Object.fromEntries(
      (!state.stale
        ? (state.response?.views.baseline.profileErrors ?? [])
        : []
      ).map((e) => [
        e.path,
        [reasonText[e.code] ?? "Kontrollera hushållsuppgiften."],
      ]),
    ),
  };
  const selection = async (id: string, field?: string) => {
    await workspace.select(id);
    if (field) requestAnimationFrame(() => focusField(field));
    else selectedRef.current?.focus();
  };
  const toError = async (path: string) => {
    const match = /^vehicle\.([\da-f-]+)\.(.*)/.exec(path);
    if (match) {
      if (
        match[2].startsWith("costInput") ||
        match[2].startsWith("input") ||
        match[2].startsWith("review")
      ) {
        navigate(
          economicLink(
            match[1],
            isManual,
            match[2].replace(/^costInput/, "input"),
          ),
        );
        return;
      }
      await workspace.select(match[1]);
    }
    requestAnimationFrame(() => focusField(path));
  };
  const run = (action: () => void) => {
    setFocusErrors(true);
    action();
  };
  const view =
    state.response?.views[h.profile.activeSensitivityMode ?? "baseline"];
  return (
    <div className="min-w-0 space-y-6">
      <header>
        <h1 className="text-3xl font-bold">Jämförelse</h1>
        <p className="mt-3 text-slate-300">
          Jämför alla aktuella bilar med samma hushållsförutsättningar och dina
          egna köpkrav. Redigering beräknas utan automatisk sparning.
        </p>
      </header>
      <section
        className={`${panelClass} space-y-4`}
        aria-label="Jämförelsens förutsättningar"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <SelectField
            label="Jämförelseläge"
            path="mode"
            value={state.mode}
            options={[
              ["stored", "Alla sparade bilar"],
              ["manual", "Fristående manuella bilar"],
            ]}
            onChange={(mode) => workspace.setMode(mode as typeof state.mode)}
          />
          <TextField
            label="Utvärderingsdatum"
            path="asOfDate"
            value={state.date}
            type="date"
            errors={state.errors}
            onChange={(date) => workspace.editDate(date)}
          />
        </div>
        <div className="flex flex-wrap gap-3">
          <Button
            variant="secondary"
            onClick={() => workspace.editDate(localDate())}
          >
            Använd dagens datum
          </Button>
          <Button onClick={() => run(() => void workspace.calculate())}>
            Beräkna nu
          </Button>
          <Button
            variant="secondary"
            disabled={workspace.reportBlockReason() !== null}
            aria-describedby="report-help"
            onClick={() => {
              if (workspace.openReport()) navigate("/search/report");
            }}
          >
            Öppna rapport
          </Button>
          {!isManual && (
            <Button
              variant="secondary"
              disabled={state.loading || !!state.busy}
              onClick={() => void workspace.refresh()}
            >
              Läs aktuellt serverunderlag
            </Button>
          )}
        </div>
        <p id="report-help" className="text-sm text-slate-300">
          {workspace.reportBlockReason() ??
            "Rapporten innehåller alla bilar, alla detaljavsnitt och aktuella osparade antaganden."}
        </p>
        <p className="text-sm text-slate-300">
          Aktivt läge:{" "}
          {h.profile.activeSensitivityMode === "favorable"
            ? "Gynnsamt"
            : h.profile.activeSensitivityMode === "cautious"
              ? "Försiktigt"
              : "Normalt"}
          . Period: {h.profile.periodMonths?.text ?? "okänd"} månader.
        </p>
        {isManual && (
          <p className="text-amber-200">
            Fristående manuellt underlag utan kontroll mot lagringen.
            Registreringsnummer krävs. Bilarna sparas inte av en beräkning.
          </p>
        )}
        {h.profileDirty && (
          <p className="text-amber-200">Osparade hushållsantaganden</p>
        )}
        {state.rulesDirty && (
          <p className="text-amber-200">Osparade köpkrav och prioriteringar</p>
        )}
        <details
          open={state.editorPanels.includes("profile")}
          onToggle={(e) =>
            workspace.setEditorPanel("profile", e.currentTarget.open)
          }
        >
          <summary className="cursor-pointer font-semibold">
            Hushållsprofil – visa och redigera
          </summary>
          <div className="mt-4 space-y-4">
            <ProfileFields
              value={h.profile}
              errors={profileErrors}
              onChange={(profile) => household.editProfile(profile)}
            />
            <Button
              disabled={!!h.busy || !!state.busy}
              onClick={() => run(() => void household.saveProfile())}
            >
              Spara hushållsprofil
            </Button>
            {h.notice && <p role="status">{h.notice}</p>}
          </div>
        </details>
        <details
          open={state.editorPanels.includes("rules")}
          onToggle={(e) =>
            workspace.setEditorPanel("rules", e.currentTarget.open)
          }
        >
          <summary className="cursor-pointer font-semibold">
            Köpkrav och prioriteringar
          </summary>
          <div className="mt-4 space-y-4">
            <RulesEditor
              value={state.rules}
              errors={state.errors}
              onChange={(rules) => workspace.editRules(rules)}
            />
            <Button
              disabled={!!state.busy}
              onClick={() => run(() => void workspace.saveRules())}
            >
              Spara köpkrav och prioriteringar
            </Button>
          </div>
        </details>
      </section>
      {(state.loading || state.calculating || state.stale || state.notice) && (
        <div className={panelClass} role="status">
          {state.loading && <p>Läser serverunderlag…</p>}
          {state.calculating && <p>Beräknar hela jämförelsen…</p>}
          {state.stale && (
            <p className="text-amber-200">
              Resultaten är inaktuella. Rekommendationer visas först när hela
              den aktuella jämförelsen är färdig.
            </p>
          )}
          {state.notice && <p>{state.notice}</p>}
          {state.problem?.problem.maximumRequestBytes && (
            <p>
              Tillåten begäran:{" "}
              {formatNumeric(state.problem.problem.maximumRequestBytes, 0)}{" "}
              byte. Senaste försök: {state.requestBytes.toLocaleString("sv-SE")}{" "}
              byte.
            </p>
          )}
        </div>
      )}
      <ComparisonErrors
        errors={state.errors}
        focus={focusErrors}
        onFocused={() => setFocusErrors(false)}
        navigate={(path) => void toError(path)}
      />
      {state.remote && (
        <section
          className={`${panelClass} space-y-3`}
          aria-label="Granska ändrat serverunderlag"
        >
          <h2 className="text-xl font-semibold">
            Serverunderlaget har ändrats
          </h2>
          <p>
            Bilantal: {state.baseline?.candidateCount.text ?? "okänt"} →{" "}
            {state.remote.candidateCount.text}. Profilrevision:{" "}
            {state.baseline?.householdProfileRevision.text} →{" "}
            {state.remote.householdProfileRevision.text}. Regelrevision:{" "}
            {state.baseline?.ruleProfileRevision.text} →{" "}
            {state.remote.ruleProfileRevision.text}.
          </p>
          <InputComparison
            local={h.profile}
            remote={state.remote.profile}
            fields={profileFields}
          />
          <details>
            <summary>Jämför lokala och aktuella köpkrav</summary>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <h3>Lokala köpkrav</h3>
                <RulesSummary value={state.rules} />
              </div>
              <div>
                <h3>Aktuella köpkrav från servern</h3>
                <RulesSummary value={state.remote.rules ?? {}} />
              </div>
            </div>
          </details>
          <p>
            Öppna berörda biluppgifter för aktuell annons och faktarevision.
            Ekonomiska konflikter granskas i{" "}
            <Link className={linkClass} to="/manual">
              Manuell kalkyl
            </Link>
            .
          </p>
          <div className="flex flex-wrap gap-3">
            <Button
              disabled={!!state.busy}
              onClick={() => void workspace.acceptRemote(true)}
            >
              Behåll granskade lokala ändringar
            </Button>
            <Button
              variant="secondary"
              disabled={!!state.busy}
              onClick={() => void workspace.acceptRemote(false)}
            >
              Använd serverunderlaget
            </Button>
          </div>
        </section>
      )}
      <section className="min-w-0 space-y-4" aria-label="Alla jämförda bilar">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-xl font-semibold">Bilar i jämförelsen</h2>
            <p className="text-sm text-slate-300">
              {isManual
                ? state.manual.length
                : (state.baseline?.candidateCount.text ?? "Okänt antal")}{" "}
              bilar totalt. Alla ingår oavsett visningssida.
            </p>
          </div>
          <SelectField
            label="Sortering"
            path="sort"
            value={state.sort}
            options={[
              ["cost", "Kostnad, billigast först"],
              ["score", "Prioriteringar, högsta undre poänggräns först"],
            ]}
            onChange={(sort) => workspace.setSort(sort as typeof state.sort)}
          />
        </div>
        {isManual ? (
          <div className="space-y-3">
            <Button variant="secondary" onClick={() => workspace.addManual()}>
              Lägg till manuell bil
            </Button>
            <div className="flex flex-wrap gap-3">
              {state.manual.map((m, i) => (
                <button
                  className={linkClass}
                  key={m.candidate.vehicleId}
                  onClick={() => void selection(m.candidate.vehicleId)}
                >
                  {m.candidate.registrationNumber || `Ny bil ${i + 1}`}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <p className="text-sm">
            <Link className={linkClass} to="/manual">
              Lägg till eller redigera ekonomiskt bilunderlag
            </Link>{" "}
            ·{" "}
            <Link className={linkClass} to="/analyze-urls">
              Granska en annons
            </Link>
          </p>
        )}
        {!state.stale && view && (
          <p role="status">
            {reasonText[view.preferenceRecommendationReason] ??
              "Granska poängintervall och datatäckning för att bedöma prioriteringarna."}
          </p>
        )}
        {!state.stale &&
          rows.some(
            (r) => r.isDefinitePreferenceWinner || r.isCheapestEligibleComplete,
          ) && (
            <div
              role="region"
              className={`${panelClass} space-y-2`}
              aria-label="Rekommendationer för hela beståndet"
            >
              {cheapestCount > 1 && (
                <p>
                  {cheapestCount} bilar delar lägsta kompletta kostnad bland
                  godkända alternativ. De är markerade i tabellen.
                </p>
              )}
              {rows
                .filter(
                  (r) =>
                    r.isDefinitePreferenceWinner ||
                    (r.isCheapestEligibleComplete && cheapestCount === 1),
                )
                .map((r) => (
                  <p key={r.vehicleId}>
                    <button
                      className={linkClass}
                      onClick={() => {
                        workspace.setPage(Math.floor(rows.indexOf(r) / 50) + 1);
                        void selection(r.vehicleId);
                      }}
                    >
                      {r.registrationNumber}
                    </button>
                    :{" "}
                    {r.isCheapestEligibleComplete &&
                      "Billigast bland godkända kompletta alternativ"}
                    {r.isDefinitePreferenceWinner &&
                      `${r.isCheapestEligibleComplete ? " · " : ""}Säker preferensvinnare`}
                  </p>
                ))}
            </div>
          )}
        {rows.length === 0 && !state.calculating && (
          <p>
            Inget aktuellt jämförelseresultat visas ännu. Ange underlag eller
            läs serveruppgifterna och beräkna.
          </p>
        )}
        <ComparisonTables
          rows={pageRows}
          response={state.response}
          stale={state.stale}
          manual={isManual}
          expanded={state.expanded}
          onExpanded={(expanded) => workspace.setExpanded(expanded)}
          select={(id, field) => void selection(id, field)}
        />
        <nav
          className="flex flex-wrap items-center gap-4"
          aria-label="Jämförelsens sidor"
        >
          <Button
            variant="secondary"
            disabled={state.page <= 1}
            onClick={() => workspace.setPage(state.page - 1)}
          >
            Föregående sida
          </Button>
          <span>
            Sida {state.page} av {Math.ceil(rows.length / 50) || 1} · 50 bilar
            per sida
          </span>
          <Button
            variant="secondary"
            disabled={state.page * 50 >= rows.length}
            onClick={() => workspace.setPage(state.page + 1)}
          >
            Nästa sida
          </Button>
        </nav>
      </section>
      {selected && (
        <section
          ref={selectedRef}
          tabIndex={-1}
          className={`${panelClass} space-y-4 outline-none focus:ring-2 focus:ring-cyan-400`}
          aria-label="Biluppgifter"
        >
          <h2 className="text-xl font-semibold">
            Biluppgifter –{" "}
            {manual?.candidate.registrationNumber ||
              edit?.base.registrationNumber ||
              selectedResult?.registrationNumber ||
              "Ny bil"}
          </h2>
          {isManual && manual && (
            <TextField
              label="Registreringsnummer"
              path={`vehicle.${selected}.registrationNumber`}
              value={manual.candidate.registrationNumber}
              errors={state.errors}
              onChange={(registrationNumber) =>
                workspace.editManual(selected, { registrationNumber })
              }
            />
          )}
          {selectedResult?.effectiveCostInput?.acquisitionType ===
            "purchase" && (
            <p>
              Köpkrav och prispoäng använder kalkylens köppris, även när det
              saknas.{" "}
              <Link className={linkClass} to={economicLink(selected, isManual)}>
                Redigera kalkylpriset
              </Link>
              .
            </p>
          )}
          {(isManual ? manual : edit) ? (
            <>
              {edit && !isManual && (
                <div className="text-sm text-slate-300">
                  <p>
                    Fordonsrevision {edit.base.revision.text}. Aktuell
                    annonsversion{" "}
                    {edit.base.currentListingVersion?.text ?? "saknas"}. Fakta
                    granskade mot{" "}
                    {edit.base.factsReviewedListingVersion?.text ??
                      "ingen annons"}
                    ; kostnader mot{" "}
                    {edit.base.costReviewedListingVersion?.text ??
                      "ingen annons"}
                    .
                  </p>
                  {edit.base.needsListingReview && (
                    <p>Annonsen behöver granskas; äldre fakta har behållits.</p>
                  )}
                  <Button
                    variant="secondary"
                    onClick={() => void workspace.select(selected, true)}
                  >
                    Läs aktuella biluppgifter
                  </Button>
                  {edit.remote && (
                    <section
                      className="my-4 space-y-3"
                      aria-label="Granska ändrade biluppgifter"
                    >
                      <h3 className="font-semibold">
                        Aktuella biluppgifter finns att granska
                      </h3>
                      <p>
                        Fordonsrevision {edit.base.revision.text} →{" "}
                        {edit.remote.revision.text}. Annonsversion{" "}
                        {edit.base.currentListingVersion?.text ?? "saknas"} →{" "}
                        {edit.remote.currentListingVersion?.text ?? "saknas"}.
                      </p>
                      <p>
                        Dina lokala val finns kvar nedan. Granska serverns
                        värden och källor innan du väljer underlag.
                      </p>
                      <details>
                        <summary>Visa aktuella serverfakta och källor</summary>
                        <FactsEditor
                          input={{}}
                          value={edit.remote.input}
                          manualMode={false}
                          prefix="remoteFacts"
                          errors={{}}
                          onChange={() => undefined}
                          readOnly
                        />
                      </details>
                      <div className="flex flex-wrap gap-3">
                        <Button
                          variant="secondary"
                          onClick={() => workspace.reviewFacts(selected, true)}
                        >
                          Behåll lokala faktaval efter granskning
                        </Button>
                        <Button
                          variant="secondary"
                          onClick={() => workspace.reviewFacts(selected, false)}
                        >
                          Använd aktuella biluppgifter
                        </Button>
                      </div>
                    </section>
                  )}
                </div>
              )}
              <FactsEditor
                input={isManual ? (manual!.candidate.facts ?? {}) : edit!.input}
                value={
                  isManual ? selectedResult?.effectiveFacts : edit!.base.input
                }
                proposal={isManual ? null : edit!.base.listingProposal}
                listingVersion={edit?.base.currentListingVersion}
                manualMode={isManual}
                prefix={`vehicle.${selected}.facts`}
                errors={state.errors}
                onChange={(input) => workspace.editFacts(selected, input)}
              />
              <div className="flex flex-wrap gap-3">
                {!isManual && (
                  <Button
                    disabled={!!state.busy}
                    onClick={() =>
                      run(() => void workspace.saveFacts(selected))
                    }
                  >
                    Spara biluppgifter
                  </Button>
                )}
                <Link
                  className={linkClass}
                  to={economicLink(selected, isManual)}
                >
                  Redigera ekonomiskt underlag
                </Link>
                <Button
                  variant="secondary"
                  disabled={!!state.busy}
                  onClick={() => void workspace.remove(selected)}
                >
                  {isManual ? "Ta bort manuell bil" : "Radera bilen permanent"}
                </Button>
              </div>
              <fieldset className={`${panelClass} space-y-3`}>
                <legend>Kostnadsbekräftelse</legend>
                <p>
                  Bekräftelsen gäller exakt bilens kostnadsunderlag. Gemensamma
                  hushållsvärden är fortfarande antaganden.
                </p>
                <p>
                  {selectedResult?.costConfirmedAt
                    ? `Underlaget i förhandsvisningen bekräftades ${selectedResult.costConfirmedAt}.`
                    : "Kostnadsunderlaget är inte bekräftat i en aktuell förhandsvisning."}
                </p>
                {(isManual
                  ? manual!.candidate.facts?.costConfirmation
                  : edit!.input.costConfirmation) === "confirm" && (
                  <p className="text-amber-200">
                    Uttrycklig osparad bekräftelse för förhandsvisningen. En
                    ändring av kostnadsunderlaget kräver nytt val.
                  </p>
                )}
                <div className="flex flex-wrap gap-3">
                  <Button
                    variant="secondary"
                    onClick={() =>
                      workspace.confirmPreview(selected, "confirm")
                    }
                  >
                    Bekräfta endast förhandsvisningen
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={() => workspace.confirmPreview(selected, "clear")}
                  >
                    Återkalla i förhandsvisningen
                  </Button>
                  {!isManual && (
                    <>
                      <Button
                        disabled={!!state.busy}
                        onClick={() =>
                          void workspace.saveFacts(selected, "confirm")
                        }
                      >
                        Bekräfta sparat kostnadsunderlag
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!!state.busy}
                        onClick={() =>
                          void workspace.saveFacts(selected, "clear")
                        }
                      >
                        Återkalla sparad bekräftelse
                      </Button>
                    </>
                  )}
                </div>
              </fieldset>
            </>
          ) : (
            <p>Läser bilens fakta och källor…</p>
          )}
        </section>
      )}
    </div>
  );
}

function ComparisonErrors({
  errors,
  focus,
  onFocused,
  navigate,
}: {
  errors: FormErrors;
  focus: boolean;
  onFocused: () => void;
  navigate: (path: string) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const key = Object.entries(errors)
    .map(([path, messages]) => `${path}:${messages.join()}`)
    .join("|");
  useEffect(() => {
    if (focus && key) {
      ref.current?.focus();
      onFocused();
    }
  }, [focus, key, onFocused]);
  if (!key) return null;
  return (
    <div
      ref={ref}
      role="alert"
      tabIndex={-1}
      className="rounded border border-rose-700 bg-rose-950/30 p-4 outline-none focus:ring-2 focus:ring-rose-300"
    >
      <h2 className="font-semibold">Kontrollera uppgifterna</h2>
      <ul className="mt-2 space-y-2">
        {Object.entries(errors).map(([path, messages]) => (
          <li key={path}>
            <button
              className="text-left text-rose-200 underline"
              onClick={() => navigate(path)}
            >
              {labelFor(path.split(".").at(-1) ?? "")}: {messages.join(" ")}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
