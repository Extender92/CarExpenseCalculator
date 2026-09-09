import { Link } from "react-router-dom";
import { ReportPreview } from "@/features/comparison/Report";
import { useComparison } from "@/features/comparison/use-comparison";

export function ComparisonReportPage() {
  const { state } = useComparison();
  if (state.report) return <ReportPreview report={state.report} />;
  return (
    <main className="report-page">
      <div className="report-actions">
        <h1>Rapportunderlaget finns inte kvar</h1>
        <p role="status">
          {state.reportInvalidated
            ? "En bil i rapporten har raderats. Beräkna jämförelsen på nytt och öppna en ny rapport."
            : "Rapporter finns endast i den här flikens minne. Öppna en ny rapport från den aktuella jämförelsen."}
        </p>
        <Link to="/search">Tillbaka till jämförelsen</Link>
      </div>
    </main>
  );
}
