import { createHash } from "node:crypto";
import { chmodSync, copyFileSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";

export const components = ["api", "web", "codex-extractor"];
export const sourceRepository = "https://github.com/Extender92/CarExpenseCalculator";
export const registryPrefix = "ghcr.io/extender92/car-expense-calculator";
export const sha256 = (content) => createHash("sha256").update(content).digest("hex");
export function versionFor(run, attempt) {
  if (![run, attempt].every(value => /^[1-9][0-9]*$/.test(String(value)))) throw new Error("Invalid workflow identity.");
  return `build-${run}-${attempt}`;
}
export function isNewer(candidate, current) {
  const parse = value => /^build-([1-9][0-9]*)-([1-9][0-9]*)$/.exec(value ?? "");
  const next = parse(candidate), previous = parse(current);
  if (!next) throw new Error("Invalid deployment version.");
  if (!previous) return true;
  return BigInt(next[1]) > BigInt(previous[1]) ||
    (next[1] === previous[1] && BigInt(next[2]) > BigInt(previous[2]));
}
export function assertPublishContext(env) {
  if (env.GITHUB_REPOSITORY !== "Extender92/CarExpenseCalculator" ||
      env.GITHUB_REF !== "refs/heads/main" || env.GITHUB_EVENT_NAME !== "push")
    throw new Error("Only the verified main push may publish.");
  if (!/^[a-f0-9]{40}$/.test(env.GITHUB_SHA ?? "")) throw new Error("Invalid commit.");
  return versionFor(env.GITHUB_RUN_ID, env.GITHUB_RUN_ATTEMPT);
}
export function createBundle(root, output, version, commit, images) {
  if (!/^build-[1-9][0-9]*-[1-9][0-9]*$/.test(version)) throw new Error("Invalid deployment version.");
  if (!/^[a-f0-9]{40}$/.test(commit)) throw new Error("Invalid commit.");
  if (Object.keys(images).sort().join() !== [...components].sort().join()) throw new Error("Incomplete image set.");
  for (const component of components) {
    const pattern = new RegExp(`^[a-z0-9][a-z0-9.:/-]*/car-expense-calculator-${component}@sha256:[a-f0-9]{64}$`);
    if (!pattern.test(images[component])) throw new Error(`Invalid ${component} digest reference.`);
  }
  const payload = path.join(output, "payload");
  mkdirSync(payload, { recursive: true });
  const names = ["compose.unraid.yaml", "update.sh", "deploy.sh", "deploy-lib.sh", ".env.example"];
  const files = {};
  for (const name of names) {
    const source = ["compose.unraid.yaml", ".env.example"].includes(name) ? path.join(root, name) : path.join(root, "deployment", name);
    copyFileSync(source, path.join(payload, name));
    // Deployment artifacts must not depend on a contributor's Git CRLF policy.
    const content = readFileSync(path.join(payload, name), "utf8").replaceAll("\r\n", "\n");
    writeFileSync(path.join(payload, name), content, { mode: name.endsWith(".sh") ? 0o755 : 0o644 });
    chmodSync(path.join(payload, name), name.endsWith(".sh") ? 0o755 : 0o644);
    files[name] = sha256(content);
  }
  const manifest = { formatVersion: 1, version, commit, architecture: "linux/amd64", images, files };
  writeFileSync(path.join(payload, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");
  const archive = path.join(output, "unraid-bundle.tar.gz");
  execFileSync("tar", ["-czf", archive, "-C", payload, ...names, "manifest.json"]);
  writeFileSync(`${archive}.sha256`, `${sha256(readFileSync(archive))}  unraid-bundle.tar.gz\n`);
  return { manifest, archive, checksum: `${archive}.sha256` };
}
