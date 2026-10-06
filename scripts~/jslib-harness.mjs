// Node harness for the Plugins/*.jslib bridges. Loads a .jslib in a node:vm sandbox the same way
// check-wrapper-parity.mjs does, then emulates the Emscripten pieces the bridges rely on:
//   - mergeInto / LibraryManager
//   - "$name" entries become globals visible to every library function (bare __y2h, __y2iap, ...)
//   - "$name__postset" strings are executed once after the library is loaded
//   - "__deps" are enforced: a function that uses a "$helper" it does not list (directly or through
//     another helper's __deps) fails the sandbox build, as the helper would not be emitted by a real link
//   - library functions are re-created from their source text (as Emscripten does), so closures over
//     file-level variables of a .jslib do not survive here either
//   - host setTimeout/setInterval/clearTimeout/clearInterval are available to the bridges
//   - SendMessage (recorded), UTF8ToString (pointer only, throws on non-numbers), lengthBytesUTF8, stringToUTF8, _malloc
// Strings returned to C# via __y2h.returnStr are readable with sb.readStr(handle).
// Each createSandbox() call builds a fresh vm context, so tests never share state.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const pluginsDir = join(root, 'Plugins');
const HELPERS = 'Yes2SDK.jslib'; // owns $__y2h and $__y2, always loaded first

const isMeta = (key) => key.endsWith('__deps') || key.endsWith('__postset') || key.endsWith('__sig');

/**
 * @param {(string | { name: string, source: string })[]} files jslib file names under Plugins/
 *   (Yes2SDK.jslib is added automatically); { name, source } supplies inline jslib text (harness self-tests)
 * @param {{ Yes2SDK?: unknown }} [opts] fake window.Yes2SDK (omit for "SDK not loaded")
 */
export function createSandbox(files, opts = {}) {
  const sent = []; // [target, method, payload]
  const memory = new Map(); // handle -> string, backs _malloc / stringToUTF8
  let nextHandle = 1000;

  const window = { addEventListener() {} };
  if (opts.Yes2SDK !== undefined) window.Yes2SDK = opts.Yes2SDK;

  const logs = [];
  const sandbox = {
    window,
    navigator: { userAgent: '', language: 'en' },
    document: { referrer: '' },
    console: {
      log: (...a) => logs.push(['log', a]),
      warn: (...a) => logs.push(['warn', a]),
      error: (...a) => logs.push(['error', a]),
    },
    SendMessage: (target, method, payload) => sent.push([target, method, payload]),
    UTF8ToString: (v) => {
      if (typeof v !== 'number') throw new TypeError('UTF8ToString expects a pointer (number), got ' + typeof v);
      return memory.get(v) ?? '';
    },
    lengthBytesUTF8: (s) => Buffer.byteLength(s, 'utf8'),
    stringToUTF8: (s, ptr) => { memory.set(ptr, s); },
    _malloc: () => nextHandle++,
    LibraryManager: { library: {} },
    setTimeout, setInterval, clearTimeout, clearInterval,
  };

  const library = {};
  sandbox.mergeInto = (_lib, additions) => Object.assign(library, additions);
  vm.createContext(sandbox);

  const sources = [{ name: HELPERS }, ...files.filter((f) => f !== HELPERS)].map((f) =>
    typeof f === 'string' || !f.source ? { name: typeof f === 'string' ? f : f.name } : f);
  const postsets = [];
  for (const { name, source } of sources) {
    // function scope: file-level vars stay private to the file, as they would not survive a real build
    vm.runInContext('(function(){' + (source ?? readFileSync(join(pluginsDir, name), 'utf8')) + '\n})()', sandbox, { filename: name });
  }

  // Re-create functions from their source text, like Emscripten's stringification does.
  const recreate = (fn) => (typeof fn === 'function' ? vm.runInContext('(' + fn.toString() + ')', sandbox) : fn);
  const helperNames = Object.keys(library)
    .filter((k) => k.startsWith('$') && !isMeta(k))
    .map((k) => k.slice(1));
  const depsOf = (key) => (Array.isArray(library[key + '__deps']) ? library[key + '__deps'] : []);
  const closure = (key) => {
    const seen = new Set();
    const walk = (k) => {
      for (const d of depsOf(k)) {
        if (typeof d === 'string' && d.startsWith('$') && !seen.has(d.slice(1))) {
          seen.add(d.slice(1));
          walk(d);
        }
      }
    };
    walk(key);
    return seen;
  };
  const checkDeps = (key, fn) => {
    const allowed = closure(key);
    // string literals and comments are not uses: blank strings first so a // or /* inside one is inert
    const src = fn.toString()
      .replace(/'(?:\\.|[^'\\\n])*'|"(?:\\.|[^"\\\n])*"/g, '""')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/\/\/.*$/gm, '');
    for (const h of helperNames) {
      const used = new RegExp('(?<![.\\w$])' + h.replace(/\$/g, '\\$') + '(?![\\w$])').test(src);
      if (used && !allowed.has(h) && key !== '$' + h) {
        throw new Error(`jslib ${key} uses $${h} but does not list it in ${key}__deps (a real link would not emit it)`);
      }
    }
  };

  // A function helper is checked and re-created; an object helper has each function-valued member
  // checked against the helper's own __deps closure and re-created (recursively for nested objects).
  const prepareHelper = (key, value) => {
    if (typeof value === 'function') {
      checkDeps(key, value);
      return recreate(value);
    }
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      const out = {};
      for (const [member, v] of Object.entries(value)) out[member] = prepareHelper(key, v);
      return out;
    }
    return value;
  };

  const exported = {};
  for (const [key, value] of Object.entries(library)) {
    if (key.startsWith('$')) {
      if (key.endsWith('__postset')) postsets.push(value);
      else if (!isMeta(key)) {
        sandbox[key.slice(1)] = prepareHelper(key, value);
      }
    } else if (!isMeta(key) && typeof value === 'function') {
      checkDeps(key, value);
      exported[key] = recreate(value);
    }
  }
  for (const code of postsets) vm.runInContext(code, sandbox);

  return {
    window,
    sent,
    logs,
    /** Call an exported Yes2SDK_*JS function. */
    call: (name, ...args) => {
      if (typeof exported[name] !== 'function') throw new Error('jslib does not export ' + name);
      return exported[name](...args);
    },
    /** Read a string that a bridge returned through __y2h.returnStr. */
    readStr: (handle) => memory.get(handle),
    /** Make a C# string argument: allocate a handle in the harness memory and return the pointer. */
    str: (s) => { const ptr = nextHandle++; memory.set(ptr, s); return ptr; },
  };
}

/** Let pending promise callbacks (.then / .catch chains) run. */
export async function flush() {
  for (let i = 0; i < 5; i++) await new Promise((r) => setImmediate(r));
}
