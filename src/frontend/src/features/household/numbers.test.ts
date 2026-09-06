import { describe, expect, it } from "vitest";
import {
  canonicalNumber,
  cloneExact,
  formatNumeric,
  n,
  parseExact,
  shiftDecimal,
  stringifyExact,
} from "./numbers";

describe("exact household JSON and editable numbers", () => {
  it("round-trips decimals and revisions beyond IEEE-754 without strings in JSON", () => {
    const json =
      '{"price":123.1234567890123456789,"revision":9007199254740993,"small":0.0000000000000000000000000001,"maximum":79228162514264337593543950335}';
    const parsed = parseExact<{ price: number; revision: number }>(json);
    expect(parsed.price.text).toBe("123.1234567890123456789");
    expect(parsed.revision.text).toBe("9007199254740993");
    expect(stringifyExact(cloneExact(parsed))).toBe(json);
  });
  it("keeps missing, explicit zero, empty arrays and included extras distinct", () => {
    expect(
      stringifyExact({
        absent: undefined,
        unknown: null,
        zero: n(0),
        empty: [],
        included: { isIncluded: true, items: [{ amount: n("0,15") }] },
      }),
    ).toBe(
      '{"unknown":null,"zero":0,"empty":[],"included":{"isIncluded":true,"items":[{"amount":0.15}]}}',
    );
  });
  it.each([
    "",
    " ",
    "abc",
    "1.2.3",
    "12kr",
    "NaN",
    "Infinity",
    "1e3",
    "1 000",
    "1,",
  ])("does not turn invalid text %j into zero", (value) => {
    expect(() => canonicalNumber(value)).toThrow();
  });
  it.each([
    "79228162514264337593543950336",
    "0.00000000000000000000000000001",
    "7922816251426433759354395033.6",
  ])("rejects decimal overflow or lost precision %s", (value) => {
    expect(() => stringifyExact(n(value))).toThrow();
  });
  it("converts miles and kilometres exactly including smallest supported decimals", () => {
    const km = "12345.12345678901234567890123";
    expect(shiftDecimal(shiftDecimal(km, -1), 1)).toBe(km);
    expect(shiftDecimal("1,1234567890123456789", 1)).toBe(
      "11.234567890123456789",
    );
    expect(shiftDecimal("0.0000000000000000000000000001", -1)).toBe(
      "0.00000000000000000000000000001",
    );
  });
  it("rounds only presentation away from zero, leaving the source unchanged", () => {
    const value = n("-1000.005");
    expect(formatNumeric(value)).toBe("−1 000,01");
    expect(value.text).toBe("-1000.005");
    expect(formatNumeric(n("0.0005"), 3)).toBe("0,001");
  });
});
