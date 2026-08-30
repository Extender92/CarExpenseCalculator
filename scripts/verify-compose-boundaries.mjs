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
};

const local = resolveCompose("compose.yaml");
const unraid = resolveCompose("compose.unraid.yaml");

verifyPublishedPorts(local, "local Compose");
verifySharedNetwork(local, ["api", "postgres", "web"], "app-network", "local Compose");
assert(local.networks["app-network"]?.name === "car-expense-local", "Local Compose must use the car-expense-local network.");
assert(local.services.postgres?.image === "postgres:18", "Local Compose must use PostgreSQL 18.");

verifyPublishedPorts(unraid, "Unraid Compose");
assert(!("postgres" in unraid.services), "Unraid Compose must not define a replacement PostgreSQL service.");
verifySharedNetwork(unraid, ["api", "web"], "car-expense-network", "Unraid Compose");
assert(unraid.networks["car-expense-network"]?.external === true, "Unraid must use an external car-expense-network.");

const unraidConnection = parseConnectionString(unraid.services.api.environment.ConnectionStrings__Postgres);
assert(unraidConnection.Host === "postgresql18", "Unraid API must connect to the postgresql18 container.");
assert(unraidConnection.Port === "5432", "Unraid API must use PostgreSQL container port 5432.");
assert(unraidConnection.Database === "car_expense_calculator", "Unraid API must use the dedicated car_expense_calculator database.");
assert(unraidConnection.Username === "car_expense_app", "Unraid API must use the dedicated car_expense_app role.");

console.log("Compose port, network, and PostgreSQL boundaries are valid.");

function resolveCompose(file) {
  const output = execFileSync(
    process.platform === "win32" ? "docker.exe" : "docker",
    ["compose", "-f", file, "config", "--format", "json"],
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
