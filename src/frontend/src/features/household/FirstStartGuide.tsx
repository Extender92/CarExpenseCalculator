import { useState } from "react";
import { Button } from "@/components/ui/button";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { profileResource } from "@/components/editing/resources";
import { ObjectFields } from "./Fields";
import { profileFields } from "./form-model";
import { useWorkspace } from "./use-workspace";
import type { ProfileInput } from "./api";
import { ErrorSummary } from "./ErrorSummary";

import { cloneExact } from "./numbers";
import { guidePreferenceKey } from "./guide-preference";
export function FirstStartGuide({ onClose }: { onClose: () => void }) {
  const { workspace, state } = useWorkspace();
  const [entryProfile] = useState(() => cloneExact(state.profile));
  const [step, setStep] = useState(0);
  const resource = profileResource(workspace);
  const groups = [["periodMonths", "startMonth", "annualDistanceKilometres"], ["purchaseCashSek"], ["energyPrices"]];
  const titles = ["Hur mycket ska du köra?", "Hur vill du betala?", "Vilka bränslepriser vill du använda?"];
  function close() {
    try { localStorage.setItem(guidePreferenceKey, "yes"); } catch { /* A presentation preference is optional. */ }
    onClose();
  }
  const fields = (keys: string[]) => <ObjectFields value={state.profile as Record<string, unknown>}
    fields={profileFields.filter(f => keys.includes(f.key))} prefix="profile" errors={state.errors}
    onChange={value => workspace.editProfile(value as ProfileInput)} />;
  return <EditorDialog open title="Kom igång" onClose={close} resources={[resource]} actions={<>
    {step > 0 && <Button variant="secondary" onClick={() => setStep(step - 1)}>Tillbaka</Button>}
    {step < 2 ? <Button onClick={() => setStep(step + 1)}>Nästa</Button> :
      <Button disabled={!!state.busy} onClick={async () => { if (await resource.save()) close(); }}>Spara och fortsätt</Button>}
    <Button variant="ghost" disabled={!!state.busy} onClick={() => { workspace.editProfile(entryProfile); close(); }}>Hoppa över</Button>
  </>}>
    <p className="text-sm text-slate-400">Steg {step + 1} av 3 · Alla uppgifter kan kompletteras senare.</p>
    <h3 className="my-4 text-xl font-semibold">{titles[step]}</h3>
    <ErrorSummary errors={state.errors} focus={Object.keys(state.errors).length > 0} />
    {state.notice && <p role="alert">{state.notice}</p>}
    {fields(groups[step])}
    {step === 1 && <details className="mt-4"><summary>Valfria lånevillkor</summary>{fields(["loanTerms"])}</details>}
    <details className="mt-6"><summary>Fördjupning: budget, laddning och osäkerhet</summary>
      {fields(profileFields.map(f => f.key).filter(key => !groups.flat().includes(key) && key !== "loanTerms"))}
    </details>
  </EditorDialog>;
}
