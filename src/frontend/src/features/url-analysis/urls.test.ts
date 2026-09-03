import { describe, expect, it } from "vitest";
import {
  maximumListingUrlLength,
  normalizeListingUrl,
  validateListingUrlList,
} from "./urls";

describe("listing URL validation", () => {
  it("normalizes casing, IDNA, default ports, fragments, and empty paths", () => {
    expect(normalizeListingUrl("  HTTPS://RÄKSMÖRGÅS.SE:443#details  ")).toMatchObject({
      submitted: "HTTPS://RÄKSMÖRGÅS.SE:443#details",
      normalized: "https://xn--rksmrgs-5wao1o.se/",
      scheme: "https",
      host: "xn--rksmrgs-5wao1o.se",
      port: "",
      path: "/",
    });
  });

  it("preserves escaped path and query contents while removing fragments", () => {
    expect(normalizeListingUrl("https://Example.com/A%20B?ci=Two&x=1#ignored")).toMatchObject({
      normalized: "https://example.com/A%20B?ci=Two&x=1",
      path: "/A%20B",
    });
  });

  it.each([
    "file:///tmp/listing",
    "https://user:password@example.com/item/1",
    "http://localhost/item/1",
    "http://service.local/item/1",
    "http://10.0.0.1/item/1",
    "http://100.64.0.1/item/1",
    "http://127.0.0.1/item/1",
    "http://169.254.1.1/item/1",
    "http://192.0.2.1/item/1",
    "http://198.18.0.1/item/1",
    "http://203.0.113.1/item/1",
    "http://224.0.0.1/item/1",
    "http://[::1]/item/1",
    "http://[::ffff:7f00:1]/item/1",
    "http://[2001:db8::1]/item/1",
    "http://[fc00::1]/item/1",
    "http://[fe80::1]/item/1",
    "http://[ff00::1]/item/1",
  ])("rejects unsupported or non-public URL %s", (url) => {
    expect(typeof normalizeListingUrl(url)).toBe("string");
  });

  it("accepts unresolved public-looking hosts and non-default ports", () => {
    expect(normalizeListingUrl("https://listings.invalid:8443/item/1")).toMatchObject({
      normalized: "https://listings.invalid:8443/item/1",
      port: "8443",
    });
  });

  it("detects query, trailing-slash, and default-scheme page duplicates", () => {
    const result = validateListingUrlList([
      "http://cars.example/item/1?ci=2",
      "https://cars.example/item/1/",
    ].join("\n"));

    expect(result.urls).toHaveLength(1);
    expect(result.errors["urls[1]"]).toContain("samma annonssida");
  });

  it("keeps non-default schemes and ports distinct", () => {
    const result = validateListingUrlList([
      "http://cars.example:8080/item/1",
      "https://cars.example:8080/item/1",
    ].join("\n"));

    expect(result.errors).toEqual({});
    expect(result.urls).toHaveLength(2);
  });

  it("enforces one through ten URLs and both length boundaries", () => {
    expect(validateListingUrlList("").errors.urls).toBeDefined();
    const eleven = Array.from({ length: 11 }, (_, index) => `https://cars.example/item/${index}`).join("\n");
    expect(validateListingUrlList(eleven).errors.urls).toContain("högst 10");

    const exact = `https://cars.example/${"a".repeat(maximumListingUrlLength - 21)}`;
    expect(exact).toHaveLength(maximumListingUrlLength);
    expect(typeof normalizeListingUrl(exact)).toBe("object");
    expect(typeof normalizeListingUrl(`${exact}a`)).toBe("string");
  });
});
