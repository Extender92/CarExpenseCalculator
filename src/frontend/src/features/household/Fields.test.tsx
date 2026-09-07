import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ProfileFields, VehicleFields } from "./Fields";
import { Section, VehicleResults } from "./Results";
import { InputFacts, LegacyReviewEditor } from "./Review";
import {
  initialProfile,
  initialVehicle,
  remainingReviews,
  validateVehicle,
  validateCostWrite,
  validateFields,
  profileFields,
  type FormErrors,
} from "./form-model";
import { n, parseExact, stringifyExact } from "./numbers";
import { candidate, previewVehicle, section } from "./test-fixtures";
import type {
  CostWrite,
  LegacyReview,
  ProfileInput,
  VehicleInput,
} from "./api";

function VehicleHarness({
  initial = initialVehicle(),
  errors = {},
}: {
  initial?: VehicleInput;
  errors?: FormErrors;
}) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <VehicleFields value={value} onChange={setValue} errors={errors} />
      <output data-testid="input">
        {(() => {
          try {
            return stringifyExact(value);
          } catch {
            return "Ogiltig text";
          }
        })()}
      </output>
    </>
  );
}
function ProfileHarness({
  initial = initialProfile(),
}: {
  initial?: ProfileInput;
}) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <ProfileFields value={value} onChange={setValue} errors={{}} />
      <output data-testid="input">
        {(() => {
          try {
            return stringifyExact(value);
          } catch {
            return "Ogiltig text";
          }
        })()}
      </output>
    </>
  );
}

