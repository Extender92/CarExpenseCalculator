import { useEffect, useRef } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import {
  ProfileFields,
  VehicleFields,
  Labeled,
  inputClass,
  panelClass,
} from "@/features/household/Fields";
import { useWorkspace } from "@/features/household/use-workspace";
import { CostHeadline, VehicleResults } from "@/features/household/Results";
import {
  InputComparison,
  InputFacts,
  LegacyReviewEditor,
} from "@/features/household/Review";
import { ErrorSummary } from "@/features/household/ErrorSummary";
import {
  profileFields,
  vehicleGroups,
  leaseFields,
  newKey,
} from "@/features/household/form-model";
import { formatMoney, sameNumber } from "@/features/household/numbers";
import { vehicleStateLabels } from "@/features/household/labels";

export const householdLink =
  "text-sm font-medium text-cyan-300 underline decoration-cyan-800 underline-offset-4 hover:text-cyan-100";

export function HouseholdPage() {
  const { workspace, state } = useWorkspace();
  const [params, setParams] = useSearchParams();
  const opened = useRef<string | null>(null);
  const requested = params.get("vehicleId") ?? params.get("listingVehicleId");
  useEffect(() => {
    workspace.start();
    workspace.onFocus();
  }, [workspace]);
  useEffect(() => {
    if (!requested) {
      opened.current = null;
      return;
    }
    if (opened.current === requested) return;
    opened.current = requested;
    if (state.active.vehicleId !== requested)
      void workspace.openVehicle(requested, params.has("listingVehicleId"));
  }, [workspace, requested, params, state.active.vehicleId]);
  const active = state.active;
  const key = active.vehicleId ? active.registrationNumber : "manual";
  const outcome = state.preview.results[key];
  const errors = { ...(!state.stale ? outcome?.fields : {}), ...state.errors };
  const latest = active.vehicleId ? state.vehicles[active.vehicleId] : null;
  const changedRemotely =
    latest && !sameNumber(latest.revision, active.baseRevision);
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
                setParams({});
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
                            setParams({ vehicleId: vehicle.vehicleId });
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
            onClick={() => void workspace.openDraft()}
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
        <ListingSuggestions />
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
            disabled={
              !!state.busy ||
              active.fromDraft ||
              active.state === "legacyPending"
            }
            onClick={() => void workspace.saveVehicle()}
          >
            Spara bilunderlag
          </Button>
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
      </section>
      {outcome?.result && (
        <div className={state.stale ? "opacity-60" : ""}>
          <VehicleResults result={outcome.result} />
        </div>
      )}
    </div>
  );
}

export function HouseholdProfilePanel() {
  const { workspace, state } = useWorkspace();
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
      <ErrorSummary
        errors={Object.fromEntries(
          Object.entries(errors).filter(([key]) => key.startsWith("profile")),
        )}
        focus={Object.keys(state.errors).length > 0}
      />
      <details open>
        <summary className="cursor-pointer font-medium">
          Redigera hushållsprofil
        </summary>
        <div className="mt-4">
          <ProfileFields
            value={state.profile}
            errors={errors}
            onChange={(profile) => workspace.editProfile(profile)}
          />
        </div>
      </details>
      <div className="flex flex-wrap gap-3">
        <Button
          disabled={!!state.busy}
          onClick={() => void workspace.saveProfile()}
        >
          Spara hushållsprofil
        </Button>
        <Button
          variant="secondary"
          disabled={state.calculating}
          onClick={() => void workspace.calculate()}
        >
          Beräkna nu
        </Button>
      </div>
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

function ListingSuggestions() {
  const { workspace, state } = useWorkspace();
  const active = state.active;
  const source = active.listingSource;
  if (!source) return null;
  const listing = source.listing;
  return (
    <details className={panelClass}>
      <summary className="cursor-pointer font-semibold">
        Granska annonsvärden innan de används
      </summary>
      <p className="my-3 text-sm text-slate-400">
        Annonsuppgifter är underlag från annonsen och behöver granskas. Ingen
        uppgift nedan ersätter kostnadsunderlaget automatiskt.
      </p>
      <div className="space-y-3">
        {listing.priceSek && (
          <p className="flex flex-wrap items-center gap-3">
            Annonspris: {formatMoney(listing.priceSek.value)}{" "}
            <Button
              variant="secondary"
              size="sm"
              disabled={active.cost.input.acquisitionType === "lease"}
              onClick={() =>
                workspace.editActive({
                  cost: {
                    ...active.cost,
                    input: {
                      ...active.cost.input,
                      priceSek: listing.priceSek!.value,
                    },
                  },
                })
              }
            >
              Använd annonspriset
            </Button>
          </p>
        )}
        {listing.annualVehicleTaxSek && (
          <p className="flex flex-wrap items-center gap-3">
            Årlig skatt: {formatMoney(listing.annualVehicleTaxSek.value)}{" "}
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                if (
                  active.cost.input.tax &&
                  !window.confirm(
                    "Ersätta nuvarande skatteposter med annonsens årsbelopp?",
                  )
                )
                  return;
                workspace.editActive({
                  cost: {
                    ...active.cost,
                    input: {
                      ...active.cost.input,
                      tax: {
                        isIncluded: false,
                        items: [
                          {
                            key: newKey(),
                            label: "Skatt från granskad annons",
                            amountSek: {
                              single: listing.annualVehicleTaxSek!.value,
                            },
                            cadence: "annual",
                            sourceUrl: source.normalizedUrl,
                          },
                        ],
                      },
                    },
                  },
                });
              }}
            >
              Använd annonsens skatt
            </Button>
          </p>
        )}
        {listing.energyConsumptions && (
          <>
            <p className="text-sm">
              Annonsens förbrukning kräver att du anger drivmedel och
              förbrukningsgrund.
            </p>
            {listing.energyConsumptions.values.map((consumption, index) => (
              <div key={index} className="text-sm">
                <InputFacts value={consumption} />
                <Button
                  className="mt-2"
                  size="sm"
                  variant="secondary"
                  disabled={(active.cost.input.energySources?.length ?? 0) >= 2}
                  onClick={() =>
                    workspace.editActive({
                      cost: {
                        ...active.cost,
                        input: {
                          ...active.cost.input,
                          energySources: [
                            ...(active.cost.input.energySources ?? []),
                            {
                              key: newKey(),
                              unit: consumption.unit,
                              consumptionPer100Kilometres: {
                                single: consumption.consumptionPer100Kilometres,
                              },
                            },
                          ],
                        },
                      },
                    })
                  }
                >
                  Lägg till annonsförbrukning {index + 1}
                </Button>
              </div>
            ))}
          </>
        )}
        <Button
          variant="secondary"
          onClick={() => void workspace.confirmListingVersion()}
        >
          Bekräfta granskning av aktuell annonsversion
        </Button>
        {active.cost.listingLinkMode === "current" && (
          <p className="text-sm text-amber-200">
            Annonsversionen bekräftas när bilunderlaget sparas.
          </p>
        )}
      </div>
    </details>
  );
}
