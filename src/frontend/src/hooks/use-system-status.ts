import { useEffect, useState } from "react";
import { getSystemStatus, type SystemStatus } from "@/api/client";

type StatusState =
  | { phase: "loading" }
  | { phase: "loaded"; data: SystemStatus }
  | { phase: "error" };

export function useSystemStatus() {
  const [state, setState] = useState<StatusState>({ phase: "loading" });

  useEffect(() => {
    const controller = new AbortController();

    getSystemStatus()
      .then((data) => {
        if (!controller.signal.aborted) setState({ phase: "loaded", data });
      })
      .catch(() => {
        if (!controller.signal.aborted) setState({ phase: "error" });
      });

    return () => controller.abort();
  }, []);

  return state;
}
