import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { getSystemStatus, listSavedListings } from "@/api/client";
import { householdApi, HouseholdApiError } from "@/features/household/api";
import { reviewDraftApi } from "@/features/url-analysis/review-drafts-api";
import { n, stringifyExact } from "@/features/household/numbers";
import { guidePreferenceKey } from "@/features/household/guide-preference";
import { App } from "./App";

vi.mock("@/api/client", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/api/client")>(),
  getSystemStatus: vi.fn(),
  listSavedListings: vi.fn(),
}));

const healthyStatus = {
  version: "1.0.0",
  status: "healthy",
  database: "available",
  features: {
    ruleBasedSearch: true,
    urlAnalysis: true,
    manualCalculator: true,
    aiReview: false,
  },
  integrations: {
    codexListingExtractionConfigured: true,
  },
};

function mockStatus(payload = healthyStatus) {
  vi.mocked(getSystemStatus).mockResolvedValue(payload);
  vi.mocked(listSavedListings).mockResolvedValue([]);
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

    expect(screen.getByRole("heading", { name: /jämför kostnaden för nästa bil/i })).toBeInTheDocument();
    expect(screen.getAllByText("Jämförelse").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Lägg till bil").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Gemensamma uppgifter").length).toBeGreaterThan(0);
    expect(await screen.findByText("Anslutet")).toBeInTheDocument();
    expect(screen.getByText("Systemstatus", { selector: "summary" })).toBeInTheDocument();
  });

  it("navigates to the URL analysis workspace", async () => {
    mockStatus();
    const user = userEvent.setup();
    renderApp();

    const links = screen.getAllByRole("link", { name: /lägg till bil/i });
    await user.click(links[0]);

    expect(await screen.findByRole("heading", { name: "Lägg till bil" })).toBeInTheDocument();
  });

  it("shows a degraded state without blocking the dashboard", async () => {
    mockStatus({ ...healthyStatus, status: "degraded", database: "unavailable" });
    renderApp();

    expect(await screen.findByText(/Databasen är inte tillgänglig/)).toBeVisible();
    expect(screen.getByText("Ej tillgänglig")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("link", { name: "Jämför sparade bilar" })).toBeVisible());
  });
});

describe("optional first-start introduction", () => {
  afterEach(() => { vi.restoreAllMocks(); localStorage.clear(); });
  function profileMissing() {
    mockStatus();
    vi.spyOn(householdApi, "profile").mockRejectedValue(new HouseholdApiError(404, "profileNotFound"));
    vi.spyOn(reviewDraftApi, "list").mockResolvedValue([]);
  }
  it("skips without writing a profile and can reopen the browser-local dismissed guide", async () => {
    profileMissing();
    const save = vi.spyOn(householdApi, "saveProfile");
    const user = userEvent.setup();
    renderApp();
    const guide = await screen.findByRole("dialog", { name: "Kom igång" });
    await user.type(within(guide).getByLabelText("Ägandeperiod (månader, 1–120)"), "36");
    await user.click(within(guide).getByRole("button", { name: "Hoppa över" }));
    expect(save).not.toHaveBeenCalled();
    expect(localStorage.getItem(guidePreferenceKey)).toBe("yes");
    await user.click(screen.getByRole("button", { name: "Öppna introduktionen" }));
    expect(within(screen.getByRole("dialog")).getByLabelText("Ägandeperiod (månader, 1–120)")).toHaveValue("");
  });
  it("saves a valid partial profile without inventing prices or loan values", async () => {
    profileMissing();
    const save = vi.spyOn(householdApi, "saveProfile").mockImplementation(async input => ({ input, revision: n(1) }));
    const user = userEvent.setup(); renderApp();
    const guide = await screen.findByRole("dialog", { name: "Kom igång" });
    await user.type(within(guide).getByLabelText("Ägandeperiod (månader, 1–120)"), "36");
    await user.click(within(guide).getByRole("button", { name: "Nästa" }));
    await user.click(within(guide).getByRole("button", { name: "Nästa" }));
    await user.click(within(guide).getByRole("button", { name: "Spara och fortsätt" }));
    await waitFor(() => expect(save).toHaveBeenCalledOnce());
    expect(save.mock.calls[0][0].periodMonths?.text).toBe("36");
    expect(save.mock.calls[0][0].purchaseCashSek == null).toBe(true);
    expect(save.mock.calls[0][0].loanTerms == null).toBe(true);
    expect(save.mock.calls[0][0].energyPrices ?? []).toEqual([]);
  });
  it("does not auto-open for existing profiles and preserves exact scenarios on editing", async () => {
    mockStatus();
    const input = { activeSensitivityMode: "baseline" as const, periodMonths: n(12), purchaseCashSek: n(0),
      energyPrices: [{ fuel: "petrol" as const, unit: "litre" as const,
        pricePerUnitSek: { favorable: n("15.123456789"), baseline: n(20), cautious: n(25) } }] };
    vi.spyOn(householdApi, "profile").mockResolvedValue({ input, revision: n(7) });
    vi.spyOn(reviewDraftApi, "list").mockResolvedValue([]);
    const save = vi.spyOn(householdApi, "saveProfile").mockImplementation(async value => ({ input: value, revision: n(8) }));
    const user = userEvent.setup(); renderApp();
    await waitFor(() => expect(householdApi.profile).toHaveBeenCalled());
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Öppna introduktionen" }));
    const guide = screen.getByRole("dialog");
    await user.clear(within(guide).getByLabelText("Ägandeperiod (månader, 1–120)"));
    await user.type(within(guide).getByLabelText("Ägandeperiod (månader, 1–120)"), "24");
    await user.click(within(guide).getByRole("button", { name: "Nästa" }));
    await user.click(within(guide).getByRole("button", { name: "Nästa" }));
    await user.click(within(guide).getByRole("button", { name: "Spara och fortsätt" }));
    await waitFor(() => expect(save).toHaveBeenCalledOnce());
    expect(stringifyExact(save.mock.calls[0][0].energyPrices)).toBe(stringifyExact(input.energyPrices));
    expect(save.mock.calls[0][0].purchaseCashSek?.text).toBe("0");
  });
});
