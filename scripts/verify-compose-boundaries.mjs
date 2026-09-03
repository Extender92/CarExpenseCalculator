import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const verificationEnvironment = {
  ...process.env,
  WEB_PORT: "8088",
  POSTGRES_DB: "car_expense_calculator",
  POSTGRES_USER: "car_expense_app",
  POSTGRES_PASSWORD: "compose-boundary-verification-only",
  CODEX_HOME_PATH: "/tmp/car-expense-codex-boundary-verification",
};

const local = resolveCompose("compose.yaml");
const unraid = resolveCompose("compose.unraid.yaml");
const e2e = resolveCompose("compose.yaml", "compose.e2e.yaml");

verifyPublishedPorts(local, "local Compose");
verifySharedNetwork(local, ["api", "codex-extractor", "postgres", "web"], "app-network", "local Compose");
assert(local.networks["app-network"]?.name === "car-expense-local", "Local Compose must use the car-expense-local network.");
assert(local.services.postgres?.image === "postgres:18", "Local Compose must use PostgreSQL 18.");
verifyCodexBoundary(local, "volume", "codex-home", "local Compose");

verifyPublishedPorts(unraid, "Unraid Compose");
assert(!("postgres" in unraid.services), "Unraid Compose must not define a replacement PostgreSQL service.");
verifySharedNetwork(unraid, ["api", "codex-extractor", "web"], "car-expense-network", "Unraid Compose");
assert(unraid.networks["car-expense-network"]?.external === true, "Unraid must use an external car-expense-network.");
verifyCodexBoundary(unraid, "bind", "/tmp/car-expense-codex-boundary-verification", "Unraid Compose");

const unraidConnection = parseConnectionString(unraid.services.api.environment.ConnectionStrings__Postgres);
assert(unraidConnection.Host === "postgresql18", "Unraid API must connect to the postgresql18 container.");
assert(unraidConnection.Port === "5432", "Unraid API must use PostgreSQL container port 5432.");
assert(unraidConnection.Database === "car_expense_calculator", "Unraid API must use the dedicated car_expense_calculator database.");
assert(unraidConnection.Username === "car_expense_app", "Unraid API must use the dedicated car_expense_app role.");
assert(local.services.api.environment.CodexExtraction__BaseUrl === "http://codex-extractor:8080", "Local API must use the internal Codex extractor address.");
assert(unraid.services.api.environment.CodexExtraction__BaseUrl === "http://codex-extractor:8080", "Unraid API must use the internal Codex extractor address.");

const fakeExtractor = e2e.services["fake-codex-extractor"];
assert(fakeExtractor, "E2E Compose must define the fake Codex extractor.");
assert(!fakeExtractor.ports || fakeExtractor.ports.length === 0, "The E2E fake extractor must publish no ports.");
assert(Object.keys(fakeExtractor.networks ?? {}).length === 1 && "extractor-test-network" in fakeExtractor.networks, "The E2E fake extractor must use only its internal test network.");
assert(e2e.networks["extractor-test-network"]?.internal === true, "The E2E fake extractor network must block external access.");
assert("app-network" in e2e.services.api.networks && "extractor-test-network" in e2e.services.api.networks, "The E2E API must bridge the app and fake-extractor networks.");
assert(e2e.services.api.environment.CodexExtraction__BaseUrl === "http://fake-codex-extractor:8080", "The E2E API must target only the fake extractor.");
const fakeEnvironment = Object.keys(fakeExtractor.environment ?? {});
assert(!fakeEnvironment.some((name) => name.startsWith("POSTGRES_") || name.startsWith("ConnectionStrings__") || name.includes("CODEX") || name.includes("OPENAI")), "The E2E fake extractor must receive no database, Codex, or OpenAI configuration.");

console.log("Compose port, network, PostgreSQL, and Codex boundaries are valid.");

