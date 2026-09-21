import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { components, sourceRepository } from "./deployment-package.mjs";

const output = path.resolve(process.argv[2] ?? "temp/prebuilt-deployment/tested-images");
const commit = process.env.CEC_BUILD_COMMIT;
if (!/^[a-f0-9]{40}$/.test(commit ?? "")) throw new Error("CEC_BUILD_COMMIT is required.");
const project = process.env.COMPOSE_PROJECT_NAME ?? "car-expense-e2e";
const names = components.map(component => `${project}-${component}:latest`);
const images = {};
for (const [index, component] of components.entries()) {
  const [image] = JSON.parse(execFileSync("docker", ["image", "inspect", names[index]], { encoding: "utf8" }));
  if (image.Config.Labels["org.opencontainers.image.source"] !== sourceRepository ||
      image.Config.Labels["org.opencontainers.image.revision"] !== commit ||
      image.Config.Labels["se.car-expense-calculator.component"] !== component ||
      image.Os !== "linux" || image.Architecture !== "amd64") throw new Error(`Unverified image identity: ${component}`);
  images[component] = image.Id;
}
mkdirSync(output, { recursive: true });
execFileSync("docker", ["save", "--output", path.join(output, "tested-images.tar"), ...names], { stdio: "inherit" });
writeFileSync(path.join(output, "tested-images.json"), JSON.stringify({ commit, images }, null, 2));
