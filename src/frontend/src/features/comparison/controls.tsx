import { useState, type ReactNode } from "react";
import { inputClass, Labeled } from "@/features/household/Fields";
import type { FormErrors } from "@/features/household/form-model";
import {
  n,
  numericText,
  shiftDecimal,
  type Numeric,
} from "@/features/household/numbers";

export function TextField({
  label,
  path,
  value,
  onChange,
  errors = {},
  type = "text",
}: {
  label: string;
  path: string;
  value: string;
  onChange: (value: string) => void;
  errors?: FormErrors;
  type?: string;
}) {
  return (
    <Labeled label={label} path={path} errors={errors}>
      {(id, describedBy) => (
        <input
          id={id}
          className={inputClass}
          type={type}
          data-field-path={path}
          value={value}
          aria-describedby={describedBy}
          aria-invalid={!!errors[path]}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </Labeled>
  );
}
export function NumberField({
  value,
  onChange,
  distance = false,
  ...props
}: {
  label: string;
  path: string;
  value?: Numeric | null;
  onChange: (value: Numeric | null) => void;
  errors?: FormErrors;
  distance?: boolean;
}) {
  const [editing, setEditing] = useState<{
    wire: string | undefined;
    text: string;
  } | null>(null);
  let text = numericText(value);
  if (distance && text) {
    try {
      text = shiftDecimal(text, -1);
    } catch {
      /* unfinished input remains editable */
    }
  }
  if (editing && editing.wire === value?.text) text = editing.text;
  return (
    <TextField
      {...props}
      value={text}
      onChange={(text) => {
        let next: Numeric | null = text.trim() ? n(text) : null;
        if (distance && next) {
          try {
            next = n(shiftDecimal(text, 1));
          } catch {
            /* keep unserializable text invalid */
          }
        }
        setEditing({ wire: next?.text, text });
        onChange(next);
      }}
    />
  );
}
export function SelectField({
  label,
  path,
  value,
  onChange,
  options,
  errors = {},
  empty = false,
}: {
  label: string;
  path: string;
  value?: string | null;
  onChange: (value: string) => void;
  options: readonly (readonly [string, string])[];
  errors?: FormErrors;
  empty?: boolean;
}) {
  return (
    <Labeled label={label} path={path} errors={errors}>
      {(id, describedBy) => (
        <select
          id={id}
          className={inputClass}
          data-field-path={path}
          aria-describedby={describedBy}
          aria-invalid={!!errors[path]}
          value={value ?? ""}
          onChange={(e) => onChange(e.target.value)}
        >
          {empty && <option value="">Välj uttryckligen</option>}
          {options.map(([key, text]) => (
            <option key={key} value={key}>
              {text}
            </option>
          ))}
        </select>
      )}
    </Labeled>
  );
}
export function Check({
  children,
  checked,
  onChange,
}: {
  children: ReactNode;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex items-center gap-2 py-1 text-sm">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      {children}
    </label>
  );
}
