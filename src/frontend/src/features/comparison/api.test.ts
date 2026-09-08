import { afterEach, describe, expect, it, vi } from "vitest";
import { n, stringifyExact } from "@/features/household/numbers";
import { comparisonApi, ComparisonApiError, jsonBytes } from "./api";
import { manualRequest, response, vehicleId } from "./test-fixtures";

afterEach(() => vi.unstubAllGlobals());
describe("comparison transport", () => {
  it("preserves decimal and revision digits through both directions", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(
        new Response('{"revision":9007199254740993,"input":null}'),
      );
    vi.stubGlobal("fetch", fetch);
    const revision = n("9007199254740993");
    const result = await comparisonApi.saveFacts(
      vehicleId(),
      {
        edits: {
          purchasePriceSek: {
            kind: "manual",
            manual: { value: n("0.1234567890123456789012345678") },
          },
        },
      },
      revision,
    );
    expect(fetch.mock.calls[0][1].body).toContain(
      '"value":0.1234567890123456789012345678',
    );
    expect(fetch.mock.calls[0][1].body).toContain(
      '"expectedRevision":9007199254740993',
    );
    expect(result.revision.text).toBe(revision.text);
  });
  it("measures UTF-8 rather than characters and keeps the old write limit", async () => {
    expect(jsonBytes("åäö")).toBe(6);
    const fetch = vi.fn();
    vi.stubGlobal("fetch", fetch);
    await expect(
      comparisonApi.saveRules(
        {
          hardRules: [
            {
              criterionKey: "locality",
              operator: "allowedSet",
              minimumEvidence: "advertised",
              allowedValues: [{ text: "å".repeat(1024 * 1024) }],
            },
          ],
        },
        n(0),
      ),
    ).rejects.toMatchObject({ status: 413 });
    expect(fetch).not.toHaveBeenCalled();
  });
  it("does not impose 2 or 32 MiB on the configurable complete route", async () => {
    const request = manualRequest();
    // Transport is responsible for byte limits; the server owns domain bounds.
    request.requestId = "x".repeat(32 * 1024 * 1024 + 1);
    const fetch = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetch);
    await comparisonApi.preview(request);
    expect(jsonBytes(fetch.mock.calls[0][1].body)).toBeGreaterThan(
      32 * 1024 * 1024,
    );
  });
  it.each([1024, 64 * 1024 * 1024])(
    "reports the authoritative server limit %i without retry",
    async (maximumRequestBytes) => {
      const fetch = vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ code: "payloadTooLarge", maximumRequestBytes }),
            { status: 413 },
          ),
        );
      vi.stubGlobal("fetch", fetch);
      await expect(
        comparisonApi.preview(manualRequest()),
      ).rejects.toMatchObject({
        status: 413,
        problem: { maximumRequestBytes: n(maximumRequestBytes) },
      });
      expect(fetch).toHaveBeenCalledOnce();
    },
  );
  it.each([
    "comparisonBusy",
    "comparisonTimedOut",
    "comparisonBaselineConflict",
  ])("does not retry %s", async (code) => {
    const fetch = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ code }), { status: 503 }),
      );
    vi.stubGlobal("fetch", fetch);
    await expect(comparisonApi.preview(manualRequest())).rejects.toBeInstanceOf(
      ComparisonApiError,
    );
    expect(fetch).toHaveBeenCalledOnce();
  });
  it("rejects incomplete JSON rather than publishing a surviving view", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(stringifyExact(response(manualRequest())).slice(0, -10)),
        ),
    );
    await expect(comparisonApi.preview(manualRequest())).rejects.toMatchObject({
      code: "invalidResponse",
    });
  });
});
