import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { panelClass } from "@/features/household/Fields";
import { InputComparison } from "@/features/household/Review";
import { profileFields } from "@/features/household/form-model";
import { useWorkspace } from "@/features/household/use-workspace";
import { useComparison } from "./use-comparison";
import { RulesSummary } from "./RulesEditor";
const linkClass = "text-cyan-300 underline";
export function BaselineReview() {
  const { workspace, state } = useComparison();
  const { state: h } = useWorkspace();
  return <>{state.remote && (
        <section
          className={`${panelClass} space-y-3`}
          aria-label="Granska ändrat serverunderlag"
        >
          <h2 className="text-xl font-semibold">
            Serverunderlaget har ändrats
          </h2>
          <p>
            Bilantal: {state.baseline?.candidateCount.text ?? "okänt"} →{" "}
            {state.remote.candidateCount.text}. Profilrevision:{" "}
            {state.baseline?.householdProfileRevision.text} →{" "}
            {state.remote.householdProfileRevision.text}. Regelrevision:{" "}
            {state.baseline?.ruleProfileRevision.text} →{" "}
            {state.remote.ruleProfileRevision.text}.
          </p>
          <InputComparison
            local={h.profile}
            remote={state.remote.profile}
            fields={profileFields}
          />
          <details>
            <summary>Jämför lokala och aktuella köpkrav</summary>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <h3>Lokala köpkrav</h3>
                <RulesSummary value={state.rules} />
              </div>
              <div>
                <h3>Aktuella köpkrav från servern</h3>
                <RulesSummary value={state.remote.rules ?? {}} />
              </div>
            </div>
          </details>
          <p>
            Öppna berörda biluppgifter för aktuell annons och faktarevision.
            Ekonomiska konflikter granskas i{" "}
            <Link className={linkClass} to="/manual">
              Manuell kalkyl
            </Link>
            .
          </p>
          <div className="flex flex-wrap gap-3">
            <Button
              disabled={!!state.busy}
              onClick={() => void workspace.acceptRemote(true)}
            >
              Behåll granskade lokala ändringar
            </Button>
            <Button
              variant="secondary"
              disabled={!!state.busy}
              onClick={() => void workspace.acceptRemote(false)}
            >
              Använd serverunderlaget
            </Button>
          </div>
        </section>
      )}</>;
}
