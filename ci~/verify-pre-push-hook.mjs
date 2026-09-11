#!/usr/bin/env node
// verify-pre-push-hook.mjs - exercise .githooks/pre-push in both checkout shapes
// (yes2games/yes2sdk-unity#97 section 6, yes2games/yes2dashboard#141 section 3).
//
// A guard nobody has seen fail is not a guard, and this one failed in a way no
// casual test would catch. The hook derives the protected branch from git rather
// than hardcoding it, reading refs/remotes/origin/HEAD first. `git clone` writes
// that ref; actions/checkout does not. Under `set -Eeuo pipefail` an assignment
// inherits the exit status of its command substitution, so in a CI checkout the
// failing lookup killed the hook before its own fallback could run - and a hook
// that exits non-zero refuses the push. It therefore refused every push,
// including feature branches and tags, which inverts a guard the contract
// specifies as advisory into a hard blocker on all work. Same defect, same fix as
// yes2games/yes2sdk-core#59.
//
// The shape is the whole point: a fixture that creates refs/remotes/origin/HEAD
// passes against the broken hook. Both shapes are built here for that reason.
//
// Usage: node ci~/verify-pre-push-hook.mjs
//
// Exit codes: 0 the guard behaves, 1 it does not, 2 usage or I/O error.

import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HOOK = resolve(dirname(fileURLToPath(import.meta.url)), "..", ".githooks", "pre-push");

const git = (cwd, ...args) =>
    execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

/**
 * "ci" mirrors actions/checkout: a working tree wired to a remote with no
 * refs/remotes/origin/HEAD. "cloned" mirrors a developer's `git clone`, which
 * writes it.
 */
function makeCheckout(shape, defaultBranch) {
    const root = mkdtempSync(join(tmpdir(), "pre-push-"));
    const origin = join(root, "origin.git");

    execFileSync("git", ["init", "--quiet", "--bare", origin]);
    git(origin, "symbolic-ref", "HEAD", `refs/heads/${defaultBranch}`);

    const seed = join(root, "seed");
    mkdirSync(seed);
    git(seed, "init", "--quiet");
    git(seed, "config", "user.email", "ci@example.invalid");
    git(seed, "config", "user.name", "ci");
    writeFileSync(join(seed, "a.txt"), "seed\n");
    git(seed, "add", "a.txt");
    git(seed, "commit", "--quiet", "-m", "seed");
    git(seed, "remote", "add", "origin", origin);
    git(seed, "push", "--quiet", "origin", `HEAD:${defaultBranch}`);

    if (shape === "cloned") {
        const work = join(root, "clone");
        execFileSync("git", ["clone", "--quiet", origin, work]);
        return { root, work };
    }
    // The seed repo has no refs/remotes/origin/HEAD, which is the CI shape.
    return { root, work: seed };
}

/** Feed the hook one ref update on stdin, the way git does. */
function runHook(work, remoteRef) {
    const result = spawnSync("bash", [HOOK, "origin", "git@example.invalid:o/r.git"], {
        cwd: work,
        input: `refs/heads/local 1111111111111111111111111111111111111111 ${remoteRef} 0000000000000000000000000000000000000000\n`,
        encoding: "utf8",
    });
    return { status: result.status, stderr: (result.stderr ?? "").trim() };
}

const failures = [];
const cases = [
    { ref: "refs/heads/main", expect: 1, what: "a push to the default branch is refused" },
    { ref: "refs/heads/feature/x", expect: 0, what: "a push to a feature branch is allowed" },
    { ref: "refs/tags/v1.2.3", expect: 0, what: "a tag push is allowed" },
    { ref: "refs/tags/edge", expect: 0, what: "the edge tag push the release job makes is allowed" },
];

for (const shape of ["ci", "cloned"]) {
    const { root, work } = makeCheckout(shape, "main");
    try {
        for (const { ref, expect, what } of cases) {
            const { status, stderr } = runHook(work, ref);
            const ok = status === expect;
            process.stdout.write(`${ok ? "ok  " : "FAIL"} [${shape}] ${what} (exit ${status})\n`);
            if (!ok) {
                failures.push(`[${shape}] ${what}: expected exit ${expect}, got ${status}${stderr ? ` - ${stderr}` : ""}`);
            }
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

if (failures.length > 0) {
    process.stderr.write(`pre-push hook is not behaving as an advisory guard (${failures.length} problem(s)):\n`);
    for (const problem of failures) {
        process.stderr.write(`  - ${problem}\n`);
    }
    process.exit(1);
}

process.stdout.write("pre-push hook OK: refuses only the default branch, in both checkout shapes\n");
