import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import { ManualCalculatorPage } from "@/pages/ManualCalculatorPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";

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
        <Route
          path="analyze-urls"
          element={
            <PlaceholderPage
              type="urls"
              eyebrow="Planerad funktion"
              title="Analysera URL:er"
              description="Här kommer du att kunna klistra in en eller flera annonslänkar, komplettera saknade uppgifter och granska bilarna mot samma regler."
              roadmap="URL-analysen är fas två och återanvänder reglerna och kostnadsmodellen från den manuella kalkylen."
            />
          }
        />
        <Route path="manual" element={<ManualCalculatorPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
