import { BaselineReview } from "@/features/comparison/BaselineReview";
import { ComparisonEditorActions } from "@/features/comparison/ComparisonEditorActions";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { panelClass } from "@/features/household/Fields";
import { fieldLabel } from "@/features/household/labels";
import { formatNumeric } from "@/features/household/numbers";
import { useWorkspace } from "@/features/household/use-workspace";
import { useComparison } from "@/features/comparison/use-comparison";
import { localDate } from "@/features/comparison/workspace";
import { RulesEditor, RulesSummary } from "@/features/comparison/RulesEditor";
import { ComparisonTables } from "@/features/comparison/Tables";
import { SelectField, TextField } from "@/features/comparison/controls";
import { labelFor, reasonText } from "@/features/comparison/catalogue";
import { economicLink, focusField, errorTarget } from "@/features/comparison/navigation";
import {
  type FormErrors,
} from "@/features/household/form-model";

import { ManualCarEditor } from "@/components/editing/ManualCarEditor";
import { CarEditor, type CarEditorTab } from "@/components/editing/CarEditor";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { rulesResource } from "@/components/editing/resources";
import { HouseholdProfilePanel } from "@/features/household/HouseholdProfilePanel";

const linkClass = "text-cyan-300 underline underline-offset-4";
export function ComparisonPage() {
  const { workspace, state } = useComparison();
  const { state: h } = useWorkspace();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [rulesOpen, setRulesOpen] = useState(false);
  const [profileField, setProfileField] = useState<string | null>(null);
  const [manualField, setManualField] = useState<string | null>(null);
  const requestedVehicle = params.get("vehicleId");
  const requestedProfileField = params.get("field")?.startsWith("profile.") ? params.get("field") : null;
  const requestedTab: CarEditorTab = params.get("tab") === "cost" ? "cost" : params.get("tab") === "listing" ? "listing" : "facts";
  useEffect(() => {
    if (requestedProfileField) workspace.closeEditor();
    else if (requestedVehicle) { workspace.setMode("stored"); void workspace.select(requestedVehicle); }
    else if (workspace.state.mode === "stored") workspace.closeEditor();
  }, [requestedVehicle, requestedProfileField, workspace]);
  const [focusErrors, setFocusErrors] = useState(false);
  useEffect(() => {
    workspace.enter();
  }, [workspace]);
  const rows = workspace.results();
  const cheapestCount = rows.filter((r) => r.isCheapestEligibleComplete).length;
  const pageRows = rows.slice((state.page - 1) * 50, state.page * 50);
  const selected = state.selected;
  const isManual = state.mode === "manual";
  const selection = async (id: string, field?: string) => {
    if (isManual) { setManualField(field ?? null); await workspace.select(id); }
    else setParams({ vehicleId: id, tab: "facts", ...(field ? { field } : {}) });
  };
  const toError = async (path: string) => {
    if (path.startsWith("profile.")) { setProfileField(path); return; }
    if (path.startsWith("rules.")) { setRulesOpen(true); requestAnimationFrame(() => focusField(path)); return; }
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
      await selection(match[1], path);
      return;
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
      <div className="flex flex-wrap items-end gap-3"><Link to="/analyze-urls" className={linkClass}>Lägg till bil</Link>
        <SelectField label="Rapportinnehåll" path="reportMode" value={state.reportMode}
          options={[["summary", "Sammanfattning"], ["full", "Fullständigt underlag"]]}
          onChange={value => workspace.setReportMode(value as "summary" | "full")} />
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
      </div>
      {workspace.reportBlockReason() && <p id="report-help" className="text-sm text-slate-400">{workspace.reportBlockReason()}</p>}
      <details className={`${panelClass} space-y-4`}><summary className="cursor-pointer font-semibold">Gemensamma uppgifter och köpkrav</summary><section aria-label="Jämförelsens förutsättningar">
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
        <HouseholdProfilePanel comparisonActions={<ComparisonEditorActions />} requestedField={requestedProfileField ?? profileField} onClosed={navigating => { setProfileField(null); if (requestedProfileField && !navigating) setParams({}, { replace: true }); }} />
        <section className="space-y-3" aria-label="Köpkrav och prioriteringar">
          <RulesSummary value={state.rules} />
          <Button variant="secondary" onClick={() => setRulesOpen(true)}>Redigera köpkrav och prioriteringar</Button>
          {rulesOpen && <EditorDialog open title="Köpkrav och prioriteringar" onClose={() => setRulesOpen(false)}
            resources={[rulesResource(workspace)]} activeResource="rules">
            <RulesEditor value={state.rules} errors={state.errors} onChange={rules => workspace.editRules(rules)} />
            <ComparisonEditorActions />
            {state.notice && <p role="status">{state.notice}</p>}
          </EditorDialog>}
        </section>
      </section></details>
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
      {!rulesOpen && !selected && <BaselineReview />}
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
        {!state.stale && view && rows.some(row => row.contributions.length > 0) && (
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
        {view && (() => {
          const shared = [...new Set(rows.flatMap(row => [row.cost.totals.ownershipCost, row.cost.totals.monthlyCost, row.cost.totals.costPerMil]
            .flatMap(value => value.missingComponents).map(path => errorTarget(row, path)).filter(path => path.startsWith("profile."))))];
          return shared.length ? <details className={panelClass}><summary>Komplettera {shared.length} gemensamma uppgifter</summary>
            <ul>{shared.map(path => <li key={path}><button className={linkClass} onClick={() => setProfileField(path)}>{fieldLabel(path)}</button></li>)}</ul></details> : null;
        })()}
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
      {selected && !isManual && <CarEditor key={selected} open vehicleId={selected} initialTab={requestedTab}
        field={params.get("field")} onClose={navigating => { workspace.closeEditor(); if (!navigating) setParams({}, { replace: true }); }} />}
      {selected && isManual && state.manual.some(row => row.candidate.vehicleId === selected) && <ManualCarEditor key={selected} id={selected} field={manualField} onClose={() => workspace.closeEditor()} />}
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
