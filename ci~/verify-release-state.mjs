#!/usr/bin/env node
// verify-release-state.mjs - the Unity release-state verifier
// (yes2games/yes2sdk-unity#97 sections 1, 2 and 9, yes2games/yes2dashboard#141
// sections 1, 8 and 13).
//
// package.json.version is the sole Unity SDK product SemVer authority. Everything
// else that carries a version - the Release Please manifest, the runtime literal,
// the README production tag, the CHANGELOG - is a mirror, and a mirror that drifts
// is how a release ships pointing at the wrong thing.
//
// Two of these checks exist because of a demonstrated failure rather than a
// preference:
//
//   - The CHANGELOG-section assertion is the detector for a version bump made
//     outside the Release Please PR. yes2sdk-core v2.7.1 is exactly that: the bump
//     rode inside a bugfix PR about debug banners and released silently
//     (yes2games/yes2dashboard#141 section 8). The stable job runs this script for
//     that assertion, so it fails loudly instead of closed and silent.
//   - The component assertion is the yes2infra PR #445 stuck state: `package-name`
//     set alongside `include-component-in-tag: false` makes a componentless release
//     branch unable to match a configured component, so Release Please declines to
//     tag an already-merged release PR and leaves the manifest advanced with no tag
//     behind it (yes2games/yes2dashboard#141 section 13).
//
// Usage:
//   node ci~/verify-release-state.mjs              # verify this checkout
//   node ci~/verify-release-state.mjs --root DIR   # verify a fixture tree
//
// Exit codes: 0 compliant, 1 policy violation, 2 usage or I/O error.
//
// No network, no prompts, no dependencies and no writes: it runs both as a
// mandatory pull-request result and as the stable job's own guard.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

// Canonical stable versions are strict plain X.Y.Z (#97 section 1). No prerelease
// suffix, no build metadata, no leading v.
const STRICT_SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;

// #97 section 1 names exactly these two safe mirrors and no others.
const RUNTIME_VERSION_FILE = "Runtime/Yes2SDK.cs";
const RUNTIME_VERSION_LINE = /Version\s*=>\s*"([^"]*)"/;

// The README integration and production lines (#97 section 11). The production one
// is a mirror Release Please rewrites; the integration one is fixed and must stay
// tag-only, because an edge GitHub Release or prerelease is a #97 non-goal.
const README_PRODUCTION = /github\.com\/yes2games\/yes2sdk-unity\.git#v(\d+\.\d+\.\d+)/g;
const README_INTEGRATION = "https://github.com/yes2games/yes2sdk-unity.git#edge";

// com.unity.test-framework is CI/test infrastructure, not a runtime dependency
// (#97 section 2). It belongs to the committed ci~/consumer harness, which owns it
// so that a consuming game does not inherit it.
const CI_ONLY_DEPENDENCIES = ["com.unity.test-framework"];

const failures = [];
const fail = (message) => failures.push(message);

function usage(message) {
    process.stderr.write(`verify-release-state: ${message}\n`);
    process.stderr.write("usage: node ci~/verify-release-state.mjs [--root DIR]\n");
    process.exit(2);
}

function parseArgs(argv) {
    let root = process.cwd();
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === "--root") {
            if (argv[i + 1] === undefined) {
                usage("--root needs a directory");
            }
            root = argv[++i];
        } else if (argv[i] === "-h" || argv[i] === "--help") {
            process.stdout.write("usage: node ci~/verify-release-state.mjs [--root DIR]\n");
            process.exit(0);
        } else {
            usage(`unknown argument: ${argv[i]}`);
        }
    }
    return resolve(root);
}

function readText(path, label) {
    try {
        return readFileSync(path, "utf8");
    } catch (error) {
        usage(`cannot read ${label} at ${path}: ${error.message}`);
    }
}

