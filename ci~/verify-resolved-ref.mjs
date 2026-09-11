#!/usr/bin/env node
// verify-resolved-ref.mjs - prove a clean consumer resolved the exact commit
// (yes2games/yes2sdk-unity#97 sections 8 and 10).
//
// "edge points at the candidate" and "the consumer resolves the candidate" are
// two different claims. A tag can be correct while a consumer resolves something
// else - a cached package, a stale lock, a ref that moved between the tag push
// and the resolve. UPM writes the commit it actually fetched into
// packages-lock.json as `hash`, so that is what gets compared here.
//
// Usage:
//   node ci~/verify-resolved-ref.mjs <consumer-dir> <expected-40-char-sha>
//
// Exit codes: 0 exact match, 1 mismatch, 2 usage or I/O error.

import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const PACKAGE_NAME = "com.yes2games.yes2sdk";

const [dir, expected] = process.argv.slice(2);
if (dir === undefined || expected === undefined) {
    process.stderr.write("usage: node ci~/verify-resolved-ref.mjs <consumer-dir> <expected-40-char-sha>\n");
    process.exit(2);
}
if (!/^[0-9a-f]{40}$/.test(expected)) {
    process.stderr.write(`verify-resolved-ref: expected a full 40-character SHA, got "${expected}"\n`);
    process.exit(2);
}

const lockPath = join(resolve(dir), "Packages", "packages-lock.json");
let lock;
try {
    lock = JSON.parse(readFileSync(lockPath, "utf8"));
} catch (error) {
    process.stderr.write(`verify-resolved-ref: cannot read ${lockPath}: ${error.message}\n`);
    process.exit(2);
}

const entry = lock.dependencies?.[PACKAGE_NAME];
if (entry === undefined) {
    process.stderr.write(`verify-resolved-ref: ${lockPath} has no ${PACKAGE_NAME} entry; the package did not resolve\n`);
    process.exit(1);
}
if (entry.source !== "git") {
    process.stderr.write(
        `verify-resolved-ref: ${PACKAGE_NAME} resolved from source "${entry.source}", not git. ` +
            "A clean consumer must fetch the ref, not a local path or a registry copy.\n"
    );
    process.exit(1);
}
if (entry.hash !== expected) {
    process.stderr.write(
        `verify-resolved-ref: ${PACKAGE_NAME} resolved ${entry.version} to ${entry.hash ?? "no hash"}, ` +
            `expected ${expected}\n`
    );
    process.exit(1);
}

process.stdout.write(`verify-resolved-ref OK: ${PACKAGE_NAME} ${entry.version} resolved exactly ${expected}\n`);
