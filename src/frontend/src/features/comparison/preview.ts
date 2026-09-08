import {
  Numeric,
  canonicalNumber,
  stringifyExact,
} from "@/features/household/numbers";
import { normalizeRegistrationNumber } from "@/features/manual-calculator/saved-scenarios";
import type { FormErrors } from "@/features/household/form-model";
import {
  ComparisonApiError,
  type ComparisonRequest,
  type ComparisonResponse,
  type Rules,
  type ComparisonError,
  type FactWrite,
} from "./api";
import { reasonText } from "./catalogue";
import { keyedPath } from "./navigation";

export function numberErrors(value: unknown, path = ""): FormErrors {
  if (value instanceof Numeric) {
    try {
      canonicalNumber(value.text);
      return {};
    } catch (error) {
      return { [path]: [(error as Error).message] };
    }
  }
  if (Array.isArray(value))
    return Object.assign(
      {},
      ...value.map((v, i) => numberErrors(v, `${path}[${i}]`)),
    );
  if (value && typeof value === "object")
    return Object.assign(
      {},
      ...Object.entries(value).map(([key, v]) =>
        numberErrors(v, path ? `${path}.${key}` : key),
      ),
    );
  return {};
}
export function ruleErrors(rules: Rules): FormErrors {
  const errors = numberErrors(rules, "rules");
  for (const group of ["hardRules", "preferences"] as const) {
    const seen = new Set<string>();
    for (const [index, rule] of (rules[group] ?? []).entries()) {
      const path = `rules.${group}[${index}]`;
      if (seen.has(rule.criterionKey))
        errors[path] = ["Kriteriet får bara anges en gång i denna samling."];
      seen.add(rule.criterionKey);
      if (!rule.minimumEvidence)
        errors[`${path}.minimumEvidence`] = [
          "Välj vilken verifiering som krävs.",
        ];
      if ("weight" in rule) {
        let text = rule.weight?.text;
        try {
          if (text) text = canonicalNumber(text);
        } catch {
          /* numeric error is already collected */
        }
        if (!text || !/^[0-5]$/.test(text))
          errors[`${path}.weight`] = ["Ange en heltalsvikt mellan 0 och 5."];
      }
    }
  }
  return errors;
}

export function factErrors(input: FactWrite, prefix: string): FormErrors {
  const errors = numberErrors(input, prefix);
  for (const [field, value] of Object.entries(input.edits ?? {})) {
    for (const [index, edit] of (Array.isArray(value)
      ? value
      : [value]
    ).entries()) {
      if (!edit) continue;
      const path = `${prefix}.edits.${field}${Array.isArray(value) ? `[${index}]` : ""}`;
      if (
        (edit.kind === "manual" || edit.kind === "resolve") &&
        edit.manual?.value == null
      )
        errors[`${path}.manual.value`] = [
          "Ange ett värde eller välj uttryckligen Okänt.",
        ];
      if (edit.kind === "conflict") {
        if (!edit.observations || edit.observations.length < 2)
          errors[`${path}.observations`] = [
            "Ange minst två olika observationer.",
          ];
        edit.observations?.forEach((o, i) => {
          if (o.kind === "manual" && o.manual?.value == null)
            errors[`${path}.observations[${i}].manual.value`] = [
              "Ange observationsvärdet.",
            ];
        });
      }
    }
  }
  return errors;
}

export function validResponse(
  request: ComparisonRequest,
  response: ComparisonResponse,
  expectedCount: number,
): void {
  const invalid = () => {
    throw new ComparisonApiError(503, "invalidResponse");
  };
  try {
    if (
      response.requestId !== request.requestId ||
      response.mode !== request.mode ||
      !/^[\da-f]{8}-(?:[\da-f]{4}-){3}[\da-f]{12}$/i.test(
        response.generationId,
      ) ||
      response.transportVersion.text !== "1" ||
      response.candidateCount.text !== String(expectedCount) ||
      response.activeSensitivityMode !==
        request.profile.activeSensitivityMode ||
      response.baselineToken !== (request.storedBase?.baselineToken ?? null)
    )
      invalid();
    const first = response.views.baseline.candidates;
    const ids = first.map((c) => c.vehicleId);
    if (
      new Set(ids).size !== expectedCount ||
      new Set(first.map((c) => c.registrationNumber)).size !== expectedCount
    )
      invalid();
    if (
      request.mode === "manual" &&
      request.candidates?.some(
        (c, i) =>
          c.vehicleId !== ids[i] ||
          normalizeRegistrationNumber(c.registrationNumber) !==
            first[i].registrationNumber,
      )
    )
      invalid();
    const sameIds = (order: string[]) =>
      order.length === expectedCount &&
      new Set(order).size === expectedCount &&
      order.every((id) => idsSet.has(id));
    const idsSet = new Set(ids);
    for (const mode of ["baseline", "favorable", "cautious"] as const) {
      const view = response.views[mode];
      if (
        view.requestId !== request.requestId ||
        view.mode !== request.mode ||
        view.storageChecked !== (request.mode === "stored") ||
        view.profile.activeSensitivityMode !== mode ||
        view.asOfDate !== request.asOfDate ||
        view.ruleVersion.text !== "1" ||
        view.resultSchemaVersion.text !== "1" ||
        view.calculationVersion.text !== "2" ||
        view.householdResultSchemaVersion.text !== "2" ||
        view.candidates.length !== expectedCount ||
        !sameIds(view.costOrder) ||
        !sameIds(view.scoreOrder)
      )
        invalid();
      view.candidates.forEach((c, i) => {
        if (
          c.vehicleId !== ids[i] ||
          c.registrationNumber !== first[i].registrationNumber ||
          c.costConfirmedAt !== first[i].costConfirmedAt ||
          stringifyExact(c.sourceRevisions) !==
            stringifyExact(first[i].sourceRevisions) ||
          !c.cost?.totals
        )
          invalid();
      });
    }
  } catch {
    invalid();
  }
}

/** Preserve the captured input indexes, then resolve a row key, never a displayed row number. */
export function mappedErrors(
  errors: ComparisonError[],
  request: ComparisonRequest,
): FormErrors {
  const fields: FormErrors = {};
  for (const error of errors) {
    const path = error.path.replace(
      /^(candidates|overrides)\[(\d+)\]/,
      (_match, collection: "candidates" | "overrides", index: string) => {
        const candidate = request[collection]?.[Number(index)];
        return candidate
          ? `vehicle.${candidate.vehicleId}`
          : `${collection}[${index}]`;
      },
    );
    const match = /^vehicle\.([\da-f-]+)\.(.*)/.exec(path);
    const candidate = (request.candidates ?? request.overrides ?? []).find(
      (c) => c.vehicleId === match?.[1],
    );
    const stable =
      candidate && match
        ? `vehicle.${candidate.vehicleId}.${keyedPath(match[2], candidate)}`
        : path;
    (fields[stable] ??= []).push(
      reasonText[error.code] ??
        "Kontrollera uppgiften och dess tillåtna värden.",
    );
  }
  return fields;
}
