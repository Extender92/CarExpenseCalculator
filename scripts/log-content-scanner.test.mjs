import assert from "node:assert/strict";
import { test } from "node:test";
import { createLogContentScanner } from "./log-content-scanner.mjs";

test("detects forbidden content split across arbitrary text chunks", () => {
  const scanner = createLogContentScanner(["fake-access-token", "thread.started", "Återgiven annons"]);
  for (const character of "safe\nfake-access-token\nthread.started\nÅtergiven annons\nfake-access-token")
    scanner.write(character);
  assert.deepEqual(scanner.violations(), ["fake-access-token", "thread.started", "Återgiven annons"]);
});

test("does not mistake partial values or unrelated logs for extraction content", () => {
  const scanner = createLogContentScanner(["item.completed", "cars.example"]);
  for (const text of ["item.", "interrupted\n", "GET /health\n", "car", "s.other\n"])
    scanner.write(text);
  assert.deepEqual(scanner.violations(), []);
});

test("checks content after more than 128 MiB of accumulated safe logs", () => {
  const scanner = createLogContentScanner(["fake-access-token"]);
  const chunk = "GET /api/health 200\n".repeat(4096);
  for (let bytes = 0; bytes < 129 * 1024 * 1024; bytes += chunk.length) scanner.write(chunk);
  scanner.write("fake-access-");
  scanner.write("token");
  assert.deepEqual(scanner.violations(), ["fake-access-token"]);
});
