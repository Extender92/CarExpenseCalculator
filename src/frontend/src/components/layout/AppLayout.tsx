import { Calculator, CarFront, Link2, Menu, Search, X } from "lucide-react";
import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { cn } from "@/lib/utils";

const navigation = [
  { to: "/", label: "Översikt", icon: CarFront, end: true },
  { to: "/search", label: "Regelsökning", icon: Search },
  { to: "/analyze-urls", label: "URL-analys", icon: Link2 },
  { to: "/manual", label: "Manuell kalkyl", icon: Calculator },
];

export function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="pointer-events-none fixed inset-0 overflow-hidden" aria-hidden="true">
        <div className="absolute -left-48 -top-48 h-96 w-96 rounded-full bg-cyan-500/10 blur-3xl" />
        <div className="absolute -right-48 top-1/3 h-96 w-96 rounded-full bg-blue-500/10 blur-3xl" />
      </div>

      <header className="sticky top-0 z-40 border-b border-slate-800/80 bg-slate-950/85 backdrop-blur-xl lg:hidden">
        <div className="flex h-16 items-center justify-between px-5">
          <Brand />
          <button
            type="button"
            className="rounded-lg p-2 text-slate-300 hover:bg-slate-800"
            aria-label={menuOpen ? "Stäng meny" : "Öppna meny"}
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((open) => !open)}
          >
            {menuOpen ? <X size={22} /> : <Menu size={22} />}
          </button>
        </div>
        {menuOpen && <Navigation onNavigate={() => setMenuOpen(false)} mobile />}
      </header>

      <aside className="fixed inset-y-0 left-0 z-30 hidden w-72 border-r border-slate-800/80 bg-slate-950/75 p-6 backdrop-blur-xl lg:block">
        <Brand />
        <p className="mt-4 text-sm leading-6 text-slate-500">
          Hitta, granska och jämför bilar på ett ställe.
        </p>
        <Navigation />
        <div className="absolute inset-x-6 bottom-6 rounded-xl border border-slate-800 bg-slate-900/50 p-4">
          <p className="text-xs font-semibold uppercase tracking-widest text-slate-500">Grundfas</p>
          <p className="mt-2 text-sm text-slate-300">Funktionerna byggs stegvis. Inga AI-anrop görs ännu.</p>
        </div>
      </aside>

      <main className="relative lg:pl-72">
        <div className="mx-auto min-h-screen max-w-7xl px-5 py-8 sm:px-8 lg:px-12 lg:py-12">
          <Outlet />
        </div>
      </main>
    </div>
  );
}

function Brand() {
  return (
    <NavLink to="/" className="flex items-center gap-3" aria-label="Bilverktyget, startsida">
      <span className="grid size-10 place-items-center rounded-xl bg-cyan-400 text-slate-950 shadow-lg shadow-cyan-500/20">
        <CarFront size={22} strokeWidth={2.5} />
      </span>
      <span>
        <span className="block text-lg font-bold tracking-tight">Bilverktyget</span>
        <span className="block text-[11px] font-semibold uppercase tracking-[0.18em] text-cyan-400">Car intelligence</span>
      </span>
    </NavLink>
  );
}

function Navigation({ onNavigate, mobile = false }: { onNavigate?: () => void; mobile?: boolean }) {
  return (
    <nav className={cn("mt-10 space-y-1", mobile && "mt-0 border-t border-slate-800 px-4 py-3")} aria-label="Huvudmeny">
      {navigation.map(({ to, label, icon: Icon, end }) => (
        <NavLink
          key={to}
          to={to}
          end={end}
          onClick={onNavigate}
          className={({ isActive }) =>
            cn(
              "flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium transition-colors",
              isActive
                ? "bg-cyan-400/10 text-cyan-300"
                : "text-slate-400 hover:bg-slate-900 hover:text-slate-100",
            )
          }
        >
          <Icon size={19} />
          {label}
        </NavLink>
      ))}
    </nav>
  );
}
