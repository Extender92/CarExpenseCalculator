import { forwardRef, type ForwardedRef } from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  allComparisonChoicesSelected,
  type ComparisonChoice,
  type ComparisonFieldName,
  type ListingComparisonDifference,
} from "./saved-listings";

interface ListingComparisonPanelProps {
  registrationNumber: string;
  differences: ListingComparisonDifference[];
  choices: Partial<Record<ComparisonFieldName, ComparisonChoice>>;
  busy: boolean;
  stale?: boolean;
  onChoice: (field: ComparisonFieldName, choice: ComparisonChoice) => void;
  onReplace: () => void;
  onCompareLatest?: () => void;
  onOpenExisting: () => void;
  onCancel: () => void;
}

export const ListingComparisonPanel = forwardRef<HTMLDivElement, ListingComparisonPanelProps>(
  function ListingComparisonPanel({
    registrationNumber,
    differences,
    choices,
    busy,
    stale = false,
    onChoice,
    onReplace,
    onCompareLatest,
    onOpenExisting,
    onCancel,
  }, ref) {
    const complete = !stale && allComparisonChoicesSelected(differences, choices);
    return (
      <div
        ref={(node) => setDialogRef(node, ref)}
        tabIndex={-1}
        role="alertdialog"
        aria-labelledby="listing-comparison-title"
        className="rounded-2xl border border-amber-400/30 bg-amber-400/10 p-5 outline-none focus:ring-2 focus:ring-amber-300"
      >
        <div className="flex gap-3">
          <AlertTriangle className="mt-0.5 shrink-0 text-amber-300" size={20} />
          <div>
            <h2 id="listing-comparison-title" className="font-semibold text-amber-100">
              Bilen {registrationNumber} finns redan
            </h2>
            <p className="mt-1 text-sm leading-6 text-amber-100/80">
              Välj aktivt vilken uppgift som ska behållas för varje skillnad. Registreringsnumret ändras aldrig.
              Den nya analysens URL, källor och analystid används vid ersättning.
            </p>
          </div>
        </div>

        {differences.length === 0 ? (
          <p className="mt-4 rounded-xl border border-amber-300/20 bg-slate-950/20 p-3 text-sm text-amber-100">
            De synliga annonsuppgifterna är redan lika. En ersättning uppdaterar endast annonsens kontext och version.
          </p>
        ) : (
          <fieldset className="mt-5 space-y-4" disabled={busy}>
            <legend className="sr-only">Välj sparad eller ny uppgift</legend>
            {differences.map((difference) => (
              <div key={difference.key} className="rounded-xl border border-slate-700 bg-slate-950/40 p-4">
                <p className="font-semibold text-slate-100">{difference.label}</p>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  <ComparisonChoiceCard
                    name={`comparison-${difference.key}`}
                    label="Behåll sparad uppgift"
                    value={difference.existingValue}
                    checked={choices[difference.key] === "existing"}
                    onChange={() => onChoice(difference.key, "existing")}
                  />
                  <ComparisonChoiceCard
                    name={`comparison-${difference.key}`}
                    label="Använd ny uppgift"
                    value={difference.candidateValue}
                    checked={choices[difference.key] === "candidate"}
                    onChange={() => onChoice(difference.key, "candidate")}
                  />
                </div>
              </div>
            ))}
          </fieldset>
        )}

        {stale ? (
          <p role="status" className="mt-4 text-sm text-amber-100">
            Den sparade bilen har ändrats igen. Dina val finns kvar, men den senaste versionen måste hämtas innan ersättning.
          </p>
        ) : !complete && (
          <p role="status" className="mt-4 text-sm text-amber-100">
            Gör ett val för samtliga skillnader innan bilen kan ersättas.
          </p>
        )}
        <div className="mt-5 flex flex-wrap gap-2">
          <Button type="button" disabled={busy || !complete} onClick={onReplace}>Ersätt sparad bil</Button>
          {stale && onCompareLatest && (
            <Button type="button" variant="secondary" disabled={busy} onClick={onCompareLatest}>Jämför med senaste</Button>
          )}
          <Button type="button" variant="secondary" disabled={busy} onClick={onOpenExisting}>Öppna sparad bil</Button>
          <Button type="button" variant="ghost" disabled={busy} onClick={onCancel}>Avbryt</Button>
        </div>
      </div>
    );
  },
);

function ComparisonChoiceCard({
  name,
  label,
  value,
  checked,
  onChange,
}: {
  name: string;
  label: string;
  value: string;
  checked: boolean;
  onChange: () => void;
}) {
  return (
    <label className="flex cursor-pointer gap-3 rounded-xl border border-slate-700 bg-slate-950/60 p-3 text-sm text-slate-200 has-[:checked]:border-cyan-400/60 has-[:checked]:bg-cyan-400/10">
      <input type="radio" name={name} checked={checked} onChange={onChange} className="mt-1" />
      <span>
        <span className="block font-semibold text-slate-100">{label}</span>
        <span className="mt-1 block break-words text-slate-400">{value}</span>
      </span>
    </label>
  );
}

function setDialogRef(node: HTMLDivElement | null, ref: ForwardedRef<HTMLDivElement>) {
  if (typeof ref === "function") ref(node);
  else if (ref) ref.current = node;
  if (node) queueMicrotask(() => node.focus());
}
