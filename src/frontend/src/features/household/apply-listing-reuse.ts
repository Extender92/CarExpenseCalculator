import type { components } from "@/api/schema";
import type { FactEdits, FactWrite } from "@/features/comparison/api";
import type { VehicleInput } from "./api";
import { cloneExact, type Exact, type Numeric } from "./numbers";

export type ReusePreview = Exact<components["schemas"]["ListingReusePreviewResponse"]>;
export const reuseWarnings: Record<string, string> = {
  electricityBasisRequired: "Välj om elförbrukningen avser batteriet eller inköpt el i Kostnader.",
  consumptionPairingRequired: "Koppla förbrukningen till rätt drivmedel och körsträcka i Kostnader.",
};

/** Pure application shared by automatic missing-only reuse and explicit replacement. */
export function applyListingReuse(input: VehicleInput, facts: FactWrite, preview: ReusePreview,
  options: { selected?: readonly string[]; replace?: readonly string[]; facts?: boolean; notesOccupied?: boolean } = {}) {
  let cost = cloneExact(input);
  const edits: FactEdits = cloneExact(facts.edits ?? {});
  const applied: string[] = [];
  const accept = (key: string, occupied: boolean, already = false) => {
    const chosen = !already && (options.selected ? options.selected.includes(key) : !occupied) &&
      (!occupied || !!options.replace?.includes(key));
    if (chosen) applied.push(key);
    return chosen;
  };
  const priceFact = preview.factTargets.find(t => t.field === "purchasePriceSek");
  const p = preview.purchasePrice;
  if (p && accept("price", p.requiresReplacement || !!priceFact?.requiresReplacement, p.alreadyApplied && !!priceFact?.alreadyApplied)) {
    cost = { ...cost, priceSek: cloneExact(p.value), priceSource: cloneExact(p.source) };
    if (options.facts !== false) edits.purchasePriceSek = { kind: "listing" };
  }
  const tax = preview.annualTax;
  if (tax && accept("tax", tax.requiresReplacement, tax.alreadyApplied))
    cost.tax = { isIncluded: false, items: [cloneExact(tax.value)] };
  for (const s of preview.energySources) if (accept(`energy:${s.key}`, s.requiresReplacement, s.alreadyApplied))
    cost.energySources = [...(cost.energySources ?? []).filter(row => row.key !== s.key), cloneExact(s.value)];
  if (options.facts !== false) {
    for (const target of preview.factTargets) {
      if (target.field === "purchasePriceSek") continue;
      if (accept(`fact:${target.field}`, target.requiresReplacement, target.alreadyApplied))
        Object.assign(edits, { [target.field]: { kind: "listing" } });
    }
    if (preview.facts.conditionNotes && accept("notes", !!options.notesOccupied || facts.edits?.conditionNotes != null))
      edits.conditionNotes = preview.facts.conditionNotes.map(() => ({ kind: "listing" }));
  }
  return { cost, facts: { ...cloneExact(facts), edits }, applied };
}

/** Only sources carried by the captured proposals acquire the adopted listing version. */
export function bindReuseSources(input: VehicleInput, version: Numeric): VehicleInput {
  const cost = cloneExact(input);
  const bind = (source: VehicleInput["priceSource"]) => source && source.listingVersion == null
    ? { ...source, listingVersion: version } : source;
  cost.priceSource = bind(cost.priceSource);
  cost.energySources = cost.energySources?.map(row => ({ ...row, fuelSource: bind(row.fuelSource), consumptionSource: bind(row.consumptionSource) }));
  if (cost.tax) cost.tax.items = cost.tax.items?.map(row => ({ ...row, listingSource: bind(row.listingSource) }));
  return cost;
}
