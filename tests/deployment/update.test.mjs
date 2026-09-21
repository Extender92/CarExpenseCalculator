import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { components, createBundle, sourceRepository, versionFor, isNewer, assertPublishContext, sha256 } from "../../scripts/deployment-package.mjs";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const parent = process.env.CEC_TEST_TEMP ?? path.join(root, "temp/prebuilt-deployment/unit");
fs.mkdirSync(parent, { recursive: true });
const commit = "a".repeat(40), previous = "b".repeat(40);
const id = value => `sha256:${String(value).padStart(64, "0")}`;
function setup(fault = "") {
  const dir = fs.mkdtempSync(path.join(parent, "case-")), install = path.join(dir, "install");
  fs.mkdirSync(install); fs.mkdirSync(path.join(dir, "bin"));
  const references = {}, current = {}, currentReferences = {};
  for (const [index, component] of components.entries()) {
    references[component] = `ghcr.io/extender92/car-expense-calculator-${component}@${id(index + 10)}`;
    current[component] = id(index + 1); currentReferences[component] = `car-expense-calculator-${component}:local`;
  }
  const bundle = createBundle(root, dir, "build-100-1", commit, references);
  for (const name of ["update.sh", "deploy.sh", "deploy-lib.sh", "compose.unraid.yaml"]) fs.copyFileSync(path.join(dir, "payload", name), path.join(install, name));
  fs.writeFileSync(path.join(install, ".env"), "WEB_PORT=6425\nPOSTGRES_PASSWORD=should-not-print-password\nCODEX_HOME_PATH=/existing/codex\n", { mode: 0o600 });
  for (const tool of ["docker", "curl"]) fs.symlinkSync(path.join(root, "tests/deployment/fake-command.mjs"), path.join(dir, "bin", tool));
  fs.chmodSync(path.join(root, "tests/deployment/fake-command.mjs"), 0o755);
  const makeImage = (component, number, revision, tags, digests) => ({ Id: id(number), Os: "linux", Architecture: "amd64", RepoTags: tags, RepoDigests: digests,
    Config: { Labels: revision ? { "org.opencontainers.image.source": sourceRepository, "org.opencontainers.image.revision": revision, "se.car-expense-calculator.component": component } : {} } });
  const list = components.flatMap((component, index) => [makeImage(component, index + 1, null, [currentReferences[component]], []), makeImage(component, index + 10, commit, [], [references[component]])]);
  list.push(makeImage("unrelated", 99, null, ["other-app:latest"], []));
  const state = { fixture: dir, fault, events: [], running: true, images: list, current, currentReferences };
  if (fault === "identity") list.find(image => image.Id === id(10)).Config.Labels["org.opencontainers.image.revision"] = previous;
  if (fault === "protected") state.protectedId = id(1);
  if (fault === "foreign-tag") list[0].RepoTags.push("another-application:latest");
  if (fault === "unidentified") list.push(makeImage("api", 98, null, ["car-expense-calculator-api:unknown-old-build"], []));
  fs.writeFileSync(path.join(dir, "state.json"), JSON.stringify(state));
  fs.writeFileSync(path.join(dir, "release.json"), JSON.stringify({ tag_name: bundle.manifest.version, draft: false, prerelease: false,
    assets: [{ name: "unraid-bundle.tar.gz", browser_download_url: "https://test.invalid/bundle" }, { name: "unraid-bundle.tar.gz.sha256", browser_download_url: "https://test.invalid/checksum" }] }));
  const env = { ...process.env, PATH: `${path.join(dir, "bin")}:${process.env.PATH}`, CEC_FAKE_STATE: path.join(dir, "state.json"), CEC_RELEASE_API: "https://test.invalid/releases/latest" };
  return { dir, install, state: () => JSON.parse(fs.readFileSync(env.CEC_FAKE_STATE)),
    run: () => spawnSync("bash", [path.join(install, "update.sh")], { env, encoding: "utf8", timeout: 15000 }), env };
}
test("publication is restricted to main push and versions have an exact monotonic ordering", () => {
  const env = { GITHUB_REPOSITORY: "Extender92/CarExpenseCalculator", GITHUB_REF: "refs/heads/main", GITHUB_EVENT_NAME: "push", GITHUB_SHA: commit, GITHUB_RUN_ID: "123", GITHUB_RUN_ATTEMPT: "2" };
  assert.equal(assertPublishContext(env), "build-123-2");
  for (const patch of [{ GITHUB_REF: "refs/heads/feature" }, { GITHUB_EVENT_NAME: "pull_request" }, { GITHUB_REPOSITORY: "someone/fork" }]) assert.throws(() => assertPublishContext({ ...env, ...patch }));
  assert(isNewer("build-100-1", "build-99-100")); assert(!isNewer("build-100-1", "build-100-2"));
  assert(!isNewer("build-100-1", "build-100-1")); assert(isNewer("build-100-2", "build-100-1"));
  assert.throws(() => versionFor("100;echo", 1));
});
test("an incomplete image set never becomes an installable bundle", () => {
  assert.throws(() => createBundle(root, parent, "build-100-1", commit, { api: "latest" }), /Incomplete image set/);
});
test("update pulls every image before stop, migrates once, checks health and removes only obsolete app images", () => {
  const fixture = setup(); const before = fs.readFileSync(path.join(fixture.install, ".env")); const result = fixture.run();
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const state = fixture.state(); assert.equal(state.events.filter(x => x === "migrate").length, 1);
  assert(state.events.findIndex(x => x === "stop") > state.events.findLastIndex(x => x.startsWith("pull:")));
  assert(state.events.findIndex(x => x.startsWith("remove:")) > state.events.findLastIndex(x => x === "health"));
  assert.deepEqual(state.images.map(x => x.Id).sort(), [id(10), id(11), id(12), id(99)].sort());
  assert.deepEqual(fs.readFileSync(path.join(fixture.install, ".env")), before);
  assert.equal(JSON.parse(fs.readFileSync(path.join(fixture.install, ".deploy-state/current.json"))).commit, commit);
  const eventsBefore = state.events.length; const again = fixture.run(); assert.equal(again.status, 0, again.stdout + again.stderr);
  assert(!fixture.state().events.slice(eventsBefore).some(x => ["stop", "start", "migrate"].includes(x)));
});
for (const fault of ["daemon", "network", "postgres", "postgres-ready", "download", "config", "mount", "pull", "identity"]) {
  test(`${fault} failure leaves the running app intact and exposes no credentials`, () => {
    const fixture = setup(fault), result = fixture.run(); assert.notEqual(result.status, 0);
    assert.equal(fixture.state().running, true); assert(!fixture.state().events.includes("stop"));
    assert(!fixture.state().events.some(x => x.startsWith("remove:"))); assert(!`${result.stdout}${result.stderr}`.includes("should-not-print-password"));
  });
}
for (const fault of ["migration", "start", "health", "wrong-running"]) {
  test(`${fault} failure does not mark the version active, retry mutations or clean images`, () => {
    const fixture = setup(fault), result = fixture.run(); assert.notEqual(result.status, 0);
    assert(!fs.existsSync(path.join(fixture.install, ".deploy-state/current.json")));
    assert(!fixture.state().events.some(x => x.startsWith("remove:")));
    assert.equal(fixture.state().events.filter(x => x === "migrate").length, 1);
  });
}
for (const fault of ["protected", "foreign-tag", "cleanup", "cleanup-inventory"]) {
  test(`${fault} retains protected images and reports a healthy update with incomplete cleanup`, () => {
    const fixture = setup(fault), result = fixture.run(); assert.equal(result.status, 2, result.stdout + result.stderr);
    assert(fixture.state().running); assert(fs.existsSync(path.join(fixture.install, ".deploy-state/current.json")));
    assert(fixture.state().images.some(image => image.Id === id(1))); assert(fixture.state().images.some(image => image.Id === id(99)));
  });
}
test("a corrupt archive is rejected before any application mutation", () => {
  const fixture = setup(); fs.appendFileSync(path.join(fixture.dir, "unraid-bundle.tar.gz"), "changed");
  const result = fixture.run(); assert.notEqual(result.status, 0); assert(!fixture.state().events.includes("stop"));
});

