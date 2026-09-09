import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, within, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Numeric, n, stringifyExact } from "@/features/household/numbers";
import { deferred } from "@/features/household/test-fixtures";
import { captureReport, reportRows } from "./report-model";
import { inputRows } from "./report-format";
import { ReportDocument, ReportPreview } from "./Report";
import { manualRequest, response } from "./test-fixtures";

afterEach(() => vi.restoreAllMocks());

describe("immutable complete comparison reports", () => {
  it("names payment directions and categories in Swedish without rewriting source text", () => {
    const rows = inputRows({
      sources: [
        {
          category: "repairAllowance",
          direction: "internalSaving",
          label: "Workshop quote",
        },
        { category: "leasePayments", direction: "outflow" },
        { category: "depositRefund", direction: "inflow" },
      ],
    });
    expect(rows.map((r) => r.value)).toEqual([
      "Reparationssparande",
      "Internt sparande",
      "Workshop quote",
      "Leasingbetalningar",
      "Utbetalning",
      "Återbetald deposition",
      "Återbetalning",
    ]);
  });
  it.each([1, 50, 51, 101, 250])(
    "preserves every candidate and both server orders for %i cars",
    (count) => {
      const source = response(manualRequest(count));
      source.views.baseline.scoreOrder.reverse();
      const report = captureReport(source, "score");
      expect(reportRows(report).map((r) => r.vehicleId)).toEqual(
        source.views.baseline.scoreOrder,
      );
      expect(new Set(reportRows(report).map((r) => r.vehicleId)).size).toBe(
        count,
      );
      for (const mode of ["baseline", "favorable", "cautious"] as const)
        expect(report.response.views[mode].candidates).toHaveLength(count);
    },
  );
  it("copies sources, all inputs, dirty marks, results and exact numbers without sharing mutable objects", () => {
    const source = response(manualRequest(2));
    source.views.baseline.profile.purchaseCashSek = n(
      "40000.12345678901234567890123",
    );
    source.views.baseline.candidates[0].sourceRevisions.vehicle =
      n("9007199254740993");
    source.views.baseline.candidates[0].unsaved.costConfirmation = true;
    const report = captureReport(
      source,
      "cost",
      new Date("2026-09-09T10:00:00Z"),
      "Europe/Stockholm",
    );
    const before = stringifyExact(report);
    source.views.baseline.profile.purchaseCashSek = n(0);
    source.views.baseline.costOrder.reverse();
    source.views.baseline.candidates[0].unsaved.costConfirmation = false;
    source.views.baseline.candidates[0].registrationNumber = "NEW111";
    expect(stringifyExact(report)).toBe(before);
    expect(
      report.response.views.baseline.profile.purchaseCashSek,
    ).toBeInstanceOf(Numeric);
    expect(reportRows(report)[0].sourceRevisions.vehicle?.text).toBe(
      "9007199254740993",
    );
    expect(
      Object.isFrozen(report.response.views.baseline.candidates[0].unsaved),
    ).toBe(true);
    expect(report.capturedAt).toBe("2026-09-09T10:00:00.000Z");
    expect(report.response.views.baseline.asOfDate).toBe("2026-09-08");
  });
  it("does not round inputs or rule anchors and keeps kilometre/mil conversion exact", () => {
    const rows = inputRows({
      annualDistanceKilometres: n("200000.1234567890123456789"),
      preferences: [
        {
          zeroPoint: n("0.1234567890123456789000"),
          fullPoint: n(100),
          weight: n(0),
        },
      ],
      sourceRevisions: { vehicle: n("9007199254740993") },
    });
    expect(rows[0].value).toBe(
      "200000,1234567890123456789 km (20000,01234567890123456789 mil)",
    );
    expect(rows.find((r) => r.path.endsWith("zeroPoint"))?.value).toBe(
      "0,1234567890123456789000",
    );
    expect(rows.at(-1)?.value).toBe("9007199254740993");
  });
  it("distinguishes unknown, zero, false, empty, included and conflicting observations", () => {
    const rows = inputRows({
      priceSek: null,
      additionalRepairAllowancePerMonthSek: { single: n(0) },
      tax: { isIncluded: true, items: [] },
      facts: {
        towBar: {
          state: "conflicting",
          observations: [{ value: false }, { value: true }],
        },
      },
    });
    expect(rows.map((r) => r.value)).toEqual(
      expect.arrayContaining([
        "Okänt / ej angivet",
        "0",
        "Ja",
        "Nej",
        "Bekräftad tom samling – inga kostnadsposter",
        "Motstridigt",
      ]),
    );
  });
  it("preserves maximum legacy entries and long source notes without a text or collection cap", () => {
    const note = "Årlig service – ägaren önskar översyn. ".repeat(80);
    const rows = inputRows(
      Array.from({ length: 100 }, (_, i) => ({
        input: {
          key: `legacy-${i}`,
          kind: i < 50 ? "recurring" : "oneTime",
          label: note,
          amountSek: n(i),
        },
        reason: "undatedOneTimeCost",
        affectedSections: ["ownership"],
      })),
    );
    expect(rows.filter((r) => r.path.endsWith(".key"))).toHaveLength(100);
    expect(rows.find((r) => r.path === "[99].input.label")?.value).toBe(note);
  });
});

