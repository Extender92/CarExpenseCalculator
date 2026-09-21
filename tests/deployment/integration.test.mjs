import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import http from "node:http";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { createBundle, components } from "../../scripts/deployment-package.mjs";

const exec = promisify(execFile);
const directory = process.env.CEC_TEST_TEMP;
const input = process.env.CEC_TESTED_IMAGES;
if (!directory || !input || process.env.DOCKER_HOST !== "tcp://127.0.0.1:2375")
  throw new Error("Run this suite in its disposable Docker-in-Docker Compose stack.");
fs.mkdirSync(directory, { recursive: true });
const docker = async (...args) => (await exec("docker", args, { maxBuffer: 32 * 1024 * 1024, timeout: 300000 })).stdout.trim();
const wait = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
async function until(operation) {
  for (let attempt = 0; attempt < 60; attempt++) {
    try { return await operation(); } catch (error) { if (attempt === 59) throw error; await wait(1000); }
  }
}

test("source-free upgrade preserves legacy PostgreSQL data and login mount, runs exact tested images and safely removes the previous version", { timeout: 600000 }, async t => {
  const tested = JSON.parse(fs.readFileSync(path.join(input, "tested-images.json")));
  await docker("load", "--input", path.join(input, "tested-images.tar"));
  await docker("network", "create", "car-expense-network");
  await docker("run", "-d", "--name", "registry", "-p", "127.0.0.1:5000:5000", "registry:3");
  await until(async () => { const response = await fetch("http://127.0.0.1:5000/v2/"); assert(response.ok); });
  await docker("run", "-d", "--name", "postgresql18", "--network", "car-expense-network",
    "-e", "POSTGRES_USER=deployment_test", "-e", "POSTGRES_PASSWORD=disposable-test-only",
    "-e", "POSTGRES_DB=deployment_test", "postgres:18");
  await until(() => docker("exec", "postgresql18", "pg_isready", "-U", "deployment_test", "-d", "deployment_test"));
  const connection = "Host=postgresql18;Database=deployment_test;Username=deployment_test;Password=disposable-test-only";
  await docker("run", "--rm", "--network", "car-expense-network", "-e", `ConnectionStrings__Postgres=${connection}`,
    tested.images.api, "migrate", "20260910214541_AllowHtmlListingExtraction");
  const sql = async command => docker("exec", "postgresql18", "psql", "-U", "deployment_test", "-d", "deployment_test", "-XAt", "-v", "ON_ERROR_STOP=1", "-c", command);
  const oldCost = { input: { candidateKey: "TST951", acquisitionType: "purchase", priceSek: 12345.67,
    residual: null, lease: null, energySources: [], tax: null, insurance: null, service: null, repairs: null,
    additionalRepairAllowancePerMonthSek: null, customCosts: null }, unresolvedItems: [] };
  await sql(`INSERT INTO vehicles (id,registration_number,vehicle_label,revision,created_at_utc,updated_at_utc) VALUES ('11111111-1111-4111-8111-111111111111','TST951','Fiktiv uppdateringsbil – åäö',7,now(),now()); INSERT INTO vehicle_cost_inputs (vehicle_id,schema_version,input) VALUES ('11111111-1111-4111-8111-111111111111',1,'${JSON.stringify(oldCost)}'); UPDATE household_state SET transition_revision=9 WHERE id=1;`);
  const snapshotQuery = "SELECT jsonb_build_object('vehicles',(SELECT jsonb_agg(v) FROM vehicles v),'costs',(SELECT jsonb_agg(c) FROM vehicle_cost_inputs c),'household',(SELECT jsonb_agg(h) FROM household_state h));";
  const before = await sql(snapshotQuery);
  assert.equal(await sql('SELECT count(*) FROM "__EFMigrationsHistory"'), "7");

  await docker("volume", "create", "deployment-codex-home");
  const authPath = await docker("volume", "inspect", "deployment-codex-home", "--format", "{{.Mountpoint}}");
  await docker("run", "--rm", "--user", "0", "--mount", `type=bind,source=${authPath},target=/auth`, "--entrypoint", "sh", tested.images.api,
    "-c", "printf 'retained-test-marker' > /auth/test-marker; chown -R 1654:1654 /auth");
  const install = path.join(directory, "deploy"); fs.mkdirSync(install, { recursive: true });
  const environment = `WEB_PORT=8091\nPOSTGRES_DB=deployment_test\nPOSTGRES_USER=deployment_test\nPOSTGRES_PASSWORD=disposable-test-only\nCODEX_HOME_PATH=${authPath}\n`;
  fs.writeFileSync(path.join(install, ".env"), environment, { mode: 0o600 });
  const references = {}, legacy = {}, oldIds = [];
  const context = path.join(directory, "old-image"); fs.mkdirSync(context, { recursive: true });
  for (const component of components) {
    const oldTag = `car-expense-calculator-${component}:local`;
    fs.writeFileSync(path.join(context, "Dockerfile"), `FROM car-expense-e2e-${component}:latest\nLABEL org.opencontainers.image.revision="${"b".repeat(40)}"\n`);
    await docker("build", "--network=none", "-t", oldTag, context);
    legacy[component] = oldTag;
    oldIds.push(await docker("image", "inspect", oldTag, "--format", "{{.Id}}"));
    const repository = `localhost:5000/extender92/car-expense-calculator-${component}`;
    await docker("tag", tested.images[component], `${repository}:test`);
    await docker("push", `${repository}:test`);
    const [image] = JSON.parse(await docker("image", "inspect", `${repository}:test`));
    references[component] = image.RepoDigests.find(value => value.startsWith(`${repository}@`));
    assert(references[component]);
    // The fixture import tag is not an installed app repository; remove it before scoped cleanup.
    await docker("image", "rm", `car-expense-e2e-${component}:latest`);
  }
  const bundle = createBundle(process.cwd(), directory, "build-100-1", tested.commit, references);
  for (const name of ["update.sh", "deploy.sh", "deploy-lib.sh", "compose.unraid.yaml"]) fs.copyFileSync(path.join(directory, "payload", name), path.join(install, name));
  await exec("docker", ["compose", "--project-name", "car-expense-calculator", "--env-file", path.join(install, ".env"), "-f", path.join(install, "compose.unraid.yaml"), "up", "--detach", "--wait", "--wait-timeout", "120"], {
    env: { ...process.env, COMPOSE_FILE: "", CEC_API_IMAGE: legacy.api, CEC_WEB_IMAGE: legacy.web, CEC_EXTRACTOR_IMAGE: legacy["codex-extractor"] }, timeout: 180000, maxBuffer: 16 * 1024 * 1024,
  });
  // A stopped unrelated container must survive as well as its image.
  await docker("create", "--name", "unrelated-stopped", "--entrypoint", "true", "registry:3");
  const databaseContainer = await docker("inspect", "postgresql18", "--format", "{{.Id}}");
  let origin;
  const server = http.createServer((request, response) => {
    if (request.url === "/latest") {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ tag_name: bundle.manifest.version, draft: false, prerelease: false, assets: ["unraid-bundle.tar.gz", "unraid-bundle.tar.gz.sha256"].map(name => ({ name, browser_download_url: `${origin}/${name}` })) }));
    } else if (["/unraid-bundle.tar.gz", "/unraid-bundle.tar.gz.sha256"].includes(request.url)) response.end(fs.readFileSync(path.join(directory, request.url.slice(1))));
    else { response.statusCode = 404; response.end(); }
  });
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
  t.after(() => new Promise(resolve => server.close(resolve)));
  const update = async name => {
    try {
      const result = await exec("bash", [path.join(install, "update.sh")], { env: { ...process.env, CEC_RELEASE_API: `${origin}/latest` }, timeout: 240000, maxBuffer: 16 * 1024 * 1024 });
      fs.writeFileSync(path.join(directory, name), result.stdout + result.stderr); return result;
    } catch (error) { fs.writeFileSync(path.join(directory, name), `${error.stdout}\n${error.stderr}`); throw error; }
  };
  await update("first-update.log");
  assert.equal(await sql('SELECT count(*) FROM "__EFMigrationsHistory"'), "8");
  assert.equal(await sql(snapshotQuery), before);
  assert.equal(fs.readFileSync(path.join(install, ".env"), "utf8"), environment);
  assert.equal(await docker("inspect", "postgresql18", "--format", "{{.Id}}"), databaseContainer);
  assert.equal(await docker("exec", "car-expense-calculator-codex-extractor-1", "cat", "/var/lib/codex/test-marker"), "retained-test-marker");
  assert.match(await docker("exec", "car-expense-calculator-codex-extractor-1", "codex", "--version"), /0\.153\.0/);
  const health = await (await fetch("http://127.0.0.1:8091/api/health/ready")).json(); assert.equal(health.status, "healthy");
  const storedResponse = await fetch("http://127.0.0.1:8091/api/vehicle-cost-inputs/11111111-1111-4111-8111-111111111111");
  assert(storedResponse.ok); const stored = await storedResponse.json();
  assert.equal(stored.registrationNumber, "TST951"); assert.equal(stored.revision, 7);
  assert.equal(stored.input.priceSek, 12345.67); assert.equal(stored.input.electricDrivingShare.mode, "inherit");
  const started = {};
  for (const component of components) {
    const [container] = JSON.parse(await docker("inspect", `car-expense-calculator-${component}-1`));
    assert.equal(container.Image, tested.images[component]); started[component] = container.State.StartedAt;
  }
  for (const oldId of oldIds) await assert.rejects(docker("image", "inspect", oldId));
  await docker("inspect", "unrelated-stopped"); await docker("image", "inspect", "registry:3");
  const again = await update("already-current.log"); assert.match(again.stdout, /redan aktuell/);
  for (const component of components) assert.equal(await docker("inspect", `car-expense-calculator-${component}-1`, "--format", "{{.State.StartedAt}}"), started[component]);
  assert(!fs.existsSync(path.join(install, ".git"))); assert(!fs.existsSync(path.join(install, "src")));
  t.diagnostic("Seven-to-eight migration upgrade; legacy data/revisions unchanged; port 8091; exact three tested images; auth marker preserved; previous app images removed; current rerun did not restart.");
});
