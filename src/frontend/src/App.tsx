import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import { ManualCalculatorPage } from "@/pages/ManualCalculatorPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { UrlAnalysisPage } from "@/pages/UrlAnalysisPage";

export function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route
          path="search"
          element={
            <PlaceholderPage
              type="search"
              eyebrow="Planerad funktion"
              title="Regelsökning"
              description="Här kommer du att kunna söka efter bilar med hårda och mjuka regler, spara profiler och rangordna kandidater. Automatisk hämtning aktiveras först när en godkänd datakälla finns."
              roadmap="Regelmotorn och sparade sökprofiler byggs efter den manuella kalkylen och URL-analysen."
            />
          }
        />
        <Route path="analyze-urls" element={<UrlAnalysisPage />} />
        <Route path="manual" element={<ManualCalculatorPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