for (const alteration of ["file-checksum", "symlink", "extra-file", "missing-image"]) test(`${alteration} in a checksummed bundle is rejected before stopping the app`, () => {
  const fixture = setup(), payload = path.join(fixture.dir, "payload");
  if (alteration === "file-checksum") fs.appendFileSync(path.join(payload, "deploy.sh"), "# altered\n");
  if (alteration === "symlink") { fs.unlinkSync(path.join(payload, "update.sh")); fs.symlinkSync("/etc/passwd", path.join(payload, "update.sh")); }
  if (alteration === "extra-file") fs.writeFileSync(path.join(payload, "unexpected"), "x");
  if (alteration === "missing-image") {
    const manifest = JSON.parse(fs.readFileSync(path.join(payload, "manifest.json"))); delete manifest.images.web;
    fs.writeFileSync(path.join(payload, "manifest.json"), JSON.stringify(manifest));
  }
  const archive = path.join(fixture.dir, "unraid-bundle.tar.gz");
  assert.equal(spawnSync("tar", ["-czf", archive, "-C", payload, ...fs.readdirSync(payload)]).status, 0);
  fs.writeFileSync(`${archive}.sha256`, `${sha256(fs.readFileSync(archive))}  unraid-bundle.tar.gz\n`);
  const result = fixture.run(); assert.notEqual(result.status, 0); assert(!fixture.state().events.includes("stop"));
});

