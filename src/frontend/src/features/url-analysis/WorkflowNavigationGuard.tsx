import { useEffect, useRef, useState } from "react";
import { EditorDialog } from "@/components/editing/EditorDialog";
import { useRegisterNavigationGuard } from "@/components/editing/navigation-context";
import { Button } from "@/components/ui/button";
import type { ListingWorkspaceItem } from "./review-model";
import { workflowPending } from "./batch-workflow";

const pending = (item: ListingWorkspaceItem) => item.dirty || (!item.saved && !item.reviewDraft) || workflowPending(item);

/** The editor guard runs first; this protects other cards retained by the workspace. */
export function WorkflowNavigationGuard({ items, save, discard, allow }: {
  items: ListingWorkspaceItem[];
  save: (item: ListingWorkspaceItem) => Promise<boolean>;
  discard: (ids: string[]) => void;
  allow: { current: boolean };
}) {
  const register = useRegisterNavigationGuard();
  const latest = useRef({ items, save, discard });
  useEffect(() => { latest.current = { items, save, discard }; });
  const answer = useRef<((accepted: boolean) => void) | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => register({
    isDirty: () => !allow.current && latest.current.items.some(pending),
    requestClose: () => {
      if (allow.current || !latest.current.items.some(pending)) return Promise.resolve(true);
      setOpen(true); setError(null);
      return new Promise(resolve => { answer.current = resolve; });
    },
  }), [register, allow]);
  function finish(accepted: boolean) {
    answer.current?.(accepted); answer.current = null; setOpen(false);
  }
  async function saveAll() {
    setBusy(true); setError(null);
    try {
      for (const item of latest.current.items.filter(pending)) {
        if (!await latest.current.save(item)) {
          setError("Alla ändringar kunde inte sparas. Redan sparade delar behålls; återstående arbete finns kvar.");
          return;
        }
        await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      }
      if (latest.current.items.some(pending)) setError("Senare ändringar finns kvar. Spara dem innan du fortsätter.");
      else finish(true);
    } finally { setBusy(false); }
  }
  if (!open) return null;
  const writing = busy || items.some(item => item.saving || item.controller || item.workflow?.writing || item.workflow?.preparing);
  return <EditorDialog open title="Osparade bilar" onClose={() => finish(false)} actions={<>
    <Button disabled={writing} onClick={() => void saveAll()}>Spara och fortsätt</Button>
    <Button variant="secondary" disabled={writing} onClick={() => {
      latest.current.discard(latest.current.items.filter(pending).map(item => item.id)); finish(true);
    }}>Kasta ändringar</Button>
    <Button variant="ghost" onClick={() => finish(false)}>Fortsätt redigera</Button>
  </>}>
    <p>Spara bilarnas ändringar innan du lämnar sidan?</p>
    <ul className="my-4 list-disc pl-5">{items.filter(pending).map(item => <li key={item.id}>
      {item.draft.fields.registrationNumber.input || item.draft.fields.vehicleLabel.input || "Annonsutkast"}
      {workflowPending(item) ? " – kostnader eller jämförelsefakta kvar" : " – annonsunderlag"}
    </li>)}</ul>
    {writing && <p role="status">Vänta tills pågående arbete är klart.</p>}
    {error && <p role="alert">{error}</p>}
  </EditorDialog>;
}
