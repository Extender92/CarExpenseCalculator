import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { VehicleFields, Labeled, inputClass, panelClass } from "./Fields";
import { useWorkspace } from "./use-workspace";
import { CostHeadline } from "./Results";
import { HouseholdProfilePanel, WorkspaceMessages, ActiveHouseholdResult } from "./HouseholdProfilePanel";
import { useLocation } from "react-router-dom";
import { InputComparison, InputFacts, LegacyReviewEditor } from "./Review";
import { ErrorSummary } from "./ErrorSummary";
import { vehicleGroups, leaseFields } from "./form-model";
import { sameNumber } from "./numbers";
import { vehicleStateLabels } from "./labels";
import { ListingReusePanel } from "./ListingReusePanel";
const householdLink = "text-sm text-cyan-300 underline";
export function VehicleCostEditor() {
  const { workspace, state } = useWorkspace();
  const location = useLocation();
  const active = state.active;
  const outcome = state.preview.results[active.vehicleId ? active.registrationNumber : "manual"];
  const errors = { ...(!state.stale ? outcome?.fields : {}), ...state.errors };
  const latest = active.vehicleId ? state.vehicles[active.vehicleId] : null;
  const changedRemotely = latest && !sameNumber(latest.revision, active.baseRevision);
  return (
      <section className="space-y-4" aria-label="Bilredigering">
        <div className="flex flex-wrap items-baseline gap-3">
          <h2 className="text-2xl font-semibold">
            {active.fromDraft
              ? "Öppet utkast"
              : active.vehicleId
                ? `Redigera ${active.registrationNumber}`
                : "Ny manuell bil"}
          </h2>
          {active.dirty && (
            <span className="text-sm text-amber-300">
              Osparade biländringar
            </span>
          )}
        </div>
        <p className="text-sm text-slate-400">
          {vehicleStateLabels[active.state]}
          {active.fromDraft && !active.costIncluded
            ? ". Utkastet innehåller endast annonsuppgifter. Att redigera kostnader lägger till kostnadsunderlag i utkastet."
            : ""}
        </p>
        {active.state === "legacyPending" && (
          <p className="rounded-lg border border-amber-800 p-3 text-sm text-amber-200">
            Den här bilen ingår i en väntande övergång.{" "}
            <Link className={householdLink} to="/manual/transition">
              Granska och bekräfta hela övergången
            </Link>{" "}
            innan aktuella hushållsunderlag sparas.
          </p>
        )}
        {changedRemotely && (
          <div className="space-y-3">
            <p role="status" className="text-amber-200">
              Bilen har ändrats på servern. Dina lokala uppgifter och tidigare
              revision finns kvar.
            </p>
            <InputComparison
              local={active.cost.input}
              remote={latest.input ?? latest.legacy?.suggestedInput}
              fields={[
                ...vehicleGroups.flatMap((group) => group.fields),
                {
                  key: "lease",
                  label: "Leasing",
                  kind: "object",
                  fields: leaseFields,
                },
              ]}
            />
            <Button
              variant="secondary"
              onClick={() => void workspace.openVehicle(latest.vehicleId)}
            >
              Öppna aktuellt serverunderlag
            </Button>
            <Button
              variant="secondary"
              onClick={() => workspace.keepVehicleAgainstRemote()}
            >
              Behåll min bilredigering efter granskning
            </Button>
          </div>
        )}
        <ErrorSummary
          errors={errors}
          focus={Object.keys(state.errors).length > 0}
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <Labeled
            label="Registreringsnummer"
            path="registrationNumber"
            errors={errors}
          >
            {(id, describedBy) => (
              <input
                id={id}
                data-field-path="registrationNumber"
                aria-describedby={describedBy}
                aria-invalid={!!errors.registrationNumber}
                className={inputClass}
                disabled={!!active.vehicleId}
                value={active.registrationNumber}
                onChange={(event) =>
                  workspace.editActive({
                    registrationNumber: event.target.value,
                  })
                }
                placeholder="ABC123"
              />
            )}
          </Labeled>
          <Labeled
            label="Bilens namn (valfritt)"
            path="vehicleLabel"
            errors={errors}
          >
            {(id) => (
              <input
                id={id}
                className={inputClass}
                disabled={active.listingSource != null}
                value={active.cost.vehicleLabel ?? ""}
                onChange={(event) =>
                  workspace.editActive({
                    cost: {
                      ...active.cost,
                      vehicleLabel: event.target.value || null,
                    },
                  })
                }
              />
            )}
          </Labeled>
        </div>
        <ListingReusePanel />
        {state.draft &&
          !sameNumber(active.draftRevision, state.draft.revision) && (
            <div className="space-y-3">
              <p className="text-amber-200">
                Utkastplatsen har ändrats. Granska dess aktuella innehåll innan
                du väljer en ny sparning.
              </p>
              <details className={panelClass}>
                <summary className="cursor-pointer">
                  Aktuellt sparat utkast
                </summary>
                <InputFacts value={state.draft.input} />
              </details>
              <Button
                variant="secondary"
                onClick={() => workspace.acceptDraftRevision()}
              >
                Använd utkastets aktuella revision efter granskning
              </Button>
            </div>
          )}
        {active.listing && (
          <details className={panelClass}>
            <summary className="cursor-pointer font-semibold">
              Granskade annonsuppgifter i utkastet
            </summary>
            <p className="my-3 text-sm text-slate-400">
              Annonsen tas i bruk tillsammans med de delar som finns i utkastet.
              Ändra annonsuppgifter i URL-granskningen och spara ett nytt utkast
              om det behövs.
            </p>
            <InputFacts value={active.listing.draft} />
          </details>
        )}
        <VehicleFields
          value={active.cost.input}
          errors={errors}
          onChange={(input) =>
            workspace.editActive({ cost: { ...active.cost, input } })
          }
        />
        <LegacyReviewEditor
          reviews={active.reviews}
          value={active.cost}
          errors={errors}
          onChange={(cost) => workspace.editActive({ cost })}
        />
        <div className="flex flex-wrap gap-3">
          <Button
            variant="secondary"
            disabled={!!state.busy}
            onClick={() => void workspace.saveDraft()}
          >
            Spara utkast
          </Button>
          {active.fromDraft && (
            <Button
              disabled={!!state.busy || active.dirty}
              onClick={() => void workspace.adoptDraft()}
            >
              Ta utkastet i bruk
            </Button>
          )}
        </div>
        {active.fromDraft && active.dirty && (
          <p className="text-sm text-amber-200">
            Spara utkastets ändringar innan det tas i bruk.
          </p>
        )}
        <CostHeadline outcome={outcome} />
        <ActiveHouseholdResult />
        {location.pathname === "/manual" && <>
          <Button variant="secondary" disabled={state.calculating} onClick={() => void workspace.calculate()}>Beräkna nu</Button>
          <WorkspaceMessages />
          <HouseholdProfilePanel />
        </>}
      </section>
  );
}
