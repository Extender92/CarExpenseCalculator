import { AlertTriangle, Banknote, CheckCircle2, Fuel, Landmark, ReceiptText } from "lucide-react";
import type { Ref } from "react";
import type { ManualCalculationResult } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  formatCadence,
  formatDistance,
  formatEnergyUnit,
  formatInteger,
  formatMissingCategory,
  formatQuantity,
  formatSek,
} from "./presentation";

interface ResultProps {
  result: ManualCalculationResult;
  isStale: boolean;
  summaryRef?: Ref<HTMLDivElement>;
}

export function ResultSummary({ result, isStale, summaryRef }: ResultProps) {
  const cashLabel = result.cashFlow.isComplete ? "Totalt kassaflöde" : "Känt kassaflöde";
  const netLabel = result.netOwnershipCost?.isComplete ? "Total ägandekostnad" : "Känd ägandekostnad";

  return (
    <div ref={summaryRef} tabIndex={-1} aria-labelledby="calculation-result-title" className="scroll-mt-24 outline-none">
      <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Badge variant={isStale ? "warning" : "success"}>
            {isStale ? "Behöver räknas om" : "Aktuellt resultat"}
          </Badge>
          <span className="text-xs font-medium text-slate-500">{result.calculationPeriodMonths} månader</span>
        </div>
        <CardTitle id="calculation-result-title" className="pt-2">Resultat</CardTitle>
        <CardDescription>{formatDistance(result.totalDistanceKilometres)} under perioden</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {isStale && (
          <StatusNotice variant="warning">
            Formuläret har ändrats. Resultatet nedan gäller dina tidigare värden.
          </StatusNotice>
        )}

        <HeadlineValue label={cashLabel} value={formatSek(result.cashFlow.knownTotalSek)} />
        <div className="grid grid-cols-2 gap-3">
          <SmallValue label="Per månad" value={formatSek(result.cashFlow.averagePerMonthSek)} />
          <SmallValue label="Per år" value={formatSek(result.cashFlow.averagePerYearSek)} />
        </div>

        {result.netOwnershipCost ? (
          <div className="border-t border-slate-800 pt-5">
            <HeadlineValue label={netLabel} value={formatSek(result.netOwnershipCost.knownTotalSek)} />
            <p className="mt-2 text-xs leading-5 text-slate-500">
              Inkluderar värdeminskning och ränta, utan att räkna amortering två gånger.
            </p>
          </div>
        ) : (
          <StatusNotice variant="muted">
            Ägandekostnaden kan inte beräknas utan ett förväntat restvärde.
          </StatusNotice>
        )}

        {result.completeness.isComplete ? (
          <div className="flex items-center gap-2 text-sm text-emerald-300">
            <CheckCircle2 size={17} /> Alla standardvärden är kända.
          </div>
        ) : (
          <div className="rounded-xl border border-amber-400/20 bg-amber-400/5 p-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-amber-300">
              <AlertTriangle size={17} /> Resultatet är inte komplett
            </div>
            <p className="mt-2 text-xs leading-5 text-slate-400">
              Saknas: {result.completeness.missingCategories.map(formatMissingCategory).join(", ")}.
            </p>
          </div>
        )}
      </CardContent>
      </Card>
    </div>
  );
}