test("a stale latest release cannot downgrade an existing installation", () => {
  const fixture = setup(), stateDir = path.join(fixture.install, ".deploy-state"); fs.mkdirSync(stateDir);
  const current = JSON.parse(fs.readFileSync(path.join(fixture.dir, "payload/manifest.json"))); current.version = "build-101-1";
  fs.writeFileSync(path.join(stateDir, "current.json"), JSON.stringify(current));
  const result = fixture.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /äldre/);
  assert(!fixture.state().events.includes("stop"));
});

test("unlabelled old app tags outside observed installation history are reported and retained", () => {
  const fixture = setup("unidentified"), result = fixture.run();
  assert.equal(result.status, 2, result.stdout + result.stderr); assert.match(result.stderr, /Kan inte säkert identifiera äldre image/);
  assert(result.stderr.includes(id(98))); assert(fixture.state().images.some(image => image.Id === id(98)));
});

test("an altered already-installed version is rejected before maintenance", () => {
  const fixture = setup(), stateDir = path.join(fixture.install, ".deploy-state"); fs.mkdirSync(stateDir);
  const current = JSON.parse(fs.readFileSync(path.join(fixture.dir, "payload/manifest.json"))); current.commit = previous;
  fs.writeFileSync(path.join(stateDir, "current.json"), JSON.stringify(current));
  const result = fixture.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /innehåll har ändrats/);
  assert(!fixture.state().events.includes("stop"));
});
test("a concurrent invocation cannot perform any update steps", () => {
  const fixture = setup(), stateDir = path.join(fixture.install, ".deploy-state"); fs.mkdirSync(stateDir);
  const result = spawnSync("flock", [path.join(stateDir, "update.lock"), "bash", path.join(fixture.install, "update.sh")], { env: fixture.env, encoding: "utf8", timeout: 5000 });
  assert.notEqual(result.status, 0); assert.match(result.stderr, /pågår redan/); assert.equal(fixture.state().events.length, 0);
});
