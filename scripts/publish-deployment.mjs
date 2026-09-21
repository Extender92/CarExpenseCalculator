import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { assertPublishContext, components, createBundle, isNewer, registryPrefix, sourceRepository } from "./deployment-package.mjs";

const version = assertPublishContext(process.env);
const root = process.cwd();
const directory = path.resolve(process.argv[3] ?? "temp/prebuilt-deployment/publication");
mkdirSync(directory, { recursive: true });
const inputDirectory = path.resolve(process.argv[2]);
const tested = JSON.parse(readFileSync(path.join(inputDirectory, "tested-images.json"), "utf8"));
if (tested.commit !== process.env.GITHUB_SHA) throw new Error("Tested artifact belongs to another commit.");
execFileSync("docker", ["load", "--input", path.join(inputDirectory, "tested-images.tar")], { stdio: "inherit" });
const docker = (...args) => execFileSync("docker", args, { encoding: "utf8" }).trim();
const gh = (...args) => execFileSync("gh", args, { encoding: "utf8" }).trim();
const anonymousConfig = path.join(directory, "anonymous-docker");
mkdirSync(anonymousConfig, { recursive: true });
writeFileSync(path.join(anonymousConfig, "config.json"), "{}");
const images = {};
for (const component of components) {
  const [image] = JSON.parse(docker("image", "inspect", tested.images[component]));
  if (image.Config.Labels["org.opencontainers.image.source"] !== sourceRepository ||
      image.Config.Labels["org.opencontainers.image.revision"] !== tested.commit ||
      image.Config.Labels["se.car-expense-calculator.component"] !== component)
    throw new Error("Loaded image does not match verified identity.");
  const repository = `${registryPrefix}-${component}`;
  const tag = `${repository}:${version}`;
  docker("tag", tested.images[component], tag);
  execFileSync("docker", ["push", tag], { stdio: "inherit" });
  const [pushed] = JSON.parse(docker("image", "inspect", tag));
  const reference = pushed.RepoDigests.find(value => value.startsWith(`${repository}@sha256:`));
  if (!reference) throw new Error("Published digest missing.");
  images[component] = reference;
}
// All packages must be public before any release becomes installable.
for (const component of components) {
  try { execFileSync("docker", ["--config", anonymousConfig, "pull", images[component]], { stdio: "inherit" }); }
  catch { throw new Error(`Make package car-expense-calculator-${component} public in GitHub Packages, then rerun the workflow. No release was promoted.`); }
}
const bundle = createBundle(root, directory, version, tested.commit, images);
const repository = "Extender92/CarExpenseCalculator";
const endpoint = `repos/${repository}/releases`;
const draftFile = path.join(directory, "release.json");
writeFileSync(draftFile, JSON.stringify({ tag_name: version, target_commitish: tested.commit,
  name: `Deployment ${version}`, draft: true, prerelease: false,
  body: `Verified Linux amd64 deployment of ${tested.commit}.\nDownload the Unraid bundle; images are pinned by digest.\nCI: https://github.com/${repository}/actions/runs/${process.env.GITHUB_RUN_ID}` }));
const draft = JSON.parse(gh("api", endpoint, "--method", "POST", "--input", draftFile));
gh("release", "upload", version, bundle.archive, bundle.checksum, "--repo", repository);
// The workflow's publication concurrency lock covers this read + promotion.
const allReleases = JSON.parse(gh("api", `${endpoint}?per_page=100`, "--paginate", "--slurp")).flat();
const published = allReleases.filter(release => !release.draft && !release.prerelease && /^build-[0-9]+-[0-9]+$/.test(release.tag_name));
const newest = published.reduce((current, release) => !current || isNewer(release.tag_name, current) ? release.tag_name : current, null);
const promoteFile = path.join(directory, "promote.json");
writeFileSync(promoteFile, JSON.stringify({ draft: false, make_latest: isNewer(version, newest) ? "true" : "false" }));
gh("api", `${endpoint}/${draft.id}`, "--method", "PATCH", "--input", promoteFile);
console.log(`Published coherent deployment ${version}.`);
