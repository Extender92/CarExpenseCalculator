import { createContext, useContext, useSyncExternalStore } from "react";
import { HouseholdWorkspace } from "./workspace";

export const WorkspaceContext = createContext<HouseholdWorkspace | null>(null);
export function useOptionalWorkspace() {
  return useContext(WorkspaceContext);
}
export function useWorkspace() {
  const workspace = useContext(WorkspaceContext);
  if (!workspace) throw new Error("Household workspace provider is missing.");
  const state = useSyncExternalStore(workspace.subscribe, workspace.snapshot);
  return { workspace, state };
}
