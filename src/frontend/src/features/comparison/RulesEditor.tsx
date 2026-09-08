import { Button } from "@/components/ui/button";
import { panelClass } from "@/features/household/Fields";
import {
  n,
  formatNumeric,
  shiftDecimal,
  type Numeric,
} from "@/features/household/numbers";
import type { FormErrors } from "@/features/household/form-model";
import type { Rules, Schema } from "./api";
import {
  criteria,
  evidenceOptions,
  signalOptions,
  type Criterion,
} from "./catalogue";
import { Check, NumberField, SelectField, TextField } from "./controls";

type Choice = Schema<"ComparisonChoice">;
export function RulesSummary({ value }: { value: Rules }) {
  const number = (v: Numeric | null | undefined, c: Criterion | undefined) =>
    v == null
      ? "saknas"
      : formatNumeric(
          c?.kind === "distance" ? n(shiftDecimal(v.text, -1)) : v,
          3,
        );
  const choices = (
    values: Choice[] | null | undefined,
    c: Criterion | undefined,
  ) =>
    (values ?? [])
      .map((v) => {
        const value = c?.choiceKey ? v[c.choiceKey] : v.text;
        return (
          c?.options?.find(([key]) => key === String(value))?.[1] ??
          String(value ?? "saknas")
        );
      })
      .join(", ") || "inget urval";
  return (
    <div className="space-y-3">
      {!(
        value.hardRules?.length ||
        value.preferences?.length ||
        value.signals?.length
      ) && <p>Inga krav, prioriteringar eller signaler.</p>}
      {value.hardRules?.map((r) => {
        const c = criteria.find((c) => c.key === r.criterionKey);
        return (
          <p key={r.criterionKey}>
            {c?.label} · {r.enabled === false ? "Avstängt krav" : "Hårt krav"} ·{" "}
            {evidenceOptions.find(([key]) => key === r.minimumEvidence)?.[1]} ·{" "}
            {c?.kind === "budget"
              ? "Inom angiven budget"
              : c?.kind === "choice"
                ? choices(r.allowedValues, c)
                : `Gränser: ${number(r.minimum, c)} till ${number(r.maximum, c)}`}
          </p>
        );
      })}
      {value.preferences?.map((r) => {
        const c = criteria.find((c) => c.key === r.criterionKey);
        return (
          <p key={r.criterionKey}>
            {c?.label} · Vikt {r.weight.text} ·{" "}
            {evidenceOptions.find(([key]) => key === r.minimumEvidence)?.[1]} ·{" "}
            {c?.kind === "choice"
              ? choices(r.preferredValues, c)
              : `0 poäng vid ${number(r.zeroPoint, c)}, 100 poäng vid ${number(r.fullPoint, c)}`}
          </p>
        );
      })}
      {value.signals?.map((s) => (
        <p key={s.key}>
          Signal: {signalOptions.find(([key]) => key === s.key)?.[1]}
          {s.shortInspectionDays && ` · Daggräns ${s.shortInspectionDays.text}`}
        </p>
      ))}
    </div>
  );
}
function Choices({
  criterion,
  value,
  onChange,
  path,
}: {
  criterion: Criterion;
  value?: Choice[] | null;
  onChange: (value: Choice[]) => void;
  path: string;
}) {
  const key = criterion.choiceKey!;
  if (!criterion.options)
    return (
      <TextField
        label="Önskade värden, separerade med semikolon"
        path={path}
        value={(value ?? []).map((v) => v.text).join(";")}
        onChange={(text) =>
          onChange(text === "" ? [] : text.split(";").map((text) => ({ text })))
        }
      />
    );
  return (
    <fieldset data-field-path={path} tabIndex={-1}>
      <legend className="text-sm font-medium">Välj värden</legend>
      {criterion.options.map(([option, label]) => (
        <Check
          key={option}
          checked={(value ?? []).some((v) => String(v[key]) === option)}
          onChange={(checked) =>
            onChange(
              checked
                ? [
                    ...(criterion.operator === "equals" ? [] : (value ?? [])),
                    { [key]: key === "boolean" ? option === "true" : option },
                  ]
                : (value ?? []).filter((v) => String(v[key]) !== option),
            )
          }
        >
          {label}
        </Check>
      ))}
    </fieldset>
  );
}
export function RulesEditor({
  value,
  onChange,
  errors,
}: {
  value: Rules;
  onChange: (value: Rules) => void;
  errors: FormErrors;
}) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-300">
        Samma krav och vikter gäller alla bilar. Vikt 0 stänger av en
        prioritering. Saknade eller svagt verifierade uppgifter behåller sitt
        möjliga poängintervall.
      </p>
      {criteria.map((c) => {
        const hardIndex = (value.hardRules ?? []).findIndex(
          (r) => r.criterionKey === c.key,
        );
        const preferenceIndex = (value.preferences ?? []).findIndex(
          (r) => r.criterionKey === c.key,
        );
        const hard = value.hardRules?.[hardIndex];
        const pref = value.preferences?.[preferenceIndex];
        const hardPath = `rules.hardRules[${hardIndex}]`;
        const prefPath = `rules.preferences[${preferenceIndex}]`;
        const updateHard = (patch: Partial<Schema<"HardRuleInput">>) =>
          onChange({
            ...value,
            hardRules: value.hardRules?.map((r, i) =>
              i === hardIndex ? { ...r, ...patch } : r,
            ),
          });
        const updatePref = (patch: Partial<Schema<"PreferenceInput">>) =>
          onChange({
            ...value,
            preferences: value.preferences?.map((r, i) =>
              i === preferenceIndex ? { ...r, ...patch } : r,
            ),
          });
        return (
          <details key={c.key} className={panelClass} data-criterion={c.key}>
            <summary className="cursor-pointer font-semibold">
              {c.label}
              {hard ? " · Krav" : ""}
              {pref ? ` · Vikt ${pref.weight.text}` : ""}
            </summary>
            <div className="mt-3 space-y-4">
              {!hard ? (
                <Button
                  variant="secondary"
                  onClick={() =>
                    onChange({
                      ...value,
                      hardRules: [
                        ...(value.hardRules ?? []),
                        {
                          criterionKey: c.key,
                          operator: c.operator,
                          minimumEvidence: "" as Schema<"EvidenceRequirement">,
                          enabled: true,
                        },
                      ],
                    })
                  }
                >
                  Lägg till krav
                </Button>
              ) : (
                <fieldset className="space-y-3">
                  <legend className="font-medium">Hårt krav</legend>
                  <Check
                    checked={hard.enabled !== false}
                    onChange={(enabled) => updateHard({ enabled })}
                  >
                    Aktivt krav
                  </Check>
                  <SelectField
                    label="Kravets verifiering"
                    path={`${hardPath}.minimumEvidence`}
                    value={hard.minimumEvidence}
                    empty
                    options={evidenceOptions}
                    errors={errors}
                    onChange={(minimumEvidence) =>
                      updateHard({
                        minimumEvidence:
                          minimumEvidence as Schema<"EvidenceRequirement">,
                      })
                    }
                  />
                  {c.kind === "choice" ? (
                    <Choices
                      criterion={c}
                      value={hard.allowedValues}
                      path={`${hardPath}.allowedValues`}
                      onChange={(allowedValues) =>
                        updateHard({ allowedValues })
                      }
                    />
                  ) : (
                    c.kind !== "budget" && (
                      <div className="grid gap-3 sm:grid-cols-2">
                        <NumberField
                          label={
                            c.kind === "date"
                              ? "Minst antal återstående dagar"
                              : "Lägsta tillåtna värde"
                          }
                          path={`${hardPath}.minimum`}
                          value={hard.minimum}
                          distance={c.kind === "distance"}
                          errors={errors}
                          onChange={(minimum) => updateHard({ minimum })}
                        />
                        {c.kind !== "date" && (
                          <NumberField
                            label="Högsta tillåtna värde"
                            path={`${hardPath}.maximum`}
                            value={hard.maximum}
                            distance={c.kind === "distance"}
                            errors={errors}
                            onChange={(maximum) => updateHard({ maximum })}
                          />
                        )}
                      </div>
                    )
                  )}
                  <Button
                    variant="secondary"
                    onClick={() =>
                      onChange({
                        ...value,
                        hardRules: value.hardRules?.filter(
                          (r) => r.criterionKey !== c.key,
                        ),
                      })
                    }
                  >
                    Ta bort krav
                  </Button>
                </fieldset>
              )}
              {c.kind !== "budget" &&
                (!pref ? (
                  <Button
                    variant="secondary"
                    onClick={() =>
                      onChange({
                        ...value,
                        preferences: [
                          ...(value.preferences ?? []),
                          {
                            criterionKey: c.key,
                            weight: n(0),
                            minimumEvidence:
                              "" as Schema<"EvidenceRequirement">,
                          },
                        ],
                      })
                    }
                  >
                    Lägg till prioritering
                  </Button>
                ) : (
                  <fieldset className="space-y-3">
                    <legend className="font-medium">Prioritering</legend>
                    <NumberField
                      label="Vikt (0–5)"
                      path={`${prefPath}.weight`}
                      value={pref.weight}
                      errors={errors}
                      onChange={(weight) =>
                        updatePref({ weight: weight ?? n("") })
                      }
                    />
                    <SelectField
                      label="Prioriteringens verifiering"
                      path={`${prefPath}.minimumEvidence`}
                      value={pref.minimumEvidence}
                      empty
                      options={evidenceOptions}
                      errors={errors}
                      onChange={(minimumEvidence) =>
                        updatePref({
                          minimumEvidence:
                            minimumEvidence as Schema<"EvidenceRequirement">,
                        })
                      }
                    />
                    {c.kind === "choice" ? (
                      <Choices
                        criterion={c}
                        value={pref.preferredValues}
                        path={`${prefPath}.preferredValues`}
                        onChange={(preferredValues) =>
                          updatePref({ preferredValues })
                        }
                      />
                    ) : (
                      <div className="grid gap-3 sm:grid-cols-2">
                        {(["zeroPoint", "fullPoint"] as const).map((key) => (
                          <NumberField
                            key={key}
                            label={
                              key === "zeroPoint"
                                ? "Värde som ger 0 poäng"
                                : "Värde som ger 100 poäng"
                            }
                            path={`${prefPath}.${key}`}
                            value={pref[key] as Numeric | null}
                            distance={c.kind === "distance"}
                            errors={errors}
                            onChange={(number) => updatePref({ [key]: number })}
                          />
                        ))}
                      </div>
                    )}
                    <Button
                      variant="secondary"
                      onClick={() =>
                        onChange({
                          ...value,
                          preferences: value.preferences?.filter(
                            (r) => r.criterionKey !== c.key,
                          ),
                        })
                      }
                    >
                      Ta bort prioritering
                    </Button>
                  </fieldset>
                ))}
            </div>
          </details>
        );
      })}
      <fieldset className={panelClass}>
        <legend>Förklarande signaler</legend>
        <p className="text-sm text-slate-400">
          Signaler ger förklaringar och ändrar aldrig poäng eller hårda krav.
        </p>
        {signalOptions.map(([key, label]) => {
          const signal = value.signals?.find((s) => s.key === key);
          return (
            <div key={key}>
              <Check
                checked={!!signal}
                onChange={(checked) =>
                  onChange({
                    ...value,
                    signals: checked
                      ? [...(value.signals ?? []), { key }]
                      : value.signals?.filter((s) => s.key !== key),
                  })
                }
              >
                {label}
              </Check>
              {key === "inspectionValidity" && signal && (
                <NumberField
                  label="Kortare besiktningsgiltighet än (dagar)"
                  path={`rules.signals[${value.signals!.indexOf(signal)}].shortInspectionDays`}
                  value={signal.shortInspectionDays}
                  errors={errors}
                  onChange={(shortInspectionDays) =>
                    onChange({
                      ...value,
                      signals: value.signals?.map((s) =>
                        s.key === key ? { ...s, shortInspectionDays } : s,
                      ),
                    })
                  }
                />
              )}
            </div>
          );
        })}
      </fieldset>
    </div>
  );
}
