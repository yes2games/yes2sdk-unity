#!/usr/bin/env node
// make-consumer.mjs - build a clean UPM consumer project against one package ref
// (yes2games/yes2sdk-unity#97 sections 8 and 10).
//
// The committed ci~/consumer resolves this checkout through a local file
// dependency, which is what the required lanes want: they test the tree in front
// of them. The edge and stable proofs want the opposite - a project with no
// Library, no lock file and no local path, resolving the package over the network
// exactly the way a consuming game does.
//
// Same Assets, same ProjectSettings, one substituted dependency. Anything else
// would be testing a different project than the one the lanes proved.
//
// Usage:
//   node ci~/make-consumer.mjs <dest> <upm-dependency-value>
//
// e.g. node ci~/make-consumer.mjs .edge-consumer \
//        https://github.com/yes2games/yes2sdk-unity.git#edge
//
// Exit codes: 0 written, 2 usage or I/O error.

import { cpSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const PACKAGE_NAME = "com.yes2games.yes2sdk";

const [dest, dependency] = process.argv.slice(2);
if (dest === undefined || dependency === undefined) {
    process.stderr.write("usage: node ci~/make-consumer.mjs <dest> <upm-dependency-value>\n");
    process.exit(2);
}

const template = join(dirname(fileURLToPath(import.meta.url)), "consumer");
const target = resolve(dest);

const manifest = JSON.parse(readFileSync(join(template, "Packages", "manifest.json"), "utf8"));
if (manifest.dependencies?.[PACKAGE_NAME] === undefined) {
    process.stderr.write(`make-consumer: ${template}/Packages/manifest.json does not depend on ${PACKAGE_NAME}\n`);
    process.exit(2);
}
manifest.dependencies[PACKAGE_NAME] = dependency;

rmSync(target, { recursive: true, force: true });
mkdirSync(join(target, "Packages"), { recursive: true });
cpSync(join(template, "Assets"), join(target, "Assets"), { recursive: true });
cpSync(join(template, "ProjectSettings"), join(target, "ProjectSettings"), { recursive: true });

// No packages-lock.json on purpose: a clean consumer must resolve the ref, not
// replay a resolution someone else recorded.
writeFileSync(join(target, "Packages", "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");

process.stdout.write(`make-consumer: wrote ${target} resolving ${PACKAGE_NAME} from ${dependency}\n`);
