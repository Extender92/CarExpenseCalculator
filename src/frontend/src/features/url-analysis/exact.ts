import { Numeric, parseExact, stringifyExact } from "@/features/household/numbers";
// Ordinary values remain accepted for existing callers; network reads always preserve numeric lexemes.
export type ListingNumber = number | Numeric;
export type Preserved<T> = T extends number ? ListingNumber : T extends (infer U)[] ? Preserved<U>[]
  : T extends object ? {[K in keyof T]: Preserved<T[K]>} : T;
export const listingNumberText = (x: ListingNumber) => x instanceof Numeric ? x.text : String(x);
export async function listingRequest<T>(method: string, url: string, body?: unknown, signal?: AbortSignal) {
  const response = await fetch(new Request(new URL(url, typeof window === "undefined" ? "http://localhost" : window.location.origin), {
    method, signal, headers: body === undefined ? {} : {"Content-Type":"application/json"},
    body: body === undefined ? undefined : stringifyExact(body)}));
  const text = await response.text();
  // The API owns the schema; this boundary additionally retains all decimal and revision tokens.
  const value = text ? parseExact<unknown>(text) as T : undefined;
  return {response, data: response.ok ? value : undefined, error: response.ok ? undefined : value as unknown};
}
