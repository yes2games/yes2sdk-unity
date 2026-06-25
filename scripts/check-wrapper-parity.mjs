#!/usr/bin/env node
// Verifies the CrazyGames wrapper (Yes2SDKPlatformInit.jslib) defines every module that a
// C#->JS bridge calls into. Source of truth = the bridge files' window.Yes2SDK.<module> refs,
// so the required surface cannot drift from the actual C# call sites.
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const pluginsDir = join(root, 'Plugins');
const WRAPPER = 'Yes2SDKPlatformInit.jslib';

const bridges = readdirSync(pluginsDir).filter((f) => f.endsWith('.jslib') && f !== WRAPPER);

// Capture two-level accesses: window.Yes2SDK.<module>.<method>(  — module must start lowercase
// (excludes internals like ._sdk and one-level calls like .on() / .initializeAsync()).
const moduleRe = /window\.Yes2SDK\.([a-z][a-zA-Z0-9]*)\.([a-zA-Z0-9]+)\s*\(/g;
const required = new Map(); // module -> Set(methods)
for (const f of bridges) {
  const src = readFileSync(join(pluginsDir, f), 'utf8');
  let m;
  while ((m = moduleRe.exec(src)) !== null) {
    const [, mod, method] = m;
    if (!required.has(mod)) required.set(mod, new Set());
    required.get(mod).add(method);
  }
}

const wrapperSrc = readFileSync(join(pluginsDir, WRAPPER), 'utf8');
const hasModule = (mod) => new RegExp('(^|[^\\w.])' + mod + '\\s*:\\s*\\{', 'm').test(wrapperSrc);
const hasMethod = (method) => new RegExp('(^|[^\\w.])' + method + '\\s*:\\s*function', 'm').test(wrapperSrc);

const missingModules = [];
const missingMethods = [];
for (const [mod, methods] of required) {
  if (!hasModule(mod)) { missingModules.push(mod); continue; }
  for (const method of methods) {
    if (!hasMethod(method)) missingMethods.push(mod + '.' + method);
  }
}

if (missingModules.length) {
  console.error('FAIL: ' + WRAPPER + ' is missing modules referenced by bridges:');
  for (const mod of missingModules.sort()) console.error('  - ' + mod);
}
if (missingMethods.length) {
  console.warn('WARN (heuristic, non-gating): wrapper may be missing methods:');
  for (const mm of missingMethods.sort()) console.warn('  - ' + mm);
}
if (!missingModules.length && !missingMethods.length) {
  console.log('OK: wrapper defines all ' + required.size + ' bridge-referenced modules.');
} else if (!missingModules.length) {
  console.log('OK (modules): all ' + required.size + ' bridge modules present; see method warnings.');
}

process.exit(missingModules.length ? 1 : 0);
