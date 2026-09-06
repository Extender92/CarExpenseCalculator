import { useEffect, useRef } from "react";
import { fieldLabel } from "./labels";
import type { FormErrors } from "./form-model";

export function ErrorSummary({
  errors,
  focus = false,
}: {
  errors: FormErrors;
  focus?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const signature = Object.entries(errors)
    .map(([path, messages]) => `${path}:${messages.join()}`)
    .join("|");
  useEffect(() => {
    if (focus && signature) ref.current?.focus();
  }, [focus, signature]);
  if (!signature) return null;
  return (
    <div
      ref={ref}
      tabIndex={-1}
      role="alert"
      className="rounded-xl border border-rose-700 bg-rose-950/30 p-4 outline-none focus:ring-2 focus:ring-rose-300"
    >
      <h3 className="font-semibold">Kontrollera uppgifterna</h3>
      <ul className="mt-2 space-y-2 text-sm">
        {Object.entries(errors).map(([path, messages]) => (
          <li key={path}>
            <button
              type="button"
              className="text-left text-rose-200 underline"
              onClick={() => {
                const vehicleId = /^transition\.([a-f0-9-]+)\./i.exec(
                  path,
                )?.[1];
                const scope = vehicleId
                  ? (document.querySelector(
                      `[data-vehicle-id="${vehicleId}"]`,
                    ) ?? document)
                  : document;
                const element = [
                  ...scope.querySelectorAll<HTMLElement>("[data-field-path]"),
                ].find(
                  (element) =>
                    element.dataset.fieldPath === path ||
                    path.endsWith(element.dataset.fieldPath ?? "no-match"),
                );
                let parent = element?.parentElement;
                while (parent) {
                  if (parent instanceof HTMLDetailsElement) parent.open = true;
                  parent = parent.parentElement;
                }
                element?.focus();
                element?.scrollIntoView({ block: "center" });
              }}
            >
              {fieldLabel(path)}: {messages.join(" ")}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
