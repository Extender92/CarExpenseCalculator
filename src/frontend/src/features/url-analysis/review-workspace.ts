import { createContext, useContext, useState, type Dispatch, type SetStateAction } from "react";
import type { ListingWorkspaceItem } from "./review-model";

export const ReviewWorkspaceContext = createContext<{
  items: ListingWorkspaceItem[];
  setItems: Dispatch<SetStateAction<ListingWorkspaceItem[]>>;
} | null>(null);
export function useReviewWorkspace() {
  const shared = useContext(ReviewWorkspaceContext);
  const [items, setItems] = useState<ListingWorkspaceItem[]>([]);
  return shared ?? { items, setItems };
}
