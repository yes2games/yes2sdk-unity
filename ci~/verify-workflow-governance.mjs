#!/usr/bin/env node
// verify-workflow-governance.mjs - canonical required-CI identity governance
// (yes2games/yes2sdk-unity#97 section 4, yes2games/yes2dashboard#141 section 2).
//
// Guards one thing: that the canonical workflow name and the canonical final-gate
// job name each identify exactly one tracked object, in the canonical file, spelled
// exactly. A second workflow or job answering to the same name makes a reader, and
// any future required-check setting, unable to tell which one it is looking at.
//
// This is not an anti-attacker control. yes2games/yes2dashboard#141 section 14
// records that a pull request editing its own workflow files is explicitly not
// defended against here, because there is no Code Owner review to require. What
// this catches is drift and accidental duplication.
//
// Self-referential by design: this is a mandatory result of the workflow it
// governs, which is why it parses the tracked tree instead of trusting
// github.workflow.
//
// Usage:
//   node ci~/verify-workflow-governance.mjs              # verify this checkout
//   node ci~/verify-workflow-governance.mjs --root DIR   # verify a fixture tree
//
// Exit codes: 0 compliant, 1 governance violation, 2 usage or I/O error.
//
// No dependencies by design: this repository is a Unity package, its root
// package.json is the UPM manifest, and there is no npm install step to hang a
// YAML library off. The scanner below reads the narrow shape a workflow file
// actually has and refuses to guess at anything else.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const CANONICAL_WORKFLOW_NAME = "YES2 Unity Required CI Workflow";
const CANONICAL_GATE_NAME = "YES2 Unity Required CI";
const CANONICAL_WORKFLOW_FILE = ".github/workflows/required-ci.yml";

const failures = [];
const fail = (message) => failures.push(message);

function usage(message) {
    process.stderr.write(`verify-workflow-governance: ${message}\n`);
    process.stderr.write("usage: node ci~/verify-workflow-governance.mjs [--root DIR]\n");
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
            process.stdout.write("usage: node ci~/verify-workflow-governance.mjs [--root DIR]\n");
            process.exit(0);
        } else {
            usage(`unknown argument: ${argv[i]}`);
        }
    }
    return resolve(root);
}

/**
 * Tracked workflow files, from the Git index rather than the filesystem: an
 * untracked local workflow does not run in Actions, and a tracked one that was
 * deleted from the worktree still does on every other checkout.
 */
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

const KEY_LINE = /^(\s*)(?:"([^"]*)"|'([^']*)'|([\w.-]+))\s*:(?:[ \t]+(.*))?$/;

/** Strip a trailing `# comment` that is outside a quoted scalar. */
function stripComment(value) {
    let quote = null;
    for (let i = 0; i < value.length; i++) {
        const char = value[i];
        if (quote !== null) {
            if (char === quote) {
                quote = null;
            }
        } else if (char === '"' || char === "'") {
            quote = char;
        } else if (char === "#" && (i === 0 || /\s/.test(value[i - 1]))) {
            return value.slice(0, i);
        }
    }
    return value;
}

function scalar(raw) {
    const text = stripComment(raw ?? "").trim();
    if (text === "") {
        return null;
    }
    const quoted = /^"([^"]*)"$/.exec(text) ?? /^'([^']*)'$/.exec(text);
    return quoted === null ? text : quoted[1];
}

/**
 * Read the workflow display name and every job display name out of one workflow
 * file. Deliberately narrow: block mapping only, which is the only shape any
 * workflow in this repository or its siblings uses.
 *
 * Block scalars (`run: |`) are skipped wholesale, because a shell script is full
 * of lines that look like YAML keys and none of them are job names. A file whose
 * top level cannot be read at all is a hard error rather than a silent miss:
 * governance that quietly sees nothing is worse than governance that stops.
 */
