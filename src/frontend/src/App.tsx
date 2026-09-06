import { Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense } from "react";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { WorkspaceProvider } from "@/features/household/WorkspaceProvider";

const ManualCalculatorPage = lazy(() =>
  import("@/pages/ManualCalculatorPage").then((module) => ({
    default: module.ManualCalculatorPage,
  })),
);
const UrlAnalysisPage = lazy(() =>
  import("@/pages/UrlAnalysisPage").then((module) => ({
    default: module.UrlAnalysisPage,
  })),
);
const HouseholdPage = lazy(() =>
  import("@/pages/HouseholdPage").then((module) => ({
    default: module.HouseholdPage,
  })),
);
const HouseholdTransitionPage = lazy(() =>
  import("@/pages/HouseholdTransitionPage").then((module) => ({
    default: module.HouseholdTransitionPage,
  })),
);

export function App() {
  return (
    <WorkspaceProvider>
      <Suspense
        fallback={
          <p role="status" className="p-8 text-slate-300">
            Läser arbetsytan…
          </p>
        }
      >
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
            <Route path="manual" element={<HouseholdPage />} />
            <Route path="manual/legacy" element={<ManualCalculatorPage />} />
            <Route
              path="manual/transition"
              element={<HouseholdTransitionPage />}
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </Suspense>
    </WorkspaceProvider>
  );
}