function readJson(path, label) {
    const raw = readText(path, label);
    try {
        return JSON.parse(raw);
    } catch (error) {
        usage(`${label} at ${path} is not valid JSON: ${error.message}`);
    }
}

function describe(value) {
    return value === undefined ? "absent" : JSON.stringify(value);
}

function trackedWorkflows(root) {
    let output;
    try {
        output = execFileSync("git", ["-C", root, "ls-files", "-z", "--", ".github/workflows"], {
            encoding: "utf8",
            stdio: ["ignore", "pipe", "pipe"],
        });
    } catch (error) {
        usage(`cannot list tracked workflows in ${root}: ${error.message.trim()}`);
    }
    return output.split("\0").filter((path) => /\.ya?ml$/i.test(path));
}

/**
 * Walk the Release Please config for a key that is set to true anywhere in it,
 * so a forbidden switch cannot hide inside a per-package override.
 */
function isEnabledAnywhere(node, key) {
    if (Array.isArray(node)) {
        return node.some((child) => isEnabledAnywhere(child, key));
    }
    if (node === null || typeof node !== "object") {
        return false;
    }
    if (node[key] === true) {
        return true;
    }
    return Object.values(node).some((child) => isEnabledAnywhere(child, key));
}

function checkPackage(root, pkg) {
    if (typeof pkg.version !== "string" || !STRICT_SEMVER.test(pkg.version)) {
        fail(`package.json.version must be a strict plain X.Y.Z (#97 section 1), found ${describe(pkg.version)}`);
    }
    for (const name of CI_ONLY_DEPENDENCIES) {
        if (pkg.dependencies !== undefined && Object.hasOwn(pkg.dependencies, name)) {
            fail(
                `package.json declares ${name} as a package dependency. #97 section 2 makes it ` +
                    "CI/test infrastructure owned by ci~/consumer, so a consuming game does not inherit it."
            );
        }
    }
}

function checkReleasePlease(root, version) {
    const manifest = readJson(join(root, ".release-please-manifest.json"), ".release-please-manifest.json");
    if (manifest["."] !== version) {
        fail(
            `.release-please-manifest.json "." must equal package.json.version ${describe(version)}, ` +
                `found ${describe(manifest["."])}`
        );
    }

    const config = readJson(join(root, "release-please-config.json"), "release-please-config.json");

    for (const forbidden of ["draft", "force-tag-creation"]) {
        if (isEnabledAnywhere(config, forbidden)) {
            fail(`release-please-config.json must not enable ${forbidden}: #97 section 1 forbids it`);
        }
    }

    const packages = config.packages;
    if (packages === null || typeof packages !== "object" || Array.isArray(packages)) {
        fail("release-please-config.json has no packages object");
        return;
    }

    const required = [
        ["release-type", "node"],
        ["include-component-in-tag", false],
        ["skip-github-release", true],
    ];

    for (const [path, entry] of Object.entries(packages)) {
        const settings = entry !== null && typeof entry === "object" && !Array.isArray(entry) ? entry : {};

        for (const [key, expected] of required) {
            const effective = settings[key] ?? config[key];
            if (effective !== expected) {
                fail(
                    `release-please-config.json package "${path}" resolves ${key} to ${describe(effective)}; ` +
                        `#97 section 1 requires ${JSON.stringify(expected)}`
                );
            }
        }

        if ((settings["include-component-in-tag"] ?? config["include-component-in-tag"]) === true) {
            continue;
        }
        for (const key of ["package-name", "component"]) {
            for (const [scope, source] of [
                ["release-please-config.json", config],
                [`package "${path}"`, settings],
            ]) {
                if (source[key] === undefined) {
                    continue;
                }
                fail(
                    `${scope} sets ${key} to ${describe(source[key])} while package "${path}" resolves ` +
                        "include-component-in-tag to false. The componentless release branch can never " +
                        "match a configured component, so Release Please declines to tag an already-merged " +
                        'release PR with "PR component: undefined does not match configured component" ' +
                        "and leaves the manifest advanced with no tag behind it (yes2infra PR #445)."
                );
            }
        }
    }
}

