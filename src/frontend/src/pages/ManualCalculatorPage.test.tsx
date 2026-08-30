import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  calculateManualScenario,
  createSavedCostScenario,
  deleteSavedCostScenario,
  getSavedCostScenario,
  listSavedCostScenarios,
  ManualCalculationApiError,
  replaceSavedCostScenario,
  SavedCostScenarioApiError,
  type ManualCalculationRequest,
  type SavedCostScenarioResponse,
  type SavedCostScenarioSummary,
} from "@/api/client";
import {
  completeManualCalculationResult,
  incompleteManualCalculationResult,
} from "@/test/manual-calculation-result";
import { ManualCalculatorPage } from "./ManualCalculatorPage";

vi.mock("@/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/api/client")>("@/api/client");
  return {
    ...actual,
    calculateManualScenario: vi.fn(),
    createSavedCostScenario: vi.fn(),
    deleteSavedCostScenario: vi.fn(),
    getSavedCostScenario: vi.fn(),
    getSavedCostScenarioByRegistration: vi.fn(),
    listSavedCostScenarios: vi.fn(),
    replaceSavedCostScenario: vi.fn(),
  };
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
    vi.mocked(createSavedCostScenario).mockReset();
    vi.mocked(deleteSavedCostScenario).mockReset();
    vi.mocked(getSavedCostScenario).mockReset();
    vi.mocked(listSavedCostScenarios).mockReset();
    vi.mocked(replaceSavedCostScenario).mockReset();
    vi.mocked(listSavedCostScenarios).mockResolvedValue([]);
  });

  it("starts with only safe defaults and progressively reveals optional fields", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByLabelText(/Beräkningsperiod/)).toHaveValue("12");
    expect(await screen.findByText("Inga bilar är sparade ännu. Fyll i kalkylen och välj Spara bil.")).toBeVisible();
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
    expect(screen.getByText("Förhandsvisning – inte sparad")).toBeVisible();

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

  it("shows saved-list states without blocking an unsaved preview", async () => {
    const user = userEvent.setup();
    vi.mocked(listSavedCostScenarios)
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockResolvedValue([]);
    vi.mocked(calculateManualScenario).mockResolvedValue(incompleteManualCalculationResult);
    renderPage();

    expect(await screen.findByText("Sparade bilar kunde inte hämtas")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Försök igen" }));
    expect(await screen.findByText("Inga bilar är sparade ännu. Fyll i kalkylen och välj Spara bil.")).toBeVisible();
    await fillValidZeroDistanceScenario(user);
    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));

    expect(await screen.findByText("Aktuellt resultat")).toBeVisible();
    expect(calculateManualScenario).toHaveBeenCalledOnce();
  });

  it("announces saved-list loading before showing the empty state", async () => {
    let resolveList!: (value: SavedCostScenarioSummary[]) => void;
    vi.mocked(listSavedCostScenarios).mockReturnValue(new Promise((resolve) => {
      resolveList = resolve;
    }));
    renderPage();

    expect(screen.getByText("Hämtar sparade bilar…")).toBeVisible();
    await act(async () => resolveList([]));
    expect(await screen.findByText("Inga bilar är sparade ännu. Fyll i kalkylen och välj Spara bil.")).toBeVisible();
  });

  it("saves directly with a normalized registration and displays the returned result", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(zeroDistanceScenario(), 1);
    vi.mocked(createSavedCostScenario).mockResolvedValue(saved);
    renderPage();
    await fillValidZeroDistanceScenario(user);
    await user.type(screen.getByLabelText("Registreringsnummer"), "abc-123");

    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    await waitFor(() => expect(createSavedCostScenario).toHaveBeenCalledWith({
      registrationNumber: "ABC123",
      scenario: zeroDistanceScenario(),
    }));
    expect(await screen.findByText("Bilen har sparats.")).toBeVisible();
    expect(screen.getByLabelText("Registreringsnummer")).toHaveValue("ABC123");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveAttribute("readonly");
    expect(screen.getByText("Sparad revision 1")).toBeVisible();
    expect(screen.getAllByText("Sparat resultat").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByRole("button", { name: "Spara ändringar" })).toBeEnabled();
    expect(calculateManualScenario).not.toHaveBeenCalled();
  });

  it("disables the form while saving and preserves it after a network failure", async () => {
    const user = userEvent.setup();
    let rejectSave!: (reason: unknown) => void;
    vi.mocked(createSavedCostScenario).mockReturnValue(new Promise((_, reject) => {
      rejectSave = reject;
    }));
    renderPage();
    await fillValidZeroDistanceScenario(user);
    await user.type(screen.getByLabelText("Registreringsnummer"), "ABC123");

    await user.click(screen.getByRole("button", { name: "Spara bil" }));
    expect(screen.getByRole("button", { name: "Sparar…" })).toBeDisabled();
    expect(screen.getByLabelText(/Inköpspris/)).toBeDisabled();
    await act(async () => rejectSave(new TypeError("Failed to fetch")));

    expect(await screen.findByText("Bilen kunde inte sparas. Dina uppgifter finns kvar.")).toBeVisible();
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("20000");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveValue("ABC123");
  });

  it("requires a valid registration only when saving", async () => {
    const user = userEvent.setup();
    vi.mocked(calculateManualScenario).mockResolvedValue(incompleteManualCalculationResult);
    renderPage();
    await fillValidZeroDistanceScenario(user);

    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    expect(createSavedCostScenario).not.toHaveBeenCalled();
    expect(screen.getAllByText("Ange registreringsnummer för att spara bilen.").length).toBeGreaterThan(0);
    await user.click(screen.getByRole("button", { name: "Beräkna kostnad" }));
    expect(await screen.findByText("Aktuellt resultat")).toBeVisible();
  });

  it("maps saved API validation paths without clearing the form", async () => {
    const user = userEvent.setup();
    vi.mocked(createSavedCostScenario).mockRejectedValue(new SavedCostScenarioApiError(
      "Validation failed",
      400,
      {
        status: 400,
        errors: {
          "scenario.purchasePriceSek": ["Value must be at least 0 and at most 100000000."],
          registrationNumber: ["Registration number is invalid."],
        },
      },
    ));
    renderPage();
    await fillValidZeroDistanceScenario(user);
    await user.type(screen.getByLabelText("Registreringsnummer"), "ABC123");

    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    expect((await screen.findAllByText("Värdet ligger utanför det tillåtna intervallet.")).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("20000");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveValue("ABC123");
  });

  it("opens a saved scenario with its stored result and immutable registration", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    vi.mocked(getSavedCostScenario).mockResolvedValue(saved);
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Öppna" }));

    expect(await screen.findByText("Den sparade bilen har öppnats.")).toBeVisible();
    expect(screen.getByLabelText("Bilens namn")).toHaveValue("Volvo V70");
    expect(screen.getByLabelText(/Årlig körsträcka/)).toHaveValue("1500");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveValue("ABC123");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveAttribute("readonly");
    expect(screen.getAllByText(/64\s000,00\s*kr/).length).toBeGreaterThan(0);
  });

  it("asks before discarding a draft and preserves it when cancelled", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    renderPage();
    await user.type(screen.getByLabelText(/Inköpspris/), "12345");

    await user.click(await screen.findByRole("button", { name: "Öppna" }));

    expect(screen.getByRole("alertdialog", { name: /Öppna Volvo V70/ })).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Avbryt" }));
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("12345");
    expect(getSavedCostScenario).not.toHaveBeenCalled();
  });

  it("asks before starting a new calculation and clears only after confirmation", async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText(/Inköpspris/), "12345");

    await user.click(screen.getByRole("button", { name: "Ny kalkyl" }));
    expect(screen.getByRole("alertdialog", { name: "Börja med en ny kalkyl?" })).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Avbryt" }));
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("12345");

    await user.click(screen.getByRole("button", { name: "Ny kalkyl" }));
    await user.click(screen.getByRole("button", { name: "Skapa ny kalkyl" }));
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("");
    expect(screen.getByLabelText(/Beräkningsperiod/)).toHaveValue("12");
  });

  it("replaces an opened scenario with its expected revision", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    const replaced = createSavedResponse({ ...completeScenario(), purchasePriceSek: 21_000 }, 4);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    vi.mocked(getSavedCostScenario).mockResolvedValue(saved);
    vi.mocked(replaceSavedCostScenario).mockResolvedValue(replaced);
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    await screen.findByText("Sparad revision 3");

    const price = screen.getByLabelText(/Inköpspris/);
    await user.clear(price);
    await user.type(price, "21000");
    expect(screen.getByText("Osparade ändringar")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Spara ändringar" }));

    await waitFor(() => expect(replaceSavedCostScenario).toHaveBeenCalledWith(
      saved.vehicleId,
      expect.objectContaining({ expectedRevision: 3 }),
    ));
    expect(await screen.findByText("Sparad revision 4")).toBeVisible();
  });

  it("requires an explicit choice before replacing a duplicate registration", async () => {
    const user = userEvent.setup();
    const existing = createSavedResponse(zeroDistanceScenario(), 2);
    const replacement = createSavedResponse(zeroDistanceScenario(), 3);
    vi.mocked(createSavedCostScenario).mockRejectedValue(new SavedCostScenarioApiError(
      "Conflict",
      409,
      undefined,
      {
        code: "registrationNumberConflict",
        existingVehicleId: existing.vehicleId,
      },
    ));
    vi.mocked(getSavedCostScenario).mockResolvedValue(existing);
    vi.mocked(replaceSavedCostScenario).mockResolvedValue(replacement);
    renderPage();
    await fillValidZeroDistanceScenario(user);
    await user.type(screen.getByLabelText("Registreringsnummer"), "ABC123");

    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    expect(await screen.findByRole("alertdialog", { name: "ABC123 är redan sparad" })).toBeVisible();
    expect(replaceSavedCostScenario).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Ersätt sparad bil" }));
    await waitFor(() => expect(replaceSavedCostScenario).toHaveBeenCalledWith(existing.vehicleId, {
      expectedRevision: 2,
      scenario: zeroDistanceScenario(),
    }));
  });

  it("can open the existing vehicle instead of replacing a duplicate", async () => {
    const user = userEvent.setup();
    const existing = createSavedResponse(completeScenario(), 2);
    vi.mocked(createSavedCostScenario).mockRejectedValue(new SavedCostScenarioApiError(
      "Conflict",
      409,
      undefined,
      { code: "registrationNumberConflict", existingVehicleId: existing.vehicleId },
    ));
    vi.mocked(getSavedCostScenario).mockResolvedValue(existing);
    renderPage();
    await fillValidZeroDistanceScenario(user);
    await user.type(screen.getByLabelText("Registreringsnummer"), "ABC123");
    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    await user.click(await screen.findByRole("button", { name: "Öppna sparad bil" }));

    expect(screen.getByLabelText("Bilens namn")).toHaveValue("Volvo V70");
    expect(screen.getByLabelText(/Årlig körsträcka/)).toHaveValue("1500");
    expect(screen.getByLabelText("Registreringsnummer")).toHaveAttribute("readonly");
    expect(replaceSavedCostScenario).not.toHaveBeenCalled();
  });

  it("preserves edits on a revision conflict and can reload explicitly", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    const latest = createSavedResponse({ ...completeScenario(), purchasePriceSek: 22_000 }, 4);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    vi.mocked(getSavedCostScenario).mockResolvedValueOnce(saved).mockResolvedValueOnce(latest);
    vi.mocked(replaceSavedCostScenario).mockRejectedValue(new SavedCostScenarioApiError(
      "Revision conflict",
      409,
      undefined,
      { code: "revisionConflict", expectedRevision: 3, actualRevision: 4 },
    ));
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    const price = screen.getByLabelText(/Inköpspris/);
    await user.clear(price);
    await user.type(price, "21000");
    await user.click(screen.getByRole("button", { name: "Spara ändringar" }));

    expect(await screen.findByText(/Dina ändringar är kvar/)).toBeVisible();
    expect(price).toHaveValue("21000");
    await user.click(screen.getByRole("button", { name: "Hämta senaste" }));
    await waitFor(() => expect(price).toHaveValue("22000"));
  });

  it("deletes the active vehicle but retains its form and result as an unsaved draft", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    vi.mocked(getSavedCostScenario).mockResolvedValue(saved);
    vi.mocked(deleteSavedCostScenario).mockResolvedValue();
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    await screen.findByText("Sparad revision 3");

    await user.click(screen.getByRole("button", { name: /Ta bort Volvo V70/ }));
    await user.click(screen.getByRole("button", { name: "Ta bort permanent" }));

    expect(await screen.findByText(/finns kvar som en osparad kalkyl/)).toBeVisible();
    expect(screen.getByLabelText(/Inköpspris/)).toHaveValue("20000");
    expect(screen.getByLabelText("Registreringsnummer")).toBeEnabled();
    expect(screen.getAllByText(/64\s000,00\s*kr/).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Spara bil" })).toBeEnabled();
  });

  it("does not delete on a stale revision and offers to reload the active vehicle", async () => {
    const user = userEvent.setup();
    const saved = createSavedResponse(completeScenario(), 3);
    vi.mocked(listSavedCostScenarios).mockResolvedValue([savedSummary(saved)]);
    vi.mocked(getSavedCostScenario).mockResolvedValue(saved);
    vi.mocked(deleteSavedCostScenario).mockRejectedValue(new SavedCostScenarioApiError(
      "Revision conflict",
      409,
      undefined,
      { code: "revisionConflict", expectedRevision: 3, actualRevision: 4 },
    ));
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    await screen.findByText("Sparad revision 3");

    await user.click(screen.getByRole("button", { name: /Ta bort Volvo V70/ }));
    await user.click(screen.getByRole("button", { name: "Ta bort permanent" }));

    expect(await screen.findByText(/Inget har raderats/)).toBeVisible();
    expect(screen.getByRole("button", { name: "Hämta senaste" })).toBeVisible();
    expect(screen.getByLabelText("Registreringsnummer")).toHaveAttribute("readonly");
  });
});

