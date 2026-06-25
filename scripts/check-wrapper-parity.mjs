#!/usr/bin/env node
// Verifies the CrazyGames wrapper (Yes2SDKPlatformInit.jslib) defines every MODULE that a
// C#->JS bridge calls into. Source of truth = the bridge files' window.Yes2SDK.<module> refs,
// so the required module surface cannot drift from the actual C# call sites.
//
// SCOPE: module presence only. This check does NOT verify method-level parity — a wrapper
// module can be present yet omit individual methods the bridges call, which on CrazyGames
// surfaces as an uncaught TypeError (undefined deref) for un-try/caught raw calls. Method-level
// parity is tracked separately in yes2sdk-unity#73. A reliable method check needs a real JS
// parser (regex/brace-walking false-positives on getters, shorthand, and string literals), so
// it is intentionally left out here rather than shipped as a misleading non-gating warning.
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const pluginsDir = join(root, 'Plugins');
const WRAPPER = 'Yes2SDKPlatformInit.jslib';

const bridges = readdirSync(pluginsDir).filter((f) => f.endsWith('.jslib') && f !== WRAPPER);

// Capture module accesses: window.Yes2SDK.<module>.  — module must start lowercase
// (excludes internals like ._sdk and one-level calls like .on() / .initializeAsync()).
const moduleRe = /window\.Yes2SDK\.([a-z][a-zA-Z0-9]*)\./g;
const required = new Set();
for (const f of bridges) {
  const src = readFileSync(join(pluginsDir, f), 'utf8');
  moduleRe.lastIndex = 0;
  let m;
  while ((m = moduleRe.exec(src)) !== null) required.add(m[1]);
}

const wrapperSrc = readFileSync(join(pluginsDir, WRAPPER), 'utf8');

// Append (?!\w) so "ad" does not prefix-match "ads:" — the module name must end at a
// non-word boundary before the key separator.
const hasModule = (mod) =>
  new RegExp('(^|[^\\w.])' + mod + '(?!\\w)\\s*:\\s*\\{', 'm').test(wrapperSrc);

const missingModules = [...required].filter((mod) => !hasModule(mod)).sort();

if (missingModules.length) {
  console.error('FAIL: ' + WRAPPER + ' is missing modules referenced by bridges:');
  for (const mod of missingModules) console.error('  - ' + mod);
  process.exit(1);
}

console.log('OK: wrapper defines all ' + required.size + ' bridge-referenced modules.');
