import { useState } from "react";
import { Button } from "@/components/ui/button";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { listingReuseValue } from "@/features/household/reuse-value";
import { labelFor } from "./catalogue";
import { stringifyExact } from "@/features/household/numbers";
import { useComparison } from "./use-comparison";

export function ConfirmFacts({ vehicleId }: { vehicleId: string }) {
  const { workspace, state } = useComparison();
  const [open, setOpen] = useState(false);
  const [reviewed, setReviewed] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const editing = state.facts[vehicleId];
  if (!editing) return null;
  const choices = Object.entries(editing.base.input?.facts ?? {}).filter(([, fact]) => fact?.state === "known" && fact.observations.length === 1);
  return <>
    <Button variant="secondary" onClick={() => { setSelected([]); setReviewed(stringifyExact(editing.base)); setOpen(true); }}>Bekräfta uppgifter</Button>
    {open && <EditorDialog open title="Bekräfta aktuella biluppgifter" onClose={() => setOpen(false)} actions={
      <Button disabled={!selected.length || !!state.busy || editing.dirty || reviewed !== stringifyExact(editing.base)} onClick={() => {
        workspace.editFacts(vehicleId, { ...editing.input, edits: { ...editing.input.edits,
          ...Object.fromEntries(selected.map(key => [key, { kind: "confirmCurrent" }])) } });
        setOpen(false);
      }}>Bekräfta valda värden</Button>}>
      <p className="mb-4">Välj uppgifter som du själv har kontrollerat. Spara bil skriver sedan bekräftelserna. Registerverifiering kräver en registerkälla.</p>
      {reviewed !== stringifyExact(editing.base) && <p role="alert">Underlaget har ändrats. Stäng och öppna bekräftelsen igen för att granska aktuella värden.</p>}
      {editing.dirty && <p role="alert">Spara eller kasta ändringarna först, så att du bekräftar de aktuella värdena.</p>}
      {choices.length === 0 && <p>Inga entydiga sparade värden finns att bekräfta.</p>}
      {choices.map(([key, fact]) => <label key={key} className="my-3 flex items-start gap-2"><input type="checkbox" checked={selected.includes(key)}
        onChange={event => setSelected(current => event.target.checked ? [...current, key] : current.filter(value => value !== key))} />
        <span>{labelFor(key)}: {listingReuseValue(fact!.observations[0].value, key)}</span></label>)}
    </EditorDialog>}
  </>;
}