function zeroDistanceScenario(): ManualCalculationRequest {
  return {
    vehicleLabel: null,
    calculationPeriodMonths: 12,
    purchasePriceSek: 20_000,
    expectedResidualValueSek: null,
    annualDistanceKilometres: 0,
    financing: null,
    energySources: [],
    vehicleTax: null,
    insurance: null,
    maintenanceAndRepairs: null,
    otherRecurringCosts: [],
    otherOneTimeCosts: [],
  };
}

function completeScenario(): ManualCalculationRequest {
  return {
    vehicleLabel: "Volvo V70",
    calculationPeriodMonths: 12,
    purchasePriceSek: 20_000,
    expectedResidualValueSek: 15_000,
    annualDistanceKilometres: 15_000,
    financing: {
      downPaymentSek: 5_000,
      annualNominalInterestRatePercent: 0,
      termMonths: 12,
    },
    energySources: [{
      label: "Bensin",
      unit: "litre",
      consumptionPer100Kilometres: 8,
      pricePerUnitSek: 20,
      distanceSharePercent: 100,
    }],
    vehicleTax: { amountSek: 2_400, cadence: "annual" },
    insurance: { amountSek: 500, cadence: "monthly" },
    maintenanceAndRepairs: { amountSek: 6_000, cadence: "annual" },
    otherRecurringCosts: [{ label: "Övrigt", amountSek: 300, cadence: "monthly" }],
    otherOneTimeCosts: [{ label: "Leverans", amountSek: 2_000 }],
  };
}