describe("report presentation", () => {
  it("prints all detail groups and captured recommendations while retaining partial amounts, gaps and dirty evidence", () => {
    const source = response(manualRequest(2), 2, "20750.00");
    const a = source.views.baseline.candidates[0];
    a.isCheapestEligibleComplete = true;
    a.unsaved.profile = true;
    a.unsaved.rules = true;
    a.unsaved.costConfirmation = true;
    a.cost.payments.externalOutflow.completeTotalSek = n(80750);
    a.cost.payments.internalSaving.completeTotalSek = n(3600);
    a.cost.startupBudget.status = "exceeded";
    const b = source.views.baseline.candidates[1];
    b.cost.totals.ownershipCost = {
      state: "partial",
      knownSubtotalSek: n(7200),
      completeTotalSek: null,
      missingComponents: ["residualHorizonMismatch"],
      errors: [],
    };
    b.score = { lower: n(45), upper: n(85) };
    b.coveragePercent = n(60);
    b.eligibility = "rejected";
    render(<ReportDocument report={captureReport(source, "cost")} />);
    const main = screen.getByRole("table", { name: "Huvudjämförelse" });
    expect(within(main).getByText("20 750,00 kr")).toBeInTheDocument();
    expect(within(main).getByText("7 200,00 kr känd del")).toBeInTheDocument();
    expect(within(main).getByText("[45,00, 85,00]")).toBeInTheDocument();
    expect(within(main).getByText("60,00 %")).toBeInTheDocument();
    expect(within(main).getByText("Bortvald")).toBeInTheDocument();
    expect(
      within(main).getByText("Billigast bland godkända kompletta alternativ"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        /Osparat: hushållsprofil, köpkrav\/prioriteringar, kostnadsbekräftelse/,
      ),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/80 750,00 kr/).length).toBeGreaterThan(0);
    expect(
      screen.getAllByText(
        /Fast restvärde gäller en annan period|Restvärdet gäller en annan ägandeperiod/,
      ).length,
    ).toBeGreaterThan(0);
    for (const name of [
      "Effektivt ekonomiskt underlag",
      "Fordonsfakta och källor",
      "Granskning och fältfel",
      "Leasing",
      "Kostnadsavstämning och budgetar",
    ])
      expect(
        screen.getByRole("table", { name: `TAA100 – ${name}` }),
      ).toBeInTheDocument();
  });
  it("keeps all 121 monthly rows and individual category provenance", () => {
    const source = response(manualRequest());
    const c = source.views.baseline.candidates[0];
    c.cost.payments.months = Array.from({ length: 121 }, (_, i) => ({
      monthOffset: n(i),
      calendarMonth: null,
      outflow: c.cost.financing,
      inflow: c.cost.financing,
      internalSaving: c.cost.financing,
      categories: [
        {
          category: "loanInterest",
          direction: "outflow",
          amount: c.cost.financing,
        },
      ],
    }));
    render(<ReportDocument report={captureReport(source, "cost")} />);
    const calendar = screen.getByRole("table", {
      name: "TAA100 – Betalningskalender",
    });
    expect(calendar.querySelectorAll("[data-report-month]")).toHaveLength(121);
    expect(within(calendar).getByText("Start (0)")).toBeInTheDocument();
    expect(
      within(calendar).getByText("Månad 120 – kalendermånad okänd – Låneränta"),
    ).toBeInTheDocument();
  });
  it("waits for fonts, focuses the preview, and preserves the report when print is cancelled", async () => {
    const fonts = deferred<FontFaceSet>();
    vi.stubGlobal("document", document);
    const original = Object.getOwnPropertyDescriptor(document, "fonts");
    Object.defineProperty(document, "fonts", {
      configurable: true,
      value: { ready: fonts.promise },
    });
    const print = vi
      .spyOn(window, "print")
      .mockImplementation(() => window.dispatchEvent(new Event("afterprint")));
    try {
      const user = userEvent.setup();
      render(
        <MemoryRouter>
          <ReportPreview
            report={captureReport(response(manualRequest()), "cost")}
          />
        </MemoryRouter>,
      );
      const button = screen.getByRole("button", {
        name: "Skriv ut / Spara som PDF",
      });
      expect(button).toBeDisabled();
      expect(
        screen.getByRole("heading", { name: "Förhandsvisning inför PDF" }),
      ).toHaveFocus();
      await act(async () => fonts.resolve({} as FontFaceSet));
      await vi.waitFor(() => expect(button).toBeEnabled());
      await user.click(button);
      await user.click(button);
      expect(print).toHaveBeenCalledTimes(2);
      expect(screen.getByRole("article")).toBeInTheDocument();
      expect(screen.queryByText(/PDF har sparats/)).not.toBeInTheDocument();
    } finally {
      if (original) Object.defineProperty(document, "fonts", original);
      else Reflect.deleteProperty(document, "fonts");
      vi.unstubAllGlobals();
    }
  });
});
