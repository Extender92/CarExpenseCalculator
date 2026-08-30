import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  calculateManualScenario,
  ManualCalculationApiError,
} from "@/api/client";
import {
  completeManualCalculationResult,
  incompleteManualCalculationResult,
} from "@/test/manual-calculation-result";
import { ManualCalculatorPage } from "./ManualCalculatorPage";

vi.mock("@/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/api/client")>("@/api/client");
  return { ...actual, calculateManualScenario: vi.fn() };
});

function renderPage() {
  return render(
    <MemoryRouter>
      <ManualCalculatorPage />
    </MemoryRouter>,
  );
}

async function fillValidZeroDistanceScenario(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Inköpspris/), "20000");
  await user.type(screen.getByLabelText(/Årlig körsträcka/), "0");
}

describe("ManualCalculatorPage", () => {
  beforeEach(() => {
    vi.mocked(calculateManualScenario).mockReset();
  });

  it("starts with only safe defaults and progressively reveals optional fields", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByLabelText(/Beräkningsperiod/)).toHaveValue("12");
    expect(screen.getByRole("radio", { name: "Kontantköp" })).toBeChecked();
    expect(screen.queryByLabelText(/Kontantinsats/)).not.toBeInTheDocument();
    expect(screen.getByText("Ingen energikälla har lagts till. Det är giltigt när körsträckan är 0 mil.")).toBeVisible();

    const taxGroup = screen.getByRole("group", { name: "Fordonsskatt" });
    expect(within(taxGroup).getByRole("radio", { name: "Okänd" })).toBeChecked();
    expect(within(taxGroup).queryByLabelText(/Belopp/)).not.toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: "Finansiering" }));
    expect(screen.getByLabelText(/Kontantinsats/)).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Lägg till energikälla" }));
    expect(screen.getByRole("button", { name: "Ta bort energikälla 1" })).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Ta bort energikälla 1" }));
    expect(screen.queryByRole("button", { name: "Ta bort energikälla 1" })).not.toBeInTheDocument();
  });

  it("blocks an invalid request and focuses an accessible error summary", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect(calculateManualScenario).not.toHaveBeenCalled();
    expect(screen.getAllByText("Ange bilens inköpspris.").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Ange årlig körsträcka.").length).toBeGreaterThan(0);
    const heading = screen.getByRole("heading", { name: "Kontrollera formuläret" });
    await waitFor(() => expect(heading.closest("[role='alert']")).toHaveFocus());
  });

  it("sends explicit nulls and renders incomplete known subtotals", async () => {
    const user = userEvent.setup();
    vi.mocked(calculateManualScenario).mockResolvedValue(incompleteManualCalculationResult);
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    await waitFor(() => expect(calculateManualScenario).toHaveBeenCalledWith(expect.objectContaining({
      calculationPeriodMonths: 12,
      purchasePriceSek: 20_000,
      annualDistanceKilometres: 0,
      expectedResidualValueSek: null,
      financing: null,
      energySources: [],
      vehicleTax: null,
      insurance: null,
      maintenanceAndRepairs: null,
      otherRecurringCosts: [],
      otherOneTimeCosts: [],
    })));
    expect((await screen.findAllByText("Känt kassaflöde")).length).toBeGreaterThan(0);
    expect(screen.getByText(/Saknas: fordonsskatt, försäkring, underhåll och reparationer, restvärde/)).toBeVisible();
    expect(screen.getAllByText("Okänd").length).toBeGreaterThanOrEqual(3);
    expect(screen.getByText(/Ägandekostnaden kan inte beräknas utan ett förväntat restvärde/)).toBeVisible();
  });

  it("shows the complete calculation breakdown and marks it stale after editing", async () => {
    const user = userEvent.setup();
    vi.mocked(calculateManualScenario).mockResolvedValue(completeManualCalculationResult);
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect((await screen.findAllByText(/64\s000,00\s*kr/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/49\s000,00\s*kr/).length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { name: "Finansiering" })).toBeVisible();
    expect(screen.getAllByRole("heading", { name: "Energi" }).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("1 200 liter")).toBeVisible();
    expect(screen.getByText("Alla standardvärden är kända.")).toBeVisible();

    await user.type(screen.getByLabelText(/Inköpspris/), "0");
    expect(screen.getByText("Behöver räknas om")).toBeVisible();
    expect(screen.getByText(/Resultatet nedan gäller dina tidigare värden/)).toBeVisible();
  });

  it("shows loading while the form is disabled", async () => {
    const user = userEvent.setup();
    let resolveRequest!: (value: typeof completeManualCalculationResult) => void;
    vi.mocked(calculateManualScenario).mockReturnValue(new Promise((resolve) => {
      resolveRequest = resolve;
    }));
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect(screen.getByRole("button", { name: "Beräknar…" })).toBeDisabled();
    expect(screen.getByLabelText(/Inköpspris/)).toBeDisabled();

    await act(async () => resolveRequest(completeManualCalculationResult));
    expect(await screen.findByText("Aktuellt resultat")).toBeVisible();
  });

  it("maps server validation to Swedish field errors and preserves input", async () => {
    const user = userEvent.setup();
    vi.mocked(calculateManualScenario).mockRejectedValue(new ManualCalculationApiError(
      "Invalid request",
      {
        title: "One or more validation errors occurred.",
        status: 400,
        errors: {
          purchasePriceSek: ["Value must be at least 0 and at most 100000000."],
        },
      },
    ));
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect((await screen.findAllByText("Värdet ligger utanför det tillåtna intervallet.")).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("20000");
    expect(screen.getByText("Servern hittade värden som behöver rättas.")).toBeVisible();
  });

  it("shows a recoverable network failure without clearing the form", async () => {
    const user = userEvent.setup();
    vi.mocked(calculateManualScenario).mockRejectedValue(new TypeError("Failed to fetch"));
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect(await screen.findByText(/Kontrollera anslutningen och försök igen/)).toBeVisible();
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("20000");
    expect(screen.getByRole("button", { name: "Beräkna kostnad" })).toBeEnabled();
  });
});