export function ResultDetails({ result, isStale }: ResultProps) {
  return (
    <section className="space-y-5" aria-labelledby="result-details-title">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-semibold text-cyan-400">Deterministisk beräkning</p>
          <h2 id="result-details-title" className="mt-1 text-2xl font-bold tracking-tight">Fullständig uppdelning</h2>
        </div>
        {isStale && <Badge variant="warning">Tidigare indata</Badge>}
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <ResultCard icon={ReceiptText} title="Kassaflöde" description="Pengar som betalas under den valda perioden.">
          <MoneyRow label="Kontantköp eller kontantinsats" value={result.cashFlow.acquisitionCashPaidSek} />
          <MoneyRow label="Lånebetalningar" value={result.cashFlow.loanPaymentsDuringPeriodSek} />
          <MoneyRow label="Energi" value={result.cashFlow.energyCostSek} />
          <MoneyRow label="Fordonsskatt" value={result.cashFlow.vehicleTaxSek} />
          <MoneyRow label="Försäkring" value={result.cashFlow.insuranceSek} />
          <MoneyRow label="Underhåll och reparationer" value={result.cashFlow.maintenanceAndRepairsSek} />
          <MoneyRow label="Övriga återkommande" value={result.cashFlow.otherRecurringCostSek} />
          <MoneyRow label="Övriga engångskostnader" value={result.cashFlow.otherOneTimeCostSek} />
          <MoneyRow label="Känd driftkostnad" value={result.cashFlow.knownOperatingCostSek} emphasized />
          <MoneyRow label={result.cashFlow.isComplete ? "Totalt kassaflöde" : "Känt kassaflöde"} value={result.cashFlow.knownTotalSek} emphasized />
          <MoneyRow label="Genomsnitt per månad" value={result.cashFlow.averagePerMonthSek} />
          <MoneyRow label="Genomsnitt per år" value={result.cashFlow.averagePerYearSek} />
        </ResultCard>

        <ResultCard icon={Landmark} title="Finansiering" description="Lånets utveckling under beräkningsperioden.">
          {result.financing ? (
            <>
              <MoneyRow label="Kontantinsats" value={result.financing.downPaymentSek} />
              <MoneyRow label="Lånebelopp" value={result.financing.principalSek} />
              <TextRow label="Nominell årsränta" value={`${formatQuantity(result.financing.annualNominalInterestRatePercent)} %`} />
              <TextRow label="Löptid" value={`${result.financing.termMonths} månader`} />
              <MoneyRow label="Månadsbetalning" value={result.financing.monthlyPaymentSek} />
              <TextRow label="Betalningar under perioden" value={formatInteger(result.financing.paymentsMade)} />
              <MoneyRow label="Betalat under perioden" value={result.financing.loanPaymentsDuringPeriodSek} />
              <MoneyRow label="Amorterat" value={result.financing.principalRepaidSek} />
              <MoneyRow label="Betald ränta" value={result.financing.interestPaidSek} />
              <MoneyRow label="Kvarvarande skuld" value={result.financing.remainingPrincipalSek} emphasized />
            </>
          ) : (
            <p className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 text-sm text-slate-400">
              Kontantköp – ingen lånefinansiering ingår.
            </p>
          )}
        </ResultCard>

        <ResultCard icon={Fuel} title="Energi" description="Förbrukning och kostnad per energikälla.">
          {result.energy.sources.length === 0 ? (
            <p className="text-sm text-slate-400">Ingen energiförbrukning eftersom körsträckan är noll.</p>
          ) : (
            <div className="space-y-4">
              {result.energy.sources.map((source, index) => (
                <div key={`${source.label}-${index}`} className="rounded-xl border border-slate-800 bg-slate-950/40 p-4">
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <h4 className="font-semibold text-slate-100">{source.label}</h4>
                    <Badge variant="muted">{formatQuantity(source.distanceSharePercent)} %</Badge>
                  </div>
                  <dl className="space-y-2">
                    <TextRow label="Tilldelad körsträcka" value={formatDistance(source.allocatedDistanceKilometres)} compact />
                    <TextRow label="Förbrukning per 100 km" value={`${formatQuantity(source.consumptionPer100Kilometres)} ${formatEnergyUnit(source.unit)}`} compact />
                    <TextRow label="Förbrukad mängd" value={`${formatQuantity(source.consumedQuantity)} ${formatEnergyUnit(source.unit)}`} compact />
                    <MoneyRow label="Pris per enhet" value={source.pricePerUnitSek} compact />
                    <MoneyRow label="Kostnad" value={source.costSek} compact emphasized />
                  </dl>
                </div>
              ))}
              <MoneyRow label="Total energikostnad" value={result.energy.totalCostSek} emphasized />
            </div>
          )}
        </ResultCard>

        <ResultCard icon={Banknote} title="Övriga kostnader" description="Egna återkommande kostnader och engångsposter.">
          {result.otherRecurringCosts.length === 0 && result.otherOneTimeCosts.length === 0 ? (
            <p className="text-sm text-slate-400">Inga övriga kostnader angavs.</p>
          ) : (
            <div className="space-y-4">
              {result.otherRecurringCosts.map((cost, index) => (
                <div key={`${cost.label}-${index}`} className="rounded-xl border border-slate-800 bg-slate-950/40 p-4">
                  <MoneyRow label={cost.label} value={cost.costDuringPeriodSek} emphasized compact />
                  <p className="mt-2 text-xs text-slate-500">
                    {formatSek(cost.amountSek)} {formatCadence(cost.cadence)}
                  </p>
                </div>
              ))}
              {result.otherOneTimeCosts.map((cost, index) => (
                <div key={`${cost.label}-${index}`} className="rounded-xl border border-slate-800 bg-slate-950/40 p-4">
                  <MoneyRow label={cost.label} value={cost.amountSek} emphasized compact />
                  <p className="mt-2 text-xs text-slate-500">Engångskostnad</p>
                </div>
              ))}
            </div>
          )}
        </ResultCard>

        <div className="xl:col-span-2">
          <ResultCard icon={Banknote} title="Nettoägandekostnad" description="Värdeminskning, ränta och drift utan dubbelräknad amortering.">
            {result.netOwnershipCost ? (
              <div className="grid gap-x-8 gap-y-0 md:grid-cols-2">
                <MoneyRow label="Förväntat restvärde" value={result.netOwnershipCost.residualValueSek} />
                <MoneyRow label="Värdeminskning" value={result.netOwnershipCost.depreciationSek} />
                <MoneyRow label="Betald ränta" value={result.netOwnershipCost.interestPaidSek} />
                <MoneyRow label="Känd driftkostnad" value={result.netOwnershipCost.knownOperatingCostSek} />
                <MoneyRow label={result.netOwnershipCost.isComplete ? "Total ägandekostnad" : "Känd ägandekostnad"} value={result.netOwnershipCost.knownTotalSek} emphasized />
                <MoneyRow label="Beräknat eget kapital vid periodens slut" value={result.netOwnershipCost.estimatedEquityAtPeriodEndSek} emphasized />
                <MoneyRow label="Genomsnitt per månad" value={result.netOwnershipCost.averagePerMonthSek} />
                <MoneyRow label="Genomsnitt per år" value={result.netOwnershipCost.averagePerYearSek} />
              </div>
            ) : (
              <StatusNotice variant="warning">
                Ange ett förväntat restvärde och beräkna igen för att få nettoägandekostnaden.
              </StatusNotice>
            )}
          </ResultCard>
        </div>
      </div>
    </section>
  );
}

