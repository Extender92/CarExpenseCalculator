import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  analyzeListing,
  createSavedListing,
  deleteSavedListing,
  getSavedListing,
  getSystemStatus,
  listSavedListings,
  ListingAnalysisApiError,
  replaceSavedListing,
  SavedListingApiError,
  type ListingAnalysisResponse,
} from "@/api/client";
import {
  completeListingAnalysisResponse,
  savedListingResponse,
  savedListingSummary,
} from "@/test/listing-analysis";
import { UrlAnalysisPage } from "./UrlAnalysisPage";

const { navigateMock } = vi.hoisted(() => ({ navigateMock: vi.fn() }));

vi.mock("react-router-dom", async (importOriginal) => {
  const original = await importOriginal<typeof import("react-router-dom")>();
  return { ...original, useNavigate: () => navigateMock };
});

vi.mock("@/api/client", async (importOriginal) => {
  const original = await importOriginal<typeof import("@/api/client")>();
  return {
    ...original,
    analyzeListing: vi.fn(),
    createSavedListing: vi.fn(),
    deleteSavedListing: vi.fn(),
    getSavedListing: vi.fn(),
    getSystemStatus: vi.fn(),
    listSavedListings: vi.fn(),
    replaceSavedListing: vi.fn(),
  };
});

const healthyStatus = {
  version: "1.0.0",
  status: "healthy",
  database: "available",
  features: { ruleBasedSearch: false, urlAnalysis: true, manualCalculator: true, aiReview: false },
  integrations: { codexListingExtractionConfigured: true },
};

beforeEach(() => {
  navigateMock.mockReset();
  vi.mocked(getSystemStatus).mockResolvedValue(healthyStatus);
  vi.mocked(analyzeListing).mockReset();
  vi.mocked(createSavedListing).mockReset();
  vi.mocked(deleteSavedListing).mockReset();
  vi.mocked(getSavedListing).mockReset();
  vi.mocked(listSavedListings).mockReset();
  vi.mocked(listSavedListings).mockResolvedValue([]);
  vi.mocked(replaceSavedListing).mockReset();
});

