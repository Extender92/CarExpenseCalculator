import type { Result } from "./api";

/** Bind indexed cost rows to their stable keys before the editor can reorder them. */
export function keyedPath(path: string, root: unknown): string {
  let current: unknown = root;
  return path.replace(
    /([^.[\]]+)|\[(\d+)\]/g,
    (part, property: string | undefined, index: string | undefined) => {
      if (index !== undefined) {
        current = Array.isArray(current) ? current[Number(index)] : undefined;
        const key =
          current && typeof current === "object" && "key" in current
            ? current.key
            : undefined;
        return typeof key === "string"
          ? `[key:${encodeURIComponent(key)}]`
          : part;
      }
      current =
        current && typeof current === "object"
          ? (current as Record<string, unknown>)[property!]
          : undefined;
      return part;
    },
  );
}
export function indexedPath(path: string, root: unknown): string {
  let current: unknown = root;
  return path.replace(
    /([^.[\]]+)|\[(\d+|key:[^\]]+)\]/g,
    (part, property: string | undefined, index: string | undefined) => {
      if (index !== undefined) {
        const at = index.startsWith("key:")
          ? Array.isArray(current)
            ? current.findIndex(
                (v) => v?.key === decodeURIComponent(index.slice(4)),
              )
            : -1
          : Number(index);
        current = Array.isArray(current) ? current[at] : undefined;
        return `[${at}]`;
      }
      current =
        current && typeof current === "object"
          ? (current as Record<string, unknown>)[property!]
          : undefined;
      return part;
    },
  );
}
export function focusField(path: string, input?: unknown) {
  if (input) path = indexedPath(path, { input });
  const elements = [
    ...document.querySelectorAll<HTMLElement>("[data-field-path]"),
  ];
  const element =
    elements.find((e) => e.dataset.fieldPath === path) ??
    elements.find(
      (e) =>
        e.dataset.fieldPath?.startsWith(`${path}.`) ||
        e.dataset.fieldPath?.startsWith(`${path}[`),
    );
  let parent = element?.parentElement;
  while (parent) {
    if (parent instanceof HTMLDetailsElement) parent.open = true;
    parent = parent.parentElement;
  }
  element?.focus();
  element?.scrollIntoView?.({ block: "center" });
}

export function economicLink(
  vehicleId: string,
  manual: boolean,
  field = "input.priceSek",
) {
  const normalized = field
    .replace(/^vehicles\[\d+\]\./, "")
    .replace(/^costInput\./, "input.");
  const section = normalized.startsWith("profile.")
    ? "profile"
    : (normalized.split(".")[1]?.replace(/\[.*$/, "") ?? "priceSek");
  const params = new URLSearchParams({
    [manual ? "comparisonCandidateId" : "vehicleId"]: vehicleId,
    section,
    field: normalized,
    returnTo: "comparison",
  });
  return `/manual?${params}`;
}
export function errorTarget(result: Result, path: string): string {
  if (path.startsWith("profile.")) return path;
  if (path === "zeroDistance") return "profile.annualDistanceKilometres";
  if (path === "residualHorizonMismatch") return "input.residual.periodMonths";
  if (path === "leaseHorizonMismatch") return "input.lease.termMonths";
  const normalized = path
    .replace(/^vehicles\[\d+\]\./, "")
    .replace(/^input\./, "");
  // Result missing lists sometimes contain category codes rather than paths.
  if (normalized === "legacyReview" || normalized.startsWith("legacy"))
    return "review";
  if (normalized === "financing") return "profile.loanTerms";
  if (normalized === "energy") return "input.energySources";
  if (normalized === "repairAllowance")
    return "input.additionalRepairAllowancePerMonthSek";
  if (normalized === "ownership" && !result.effectiveCostInput)
    return "input.priceSek";
  return keyedPath(`input.${normalized}`, { input: result.effectiveCostInput });
}
