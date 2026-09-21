import { useState } from "react";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { savedListingResponse } from "@/test/listing-analysis";
import { savedListingToReviewState } from "./saved-listings";
import type { ListingWorkspaceItem } from "./review-model";
import { ListingReviewForm } from "./ListingReviewForm";

describe("listing review evidence and error targets", () => {
  it("confirms equipment explicitly and removes confirmation after a later collection edit", async () => {
    let item: ListingWorkspaceItem = { ...savedListingToReviewState(savedListingResponse), id: "review", dirty: false,
      error: null, persistenceNotice: null, saving: false, validationErrors: {}, controller: null };
    function Harness() {
      const [current, setCurrent] = useState(item);
      return <ListingReviewForm item={current} onChange={(draft, errors = {}) => { item = { ...current, draft, validationErrors: errors }; setCurrent(item); }} />;
    }
    render(<Harness />);
    await userEvent.click(screen.getByText("Utrustning och uppgifter från säljaren", { selector: "summary" }));
    await userEvent.click(screen.getByRole("button", { name: "Bekräfta utrustning" }));
    expect(item.draft.equipment.provenance?.verification).toBe("userConfirmed");
    expect(item.draft.fields.ownerCount.provenance?.verification).toBe("unverified");
    await userEvent.type(screen.getByLabelText("Utrustning 1"), " extra");
    expect(item.draft.equipment.provenance?.verification).toBe("unverified");
  });
  it("opens the collapsed section and focuses the specific invalid control", async () => {
    const item: ListingWorkspaceItem = { ...savedListingToReviewState(savedListingResponse), id: "review", dirty: false,
      error: null, persistenceNotice: null, saving: false, validationErrors: { "details.seats": "Felaktigt sittplatsantal" }, controller: null };
    render(<ListingReviewForm item={item} onChange={() => {}} />);
    await userEvent.click(screen.getByRole("button", { name: "Felaktigt sittplatsantal" }));
    expect(screen.getByLabelText("Sittplatser")).toHaveFocus();
    expect(screen.getByLabelText("Sittplatser")).toBeVisible();
  });
});

it("offers an empty confirmation selection, confirms only chosen current values, and never assigns registry evidence", async () => {
  let item: ListingWorkspaceItem = { ...savedListingToReviewState(savedListingResponse), id: "selection", dirty: false,
    error: null, persistenceNotice: null, saving: false, validationErrors: {}, controller: null };
  function Harness() {
    const [current, setCurrent] = useState(item);
    return <ListingReviewForm item={current} onChange={draft => { item = { ...current, draft }; setCurrent(item); }} />;
  }
  render(<Harness />);
  await userEvent.click(screen.getByRole("button", { name: "Bekräfta uppgifter" }));
  const dialog = screen.getByRole("dialog");
  for (const checkbox of within(dialog).getAllByRole("checkbox")) expect(checkbox).not.toBeChecked();
  expect(within(dialog).getByRole("button", { name: "Bekräfta valda värden" })).toBeDisabled();
  await userEvent.click(within(dialog).getByRole("checkbox", { name: "Antal ägare: 4" }));
  await userEvent.click(within(dialog).getByRole("button", { name: "Bekräfta valda värden" }));
  expect(item.draft.fields.ownerCount.provenance?.verification).toBe("userConfirmed");
  expect(item.draft.fields.ownerCount.provenance?.origin).toBe("user");
  expect(item.draft.fields.priceSek.provenance?.verification).toBe("unverified");
  await userEvent.clear(screen.getByLabelText("Antal ägare"));
  await userEvent.type(screen.getByLabelText("Antal ägare"), "5");
  expect(item.draft.fields.ownerCount.provenance?.verification).toBe("unverified");
});
