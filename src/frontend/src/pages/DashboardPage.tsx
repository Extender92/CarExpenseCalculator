import { ArrowRight, Calculator, CheckCircle2, Database, Link2, Search, Server, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSystemStatus } from "@/hooks/use-system-status";
import { cn } from "@/lib/utils";

const modes = [
  {
    title: "Regelsökning",
    description: "Sök efter bilar som matchar pris, miltal, dragkrok och ägarhistorik.",
    to: "/search",
    icon: Search,
    color: "cyan",
    feature: "ruleBasedSearch",
  },
  {
    title: "Analysera URL:er",
    description: "Klistra in länkar till intressanta annonser och jämför underlaget på samma villkor.",
    to: "/analyze-urls",
    icon: Link2,
    color: "blue",
    feature: "urlAnalysis",
  },
  {
    title: "Manuell kalkyl",
    description: "Fyll i bilens kostnader själv och räkna på ett scenario utan annonskälla.",
    to: "/manual",
    icon: Calculator,
    color: "violet",
    feature: "manualCalculator",
  },
] as const;

export function DashboardPage() {
  const status = useSystemStatus();

  return (
    <div className="space-y-10">
      <section className="flex flex-col justify-between gap-6 border-b border-slate-800 pb-9 md:flex-row md:items-end">
        <div className="max-w-3xl">
          <Badge variant="success">Manuell kalkyl och URL-analys tillgängliga</Badge>
          <h1 className="mt-5 text-4xl font-bold tracking-tight text-white sm:text-5xl">
            Ett bättre beslutsunderlag för nästa bil.
          </h1>
          <p className="mt-5 max-w-2xl text-base leading-7 text-slate-400 sm:text-lg">
            Samla sökning, annonsgranskning och kostnader i en lokal arbetsyta. Regler och beräkningar
            blir alltid grunden; AI blir senare ett extra granskningslager.
          </p>
        </div>
        <SystemBadge phase={status.phase} status={status.phase === "loaded" ? status.data.status : undefined} />
      </section>

      <section aria-labelledby="choose-mode">
        <div className="mb-5 flex items-center justify-between gap-4">
          <div>
            <p className="text-sm font-semibold text-cyan-400">Tre sätt att börja</p>
            <h2 id="choose-mode" className="mt-1 text-2xl font-bold tracking-tight">Vad vill du göra?</h2>
          </div>
          <span className="hidden text-sm text-slate-500 sm:block">Funktionerna aktiveras längs roadmapen</span>
        </div>

        <div className="grid gap-5 xl:grid-cols-3">
          {modes.map(({ title, description, to, icon: Icon, color, feature }) => {
            const available = status.phase === "loaded" && status.data.features[feature];
            return (
              <Card key={to} className="group relative overflow-hidden transition hover:-translate-y-1 hover:border-slate-700">
                <div className={cn("absolute inset-x-0 top-0 h-px opacity-70", color === "cyan" && "bg-cyan-400", color === "blue" && "bg-blue-400", color === "violet" && "bg-violet-400")} />
                <CardHeader>
                  <div className="flex items-center justify-between gap-3">
                    <span className={cn("grid size-11 place-items-center rounded-xl", color === "cyan" && "bg-cyan-400/10 text-cyan-300", color === "blue" && "bg-blue-400/10 text-blue-300", color === "violet" && "bg-violet-400/10 text-violet-300")}>
                      <Icon size={22} />
                    </span>
                    <Badge variant={available ? "success" : "muted"}>{available ? "Tillgänglig" : "Planerad"}</Badge>
                  </div>
                  <CardTitle className="pt-3">{title}</CardTitle>
                  <CardDescription>{description}</CardDescription>
                </CardHeader>
                <CardContent>
                  <Link to={to} className={cn(buttonVariants({ variant: "secondary" }), "w-full justify-between")}>
                    Öppna
                    <ArrowRight size={16} className="transition-transform group-hover:translate-x-1" />
                  </Link>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </section>

      <section className="grid gap-5 lg:grid-cols-[1.4fr_1fr]" aria-label="Projektstatus">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Första sökprofilen</CardTitle>
                <CardDescription>Reglerna vi bygger den framtida sökningen kring.</CardDescription>
              </div>
              <Badge variant="muted">Planerad</Badge>
            </div>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-2">
              {["Dragkrok krävs", "5 000–20 000 kr", "Max 20 000 mil", "Högst 6 ägare"].map((rule) => (
                <div key={rule} className="flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-sm text-slate-300">
                  <CheckCircle2 size={17} className="shrink-0 text-cyan-400" />
                  <span>{rule}</span>
                </div>
              ))}
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Systemstatus</CardTitle>
            <CardDescription>Kontrolleras via samma interna API som resten av webbappen.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <StatusRow icon={Server} label="API" value={status.phase === "error" ? "Ej tillgängligt" : "Anslutet"} healthy={status.phase !== "error"} />
            <StatusRow
              icon={Database}
              label="PostgreSQL"
              value={status.phase === "loaded" ? (status.data.database === "available" ? "Tillgänglig" : "Ej tillgänglig") : "Kontrollerar…"}
              healthy={status.phase === "loaded" && status.data.database === "available"}
            />
            <StatusRow icon={Sparkles} label="AI-granskning" value="Avstängd" healthy={false} muted />
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

function SystemBadge({ phase, status }: { phase: "loading" | "loaded" | "error"; status?: string }) {
  const healthy = phase === "loaded" && status === "healthy";
  const label = phase === "loading" ? "Kontrollerar systemet" : healthy ? "Systemet är friskt" : "Systemet är degraderat";

  return (
    <Badge variant={healthy ? "success" : phase === "loading" ? "muted" : "warning"} className="shrink-0 gap-2 py-2">
      <span className={cn("size-2 rounded-full", healthy ? "bg-emerald-400" : phase === "loading" ? "bg-slate-400" : "bg-amber-400")} />
      {label}
    </Badge>
  );
}

function StatusRow({ icon: Icon, label, value, healthy, muted = false }: { icon: typeof Server; label: string; value: string; healthy: boolean; muted?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-xl border border-slate-800 bg-slate-950/50 p-3">
      <span className="flex items-center gap-3 text-sm text-slate-300"><Icon size={17} className="text-slate-500" />{label}</span>
      <span className={cn("text-xs font-semibold", muted ? "text-slate-500" : healthy ? "text-emerald-400" : "text-amber-400")}>{value}</span>
    </div>
  );
}
