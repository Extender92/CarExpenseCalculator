import { useState } from "react";
import { render, screen } from "@testing-library/react";
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
