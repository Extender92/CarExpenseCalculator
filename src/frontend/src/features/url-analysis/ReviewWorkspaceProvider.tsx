import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ReviewWorkspaceContext } from "./review-workspace";
import type { ListingWorkspaceItem } from "./review-model";
import { listingNumberText } from "./exact";
import type { AcknowledgedRevision } from "@/lib/vehicle-events";

export function ReviewWorkspaceProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ListingWorkspaceItem[]>([]);
  const value = useMemo(() => ({ items, setItems }), [items]);
  useEffect(() => {
    const acknowledged = (event: Event) => {
      const { vehicleId, previous, current } = (event as CustomEvent<AcknowledgedRevision>).detail;
      setItems(items => items.map(item => item.saved?.vehicleId === vehicleId && previous && listingNumberText(item.saved.revision) === previous.text
        ? { ...item, saved: { ...item.saved, revision: current }, householdBaseRevision: current.text } : item));
    };
    window.addEventListener("vehicle-revision-acknowledged", acknowledged);
    return () => window.removeEventListener("vehicle-revision-acknowledged", acknowledged);
  }, []);
  useEffect(() => {
    if (!items.some(item => item.dirty || (!item.saved && !item.reviewDraft) || !!item.saved && !!item.workflow && (!item.workflow.costsSaved || !item.workflow.factsSaved))) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [items]);
  return <ReviewWorkspaceContext.Provider value={value}>{children}</ReviewWorkspaceContext.Provider>;
}
