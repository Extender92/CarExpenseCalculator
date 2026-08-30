import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { getSystemStatus } from "@/api/client";
import { App } from "./App";

vi.mock("@/api/client", () => ({
  getSystemStatus: vi.fn(),
}));

const healthyStatus = {
  version: "1.0.0",
  status: "healthy",
  database: "available",
  features: {
    ruleBasedSearch: false,
    urlAnalysis: false,
    manualCalculator: true,
    aiReview: false,
  },
};

function mockStatus(payload = healthyStatus) {
  vi.mocked(getSystemStatus).mockResolvedValue(payload);
}

function renderApp(initialEntry = "/") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <App />
    </MemoryRouter>,
  );
}

describe("Bilverktyget", () => {
  it("shows the Swedish dashboard and all three modes", async () => {
    mockStatus();
    renderApp();

    expect(screen.getByRole("heading", { name: /ett bättre beslutsunderlag/i })).toBeInTheDocument();
    expect(screen.getAllByText("Regelsökning").length).toBeGreaterThan(0);
    expect(screen.getAllByText("URL-analys").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Manuell kalkyl").length).toBeGreaterThan(0);
    expect(await screen.findByText("Systemet är friskt")).toBeInTheDocument();
    expect(screen.getAllByText("Tillgänglig").length).toBeGreaterThan(0);
  });

  it("navigates to the URL analysis placeholder", async () => {
    mockStatus();
    const user = userEvent.setup();
    renderApp();

    const links = screen.getAllByRole("link", { name: /url-analys/i });
    await user.click(links[0]);

    expect(screen.getByRole("heading", { name: "Analysera URL:er" })).toBeInTheDocument();
  });

  it("shows a degraded state without blocking the dashboard", async () => {
    mockStatus({ ...healthyStatus, status: "degraded", database: "unavailable" });
    renderApp();

    expect(await screen.findByText("Systemet är degraderat")).toBeInTheDocument();
    expect(screen.getByText("Ej tillgänglig")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("Vad vill du göra?")).toBeVisible());
  });
});