describe("Swedish household form semantics", () => {
  it("reads a stored sensitivity trio even when the API includes single: null", () => {
    const input = parseExact<ProfileInput>(
      '{"electricDrivingSharePercent":{"single":null,"favorable":70,"baseline":50,"cautious":30}}',
    );
    render(<ProfileHarness initial={input} />);
    expect(
      screen.getByLabelText("Typ av värde för Elandel av körsträckan (%)"),
    ).toHaveValue("trio");
    expect(
      screen.getByLabelText("Elandel av körsträckan (%) – gynnsamt"),
    ).toHaveValue("70");
    expect(
      screen.getByLabelText("Elandel av körsträckan (%) – normalt"),
    ).toHaveValue("50");
    expect(
      screen.getByLabelText("Elandel av körsträckan (%) – försiktigt"),
    ).toHaveValue("30");
    expect(
      validateFields(
        { ...initialProfile(), ...input },
        profileFields,
        "profile",
      ),
    ).toEqual({});
  });
  it("names recovered legacy assumptions and cost collections in Swedish", () => {
    render(
      <InputFacts
        value={{
          calculationPeriodMonths: n(24),
          expectedResidualValueSek: n(15000),
          vehicleTax: null,
          maintenanceAndRepairs: null,
          otherRecurringCosts: [],
          otherOneTimeCosts: [],
        }}
      />,
    );
    for (const label of [
      "Äldre kalkylperiod (månader)",
      "Äldre fast restvärde (kr)",
      "Fordonsskatt",
      "Kombinerat äldre underhåll",
      "Äldre återkommande kostnader",
      "Äldre engångskostnader",
    ])
      expect(screen.getByText(label, { exact: true })).toBeInTheDocument();
  });
  it("starts without economic defaults and distinguishes zero from empty numeric input", () => {
    render(<ProfileHarness />);
    const cash = screen.getByLabelText("Kontanter till bilköpet (kr)");
    expect(cash).toHaveValue("");
    expect(screen.getByLabelText("Aktivt osäkerhetsläge")).toHaveValue(
      "baseline",
    );
    fireEvent.change(cash, { target: { value: "0" } });
    expect(screen.getByTestId("input")).toHaveTextContent(
      '"purchaseCashSek":0',
    );
    fireEvent.change(cash, { target: { value: "" } });
    expect(screen.getByTestId("input")).toHaveTextContent(
      '"purchaseCashSek":null',
    );
  });
  it("preserves exact mil conversion and marks unfinished text as invalid rather than zero", () => {
    render(<ProfileHarness />);
    const distance = screen.getByLabelText("Årlig körsträcka (mil)");
    fireEvent.change(distance, { target: { value: "1,1234567890123456789" } });
    expect(screen.getByTestId("input")).toHaveTextContent(
      '"annualDistanceKilometres":11.234567890123456789',
    );
    fireEvent.change(distance, { target: { value: "1," } });
    expect(distance).toHaveValue("1,");
    expect(screen.getByTestId("input")).toHaveTextContent("Ogiltig text");
  });
  it("keeps all three sensitivity entries independent", async () => {
    render(<ProfileHarness />);
    const user = userEvent.setup();
    await user.selectOptions(
      screen.getByLabelText("Typ av värde för Elandel av körsträckan (%)"),
      "trio",
    );
    await user.type(
      screen.getByLabelText("Elandel av körsträckan (%) – normalt"),
      "40",
    );
    expect(
      screen.getByLabelText("Elandel av körsträckan (%) – gynnsamt"),
    ).toHaveValue("");
    expect(screen.getByTestId("input")).toHaveTextContent(
      '"electricDrivingSharePercent":{"favorable":null,"baseline":40,"cautious":null}',
    );
    await user.selectOptions(
      screen.getByLabelText("Aktivt osäkerhetsläge"),
      "cautious",
    );
    expect(
      screen.getByLabelText("Elandel av körsträckan (%) – normalt"),
    ).toHaveValue("40");
  });
  it("distinguishes unknown, confirmed empty and included costs with extras", async () => {
    render(<VehicleHarness />);
    const user = userEvent.setup();
    await user.click(screen.getByText("Skatt och försäkring", { exact: true }));
    const mode = screen.getByLabelText("Uppgifter om fordonsskatt");
    expect(mode).toHaveValue("unknown");
    await user.selectOptions(mode, "empty");
    expect(screen.getByTestId("input")).toHaveTextContent(
      '"tax":{"isIncluded":false,"items":[]}',
    );
    await user.selectOptions(mode, "included");
    await user.click(
      screen.getByRole("button", { name: "Lägg till post i fordonsskatt" }),
    );
    await user.type(screen.getByLabelText("Benämning"), "Tillägg");
    expect(screen.getByTestId("input")).toHaveTextContent('"isIncluded":true');
    expect(
      within(screen.getByLabelText("Typ av värde för Belopp (kr)")).queryByRole(
        "option",
        { name: "Tre osäkerhetsvärden" },
      ),
    ).not.toBeInTheDocument();
  });
  it("fills lease payment intervals only on explicit action, without defaulting final obligations", async () => {
    render(<VehicleHarness />);
    const user = userEvent.setup();
    await user.selectOptions(
      screen.getByLabelText("Anskaffningsform"),
      "lease",
    );
    expect(screen.getByLabelText("Förhöjd första avgift (kr)")).toHaveValue("");
    await user.type(screen.getByLabelText("Till månad"), "3");
    await user.type(
      screen.getByLabelText("Avgift för varje månad (kr)"),
      "123,1234567890123456789",
    );
    expect(screen.getByTestId("input")).not.toHaveTextContent(
      "monthlyPayments",
    );
    await user.click(
      screen.getByRole("button", { name: "Fyll betalningsmånaderna" }),
    );
    const text = screen.getByTestId("input").textContent!;
    expect(text.match(/123.1234567890123456789/g)).toHaveLength(3);
    expect(text).not.toContain("depositRefundSek");
    expect(text).not.toContain("endFees");
  });
  it("associates Swedish field errors with the relevant control", () => {
    render(<VehicleHarness errors={{ "input.priceSek": ["Ange ett tal."] }} />);
    const price = screen.getByLabelText("Inköpspris (kr)");
    expect(price).toHaveAttribute("aria-invalid", "true");
    expect(price).toHaveAccessibleDescription("Ange ett tal.");
  });
});

