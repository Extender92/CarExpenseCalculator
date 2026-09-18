import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { VehicleFields } from "@/features/household/Fields";
import { initialVehicle, validateVehicle } from "@/features/household/form-model";
import { cloneExact } from "@/features/household/numbers";
import { useComparison } from "@/features/comparison/use-comparison";
import { FactsEditor } from "@/features/comparison/FactsEditor";
import { CostConfirmation } from "@/features/comparison/CostConfirmation";
import { ComparisonEditorActions } from "@/features/comparison/ComparisonEditorActions";
import { TextField } from "@/features/comparison/controls";
import { factErrors } from "@/features/comparison/preview";
import { focusField } from "@/features/comparison/navigation";
import { validateRegistrationNumber } from "@/features/manual-calculator/saved-scenarios";
import { EditorDialog, type EditingResource } from "./EditorDialog";

/** Manual comparison candidates remain in this browser workspace, as before. */
export function ManualCarEditor({ id, field, onClose }: { id: string; field?: string | null; onClose: () => void }) {
  const { workspace, state } = useComparison();
  const candidate = state.manual.find(row => row.candidate.vehicleId === id)!.candidate;
  const [base, setBase] = useState(() => cloneExact(candidate));
  const [baseConfirmation, setBaseConfirmation] = useState(() => state.manual.find(row => row.candidate.vehicleId === id)?.confirmedCost);
  const [tab, setTab] = useState<"identity" | "cost" | "facts">(field?.includes("facts") ? "facts" : "identity");
  useEffect(() => { if (field) requestAnimationFrame(() => focusField(field)); }, [field]);
  const costErrors = candidate.costInput ? validateVehicle(candidate.costInput) : {};
  const factsErrors = factErrors(candidate.facts ?? {}, `vehicle.${id}.facts`);
  const errors = { ...state.errors, ...costErrors, ...factsErrors };
  const resource = (key: typeof tab, label: string, current: unknown, previous: unknown): EditingResource => ({
    key, label, dirty: JSON.stringify(current) !== JSON.stringify(previous),
    save: async () => {
      if ((key === "cost" && Object.keys(costErrors).length) || (key === "facts" && Object.keys(factsErrors).length) || (key === "identity" && validateRegistrationNumber(candidate.registrationNumber).error)) return false;
      setBase(old => ({ ...old, ...(key === "identity" ? { registrationNumber: candidate.registrationNumber }
        : key === "cost" ? { costInput: cloneExact(candidate.costInput) } : { facts: cloneExact(candidate.facts) }) }));
      if (key === "facts") setBaseConfirmation(state.manual.find(row => row.candidate.vehicleId === id)?.confirmedCost);
      return true;
    },
    discard: () => key === "facts" ? workspace.restoreManualFacts(id, base.facts, baseConfirmation)
      : workspace.editManual(id, key === "identity" ? { registrationNumber: base.registrationNumber } : { costInput: cloneExact(base.costInput) }),
  });
  const resources = [resource("identity", "Bilidentitet i arbetsytan", candidate.registrationNumber, base.registrationNumber),
    resource("cost", "Kalkyl i arbetsytan", candidate.costInput, base.costInput),
    resource("facts", "Jämförelsefakta i arbetsytan", candidate.facts, base.facts)];
  return <EditorDialog open title={`Redigera manuell bil – ${candidate.registrationNumber || "Ny bil"}`} onClose={onClose}
    resources={resources} activeResource={tab}>
    <p className="mb-4">Detta fristående underlag används endast i denna webbläsarflik. Spara i arbetsytan behåller dina val här; inget skrivs till databasen.</p>
    <nav className="mb-4 flex flex-wrap gap-2" aria-label="Manuella bilens underlag">
      <Button variant="secondary" aria-pressed={tab === "identity"} onClick={() => setTab("identity")}>Bilidentitet</Button>
      <Button variant="secondary" aria-pressed={tab === "cost"} onClick={() => setTab("cost")}>Kalkyl</Button>
      <Button variant="secondary" aria-pressed={tab === "facts"} onClick={() => setTab("facts")}>Jämförelsefakta</Button>
    </nav>
    <div hidden={tab !== "identity"}><TextField label="Registreringsnummer" path={`vehicle.${id}.registrationNumber`}
      value={candidate.registrationNumber} errors={errors} onChange={registrationNumber => workspace.editManual(id, { registrationNumber })} />
      {validateRegistrationNumber(candidate.registrationNumber).error && <p className="text-rose-300">Ange ett giltigt registreringsnummer för jämförelsen.</p>}
    </div>
    <div hidden={tab !== "cost"}>{candidate.costInput ? <VehicleFields value={candidate.costInput} errors={errors}
      onChange={costInput => workspace.editManual(id, { costInput })} />
      : <Button onClick={() => workspace.editManual(id, { costInput: initialVehicle() })}>Lägg till kalkylunderlag</Button>}</div>
    <div hidden={tab !== "facts"}><FactsEditor input={candidate.facts ?? {}} value={workspace.result(id)?.effectiveFacts}
      manualMode prefix={`vehicle.${id}.facts`} errors={errors} onChange={input => workspace.editFacts(id, input)} />
      <CostConfirmation id={id} /></div>
    <Button className="mt-6" variant="secondary" onClick={async () => { await workspace.remove(id); if (!workspace.state.selected) onClose(); }}>Ta bort manuell bil</Button>
    <ComparisonEditorActions />
  </EditorDialog>;
}