function resolveCompose(...files) {
  const fileArguments = files.flatMap((file) => ["-f", file]);
  const output = execFileSync(
    process.platform === "win32" ? "docker.exe" : "docker",
    ["compose", ...fileArguments, "config", "--format", "json"],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
      env: verificationEnvironment,
      stdio: ["ignore", "pipe", "inherit"],
    },
  );

  return JSON.parse(output);
}

function verifyPublishedPorts(config, description) {
  const publishedServices = Object.entries(config.services)
    .filter(([, service]) => Array.isArray(service.ports) && service.ports.length > 0);

  assert(publishedServices.length === 1, `${description} must publish exactly one service.`);
  const [[serviceName, service]] = publishedServices;
  assert(serviceName === "web", `${description} may publish only the web service.`);
  assert(service.ports.length === 1, `${description} web must publish exactly one port.`);

  const [port] = service.ports;
  assert(port.target === 80, `${description} web must target container port 80.`);
  assert(String(port.published) === "8088", `${description} must default to host port 8088.`);
  assert(port.protocol === "tcp", `${description} web port must use TCP.`);
}

function verifySharedNetwork(config, serviceNames, networkName, description) {
  for (const serviceName of serviceNames) {
    const service = config.services[serviceName];
    assert(service, `${description} must define the ${serviceName} service.`);
    assert(
      Object.keys(service.networks ?? {}).length === 1 && networkName in service.networks,
      `${description} ${serviceName} must use only ${networkName}.`,
    );
  }
}

function verifyCodexBoundary(config, expectedMountType, expectedSource, description) {
  const extractor = config.services["codex-extractor"];
  assert(extractor, `${description} must define codex-extractor.`);
  assert(!extractor.ports || extractor.ports.length === 0, `${description} codex-extractor must publish no port.`);
  assert(extractor.environment.CODEX_HOME === "/var/lib/codex", `${description} must use the dedicated container Codex home.`);
  assert(extractor.environment.CODEX_MODEL === "gpt-5.6-luna", `${description} must request gpt-5.6-luna.`);
  assert(extractor.environment.CODEX_REASONING_EFFORT === "medium", `${description} must use medium reasoning.`);
  const healthCommand = (extractor.healthcheck?.test ?? []).join(" ");
  assert(healthCommand.includes("/health/live"), `${description} Codex health must use process liveness.`);
  assert(!healthCommand.includes("/internal/status") && !healthCommand.includes("login"), `${description} Codex health must not require authentication readiness.`);
  assert(config.services.api.depends_on?.["codex-extractor"]?.condition === "service_healthy", `${description} API must depend on Codex process liveness.`);

  const forbiddenEnvironment = Object.keys(extractor.environment)
    .filter((name) => name.startsWith("POSTGRES_")
      || name.startsWith("ConnectionStrings__")
      || name === "OPENAI_API_KEY"
      || name === "CODEX_API_KEY");
  assert(forbiddenEnvironment.length === 0, `${description} codex-extractor must receive no database or API-key configuration.`);

  const mounts = extractor.volumes ?? [];
  assert(mounts.length === 1, `${description} codex-extractor must have exactly one mount.`);
  const [mount] = mounts;
  assert(mount.type === expectedMountType, `${description} Codex home must use a ${expectedMountType} mount.`);
  assert(mount.source === expectedSource, `${description} Codex home mount source is incorrect.`);
  assert(mount.target === "/var/lib/codex", `${description} may mount only the dedicated Codex home.`);

  for (const serviceName of ["api", "web", "postgres"]) {
    const service = config.services[serviceName];
    if (!service) continue;
    assert(
      !(service.volumes ?? []).some((volume) => volume.source === expectedSource || volume.target === "/var/lib/codex"),
      `${description} ${serviceName} must not mount Codex authentication state.`,
    );
  }
}

function parseConnectionString(connectionString) {
  return Object.fromEntries(
    connectionString
      .split(";")
      .filter(Boolean)
      .map((entry) => {
        const separator = entry.indexOf("=");
        return [entry.slice(0, separator), entry.slice(separator + 1)];
      }),
  );
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}