describe("results and legacy review presentation", () => {
  it("keeps invalid or duplicate legacy targets unresolved instead of declaring a complete preview", () => {
    const reviews: LegacyReview[] = [
      {
        input: { key: "old-energy", kind: "energy", label: "Bränsle" },
        reason: "energyIdentityRequired",
        affectedSections: ["energy"],
      },
    ];
    const cost: CostWrite = {
      input: {
        candidateKey: "car",
        customCosts: {
          isIncluded: false,
          items: [{ key: "target", label: "Service", cadence: "monthly" }],
        },
        energySources: [{ key: "energy-target" }],
      },
      legacyDecisions: [
        { key: "old-energy", disposition: "map", targetKey: "target" },
      ],
    };
    expect(
      remainingReviews(reviews, cost.legacyDecisions, cost.input),
    ).toHaveLength(1);
    expect(
      validateCostWrite(cost, reviews)["review.old-energy.targetKey"],
    ).toBeDefined();
    cost.legacyDecisions![0].targetKey = "energy-target";
    expect(
      remainingReviews(reviews, cost.legacyDecisions, cost.input),
    ).toHaveLength(0);
    cost.legacyDecisions!.push({
      key: "another",
      disposition: "map",
      targetKey: "energy-target",
    });
    expect(
      remainingReviews(reviews, cost.legacyDecisions, cost.input),
    ).toHaveLength(1);
  });
  it("shows calendar completeness as a state without inventing a monetary total", () => {
    render(
      <Section
        label="Kalenderns fullständighet"
        value={section(0)}
        statusOnly
      />,
    );
    expect(screen.getByText("Komplett")).toBeVisible();
    expect(screen.queryByText(/kr/)).not.toBeInTheDocument();
  });
  it("rejects multiple URLs in a single evidence link", () => {
    const input = initialVehicle();
    input.customCosts = {
      isIncluded: false,
      items: [
        {
          key: "evidence",
          label: "Underlag",
          cadence: "monthly",
          sourceUrl: "https://example.com/one\nhttps://example.com/two",
        },
      ],
    };
    expect(
      validateVehicle(input)["input.customCosts.items[0].sourceUrl"],
    ).toBeDefined();
    input.customCosts.items![0].sourceUrl = "https://example.com/one";
    expect(
      validateVehicle(input)["input.customCosts.items[0].sourceUrl"],
    ).toBeUndefined();
  });
  it("uses the API budget verdict even when rounded displayed amounts equal the limit", () => {
    const value = previewVehicle(candidate(1));
    value.sections.monthlyBudget.status = "exceeded";
    value.sections.monthlyBudget.fundingRequired = section("100.00");
    render(<VehicleResults result={value} />);
    expect(screen.getByText("Budgeten överskrids")).toBeVisible();
    expect(screen.queryByText("Inom budget")).not.toBeInTheDocument();
  });
  it("keeps known subtotals separate from unavailable totals and does not hide zero", () => {
    const value = previewVehicle(candidate(1));
    value.sections.totals.ownershipCost = {
      ...section(0),
      state: "partial",
      completeTotalSek: null,
      missingComponents: ["vehicles[0].input.service"],
    };
    value.isCostComparable = false;
    render(<VehicleResults result={value} />);
    expect(screen.getByText("känd del")).toBeVisible();
    expect(
      screen.getByText(
        "Kalkylen är ofullständig. Kända delkostnader visas nedan.",
      ),
    ).toBeVisible();
  });
  it("preserves 100 review sources and sends only unresolved items after an explicit mapping", async () => {
    const reviews: LegacyReview[] = Array.from({ length: 100 }, (_, index) => ({
      input: {
        key: `legacy-${index}`,
        kind: index < 50 ? "recurring" : "oneTime",
        label: `Post ${index}`,
        amountSek: n(index),
      },
      reason: "paymentTimingRequired",
      affectedSections: ["customCosts"],
    }));
    let current: CostWrite = {
      input: {
        candidateKey: "car",
        customCosts: {
          isIncluded: false,
          items: [
            {
              key: "target",
              label: "Vald målpost",
              amountSek: { single: n(0) },
              cadence: "monthly",
            },
          ],
        },
      },
    };
    const update = vi.fn((value: CostWrite) => {
      current = value;
      rerender(
        <LegacyReviewEditor
          reviews={reviews}
          value={current}
          onChange={update}
        />,
      );
    });
    const { rerender } = render(
      <LegacyReviewEditor
        reviews={reviews}
        value={current}
        onChange={update}
      />,
    );
    expect(screen.getAllByLabelText(/^Beslut för Post/)).toHaveLength(100);
    // Scope repeated label queries to their fieldset. Searching the entire
    // 100-row jsdom tree for every label is quadratic on slower CI runners.
    const firstRow = within(
      screen.getByText("Post 0", { selector: "legend" }).closest("fieldset")!,
    );
    const lastRow = within(
      screen.getByText("Post 99", { selector: "legend" }).closest("fieldset")!,
    );
    fireEvent.change(firstRow.getByLabelText("Beslut för Post 0"), {
      target: { value: "map" },
    });
    fireEvent.change(firstRow.getByLabelText("Målpost för Post 0"), {
      target: { value: "target" },
    });
    expect(remainingReviews(reviews, current.legacyDecisions)).toHaveLength(99);
    expect(current.input.customCosts?.items).toHaveLength(1);
    fireEvent.change(lastRow.getByLabelText("Beslut för Post 99"), {
      target: { value: "discard" },
    });
    expect(remainingReviews(reviews, current.legacyDecisions)).toHaveLength(98);
    expect(reviews).toHaveLength(100);
  });
});
