import { createContext, useContext, useSyncExternalStore } from "react";
import type { ComparisonWorkspace } from "./workspace";
export const ComparisonContext = createContext<ComparisonWorkspace | null>(
  null,
);
export function useComparison() {
  const workspace = useContext(ComparisonContext);
  if (!workspace) throw new Error("Comparison provider is missing.");
  const state = useSyncExternalStore(workspace.subscribe, workspace.snapshot);
  return { workspace, state };
}
