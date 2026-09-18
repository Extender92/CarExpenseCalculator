import { createContext, useContext } from "react";
export interface NavigationGuard {
  isDirty: () => boolean;
  requestClose: () => Promise<boolean>;
}
export const GuardContext = createContext<(guard: NavigationGuard) => () => void>(() => () => {});
export const useRegisterNavigationGuard = () => useContext(GuardContext);