describe("Swedish URL analysis workspace", () => {
  it("starts safely and reports invalid, duplicate, local, and excessive URLs", async () => {
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    expect(screen.getByText("Inga annonsunderlag är öppna ännu.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Ange minst en");
    expect(screen.getByRole("alert")).toHaveFocus();

    await user.type(screen.getByLabelText("URL:er"), [
      "http://localhost/item/1",
      "https://cars.example/item/1?ci=2",
      "http://cars.example/item/1/",
    ].join("\n"));
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Lokala adresser");
    expect(screen.getByRole("alert")).toHaveTextContent("samma annonssida");
    expect(analyzeListing).not.toHaveBeenCalled();
  });

  it("analyzes and renders every review area, source evidence, values, and provenance", async () => {
    vi.mocked(analyzeListing).mockResolvedValue(completeListingAnalysisResponse);
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), completeListingAnalysisResponse.submittedUrl);
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));

    expect(await screen.findByText("Volvo V70 2.4")).toBeInTheDocument();
    expect(screen.getByText("20 000 kr")).toBeInTheDocument();
    expect(screen.getByText("16710 mil")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));

    for (const heading of ["Identitet", "Annons", "Tekniska uppgifter", "Historik och besiktning", "Utrustning och uppgifter från säljaren", "Källor och proveniens"]) {
      expect(screen.getByRole("heading", { name: heading })).toBeInTheDocument();
    }
    expect(screen.getByRole("link", { name: /cars\.example\/item\/1/ })).toHaveAttribute("rel", "noopener noreferrer");
    expect(screen.getByText("Matchar annonsen")).toBeInTheDocument();
    expect(screen.getAllByText(/Annons · AI · Inte verifierad/).length).toBeGreaterThan(20);
    expect(screen.getByLabelText("Ort eller stad")).toHaveValue("Tenhult");
    expect(screen.getByLabelText("Län")).toHaveValue("Jönköpings län");

    const make = screen.getByLabelText("Märke");
    await user.clear(make);
    await user.type(make, "Saab");
    expect(screen.getAllByText(/Användare · Manuell · Bekräftad/).length).toBeGreaterThan(0);
    expect(screen.getByLabelText("Modell")).toHaveValue("V70");
    expect(screen.getByText((content) => content.startsWith("Motsvarar") && content.includes("167") && content.includes("km"))).toBeInTheDocument();

    const county = screen.getByLabelText("Län");
    await user.clear(county);
    expect(screen.getByLabelText("Ort eller stad")).toHaveValue("Tenhult");
    expect(screen.getByText("län")).toBeInTheDocument();
  });

  it("keeps manual entry available when extraction is unconfigured", async () => {
    vi.mocked(getSystemStatus).mockResolvedValue({
      ...healthyStatus,
      integrations: { codexListingExtractionConfigured: false },
    });
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), "https://cars.example/item/manual");
    await waitFor(() => expect(screen.getByRole("button", { name: "Analysera URL:er" })).toBeDisabled());
    expect(screen.getByText(/inte konfigurerad/i)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Skapa manuella utkast" }));

    expect(screen.getByText(/skapades utan automatisk extraktion/i)).toBeInTheDocument();
    expect(screen.getByLabelText("Annonspris")).toHaveValue("");
    await user.type(screen.getByLabelText("Annonspris"), "0");
    await user.selectOptions(screen.getByLabelText("Dragkrok"), "false");
    expect(screen.getByText("0 kr")).toBeInTheDocument();
    expect(screen.getAllByText("Nej").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Användare · Manuell · Bekräftad/).length).toBeGreaterThanOrEqual(2);
    expect(analyzeListing).not.toHaveBeenCalled();
  });

  it("runs at most two independent requests and preserves success when another fails", async () => {
    const deferred = [createDeferred<ListingAnalysisResponse>(), createDeferred<ListingAnalysisResponse>(), createDeferred<ListingAnalysisResponse>()];
    deferred.forEach((promise) => vi.mocked(analyzeListing).mockImplementationOnce(() => promise.promise));
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), [
      "https://cars.example/item/1",
      "https://cars.example/item/2",
      "https://cars.example/item/3",
    ].join("\n"));
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    await waitFor(() => expect(analyzeListing).toHaveBeenCalledTimes(2));

    deferred[0].resolve(completeListingAnalysisResponse);
    await waitFor(() => expect(analyzeListing).toHaveBeenCalledTimes(3));
    deferred[1].reject(new ListingAnalysisApiError("Tillfälligt fel", 503, "listingAnalysisProviderUnavailable"));
    deferred[2].resolve({ ...completeListingAnalysisResponse, normalizedUrl: "https://cars.example/item/3" });

    await waitFor(() => expect(screen.getAllByText("Komplett extraktion")).toHaveLength(2));
    expect(screen.getByText("Tillfälligt fel")).toBeInTheDocument();
    expect(screen.getByText("Analysen misslyckades")).toBeInTheDocument();
  });

  it("allows a ten-URL batch while extractor status is unknown", async () => {
    vi.mocked(getSystemStatus).mockImplementation(() => new Promise(() => undefined));
    let call = 0;
    vi.mocked(analyzeListing).mockImplementation(async (url) => {
      call += 1;
      return {
        ...completeListingAnalysisResponse,
        submittedUrl: url,
        normalizedUrl: url,
        status: call === 10 ? "partial" : "complete",
      };
    });
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    const urls = Array.from({ length: 10 }, (_, index) => `https://cars.example/item/${index + 1}`);
    await user.type(screen.getByLabelText("URL:er"), urls.join("\n"));
    expect(screen.getByRole("button", { name: "Analysera URL:er" })).toBeEnabled();
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));

    await waitFor(() => expect(analyzeListing).toHaveBeenCalledTimes(10));
    expect(await screen.findAllByText("Komplett extraktion")).toHaveLength(9);
    expect(screen.getByText("Delvis extraktion")).toBeInTheDocument();
  });

  it("requires confirmation before a successful retry can replace edited values", async () => {
    vi.mocked(analyzeListing).mockResolvedValue(completeListingAnalysisResponse);
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), completeListingAnalysisResponse.submittedUrl);
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    await screen.findByText("Volvo V70 2.4");
    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));
    await user.clear(screen.getByLabelText("Märke"));
    await user.type(screen.getByLabelText("Märke"), "Saab");
    await user.click(screen.getByRole("button", { name: "Analysera igen" }));

    expect(screen.getByRole("alertdialog", { name: "Ersätt manuella ändringar?" })).toBeInTheDocument();
    expect(analyzeListing).toHaveBeenCalledTimes(1);
    await user.click(screen.getByRole("button", { name: "Analysera och ersätt" }));
    await waitFor(() => expect(analyzeListing).toHaveBeenCalledTimes(2));
    expect(await screen.findByLabelText("Märke")).toHaveValue("Volvo");
  });

  it("supports collection unknown, known-empty, and entered states with accessible controls", async () => {
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);
    await user.type(screen.getByLabelText("URL:er"), "https://cars.example/item/manual");
    await user.click(screen.getByRole("button", { name: "Skapa manuella utkast" }));

    const equipment = screen.getByLabelText("Utrustning");
    await user.selectOptions(equipment, "empty");
    expect(equipment).toHaveValue("empty");
    await user.selectOptions(equipment, "values");
    await user.type(screen.getByLabelText("Utrustning 1"), "AC");
    expect(screen.getByLabelText("Utrustning 1")).toHaveValue("AC");
    await user.click(screen.getByRole("button", { name: "Ta bort utrustning 1" }));
    expect(screen.queryByLabelText("Utrustning 1")).not.toBeInTheDocument();
    await user.selectOptions(equipment, "empty");
    await user.click(screen.getByRole("button", { name: "Kontrollera uppgifter" }));
    expect(screen.getByText(/giltigt format/i)).toBeInTheDocument();
  });

  it("loads saved summaries independently and recovers from a list failure", async () => {
    vi.mocked(listSavedListings)
      .mockRejectedValueOnce(new SavedListingApiError("Databasen svarar inte."))
      .mockResolvedValueOnce([savedListingSummary]);
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("Databasen svarar inte");
    await user.click(screen.getByRole("button", { name: "Försök igen" }));

    expect(await screen.findByText("ABC123")).toBeInTheDocument();
    expect(screen.getByText("Volvo V70 2008")).toBeInTheDocument();
    expect(screen.getByText(/revision 3/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Skapa kalkyl" }));
    expect(navigateMock).toHaveBeenCalledWith(
      `/manual?listingVehicleId=${savedListingSummary.vehicleId}`,
    );
  });

  it("focuses an already open saved card and confirms before discarding its edits", async () => {
    vi.mocked(listSavedListings).mockResolvedValue([savedListingSummary]);
    vi.mocked(getSavedListing).mockResolvedValue(savedListingResponse);
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    const heading = await screen.findByText("Volvo V70 2.4");
    const card = heading.closest('[id^="workspace-"]');
    expect(card).not.toBeNull();
    await waitFor(() => expect(card).toHaveFocus());

    await user.click(screen.getByRole("button", { name: "Visa öppet kort" }));
    expect(getSavedListing).toHaveBeenCalledTimes(1);
    expect(card).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));
    await user.clear(screen.getByLabelText("Märke"));
    await user.type(screen.getByLabelText("Märke"), "Saab");
    await user.click(screen.getByRole("button", { name: "Visa öppet kort" }));

    expect(screen.getByRole("alertdialog", { name: "Läs in den sparade bilen igen?" })).toBeInTheDocument();
    expect(screen.getByLabelText("Märke")).toHaveValue("Saab");
    expect(getSavedListing).toHaveBeenCalledTimes(1);
  });

  it("creates a saved listing directly and then replaces an edited opened vehicle", async () => {
    vi.mocked(analyzeListing).mockResolvedValue(completeListingAnalysisResponse);
    vi.mocked(createSavedListing).mockResolvedValue(savedListingResponse);
    vi.mocked(replaceSavedListing).mockResolvedValue({
      ...savedListingResponse,
      revision: 4,
      listingVersion: 3,
      listing: {
        ...savedListingResponse.listing,
        make: {
          value: "Saab",
          provenance: {
            origin: "user",
            extractionMethod: "manual",
            verification: "userConfirmed",
            sourceUrl: savedListingResponse.normalizedUrl,
          },
        },
      },
    });
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), completeListingAnalysisResponse.submittedUrl);
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    await screen.findByText("Volvo V70 2.4");
    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    await waitFor(() => expect(createSavedListing).toHaveBeenCalledTimes(1));
    expect(vi.mocked(createSavedListing).mock.calls[0][0]).toMatchObject({
      registrationNumber: "ABC123",
      listing: { draft: { odometerKilometres: { value: 167100 } } },
    });
    expect(await screen.findByText("Bilen har sparats.")).toBeInTheDocument();
    expect(screen.getByText("Sparad")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));
    expect(screen.getByLabelText("Registreringsnummer")).toHaveAttribute("readonly");
    await user.clear(screen.getByLabelText("Märke"));
    await user.type(screen.getByLabelText("Märke"), "Saab");
    expect(screen.getByText("Ändrad sedan sparning")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Spara ändringar" }));

    await waitFor(() => expect(replaceSavedListing).toHaveBeenCalledWith(
      savedListingResponse.vehicleId,
      expect.objectContaining({ expectedRevision: 3 }),
    ));
    expect(await screen.findByText("Ändringarna har sparats.")).toBeInTheDocument();
  });

  it("requires explicit old or new choices before replacing a duplicate registration", async () => {
    const candidate = {
      ...completeListingAnalysisResponse,
      listing: {
        ...completeListingAnalysisResponse.listing,
        make: {
          value: "Saab",
          provenance: completeListingAnalysisResponse.listing.make!.provenance,
        },
      },
    };
    vi.mocked(analyzeListing).mockResolvedValue(candidate);
    vi.mocked(createSavedListing).mockRejectedValue(new SavedListingApiError(
      "Det finns redan en sparad bil med registreringsnumret.",
      409,
      undefined,
      {
        code: "registrationNumberConflict",
        existingVehicleId: savedListingResponse.vehicleId,
        actualRevision: 3,
      },
    ));
    vi.mocked(getSavedListing).mockResolvedValue(savedListingResponse);
    vi.mocked(replaceSavedListing).mockResolvedValue({ ...savedListingResponse, revision: 4, listingVersion: 3 });
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), candidate.submittedUrl);
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    await screen.findByText("Saab V70 2.4");
    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    const dialog = await screen.findByRole("alertdialog", { name: /ABC123 finns redan/ });
    const replaceButton = screen.getByRole("button", { name: "Ersätt sparad bil" });
    expect(dialog).toHaveFocus();
    expect(replaceButton).toBeDisabled();
    await user.click(screen.getByRole("radio", { name: /Behåll sparad uppgift.*Volvo/ }));
    expect(replaceButton).toBeEnabled();
    await user.click(replaceButton);

    await waitFor(() => expect(replaceSavedListing).toHaveBeenCalledTimes(1));
    const request = vi.mocked(replaceSavedListing).mock.calls[0][1];
    expect(request.listing.draft.make).toMatchObject({
      value: "Volvo",
      provenance: { origin: "user", extractionMethod: "manual", verification: "userConfirmed" },
    });
  });

  it("explicitly attaches a listing to a scenario-only vehicle without replacing its calculation", async () => {
    vi.mocked(analyzeListing).mockResolvedValue(completeListingAnalysisResponse);
    vi.mocked(createSavedListing).mockRejectedValue(new SavedListingApiError(
      "Det finns redan en sparad bil med registreringsnumret.",
      409,
      undefined,
      {
        code: "registrationNumberConflict",
        existingVehicleId: savedListingResponse.vehicleId,
        actualRevision: 7,
      },
    ));
    vi.mocked(getSavedListing).mockRejectedValue(new SavedListingApiError(
      "Den sparade annonsen finns inte längre.",
      404,
      undefined,
      { code: "savedListingNotFound" },
    ));
    vi.mocked(replaceSavedListing).mockResolvedValue({
      ...savedListingResponse,
      revision: 8,
      listingVersion: 1,
      hasSavedCostScenario: true,
    });
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.type(screen.getByLabelText("URL:er"), completeListingAnalysisResponse.submittedUrl);
    await user.click(screen.getByRole("button", { name: "Analysera URL:er" }));
    await screen.findByText("Volvo V70 2.4");
    await user.click(screen.getByRole("button", { name: "Spara bil" }));

    expect(await screen.findByRole("alertdialog", { name: /har redan en sparad kalkyl/ })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Koppla annons till befintlig bil" }));

    await waitFor(() => expect(replaceSavedListing).toHaveBeenCalledWith(
      savedListingResponse.vehicleId,
      expect.objectContaining({ expectedRevision: 7 }),
    ));
    expect(await screen.findByText(/sparade kalkylen finns kvar/)).toBeInTheDocument();
  });

  it("preserves edits after a revision conflict and compares only when requested", async () => {
    vi.mocked(listSavedListings).mockResolvedValue([savedListingSummary]);
    vi.mocked(getSavedListing).mockResolvedValue(savedListingResponse);
    vi.mocked(replaceSavedListing).mockRejectedValue(new SavedListingApiError(
      "Den sparade bilen har ändrats sedan den öppnades.",
      409,
      undefined,
      { code: "revisionConflict", expectedRevision: 3, actualRevision: 4 },
    ));
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    await screen.findByText("Volvo V70 2.4");
    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));
    await user.clear(screen.getByLabelText("Märke"));
    await user.type(screen.getByLabelText("Märke"), "Saab");
    await user.click(screen.getByRole("button", { name: "Spara ändringar" }));

    expect(await screen.findByText(/Ditt utkast finns kvar/)).toBeInTheDocument();
    expect(screen.getByLabelText("Märke")).toHaveValue("Saab");
    expect(getSavedListing).toHaveBeenCalledTimes(1);
    await user.click(screen.getByRole("button", { name: "Jämför med senaste" }));
    await waitFor(() => expect(getSavedListing).toHaveBeenCalledTimes(2));
    expect(await screen.findByRole("alertdialog", { name: /ABC123 finns redan/ })).toBeInTheDocument();
  });

  it("warns about whole-aggregate deletion and retains the open card as an unsaved draft", async () => {
    const combinedSummary = { ...savedListingSummary, hasSavedCostScenario: true };
    const combinedResponse = { ...savedListingResponse, hasSavedCostScenario: true };
    vi.mocked(listSavedListings)
      .mockResolvedValueOnce([combinedSummary])
      .mockResolvedValue([]);
    vi.mocked(getSavedListing).mockResolvedValue(combinedResponse);
    vi.mocked(deleteSavedListing).mockResolvedValue();
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    await user.click(await screen.findByRole("button", { name: "Öppna" }));
    await screen.findByText("Volvo V70 2.4");
    await user.click(screen.getByRole("button", { name: "Radera bilen" }));

    const dialog = screen.getByRole("alertdialog", { name: /Radera ABC123 permanent/ });
    expect(dialog).toHaveTextContent("sparad kalkyl som raderas samtidigt");
    await user.click(screen.getByRole("button", { name: "Radera bilen permanent" }));

    await waitFor(() => expect(deleteSavedListing).toHaveBeenCalledWith(savedListingResponse.vehicleId, 3));
    expect(await screen.findByText(/ligger kvar här som ett osparat utkast/)).toBeInTheDocument();
    expect(screen.getByText("Osparat utkast")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Granska och komplettera alla uppgifter" }));
    expect(screen.getByLabelText("Registreringsnummer")).not.toHaveAttribute("readonly");
  });
});

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}
