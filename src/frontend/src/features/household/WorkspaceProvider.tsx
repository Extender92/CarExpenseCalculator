import { useEffect, useState, type ReactNode } from "react";
import { HouseholdWorkspace } from "./workspace";

import { WorkspaceContext } from "./use-workspace";

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const [workspace] = useState(() => new HouseholdWorkspace());
  useEffect(() => {
    const focus = () => workspace.onFocus();
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (workspace.isDirty()) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    const deleted = (event: Event) =>
      workspace.forgetVehicle((event as CustomEvent<string>).detail);
    const changed = () => workspace.onFocus();
    window.addEventListener("focus", focus);
    window.addEventListener("beforeunload", beforeUnload);
    window.addEventListener("vehicle-deleted", deleted);
    window.addEventListener("vehicle-changed", changed);
    return () => {
      window.removeEventListener("focus", focus);
      window.removeEventListener("beforeunload", beforeUnload);
      window.removeEventListener("vehicle-deleted", deleted);
      window.removeEventListener("vehicle-changed", changed);
      workspace.dispose();
    };
  }, [workspace]);
  return (
    <WorkspaceContext.Provider value={workspace}>
      {children}
    </WorkspaceContext.Provider>
  );
}
