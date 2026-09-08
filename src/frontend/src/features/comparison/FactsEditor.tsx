import { Button } from "@/components/ui/button";
import { panelClass } from "@/features/household/Fields";
import {
  formatNumeric,
  n,
  shiftDecimal,
  type Numeric,
} from "@/features/household/numbers";
import type { FormErrors } from "@/features/household/form-model";
import type { FactEdits, FactWrite, Schema } from "./api";
import {
  criteria,
  evidenceOptions,
  factStateLabels,
  labelFor,
  type FactKey,
} from "./catalogue";
import { Check, NumberField, SelectField, TextField } from "./controls";

export interface EditableAction {
  kind: Schema<"FactEditKind">;
  manual?: { value: unknown; observedAt?: string | null };
  observations?: {
    kind: Schema<"FactSelectionKind">;
    observationIndex?: Numeric;
    manual?: { value: unknown; observedAt?: string | null };
  }[];
}
export interface VisibleFact {
  state: Schema<"VehicleFactState">;
  observations: {
    value: unknown;
    evidence: Schema<"ComparisonEvidence">;
    sourceListingVersion: Numeric | null;
  }[];
}
function displayedValue(value: unknown, key?: string): string {
  if (value === null || value === undefined) return "Okänt";
  if (typeof value === "boolean") return value ? "Ja" : "Nej";
  if (typeof value === "object" && "text" in value) {
    const distance =
      key === "odometerKilometres" || key === "lastServiceOdometerKilometres";
    return formatNumeric(
      distance
        ? n(shiftDecimal((value as Numeric).text, -1))
        : (value as Numeric),
      distance ? 3 : 2,
    );
  }
  if (Array.isArray(value))
    return value.length
      ? value.map((v) => displayedValue(v, key)).join(", ")
      : "Bekräftad tom samling";
  return (
    criteria
      .find((c) => c.key === key)
      ?.options?.find(([k]) => k === value)?.[1] ?? String(value)
  );
}
export function Evidence({
  evidence,
  version,
}: {
  evidence: Schema<"ComparisonEvidence">;
  version?: Numeric | null;
}) {
  const origin =
    {
      listing: "Annons",
      user: "Användare",
      registry: "Register",
      unknown: "Okänd källa",
    }[evidence.origin] ?? evidence.origin;
  const level =
    evidence.verification === "unverified"
      ? "Obekräftat"
      : (evidenceOptions.find(([key]) => key === evidence.verification)?.[1] ??
        evidence.verification);
  return (
    <span className="block text-xs text-slate-400">
      {origin} · {level}
      {` · ${evidence.extractionMethod === "ai" ? "AI-extraherat" : "Manuellt underlag"}`}
      {version != null && ` · Annonsversion ${version.text}`}
      {evidence.sourceUrl && (
        <>
          {" "}
          ·{" "}
          <a
            className="text-cyan-300 underline"
            href={evidence.sourceUrl}
            target="_blank"
            rel="noreferrer"
          >
            Källa
          </a>
        </>
      )}
      {evidence.observedAt && ` · Observerat ${evidence.observedAt}`}
      {evidence.confirmedAt && ` · Bekräftat ${evidence.confirmedAt}`}
    </span>
  );
}
function ValueField({
  field,
  value,
  onChange,
  path,
  errors,
}: {
  field: string;
  value: unknown;
  onChange: (value: unknown) => void;
  path: string;
  errors: FormErrors;
}) {
  const criterion = criteria.find((c) => c.key === field);
  if (
    criterion?.kind === "number" ||
    criterion?.kind === "distance" ||
    field === "lastServiceOdometerKilometres"
  )
    return (
      <NumberField
        label="Värde"
        path={path}
        value={value as Numeric | null}
        distance={
          criterion?.kind === "distance" ||
          field === "lastServiceOdometerKilometres"
        }
        errors={errors}
        onChange={onChange}
      />
    );
  if (field === "fuelTypes")
    return (
      <fieldset tabIndex={-1} data-field-path={path}>
        <legend>Drivmedel – tomt urval är en angiven tom samling</legend>
        {criterion?.options?.map(([key, label]) => (
          <Check
            key={key}
            checked={Array.isArray(value) && value.includes(key)}
            onChange={(checked) =>
              onChange(
                checked
                  ? [...(Array.isArray(value) ? value : []), key]
                  : (Array.isArray(value) ? value : []).filter(
                      (v) => v !== key,
                    ),
              )
            }
          >
            {label}
          </Check>
        ))}
      </fieldset>
    );
  if (criterion?.options)
    return (
      <SelectField
        label="Värde"
        path={path}
        value={value == null ? "" : String(value)}
        options={criterion.options}
        empty
        errors={errors}
        onChange={(text) =>
          onChange(
            text === "" ? null : field === "towBar" ? text === "true" : text,
          )
        }
      />
    );
  return (
    <TextField
      label={
        field === "inspectionValidThrough"
          ? "Besiktningen giltig till och med"
          : "Värde"
      }
      path={path}
      value={value == null ? "" : String(value)}
      type={
        field === "inspectionValidThrough" || field === "lastServiceDate"
          ? "date"
          : "text"
      }
      errors={errors}
      onChange={(text) => onChange(text === "" ? null : text)}
    />
  );
}
function FactRow({
  field,
  fact,
  proposal,
  action,
  onChange,
  path,
  manualMode,
  errors,
}: {
  field: string;
  fact?: VisibleFact;
  proposal?: VisibleFact;
  action?: EditableAction;
  onChange: (action?: EditableAction) => void;
  path: string;
  manualMode: boolean;
  errors: FormErrors;
}) {
  const kind = action?.kind ?? "preserve";
  const known = fact?.state === "known" ? fact.observations[0]?.value : null;
  const options: [string, string][] = [
    ["preserve", "Behåll"],
    ["unknown", "Ange okänt"],
    ["notApplicable", "Ange ej tillämpligt"],
  ];
  if (fact?.state === "conflicting")
    options.push([
      manualMode ? "manual" : "resolve",
      "Lös konflikten uttryckligen",
    ]);
  else options.push(["manual", "Ange manuellt och bekräfta värdet"]);
  if (!manualMode && proposal?.state === "known")
    options.push(["listing", "Hämta annonsvärdet"]);
  options.push(["conflict", "Ange motstridiga värden"]);
  return (
    <details className={panelClass} data-fact={field}>
      <summary className="cursor-pointer font-medium">
        {labelFor(field)} · {fact ? factStateLabels[fact.state] : "Okänt"}
        {kind !== "preserve" ? " · Osparat val" : ""}
      </summary>
      <div className="mt-3 space-y-3">
        {fact?.observations.map((o, i) => (
          <div key={i}>
            <p>{displayedValue(o.value, field)}</p>
            <Evidence evidence={o.evidence} version={o.sourceListingVersion} />
          </div>
        ))}
        {proposal?.observations.map((o, i) => (
          <div key={i} className="rounded border border-slate-700 p-2">
            <p>Annonsförslag: {displayedValue(o.value, field)}</p>
            <Evidence evidence={o.evidence} version={o.sourceListingVersion} />
          </div>
        ))}
        <SelectField
          label={`Åtgärd för ${labelFor(field)}`}
          path={`${path}.kind`}
          value={kind}
          options={options}
          errors={errors}
          onChange={(text) => {
            const kind = text as EditableAction["kind"];
            onChange(
              kind === "preserve"
                ? undefined
                : kind === "manual" || kind === "resolve"
                  ? {
                      kind,
                      manual: {
                        value: known ?? (field === "fuelTypes" ? [] : null),
                      },
                    }
                  : kind === "conflict"
                    ? {
                        kind,
                        observations: [
                          { kind: "manual", manual: { value: null } },
                          { kind: "manual", manual: { value: null } },
                        ],
                      }
                    : { kind },
            );
          }}
        />
        {(kind === "manual" || kind === "resolve") && (
          <>
            <ValueField
              field={field}
              value={action?.manual?.value}
              path={`${path}.manual.value`}
              errors={errors}
              onChange={(value) =>
                onChange({ kind, manual: { ...action?.manual, value } })
              }
            />
            <TextField
              label="Observationstid (valfri, ISO 8601 med tidszon)"
              path={`${path}.manual.observedAt`}
              value={action?.manual?.observedAt ?? ""}
              onChange={(observedAt) =>
                onChange({
                  ...action!,
                  manual: {
                    value: action?.manual?.value,
                    observedAt: observedAt || null,
                  },
                })
              }
            />
            <p className="text-xs text-slate-400">
              Det manuella värdet blir användarbekräftat när åtgärden används.
              Tidigare verifiering följer inte med; ingen registerverifiering
              skapas.
            </p>
          </>
        )}
        {kind === "conflict" && (
          <fieldset className="space-y-3">
            <legend>Aktuella observationer</legend>
            {action?.observations?.map((observation, index) => {
              const update = (next: typeof observation) =>
                onChange({
                  ...action,
                  observations: action.observations?.map((o, i) =>
                    i === index ? next : o,
                  ),
                });
              return (
                <div
                  className="space-y-2 rounded border border-slate-700 p-3"
                  key={index}
                >
                  <SelectField
                    label={`Källa för observation ${index + 1}`}
                    path={`${path}.observations[${index}].kind`}
                    value={
                      observation.kind === "current"
                        ? `current-${observation.observationIndex?.text}`
                        : observation.kind
                    }
                    options={[
                      ["manual", "Manuell uppgift"],
                      ...(!manualMode && proposal?.state === "known"
                        ? [["listing", "Aktuellt annonsförslag"] as const]
                        : []),
                      ...(!manualMode
                        ? (fact?.observations ?? []).map(
                            (o, i) =>
                              [
                                `current-${i}`,
                                `Befintlig observation ${i + 1}: ${displayedValue(o.value, field)}`,
                              ] as const,
                          )
                        : []),
                    ]}
                    onChange={(kind) =>
                      update(
                        kind.startsWith("current-")
                          ? {
                              kind: "current",
                              observationIndex: n(kind.slice(8)),
                            }
                          : kind === "listing"
                            ? { kind: "listing" }
                            : { kind: "manual", manual: { value: null } },
                      )
                    }
                  />
                  {observation.kind === "manual" && (
                    <ValueField
                      field={field}
                      value={observation.manual?.value}
                      path={`${path}.observations[${index}].manual.value`}
                      errors={errors}
                      onChange={(value) =>
                        update({
                          kind: "manual",
                          manual: { ...observation.manual, value },
                        })
                      }
                    />
                  )}
                  <Button
                    variant="secondary"
                    onClick={() =>
                      onChange({
                        ...action,
                        observations: action.observations?.filter(
                          (_, i) => i !== index,
                        ),
                      })
                    }
                  >
                    Ta bort observation {index + 1}
                  </Button>
                </div>
              );
            })}
            <Button
              variant="secondary"
              onClick={() =>
                onChange({
                  ...action,
                  kind: "conflict",
                  observations: [
                    ...(action?.observations ?? []),
                    { kind: "manual", manual: { value: null } },
                  ],
                })
              }
            >
              Lägg till observation
            </Button>
          </fieldset>
        )}
      </div>
    </details>
  );
}
export function FactsEditor({
  input,
  value,
  proposal,
  listingVersion,
  manualMode,
  onChange,
  prefix,
  errors,
  readOnly = false,
}: {
  input: FactWrite;
  value?: Schema<"ComparisonFactSet"> | null;
  proposal?: Schema<"ComparisonFactSet"> | null;
  listingVersion?: Numeric | null;
  manualMode: boolean;
  onChange: (input: FactWrite) => void;
  prefix: string;
  errors: FormErrors;
  readOnly?: boolean;
}) {
  const fields: FactKey[] = [
    ...criteria.flatMap((c) => (c.fact ? [c.fact] : [])),
    "lastServiceDate",
    "lastServiceOdometerKilometres",
    "serviceNotes",
  ];
  const change = (edits: FactEdits) => {
    const needsListing = Object.values(edits)
      .flat()
      .some(
        (e) =>
          e?.kind === "listing" ||
          (e?.kind === "conflict" &&
            e.observations?.some((o) => o.kind === "listing")),
      );
    onChange({
      ...input,
      edits,
      expectedListingVersion:
        !manualMode && (needsListing || input.reviewCurrentListing)
          ? listingVersion
          : undefined,
    });
  };
  const notes = input.edits?.conditionNotes ?? undefined;
  if (readOnly)
    return (
      <div className="space-y-3">
        {[...fields, "conditionNotes" as const].map((field) => (
          <div key={field}>
            <p className="font-semibold">{labelFor(field)}</p>
            {(field === "conditionNotes"
              ? (value?.conditionNotes ?? [])
              : [value?.facts[field]]
            ).map((fact, i) => (
              <div key={i}>
                <p>{fact ? factStateLabels[fact.state] : "Okänt"}</p>
                {fact?.observations.map((o, j) => (
                  <p key={j}>
                    {displayedValue(o.value, field)}
                    <Evidence
                      evidence={o.evidence}
                      version={o.sourceListingVersion}
                    />
                  </p>
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>
    );
  return (
    <div className="space-y-3">
      {fields.map((field) => (
        <FactRow
          key={field}
          field={field}
          fact={value?.facts[field]}
          proposal={proposal?.facts[field]}
          action={input.edits?.[field] as EditableAction | undefined}
          manualMode={manualMode}
          path={`${prefix}.edits.${field}`}
          errors={errors}
          onChange={(action) => change({ ...input.edits, [field]: action })}
        />
      ))}
      <details className={panelClass}>
        <summary className="cursor-pointer font-medium">
          Skick- och reparationsuppgifter
        </summary>
        <p className="my-3 text-sm text-slate-400">
          Fritext visas med källa. Den blir inte automatiskt en diagnos eller
          poäng.
        </p>
        {notes === undefined ? (
          <Button
            variant="secondary"
            onClick={() =>
              change({
                ...input.edits,
                conditionNotes: (value?.conditionNotes ?? []).map(() => ({
                  kind: "preserve",
                })),
              })
            }
          >
            Redigera skickuppgifter
          </Button>
        ) : (
          <>
            {notes.length === 0 && (
              <p>Uttryckligen tom samling skickuppgifter.</p>
            )}
            {notes.map((action, index) => (
              <FactRow
                key={index}
                field="conditionNotes"
                fact={value?.conditionNotes?.[index]}
                proposal={proposal?.conditionNotes?.[index]}
                action={action as EditableAction}
                manualMode={manualMode}
                path={`${prefix}.edits.conditionNotes[${index}]`}
                errors={errors}
                onChange={(next) =>
                  change({
                    ...input.edits,
                    conditionNotes: notes.map((v, i) =>
                      i === index
                        ? ((next ?? { kind: "preserve" }) as typeof v)
                        : v,
                    ),
                  })
                }
              />
            ))}
            <div className="mt-3 flex flex-wrap gap-3">
              <Button
                variant="secondary"
                disabled={notes.length >= 10}
                onClick={() =>
                  change({
                    ...input.edits,
                    conditionNotes: [
                      ...notes,
                      { kind: "manual", manual: { value: "" } },
                    ],
                  })
                }
              >
                Lägg till skickuppgift
              </Button>
              <Button
                variant="secondary"
                onClick={() => change({ ...input.edits, conditionNotes: [] })}
              >
                Töm skickuppgifter
              </Button>
              <Button
                variant="secondary"
                onClick={() =>
                  change({ ...input.edits, conditionNotes: undefined })
                }
              >
                Behåll sparad samling
              </Button>
            </div>
          </>
        )}
        {notes === undefined &&
          value?.conditionNotes?.map((fact, i) => (
            <div key={i}>
              {fact.observations.map((o, j) => (
                <p key={j}>
                  {displayedValue(o.value)}
                  <Evidence
                    evidence={o.evidence}
                    version={o.sourceListingVersion}
                  />
                </p>
              ))}
            </div>
          ))}
      </details>
      {!manualMode && listingVersion != null && (
        <Check
          checked={input.reviewCurrentListing === true}
          onChange={(reviewCurrentListing) =>
            onChange({
              ...input,
              reviewCurrentListing,
              expectedListingVersion: listingVersion,
            })
          }
        >
          Jag har granskat aktuell annonsversion {listingVersion.text}. Tidigare
          observationer behåller sina ursprungliga källversioner.
        </Check>
      )}
    </div>
  );
}
