#!/usr/bin/env node
// Verifies the CrazyGames wrapper (Yes2SDKPlatformInit.jslib) defines every module AND every
// method that a C#->JS bridge calls into. Source of truth = the bridge files' window.Yes2SDK.*
// references, so the required surface cannot drift from the actual C# call sites.
//
// Why method-level matters: each bridge guards only with __y2h.has('<module>') (module present
// -> true), then dereferences the method directly. A wrapper module that is present but omits a
// method surfaces on CrazyGames as an uncaught TypeError (undefined deref) for the raw-call async
// bridges whose trailing .then().catch() runs after the throw. Tracked in yes2sdk-unity#73.
//
// How the wrapper is inspected: rather than regex/brace-walking the source (which false-positives
// on getters, shorthand, and string literals), we evaluate the .jslib in a vm sandbox with a
// stubbed CrazyGames global, run the postset init function, and introspect the resulting
// window.Yes2SDK object. This reports exact method presence with no parsing heuristics and no
// added dependency (node:vm is stdlib).
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const pluginsDir = join(root, 'Plugins');
const WRAPPER = 'Yes2SDKPlatformInit.jslib';

const bridges = readdirSync(pluginsDir).filter((f) => f.endsWith('.jslib') && f !== WRAPPER);

// Capture module.method accesses: window.Yes2SDK.<module>.<member>
// - module must start lowercase (excludes internals like ._sdk)
// - skip _-prefixed members (private fields the bridges never call)
const memberRe = /window\.Yes2SDK\.([a-z][a-zA-Z0-9]*)\.([a-zA-Z_$][a-zA-Z0-9_$]*)/g;
const required = new Map(); // module -> Set<method>
for (const f of bridges) {
  const src = readFileSync(join(pluginsDir, f), 'utf8');
  memberRe.lastIndex = 0;
  let m;
  while ((m = memberRe.exec(src)) !== null) {
    const [, mod, member] = m;
    if (member.startsWith('_')) continue;
    if (!required.has(mod)) required.set(mod, new Set());
    required.get(mod).add(member);
  }
}

// Build the wrapper's window.Yes2SDK object in a sandbox.
const wrapperSrc = readFileSync(join(pluginsDir, WRAPPER), 'utf8');
const library = {};
const sandbox = {
  window: { CrazyGames: { SDK: {} } },
  navigator: { userAgent: '', language: 'en' },
  document: { referrer: '' },
  // Silence the wrapper's __y2.log/warn chatter; surface only errors.
  console: { log() {}, warn() {}, error: console.error.bind(console) },
  mergeInto: (_lib, additions) => Object.assign(library, additions),
  LibraryManager: { library: {} },
};
vm.createContext(sandbox);
vm.runInContext(wrapperSrc, sandbox, { filename: WRAPPER });

if (typeof library.$__yes2PlatformInit !== 'function') {
  console.error('FAIL: ' + WRAPPER + ' did not register $__yes2PlatformInit via mergeInto.');
  process.exit(1);
}
library.$__yes2PlatformInit();
const api = sandbox.window.Yes2SDK;
if (!api) {
  console.error('FAIL: ' + WRAPPER + ' init did not create window.Yes2SDK (CG guard not met?).');
  process.exit(1);
}

const missing = []; // { module, method? }
let methodCount = 0;
for (const [mod, methods] of [...required].sort((a, b) => a[0].localeCompare(b[0]))) {
  const modObj = api[mod];
  if (!modObj || typeof modObj !== 'object') {
    missing.push({ module: mod });
    continue;
  }
  for (const method of [...methods].sort()) {
    methodCount++;
    if (typeof modObj[method] !== 'function') {
      missing.push({ module: mod, method });
    }
  }
}

if (missing.length) {
  console.error('FAIL: ' + WRAPPER + ' is missing surface referenced by bridges:');
  for (const x of missing) {
    console.error(x.method ? `  - ${x.module}.${x.method}` : `  - ${x.module} (entire module)`);
  }
  process.exit(1);
}

console.log(
  'OK: wrapper defines all ' + methodCount + ' bridge-referenced methods across ' +
  required.size + ' modules.'
);
