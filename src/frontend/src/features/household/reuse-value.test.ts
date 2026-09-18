import { describe, expect, it } from "vitest";
import { n } from "./numbers";
import { listingReuseValue } from "./reuse-value";

describe("listing reuse value presentation", () => {
  it("translates typed vehicle choices into Swedish", () => {
    expect(listingReuseValue(["petrol", "electricity"], "fuelTypes")).toBe("Bensin, El");
    expect(listingReuseValue("manual", "transmission")).toBe("Manuell");
    expect(listingReuseValue("frontWheelDrive", "drivetrain")).toBe("Framhjulsdrift");
  });
  it("shows kilometres as exact Swedish mil without mutating or rounding the source", () => {
    const source = n("130000.001");
    expect(listingReuseValue(source, "odometerKilometres")).toBe("13000,0001");
    expect(source.text).toBe("130000.001");
    expect(listingReuseValue(n("0.0000000000000000000000000001"), "odometerKilometres"))
      .toBe("0,00000000000000000000000000001");
  });
  it("distinguishes missing, zero, false and an explicit empty collection", () => {
    expect(listingReuseValue(null, "ownerCount")).toBe("Saknas");
    expect(listingReuseValue(n("0"), "ownerCount")).toBe("0");
    expect(listingReuseValue(false, "towBar")).toBe("Nej");
    expect(listingReuseValue([], "fuelTypes")).toBe("Uttryckligen tom samling");
  });
  it("preserves free text even when it resembles a choice code", () => {
    expect(listingReuseValue("manual", "locality")).toBe("manual");
  });
});