function scanWorkflow(relative, text) {
    const lines = text.split(/\r?\n/);
    let workflowName = null;
    const jobNames = [];
    let sawTopLevelKey = false;

    let inJobs = false;
    let jobIdIndent = null;
    let jobBodyIndent = null;
    let currentJobId = null;
    let currentJobName = null;
    let blockScalarIndent = null;

    const closeJob = () => {
        if (currentJobId !== null) {
            jobNames.push(currentJobName ?? currentJobId);
            currentJobId = null;
            currentJobName = null;
        }
    };

    for (const line of lines) {
        if (line.trim() === "" || /^\s*#/.test(line)) {
            continue;
        }
        const indent = line.length - line.trimStart().length;

        if (blockScalarIndent !== null) {
            if (indent > blockScalarIndent) {
                continue;
            }
            blockScalarIndent = null;
        }

        const match = KEY_LINE.exec(line);
        if (match === null) {
            continue;
        }
        const key = match[2] ?? match[3] ?? match[4];
        const rawValue = match[5];
        const value = scalar(rawValue);

        if (/^[|>]/.test((rawValue ?? "").trim())) {
            blockScalarIndent = indent;
            continue;
        }

        if (indent === 0) {
            sawTopLevelKey = true;
            closeJob();
            inJobs = key === "jobs";
            jobIdIndent = null;
            jobBodyIndent = null;
            if (key === "name") {
                workflowName = value;
            }
            continue;
        }

        if (!inJobs) {
            continue;
        }

        if (jobIdIndent === null) {
            jobIdIndent = indent;
        }

        if (indent === jobIdIndent) {
            closeJob();
            currentJobId = key;
            jobBodyIndent = null;
            continue;
        }

        if (currentJobId === null) {
            continue;
        }
        if (jobBodyIndent === null) {
            jobBodyIndent = indent;
        }
        if (indent === jobBodyIndent && key === "name") {
            currentJobName = value;
        }
    }
    closeJob();

    if (!sawTopLevelKey) {
        usage(`cannot read any top-level key out of ${relative}; refusing to report it as clean`);
    }
    return { workflowName, jobNames };
}

/**
 * Collapse a display name to the form two names have to differ in for a human to
 * tell them apart at a glance. `Yes2 Unity Required CI` and `YES2  Unity Required
 * CI` are both spoofs of the canonical gate, not distinct identities.
 */
function identityKey(name) {
    return name.trim().replace(/\s+/g, " ").toLowerCase();
}

function collectIdentities(root) {
    const workflows = new Map();
    const gates = new Map();

    for (const relative of trackedWorkflows(root)) {
        const { workflowName, jobNames } = scanWorkflow(relative, readFileSync(join(root, relative), "utf8"));

        if (workflowName !== null && identityKey(workflowName) === identityKey(CANONICAL_WORKFLOW_NAME)) {
            workflows.set(relative, workflowName);
        }
        for (const jobName of jobNames) {
            if (identityKey(jobName) === identityKey(CANONICAL_GATE_NAME)) {
                const claimed = gates.get(relative) ?? [];
                claimed.push(jobName);
                gates.set(relative, claimed);
            }
        }
    }

    return { workflows, gates };
}

function checkUnique(claims, kind, canonical) {
    const total = [...claims.values()].reduce((sum, value) => sum + (Array.isArray(value) ? value.length : 1), 0);

    if (total === 0) {
        fail(`no tracked workflow declares the canonical ${kind} name "${canonical}"`);
        return;
    }
    if (total > 1) {
        const where = [...claims.entries()]
            .map(([file, value]) => `${file} (${Array.isArray(value) ? value.join(", ") : value})`)
            .join("; ")
            .replace(/"/g, "'");
        fail(
            `the canonical ${kind} name "${canonical}" is claimed ${total} times: ${where}. ` +
                "A duplicate or spoofed identity means neither a reader nor a required-check " +
                "setting can tell which object it names."
        );
        return;
    }

    const [file, value] = [...claims.entries()][0];
    if (file !== CANONICAL_WORKFLOW_FILE) {
        fail(`the canonical ${kind} name "${canonical}" is declared in ${file}, not ${CANONICAL_WORKFLOW_FILE}`);
    }
    const spelling = Array.isArray(value) ? value[0] : value;
    if (spelling !== canonical) {
        fail(`${file} spells the canonical ${kind} name "${spelling}"; it must be exactly "${canonical}"`);
    }
}

const root = parseArgs(process.argv.slice(2));

if (!trackedWorkflows(root).includes(CANONICAL_WORKFLOW_FILE)) {
    fail(`${CANONICAL_WORKFLOW_FILE} is not tracked`);
}
const { workflows, gates } = collectIdentities(root);
checkUnique(workflows, "workflow", CANONICAL_WORKFLOW_NAME);
checkUnique(gates, "final-gate job", CANONICAL_GATE_NAME);

if (failures.length > 0) {
    process.stderr.write(`workflow governance failed (${failures.length} problem(s)):\n`);
    for (const problem of failures) {
        process.stderr.write(`  - ${problem}\n`);
    }
    process.exit(1);
}

process.stdout.write(
    `workflow governance OK: "${CANONICAL_WORKFLOW_NAME}" and "${CANONICAL_GATE_NAME}" ` +
        `are each claimed exactly once, by ${CANONICAL_WORKFLOW_FILE}\n`
);
