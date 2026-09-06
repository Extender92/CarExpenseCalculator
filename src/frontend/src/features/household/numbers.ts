import {
  isLosslessNumber,
  LosslessNumber,
  parse,
  stringify,
} from "lossless-json";

/** Editable numeric text. It deliberately permits unfinished/invalid user input. */
export class Numeric {
  constructor(public readonly text: string) {}
}

export type Exact<T> = T extends number
  ? Numeric
  : T extends readonly (infer U)[]
    ? Exact<U>[]
    : T extends object
      ? { [K in keyof T]: Exact<T[K]> }
      : T;

export const n = (value: string | number) => new Numeric(String(value));
export const numericText = (value: Numeric | null | undefined) =>
  value?.text ?? "";

export function canonicalNumber(text: string, bounded = true): string {
  const value = text.trim().replace(",", ".");
  if (!/^-?\d+(?:\.\d+)?$/.test(value))
    throw new Error(
      "Ange ett tal med komma eller punkt, utan enhet eller tusentalsavgränsare.",
    );
  const negative = value.startsWith("-");
  const [integer, fraction = ""] = value.replace(/^-/, "").split(".");
  const whole = integer.replace(/^0+(?=\d)/, "");
  const decimals = fraction.replace(/0+$/, "");
  const coefficient = BigInt(whole + decimals);
  if (
    bounded &&
    (decimals.length > 28 || coefficient > 79228162514264337593543950335n)
  ) {
    throw new Error(
      "Talet kan inte överföras med bibehållen decimalprecision. Ange ett mindre tal eller färre decimaler.",
    );
  }
  return `${negative && coefficient !== 0n ? "-" : ""}${whole}${decimals ? `.${decimals}` : ""}`;
}

export function parseExact<T>(json: string): Exact<T> {
  return parse(json, (_key, value) =>
    isLosslessNumber(value) ? n(value.value) : value,
  ) as Exact<T>;
}

export function stringifyExact(value: unknown): string {
  return (
    stringify(value, (_key, item) =>
      item instanceof Numeric
        ? new LosslessNumber(canonicalNumber(item.text))
        : item,
    ) ?? "null"
  );
}

export function fromOrdinary<T>(value: T): Exact<T> {
  if (typeof value === "number") return n(value) as Exact<T>;
  if (Array.isArray(value)) return value.map(fromOrdinary) as Exact<T>;
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, fromOrdinary(item)]),
    ) as Exact<T>;
  }
  return value as Exact<T>;
}

export function cloneExact<T>(value: T): T {
  if (value instanceof Numeric) return n(value.text) as T;
  if (Array.isArray(value)) return value.map(cloneExact) as T;
  if (value !== null && typeof value === "object")
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, cloneExact(item)]),
    ) as T;
  return value;
}

/** Exact power-of-ten unit conversion; never converts the amount to a JS number. */
export function shiftDecimal(text: string, places: number): string {
  const canonical = canonicalNumber(text, false);
  const negative = canonical.startsWith("-");
  const [whole, fraction = ""] = canonical.replace(/^-/, "").split(".");
  const digits = whole + fraction;
  const point = whole.length + places;
  const shifted =
    point <= 0
      ? `0.${"0".repeat(-point)}${digits}`
      : point >= digits.length
        ? digits + "0".repeat(point - digits.length)
        : `${digits.slice(0, point)}.${digits.slice(point)}`;
  return canonicalNumber(`${negative ? "-" : ""}${shifted}`, false);
}

export function formatNumeric(
  value: Numeric | null | undefined,
  places = 2,
): string {
  if (value == null) return "—";
  try {
    const canonical = canonicalNumber(value.text);
    const negative = canonical.startsWith("-");
    const [whole, fraction = ""] = canonical.replace(/^-/, "").split(".");
    const padded = fraction.padEnd(places + 1, "0");
    let scaled = BigInt(whole + padded.slice(0, places));
    if (padded[places] >= "5") scaled += 1n;
    const digits = scaled.toString().padStart(places + 1, "0");
    const integer = places ? digits.slice(0, -places) : digits;
    return `${negative && scaled !== 0n ? "−" : ""}${BigInt(integer).toLocaleString("sv-SE")}${places ? `,${digits.slice(-places)}` : ""}`;
  } catch {
    return "—";
  }
}

export const formatMoney = (value: Numeric | null | undefined) =>
  value == null ? "—" : `${formatNumeric(value)} kr`;
export const sameNumber = (
  a: Numeric | null | undefined,
  b: Numeric | null | undefined,
) => a?.text === b?.text;
