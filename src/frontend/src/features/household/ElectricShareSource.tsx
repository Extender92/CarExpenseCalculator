import type { components } from "@/api/schema";
import { canonicalNumber, type Exact } from "./numbers";

export function ElectricShareSource({ value }: { value: Exact<components["schemas"]["EffectiveElectricShare"]> | null | undefined }) {
  if (!value) return null;
  return <p className="text-sm">Elandel: {value.percent == null ? "Okänd" : `${canonicalNumber(value.percent.text).replace(".", ",")} %`} · {value.origin === "vehicle" ? "Bilens eget val" : "Hushållets gemensamma värde"}</p>;
}
