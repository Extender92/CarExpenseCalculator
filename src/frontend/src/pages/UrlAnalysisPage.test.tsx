import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  analyzeListing,
  getSystemStatus,
  ListingAnalysisApiError,
  type ListingAnalysisResponse,
} from "@/api/client";
import { completeListingAnalysisResponse } from "@/test/listing-analysis";
import { UrlAnalysisPage } from "./UrlAnalysisPage";

vi.mock("@/api/client", async (importOriginal) => {
  const original = await importOriginal<typeof import("@/api/client")>();
  return {
    ...original,
    analyzeListing: vi.fn(),
    getSystemStatus: vi.fn(),
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
  vi.mocked(getSystemStatus).mockResolvedValue(healthyStatus);
  vi.mocked(analyzeListing).mockReset();
});

describe("Swedish URL analysis workspace", () => {
  it("starts safely and reports invalid, duplicate, local, and excessive URLs", async () => {
    const user = userEvent.setup();
    render(<UrlAnalysisPage />);

    expect(screen.getByText("Inga URL:er har lagts till ännu.")).toBeInTheDocument();
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