function ResultCard({ icon: Icon, title, description, children }: { icon: typeof Banknote; title: string; description: string; children: React.ReactNode }) {
  return (
    <Card className="h-full">
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-400/10 text-cyan-300"><Icon size={20} /></span>
          <div><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></div>
        </div>
      </CardHeader>
      <CardContent><dl className="space-y-1">{children}</dl></CardContent>
    </Card>
  );
}

function HeadlineValue({ label, value }: { label: string; value: string }) {
  return <div><p className="text-sm font-medium text-slate-400">{label}</p><p className="mt-1 text-3xl font-bold tracking-tight text-white">{value}</p></div>;
}

function SmallValue({ label, value }: { label: string; value: string }) {
  return <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-3"><p className="text-xs text-slate-500">{label}</p><p className="mt-1 text-sm font-semibold text-slate-200">{value}</p></div>;
}

function MoneyRow({ label, value, emphasized = false, compact = false }: { label: string; value: number | null; emphasized?: boolean; compact?: boolean }) {
  return <TextRow label={label} value={value === null ? "Okänd" : formatSek(value)} emphasized={emphasized} compact={compact} unknown={value === null} />;
}

function TextRow({ label, value, emphasized = false, compact = false, unknown = false }: { label: string; value: string; emphasized?: boolean; compact?: boolean; unknown?: boolean }) {
  return (
    <div className={`flex items-start justify-between gap-4 border-b border-slate-800/70 last:border-0 ${compact ? "py-1.5" : "py-2.5"}`}>
      <dt className={`text-sm ${emphasized ? "font-semibold text-slate-200" : "text-slate-400"}`}>{label}</dt>
      <dd className={`text-right text-sm ${unknown ? "text-amber-300" : emphasized ? "font-semibold text-white" : "text-slate-200"}`}>{value}</dd>
    </div>
  );
}

function StatusNotice({ variant, children }: { variant: "warning" | "muted"; children: React.ReactNode }) {
  return <div className={variant === "warning" ? "rounded-xl border border-amber-400/20 bg-amber-400/5 p-4 text-sm leading-6 text-amber-200" : "rounded-xl border border-slate-800 bg-slate-950/40 p-4 text-sm leading-6 text-slate-400"}>{children}</div>;
}
