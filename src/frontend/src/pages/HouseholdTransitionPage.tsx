import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useWorkspace } from "@/features/household/use-workspace";
import { VehicleFields, panelClass } from "@/features/household/Fields";
import { InputFacts, LegacyReviewEditor } from "@/features/household/Review";
import { CostHeadline, VehicleResults } from "@/features/household/Results";
import { ErrorSummary } from "@/features/household/ErrorSummary";
import {
  HouseholdProfilePanel,
  WorkspaceMessages,
  householdLink,
} from "./HouseholdPage";

export function HouseholdTransitionPage() {
  const { workspace, state } = useWorkspace();
  useEffect(() => {
    workspace.start();
    workspace.onFocus();
    void workspace.loadTransition();
  }, [workspace]);
  const transition = state.transition;
  return (
    <div className="space-y-6">
      <header>
        <p className="text-sm font-semibold text-cyan-300">Hushållskalkyl</p>
        <h1 className="mt-2 text-3xl font-bold">Granska äldre underlag</h1>
        <p className="mt-3 max-w-3xl text-slate-300">
          Fyll i en gemensam profil och granska samtliga äldre bilar. Deras
          ursprungliga hushållsantaganden förs inte över automatiskt.
          Bekräftelsen sparar hela övergången tillsammans och tar bort ersatta
          äldre antaganden och resultat.
        </p>
        <div className="mt-3 flex gap-5">
          <Link className={householdLink} to="/manual">
            Till hushållskalkylen
          </Link>
          <Link className={householdLink} to="/manual/legacy">
            Äldre kalkyler
          </Link>
        </div>
      </header>
      <WorkspaceMessages />
      <HouseholdProfilePanel />
      <ErrorSummary
        errors={Object.fromEntries(
          Object.entries(state.errors).filter(
            ([path]) => !path.startsWith("profile"),
          ),
        )}
        focus
      />
      <div className="flex flex-wrap items-center gap-4">
        <Button
          variant="secondary"
          disabled={!!state.busy}
          onClick={() => void workspace.loadTransition(true)}
        >
          Läs om övergången
        </Button>
        {transition?.dirty && (
          <p className="text-sm text-amber-200">Osparade granskningsbeslut</p>
        )}
      </div>
      {!transition ? (
        <p>Övergångsunderlaget har inte lästs in.</p>
      ) : transition.snapshot.vehicles.length === 0 ? (
        <p>Det finns inga äldre kalkyler som väntar på övergång.</p>
      ) : (
        <>
          {transition.snapshot.vehicles.map((vehicle) => {
            const cost = transition.costs[vehicle.vehicleId];
            const outcome = state.preview.results[vehicle.registrationNumber];
            return (
              <section
                key={vehicle.vehicleId}
                data-vehicle-id={vehicle.vehicleId}
                className={`${panelClass} space-y-4`}
              >
                <h2 className="text-xl font-semibold">
                  {vehicle.registrationNumber}
                  {vehicle.vehicleLabel ? ` · ${vehicle.vehicleLabel}` : ""}
                </h2>
                <details className={panelClass}>
                  <summary className="cursor-pointer font-semibold">
                    Originaluppgifter från äldre kalkyl
                  </summary>
                  <div className="mt-4 text-sm">
                    <InputFacts value={vehicle.legacy?.input} />
                  </div>
                </details>
                <p className="text-sm text-slate-400">
                  Föreslaget bilunderlag med dina aktuella ändringar. Fast
                  restvärde behåller originalperioden. Alla äldre poster finns i
                  granskningen nedan.
                </p>
                <VehicleFields
                  value={cost.input}
                  errors={{
                    ...(!state.stale ? outcome?.fields : {}),
                    ...Object.fromEntries(
                      Object.entries(state.errors)
                        .filter(([path]) =>
                          path.startsWith(`transition.${vehicle.vehicleId}.`),
                        )
                        .map(([path, messages]) => [
                          path.replace(`transition.${vehicle.vehicleId}.`, ""),
                          messages,
                        ]),
                    ),
                  }}
                  onChange={(input) =>
                    workspace.editTransition(vehicle.vehicleId, {
                      ...cost,
                      input,
                    })
                  }
                />
                <LegacyReviewEditor
                  errors={Object.fromEntries(
                    Object.entries(state.errors)
                      .filter(([path]) =>
                        path.startsWith(`transition.${vehicle.vehicleId}.`),
                      )
                      .map(([path, messages]) => [
                        path.replace(`transition.${vehicle.vehicleId}.`, ""),
                        messages,
                      ]),
                  )}
                  reviews={
                    vehicle.legacy?.items ?? vehicle.unresolvedLegacyItems
                  }
                  value={cost}
                  onChange={(cost) =>
                    workspace.editTransition(vehicle.vehicleId, cost)
                  }
                />
                <CostHeadline outcome={outcome} />
                {outcome?.result && (
                  <details>
                    <summary className="cursor-pointer text-cyan-300">
                      Visa beräkning och kvarvarande luckor
                    </summary>
                    <div className="mt-4">
                      <VehicleResults result={outcome.result} />
                    </div>
                  </details>
                )}
              </section>
            );
          })}
          <div className={`${panelClass} space-y-3`}>
            <p className="text-sm text-slate-300">
              Bekräfta samtliga {transition.snapshot.vehicles.length} bilar i
              ett steg. Ett för stort eller ändrat underlag måste granskas igen.
              Ingen del av beståndet sparas separat.
            </p>
            <Button
              disabled={!!state.busy}
              onClick={() => void workspace.confirmTransition()}
            >
              Bekräfta hela övergången
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
