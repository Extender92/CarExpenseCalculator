import { useLocation, useNavigate } from "react-router-dom";
import { useRef, useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { useOptionalComparison } from "./use-comparison";
import { BaselineReview } from "./BaselineReview";
import { focusField } from "./navigation";
import { labelFor } from "./catalogue";

/** Calculations and conflict review stay reachable while the rest of the page is inert. */
export function ComparisonEditorActions() {
  const context = useOptionalComparison();
  const location = useLocation();
  const navigate = useNavigate();
  const errors = useRef<HTMLDivElement>(null);
  const [focusErrors, setFocusErrors] = useState(false);
  const errorKey = JSON.stringify(context?.state.errors ?? {});
  useEffect(() => {
    if (focusErrors && errorKey !== "{}") errors.current?.focus();
  }, [focusErrors, errorKey]);
  if (!context || location.pathname !== "/search") return null;
  const { workspace, state } = context;
  return <section className="mt-5 space-y-3" aria-label="Aktuell jämförelse">
    <div className="flex flex-wrap gap-3">
      <Button variant="secondary" disabled={state.calculating} onClick={() => { setFocusErrors(true); void workspace.calculate(); }}>Beräkna nu</Button>
      <Button variant="secondary" disabled={workspace.reportBlockReason() !== null} onClick={() => {
        if (workspace.openReport()) navigate("/search/report");
      }}>Öppna rapport</Button>
      {state.mode === "stored" && <Button variant="secondary" disabled={state.loading || !!state.busy}
        onClick={() => void workspace.refresh()}>Läs aktuellt serverunderlag</Button>}
    </div>
    {Object.keys(state.errors).length > 0 && <div ref={errors} role="alert" tabIndex={-1} className="rounded border border-rose-700 p-3 focus:ring-2 focus:ring-rose-300">
      <h3>Kontrollera uppgifterna</h3>
      <ul>{Object.entries(state.errors).map(([path, messages]) => <li key={path}>
        <button className="text-left text-rose-200 underline" onClick={() => focusField(path)}>{labelFor(path.split(".").at(-1) ?? "")}: {messages.join(" ")}</button>
      </li>)}</ul>
    </div>}
    <p className="text-sm text-slate-400">Rapporten fångar den aktuella jämförelsen. Osparade ändringar hanteras innan du lämnar dialogen.</p>
    <BaselineReview />
  </section>;
}
