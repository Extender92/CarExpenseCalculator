import type { SectionResult, VehiclePreview } from "./api";
import { formatMoney, formatNumeric, numericText } from "./numbers";
import { fieldLabel, labels, paymentLabel } from "./labels";
import { fuels, units } from "./form-model";
import { translatedFieldError, type PreviewOutcome } from "./preview";
import { panelClass } from "./Fields";

const states = {
  complete: "Komplett",
  partial: "Delvis känt",
  unavailable: "Uppgifter saknas",
  invalid: "Ogiltigt underlag",
  notApplicable: "Ej tillämpligt",
};
const budgets = {
  notConfigured: "Ingen budgetgräns angiven",
  withinLimit: "Inom budget",
  exceeded: "Budgeten överskrids",
  unknown: "Kan inte bedömas ännu",
  invalid: "Ogiltig budget",
};

export function Amount({ value }: { value: SectionResult }) {
  return (
    <span>
      {value.state === "notApplicable" ? (
        "Ej tillämpligt"
      ) : value.completeTotalSek != null ? (
        formatMoney(value.completeTotalSek)
      ) : value.knownSubtotalSek != null ? (
        <>
          {formatMoney(value.knownSubtotalSek)}{" "}
          <span className="text-xs text-amber-300">känd del</span>
        </>
      ) : (
        "Okänt"
      )}
    </span>
  );
}
export function Section({
  label,
  value,
  statusOnly = false,
}: {
  label: string;
  value: SectionResult;
  statusOnly?: boolean;
}) {
  return (
    <div className="border-b border-slate-800 py-3 last:border-0">
      <div className="flex flex-wrap justify-between gap-2">
        <span>{label}</span>
        <strong>
          {statusOnly ? states[value.state] : <Amount value={value} />}
        </strong>
      </div>
      {!statusOnly && value.state !== "complete" && (
        <p className="text-xs text-slate-400">{states[value.state]}</p>
      )}
      {(value.missingComponents.length > 0 || value.errors.length > 0) && (
        <ul className="mt-1 space-y-1 text-xs text-amber-200">
          {value.missingComponents.map((path, index) => (
            <li key={`missing-${index}`}>{fieldLabel(path)}</li>
          ))}
          {value.errors.map((error, index) => (
            <li key={`error-${index}`}>
              {fieldLabel(error.path)}: {translatedFieldError(error)}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
export function CostHeadline({ outcome }: { outcome?: PreviewOutcome }) {
  if (!outcome)
    return (
      <p className="text-sm text-slate-400">Ingen aktuell förhandsvisning.</p>
    );
  if (!outcome.result)
    return <p className="text-sm text-amber-200">{outcome.error}</p>;
  const totals = outcome.result.sections.totals;
  return (
    <dl className="grid gap-3 text-sm sm:grid-cols-3">
      {(["ownershipCost", "monthlyCost", "costPerMil"] as const).map((key) => (
        <div key={key}>
          <dt className="text-slate-400">{labels[key]}</dt>
          <dd className="mt-1 font-semibold">
            <Amount value={totals[key]} />
          </dd>
        </div>
      ))}
    </dl>
  );
}
export function VehicleResults({ result }: { result: VehiclePreview }) {
  const s = result.sections;
  return (
    <section className="space-y-4" aria-label="Beräkningsresultat för vald bil">
      <div className={panelClass}>
        <h3 className="text-lg font-semibold">Kostnader för vald bil</h3>
        <p className="mt-1 text-sm text-slate-400">
          {result.isCostComparable
            ? "Periodens tillämpliga kostnader är kompletta."
            : "Kalkylen är ofullständig. Kända delkostnader visas nedan."}
        </p>
        <Section label={labels.ownershipCost} value={s.totals.ownershipCost} />
        <Section label={labels.monthlyCost} value={s.totals.monthlyCost} />
        <Section label={labels.costPerMil} value={s.totals.costPerMil} />
        <Section label={labels.endEquity} value={s.totals.endEquity} />
        <p className="text-xs text-slate-400">
          Amortering och kontantbetalning är betalningar, inte ägandekostnader.
          Negativt eget kapital kan förekomma.
        </p>
      </div>
      <details className={panelClass}>
        <summary className="cursor-pointer font-semibold">
          Finansiering, värdeminskning och leasing
        </summary>
        <Section label={labels.financing} value={s.financing} />
        <Section label={labels.depreciation} value={s.depreciation.cost} />
        {s.acquisitionType === "purchase" && (
          <dl className="grid gap-2 text-sm sm:grid-cols-2">
            {[
              [
                "Kontantbetalning",
                s.financingDetails?.allocation?.cashAppliedSek,
              ],
              ["Lånebelopp", s.financingDetails?.allocation?.principalSek],
              [
                "Oanvända köpkontanter",
                s.financingDetails?.allocation?.unusedPurchaseCashSek,
              ],
              [
                "Lånebetalning per månad, utan avgift",
                s.financingDetails?.loan?.monthlyInstallmentSek,
              ],
              [
                "Amorterat under perioden",
                s.financingDetails?.loan?.principalRepaidSek,
              ],
              [
                "Kvarvarande skuld",
                s.financingDetails?.loan?.remainingPrincipalSek,
              ],
              ["Restvärde", s.depreciation.residualValueSek],
            ].map(([label, amount]) => (
              <div key={String(label)}>
                <dt className="text-slate-400">{String(label)}</dt>
                <dd>
                  {formatMoney(
                    amount as typeof s.depreciation.residualValueSek,
                  )}
                </dd>
              </div>
            ))}
          </dl>
        )}
        <Section label={labels.lease} value={s.lease.cost} />
        {s.acquisitionType === "lease" && (
          <>
            <p className="text-sm">
              Avtalstid: {numericText(s.lease.termMonths) || "okänd"} månader.
              Täckt period: {numericText(s.lease.coveredMonths) || "okänd"}{" "}
              månader.{" "}
              {s.lease.isEstimate &&
                "Avtalsbetalningarna är uttryckliga uppskattningar."}
            </p>
            <p className="text-sm">
              Överkörning: {formatNumeric(s.lease.excessDistanceKilometres, 3)}{" "}
              km
            </p>
            <Section
              label="Innehållen deposition"
              value={s.lease.depositWithheld}
            />
          </>
        )}
      </details>
      <details className={panelClass}>
        <summary className="cursor-pointer font-semibold">
          Energi och driftskostnader
        </summary>
        <Section
          label={`Energi${s.energy.isIncluded ? " (ingår)" : ""}`}
          value={s.energy.cost}
        />
        {s.energy.sources.map((source) => (
          <div
            key={source.key}
            className="mb-3 rounded-lg bg-slate-950/50 p-3 text-sm"
          >
            <p>
              {fuels.find(([fuel]) => fuel === source.fuel)?.[1] ??
                "Okänt drivmedel"}
              : använd mängd {formatNumeric(source.baseQuantity, 3)}, inköpt
              mängd {formatNumeric(source.purchasedQuantity, 3)}{" "}
              {units.find(([unit]) => unit === source.unit)?.[1] ??
                "okänd enhet"}
              .
            </p>
            <p>
              Effektivt pris: {formatMoney(source.effectivePricePerUnitSek)} per
              enhet.
            </p>
            <Section label="Energikostnad" value={source.cost} />
          </div>
        ))}
        {(
          ["tax", "insurance", "service", "repairs", "customCosts"] as const
        ).map((key) => (
          <div key={key}>
            <Section
              label={`${labels[key]}${s[key].isIncluded ? " (ingår, eventuella tillägg visas)" : ""}`}
              value={s[key].cost}
            />
            {s[key].items.map((item) => (
              <div className="pl-3" key={item.key}>
                <Section label={item.label} value={item.cost} />
              </div>
            ))}
          </div>
        ))}
        <Section
          label="Reserv för ytterligare reparationer"
          value={s.repairAllowance}
        />
      </details>
      <div className={panelClass}>
        <h3 className="font-semibold">Budgetbedömning</h3>
        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          {(["startupBudget", "monthlyBudget"] as const).map((key) => (
            <div key={key}>
              <h4 className="font-medium">
                {key === "startupBudget"
                  ? "Separat startbudget"
                  : "Löpande månadsbudget"}
              </h4>
              <p
                className={
                  s[key].status === "exceeded" || s[key].status === "invalid"
                    ? "text-rose-300"
                    : s[key].status === "withinLimit"
                      ? "text-emerald-300"
                      : "text-amber-200"
                }
              >
                {budgets[s[key].status]}
              </p>
              <p className="text-sm text-slate-400">
                Gräns: {formatMoney(s[key].limitSek)}
              </p>
              <Section
                label={
                  key === "startupBudget"
                    ? "Startbehov, utöver bilköpets kontanter"
                    : "Genomsnittligt finansieringsbehov"
                }
                value={s[key].fundingRequired}
              />
            </div>
          ))}
        </div>
        <p className="text-xs text-slate-400">
          Månadsbudgeten avser genomsnittliga utbetalningar plus
          reparationssparande. Återbetalningar minskar inte budgetbehovet.
        </p>
      </div>
      <details className={panelClass}>
        <summary className="cursor-pointer font-semibold">
          Betalningskalender
        </summary>
        <p className="mt-3 text-sm text-slate-400">
          Månad 0 är en separat startpost. Energi fördelas jämnt som en
          uppskattning. Reparationssparande är intern överföring.
        </p>
        <Section
          label="Kalenderns fullständighet"
          value={s.payments.calendarStatus}
          statusOnly
        />
        <Section
          label="Externa utbetalningar"
          value={s.payments.externalOutflow}
        />
        <Section label="Återbetalningar" value={s.payments.externalInflow} />
        <Section
          label="Internt reparationssparande"
          value={s.payments.internalSaving}
        />
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Betalningar per månad</caption>
            <thead>
              <tr>
                {[
                  "Månad",
                  "Utbetalningar",
                  "Återbetalningar",
                  "Reparationssparande",
                ].map((text) => (
                  <th scope="col" key={text} className="p-2">
                    {text}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {s.payments.months.map((month) => (
                <tr
                  key={month.monthOffset.text}
                  className="border-t border-slate-800"
                >
                  <th scope="row" className="p-2 font-normal">
                    {month.monthOffset.text === "0"
                      ? "Start (0)"
                      : month.calendarMonth
                        ? `${month.calendarMonth.year.text}-${month.calendarMonth.month.text.padStart(2, "0")}`
                        : `Månad ${month.monthOffset.text}`}
                    <details>
                      <summary className="cursor-pointer text-xs text-cyan-300">
                        Kategorier
                      </summary>
                      {month.categories.map((category, index) => (
                        <p className="py-1 text-xs" key={index}>
                          {fieldLabel(category.category)} ·{" "}
                          {labels[category.direction]}:{" "}
                          <Amount value={category.amount} />
                        </p>
                      ))}
                    </details>
                  </th>
                  <td className="p-2">
                    <Amount value={month.outflow} />
                  </td>
                  <td className="p-2">
                    <Amount value={month.inflow} />
                  </td>
                  <td className="p-2">
                    <Amount value={month.internalSaving} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <details className="mt-4">
          <summary className="cursor-pointer text-sm font-semibold">
            Betalningarnas underlag och saknade tidpunkter
          </summary>
          {s.payments.sources.map((source) => (
            <div key={source.key}>
              <Section
                label={`${paymentLabel(source.label)}${source.isEstimate ? " (uppskattning)" : ""}`}
                value={source.payments}
              />
              <p className="text-xs text-slate-400">
                {fieldLabel(source.category)} · {labels[source.direction]} ·
                Månader:{" "}
                {source.monthOffsets.map((month) => month.text).join(", ") ||
                  "tidpunkt saknas"}
              </p>
              <Section
                label="Belopp utan fastställd tidpunkt"
                value={source.unscheduledAmount}
              />
            </div>
          ))}
        </details>
      </details>
      <details className={panelClass}>
        <summary className="cursor-pointer font-semibold">
          Avstämning mellan kostnad och kassaflöde
        </summary>
        <p className="mt-3 text-sm text-slate-400">
          Kontantbetalning och amortering bygger eget kapital. Värdeminskning,
          periodisering, deposition och reparationsreserv förklarar skillnaden
          mellan kostnad och pengar som betalas.
        </p>
        {Object.entries(s.reconciliation).map(([key, value]) => (
          <Section
            key={key}
            label={labels[key] ?? fieldLabel(key)}
            value={value}
          />
        ))}
      </details>
    </section>
  );
}
