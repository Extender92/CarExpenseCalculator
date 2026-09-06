import { afterEach, expect, it, vi } from "vitest";
import { savedListingResponse } from "@/test/listing-analysis";
import { householdApi } from "./api";
import { fromOrdinary, n, stringifyExact } from "./numbers";
import { readListingForHouseholdDraft } from "./listing-read";
import { reviewedListingForDraft } from "./listing-draft";

afterEach(() => vi.restoreAllMocks());
it("preserves the exact listing amounts, provenance and origin revision for a shared draft", async () => {
  const exact = fromOrdinary(savedListingResponse);
  exact.revision = n("9007199254740993");
  exact.listing.priceSek!.value = n("123456.123456789012345");
  exact.listing.odometerKilometres!.value = n("98765.123456789012345");
  exact.listing.energyConsumptions!.values[0].consumptionPer100Kilometres = n(
    "1.1234567890123456789",
  );
  vi.spyOn(householdApi, "listing").mockResolvedValue(exact);
  const state = await readListingForHouseholdDraft(exact.vehicleId);
  const built = reviewedListingForDraft({
    ...state,
    id: "listing",
    dirty: false,
    error: null,
    persistenceNotice: null,
    saving: false,
    validationErrors: {},
    controller: null,
  });
  expect(state.householdBaseRevision).toBe("9007199254740993");
  expect(stringifyExact(built.listing)).toContain("123456.123456789012345");
  expect(stringifyExact(built.listing)).toContain("98765.123456789012345");
  expect(stringifyExact(built.listing)).toContain("1.1234567890123456789");
  expect(built.listing.draft.priceSek?.provenance).toEqual(
    exact.listing.priceSek?.provenance,
  );
});
