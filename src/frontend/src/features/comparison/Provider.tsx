import { useEffect, useState, type ReactNode } from "react";
import { useWorkspace } from "@/features/household/use-workspace";
import { ComparisonWorkspace } from "./workspace";
import { ComparisonContext } from "./use-comparison";

export function ComparisonProvider({ children }: { children: ReactNode }) {
  const { workspace: household } = useWorkspace();
  const [workspace] = useState(() => new ComparisonWorkspace(household));
  useEffect(() => {
    workspace.connect();
    const focus = () => workspace.onFocus();
    const changed = () => {
      if (!workspace.state.busy) {
        workspace.schedule();
        workspace.onFocus();
      }
    };
    const deleted = (event: Event) => {
      workspace.forget((event as CustomEvent<string>).detail);
      changed();
    };
    const unload = (event: BeforeUnloadEvent) => {
      if (workspace.isDirty()) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("focus", focus);
    window.addEventListener("vehicle-changed", changed);
    window.addEventListener("vehicle-deleted", deleted);
    window.addEventListener("beforeunload", unload);
    return () => {
      window.removeEventListener("focus", focus);
      window.removeEventListener("vehicle-changed", changed);
      window.removeEventListener("vehicle-deleted", deleted);
      window.removeEventListener("beforeunload", unload);
      workspace.dispose();
    };
  }, [workspace]);
  return (
    <ComparisonContext.Provider value={workspace}>
      {children}
    </ComparisonContext.Provider>
  );
}
