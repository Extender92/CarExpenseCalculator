import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Button, buttonVariants } from "@/components/ui/button";
import { useSystemStatus } from "@/hooks/use-system-status";
import { useWorkspace } from "@/features/household/use-workspace";
import { householdApi, HouseholdApiError, emptyProfileResponse } from "@/features/household/api";
import { FirstStartGuide } from "@/features/household/FirstStartGuide";
import { guideDismissed } from "@/features/household/guide-preference";
import { reviewDraftApi } from "@/features/url-analysis/review-drafts-api";
import { useReviewWorkspace } from "@/features/url-analysis/review-workspace";

export function DashboardPage() {
  const status = useSystemStatus();
  const { workspace } = useWorkspace();
  const { items } = useReviewWorkspace();
  const [guide, setGuide] = useState(false);
  const [draftCount, setDraftCount] = useState(0);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    void householdApi.profile(controller.signal).then(profile => {
      if (controller.signal.aborted) return;
      workspace.receiveProfile(profile);
      if (!profile.input && !guideDismissed()) setGuide(true);
    }).catch(error => {
      if (controller.signal.aborted) return;
      if (error instanceof HouseholdApiError && error.code === "profileNotFound") {
        workspace.receiveProfile(emptyProfileResponse());
        if (!guideDismissed()) setGuide(true);
      } else setError("Gemensamma uppgifter kunde inte läsas. Försök igen under Gemensamma uppgifter.");
    });
    void reviewDraftApi.list().then(drafts => { if (!controller.signal.aborted) setDraftCount(drafts.length); })
      .catch(() => { if (!controller.signal.aborted) setError("Sparade utkast kunde inte läsas. Försök igen under Lägg till bil."); });
    return () => controller.abort();
  }, [workspace]);
  return <div className="max-w-3xl space-y-8">
    <header><h1 className="text-3xl font-bold sm:text-4xl">Jämför kostnaden för nästa bil</h1>
      <p className="mt-4 text-lg text-slate-300">Lägg till annonser eller egna biluppgifter. Se vad bilarna kostar med samma körsträcka och förutsättningar.</p></header>
    <div className="flex flex-wrap gap-4">
      <Link to="/analyze-urls" className={buttonVariants({ size: "lg" })}>Lägg till bil</Link>
      <Link to="/search" className={buttonVariants({ size: "lg", variant: "secondary" })}>Jämför sparade bilar</Link>
    </div>
    {(draftCount > 0 || items.length > 0) && <Link className="block text-cyan-300 underline" to="/analyze-urls#review-drafts">Fortsätt med dina utkast</Link>}
    <p className="text-slate-400">Du kan spara ofullständiga uppgifter och komplettera senare. Annonsuppgifter behöver inte bekräftas för att sparas.</p>
    <Button variant="secondary" onClick={() => setGuide(true)}>Öppna introduktionen</Button>
    {error && <p role="alert" className="text-amber-200">{error}</p>}
    {status.phase === "error" && <p role="alert">Servern kunde inte nås. Sparning och annonshämtning kan vara otillgängliga.</p>}
    {status.phase === "loaded" && status.data.database !== "available" && <p role="alert">Databasen är inte tillgänglig. Uppgifter kan inte sparas just nu.</p>}
    <details className="rounded-xl border border-slate-800 p-4"><summary className="cursor-pointer">Systemstatus</summary>
      <dl className="mt-4 space-y-2"><div><dt>API</dt><dd>{status.phase === "loaded" ? "Anslutet" : status.phase === "error" ? "Ej tillgängligt" : "Kontrollerar…"}</dd></div>
        <div><dt>PostgreSQL</dt><dd>{status.phase === "loaded" ? status.data.database === "available" ? "Tillgänglig" : "Ej tillgänglig" : "Kontrollerar…"}</dd></div>
        <div><dt>AI-granskning</dt><dd>Avstängd</dd></div></dl>
    </details>
    {guide && <FirstStartGuide onClose={() => setGuide(false)} />}
  </div>;
}
