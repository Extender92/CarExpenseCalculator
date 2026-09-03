export const maximumListingUrlCount = 10;
export const maximumListingUrlLength = 2_048;

const rejectedIpv4Ranges: ReadonlyArray<readonly [string, number]> = [
  ["0.0.0.0", 8],
  ["10.0.0.0", 8],
  ["100.64.0.0", 10],
  ["127.0.0.0", 8],
  ["169.254.0.0", 16],
  ["172.16.0.0", 12],
  ["192.0.0.0", 24],
  ["192.0.2.0", 24],
  ["192.88.99.0", 24],
  ["192.168.0.0", 16],
  ["198.18.0.0", 15],
  ["198.51.100.0", 24],
  ["203.0.113.0", 24],
  ["224.0.0.0", 4],
  ["240.0.0.0", 4],
];

const rejectedIpv6Ranges: ReadonlyArray<readonly [string, number]> = [
  ["::", 128],
  ["::1", 128],
  ["::ffff:0:0", 96],
  ["64:ff9b::", 96],
  ["64:ff9b:1::", 48],
  ["100::", 64],
  ["2001::", 23],
  ["2001:db8::", 32],
  ["2002::", 16],
  ["2620:4f:8000::", 48],
  ["3fff::", 20],
  ["5f00::", 16],
  ["fc00::", 7],
  ["fec0::", 10],
  ["fe80::", 10],
  ["ff00::", 8],
];

export interface NormalizedListingUrl {
  submitted: string;
  normalized: string;
  scheme: "http" | "https";
  host: string;
  port: string;
  path: string;
  pageIdentity: string;
}

export interface UrlListValidation {
  urls: NormalizedListingUrl[];
  errors: Record<string, string>;
}

export function validateListingUrlList(value: string): UrlListValidation {
  const lines = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const errors: Record<string, string> = {};

  if (lines.length === 0) {
    errors.urls = "Ange minst en annons-URL.";
  } else if (lines.length > maximumListingUrlCount) {
    errors.urls = `Ange högst ${maximumListingUrlCount} URL:er.`;
  }

  const urls: NormalizedListingUrl[] = [];
  const identities = new Map<string, number>();
  lines.forEach((line, index) => {
    const result = normalizeListingUrl(line);
    if (typeof result === "string") {
      errors[`urls[${index}]`] = `Rad ${index + 1}: ${result}`;
      return;
    }

    const originalIndex = identities.get(result.pageIdentity);
    if (originalIndex !== undefined) {
      errors[`urls[${index}]`] = `Rad ${index + 1}: URL:en avser samma annonssida som rad ${originalIndex + 1}.`;
      return;
    }

    identities.set(result.pageIdentity, index);
    urls.push(result);
  });

  return { urls, errors };
}

export function normalizeListingUrl(value: string): NormalizedListingUrl | string {
  const submitted = value.trim();
  if (!submitted) return "URL:en får inte vara tom.";
  if (submitted.length > maximumListingUrlLength) {
    return `URL:en får vara högst ${maximumListingUrlLength} tecken.`;
  }

  let parsed: URL;
  try {
    parsed = new URL(submitted);
  } catch {
    return "Ange en fullständig och giltig URL.";
  }

  const scheme = parsed.protocol.slice(0, -1).toLowerCase();
  if (scheme !== "http" && scheme !== "https") {
    return "URL:en måste använda http eller https.";
  }
  if (parsed.username || parsed.password) return "URL:er med användaruppgifter tillåts inte.";
  if (!parsed.hostname) return "URL:en måste innehålla ett värdnamn.";

  const host = parsed.hostname.toLowerCase();
  const classificationHost = host.replace(/^\[|\]$/g, "").replace(/\.+$/, "");
  if (!classificationHost) return "URL:en måste innehålla ett värdnamn.";
  if (
    classificationHost === "localhost"
    || classificationHost.endsWith(".localhost")
    || classificationHost === "local"
    || classificationHost.endsWith(".local")
  ) {
    return "Lokala adresser tillåts inte.";
  }

  const ipv4 = parseIpv4(classificationHost);
  if (ipv4 !== null && rejectedIpv4Ranges.some(([network, bits]) => ipv4InRange(ipv4, parseIpv4(network)!, bits))) {
    return "Privata, reserverade och andra icke-publika IP-adresser tillåts inte.";
  }
  const ipv6 = parseIpv6(classificationHost);
  if (ipv6 && rejectedIpv6Ranges.some(([network, bits]) => bytesInRange(ipv6, parseIpv6(network)!, bits))) {
    return "Privata, reserverade och andra icke-publika IP-adresser tillåts inte.";
  }

  parsed.hash = "";
  const path = parsed.pathname || "/";
  const authority = parsed.host.toLowerCase();
  const normalized = `${scheme}://${authority}${path}${parsed.search}`;
  if (normalized.length > maximumListingUrlLength) {
    return `Den normaliserade URL:en får vara högst ${maximumListingUrlLength} tecken.`;
  }

  const port = parsed.port;
  const comparablePath = path.length > 1 && path.endsWith("/") ? path.slice(0, -1) : path;
  const pageIdentity = port
    ? `${scheme}|${host}|${port}|${comparablePath}`
    : `default|${host}|${comparablePath}`;

  return {
    submitted,
    normalized,
    scheme,
    host,
    port,
    path,
    pageIdentity,
  };
}

function parseIpv4(value: string): number | null {
  const parts = value.split(".");
  if (parts.length !== 4 || parts.some((part) => !/^\d{1,3}$/.test(part))) return null;
  const bytes = parts.map(Number);
  if (bytes.some((part) => part > 255)) return null;
  return (((bytes[0] * 256 + bytes[1]) * 256 + bytes[2]) * 256 + bytes[3]) >>> 0;
}

function ipv4InRange(value: number, network: number, prefixLength: number) {
  const mask = prefixLength === 0 ? 0 : (0xffffffff << (32 - prefixLength)) >>> 0;
  return (value & mask) === (network & mask);
}

function parseIpv6(value: string): Uint8Array | null {
  if (!value.includes(":")) return null;
  const halves = value.split("::");
  if (halves.length > 2) return null;

  const left = parseIpv6Words(halves[0]);
  const right = halves.length === 2 ? parseIpv6Words(halves[1]) : [];
  if (!left || !right) return null;
  const missing = 8 - left.length - right.length;
  if ((halves.length === 1 && missing !== 0) || (halves.length === 2 && missing < 1)) return null;
  const words = [...left, ...Array(Math.max(0, missing)).fill(0), ...right];
  if (words.length !== 8) return null;

  const bytes = new Uint8Array(16);
  words.forEach((word, index) => {
    bytes[index * 2] = word >> 8;
    bytes[index * 2 + 1] = word & 0xff;
  });
  return bytes;
}

function parseIpv6Words(value: string): number[] | null {
  if (!value) return [];
  const parts = value.split(":");
  const words: number[] = [];
  for (const part of parts) {
    if (!/^[0-9a-f]{1,4}$/i.test(part)) return null;
    words.push(Number.parseInt(part, 16));
  }
  return words;
}

function bytesInRange(value: Uint8Array, network: Uint8Array, prefixLength: number) {
  const fullBytes = Math.floor(prefixLength / 8);
  for (let index = 0; index < fullBytes; index += 1) {
    if (value[index] !== network[index]) return false;
  }
  const remainingBits = prefixLength % 8;
  if (remainingBits === 0) return true;
  const mask = (0xff << (8 - remainingBits)) & 0xff;
  return (value[fullBytes] & mask) === (network[fullBytes] & mask);
}
