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
  // Fix #3: reset lastIndex before each file scan so no stale state leaks between inputs.
  moduleRe.lastIndex = 0;
  let m;
  while ((m = moduleRe.exec(src)) !== null) {
    const [, mod, method] = m;
    if (!required.has(mod)) required.set(mod, new Set());
    required.get(mod).add(method);
  }
}

const wrapperSrc = readFileSync(join(pluginsDir, WRAPPER), 'utf8');

// Fix #1: append (?!\w) so "ad" does not prefix-match "ads:" — the module name must end at a
// non-word boundary before the key separator.
const hasModule = (mod) =>
  new RegExp('(^|[^\\w.])' + mod + '(?!\\w)\\s*:\\s*\\{', 'm').test(wrapperSrc);

// Fix #2: scope the method search to the specific module's object-literal body so a method
// present in a *different* module does not satisfy the check for this one.
// Extract the module block by counting braces from the opening '{' of "<mod>: {".
const extractModuleBlock = (mod) => {
  // Use the same word-boundary pattern as hasModule to find the block start.
  const keyRe = new RegExp('(?:^|[^\\w.])' + mod + '(?!\\w)\\s*:\\s*\\{', 'm');
  const keyMatch = keyRe.exec(wrapperSrc);
  if (!keyMatch) return '';
  // Walk forward counting braces; the opening '{' is the last char of the match.
  const openPos = keyMatch.index + keyMatch[0].length - 1;
  let depth = 0;
  let end = openPos;
  for (let i = openPos; i < wrapperSrc.length; i++) {
    if (wrapperSrc[i] === '{') depth++;
    else if (wrapperSrc[i] === '}') { depth--; if (depth === 0) { end = i; break; } }
  }
  return wrapperSrc.slice(openPos, end + 1);
};
const hasMethod = (mod, method) => {
  const block = extractModuleBlock(mod);
  if (!block) return false;
  return new RegExp('(^|[^\\w.])' + method + '\\s*:\\s*function', 'm').test(block);
};

const missingModules = [];
const missingMethods = [];
for (const [mod, methods] of required) {
  if (!hasModule(mod)) { missingModules.push(mod); continue; }
  for (const method of methods) {
    if (!hasMethod(mod, method)) missingMethods.push(mod + '.' + method);
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
