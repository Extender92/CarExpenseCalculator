import { ListingContent } from "@/features/url-analysis/ListingContent";
import { memo, useEffect, useRef, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { formatNumeric } from "@/features/household/numbers";
import { labels } from "@/features/household/labels";
import { eligibilityLabels, reasonText } from "./catalogue";
import {
  amountText,
  budgetLabels,
  inputRows,
  reportLabel,
  scoreText,
} from "./report-format";
import {
  reportRows,
  type ComparisonReportInput,
  type Immutable,
} from "./report-model";
import type { Result } from "./api";
import "./report.css";

function Table({
  title,
  headings,
  children,
}: {
  title: string;
  headings: string[];
  children: ReactNode;
}) {
  return (
    <div
      className="report-table-scroll"
      tabIndex={0}
      role="region"
      aria-label={title}
    >
      <table aria-label={title}>
        <thead>
          <tr>
            <th colSpan={headings.length} className="report-table-title">
              {title}
            </th>
          </tr>
          <tr>
            {headings.map((h) => (
              <th scope="col" key={h}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}
function DataTable({
  title,
  value,
  exact = true,
}: {
  title: string;
  value: unknown;
  exact?: boolean;
}) {
  return (
    <Table title={title} headings={["Uppgift", "Värde / underlag"]}>
      {inputRows(value, exact).map((r) => (
        <tr key={r.path} data-report-path={r.path}>
          <th scope="row">{r.label || "Underlag"}</th>
          <td>{r.value}</td>
        </tr>
      ))}
    </Table>
  );
}
const dirtyNames = {
  profile: "hushållsprofil",
  rules: "köpkrav/prioriteringar",
  facts: "biluppgifter",
  costs: "ekonomiskt underlag",
  reviewDecisions: "granskningsbeslut",
  costConfirmation: "kostnadsbekräftelse",
  listingReview: "annonsgranskning",
};
function unsaved(r: Immutable<Result>) {
  return Object.entries(r.unsaved)
    .filter(([, value]) => value)
    .map(([key]) => dirtyNames[key as keyof typeof dirtyNames])
    .join(", ");
}
function recommendation(r: Immutable<Result>) {
  return (
    <>
      {r.isCheapestEligibleComplete && (
        <p>Billigast bland godkända kompletta alternativ</p>
      )}
      {r.isDefinitePreferenceWinner && <p>Säker preferensvinnare</p>}
    </>
  );
}

function Calendar({ result: r }: { result: Immutable<Result> }) {
  return (
    <Table
      title={`${r.registrationNumber} – Betalningskalender`}
      headings={[
        "Månad / kategori",
        "Utbetalning",
        "Återbetalning",
        "Reparationssparande",
      ]}
    >
      {r.cost.payments.months.flatMap((m) => {
        const month =
          m.monthOffset.text === "0"
            ? "Start (0)"
            : m.calendarMonth
              ? `${m.calendarMonth.year.text}-${m.calendarMonth.month.text.padStart(2, "0")} (månad ${m.monthOffset.text})`
              : `Månad ${m.monthOffset.text} – kalendermånad okänd`;
        return [
          <tr key={m.monthOffset.text} data-report-month={m.monthOffset.text}>
            <th scope="row">{month}</th>
            <td>{amountText(m.outflow)}</td>
            <td>{amountText(m.inflow)}</td>
            <td>{amountText(m.internalSaving)}</td>
          </tr>,
          ...[
            m.outflow,
            m.inflow,
            m.internalSaving,
            ...m.categories.map((c) => c.amount),
          ].flatMap((section, i) =>
            section.errors.length || section.missingComponents.length
              ? [
                  <tr key={`${m.monthOffset.text}-errors-${i}`}>
                    <th scope="row">
                      {month} – luckor och fel {i + 1}
                    </th>
                    <td colSpan={3}>
                      {inputRows(section)
                        .map((r) => r.value)
                        .join("\n")}
                    </td>
                  </tr>,
                ]
              : [],
          ),
          ...m.categories.map((c, i) => (
            <tr key={`${m.monthOffset.text}-${i}`}>
              <th scope="row">
                {month} – {reportLabel(c.category)}
              </th>
              <td>{c.direction === "outflow" ? amountText(c.amount) : "—"}</td>
              <td>{c.direction === "inflow" ? amountText(c.amount) : "—"}</td>
              <td>
                {c.direction === "internalSaving" ? amountText(c.amount) : "—"}
              </td>
            </tr>
          )),
        ];
      })}
    </Table>
  );
}

export const ReportDocument = memo(function ReportDocument({
  report,
}: {
  report: ComparisonReportInput;
}) {
  const response = report.response;
  const view = response.views[response.activeSensitivityMode];
  const rows = reportRows(report);
  const modes = ["favorable", "baseline", "cautious"] as const;
  const byMode = Object.fromEntries(
    modes.map((mode) => [
      mode,
      new Map(response.views[mode].candidates.map((c) => [c.vehicleId, c])),
    ]),
  );
  const hasUnsaved = rows.some((r) => !!unsaved(r));
  return (
    <article className="comparison-report" aria-label="Jämförelserapport">
      <header>
        <p className="report-kicker">Car Expense Calculator</p>
        <h1>Jämförelserapport</h1>
        <p>
          Jämförelsen fångades{" "}
          {new Date(report.capturedAt).toLocaleString("sv-SE", {
            timeZone: report.timeZone,
          })}{" "}
          ({report.timeZone}).
        </p>
        <p>
          Utvärderingsdatum: {view.asOfDate} ·{" "}
          {response.mode === "stored"
            ? "Sparade bilar"
            : "Fristående manuellt underlag utan kontroll mot lagringen"}
        </p>
        <p>
          Period: {view.profile.periodMonths?.text ?? "okänd"} månader ·{" "}
          {response.candidateCount.text} bilar ·{" "}
          {labels[response.activeSensitivityMode]} ·{" "}
          {report.sort === "cost" ? "Kostnadssortering" : "Poängsortering"}
        </p>
        <p>
          Rapporten återger underlaget vid fångsttidpunkten. Ingen ny kontroll
          mot lagringen sker vid utskrift.
        </p>
        {hasUnsaved && (
          <p className="report-notice">
            <strong>Osparade antaganden ingår.</strong> Berörda uppgifter anges
            vid varje bil.
          </p>
        )}
        <p>
          {reasonText[view.preferenceRecommendationReason] ??
            view.preferenceRecommendationReason}
        </p>
      </header>

      <h2>Hela jämförelsen</h2>
      <Table
        title="Huvudjämförelse"
        headings={[
          "Bil och underlag",
          "Periodkostnad",
          "Per månad",
          "Per mil",
          "Kravutfall",
          "Poängintervall",
          "Datatäckning",
        ]}
      >
        {rows.map((r) => (
          <tr key={r.vehicleId} data-report-vehicle={r.vehicleId}>
            <th scope="row">
              {r.registrationNumber}
              <p className="report-note">
                {response.mode === "manual"
                  ? "Manuellt underlag"
                  : r.storedInputState === "legacyPending"
                    ? "Äldre underlag behöver övergång"
                    : r.storedInputState === "listingOnly"
                      ? "Annonsbil"
                      : "Aktuellt underlag"}
              </p>
              {unsaved(r) && <p className="report-note">Osparade antaganden</p>}
              {r.needsListingReview && (
                <p className="report-note">Annonsen behöver granskas</p>
              )}
            </th>
            <td>
              {amountText(r.cost.totals.ownershipCost)}
              {recommendation(r)}
            </td>
            <td>{amountText(r.cost.totals.monthlyCost)}</td>
            <td>{amountText(r.cost.totals.costPerMil)}</td>
            <td>{eligibilityLabels[r.eligibility]}</td>
            <td>
              {scoreText(r.score)}
              {r.scoreUnavailableReason && (
                <p>
                  {reasonText[r.scoreUnavailableReason] ??
                    "Poäng kan inte bedömas"}
                </p>
              )}
            </td>
            <td>
              {r.coveragePercent == null
                ? "Ingen aktiv prioritering"
                : `${formatNumeric(r.coveragePercent)} %`}
            </td>
          </tr>
        ))}
      </Table>
      <p className="report-note">
        Känd del är ingen komplett summa. Poängintervall och ordning kommer från
        beräkningen före visningsavrundning. Överlappande intervall innebär
        ingen säker rangordning.
      </p>

      <h2>Gemensamma antaganden och prioriteringar</h2>
      <p>
        Indata och poängmål återges med full decimalprecision. Tomma uppgifter
        fylls inte från ett annat osäkerhetsläge.
      </p>
      <DataTable title="Effektiv hushållsprofil" value={view.profile} />
      <DataTable
        title="Köpkrav, prioriteringar och signalinställningar"
        value={view.rules}
      />
      <p>
        Vikt 0 stänger av kriteriet för alla bilar. Okända eller otillräckligt
        verifierade kriterier behåller sin vikt i det gemensamma
        poängintervallet. Signaler ändrar inte poäng eller kravutfall.
      </p>
      {view.profileErrors.length > 0 && (
        <DataTable title="Fel i hushållsprofilen" value={view.profileErrors} />
      )}

      <h2>Alla tre osäkerhetslägen</h2>
      <Table
        title="Känslighetsanalys"
        headings={[
          "Bil / läge",
          "Periodkostnad",
          "Per månad",
          "Per mil",
          "Poängintervall",
          "Täckning",
          "Start- / månadsbudget",
        ]}
      >
        {rows.flatMap((r) =>
          modes.map((mode) => {
            const c = byMode[mode].get(r.vehicleId)!;
            return (
              <tr key={`${r.vehicleId}-${mode}`}>
                <th scope="row">
                  {r.registrationNumber} – {labels[mode]}
                </th>
                <td>{amountText(c.cost.totals.ownershipCost)}</td>
                <td>{amountText(c.cost.totals.monthlyCost)}</td>
                <td>{amountText(c.cost.totals.costPerMil)}</td>
                <td>{scoreText(c.score)}</td>
                <td>
                  {c.coveragePercent == null
                    ? "Ingen aktiv prioritering"
                    : `${formatNumeric(c.coveragePercent)} %`}
                </td>
                <td>
                  {budgetLabels[c.cost.startupBudget.status]} /{" "}
                  {budgetLabels[c.cost.monthlyBudget.status]}
                </td>
              </tr>
            );
          }),
        )}
      </Table>

      <h2>Detaljer och källunderlag per bil</h2>
      <p>
        Kostnad, externa utbetalningar och återbetalningar redovisas separat.
        Reparationsreserven är internt sparande, aldrig en extra
        verkstadsbetalning. Start (0) ingår inte i den löpande månadsbudgeten.
        Energi fördelas jämnt över täckta månader som en uppskattning.
      </p>
      {rows.map((r) => {
        const { months, ...payments } = r.cost.payments;
        const groups: [string, unknown][] = [
          [
            "Finansiering och värdeminskning",
            {
              financing: r.cost.financing,
              financingDetails: r.cost.financingDetails,
              depreciation: r.cost.depreciation,
              endEquity: r.cost.totals.endEquity,
            },
          ],
          [
            "Energi, skatt och försäkring",
            {
              energy: r.cost.energy,
              tax: r.cost.tax,
              insurance: r.cost.insurance,
            },
          ],
          [
            "Service, reparationer och egna kostnader",
            {
              service: r.cost.service,
              repairs: r.cost.repairs,
              repairAllowance: r.cost.repairAllowance,
              customCosts: r.cost.customCosts,
            },
          ],
          ["Leasing", r.cost.lease],
          ["Betalningsunderlag", payments],
          [
            "Kostnadsavstämning och budgetar",
            {
              reconciliation: r.cost.reconciliation,
              startupBudget: r.cost.startupBudget,
              monthlyBudget: r.cost.monthlyBudget,
            },
          ],
        ];
        return (
          <section key={r.vehicleId} data-report-details={r.vehicleId}>
            <h3>{r.registrationNumber}</h3>
            {unsaved(r) && (
              <p className="report-notice">Osparat: {unsaved(r)}.</p>
            )}
            {groups.map(([name, value]) => (
              <DataTable
                key={name}
                title={`${r.registrationNumber} – ${name}`}
                value={value}
                exact={false}
              />
            ))}
            {response.listings?.filter(l => l.vehicleId === r.vehicleId).map(l =>
              <ListingContent key={l.vehicleId} title={`${r.registrationNumber} – Annonsunderlag`} value={l} />)}
            {months.length > 0 && <Calendar result={r} />}
            <DataTable
              title={`${r.registrationNumber} – Effektivt ekonomiskt underlag`}
              value={r.effectiveCostInput}
            />
            <DataTable
              title={`${r.registrationNumber} – Fordonsfakta och källor`}
              value={r.effectiveFacts}
            />
            <DataTable
              title={`${r.registrationNumber} – Kravutfall och poängbidrag`}
              value={{
                hardRules: r.hardRules,
                contributions: r.contributions,
                signals: r.signals,
              }}
            />
            <DataTable
              title={`${r.registrationNumber} – Granskning och fältfel`}
              value={{
                unresolvedLegacyItems: r.unresolvedLegacyItems,
                errors: r.errors,
                inputErrors: r.cost.inputErrors,
              }}
            />
            <DataTable
              title={`${r.registrationNumber} – Källversioner och bekräftelse`}
              value={{
                sourceRevisions: r.sourceRevisions,
                costConfirmedAt: r.costConfirmedAt,
                needsListingReview: r.needsListingReview,
              }}
            />
            {modes.map((mode) => {
              const c = byMode[mode].get(r.vehicleId)!;
              return (
                <DataTable
                  key={mode}
                  title={`${r.registrationNumber} – ${labels[mode]}: resultat och luckor`}
                  exact={false}
                  value={{
                    totals: c.cost.totals,
                    startupBudget: c.cost.startupBudget,
                    monthlyBudget: c.cost.monthlyBudget,
                    inputErrors: c.cost.inputErrors,
                    errors: c.errors,
                  }}
                />
              );
            })}
          </section>
        );
      })}
      <h2>Rapportens versionsunderlag</h2>
      <dl className="report-metadata">
        <dt>Anropsidentitet</dt>
        <dd>{response.requestId}</dd>
        <dt>Beräkningsomgång</dt>
        <dd>{response.generationId}</dd>
        <dt>Baslinjens ändringskontroll</dt>
        <dd>{response.baselineToken ?? "Ej tillämpligt i manuellt läge"}</dd>
        <dt>Transport / regel / jämförelseresultat</dt>
        <dd>
          {response.transportVersion.text} / {view.ruleVersion.text} /{" "}
          {view.resultSchemaVersion.text}
        </dd>
        <dt>Hushållsberäkning / hushållsresultat</dt>
        <dd>
          {view.calculationVersion.text} /{" "}
          {view.householdResultSchemaVersion.text}
        </dd>
      </dl>
    </article>
  );
});

/** Native print may be cancelled. No afterprint handler claims a saved file. */
export function ReportPreview({ report }: { report: ComparisonReportInput }) {
  const [readyFor, setReadyFor] = useState<ComparisonReportInput | null>(null);
  const [error, setError] = useState<string | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    let cancelled = false;
    let frame = 0;
    heading.current?.focus();
    void (document.fonts?.ready ?? Promise.resolve())
      .then(() => {
        if (!cancelled)
          frame = requestAnimationFrame(() => {
            if (!cancelled) {
              setReadyFor(report);
              setError(null);
            }
          });
      })
      .catch(() => {
        if (!cancelled)
          setError(
            "Teckensnitten kunde inte förberedas. Öppna rapporten på nytt innan du skriver ut.",
          );
      });
    return () => {
      cancelled = true;
      cancelAnimationFrame(frame);
    };
  }, [report]);
  const ready = readyFor === report;
  return (
    <main className="report-page">
      <div className="report-actions">
        <h1 ref={heading} tabIndex={-1}>
          Förhandsvisning inför PDF
        </h1>
        <p>
          Välj Spara som PDF i utskriftsdialogen, alla sidor och A4 liggande.
          Utskrift sparar inte dina antaganden i programmet.
        </p>
        <button
          type="button"
          disabled={!ready}
          onClick={() => {
            if (!ready) return;
            try {
              window.print();
            } catch {
              setError(
                "Utskriftsdialogen kunde inte öppnas. Rapporten finns kvar; försök igen med webbläsarens utskriftsfunktion.",
              );
            }
          }}
        >
          Skriv ut / Spara som PDF
        </button>
        <Link to="/search">Tillbaka till jämförelsen</Link>
        <p role={error ? "alert" : "status"}>
          {error ??
            (ready
              ? "Hela rapporten är klar för utskrift."
              : "Förbereder hela rapporten och teckensnitten…")}
        </p>
      </div>
      <div
        className={ready ? "report-ready" : "report-preparing"}
        aria-busy={!ready}
      >
        <ReportDocument report={report} />
      </div>
    </main>
  );
}
