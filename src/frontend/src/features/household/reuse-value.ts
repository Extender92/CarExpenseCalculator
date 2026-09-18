import { criteria } from "@/features/comparison/catalogue";
import { canonicalNumber, shiftDecimal } from "./numbers";

/** Match the comparison field's Swedish label/unit without rounding its value. */
export function listingReuseValue(value: unknown, field: string): string {
  if (value == null) return "Saknas";
  if (typeof value === "boolean") return value ? "Ja" : "Nej";
  if (Array.isArray(value)) return value.length
    ? value.map(item => listingReuseValue(item, field)).join(", ") : "Uttryckligen tom samling";
  if (typeof value === "object" && "text" in value) {
    const text = String(value.text);
    return canonicalNumber(field === "odometerKilometres" ? shiftDecimal(text, -1) : text, false).replace(".", ",");
  }
  return criteria.find(item => item.key === field)?.options?.find(([key]) => key === value)?.[1] ?? String(value);
}
