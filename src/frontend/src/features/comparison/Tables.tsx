import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Amount } from "@/features/household/Results";
import { fieldLabel, labels, paymentLabel } from "@/features/household/labels";
import { formatMoney, formatNumeric } from "@/features/household/numbers";
import { fuels, units } from "@/features/household/form-model";
import { panelClass } from "@/features/household/Fields";
import type { SectionResult } from "@/features/household/api";
import type { ComparisonResponse, Result, Schema } from "./api";
import { criteria, eligibilityLabels, labelFor, reasonText } from "./catalogue";
import { Evidence } from "./FactsEditor";
import { economicLink, errorTarget } from "./navigation";

const linkClass = "text-cyan-300 underline underline-offset-4";
type Column = { label: string; render: (r: Result) => ReactNode };
function Table({
  caption,
  rows,
  columns,
  select,
}: {
  caption: string;
  rows: Result[];
  columns: Column[];
  select: (id: string, field?: string) => void;
}) {
  return (
    <div
      className="max-w-full overflow-x-auto rounded-lg border border-slate-700"
      tabIndex={0}
      role="region"
      aria-label={caption}
    >
      <table className="w-full text-left text-sm">
        <caption className="p-3 text-left font-semibold">{caption}</caption>
        <thead>
          <tr>
            <th scope="col" className="sticky left-0 z-10 bg-slate-900 p-3">
              Bil
            </th>
            {columns.map((c) => (
              <th scope="col" key={c.label} className="min-w-36 p-3 align-top">
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.vehicleId}
              data-vehicle-id={r.vehicleId}
              className="border-t border-slate-700"
            >
              <th
                scope="row"
                className="sticky left-0 bg-slate-900 p-3 align-top"
              >
                <button
                  type="button"
                  className={linkClass}
                  onClick={() => select(r.vehicleId)}
                >
                  {r.registrationNumber}
                </button>
              </th>
              {columns.map((c) => (
                <td className="p-3 align-top" key={c.label}>
                  {c.render(r)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
function Cost({
  value,
  result,
  manual,
}: {
  value: SectionResult;
  result: Result;
  manual: boolean;
}) {
  return (
    <div>
      <Amount value={value} />
      {value.state === "invalid" && (
        <p className="text-rose-300">Ogiltigt underlag</p>
      )}
      {(value.missingComponents.length > 0 || value.errors.length > 0) && (
        <ul className="mt-2 space-y-1 text-xs">
          {[
            ...value.missingComponents.map((path) => ({ path, code: path })),
            ...value.errors,
          ].map(({ path, code }, i) => (
            <li key={`${path}-${i}`}>
              <Link
                className={linkClass}
                to={economicLink(
                  result.vehicleId,
                  manual,
                  errorTarget(result, path),
                )}
              >
                {reasonText[code] ?? fieldLabel(path)}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
function Category({
  result,
  name,
  manual,
}: {
  result: Result;
  name: "tax" | "insurance" | "service" | "repairs" | "customCosts";
  manual: boolean;
}) {
  const section = result.cost[name];
  return (
    <div>
      {section.isIncluded && (
        <p className="text-xs text-slate-300">
          Ingår, eventuella tillägg nedan
        </p>
      )}
      <Cost value={section.cost} result={result} manual={manual} />
      {section.items.length > 0 && (
        <details className="mt-2">
          <summary className="cursor-pointer">Kostnadsposter</summary>
          {section.items.map((item) => (
            <div className="mt-2" key={item.key}>
              <p>{item.label}</p>
              <Cost value={item.cost} result={result} manual={manual} />
            </div>
          ))}
        </details>
      )}
    </div>
  );
}
function Score({ value }: { value: Schema<"ScoreRange"> | null }) {
  return value ? (
    <span>
      [{formatNumeric(value.lower)}, {formatNumeric(value.upper)}]
    </span>
  ) : (
    <span>Ingen poäng</span>
  );
}
const budgetLabels = {
  notConfigured: "Ingen budgetgräns angiven",
  withinLimit: "Inom budget",
  exceeded: "Budgeten överskrids",
  unknown: "Kan inte bedömas ännu",
  invalid: "Ogiltig budget",
};
const detailPanels = [
  "Krav, prioriteringar och källor",
  "Finansiering och värdeminskning",
  "Energi, skatt och försäkring",
  "Service, reparationer och egna kostnader",
  "Leasing",
  "Betalningar och budgetar",
  "Känslighetsanalys",
];

export function ComparisonTables({
  rows,
  response,
  stale,
  manual,
  expanded,
  onExpanded,
  select,
}: {
  rows: Result[];
  response: ComparisonResponse | null;
  stale: boolean;
  manual: boolean;
  expanded: string[];
  onExpanded: (expanded: string[]) => void;
  select: (id: string, field?: string) => void;
}) {
  const cost = (r: Result, value: SectionResult) => (
    <Cost value={value} result={r} manual={manual} />
  );
  const criterionEditor = (r: Result, key: string) => {
    const criterion = criteria.find((c) => c.key === key);
    if (
      criterion?.fact &&
      !(
        key === "purchasePriceSek" &&
        r.effectiveCostInput?.acquisitionType === "purchase"
      )
    )
      return (
        <button
          className={linkClass}
          onClick={() =>
            select(
              r.vehicleId,
              `vehicle.${r.vehicleId}.facts.edits.${criterion.fact}.kind`,
            )
          }
        >
          Granska {labelFor(key)}
        </button>
      );
    return (
      <Link
        className={linkClass}
        to={economicLink(
          r.vehicleId,
          manual,
          criterion?.kind === "budget" ? `profile.${key}Sek` : "input.priceSek",
        )}
      >
        Granska ekonomiskt underlag
      </Link>
    );
  };
  const main: Column[] = [
    {
      label: "Underlag och krav",
      render: (r) => (
        <>
          <p>{eligibilityLabels[r.eligibility]}</p>
          <p className="text-xs text-slate-400">
            {r.storedInputState === "legacyPending"
              ? "Äldre underlag behöver övergång"
              : r.storedInputState === "listingOnly"
                ? "Annonsbil"
                : manual
                  ? "Manuellt underlag"
                  : "Aktuellt underlag"}
          </p>
          {r.needsListingReview && <p>Annonsen behöver granskas</p>}
          {Object.values(r.unsaved).some(Boolean) && (
            <p className="text-amber-200">Osparade antaganden</p>
          )}
        </>
      ),
    },
    {
      label: "Ägandekostnad för perioden",
      render: (r) => (
        <>
          {cost(r, r.cost.totals.ownershipCost)}
          {!stale && r.isCheapestEligibleComplete && (
            <p className="mt-1 text-emerald-300">
              Billigast bland godkända kompletta alternativ
            </p>
          )}
        </>
      ),
    },
    {
      label: "Kostnad per månad",
      render: (r) => cost(r, r.cost.totals.monthlyCost),
    },
    {
      label: "Kostnad per mil",
      render: (r) => cost(r, r.cost.totals.costPerMil),
    },
    {
      label: "Poängintervall",
      render: (r) => (
        <>
          <Score value={r.score} />
          {r.scoreUnavailableReason && (
            <p>
              {reasonText[r.scoreUnavailableReason] ??
                "Poängen kan inte bedömas."}
            </p>
          )}
          {!stale && r.isDefinitePreferenceWinner && (
            <p className="text-emerald-300">Säker preferensvinnare</p>
          )}
        </>
      ),
    },
    {
      label: "Datatäckning",
      render: (r) =>
        r.coveragePercent == null
          ? "Ingen aktiv prioritering"
          : `${formatNumeric(r.coveragePercent)} %`,
    },
    {
      label: "Redigera",
      render: (r) => (
        <Link className={linkClass} to={economicLink(r.vehicleId, manual)}>
          Ekonomiskt underlag
        </Link>
      ),
    },
  ];
  const panels: Column[][] = [
    [
      {
        label: "Hårda krav",
        render: (r) => (
          <div className="space-y-3">
            {r.hardRules.length === 0 && <p>Inga aktiva krav.</p>}
            {r.hardRules.map((rule) => (
              <div key={rule.rule.criterionKey}>
                <p className="font-medium">
                  {labelFor(rule.rule.criterionKey)} ·{" "}
                  {rule.state === "pass"
                    ? "Uppfyllt"
                    : rule.state === "fail"
                      ? "Uppfylls inte"
                      : "Behöver verifieras"}
                </p>
                <p>{rule.explanation}</p>
                {criterionEditor(r, rule.rule.criterionKey)}
                <p>
                  Kräver:{" "}
                  {rule.assessment.minimumEvidence === "advertised"
                    ? "Annonsuppgift"
                    : rule.assessment.minimumEvidence === "userConfirmed"
                      ? "Användarbekräftelse"
                      : "Registerverifiering"}
                </p>
                {rule.assessment.evidence.map((e, i) => (
                  <Evidence key={i} evidence={e} />
                ))}
              </div>
            ))}
          </div>
        ),
      },
      {
        label: "Prioriteringar",
        render: (r) => (
          <div className="space-y-3">
            {r.contributions.map((c) => (
              <div key={c.preference.criterionKey}>
                <p className="font-medium">
                  {labelFor(c.preference.criterionKey)} · Vikt{" "}
                  {c.preference.weight.text}
                </p>
                <p>{c.explanation}</p>
                {criterionEditor(r, c.preference.criterionKey)}
                <p>
                  Poäng: <Score value={c.range} /> · Viktat bidrag:{" "}
                  <Score value={c.weightedContribution} />
                </p>
                {c.assessment.evidence.map((e, i) => (
                  <Evidence key={i} evidence={e} />
                ))}
              </div>
            ))}
          </div>
        ),
      },
      {
        label: "Signaler och granskning",
        render: (r) => (
          <div className="space-y-3">
            {r.signals.map((s, i) => (
              <div key={i}>
                <p>
                  {s.kind === "positive"
                    ? "Positiv uppgift: "
                    : s.kind === "warning"
                      ? "Att granska: "
                      : "Källuppgift: "}
                  {s.explanation}
                </p>
                {s.evidence && <Evidence evidence={s.evidence} />}
              </div>
            ))}
            {r.unresolvedLegacyItems.map((item) => (
              <p key={item.input.key}>
                <Link
                  className={linkClass}
                  to={economicLink(r.vehicleId, manual, "review")}
                >
                  {item.input.label}: {fieldLabel(item.reason)}
                </Link>{" "}
                · Berör {item.affectedSections.map(fieldLabel).join(", ")}
              </p>
            ))}
            {r.errors.map((e, i) => (
              <p key={i}>
                {labelFor(e.path.split(".").pop() ?? "")}:{" "}
                {reasonText[e.code] ?? "Uppgiften behöver granskas."}{" "}
                <button
                  className={linkClass}
                  onClick={() => select(r.vehicleId)}
                >
                  Öppna biluppgifter
                </button>
              </p>
            ))}
          </div>
        ),
      },
    ],
    [
      {
        label: "Finansiering",
        render: (r) => (
          <>
            {cost(r, r.cost.financing)}
            <dl className="mt-2 space-y-2">
              {[
                [
                  "Kontantbetalning",
                  r.cost.financingDetails?.allocation?.cashAppliedSek,
                ],
                [
                  "Lånebelopp",
                  r.cost.financingDetails?.allocation?.principalSek,
                ],
                ["Ränta", r.cost.financingDetails?.loan?.interestPaidSek],
                [
                  "Amortering",
                  r.cost.financingDetails?.loan?.principalRepaidSek,
                ],
                [
                  "Kvarvarande skuld",
                  r.cost.financingDetails?.loan?.remainingPrincipalSek,
                ],
                [
                  "Lånebetalning per månad",
                  r.cost.financingDetails?.loan?.monthlyInstallmentSek,
                ],
              ].map(([label, amount]) => (
                <div key={String(label)}>
                  <dt>{String(label)}</dt>
                  <dd>
                    {formatMoney(
                      amount as typeof r.cost.depreciation.residualValueSek,
                    )}
                  </dd>
                </div>
              ))}
            </dl>
          </>
        ),
      },
      {
        label: "Värdeminskning",
        render: (r) => (
          <>
            {cost(r, r.cost.depreciation.cost)}
            <p>
              Restvärde: {formatMoney(r.cost.depreciation.residualValueSek)}
            </p>
          </>
        ),
      },
      {
        label: "Slutligt eget kapital",
        render: (r) => cost(r, r.cost.totals.endEquity),
      },
    ],
    [
      {
        label: "Energi",
        render: (r) => (
          <>
            {r.cost.energy.isIncluded && <p>Ingår</p>}
            {cost(r, r.cost.energy.cost)}
            {r.cost.energy.sources.map((s) => (
              <div className="mt-3" key={s.key}>
                <p>
                  {fuels.find(([k]) => k === s.fuel)?.[1] ?? "Okänt bränsle"}:{" "}
                  {formatNumeric(s.purchasedQuantity, 3)}{" "}
                  {units.find(([k]) => k === s.unit)?.[1] ?? "okänd enhet"}{" "}
                  inköpt
                </p>
                <p>
                  Använt: {formatNumeric(s.baseQuantity, 3)} · Pris:{" "}
                  {formatMoney(s.effectivePricePerUnitSek)}
                </p>
                {cost(r, s.cost)}
              </div>
            ))}
          </>
        ),
      },
      ...(["tax", "insurance"] as const).map((name) => ({
        label: labels[name],
        render: (r: Result) => (
          <Category result={r} name={name} manual={manual} />
        ),
      })),
    ],
    [
      ...(["service", "repairs", "customCosts"] as const).map((name) => ({
        label: labels[name],
        render: (r: Result) => (
          <Category result={r} name={name} manual={manual} />
        ),
      })),
      {
        label: "Ytterligare reparationsreserv",
        render: (r) => (
          <>
            {cost(r, r.cost.repairAllowance)}
            <p className="text-xs text-slate-400">
              Internt sparande, separat från verkstadsbetalningar.
            </p>
          </>
        ),
      },
    ],
    [
      {
        label: "Avtalskostnad",
        render: (r) => (
          <>
            {cost(r, r.cost.lease.cost)}
            {r.cost.lease.isEstimate && (
              <p>Uttryckligt uppskattade avtalsbetalningar</p>
            )}
          </>
        ),
      },
      {
        label: "Avtalstäckning",
        render: (r) =>
          r.cost.acquisitionType === "lease" ? (
            <>
              <p>
                {r.cost.lease.coveredMonths?.text ?? "Okänd"} månader täcks av{" "}
                {r.cost.lease.termMonths?.text ?? "okänd"} avtalsmånader.
              </p>
              <p>
                Överkörning:{" "}
                {formatNumeric(r.cost.lease.excessDistanceKilometres, 3)} km
              </p>
            </>
          ) : (
            "Ej tillämpligt"
          ),
      },
      {
        label: "Deposition",
        render: (r) => (
          <>
            <p>Utbetald:</p>
            {cost(r, r.cost.reconciliation.depositPaid)}
            <p>Återbetald:</p>
            {cost(r, r.cost.reconciliation.depositRefund)}
            <p>Innehållen kostnad:</p>
            {cost(r, r.cost.lease.depositWithheld)}
          </>
        ),
      },
    ],
    [
      {
        label: "Betalningar",
        render: (r) => (
          <>
            <p>Externa utbetalningar</p>
            {cost(r, r.cost.payments.externalOutflow)}
            <p>Återbetalningar</p>
            {cost(r, r.cost.payments.externalInflow)}
            <p>Internt reparationssparande</p>
            {cost(r, r.cost.payments.internalSaving)}
            <details className="mt-3">
              <summary className="cursor-pointer">
                Kalender och underlag
              </summary>
              <p>
                Start (0) redovisas separat. Energi är jämnt fördelad som
                uppskattning.
              </p>
              <table className="my-3 text-xs">
                <caption>Betalningar för {r.registrationNumber}</caption>
                <thead>
                  <tr>
                    {["Månad", "Utbetalning", "Återbetalning", "Sparande"].map(
                      (t) => (
                        <th scope="col" className="p-2" key={t}>
                          {t}
                        </th>
                      ),
                    )}
                  </tr>
                </thead>
                <tbody>
                  {r.cost.payments.months.map((m) => (
                    <tr key={m.monthOffset.text}>
                      <th scope="row" className="p-2">
                        {m.monthOffset.text === "0"
                          ? "Start (0)"
                          : m.calendarMonth
                            ? `${m.calendarMonth.year.text}-${m.calendarMonth.month.text.padStart(2, "0")}`
                            : `Månad ${m.monthOffset.text}`}
                      </th>
                      <td className="p-2">
                        <Amount value={m.outflow} />
                      </td>
                      <td className="p-2">
                        <Amount value={m.inflow} />
                      </td>
                      <td className="p-2">
                        <Amount value={m.internalSaving} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {r.cost.payments.sources.map((s) => (
                <div key={s.key}>
                  <p>
                    {paymentLabel(s.label)} · {fieldLabel(s.category)} ·{" "}
                    {fieldLabel(s.direction)}
                    {s.isEstimate ? " · Uppskattning" : ""}
                  </p>
                  {cost(r, s.payments)}
                  <p>Utan fastställd tidpunkt:</p>
                  {cost(r, s.unscheduledAmount)}
                </div>
              ))}
            </details>
          </>
        ),
      },
      {
        label: "Budgetar",
        render: (r) => (
          <div className="space-y-3">
            {(["startupBudget", "monthlyBudget"] as const).map((key) => (
              <div key={key}>
                <p className="font-medium">
                  {labels[key]}: {budgetLabels[r.cost[key].status]}
                </p>
                <p>Gräns: {formatMoney(r.cost[key].limitSek)}</p>
                {cost(r, r.cost[key].fundingRequired)}
              </div>
            ))}
            <p className="text-xs">
              Månadsbudgeten gäller genomsnittliga utbetalningar plus
              reparationssparande. Återbetalning minskar inte behovet.
            </p>
          </div>
        ),
      },
      {
        label: "Kostnadsavstämning",
        render: (r) => (
          <details>
            <summary className="cursor-pointer">Kostnad och kassaflöde</summary>
            {Object.entries(r.cost.reconciliation).map(([key, value]) => (
              <div className="mt-2" key={key}>
                <p>{labels[key]}</p>
                {cost(r, value)}
              </div>
            ))}
          </details>
        ),
      },
    ],
    (["favorable", "baseline", "cautious"] as const).map((mode) => ({
      label: labels[mode],
      render: (r: Result) => {
        const v = response?.views[mode].candidates.find(
          (c) => c.vehicleId === r.vehicleId,
        );
        return v ? (
          <>
            {cost(v, v.cost.totals.ownershipCost)}
            <p>
              Poäng: <Score value={v.score} />
            </p>
          </>
        ) : (
          "Inget aktuellt resultat"
        );
      },
    })),
  ];
  return (
    <div className="min-w-0 space-y-4" aria-busy={stale}>
      <Table
        caption="Huvudjämförelse"
        rows={rows}
        columns={main}
        select={select}
      />
      <div className="flex flex-wrap gap-3">
        <Button variant="secondary" onClick={() => onExpanded(detailPanels)}>
          Öppna alla
        </Button>
        <Button variant="secondary" onClick={() => onExpanded([])}>
          Stäng alla
        </Button>
      </div>
      {detailPanels.map((title, i) => (
        <details
          key={title}
          className={`${panelClass} min-w-0`}
          open={expanded.includes(title)}
          onToggle={(event) => {
            if (event.currentTarget.open !== expanded.includes(title))
              onExpanded(
                event.currentTarget.open
                  ? [...expanded, title]
                  : expanded.filter((x) => x !== title),
              );
          }}
        >
          <summary className="cursor-pointer font-semibold">{title}</summary>
          {expanded.includes(title) && (
            <div className="mt-3">
              <Table
                caption={title}
                rows={rows}
                columns={panels[i]}
                select={select}
              />
            </div>
          )}
        </details>
      ))}
    </div>
  );
}
