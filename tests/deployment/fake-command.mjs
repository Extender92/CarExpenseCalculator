#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
const file = process.env.CEC_FAKE_STATE;
const state = JSON.parse(fs.readFileSync(file, "utf8"));
const command = path.basename(process.argv[1]);
const args = process.argv.slice(2);
const save = () => fs.writeFileSync(file, JSON.stringify(state));
const output = value => process.stdout.write(typeof value === "string" ? value : JSON.stringify(value));
const die = () => { save(); process.stderr.write("diagnostic should-not-print-password\n"); process.exit(1); };
const record = event => { state.events.push(event); save(); };
const id = n => `sha256:${String(n).padStart(64, "0")}`;
const image = reference => state.images.find(value => value.Id === reference || value.RepoTags.includes(reference) || value.RepoDigests.includes(reference));
const container = service => ({ Id: service, Image: state.current[service],
  Config: { Image: state.currentReferences[service], Labels: { "com.docker.compose.project": "car-expense-calculator", "com.docker.compose.service": service } },
  State: { Running: state.running, Health: { Status: "healthy" } },
  Mounts: service === "codex-extractor" ? [{ Destination: "/var/lib/codex", Type: "bind", Source: state.fault === "mount" ? "/different/codex" : "/existing/codex" }] : [] });
if (command === "curl") {
  const url = args.at(-1), at = args.indexOf("--output");
  let data;
  if (url.includes("/api/")) {
    record("health"); if (state.fault === "health") die();
    if (!url.startsWith("http://127.0.0.1:6425/")) die();
    data = JSON.stringify({ status: "healthy", database: "available", integrations: { codexListingExtractionConfigured: true } });
  } else {
    record("download"); if (state.fault === "download") die();
    const mapping = { "https://test.invalid/releases/latest": "release.json", "https://test.invalid/bundle": "unraid-bundle.tar.gz", "https://test.invalid/checksum": "unraid-bundle.tar.gz.sha256" };
    if (!mapping[url]) die();
    data = fs.readFileSync(path.join(state.fixture, mapping[url]));
  }
  if (at >= 0) fs.writeFileSync(args[at + 1], data); else output(data);
} else if (command === "docker") {
  if (args[0] === "info") { if (state.fault === "daemon") die(); output(args.includes("--format") ? "linux/amd64\n" : "ok\n"); }
  else if (args[0] === "network") { if (state.fault === "network") die(); output([{}]); }
  else if (args[0] === "exec" && args[1] === "postgresql18") { if (state.fault === "postgres-ready") die(); }
  else if (args[0] === "inspect") {
    if (args[1] === "postgresql18") output([{ State: { Running: state.fault !== "postgres" }, NetworkSettings: { Networks: { "car-expense-network": {} } } }]);
    else output(args.slice(1).map(service => service === "unrelated" ? { Image: state.protectedId } : container(service)));
  } else if (args[0] === "ps") {
    if (state.fault === "cleanup-inventory" && !args.includes("--filter")) die();
    output(["api", "web", "codex-extractor", ...(!args.includes("--filter") && state.protectedId ? ["unrelated"] : [])].join("\n") + "\n");
  } else if (args[0] === "pull") { record(`pull:${args[1]}`); if (state.fault === "pull" && args[1].includes("-web@")) die(); output("pulled\n"); }
  else if (args[0] === "image" && args[1] === "inspect") { const values = args.slice(2).map(image); if (values.some(value => !value)) die(); output(values); }
  else if (args[0] === "image" && args[1] === "ls") output(state.images.filter(value => !args.includes("--filter") || value.Config.Labels["se.car-expense-calculator.component"]).map(value => value.Id).join("\n") + "\n");
  else if (args[0] === "image" && args[1] === "rm") {
    record(`remove:${args[2]}`); if (state.fault === "cleanup") die();
    const value = image(args[2]); state.images = state.images.filter(candidate => candidate !== value); save();
  } else if (args[0] === "compose") {
    if (args[1] === "version") output("Docker Compose version v5.5.0\n");
    else if (args.includes("config")) {
      record("config"); if (state.fault === "config") die();
      const config = { name: "car-expense-calculator", services: {
        api: { image: process.env.CEC_API_IMAGE, environment: { ConnectionStrings__Postgres: "Host=postgresql18;Password=should-not-print-password" } },
        web: { image: process.env.CEC_WEB_IMAGE, ports: [{ target: 80, published: "6425" }] },
        "codex-extractor": { image: process.env.CEC_EXTRACTOR_IMAGE, volumes: [{ type: "bind", target: "/var/lib/codex", source: "/existing/codex" }] },
      }, networks: { "car-expense-network": { external: true, name: "car-expense-network" } } };
      output(config);
    } else if (args.includes("stop")) { record("stop"); state.running = false; save(); }
    else if (args.includes("run")) { record("migrate"); if (state.fault === "migration") die(); }
    else if (args.includes("up")) {
      record("start"); if (state.fault === "start") die();
      state.running = true;
      for (const [service, env] of [["api", "CEC_API_IMAGE"], ["web", "CEC_WEB_IMAGE"], ["codex-extractor", "CEC_EXTRACTOR_IMAGE"]]) {
        state.currentReferences[service] = process.env[env]; state.current[service] = image(process.env[env]).Id;
      }
      if (state.fault === "wrong-running") state.current.api = id(1);
      save();
    } else die();
  } else die();
} else die();
