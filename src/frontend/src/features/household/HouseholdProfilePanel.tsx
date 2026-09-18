import { useEffect, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { profileResource } from "@/components/editing/resources";
import { focusField } from "@/features/comparison/navigation";
import { useWorkspace } from "./use-workspace";
import { ProfileFields, panelClass } from "./Fields";
import { formatNumeric } from "./numbers";
import { ErrorSummary } from "./ErrorSummary";
import { InputComparison } from "./Review";
import { profileFields } from "./form-model";
import { VehicleResults } from "./Results";
export function HouseholdProfilePanel({ requestedField, onClosed, comparisonActions }: { requestedField?: string | null; onClosed?: (navigating?: boolean) => void; comparisonActions?: ReactNode } = {}) {
  const { workspace, state } = useWorkspace();
  const [locallyOpen, setOpen] = useState(false);
  const open = locallyOpen || !!requestedField;
  useEffect(() => { if (open && requestedField) requestAnimationFrame(() => focusField(requestedField)); }, [open, requestedField]);
  const errors = {
    ...(!state.stale ? state.preview.profileErrors : {}),
    ...state.errors,
  };
  return (
    <section
      className={`${panelClass} space-y-4`}
      aria-label="Gemensam hushållsprofil"
    >
      <div className="flex flex-wrap items-baseline gap-3">
        <h2 className="text-xl font-semibold">
          Hushållets gemensamma förutsättningar
        </h2>
        {state.profileDirty && (
          <span className="text-sm text-amber-300">
            Osparade profiländringar
          </span>
        )}
      </div>
      <p className="text-sm text-slate-400">
        Kontanter avser själva bilköpet. Startutgifter har en separat budget.
        Ett tomt fält förblir okänt.
      </p>
      <p className="text-sm">Period: {formatNumeric(state.profile.periodMonths, 0)} månader · Körsträcka: {formatNumeric(state.profile.annualDistanceKilometres, 2)} km/år</p>
      <Button variant="secondary" onClick={() => setOpen(true)}>Redigera hushållsprofil</Button>
      {open && <EditorDialog open title="Hushållets gemensamma förutsättningar" onClose={navigating => { setOpen(false); onClosed?.(navigating); }}
        resources={[profileResource(workspace)]} activeResource="profile">
      {state.profileDirty && <p className="text-amber-300">Osparade profiländringar</p>}
      <ErrorSummary
        errors={Object.fromEntries(
          Object.entries(errors).filter(([key]) => key.startsWith("profile")),
        )}
        focus={Object.keys(state.errors).length > 0}
      />
      <div>

        <div className="mt-4">
          <ProfileFields
            value={state.profile}
            errors={errors}
            onChange={(profile) => workspace.editProfile(profile)}
          />
        </div>
      </div>
      <div className="flex flex-wrap gap-3">
        {!comparisonActions && <Button
          variant="secondary"
          disabled={state.calculating}
          onClick={() => void workspace.calculate()}
        >
          Beräkna nu
        </Button>}
      </div>
      {comparisonActions}
      {!comparisonActions && <>
        <WorkspaceMessages />
        <ActiveHouseholdResult />
      </>}
      {state.remoteProfile && (
        <div className="space-y-3">
          <p className="text-amber-200">
            Profilen har ändrats på servern. Din redigering är bevarad.
          </p>
          <InputComparison
            local={state.profile}
            remote={state.remoteProfile.input}
            fields={profileFields}
          />
          <Button
            variant="secondary"
            onClick={() => workspace.acceptRemoteProfile()}
          >
            Använd serverns profil
          </Button>
          <Button
            variant="secondary"
            onClick={() => workspace.keepProfileAgainstRemote()}
          >
            Behåll min profilredigering efter granskning
          </Button>
        </div>
      )}
      </EditorDialog>}

    </section>
  );
}

export function WorkspaceMessages() {
  const { workspace, state } = useWorkspace();
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p
          role="status"
          className={`text-sm ${state.stale ? "text-amber-300" : "text-slate-400"}`}
        >
          {state.calculating
            ? "Beräknar hela omgången… Tidigare resultat är inaktuella."
            : state.stale
              ? "Resultaten är inaktuella och uppdateras efter redigering."
              : "Förhandsvisningen gäller nuvarande uppgifter, inklusive osparade ändringar."}
        </p>
        <Button
          variant="ghost"
          disabled={state.loading || !!state.busy}
          onClick={() => void workspace.refresh()}
        >
          {state.loading ? "Läser serverläget…" : "Uppdatera serverläget"}
        </Button>
      </div>
      {state.notice && (
        <p
          role="alert"
          className="rounded-lg border border-amber-800 bg-amber-950/20 p-3 text-sm text-amber-100"
        >
          {state.notice}
        </p>
      )}
      {state.storageNotice && (
        <p
          role="status"
          className="rounded-lg border border-amber-800 p-3 text-sm text-amber-200"
        >
          {state.storageNotice}
        </p>
      )}
    </div>
  );
}

export function ActiveHouseholdResult() {
  const { state } = useWorkspace();
  const key = state.active.vehicleId ? state.active.registrationNumber : "manual";
  const outcome = state.preview.results[key];
  return outcome?.result ? <details className={panelClass}>
    <summary>Detaljerat kalkylresultat</summary>
    <VehicleResults result={outcome.result} />
  </details> : null;
}
