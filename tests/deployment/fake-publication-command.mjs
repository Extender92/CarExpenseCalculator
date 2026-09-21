#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { components, sourceRepository } from "../../scripts/deployment-package.mjs";
const file = process.env.CEC_PUBLICATION_STATE;
const state = JSON.parse(fs.readFileSync(file));
const args = process.argv.slice(2);
const command = path.basename(process.argv[1]);
const event = (name, value) => { state.events.push({ name, value }); fs.writeFileSync(file, JSON.stringify(state)); };
const output = value => process.stdout.write(JSON.stringify(value));
const fail = () => process.exit(1);
if (command === "docker") {
  if (args[0] === "load") event("load");
  else if (args[0] === "tag") event("tag", args[2]);
  else if (args[0] === "push") { event("push", args[1]); if (state.fault === "push" && args[1].includes("-web:")) fail(); }
  else if (args.includes("pull")) { event("anonymous-pull", args.at(-1)); if (state.fault === "private") fail(); }
  else if (args[0] === "image" && args[1] === "inspect") {
    const component = components.find(value => state.images[value] === args[2] || args[2].includes(`-${value}:`));
    if (!component) fail();
    output([{ Config: { Labels: { "org.opencontainers.image.source": sourceRepository, "org.opencontainers.image.revision": state.commit, "se.car-expense-calculator.component": component } },
      RepoDigests: [`ghcr.io/extender92/car-expense-calculator-${component}@${state.images[component]}`] }]);
  } else fail();
} else if (command === "gh") {
  if (args[0] === "release" && args[1] === "upload") { event("upload", args.slice(2)); if (state.fault === "upload") fail(); }
  else if (args[0] === "api" && args.includes("POST")) { event("draft", JSON.parse(fs.readFileSync(args[args.indexOf("--input") + 1]))); output({ id: 42 }); }
  else if (args[0] === "api" && args.includes("PATCH")) { event("promote", JSON.parse(fs.readFileSync(args[args.indexOf("--input") + 1]))); output({ id: 42 }); }
  else if (args[0] === "api" && args[1].includes("?per_page=")) { output([[{ tag_name: state.newest, draft: false, prerelease: false }]]); }
  else fail();
} else fail();
