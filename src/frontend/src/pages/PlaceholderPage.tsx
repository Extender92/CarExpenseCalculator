import type { LucideIcon } from "lucide-react";
import { ArrowLeft, Calculator, Link2, Search } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

const pageIcons: Record<string, LucideIcon> = {
  search: Search,
  urls: Link2,
  manual: Calculator,
};

interface PlaceholderPageProps {
  type: keyof typeof pageIcons;
  eyebrow: string;
  title: string;
  description: string;
  roadmap: string;
}

export function PlaceholderPage({ type, eyebrow, title, description, roadmap }: PlaceholderPageProps) {
  const Icon = pageIcons[type];

  return (
    <div className="mx-auto max-w-3xl py-8 sm:py-20">
      <Link to="/" className="mb-8 inline-flex items-center gap-2 text-sm font-medium text-slate-400 hover:text-white">
        <ArrowLeft size={16} /> Till översikten
      </Link>
      <Card className="overflow-hidden">
        <div className="h-1 bg-gradient-to-r from-cyan-400 via-blue-400 to-violet-400" />
        <CardContent className="p-8 sm:p-12">
          <span className="grid size-14 place-items-center rounded-2xl bg-cyan-400/10 text-cyan-300">
            <Icon size={28} />
          </span>
          <Badge variant="muted" className="mt-8">{eyebrow}</Badge>
          <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-4xl">{title}</h1>
          <p className="mt-5 text-base leading-7 text-slate-400">{description}</p>
          <div className="mt-8 rounded-xl border border-slate-800 bg-slate-950/60 p-5">
            <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400">Roadmap</p>
            <p className="mt-2 text-sm leading-6 text-slate-300">{roadmap}</p>
          </div>
          <Link to="/" className={cn(buttonVariants({ variant: "secondary" }), "mt-8")}>
            <ArrowLeft size={16} /> Tillbaka
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
