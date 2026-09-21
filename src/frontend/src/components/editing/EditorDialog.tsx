import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { useRegisterNavigationGuard } from "./navigation-context";

export interface EditingResource {
  key: string;
  label: string;
  dirty: boolean;
  busy?: boolean;
  save: () => Promise<boolean>;
  discard: () => void;
}
interface Props {
  open: boolean;
  title: string;
  children: ReactNode;
  resources?: EditingResource[];
  activeResource?: string;
  /** Navigation owns the URL transition; callers must not replace its history entry. */
  onClose: (navigating?: boolean) => void;
  actions?: ReactNode;
}
let modalCount = 0;
let priorBodyOverflow = "";
export function EditorDialog({ open, title, children, resources = [], activeResource, onClose, actions }: Props) {
  const id = useId();
  const element = useRef<HTMLDialogElement>(null);
  const latest = useRef({ resources, onClose });
  useEffect(() => { latest.current = { resources, onClose }; });
  const opener = useRef<HTMLElement | null>(null);
  const scroll = useRef({ x: 0, y: 0 });
  const pendingClose = useRef<((accepted: boolean) => void) | null>(null);
  const closing = useRef(false);
  const navigationClose = useRef(false);
  const question = useRef<HTMLElement>(null);
  const [asking, setAsking] = useState(false);
  const [savingAll, setSavingAll] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const register = useRegisterNavigationGuard();
  const finishClose = useCallback(() => {
    closing.current = true;
    pendingClose.current?.(true);
    pendingClose.current = null;
    setAsking(false);
    latest.current.onClose(navigationClose.current);
  }, []);
  const requestClose = useCallback((navigating = false): Promise<boolean> => {
    navigationClose.current = navigating;
    if (!latest.current.resources.some(resource => resource.dirty || resource.busy)) {
      closing.current = true;
      latest.current.onClose(navigating);
      return Promise.resolve(true);
    }
    setAsking(true);
    return new Promise(resolve => {
      pendingClose.current?.(false);
      pendingClose.current = resolve;
    });
  }, []);

  useEffect(() => {
    const dialog = element.current;
    if (!dialog || !open) return;
    closing.current = false;
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    scroll.current = { x: window.scrollX, y: window.scrollY };
    if (!dialog.open) dialog.showModal();
    if (modalCount++ === 0) priorBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const unregister = register({
      isDirty: () => !closing.current && latest.current.resources.some(resource => resource.dirty || resource.busy),
      requestClose: () => requestClose(true),
    });
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (latest.current.resources.some(resource => resource.dirty || resource.busy)) {
        event.preventDefault(); event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", beforeUnload);
    return () => {
      unregister();
      window.removeEventListener("beforeunload", beforeUnload);
      dialog.close();
      if (--modalCount === 0) document.body.style.overflow = priorBodyOverflow;
      opener.current?.focus({ preventScroll: true });
      window.scrollTo(scroll.current.x, scroll.current.y);
      pendingClose.current?.(false);
      pendingClose.current = null;
    };
  }, [open, register, requestClose]);
  useEffect(() => {
    if (asking) { question.current?.scrollIntoView?.({ block: "nearest" }); question.current?.querySelector("button")?.focus(); }
  }, [asking]);

  async function saveChanged() {
    setSavingAll(true);
    setMessage(null);
    const saved: string[] = [];
    try {
      const keys = latest.current.resources.filter(resource => resource.dirty).map(resource => resource.key);
      for (const key of keys) {
        const resource = latest.current.resources.find(resource => resource.key === key);
        if (!resource?.dirty) continue;
        if (resource.busy || !await resource.save()) {
          setMessage(saved.length ? `Sparat: ${saved.join(", ")}. Övriga ändringar finns kvar. Rätta felet och försök igen.`
            : "Ändringarna kunde inte sparas. De finns kvar i dialogen.");
          return;
        }
        saved.push(resource.label);
        // Publish acknowledged revisions and save baselines before the next resource.
        await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      }
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      if (latest.current.resources.some(resource => resource.dirty || resource.busy)) {
        setMessage("Sparningen är klar, men det finns senare ändringar kvar att spara.");
        return;
      }
      finishClose();
    } catch {
      setMessage(saved.length ? `Sparat: ${saved.join(", ")}. Återstående ändringar finns kvar.`
        : "Sparningen misslyckades. Dina ändringar finns kvar.");
    } finally { setSavingAll(false); }
  }
  const active = resources.find(resource => resource.key === activeResource);
  const busy = savingAll || resources.some(resource => resource.busy);
  return <dialog ref={element} aria-labelledby={id} className="editor-dialog" onCancel={event => {
    event.preventDefault(); void requestClose();
  }} onClick={event => {
    if (event.target !== event.currentTarget) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)
      void requestClose();
  }}>
    <div className="editor-dialog-layout">
      <header className="flex shrink-0 items-center justify-between gap-4 border-b border-slate-700 p-4">
        <h2 id={id} className="min-w-0 break-words text-lg font-bold">{title}</h2>
        <Button type="button" variant="ghost" onClick={() => void requestClose()}>Stäng</Button>
      </header>
      <div className="editor-dialog-content">
        {asking && <section ref={question} role="alert" aria-label="Osparade ändringar" className="mb-4 rounded-xl border border-amber-400 bg-slate-900 p-4">
          <h3 className="font-semibold">Spara ändringarna innan du stänger?</h3>
          <ul className="my-3 list-disc pl-5">{resources.filter(resource => resource.dirty).map(resource =>
            <li key={resource.key}>{resource.label}</li>)}</ul>
          <div className="flex flex-wrap gap-2">
            <Button type="button" disabled={busy} onClick={() => void saveChanged()}>Spara och stäng</Button>
            <Button type="button" variant="secondary" disabled={busy} onClick={() => {
              latest.current.resources.filter(resource => resource.dirty).forEach(resource => resource.discard());
              finishClose();
            }}>Kasta ändringar</Button>
            <Button type="button" variant="ghost" onClick={() => {
              pendingClose.current?.(false); pendingClose.current = null; setAsking(false);
            }}>Fortsätt redigera</Button>
          </div>
          {busy && <p role="status" className="mt-2">Vänta tills pågående sparning är klar.</p>}
        </section>}
        {message && <p role="alert" className="mb-4 text-amber-200">{message}</p>}
        {children}
      </div>
      <footer className="flex shrink-0 flex-wrap items-center gap-3 border-t border-slate-700 bg-slate-950 p-4">
        {active && <Button type="button" disabled={!active.dirty || busy} onClick={() => void active.save()}>Spara {active.label.toLocaleLowerCase("sv-SE")}</Button>}
        {actions}
        <Button type="button" variant="secondary" onClick={() => void requestClose()}>Stäng</Button>
      </footer>
    </div>
  </dialog>;
}
