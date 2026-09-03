import { describe, expect, it, vi } from "vitest";
import { FifoRequestScheduler } from "./request-scheduler";

describe("FifoRequestScheduler", () => {
  it("runs FIFO with at most two requests and continues after failure", async () => {
    const scheduler = new FifoRequestScheduler(2);
    const started: number[] = [];
    const releases: Array<() => void> = [];
    let active = 0;
    let maximumActive = 0;

    const tasks = Array.from({ length: 4 }, (_, index) => scheduler.schedule(async () => {
      started.push(index);
      active += 1;
      maximumActive = Math.max(maximumActive, active);
      await new Promise<void>((resolve) => releases[index] = resolve);
      active -= 1;
      if (index === 1) throw new Error("expected failure");
      return index;
    }));
    const resultsPromise = Promise.allSettled(tasks);

    await vi.waitFor(() => expect(started).toEqual([0, 1]));
    releases[0]();
    await vi.waitFor(() => expect(started).toEqual([0, 1, 2]));
    releases[1]();
    await vi.waitFor(() => expect(started).toEqual([0, 1, 2, 3]));
    releases[2]();
    releases[3]();

    const results = await resultsPromise;
    expect(maximumActive).toBe(2);
    expect(results.map((result) => result.status)).toEqual(["fulfilled", "rejected", "fulfilled", "fulfilled"]);
  });

  it("cancels a queued request without running it", async () => {
    const scheduler = new FifoRequestScheduler(1);
    let release!: () => void;
    const first = scheduler.schedule(() => new Promise<void>((resolve) => release = resolve));
    const controller = new AbortController();
    let ran = false;
    const queued = scheduler.schedule(async () => { ran = true; }, controller.signal);
    controller.abort();

    await expect(queued).rejects.toMatchObject({ name: "AbortError" });
    release();
    await first;
    expect(ran).toBe(false);
  });

  it("releases capacity immediately when running work is cancelled", async () => {
    const scheduler = new FifoRequestScheduler(1);
    let release!: () => void;
    const controller = new AbortController();
    const started: string[] = [];
    const first = scheduler.schedule(() => {
      started.push("first");
      return new Promise<void>((resolve) => release = resolve);
    }, controller.signal);
    const second = scheduler.schedule(async () => {
      started.push("second");
      return "second";
    });

    controller.abort();

    await expect(first).rejects.toMatchObject({ name: "AbortError" });
    await expect(second).resolves.toBe("second");
    expect(started).toEqual(["first", "second"]);
    release();
  });
});
