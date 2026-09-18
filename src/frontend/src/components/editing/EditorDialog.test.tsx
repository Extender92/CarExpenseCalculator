import { useState } from "react";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, Link, MemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { deferred } from "@/features/household/test-fixtures";
import { EditorDialog } from "./EditorDialog";
import { NavigationGuardProvider } from "./NavigationGuard";

function Harness({ save = async () => true, failSecond = false }: { save?: (value: string) => Promise<boolean>; failSecond?: boolean }) {
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState("Sparat");
  const [base, setBase] = useState("Sparat");
  const [facts, setFacts] = useState(false);
  const [revision, setRevision] = useState(1);
  return <><button onClick={() => setOpen(true)}>Redigera</button><span>Revision {revision}</span>
    {open && <EditorDialog open title="Bilens underlag" onClose={() => setOpen(false)} activeResource="listing" resources={[
      { key: "listing", label: "Annons", dirty: value !== base, discard: () => setValue(base), save: async () => {
        const snapshot = value;
        if (!await save(snapshot)) return false;
        setBase(snapshot); setRevision(current => current + 1); return true;
      } },
      { key: "facts", label: "Fakta", dirty: facts, discard: () => setFacts(false), save: async () => {
        if (failSecond || revision !== 2) return false;
        setFacts(false); setRevision(current => current + 1); return true;
      } },
    ]}>
      <label>Värde<input value={value} onChange={event => setValue(event.target.value)} /></label>
      <button onClick={() => setFacts(true)}>Ändra fakta</button>
      <Link to="/other">Annan sida</Link>
    </EditorDialog>}
  </>;
}
async function edit() {
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Redigera" }));
  await user.clear(screen.getByLabelText("Värde"));
  await user.type(screen.getByLabelText("Värde"), "Ändrat");
  return user;
}
describe("shared native editing dialog", () => {
  it("asks for a dirty parent after closing a clean nested editor during navigation", async () => {
    function Nested() {
      const [parent, setParent] = useState(true);
      const [child, setChild] = useState(false);
      const [dirty, setDirty] = useState(true);
      return parent && <EditorDialog open title="Bil" onClose={() => setParent(false)} resources={[
        { key: "car", label: "Bil", dirty, discard: () => setDirty(false), save: async () => { setDirty(false); return true; } },
      ]}><button onClick={() => setChild(true)}>Öppna hushåll</button>{child && <EditorDialog open title="Hushåll" onClose={() => setChild(false)}><Link to="/other">Lämna</Link></EditorDialog>}</EditorDialog>;
    }
    const router = createMemoryRouter([{ path: "/", element: <NavigationGuardProvider><Nested /></NavigationGuardProvider> },
      { path: "/other", element: <p>Framme</p> }]);
    render(<RouterProvider router={router} />);
    const user = userEvent.setup();
    expect(document.body.style.overflow).toBe("hidden");
    await user.click(screen.getByRole("button", { name: "Öppna hushåll" }));
    await user.click(screen.getByRole("link", { name: "Lämna" }));
    await screen.findByRole("button", { name: "Spara och stäng" });
    expect(screen.queryByRole("dialog", { name: "Hushåll" })).not.toBeInTheDocument();
    expect(document.body.style.overflow).toBe("hidden");
    await user.click(screen.getByRole("button", { name: "Spara och stäng" }));
    await screen.findByText("Framme");
    expect(document.body.style.overflow).not.toBe("hidden");
  });

  it("routes Escape through the same three choices, discards only edits, and restores focus", async () => {
    render(<MemoryRouter><Harness /></MemoryRouter>);
    const user = await edit();
    const event = new Event("cancel", { cancelable: true });
    fireEvent(screen.getByRole("dialog"), event);
    expect(event.defaultPrevented).toBe(true);
    expect(screen.getByRole("button", { name: "Spara och stäng" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Fortsätt redigera" }));
    expect(screen.getByLabelText("Värde")).toHaveValue("Ändrat");
    await user.click(screen.getAllByRole("button", { name: "Stäng" })[0]);
    await user.click(screen.getByRole("button", { name: "Kasta ändringar" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Redigera" })).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Redigera" }));
    expect(screen.getByLabelText("Värde")).toHaveValue("Sparat");
  });

  it("ordinary save affects only the selected resource", async () => {
    render(<MemoryRouter><Harness /></MemoryRouter>); const user = await edit();
    await user.click(screen.getByRole("button", { name: "Ändra fakta" }));
    await user.click(screen.getByRole("button", { name: "Spara annons" }));
    expect(screen.getByText("Revision 2")).toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: "Stäng" })[0]);
    const prompt = screen.getByRole("alert", { name: "Osparade ändringar" });
    expect(within(prompt).getByText("Fakta")).toBeInTheDocument();
    expect(within(prompt).queryByText("Annons")).not.toBeInTheDocument();
  });

  it("saves several resources sequentially with acknowledged revisions", async () => {
    const save = vi.fn().mockResolvedValue(true);
    render(<MemoryRouter><Harness save={save} /></MemoryRouter>); const user = await edit();
    await user.click(screen.getByRole("button", { name: "Ändra fakta" }));
    await user.click(screen.getAllByRole("button", { name: "Stäng" })[0]);
    await user.click(screen.getByRole("button", { name: "Spara och stäng" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByText("Revision 3")).toBeInTheDocument();
    expect(save).toHaveBeenCalledExactlyOnceWith("Ändrat");
  });

  it("keeps partial failures open without retrying and discards to the latest successful save", async () => {
    const save = vi.fn().mockResolvedValue(true);
    render(<MemoryRouter><Harness save={save} failSecond /></MemoryRouter>); const user = await edit();
    await user.click(screen.getByRole("button", { name: "Ändra fakta" }));
    await user.click(screen.getAllByRole("button", { name: "Stäng" })[0]);
    await user.click(screen.getByRole("button", { name: "Spara och stäng" }));
    expect(await screen.findByText(/Sparat: Annons\. Övriga ändringar finns kvar/)).toBeInTheDocument();
    expect(save).toHaveBeenCalledOnce();
    await user.click(screen.getByRole("button", { name: "Kasta ändringar" }));
    await user.click(screen.getByRole("button", { name: "Redigera" }));
    expect(screen.getByLabelText("Värde")).toHaveValue("Ändrat");
  });

  it("preserves later edits when a save finishes and does not close until they are handled", async () => {
    const pending = deferred<boolean>();
    render(<MemoryRouter><Harness save={() => pending.promise} /></MemoryRouter>); const user = await edit();
    await user.click(screen.getAllByRole("button", { name: "Stäng" })[0]);
    await user.click(screen.getByRole("button", { name: "Spara och stäng" }));
    await user.type(screen.getByLabelText("Värde"), " senare");
    await act(async () => pending.resolve(true));
    expect(await screen.findByText(/det finns senare ändringar kvar att spara/)).toBeInTheDocument();
    expect(screen.getByLabelText("Värde")).toHaveValue("Ändrat senare");
    await user.click(screen.getByRole("button", { name: "Kasta ändringar" }));
    await user.click(screen.getByRole("button", { name: "Redigera" }));
    expect(screen.getByLabelText("Värde")).toHaveValue("Ändrat");
  });

  it("warns on reload only while a resource is dirty", async () => {
    render(<MemoryRouter><Harness /></MemoryRouter>); const user = await edit();
    const before = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(before); expect(before.defaultPrevented).toBe(true);
    await user.click(screen.getByRole("button", { name: "Spara annons" }));
    const after = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(after); expect(after.defaultPrevented).toBe(false);
  });

  it("blocks internal navigation and browser Back, then honors continue or discard", async () => {
    const router = createMemoryRouter([{ path: "*", element: <NavigationGuardProvider><Harness /></NavigationGuardProvider> },
      { path: "/other", element: <p>Annan vy</p> }], { initialEntries: ["/previous", "/edit"], initialIndex: 1 });
    render(<RouterProvider router={router} />); const user = await edit();
    await user.click(screen.getByRole("link", { name: "Annan sida" }));
    await user.click(await screen.findByRole("button", { name: "Fortsätt redigera" }));
    expect(router.state.location.pathname).toBe("/edit");
    await act(async () => { await router.navigate(-1); });
    await user.click(await screen.findByRole("button", { name: "Kasta ändringar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/previous"));
  });
});
