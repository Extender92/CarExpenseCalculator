import { Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense } from "react";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import { WorkspaceProvider } from "@/features/household/WorkspaceProvider";
import { ComparisonProvider } from "@/features/comparison/Provider";

const ComparisonPage = lazy(() =>
  import("@/pages/ComparisonPage").then((module) => ({
    default: module.ComparisonPage,
  })),
);

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
      <ComparisonProvider>
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
              <Route path="search" element={<ComparisonPage />} />
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
      </ComparisonProvider>
    </WorkspaceProvider>
  );
}
