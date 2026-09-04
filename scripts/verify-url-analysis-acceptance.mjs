import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const docker = process.platform === "win32" ? "docker.exe" : "docker";
const projectName = process.env.COMPOSE_PROJECT_NAME ?? "car-expense-e2e";
const compose = [
  "compose",
  "--project-name",
  projectName,
  "-f",
  "compose.yaml",
  "-f",
  "compose.e2e.yaml",
];

const state = JSON.parse(runCompose([
  "exec",
  "-T",
  "fake-codex-extractor",
  "node",
  "-e",
  "fetch('http://localhost:8080/internal/test-state').then(async response => { if (!response.ok) process.exit(1); process.stdout.write(await response.text()); }).catch(() => process.exit(1))",
]));

assert(state.activeOperations === 0, "The fake extractor must have no active operations after Playwright.");
assert(state.maximumCapacity === 2, "The fake extractor capacity must be exactly two.");
assert(state.maximumConcurrentOperations === 2, "The acceptance run must exercise exactly two concurrent extraction operations.");

for (const outcome of [
  "complete",
  "partial",
  "unavailable",
  "unmatchedSource",
  "rateLimited",
  "timedOut",
  "providerUnavailable",
  "invalidOutput",
  "retryOnce",
]) {
  assert((state.outcomeCounts[outcome] ?? 0) >= 1, `The acceptance run did not exercise ${outcome}.`);
}
assert((state.outcomeCounts.slow ?? 0) >= 10, "The acceptance run must exercise a ten-URL delayed batch.");

const retryEntries = matchingCounts(state.invocationCounts, "retry-once-");
assert(retryEntries.length >= 1, "The acceptance run did not record an explicit retry case.");
assert(retryEntries.some(([, count]) => count === 2), "An explicit retry case must invoke the extractor exactly twice.");

for (const prefix of ["rate-limited-", "timed-out-", "provider-unavailable-", "invalid-output-"]) {
  const entries = matchingCounts(state.invocationCounts, prefix);
  assert(entries.length >= 1, `The acceptance run did not record ${prefix}.`);
  assert(entries.every(([, count]) => count === 1), `${prefix} was retried without an explicit user action.`);
}

const runningServices = runCompose(["ps", "--services", "--status", "running"])
  .split(/\r?\n/)
  .filter(Boolean);
assert(runningServices.includes("fake-codex-extractor"), "The private fake extractor must be running for acceptance.");
assert(!runningServices.includes("codex-extractor"), "The real Codex extractor must not run during fake acceptance.");

const fakeContainerId = runCompose(["ps", "--quiet", "fake-codex-extractor"]).trim();
assert(fakeContainerId.length > 0, "The fake extractor container could not be resolved.");
const [fakeContainer] = JSON.parse(runDocker(["inspect", fakeContainerId]));
assert(fakeContainer.Mounts.length === 0, "The fake extractor must mount no authentication or application data.");

const logs = runCompose(["logs", "--no-color", "api", "web", "fake-codex-extractor"]);
for (const forbidden of [
  "cars.example",
  "fake-access-token",
  "thread.started",
  "item.completed",
  "Motor och växellåda fungerar bra",
]) {
  assert(!logs.includes(forbidden), `Container logs contain forbidden extraction content: ${forbidden}`);
}

console.log("URL-analysis acceptance state, concurrency, isolation, and safe-log checks are valid.");

function matchingCounts(counts, prefix) {
  return Object.entries(counts).filter(([identifier]) => identifier.startsWith(prefix));
}

function runCompose(args) {
  return runDocker([...compose, ...args]);
}

function runDocker(args) {
  return execFileSync(docker, args, {
    cwd: repositoryRoot,
    encoding: "utf8",
    env: process.env,
    stdio: ["ignore", "pipe", "inherit"],
  });
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
