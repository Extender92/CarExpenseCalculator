import { afterEach, describe, expect, it, vi } from "vitest";
import { bodyBytes, householdApi, maximumRequestBytes } from "./api";
import { previewBatches, calculateGeneration, limitedMap } from "./preview";
import { initialProfile, validateVehicle } from "./form-model";
import { n } from "./numbers";
import { candidate, deferred, previewResponse } from "./test-fixtures";

afterEach(() => vi.restoreAllMocks());
describe("household preview batching", () => {
  it("sends unknown amounts for independent server results without inventing sensitivity values", () => {
    const car = candidate(1);
    car.input.additionalRepairAllowancePerMonthSek = null;
    car.input.tax = {
      isIncluded: false,
      items: [
        {
          key: "tax",
          label: "Skatt",
          cadence: "annual",
          amountSek: null,
        },
      ],
    };
    const result = previewBatches(initialProfile(), [car], "missing-mode");
    expect(result.errors).toEqual({});
    expect(result.batches).toHaveLength(1);
    expect(
      result.batches[0].vehicles[0].input.additionalRepairAllowancePerMonthSek,
    ).toBeNull();
    expect(
      result.batches[0].vehicles[0].input.tax?.items[0].amountSek,
    ).toBeNull();
  });
  it("never starts more than two concurrent preview requests", async () => {
    const pending = Array.from({ length: 3 }, () =>
      deferred<ReturnType<typeof previewResponse>>(),
    );
    let started = 0;
    const api = vi
      .spyOn(householdApi, "preview")
      .mockImplementation(() => pending[started++].promise);
    const operation = calculateGeneration(
      initialProfile(),
      Array.from({ length: 201 }, (_, index) => candidate(index)),
      "concurrency",
      new AbortController().signal,
    );
    expect(started).toBe(2);
    pending[0].resolve(previewResponse(api.mock.calls[0][0]));
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    expect(started).toBe(3);
    pending[1].resolve(previewResponse(api.mock.calls[1][0]));
    pending[2].resolve(previewResponse(api.mock.calls[2][0]));
    expect(Object.keys((await operation).results)).toHaveLength(201);
  });
  it("does not let a duplicate identity overwrite an error with a successful result", () => {
    const car = candidate(1);
    const result = previewBatches(
      initialProfile(),
      [car, { ...car }, candidate(2)],
      "duplicate",
    );
    expect(result.errors.ABC001.error).toMatch(/flera gånger/);
    expect(
      result.batches
        .flatMap((batch) => batch.vehicles)
        .map((vehicle) => vehicle.input.candidateKey),
    ).toEqual(["ABC002"]);
  });
  it("splits 101 registered candidates and a manual candidate with stable ordering", () => {
    const candidates = Array.from({ length: 101 }, (_, index) =>
      candidate(index),
    );
    candidates.push({
      input: { candidateKey: "manual" },
      unresolvedLegacyItems: [],
      registrationNumber: null,
    });
    const result = previewBatches(initialProfile(), candidates, "round");
    expect(result.batches.map((batch) => batch.vehicles.length)).toEqual([
      100, 1, 1,
    ]);
    expect(result.errors).toEqual({});
    expect(result.batches.flatMap((batch) => batch.vehicles)).toEqual(
      candidates,
    );
  });
  it("splits by actual UTF-8 bytes before the count limit", () => {
    const cars = Array.from({ length: 12 }, (_, index) => {
      const car = candidate(index);
      for (const key of ["service", "repairs", "customCosts"] as const)
        car.input[key] = {
          isIncluded: false,
          items: Array.from({ length: 50 }, (_, row) => ({
            key: `${key}-${row}`,
            label: "å".repeat(120),
            evidenceNote: "å".repeat(1000),
          })),
        };
      return car;
    });
    const result = previewBatches(initialProfile(), cars, "utf8");
    expect(result.batches.length).toBeGreaterThan(1);
    expect(result.errors).toEqual({});
    expect(
      result.batches.every(
        (batch) =>
          batch.vehicles.length < 100 &&
          bodyBytes(batch) <= maximumRequestBytes,
      ),
    ).toBe(true);
    expect(result.batches.flatMap((batch) => batch.vehicles).length).toBe(12);
  });
  it("isolates an oversized candidate rather than discarding other cars", () => {
    const huge = candidate(1);
    huge.unresolvedLegacyItems = [
      {
        key: "legacy",
        kind: "maintenance",
        label: "å".repeat(maximumRequestBytes),
      },
    ];
    const result = previewBatches(
      initialProfile(),
      [huge, candidate(2)],
      "large",
    );
    expect(result.errors.ABC001.error).toMatch(/2 MiB/);
    expect(
      result.batches
        .flatMap((batch) => batch.vehicles)
        .map((car) => car.input.candidateKey),
    ).toEqual(["ABC002"]);
  });
  it("keeps parseable numerical range errors for server-side partial calculation", async () => {
    const car = candidate(1);
    car.input.priceSek = n(-1);
    car.input.residual = {
      mode: "fixedAmount",
      value: { single: n(10) },
      periodMonths: n(121),
    };
    const api = vi
      .spyOn(householdApi, "preview")
      .mockImplementation(async (request) => previewResponse(request));
    const result = await calculateGeneration(
      { ...initialProfile(), purchaseCashSek: n(-1) },
      [car],
      "range",
      new AbortController().signal,
    );
    expect(api).toHaveBeenCalledOnce();
    expect(result.results.ABC001.result).toBeDefined();
  });
  it("does not submit invalid text or unfinished sensitivity values", () => {
    const first = candidate(1);
    first.input.priceSek = n("abc");
    const second = candidate(2);
    second.input.residual = {
      mode: "fixedAmount",
      value: { favorable: n(1), baseline: null, cautious: n(3) },
    };
    const result = previewBatches(
      initialProfile(),
      [first, second, candidate(3)],
      "invalid",
    );
    expect(Object.keys(result.errors)).toEqual(["ABC001", "ABC002"]);
    expect(result.batches[0].vehicles).toHaveLength(1);
  });
  it("publishes only after every batch completes and retains other results on failure", async () => {
    const pending = deferred<ReturnType<typeof previewResponse>>();
    let calls = 0;
    const api = vi
      .spyOn(householdApi, "preview")
      .mockImplementation(async () => {
        calls++;
        if (calls === 1) return pending.promise;
        throw new Error("Nätverksfel");
      });
    let published = false;
    const resultPromise = calculateGeneration(
      initialProfile(),
      Array.from({ length: 101 }, (_, index) => candidate(index)),
      "all",
      new AbortController().signal,
    ).then((result) => {
      published = true;
      return result;
    });
    await Promise.resolve();
    expect(published).toBe(false);
    pending.resolve(previewResponse(api.mock.calls[0][0]));
    const result = await resultPromise;
    expect(result.results.ABC000.result).toBeDefined();
    expect(result.results.ABC100.error).toBe("Nätverksfel");
  });
  it("maps server field paths back to the correct car and leasing row", async () => {
    vi.spyOn(householdApi, "preview").mockImplementation(async (request) => {
      const response = previewResponse(request);
      response.vehicles[1].sections.inputErrors = [
        {
          path: "vehicles[1].input.lease.endFees.items[0].amountSek.single",
          code: "outOfRange",
          message: "English",
        },
      ];
      return response;
    });
    const result = await calculateGeneration(
      initialProfile(),
      [candidate(1), candidate(2)],
      "paths",
      new AbortController().signal,
    );
    expect(result.results.ABC001.fields).toEqual({});
    expect(
      result.results.ABC002.fields?.[
        "input.lease.endFees[0].amountSek.single"
      ]?.[0],
    ).toMatch(/intervallet/);
  });
  it("limits concurrency while preserving result order", async () => {
    const requests = Array.from({ length: 8 }, () => deferred<number>());
    let count = 0;
    const result = limitedMap(requests, 4, (request) => {
      count++;
      return request.promise;
    });
    expect(count).toBe(4);
    requests[3].resolve(3);
    await Promise.resolve();
    await Promise.resolve();
    expect(count).toBe(5);
    requests.forEach((request, index) => request.resolve(index));
    expect(await result).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
  });
  it("rejects duplicate cost keys and payment months locally", () => {
    const car = candidate(1);
    car.input.service = {
      isIncluded: false,
      items: [{ key: "same", label: "Service" }],
    };
    car.input.repairs = {
      isIncluded: true,
      items: [{ key: "same", label: "Reparation" }],
    };
    expect(
      validateVehicle(car.input)["input.repairs.items[0].label"],
    ).toBeDefined();
    car.input = {
      candidateKey: "lease",
      acquisitionType: "lease",
      lease: {
        monthlyPayments: [{ monthOffset: n(1) }, { monthOffset: n(1) }],
      },
    };
    expect(
      validateVehicle(car.input)["input.lease.monthlyPayments[1].monthOffset"],
    ).toBeDefined();
  });
});
