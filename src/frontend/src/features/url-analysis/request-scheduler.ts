interface QueueEntry<T> {
  task: () => Promise<T>;
  signal?: AbortSignal;
  resolve: (value: T) => void;
  reject: (reason?: unknown) => void;
  removeAbortListener?: () => void;
}

export class FifoRequestScheduler {
  private readonly queue: QueueEntry<unknown>[] = [];
  private running = 0;

  constructor(private readonly maximumConcurrency = 2) {
    if (!Number.isInteger(maximumConcurrency) || maximumConcurrency < 1) {
      throw new RangeError("Maximum concurrency must be a positive integer.");
    }
  }

  schedule<T>(task: () => Promise<T>, signal?: AbortSignal): Promise<T> {
    if (signal?.aborted) return Promise.reject(abortError());

    return new Promise<T>((resolve, reject) => {
      const entry: QueueEntry<T> = { task, signal, resolve, reject };
      if (signal) {
        const onAbort = () => {
          const index = this.queue.indexOf(entry as QueueEntry<unknown>);
          if (index >= 0) {
            this.queue.splice(index, 1);
            reject(abortError());
          }
        };
        signal.addEventListener("abort", onAbort, { once: true });
        entry.removeAbortListener = () => signal.removeEventListener("abort", onAbort);
      }

      this.queue.push(entry as QueueEntry<unknown>);
      this.pump();
    });
  }

  private pump() {
    while (this.running < this.maximumConcurrency && this.queue.length > 0) {
      const entry = this.queue.shift()!;
      entry.removeAbortListener?.();
      if (entry.signal?.aborted) {
        entry.reject(abortError());
        continue;
      }

      this.running += 1;
      void runEntry(entry)
        .then(entry.resolve, entry.reject)
        .finally(() => {
          this.running -= 1;
          this.pump();
        });
    }
  }
}

function runEntry(entry: QueueEntry<unknown>) {
  const task = entry.task();
  if (!entry.signal) return task;
  if (entry.signal.aborted) return Promise.reject(abortError());

  return new Promise<unknown>((resolve, reject) => {
    const onAbort = () => reject(abortError());
    entry.signal!.addEventListener("abort", onAbort, { once: true });
    void task.then(resolve, reject).finally(() => entry.signal!.removeEventListener("abort", onAbort));
  });
}

function abortError() {
  return new DOMException("The operation was aborted.", "AbortError");
}
