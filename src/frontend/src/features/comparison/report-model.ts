import { cloneExact } from "@/features/household/numbers";
import type { ComparisonResponse, Result } from "./api";

export type Immutable<T> = T extends object
  ? { readonly [K in keyof T]: Immutable<T[K]> }
  : T;

/** One tab-local capture, never a persisted report or new calculation request. */
export interface ComparisonReportInput {
  readonly response: Immutable<ComparisonResponse>;
  readonly sort: "cost" | "score";
  readonly capturedAt: string;
  readonly timeZone: string;
}

function freeze<T>(value: T): Immutable<T> {
  if (value && typeof value === "object") {
    Object.values(value).forEach(freeze);
    Object.freeze(value);
  }
  return value as Immutable<T>;
}

export function captureReport(
  response: ComparisonResponse,
  sort: ComparisonReportInput["sort"],
  now = new Date(),
  timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone,
): ComparisonReportInput {
  return freeze({
    response: cloneExact(response),
    sort,
    capturedAt: now.toISOString(),
    timeZone,
  });
}

export function reportRows(report: ComparisonReportInput): Immutable<Result>[] {
  const view = report.response.views[report.response.activeSensitivityMode];
  const byId = new Map(view.candidates.map((c) => [c.vehicleId, c]));
  return (report.sort === "cost" ? view.costOrder : view.scoreOrder).map(
    (id) => byId.get(id)!,
  );
}
