import { Calculator, Database, FolderOpen, LoaderCircle, RefreshCw, Trash2 } from "lucide-react";
import type { SavedListingSummary } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { formatDateTime, formatMoneyInput } from "./presentation";

export type SavedListingListState = "loading" | "ready" | "error";

interface SavedListingsPanelProps {
  state: SavedListingListState;
  listings: SavedListingSummary[];
  error: string | null;
  openVehicleIds: ReadonlySet<string>;
  busyVehicleId: string | null;
  onRetry: () => void;
  onOpen: (listing: SavedListingSummary) => void;
  onCalculate: (listing: SavedListingSummary) => void;
  onDelete: (listing: SavedListingSummary) => void;
}

export function SavedListingsPanel({
  state,
  listings,
  error,
  openVehicleIds,
  busyVehicleId,
  onRetry,
  onOpen,
  onCalculate,
  onDelete,
}: SavedListingsPanelProps) {
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
              <CardDescription>Öppna, granska eller ta bort bilens aktuella sparade annons.</CardDescription>
            </div>
          </div>
          {state !== "loading" && (
            <Button type="button" variant="secondary" size="sm" onClick={onRetry}>
              <RefreshCw size={16} /> Uppdatera listan
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {state === "loading" && (
          <div role="status" className="flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-950/30 px-4 py-6 text-sm text-slate-400">
            <LoaderCircle className="animate-spin" size={18} /> Hämtar sparade bilar…
          </div>
        )}

        {state === "error" && (
          <div role="alert" className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4">
            <p className="font-semibold text-rose-200">Sparade bilar kunde inte hämtas</p>
            <p className="mt-1 text-sm leading-6 text-rose-100">{error}</p>
            <Button type="button" variant="secondary" size="sm" className="mt-3" onClick={onRetry}>
              <RefreshCw size={16} /> Försök igen
            </Button>
          </div>
        )}

        {state === "ready" && listings.length === 0 && (
          <p className="rounded-xl border border-dashed border-slate-800 bg-slate-950/20 px-4 py-6 text-sm text-slate-500">
            Inga annonser är sparade ännu. Analysera eller skapa ett manuellt utkast och välj Spara bil.
          </p>
        )}

        {state === "ready" && listings.length > 0 && (
          <ul className="grid gap-4 lg:grid-cols-2">
            {listings.map((listing) => {
              const isOpen = openVehicleIds.has(listing.vehicleId);
              const busy = busyVehicleId === listing.vehicleId;
              const title = listing.vehicleLabel
                ? `${listing.vehicleLabel} (${listing.registrationNumber})`
                : listing.registrationNumber;
              const model = [listing.make, listing.model, listing.modelYear].filter((value) => value !== null).join(" ");
              return (
                <li
                  key={listing.vehicleId}
                  className={cn(
                    "rounded-2xl border bg-slate-950/30 p-4",
                    isOpen ? "border-cyan-400/50 ring-1 ring-cyan-400/20" : "border-slate-800",
                  )}
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <h3 className="font-semibold text-slate-100">{title}</h3>
                        {isOpen && <Badge>Öppen</Badge>}
                        <Badge variant={listing.status === "complete" ? "success" : "warning"}>
                          {statusLabel(listing.status)}
                        </Badge>
                        {listing.hasSavedCostScenario && (listing.savedCostScenarioOutdated
                          ? <Badge variant="warning">Kalkyl inaktuell</Badge>
                          : listing.savedCostScenarioSourceListingVersion !== null
                            ? <Badge variant="success">Kalkyl aktuell</Badge>
                            : <Badge variant="muted">Manuell kalkyl</Badge>)}
                      </div>
                      <p className="mt-1 text-sm text-slate-300">{model || "Modelluppgifter saknas"}</p>
                      <p className="mt-1 text-xs text-slate-500">
                        Uppdaterad {formatDateTime(listing.updatedAtUtc)} · revision {listing.revision} · annonsversion {listing.listingVersion}
                      </p>
                    </div>
                  </div>

                  <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
                    <SummaryValue label="Pris" value={listing.priceSek === null ? "Okänt" : formatMoneyInput(String(listing.priceSek))} />
                    <SummaryValue label="Mätarställning" value={listing.odometerKilometres === null ? "Okänd" : `${formatQuantity(listing.odometerKilometres / 10)} mil`} />
                    <SummaryValue label="Saknade fält" value={String(listing.missingFieldCount)} />
                  </dl>

                  <div className="mt-4 flex flex-wrap gap-2">
                    <Button type="button" size="sm" disabled={busy} onClick={() => onOpen(listing)}>
                      {busy ? <LoaderCircle className="animate-spin" size={16} /> : <FolderOpen size={16} />}
                      {isOpen ? "Visa öppet kort" : "Öppna"}
                    </Button>
                    <Button type="button" variant="secondary" size="sm" disabled={busy} onClick={() => onCalculate(listing)}>
                      <Calculator size={16} /> {listing.hasSavedCostScenario ? "Öppna kalkyl" : "Skapa kalkyl"}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={busyVehicleId !== null}
                      aria-label={`Radera ${title}`}
                      onClick={() => onDelete(listing)}
                    >
                      <Trash2 size={16} /> Radera
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

function statusLabel(status: SavedListingSummary["status"]) {
  if (status === "complete") return "Komplett";
  if (status === "partial") return "Delvis";
  return "Manuellt/otillgängligt";
}

function formatQuantity(value: number) {
  return new Intl.NumberFormat("sv-SE", { maximumFractionDigits: 3 }).format(value);
}
