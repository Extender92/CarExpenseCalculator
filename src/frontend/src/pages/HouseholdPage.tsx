import { HouseholdProfilePanel, WorkspaceMessages } from "@/features/household/HouseholdProfilePanel";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import {
  panelClass,
} from "@/features/household/Fields";
import { useWorkspace } from "@/features/household/use-workspace";
import { CostHeadline, VehicleResults } from "@/features/household/Results";
import { vehicleStateLabels } from "@/features/household/labels";
import { ManualCostEditor } from "@/features/comparison/ManualCostEditor";
import { CarEditor } from "@/components/editing/CarEditor";
import { focusField } from "@/features/comparison/navigation";

export const householdLink =
  "text-sm font-medium text-cyan-300 underline decoration-cyan-800 underline-offset-4 hover:text-cyan-100";

export function HouseholdPage() {
  const [params] = useSearchParams();
  const manual = params.get("comparisonCandidateId");
  return manual ? (
    <ManualCostEditor id={manual} field={params.get("field")} />
  ) : (
    <StoredHouseholdPage />
  );
}
function StoredHouseholdPage() {
  const { workspace, state } = useWorkspace();
  const [params, setParams] = useSearchParams();
  const opened = useRef<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(!!(params.get("vehicleId") ?? params.get("listingVehicleId")));
  const newCarOpened = useRef(false);
  useEffect(() => {
    if (params.has("newCar") && !newCarOpened.current) {
      newCarOpened.current = true;
      if (workspace.newVehicle()) queueMicrotask(() => setEditorOpen(true));
    }
  }, [params, workspace]);
  const requested = params.get("vehicleId") ?? params.get("listingVehicleId");
  useEffect(() => {
    workspace.start();
    workspace.onFocus();
    return () => workspace.setCalculationActive(false);
  }, [workspace]);
  useEffect(() => {
    if (!requested) {
      if (opened.current && state.active.vehicleId) setEditorOpen(false);
      opened.current = null;
      return;
    }
    if (opened.current === requested) return;
    opened.current = requested;
    setEditorOpen(true);
    if (state.active.vehicleId !== requested)
      void workspace.openVehicle(requested, params.has("listingVehicleId"));
  }, [workspace, requested, params, state.active.vehicleId]);
  const active = state.active;
  const requestedField = params.get("field");
  useEffect(() => {
    if (requestedField && (!requested || active.vehicleId === requested))
      focusField(requestedField, workspace.state.active.cost.input);
  }, [requestedField, requested, active.vehicleId, active.token, workspace]);
  const key = active.vehicleId ? active.registrationNumber : "manual";
  const outcome = state.preview.results[key];
  if (
    params.get("returnTo") === "comparison" &&
    requested &&
    requested !== active.vehicleId
  )
    return (
      <section className={`${panelClass} space-y-4`}>
        <h1 className="text-2xl font-semibold">
          Öppnar bilens ekonomiska underlag
        </h1>
        <p role="status">
          {state.notice ??
            "Väntar på den valda bilen. Ekonomifälten visas när rätt underlag har lästs."}
        </p>
        <Link className={householdLink} to="/search">
          Tillbaka till jämförelsen
        </Link>
        <div className="flex flex-wrap gap-3">
          <Button
            variant="secondary"
            onClick={() =>
              void workspace.openVehicle(
                requested,
                params.has("listingVehicleId"),
              )
            }
          >
            Försök öppna den valda bilen igen
          </Button>
          <Button
            variant="secondary"
            onClick={() => setParams({ returnTo: "comparison" })}
          >
            Behåll nuvarande ekonomiredigering
          </Button>
        </div>
      </section>
    );
  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-semibold uppercase tracking-wider text-cyan-300">
          Manuell kalkyl
        </p>
        <h1 className="mt-2 text-3xl font-bold">Hushållskalkyl</h1>
        <p className="mt-3 max-w-3xl text-slate-300">
          Gemensamma förutsättningar för alla bilar. Ändra uppgifterna och se
          uppskattad kostnad för den period du väljer. Sparning sker först när
          du väljer en sparknapp.
        </p>
        <nav aria-label="Kalkylflöden" className="mt-3 flex flex-wrap gap-5">
          {params.get("returnTo") === "comparison" && (
            <Link className={householdLink} to="/search">
              Tillbaka till jämförelsen
            </Link>
          )}
          <Link
            className={householdLink}
            to={
              params.has("listingVehicleId")
                ? `/manual/legacy?listingVehicleId=${params.get("listingVehicleId")}`
                : "/manual/legacy"
            }
          >
            Äldre kalkyler
          </Link>
          <Link className={householdLink} to="/manual/transition">
            Granska äldre underlag
          </Link>
          <Link className={householdLink} to="/analyze-urls">
            Annonsgranskning
          </Link>
        </nav>
      </header>
      <WorkspaceMessages />
      <HouseholdProfilePanel />
      <section className={`${panelClass} space-y-4`} aria-label="Bilöversikt">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-xl font-semibold">
            Dina bilar ({state.summaries.length})
          </h2>
          <Button
            variant="secondary"
            onClick={() => {
              if (workspace.newVehicle()) {
                // Keep the old URL consumed until navigation commits. The
                // external workspace can render before the router transition.
                setParams({}); setEditorOpen(true);
              }
            }}
          >
            Ny bil
          </Button>
        </div>
        <p className="text-sm text-slate-400">
          Bilarna visas i registreringsordning. Alla använder samma profil och
          osäkerhetsläge.
        </p>
        {state.summaries.length === 0 && (
          <p className="text-sm text-slate-300">
            Inga sparade bilar har lästs in. Du kan fylla i ett manuellt
            alternativ nedan.
          </p>
        )}
        <div className="space-y-3">
          {state.summaries.map((vehicle) => (
            <article
              key={vehicle.vehicleId}
              className={`rounded-xl border p-4 ${active.vehicleId === vehicle.vehicleId ? "border-cyan-600 bg-cyan-950/20" : "border-slate-700"}`}
            >
              <div className="mb-3 flex flex-wrap justify-between gap-3">
                <div>
                  <h3 className="font-semibold">
                    {vehicle.registrationNumber}
                    {vehicle.vehicleLabel ? ` · ${vehicle.vehicleLabel}` : ""}
                  </h3>
                  <p className="text-xs text-slate-400">
                    {vehicleStateLabels[vehicle.state]}
                    {vehicle.needsListingReview
                      ? " · Annonsen behöver granskas igen"
                      : ""}
                    {vehicle.reviewItems.length
                      ? ` · ${vehicle.reviewItems.length} granskningsposter`
                      : ""}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() =>
                      void workspace
                        .openVehicle(vehicle.vehicleId)
                        .then((ok) => {
                          if (ok) {
                            opened.current = vehicle.vehicleId;
                            setParams({ vehicleId: vehicle.vehicleId }); setEditorOpen(true);
                          }
                        })
                    }
                  >
                    Öppna {vehicle.registrationNumber}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={!!state.busy}
                    onClick={() => void workspace.deleteVehicle(vehicle)}
                  >
                    Radera {vehicle.registrationNumber}
                  </Button>
                </div>
              </div>
              {state.readErrors[vehicle.vehicleId] ? (
                <p className="text-amber-200">
                  {state.readErrors[vehicle.vehicleId]}
                </p>
              ) : (
                <CostHeadline
                  outcome={state.preview.results[vehicle.registrationNumber]}
                />
              )}
            </article>
          ))}
        </div>
      </section>
      <section
        className={`${panelClass} space-y-4`}
        aria-label="Gemensamt utkast"
      >
        <h2 className="text-lg font-semibold">Gemensamt bilutkast</h2>
        <p className="text-sm text-slate-300">
          {state.draft
            ? state.draft.input
              ? `Sparat utkast: ${state.draft.input.registrationNumber}. Det ligger kvar tills det används, ersätts eller raderas.`
              : "Utkastplatsen är tom."
            : "Utkastets serverläge är inte känt ännu."}
        </p>
        <div className="flex flex-wrap gap-3">
          <Button
            variant="secondary"
            disabled={!!state.busy}
            onClick={() => void workspace.openDraft().then(() => setEditorOpen(true))}
          >
            Öppna sparat utkast
          </Button>
          <Button
            variant="ghost"
            disabled={!!state.busy || !state.draft?.input}
            onClick={() => void workspace.deleteDraft()}
          >
            Radera sparat utkast
          </Button>
        </div>
      </section>
      <Button onClick={() => setEditorOpen(true)}>Redigera bil</Button>
      {editorOpen && <CarEditor open vehicleId={active.vehicleId} initialTab="cost" field={requestedField} onClose={() => setEditorOpen(false)} />}
      {!editorOpen && outcome?.result && (
        <div className={state.stale ? "opacity-60" : ""}>
          <VehicleResults result={outcome.result} />
        </div>
      )}
    </div>
  );
}