function createSavedResponse(
  scenario: ManualCalculationRequest,
  revision: number,
): SavedCostScenarioResponse {
  return {
    vehicleId: "0194f7a8-5c33-7f43-b516-d5c2f94dcd31",
    registrationNumber: "ABC123",
    revision,
    calculationVersion: 1,
    resultSchemaVersion: 1,
    createdAtUtc: "2026-08-30T08:00:00Z",
    updatedAtUtc: "2026-08-30T09:00:00Z",
    calculatedAtUtc: "2026-08-30T09:00:00Z",
    scenario,
    result: scenario.annualDistanceKilometres === 0
      ? incompleteManualCalculationResult
      : completeManualCalculationResult,
  };
}

function savedSummary(saved: SavedCostScenarioResponse): SavedCostScenarioSummary {
  return {
    vehicleId: saved.vehicleId,
    registrationNumber: saved.registrationNumber,
    vehicleLabel: saved.scenario.vehicleLabel ?? null,
    revision: saved.revision,
    purchasePriceSek: saved.scenario.purchasePriceSek,
    calculationPeriodMonths: saved.scenario.calculationPeriodMonths,
    cashFlowKnownTotalSek: saved.result.cashFlow.knownTotalSek,
    netOwnershipCostKnownTotalSek: saved.result.netOwnershipCost?.knownTotalSek ?? null,
    completeness: saved.result.completeness,
    updatedAtUtc: saved.updatedAtUtc,
  };
}
