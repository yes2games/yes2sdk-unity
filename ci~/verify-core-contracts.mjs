#!/usr/bin/env node
import { createHash } from "node:crypto";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const ci = dirname(fileURLToPath(import.meta.url));
const root = join(ci, "core-contracts");
const failures = [];

const sha256 = (bytes) => `sha256:${createHash("sha256").update(bytes).digest("hex")}`;
const readJson = (path) => JSON.parse(readFileSync(path, "utf8"));

function walk(dir) {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
        entry.isDirectory() ? walk(join(dir, entry.name)) : [relative(root, join(dir, entry.name)).split(sep).join("/")]
    );
}

const lock = readJson(join(ci, "core-contracts.lock.json"));
const manifestText = readFileSync(join(root, "CONTRACTS.json"), "utf8");
const manifest = JSON.parse(manifestText);
const paths = walk(root)
    .filter((path) => path !== "CONTRACTS.json")
    .sort((a, b) => Buffer.compare(Buffer.from(a), Buffer.from(b)));
const files = paths.map((path) => ({ path, sha256: sha256(readFileSync(join(root, path))) }));

if (!/^[0-9a-f]{40}$/.test(lock.commit ?? "")) {
    failures.push(`lock commit ${JSON.stringify(lock.commit)} is not a full SHA`);
}
if (manifest.schemaVersion !== 1) {
    failures.push(`CONTRACTS.json schemaVersion ${manifest.schemaVersion} is not 1`);
}
for (const path of paths.filter((path) => !/^[A-Za-z0-9._/-]+$/.test(path))) {
    failures.push(`${path} has a character outside A-Z a-z 0-9 . _ - /`);
}
const listed = new Map((manifest.files ?? []).map((file) => [file.path, file.sha256]));
for (const file of files) {
    if (!listed.has(file.path)) {
        failures.push(`${file.path} is not in CONTRACTS.json`);
    } else if (listed.get(file.path) !== file.sha256) {
        failures.push(`${file.path} is ${file.sha256}, CONTRACTS.json says ${listed.get(file.path)}`);
    }
}
for (const path of listed.keys()) {
    if (!paths.includes(path)) {
        failures.push(`${path} is in CONTRACTS.json but missing`);
    }
}

const aggregate = sha256(files.map((file) => `${file.sha256.slice(7)}  ${file.path}\n`).join(""));
if (aggregate !== manifest.aggregate) {
    failures.push(`aggregate ${aggregate} does not match CONTRACTS.json ${manifest.aggregate}`);
}
if (manifestText !== `${JSON.stringify({ schemaVersion: 1, files, aggregate }, null, 2)}\n`) {
    failures.push("CONTRACTS.json is not the canonical manifest Core's generate-contracts.mjs writes");
}
if (aggregate !== lock.aggregate) {
    failures.push(`aggregate ${aggregate} does not match the lock ${lock.aggregate}`);
}

if (failures.length > 0) {
    for (const failure of failures) {
        process.stderr.write(`verify-core-contracts: ${failure}\n`);
    }
    process.exit(1);
}
process.stdout.write(`verify-core-contracts OK: ${files.length} files match ${lock.source}@${lock.commit} (${aggregate})\n`);