function checkMirrors(root, version) {
    const runtime = readText(join(root, RUNTIME_VERSION_FILE), RUNTIME_VERSION_FILE);
    const literal = RUNTIME_VERSION_LINE.exec(runtime);
    if (literal === null) {
        fail(`${RUNTIME_VERSION_FILE} has no Yes2SDK.Version string literal to mirror package.json.version into`);
    } else if (literal[1] !== version) {
        fail(
            `${RUNTIME_VERSION_FILE} mirrors version ${describe(literal[1])}, ` +
                `but package.json.version is ${describe(version)}`
        );
    }

    const readme = readText(join(root, "README.md"), "README.md");
    const production = [...readme.matchAll(README_PRODUCTION)].map((match) => match[1]);
    if (production.length === 0) {
        fail(`README.md carries no production UPM tag URL to mirror package.json.version into`);
    } else if (production.length > 1) {
        fail(
            `README.md carries ${production.length} production UPM tag URLs (${production.join(", ")}); ` +
                "#97 section 1 specifies exactly one safe mirror, and a second one Release Please does " +
                "not rewrite goes stale on the next release."
        );
    } else if (production[0] !== version) {
        fail(
            `README.md mirrors production tag v${production[0]}, but package.json.version is ${describe(version)}`
        );
    }

    if (!readme.includes(README_INTEGRATION)) {
        fail(
            `README.md does not document the integration channel ${README_INTEGRATION}. ` +
                "#97 section 8 makes edge a mutable Git tag and nothing else."
        );
    }
}

/**
 * The CHANGELOG section for the version being released. This is the whole point of
 * the check: Release Please writes the section in the same PR that writes the
 * version, so a version with no section is a version that was bumped somewhere
 * else.
 */
function checkChangelog(root, version) {
    const changelog = readText(join(root, "CHANGELOG.md"), "CHANGELOG.md");
    const section = new RegExp(`^##+\\s*\\[?${version.replace(/\./g, "\\.")}\\]?[\\s(]`, "m");
    if (!section.test(changelog)) {
        fail(
            `CHANGELOG.md has no section for ${version}. Release Please writes the version bump and its ` +
                "CHANGELOG section in the same pull request, so a version with no section was bumped " +
                "outside that pull request and would otherwise release silently against the wrong notes " +
                "(yes2games/yes2dashboard#141 section 8)."
        );
    }
}

/**
 * edge is a mutable Git tag and nothing else (#97 section 8). No tracked workflow
 * may create a GitHub prerelease, for edge or for anything else.
 */
function checkNoPrerelease(root) {
    for (const relative of trackedWorkflows(root)) {
        const body = readText(join(root, relative), relative);
        if (/(--prerelease\b|^\s*prerelease:\s*true\b)/m.test(body)) {
            fail(
                `tracked workflow ${relative} can create a prerelease. #97 section 8 makes edge a ` +
                    "mutable Git tag with no GitHub Release and no prerelease behind it."
            );
        }
    }
}

const root = parseArgs(process.argv.slice(2));
const pkg = readJson(join(root, "package.json"), "package.json");

checkPackage(root, pkg);
if (typeof pkg.version === "string") {
    checkReleasePlease(root, pkg.version);
    checkMirrors(root, pkg.version);
    checkChangelog(root, pkg.version);
}
checkNoPrerelease(root);

if (failures.length > 0) {
    process.stderr.write(`release state is non-compliant (${failures.length} problem(s)):\n`);
    for (const problem of failures) {
        process.stderr.write(`  - ${problem}\n`);
    }
    process.exit(1);
}

process.stdout.write(
    `release state OK: ${pkg.version} is mirrored by the Release Please manifest, ` +
        `${RUNTIME_VERSION_FILE}, the README production tag and a CHANGELOG section in ${root}\n`
);
