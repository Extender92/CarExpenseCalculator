import { useCallback, useContext, useEffect, useRef, type ReactNode } from "react";
import { UNSAFE_DataRouterContext, useBlocker } from "react-router-dom";

import { GuardContext, type NavigationGuard } from "./navigation-context";

/** One router blocker coordinates whichever editor currently owns focus. */
export function NavigationGuardProvider({ children }: { children: ReactNode }) {
  const stack = useRef<NavigationGuard[]>([]);
  const register = useCallback((guard: NavigationGuard) => {
    stack.current.push(guard);
    return () => {
      stack.current = stack.current.filter(item => item !== guard);
    };
  }, []);
  const dataRouter = useContext(UNSAFE_DataRouterContext);
  return <GuardContext.Provider value={register}>
    {dataRouter && <RouterGuard stack={stack} />}
    {children}
  </GuardContext.Provider>;
}
function RouterGuard({ stack }: { stack: { current: NavigationGuard[] } }) {
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    stack.current.some(guard => guard.isDirty()) &&
    (currentLocation.pathname !== nextLocation.pathname || currentLocation.search !== nextLocation.search));
  useEffect(() => {
    if (blocker.state !== "blocked") return;
    let live = true;
    void (async () => {
      for (const guard of [...stack.current].reverse()) {
        if (!await guard.requestClose()) return false;
        await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      }
      return true;
    })().then(accepted => {
      if (!live) return;
      if (accepted) blocker.proceed(); else blocker.reset();
    });
    return () => { live = false; };
  }, [blocker, stack]);
  return null;
}
