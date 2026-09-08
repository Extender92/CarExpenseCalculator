import { useState } from "react";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { n, stringifyExact } from "@/features/household/numbers";
import { ComparisonTables } from "./Tables";
import { FactsEditor } from "./FactsEditor";
import { RulesEditor } from "./RulesEditor";
import type { FactWrite, Rules } from "./api";
import { manualRequest, response, unknownFacts } from "./test-fixtures";

function FactForm({
  onChange = vi.fn(),
}: {
  onChange?: (value: FactWrite) => void;
}) {
  const [input, set] = useState<FactWrite>({});
  return (
    <FactsEditor
      input={input}
      value={unknownFacts()}
      manualMode
      prefix="facts"
      errors={{}}
      onChange={(value) => {
        set(value);
        onChange(value);
      }}
    />
  );
}
function RuleForm({ onChange }: { onChange: (value: Rules) => void }) {
  const [value, set] = useState<Rules>({});
  return (
    <RulesEditor
      value={value}
      errors={{}}
      onChange={(value) => {
        set(value);
        onChange(value);
      }}
    />
  );
}
describe("comparison forms", () => {
  it("keeps explicit false distinct from unknown and never offers registry verification as a fact action", async () => {
    const changed = vi.fn();
    const user = userEvent.setup();
    render(<FactForm onChange={changed} />);
    const section = screen.getByText("Dragkrok · Okänt").closest("details")!;
    await user.click(within(section).getByText("Dragkrok · Okänt"));
    await user.selectOptions(
      within(section).getByLabelText("Åtgärd för Dragkrok"),
      "manual",
    );
    await user.selectOptions(within(section).getByLabelText("Värde"), "false");
    expect(changed.mock.lastCall![0].edits.towBar.manual.value).toBe(false);
    expect(
      within(section).queryByRole("option", { name: "Registerverifierat" }),
    ).not.toBeInTheDocument();
    await user.selectOptions(
      within(section).getByLabelText("Åtgärd för Dragkrok"),
      "unknown",
    );
    expect(changed.mock.lastCall![0].edits.towBar).toEqual({ kind: "unknown" });
  });
  it("keeps exact mil conversion and a zero fact value", async () => {
    const changed = vi.fn();
    const user = userEvent.setup();
    render(<FactForm onChange={changed} />);
    const section = screen
      .getByText("Mätarställning (mil) · Okänt")
      .closest("details")!;
    await user.click(within(section).getByText("Mätarställning (mil) · Okänt"));
    await user.selectOptions(
      within(section).getByLabelText("Åtgärd för Mätarställning (mil)"),
      "manual",
    );
    await user.type(
      within(section).getByLabelText("Värde"),
      "20000,1234567890123456789",
    );
    expect(
      changed.mock.lastCall![0].edits.odometerKilometres.manual.value.text,
    ).toBe("200001.234567890123456789");
    await user.clear(within(section).getByLabelText("Värde"));
    await user.type(within(section).getByLabelText("Värde"), "0");
    expect(
      changed.mock.lastCall![0].edits.odometerKilometres.manual.value.text,
    ).toBe("0");
  });
  it("starts with no rule, requires an evidence choice, and lets weights change without saving", async () => {
    const changed = vi.fn();
    const user = userEvent.setup();
    render(<RuleForm onChange={changed} />);
    const price = screen.getByText("Köppris (kr)").closest("details")!;
    await user.click(within(price).getByText("Köppris (kr)"));
    await user.click(
      within(price).getByRole("button", { name: "Lägg till prioritering" }),
    );
    expect(changed.mock.lastCall![0].preferences[0].minimumEvidence).toBe("");
    await user.selectOptions(
      within(price).getByLabelText("Prioriteringens verifiering"),
      "userConfirmed",
    );
    await user.clear(within(price).getByLabelText("Vikt (0–5)"));
    await user.type(within(price).getByLabelText("Vikt (0–5)"), "3");
    await user.type(
      within(price).getByLabelText("Värde som ger 0 poäng"),
      "100000",
    );
    await user.type(
      within(price).getByLabelText("Värde som ger 100 poäng"),
      "20000",
    );
    expect(stringifyExact(changed.mock.lastCall![0])).toContain('"weight":3');
    expect(
      screen.queryByRole("button", { name: /spara/i }),
    ).not.toBeInTheDocument();
  });
  it("retains absent notes separately from an explicitly cleared collection", async () => {
    const changed = vi.fn();
    const user = userEvent.setup();
    render(<FactForm onChange={changed} />);
    await user.click(screen.getByText("Skick- och reparationsuppgifter"));
    await user.click(
      screen.getByRole("button", { name: "Redigera skickuppgifter" }),
    );
    expect(changed.mock.lastCall![0].edits.conditionNotes).toEqual([]);
    await user.click(
      screen.getByRole("button", { name: "Behåll sparad samling" }),
    );
    expect(changed.mock.lastCall![0].edits.conditionNotes).toBeUndefined();
  });
});
describe("comparison tables", () => {
  it("shows intervals, coverage, and partial costs without recalculating them", () => {
    const reply = response(manualRequest());
    const rows = reply.views.baseline.candidates;
    rows[0].score = { lower: n(45), upper: n(85) };
    rows[0].coveragePercent = n(60);
    rows[0].cost.totals.ownershipCost = {
      state: "partial",
      knownSubtotalSek: n(20750),
      completeTotalSek: null,
      missingComponents: ["input.tax"],
      errors: [],
    };
    render(
      <MemoryRouter>
        <ComparisonTables
          rows={rows}
          response={reply}
          stale={false}
          manual
          expanded={[]}
          onExpanded={vi.fn()}
          select={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("[45,00, 85,00]")).toBeVisible();
    expect(screen.getByText("60,00 %")).toBeVisible();
    expect(screen.getByText("känd del")).toBeVisible();
    expect(screen.getByRole("link", { name: /fordonsskatt/i })).toHaveAttribute(
      "href",
      expect.stringContaining("field=input.tax"),
    );
  });
  it("hides recommendation badges whenever the generation is stale", () => {
    const reply = response(manualRequest());
    const rows = reply.views.baseline.candidates;
    rows[0].isCheapestEligibleComplete = true;
    rows[0].isDefinitePreferenceWinner = true;
    render(
      <MemoryRouter>
        <ComparisonTables
          rows={rows}
          response={reply}
          stale
          manual
          expanded={[]}
          onExpanded={vi.fn()}
          select={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(
      screen.queryByText("Säker preferensvinnare"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText("Billigast bland godkända kompletta alternativ"),
    ).not.toBeInTheDocument();
  });
  it("keeps complete rejected cars visible and leaves their status separate from scores", () => {
    const reply = response(manualRequest());
    const rows = reply.views.baseline.candidates;
    rows[0].eligibility = "rejected";
    render(
      <MemoryRouter>
        <ComparisonTables
          rows={rows}
          response={reply}
          stale={false}
          manual
          expanded={[]}
          onExpanded={vi.fn()}
          select={vi.fn()}
        />
      </MemoryRouter>,
    );
    expect(screen.getByText("Bortvald")).toBeVisible();
    expect(screen.getByText("[85,00, 85,00]")).toBeVisible();
  });
});
