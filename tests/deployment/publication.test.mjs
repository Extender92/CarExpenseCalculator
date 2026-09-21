import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { components } from "../../scripts/deployment-package.mjs";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const parent = path.join(process.env.CEC_TEST_TEMP ?? path.join(root, "temp/prebuilt-deployment"), "publication-tests");
fs.mkdirSync(parent, { recursive: true });
function publish(fault = "", newest = "build-99-5", patch = {}) {
  const dir = fs.mkdtempSync(path.join(parent, "case-")); fs.mkdirSync(path.join(dir, "bin"));
  const commit = "a".repeat(40), images = Object.fromEntries(components.map((name, index) => [name, `sha256:${String(index + 1).padStart(64, "0")}`]));
  const input = path.join(dir, "input"); fs.mkdirSync(input);
  fs.writeFileSync(path.join(input, "tested-images.json"), JSON.stringify({ commit, images }));
  const stateFile = path.join(dir, "state.json"); fs.writeFileSync(stateFile, JSON.stringify({ fault, newest, commit, images, events: [] }));
  fs.chmodSync(path.join(root, "tests/deployment/fake-publication-command.mjs"), 0o755);
  for (const tool of ["docker", "gh"]) fs.symlinkSync(path.join(root, "tests/deployment/fake-publication-command.mjs"), path.join(dir, "bin", tool));
  const result = spawnSync(process.execPath, ["scripts/publish-deployment.mjs", input, path.join(dir, "output")], { cwd: root, encoding: "utf8", timeout: 30000,
    env: { ...process.env, PATH: `${path.join(dir, "bin")}:${process.env.PATH}`, CEC_PUBLICATION_STATE: stateFile,
      GITHUB_REPOSITORY: "Extender92/CarExpenseCalculator", GITHUB_REF: "refs/heads/main", GITHUB_EVENT_NAME: "push", GITHUB_SHA: commit, GITHUB_RUN_ID: "100", GITHUB_RUN_ATTEMPT: "1", ...patch } });
  return { result, state: JSON.parse(fs.readFileSync(stateFile)) };
}
test("publication cannot run until every verification job succeeds and never runs on PRs", () => {
  const workflow = fs.readFileSync(path.join(root, ".github/workflows/ci.yml"), "utf8");
  const publishJob = workflow.split("\n  publish:\n")[1]; assert(publishJob);
  assert.match(publishJob, /needs: \[backend, frontend, api-contract, deployment, containers\]/);
  assert.match(publishJob, /if: github.event_name == 'push' && github.ref == 'refs\/heads\/main'/);
  assert.match(publishJob, /group: publish-car-expense-calculator\s+cancel-in-progress: false/);
  assert.match(publishJob, /queue: max/);
  assert.doesNotMatch(publishJob, /if:.*(?:always|failure|cancelled)\(\).*\n\s+runs-on/);
  assert.doesNotMatch(publishJob, /docker (?:build|compose.*build)/);
  const { result, state } = publish("", null, { GITHUB_EVENT_NAME: "pull_request" });
  assert.notEqual(result.status, 0); assert.equal(state.events.length, 0);
});
test("only the loaded tested images are pushed, and all anonymous pulls and assets precede promotion", () => {
  const { result, state } = publish(); assert.equal(result.status, 0, result.stderr);
  assert.equal(state.events.filter(x => x.name === "load").length, 1);
  assert.equal(state.events.filter(x => x.name === "push").length, 3);
  assert.equal(state.events.filter(x => x.name === "anonymous-pull").length, 3);
  const draft = state.events.findIndex(x => x.name === "draft"), upload = state.events.findIndex(x => x.name === "upload"), promoted = state.events.findIndex(x => x.name === "promote");
  assert(draft > state.events.findLastIndex(x => x.name === "anonymous-pull")); assert(upload > draft); assert(promoted > upload);
  assert.equal(state.events[promoted].value.make_latest, "true");
});
for (const fault of ["push", "private", "upload"]) test(`${fault} failure cannot expose a partial release as latest`, () => {
  const { result, state } = publish(fault); assert.notEqual(result.status, 0);
  assert(!state.events.some(x => x.name === "promote"));
});
test("an older serialized run completing later cannot replace the newest release", () => {
  const { result, state } = publish("", "build-101-1"); assert.equal(result.status, 0, result.stderr);
  assert.equal(state.events.find(x => x.name === "promote").value.make_latest, "false");
});
test("an artifact for another commit is rejected before any image or release write", () => {
  const { result, state } = publish("", null, { GITHUB_SHA: "b".repeat(40) }); assert.notEqual(result.status, 0);
  assert.equal(state.events.length, 0);
});
