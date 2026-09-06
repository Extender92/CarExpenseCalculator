import { afterEach, expect, it, vi } from "vitest";
import { householdApi, HouseholdApiError } from "./api";
import { n } from "./numbers";
import { deferred, profile } from "./test-fixtures";

afterEach(() => vi.unstubAllGlobals());
it("uses exact JSON numeric tokens in writes and conflict metadata", async () => {
  const fetch = vi
    .fn()
    .mockResolvedValue(
      new Response(
        '{"code":"profileRevisionConflict","actualRevision":9007199254740993,"detail":"private database detail"}',
        { status: 409 },
      ),
    );
  vi.stubGlobal("fetch", fetch);
  const error = await householdApi
    .saveProfile(
      { ...profile(), purchaseCashSek: n("123.1234567890123456789") },
      n("9007199254740992"),
    )
    .catch((error) => error as HouseholdApiError);
  expect(error).toBeInstanceOf(HouseholdApiError);
  expect((error as HouseholdApiError).actualRevision?.text).toBe(
    "9007199254740993",
  );
  expect((error as Error).message).not.toContain("private");
  expect(fetch.mock.calls[0][1].body).toContain(
    '"purchaseCashSek":123.1234567890123456789',
  );
  expect(fetch.mock.calls[0][1].body).toContain(
    '"expectedRevision":9007199254740992',
  );
});

it("rejects an oversized atomic transition before sending any part", async () => {
  const fetch = vi.fn();
  vi.stubGlobal("fetch", fetch);
  const vehicles = Array.from({ length: 30 }, (_, index) => ({
    vehicleId: `10000000-0000-0000-0000-${String(index + 1).padStart(12, "0")}`,
    expectedRevision: n(1),
    cost: {
      input: {
        candidateKey: `ABC${String(index).padStart(3, "0")}`,
        service: {
          isIncluded: false,
          items: Array.from({ length: 50 }, (_, row) => ({
            key: `service-${row}`,
            label: "Service",
            evidenceNote: "å".repeat(1000),
          })),
        },
      },
    },
  }));
  await expect(
    householdApi.confirmTransition({
      profile: profile(),
      expectedProfileRevision: n(0),
      expectedTransitionRevision: n(1),
      vehicles,
    }),
  ).rejects.toMatchObject({ status: 413, code: "payloadTooLarge" });
  expect(fetch).not.toHaveBeenCalled();
});

it("does not report success when cancellation arrives while reading the response body", async () => {
  const body = deferred<string>();
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockResolvedValue({ ok: true, status: 200, text: () => body.promise }),
  );
  const controller = new AbortController();
  const operation = householdApi.profile(controller.signal);
  await Promise.resolve();
  controller.abort();
  body.resolve('{"input":null,"revision":0}');
  await expect(operation).rejects.toMatchObject({ name: "AbortError" });
});
