import { execFileSync } from "node:child_process";
import { expect } from "@playwright/test";

// Only call with the two UUIDs created by the current test. This guard is shared
// by stage acceptance suites; no configurable SQL target or product endpoint.
export function corruptOwnedLegacyResults(ids: readonly string[]) {
  expect(ids).toHaveLength(2);
  for (const id of ids) expect(id).toMatch(/^[0-9a-f-]{36}$/);
  const project = process.env.COMPOSE_PROJECT_NAME ?? "car-expense-e2e";
  expect(project).toBe("car-expense-e2e");
  const docker = process.platform === "win32" ? "docker.exe" : "docker";
  const compose = [
    "compose",
    "-p",
    project,
    "-f",
    "compose.yaml",
    "-f",
    "compose.e2e.yaml",
  ];
  const services = execFileSync(
    docker,
    [...compose, "ps", "--services", "--status", "running"],
    { cwd: "../..", encoding: "utf8" },
  );
  expect(services.split(/\r?\n/)).toContain("fake-codex-extractor");
  expect(services.split(/\r?\n/)).not.toContain("codex-extractor");
  execFileSync(
    docker,
    [
      ...compose,
      "exec",
      "-T",
      "postgres",
      "psql",
      "-U",
      "car_expense_app",
      "-d",
      "car_expense_calculator",
      "-v",
      "ON_ERROR_STOP=1",
      "-c",
      `UPDATE saved_cost_scenarios SET result_schema_version=999, result_snapshot='{}'::jsonb WHERE vehicle_id IN ('${ids[0]}','${ids[1]}')`,
    ],
    { cwd: "../..", encoding: "utf8" },
  );
}
