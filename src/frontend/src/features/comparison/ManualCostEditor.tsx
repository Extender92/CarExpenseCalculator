import { useEffect } from "react";
import { Link } from "react-router-dom";
import { VehicleFields, panelClass } from "@/features/household/Fields";
import { ErrorSummary } from "@/features/household/ErrorSummary";
import { validateVehicle } from "@/features/household/form-model";
import { useComparison } from "./use-comparison";
import { focusField } from "./navigation";

export function ManualCostEditor({
  id,
  field,
}: {
  id: string;
  field: string | null;
}) {
  const { workspace, state } = useComparison();
  const candidate = state.manual.find(
    (m) => m.candidate.vehicleId === id,
  )?.candidate;
  useEffect(() => {
    if (candidate) workspace.ensureManualCost(id);
  }, [workspace, id, candidate]);
  const costReady = !!candidate?.costInput;
  useEffect(() => {
    if (costReady && field) focusField(field, workspace.manualCost(id));
  }, [field, costReady, workspace, id]);
  const errors = candidate?.costInput
    ? validateVehicle(candidate.costInput)
    : {};
  return (
    <section className={`${panelClass} space-y-4`}>
      <h1 className="text-2xl font-bold">
        Manuellt jämförelseunderlag –{" "}
        {candidate?.registrationNumber || "Ny bil"}
      </h1>
      <Link className="text-cyan-300 underline" to="/search">
        Tillbaka till jämförelsen
      </Link>
      <p>
        Ändringar behålls i flikens arbetsyta och används i den manuella
        jämförelsen. Inget sparas i databasen.
      </p>
      {!candidate ? (
        <p>
          Det manuella underlaget finns inte i denna flik. Gå tillbaka och lägg
          till bilen igen.
        </p>
      ) : (
        candidate.costInput && (
          <>
            <ErrorSummary errors={errors} />
            <VehicleFields
              value={candidate.costInput}
              errors={errors}
              onChange={(costInput) => workspace.editManual(id, { costInput })}
            />
          </>
        )
      )}
    </section>
  );
}
