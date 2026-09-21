import { reconcileReuseSources } from "@/features/url-analysis/batch-workflow";
import { describe, expect, it } from "vitest";
import { unknownFacts } from "@/features/comparison/test-fixtures";
import { applyListingReuse, bindReuseSources, type ReusePreview } from "./apply-listing-reuse";
import { n, stringifyExact } from "./numbers";
import type { VehicleInput } from "./api";

const empty = (): VehicleInput => ({ candidateKey: "new", acquisitionType: "purchase" });
const source = () => ({ listingReference: "https://www.blocket.se/mobility/item/123", field: "priceSek" as const, listingVersion: null });
function preview(): ReusePreview {
  return { purchasePrice: { key: "price", value: n("10000.123456789"), source: source(), requiresReplacement: false, alreadyApplied: false },
    annualTax: { key: "tax", value: { key: "listing-tax", label: "Skatt", amountSek: { single: n(0) }, cadence: "annual", listingSource: { ...source(), field: "annualVehicleTaxSek" } },
      source: { ...source(), field: "annualVehicleTaxSek" }, requiresReplacement: false, alreadyApplied: false },
    energySources: [{ key: "listing-petrol", value: { key: "listing-petrol", fuel: "petrol", unit: "litre", consumptionPer100Kilometres: { single: n("5.123456789") },
      consumptionLabel: "NEDC", fuelSource: { ...source(), field: "fuelTypes" }, consumptionSource: { ...source(), field: "energyConsumptions" } },
      source: { ...source(), field: "fuelTypes" }, requiresReplacement: false, alreadyApplied: false }],
    facts: unknownFacts(), factTargets: [{ field: "purchasePriceSek", requiresReplacement: false, alreadyApplied: false }, { field: "towBar", requiresReplacement: false, alreadyApplied: false }], warnings: [] };
}
describe("shared listing reuse application", () => {
  it("fills exact prices, zero tax, labelled consumption and facts without confirming or inventing costs", () => {
    const result = applyListingReuse(empty(), {}, preview());
    expect(result.cost.priceSek?.text).toBe("10000.123456789");
    expect(result.cost.tax?.items?.[0].amountSek?.single?.text).toBe("0");
    expect(result.cost.energySources?.[0].consumptionLabel).toBe("NEDC");
    expect(result.cost.energySources?.[0].consumptionPer100Kilometres?.single?.text).toBe("5.123456789");
    expect(result.facts.edits).toEqual({ purchasePriceSek: { kind: "listing" }, towBar: { kind: "listing" } });
    expect(result.cost.insurance).toBeUndefined();
    expect(result.cost.residual).toBeUndefined();
  });
  it("preserves occupied zero, false and deliberately empty collections", () => {
    const p = preview();
    p.purchasePrice!.requiresReplacement = true; p.annualTax!.requiresReplacement = true;
    p.energySources[0].requiresReplacement = true;
    p.factTargets.forEach(f => { f.requiresReplacement = true; });
    const cost = { ...empty(), priceSek: n(0), tax: { isIncluded: false, items: [] }, energySources: [] };
    const facts = { edits: { towBar: { kind: "editManual" as const, manual: { value: false } } } };
    const result = applyListingReuse(cost, facts, p);
    expect(stringifyExact(result.cost)).toBe(stringifyExact(cost));
    expect(result.facts.edits).toEqual(facts.edits);
  });
  it("requires a separate replacement selection and does not duplicate collection keys", () => {
    const p = preview(); p.purchasePrice!.requiresReplacement = true;
    expect(applyListingReuse({ ...empty(), priceSek: n(0) }, {}, p, { selected: ["price"] }).cost.priceSek?.text).toBe("0");
    expect(applyListingReuse({ ...empty(), priceSek: n(0) }, {}, p, { selected: ["price"], replace: ["price"] }).cost.priceSek?.text).toBe("10000.123456789");
    const first = applyListingReuse(empty(), {}, p);
    const again = applyListingReuse(first.cost, first.facts, p);
    expect(again.cost.energySources).toHaveLength(1); expect(again.cost.tax?.items).toHaveLength(1);
  });
  it("binds only new typed cost sources to adoption without changing original labels or old sources", () => {
    const cost = applyListingReuse(empty(), {}, preview()).cost;
    cost.priceSource!.listingVersion = n(7);
    const bound = bindReuseSources(cost, n(12));
    expect(bound.priceSource?.listingVersion?.text).toBe("7");
    expect(bound.energySources?.[0].consumptionSource?.listingVersion?.text).toBe("12");
    expect(bound.tax?.items?.[0].listingSource?.listingVersion?.text).toBe("12");
    expect(cost.energySources?.[0].consumptionSource?.listingVersion).toBeNull();
    expect(bound.energySources?.[0].consumptionLabel).toBe("NEDC");
  });
});

describe("listing changes after automatic prefill", () => {
  it("preserves zero and cleared collections, never filling them back", () => {
    const cost = { ...empty(), priceSek: n(0), energySources: [], tax: { isIncluded: false, items: [] } };
    const result = reconcileReuseSources(cost, {}, preview());
    expect(result.cost.priceSek?.text).toBe("0");
    expect(result.cost.energySources).toEqual([]);
    expect(result.cost.tax?.items).toEqual([]);
  });
  it("drops unsupported source claims while keeping chosen amounts and unconfirmed coordinated price facts", () => {
    const initial = applyListingReuse(empty(), {}, preview());
    const next = preview(); next.purchasePrice!.value = n(50000);
    next.energySources[0].value.consumptionPer100Kilometres = { single: n(9) };
    const result = reconcileReuseSources(initial.cost, initial.facts, next);
    expect(result.cost.priceSek?.text).toBe("10000.123456789");
    expect(result.cost.priceSource).toBeNull();
    expect(result.facts.edits?.purchasePriceSek).toEqual({ kind: "editManual", manual: { value: n("10000.123456789") } });
    expect(result.cost.energySources![0].consumptionSource).toBeNull();
    expect(result.cost.energySources![0].fuelSource).toEqual(initial.cost.energySources![0].fuelSource);
    expect(result.cost.energySources![0].consumptionLabel).toBe("NEDC");
    expect(result.cost.tax).toEqual(initial.cost.tax);
  });
});
