#!/usr/bin/env node
// Structural gate for the optimizer check registry.
// 1. Every check class declares the members the window renders.
// 2. Every DocsAnchor resolves to a heading on the published optimization page.
// Same idiom as check-wrapper-parity.mjs: no Unity, no test framework, plain node.

import { readdirSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const CHECKS_DIR = join(HERE, '..', 'Editor', 'Optimizer', 'Checks');
const DOCS_RAW_URL = 'https://developer.yes2games.com/docs/raw/unity-optimization.md';
const RELEASE = process.argv.includes('--release');

const REQUIRED_MEMBERS = ['Id', 'DocsAnchor', 'Title', 'Category', 'CanFix'];

function readChecks() {
  return readdirSync(CHECKS_DIR)
    .filter((f) => f.endsWith('.cs'))
    .map((file) => {
      const source = readFileSync(join(CHECKS_DIR, file), 'utf8');
      const value = (member) => {
        const m = source.match(new RegExp(`public\\s+\\S+\\s+${member}\\s*=>\\s*([^;]+);`));
        return m ? m[1].trim().replace(/^"|"$/g, '') : null;
      };
      return { file, members: Object.fromEntries(REQUIRED_MEMBERS.map((m) => [m, value(m)])) };
    });
}

function slugify(heading) {
  return heading
    .toLowerCase()
    .replace(/[^a-z0-9 -]/g, '')
    .trim()
    .replace(/\s+/g, '-');
}

async function readPageAnchors() {
  let res;
  try {
    res = await fetch(DOCS_RAW_URL);
  } catch {
    // Unreachable is unreachable, whether the host answers with an error status or not at all. A DNS
    // or network failure has to take the same soft-pass path, or one blip fails every pull request.
    return null;
  }

  if (!res.ok) return null;
  const md = await res.text();
  const anchors = new Set();
  for (const line of md.split('\n')) {
    const heading = line.match(/^#{1,6}\s+(.*)$/);
    if (heading) anchors.add(slugify(heading[1]));
    const explicit = line.match(/<a\s+id="([^"]+)"/);
    if (explicit) anchors.add(explicit[1]);
  }
  return anchors;
}

const checks = readChecks();
const anchors = await readPageAnchors();
let failed = false;

if (checks.length === 0) {
  console.error('No check classes found in Editor/Optimizer/Checks');
  failed = true;
}

for (const { file, members } of checks) {
  const missing = REQUIRED_MEMBERS.filter((m) => !members[m]);
  if (missing.length > 0) {
    console.error(`${file}: missing ${missing.join(', ')}`);
    failed = true;
    continue;
  }

  if (anchors === null) {
    console.log(`${members.Id} -> #${members.DocsAnchor} (page not published yet)`);
    continue;
  }

  if (anchors.has(members.DocsAnchor)) {
    console.log(`${members.Id} -> #${members.DocsAnchor} ok`);
  } else {
    console.error(`${file}: anchor '#${members.DocsAnchor}' is not on the published page`);
    failed = true;
  }
}

if (anchors === null) {
  const message = `Could not read ${DOCS_RAW_URL}`;
  if (RELEASE) {
    console.error(`${message}. The docs page must be live before tagging a release.`);
    failed = true;
  } else {
    console.warn(`${message}. Skipping anchor resolution.`);
  }
}

process.exitCode = failed ? 1 : 0;
