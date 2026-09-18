import { execFileSync } from "node:child_process";
import type { FullConfig } from "@playwright/test";

/** Fail before fixture writes if a test URL points at an ordinary installation. */
export default async function verifyIsolatedStack(config: FullConfig) {
  const project = process.env.COMPOSE_PROJECT_NAME ?? "car-expense-e2e";
  if (project !== "car-expense-e2e") throw new Error("E2E requires the disposable car-expense-e2e project.");
  const docker = process.platform === "win32" ? "docker.exe" : "docker";
  const inspect = (service: string) => JSON.parse(execFileSync(docker, ["inspect", `${project}-${service}-1`], { encoding: "utf8" }))[0];
  const web = inspect("web");
  const api = inspect("api");
  if (api.Config.Labels["com.docker.compose.project"] !== project ||
      !api.Config.Env.includes("CodexExtraction__BaseUrl=http://fake-codex-extractor:8080"))
    throw new Error("E2E requires the disposable API with fake extraction.");
  const ports = web.NetworkSettings.Ports["80/tcp"] ?? [];
  for (const testProject of config.projects) {
    const url = new URL(testProject.use.baseURL!);
    if (!["localhost", "127.0.0.1"].includes(url.hostname) ||
        !ports.some((port: { HostPort: string }) => port.HostPort === url.port))
      throw new Error("E2E_BASE_URL must match the disposable web container's published port.");
  }
  const base = config.projects[0].use.baseURL!;
  const inventory = await fetch(new URL("/api/vehicle-cost-inputs", base));
  if (!inventory.ok || (await inventory.json()).length !== 0)
    throw new Error("The disposable E2E inventory must be empty before a run. Inspect and remove only test-owned leftovers.");
}
