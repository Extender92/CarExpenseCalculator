import { describe, expect, it } from "vitest";
import { n } from "@/features/household/numbers";
import {
  mappedErrors,
  numberErrors,
  ruleErrors,
  validResponse,
} from "./preview";
import { manualRequest, response, vehicleId } from "./test-fixtures";
import { keyedPath, indexedPath } from "./navigation";

describe("complete comparison response publication", () => {
  it.each([0, 1, 50, 51, 100, 101, 250])(
    "validates %i candidates without imposing a selection cap",
    (count) => {
      const request = manualRequest(count);
      expect(() =>
        validResponse(request, response(request), count),
      ).not.toThrow();
    },
  );
  it.each([
    "view",
    "count",
    "identity",
    "order",
    "request",
    "mode",
    "revision",
    "generation",
    "membership",
  ])("rejects inconsistent %s", (defect) => {
    const request = manualRequest(2);
    const reply = response(request);
    if (defect === "view")
      delete (reply.views as Partial<typeof reply.views>).cautious;
    if (defect === "count") reply.candidateCount = n(1);
    if (defect === "identity")
      reply.views.favorable.candidates[1].vehicleId = vehicleId(0);
    if (defect === "order")
      reply.views.cautious.scoreOrder = [vehicleId(0), vehicleId(0)];
    if (defect === "request") reply.requestId = "another-request";
    if (defect === "mode")
      reply.views.favorable.profile.activeSensitivityMode = "baseline";
    if (defect === "revision")
      reply.views.cautious.candidates[0].sourceRevisions.vehicle = n(999);
    if (defect === "generation") reply.generationId = "";
    if (defect === "membership") reply.views.baseline.candidates.reverse();
    expect(() => validResponse(request, reply, 2)).toThrow();
  });
  it("preserves authoritative ordering where visible amounts are equal", () => {
    const request = manualRequest(2);
    const reply = response(request);
    for (const view of Object.values(reply.views)) {
      view.costOrder.reverse();
      view.scoreOrder.reverse();
    }
    validResponse(request, reply, 2);
    expect(reply.views.baseline.costOrder).toEqual([
      vehicleId(1),
      vehicleId(0),
    ]);
  });
});
describe("local serializability and field identity", () => {
  it("keeps a field link attached to the cost item after insertion and reordering", () => {
    const old = {
      input: {
        customCosts: { items: [{ key: "service å/1" }, { key: "other" }] },
      },
    };
    const path = keyedPath("input.customCosts.items[0].amountSek.single", old);
    expect(path).toContain("key:service%20%C3%A5%2F1");
    const changed = {
      input: {
        customCosts: {
          items: [{ key: "other" }, { key: "new" }, { key: "service å/1" }],
        },
      },
    };
    expect(indexedPath(path, changed)).toBe(
      "input.customCosts.items[2].amountSek.single",
    );
    expect(indexedPath(path, { input: {} })).toContain("[-1]");
  });
  it("accepts interpretable domain errors without coercing or rounding them", () => {
    expect(
      numberErrors({
        ownerCount: n(-100),
        price: n("0.1234567890123456789012345678"),
      }),
    ).toEqual({});
    expect(numberErrors({ price: n("oops") })).toHaveProperty("price");
  });
  it("validates supplied disabled weights and requires explicit evidence", () => {
    expect(
      ruleErrors({
        preferences: [
          {
            criterionKey: "seats",
            weight: n("0,0"),
            minimumEvidence: "userConfirmed",
          },
        ],
      }),
    ).toEqual({});
    expect(
      ruleErrors({
        preferences: [
          {
            criterionKey: "seats",
            weight: n(0),
            minimumEvidence: "userConfirmed",
          },
        ],
      }),
    ).toEqual({});
    expect(
      ruleErrors({
        preferences: [
          {
            criterionKey: "seats",
            weight: n(6),
            minimumEvidence: "userConfirmed",
          },
        ],
      }),
    ).toHaveProperty("rules.preferences[0].weight");
  });
  it("maps compact override indexes to the captured car rather than its display position", () => {
    const request = {
      ...manualRequest(),
      candidates: undefined,
      overrides: [manualRequest(2).candidates![1]],
    };
    expect(
      mappedErrors(
        [
          {
            path: "overrides[0].facts.edits.seats.manual.value",
            code: "outOfRange",
            message: "internal",
          },
        ],
        request,
      ),
    ).toEqual({
      [`vehicle.${vehicleId(1)}.facts.edits.seats.manual.value`]: [
        "Värdet ligger utanför tillåtet intervall.",
      ],
    });
  });
});
