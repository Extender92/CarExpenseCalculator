import { useId, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import type { ProfileInput, VehicleInput } from "./api";
import {
  costFields,
  leaseFields,
  newCost,
  profileFields,
  vehicleGroups,
  type Field,
  type FormErrors,
} from "./form-model";
import { Numeric, n, numericText, shiftDecimal } from "./numbers";

export const inputClass =
  "w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 focus:border-cyan-400 focus:outline-none focus:ring-1 focus:ring-cyan-400 disabled:opacity-50";
export const panelClass =
  "rounded-xl border border-slate-700 bg-slate-900/40 p-4";

export function Labeled({
  label,
  path,
  errors = {},
  children,
}: {
  label: string;
  path: string;
  errors?: FormErrors;
  children: (id: string, describedBy: string | undefined) => ReactNode;
}) {
  const id = useId();
  const messages = errors[path] ?? [];
  return (
    <div className="space-y-1.5">
      <label className="block text-sm font-medium text-slate-200" htmlFor={id}>
        {label}
      </label>
      {children(id, messages.length ? `${id}-errors` : undefined)}
      {messages.length > 0 && (
        <ul id={`${id}-errors`} className="text-sm text-rose-300">
          {messages.map((message, index) => (
            <li key={index}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function ObjectFields({
  value,
  fields,
  onChange,
  prefix,
  errors = {},
}: {
  value: Record<string, unknown>;
  fields: Field[];
  onChange: (value: Record<string, unknown>) => void;
  prefix: string;
  errors?: FormErrors;
}) {
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {fields.map((field) => (
        <div
          key={field.key}
          className={
            ["array", "object", "category", "sensitivity"].includes(field.kind)
              ? "sm:col-span-2"
              : ""
          }
        >
          <FieldControl
            field={field}
            value={value[field.key]}
            onChange={(entry) => onChange({ ...value, [field.key]: entry })}
            path={`${prefix}.${field.key}`}
            errors={errors}
          />
        </div>
      ))}
    </div>
  );
}

function FieldControl({
  field,
  value,
  onChange,
  path,
  errors,
}: {
  field: Field;
  value: unknown;
  onChange: (value: unknown) => void;
  path: string;
  errors: FormErrors;
}) {
  if (field.kind === "sensitivity") {
    const entry = value as Record<string, unknown> | null | undefined;
    const mode =
      entry == null ? "unknown" : "single" in entry ? "single" : "trio";
    return (
      <fieldset className={panelClass}>
        <legend className="px-1 text-sm font-semibold">{field.label}</legend>
        <Labeled
          label={`Typ av värde för ${field.label}`}
          path={path}
          errors={errors}
        >
          {(id, describedBy) => (
            <select
              id={id}
              aria-describedby={describedBy}
              className={inputClass}
              value={mode}
              onChange={(event) =>
                onChange(
                  event.target.value === "unknown"
                    ? null
                    : event.target.value === "single"
                      ? { single: null }
                      : { favorable: null, baseline: null, cautious: null },
                )
              }
            >
              <option value="unknown">Okänt</option>
              <option value="single">Ett angivet värde</option>
              {!field.singleOnly && (
                <option value="trio">Tre osäkerhetsvärden</option>
              )}
            </select>
          )}
        </Labeled>
        {entry && (
          <div className="mt-3">
            <ObjectFields
              value={entry}
              prefix={path}
              errors={errors}
              onChange={onChange}
              fields={
                mode === "single"
                  ? [
                      {
                        key: "single",
                        label: field.label,
                        kind: "number",
                        required: true,
                      },
                    ]
                  : [
                      {
                        key: "favorable",
                        label: `${field.label} – gynnsamt`,
                        kind: "number",
                        required: true,
                      },
                      {
                        key: "baseline",
                        label: `${field.label} – normalt`,
                        kind: "number",
                        required: true,
                      },
                      {
                        key: "cautious",
                        label: `${field.label} – försiktigt`,
                        kind: "number",
                        required: true,
                      },
                    ]
              }
            />
          </div>
        )}
      </fieldset>
    );
  }
  if (field.kind === "object") {
    const entry = value as Record<string, unknown> | null | undefined;
    return (
      <fieldset className={panelClass}>
        <legend className="px-1 text-sm font-semibold">{field.label}</legend>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={entry != null}
            onChange={(event) =>
              onChange(event.target.checked ? (field.factory?.() ?? {}) : null)
            }
          />
          Ange {field.label.toLocaleLowerCase("sv-SE")}
        </label>
        {entry && (
          <div className="mt-4">
            <ObjectFields
              value={entry}
              fields={field.fields ?? []}
              onChange={onChange}
              prefix={path}
              errors={errors}
            />
          </div>
        )}
      </fieldset>
    );
  }
  if (field.kind === "category" || field.kind === "array") {
    const category = field.kind === "category";
    const entry = value as
      | { isIncluded: boolean; items: Record<string, unknown>[] }
      | null
      | undefined;
    const rows = category
      ? entry?.items
      : (value as Record<string, unknown>[] | null | undefined);
    const mode =
      rows == null
        ? "unknown"
        : category && entry?.isIncluded
          ? "included"
          : rows.length === 0
            ? "empty"
            : "values";
    const setRows = (next: Record<string, unknown>[]) =>
      onChange(
        category
          ? { isIncluded: entry?.isIncluded ?? false, items: next }
          : next,
      );
    const createRow = field.factory ?? newCost;
    return (
      <fieldset className={`${panelClass} space-y-4`}>
        <legend className="px-1 text-sm font-semibold">{field.label}</legend>
        <Labeled
          label={`Uppgifter om ${field.label.toLocaleLowerCase("sv-SE")}`}
          path={path}
          errors={errors}
        >
          {(id, describedBy) => (
            <select
              id={id}
              aria-describedby={describedBy}
              className={inputClass}
              value={mode}
              onChange={(event) => {
                const next = event.target.value;
                if (next === "unknown") onChange(null);
                else if (category)
                  onChange({
                    isIncluded: next === "included",
                    items:
                      next === "empty"
                        ? []
                        : next === "included"
                          ? (rows ?? [])
                          : rows?.length
                            ? rows
                            : [createRow()],
                  });
                else
                  onChange(
                    next === "empty" ? [] : rows?.length ? rows : [createRow()],
                  );
              }}
            >
              {!field.required && <option value="unknown">Okänt</option>}
              <option value="empty">
                Bekräftat inga poster{category ? " (0 kr)" : ""}
              </option>
              <option value="values">Angivna poster</option>
              {category && (
                <option value="included">Ingår, med eventuella tillägg</option>
              )}
            </select>
          )}
        </Labeled>
        {rows?.map((row, index) => (
          <div
            key={typeof row.key === "string" ? row.key : index}
            className="space-y-3 rounded-lg border border-slate-700 p-3"
          >
            <ObjectFields
              value={row}
              fields={
                category
                  ? costFields.map((item) =>
                      ["tax", "insurance"].includes(field.key) &&
                      item.kind === "sensitivity"
                        ? { ...item, singleOnly: true }
                        : item,
                    )
                  : (field.fields ?? [])
              }
              prefix={`${path}${category ? ".items" : ""}[${index}]`}
              errors={errors}
              onChange={(updated) =>
                setRows(rows.map((old, i) => (i === index ? updated : old)))
              }
            />
            <Button
              type="button"
              variant="ghost"
              onClick={() => setRows(rows.filter((_, i) => i !== index))}
            >
              Ta bort {field.label.toLocaleLowerCase("sv-SE")} {index + 1}
            </Button>
          </div>
        ))}
        {rows != null && (
          <Button
            type="button"
            variant="secondary"
            disabled={rows.length >= (field.maximum ?? 50)}
            onClick={() => setRows([...rows, createRow()])}
          >
            Lägg till post i {field.label.toLocaleLowerCase("sv-SE")}
          </Button>
        )}
      </fieldset>
    );
  }
  if (field.kind === "boolean")
    return (
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={value === true}
          onChange={(event) => onChange(event.target.checked)}
        />
        {field.label}
      </label>
    );
  return (
    <Labeled label={field.label} path={path} errors={errors}>
      {(id, describedBy) => (
        <>
          {field.kind === "select" ? (
            <select
              id={id}
              data-field-path={path}
              aria-describedby={describedBy}
              aria-invalid={!!errors[path]}
              className={inputClass}
              value={typeof value === "string" ? value : ""}
              onChange={(event) => onChange(event.target.value || null)}
            >
              {!field.required && <option value="">Okänt</option>}
              {field.options?.map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          ) : field.kind === "month" ? (
            <input
              id={id}
              data-field-path={path}
              aria-describedby={describedBy}
              aria-invalid={!!errors[path]}
              className={inputClass}
              type="month"
              value={monthText(value)}
              onChange={(event) => {
                const [year, month] = event.target.value.split("-");
                onChange(
                  year && month ? { year: n(year), month: n(month) } : null,
                );
              }}
            />
          ) : (
            <input
              id={id}
              data-field-path={path}
              aria-describedby={describedBy}
              aria-invalid={!!errors[path]}
              className={inputClass}
              type="text"
              inputMode={
                field.kind === "text"
                  ? undefined
                  : field.kind === "integer"
                    ? "numeric"
                    : "decimal"
              }
              value={
                field.kind === "text"
                  ? String(value ?? "")
                  : field.kind === "distance"
                    ? distanceText(value)
                    : numericText(value as Numeric | null)
              }
              onChange={(event) => {
                const raw = event.target.value;
                if (field.kind === "text")
                  onChange(raw || (field.required ? "" : null));
                else if (!raw) onChange(null);
                else if (field.kind === "distance") {
                  try {
                    onChange(n(shiftDecimal(raw, 1)));
                  } catch {
                    onChange(n(raw));
                  }
                } else onChange(n(raw));
              }}
            />
          )}
          {field.help && <p className="text-xs text-slate-400">{field.help}</p>}
        </>
      )}
    </Labeled>
  );
}
function monthText(value: unknown) {
  const month = value as { year: Numeric; month: Numeric } | null;
  return month?.year && month.month
    ? `${month.year.text.padStart(4, "0")}-${month.month.text.padStart(2, "0")}`
    : "";
}
function distanceText(value: unknown) {
  if (!(value instanceof Numeric)) return "";
  try {
    return shiftDecimal(value.text, -1);
  } catch {
    return value.text;
  }
}

export function ProfileFields({
  value,
  onChange,
  errors,
}: {
  value: ProfileInput;
  onChange: (value: ProfileInput) => void;
  errors: FormErrors;
}) {
  return (
    <ObjectFields
      value={value as Record<string, unknown>}
      fields={profileFields}
      prefix="profile"
      errors={errors}
      onChange={(next) => onChange(next as ProfileInput)}
    />
  );
}

export function VehicleFields({
  value,
  onChange,
  errors,
}: {
  value: VehicleInput;
  onChange: (value: VehicleInput) => void;
  errors: FormErrors;
}) {
  return (
    <div className="space-y-4">
      <Labeled
        label="Anskaffningsform"
        path="input.acquisitionType"
        errors={errors}
      >
        {(id) => (
          <select
            id={id}
            className={inputClass}
            value={value.acquisitionType ?? "purchase"}
            onChange={(event) =>
              onChange(
                event.target.value === "lease"
                  ? {
                      ...value,
                      acquisitionType: "lease",
                      priceSek: null,
                      residual: null,
                      lease: { energyIncluded: false },
                    }
                  : { ...value, acquisitionType: "purchase", lease: null },
              )
            }
          >
            <option value="purchase">Köp</option>
            <option value="lease">Leasing</option>
          </select>
        )}
      </Labeled>
      {value.acquisitionType === "lease" && (
        <details open className={panelClass}>
          <summary className="cursor-pointer font-semibold">
            Leasingavtal och betalningar
          </summary>
          <div className="mt-4 space-y-4">
            <ObjectFields
              value={(value.lease ?? {}) as Record<string, unknown>}
              fields={leaseFields}
              prefix="input.lease"
              errors={errors}
              onChange={(lease) => onChange({ ...value, lease })}
            />
            <FillLeasePayments value={value} onChange={onChange} />
          </div>
        </details>
      )}
      {vehicleGroups
        .filter((_, index) => value.acquisitionType !== "lease" || index !== 0)
        .map((group, index) => (
          <details key={group.label} open={index === 0} className={panelClass}>
            <summary className="cursor-pointer font-semibold">
              {group.label}
            </summary>
            <div className="mt-4">
              <ObjectFields
                value={value as Record<string, unknown>}
                fields={group.fields}
                prefix="input"
                errors={errors}
                onChange={(next) => onChange(next as VehicleInput)}
              />
            </div>
          </details>
        ))}
    </div>
  );
}

function FillLeasePayments({
  value,
  onChange,
}: {
  value: VehicleInput;
  onChange: (value: VehicleInput) => void;
}) {
  const [from, setFrom] = useState("1");
  const [to, setTo] = useState("");
  const [amount, setAmount] = useState("");
  const [error, setError] = useState("");
  return (
    <fieldset className={panelClass}>
      <legend>Fyll samma leasingavgift för flera månader</legend>
      <div className="grid gap-3 sm:grid-cols-3">
        {[
          { label: "Från månad", value: from, set: setFrom },
          { label: "Till månad", value: to, set: setTo },
          {
            label: "Avgift för varje månad (kr)",
            value: amount,
            set: setAmount,
          },
        ].map((field) => (
          <Labeled key={field.label} label={field.label} path="fill">
            {(id) => (
              <input
                id={id}
                className={inputClass}
                value={field.value}
                onChange={(event) => field.set(event.target.value)}
              />
            )}
          </Labeled>
        ))}
      </div>
      <Button
        type="button"
        className="mt-3"
        variant="secondary"
        onClick={() => {
          if (
            !/^\d+$/.test(from) ||
            !/^\d+$/.test(to) ||
            Number(from) < 1 ||
            Number(to) > 120 ||
            Number(from) > Number(to) ||
            !amount.trim()
          ) {
            setError(
              "Ange ett intervall inom 1–120 och ett uttryckligt belopp.",
            );
            return;
          }
          const existing = value.lease?.monthlyPayments ?? [];
          if (
            existing.some(
              (item) =>
                Number(item.monthOffset.text) >= Number(from) &&
                Number(item.monthOffset.text) <= Number(to),
            ) &&
            !window.confirm(
              "Ersätta befintliga betalningar i det valda intervallet?",
            )
          )
            return;
          const payments = existing.filter(
            (item) =>
              Number(item.monthOffset.text) < Number(from) ||
              Number(item.monthOffset.text) > Number(to),
          );
          for (let month = Number(from); month <= Number(to); month++)
            payments.push({ monthOffset: n(month), amountSek: n(amount) });
          payments.sort(
            (a, b) => Number(a.monthOffset.text) - Number(b.monthOffset.text),
          );
          onChange({
            ...value,
            lease: { ...value.lease, monthlyPayments: payments },
          });
          setError("");
        }}
      >
        Fyll betalningsmånaderna
      </Button>
      {error && (
        <p role="alert" className="mt-2 text-rose-300">
          {error}
        </p>
      )}
    </fieldset>
  );
}
