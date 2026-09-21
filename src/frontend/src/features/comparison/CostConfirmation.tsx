import { Button } from "@/components/ui/button";
import { panelClass } from "@/features/household/Fields";
import { useComparison } from "./use-comparison";

export function CostConfirmation({ id: selected }: { id: string }) {
  const { workspace, state } = useComparison();
  const isManual = state.mode === "manual";
  const manual = state.manual.find(m => m.candidate.vehicleId === selected);
  const edit = state.facts[selected];
  const selectedResult = workspace.result(selected);
  if (isManual ? !manual : !edit) return null;
  return (              <fieldset className={`${panelClass} space-y-3`}>
                <legend>Kostnadsbekräftelse</legend>
                <p>
                  Bekräftelsen gäller exakt bilens kostnadsunderlag. Gemensamma
                  hushållsvärden är fortfarande antaganden.
                </p>
                <p>
                  {selectedResult?.costConfirmedAt
                    ? `Underlaget i förhandsvisningen bekräftades ${selectedResult.costConfirmedAt}.`
                    : "Kostnadsunderlaget är inte bekräftat i en aktuell förhandsvisning."}
                </p>
                {(isManual
                  ? manual!.candidate.facts?.costConfirmation
                  : edit!.input.costConfirmation) === "confirm" && (
                  <p className="text-amber-200">
                    Uttrycklig osparad bekräftelse för förhandsvisningen. En
                    ändring av kostnadsunderlaget kräver nytt val.
                  </p>
                )}
                <div className="flex flex-wrap gap-3">
                  <Button
                    variant="secondary"
                    onClick={() =>
                      workspace.confirmPreview(selected, "confirm")
                    }
                  >
                    Bekräfta endast förhandsvisningen
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={() => workspace.confirmPreview(selected, "clear")}
                  >
                    Återkalla i förhandsvisningen
                  </Button>
                  {!isManual && (
                    <>
                      <Button
                        disabled={!!state.busy}
                        onClick={() =>
                          void workspace.saveFacts(selected, "confirm")
                        }
                      >
                        Bekräfta sparat kostnadsunderlag
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!!state.busy}
                        onClick={() =>
                          void workspace.saveFacts(selected, "clear")
                        }
                      >
                        Återkalla sparad bekräftelse
                      </Button>
                    </>
                  )}
                </div>
              </fieldset>);
}
