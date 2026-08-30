import { Database, FolderOpen, LoaderCircle, RefreshCw, Trash2 } from "lucide-react";
import type { SavedCostScenarioSummary } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatDateTime, formatSek } from "./presentation";
import { cn } from "@/lib/utils";

export type SavedListState = "loading" | "ready" | "error";

interface SavedScenariosPanelProps {
  state: SavedListState;
  scenarios: SavedCostScenarioSummary[];
  error: string | null;
  currentVehicleId: string | null;
  busy: boolean;
  onRetry: () => void;
  onOpen: (scenario: SavedCostScenarioSummary) => void;
  onDelete: (scenario: SavedCostScenarioSummary) => void;
}

export function SavedScenariosPanel({
  state,
  scenarios,
  error,
  currentVehicleId,
  busy,
  onRetry,
  onOpen,
  onDelete,
}: SavedScenariosPanelProps) {
  return (
    <Card aria-busy={state === "loading"}>
      <CardHeader>
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
          <div className="flex items-start gap-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-400/10 text-cyan-300">
              <Database size={20} />
            </span>
            <div>
              <CardTitle>Sparade bilar</CardTitle>
              <CardDescription>
                Öppna, uppdatera eller ta bort bilens aktuella sparade kalkyl.
              </CardDescription>
            </div>
          </div>
          {state !== "loading" && (
            <Button type="button" variant="secondary" size="sm" disabled={busy} onClick={onRetry}>
              <RefreshCw size={16} /> Uppdatera listan
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {state === "loading" && (
          <div className="flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-950/30 px-4 py-6 text-sm text-slate-400" role="status">
            <LoaderCircle className="animate-spin" size={18} /> Hämtar sparade bilar…
          </div>
        )}

        {state === "error" && (
          <div className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4" role="alert">
            <p className="font-semibold text-rose-200">Sparade bilar kunde inte hämtas</p>
            <p className="mt-1 text-sm leading-6 text-rose-100">{error}</p>
            <Button type="button" variant="secondary" size="sm" className="mt-3" disabled={busy} onClick={onRetry}>
              <RefreshCw size={16} /> Försök igen
            </Button>
          </div>
        )}

        {state === "ready" && scenarios.length === 0 && (
          <p className="rounded-xl border border-dashed border-slate-800 bg-slate-950/20 px-4 py-6 text-sm text-slate-500">
            Inga bilar är sparade ännu. Fyll i kalkylen och välj Spara bil.
          </p>
        )}

        {state === "ready" && scenarios.length > 0 && (
          <ul className="grid gap-4 lg:grid-cols-2">
            {scenarios.map((scenario) => {
              const isCurrent = scenario.vehicleId === currentVehicleId;
              const title = scenario.vehicleLabel
                ? `${scenario.vehicleLabel} (${scenario.registrationNumber})`
                : scenario.registrationNumber;
              return (
                <li
                  key={scenario.vehicleId}
                  className={cn(
                    "rounded-2xl border bg-slate-950/30 p-4",
                    isCurrent ? "border-cyan-400/50 ring-1 ring-cyan-400/20" : "border-slate-800",
                  )}
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <h3 className="font-semibold text-slate-100">{title}</h3>
                        {isCurrent && <Badge>Öppen</Badge>}
                        <Badge variant={scenario.completeness.isComplete ? "success" : "warning"}>
                          {scenario.completeness.isComplete ? "Komplett" : "Ofullständig"}
                        </Badge>
                      </div>
                      <p className="mt-1 text-xs text-slate-500">
                        Uppdaterad {formatDateTime(scenario.updatedAtUtc)} · revision {scenario.revision}
                      </p>
                    </div>
                  </div>

                  <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
                    <SummaryValue label="Inköpspris" value={formatSek(scenario.purchasePriceSek)} />
                    <SummaryValue label="Period" value={`${scenario.calculationPeriodMonths} månader`} />
                    <SummaryValue label="Känt kassaflöde" value={formatSek(scenario.cashFlowKnownTotalSek)} />
                    <SummaryValue
                      label="Känd ägandekostnad"
                      value={scenario.netOwnershipCostKnownTotalSek === null
                        ? "Ej tillgänglig"
                        : formatSek(scenario.netOwnershipCostKnownTotalSek)}
                    />
                  </dl>

                  <div className="mt-4 flex flex-wrap gap-2">
                    <Button type="button" size="sm" disabled={busy || isCurrent} onClick={() => onOpen(scenario)}>
                      <FolderOpen size={16} /> {isCurrent ? "Öppen" : "Öppna"}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={busy}
                      aria-label={`Ta bort ${title}`}
                      onClick={() => onDelete(scenario)}
                    >
                      <Trash2 size={16} /> Ta bort
                    </Button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function SummaryValue({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="mt-0.5 font-medium text-slate-200">{value}</dd>
    </div>
  );
}
