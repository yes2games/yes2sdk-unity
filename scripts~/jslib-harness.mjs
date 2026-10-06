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
//   - SendMessage (recorded), UTF8ToString, lengthBytesUTF8, stringToUTF8, _malloc
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
    UTF8ToString: (v) => (typeof v === 'number' ? memory.get(v) ?? '' : v),
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
    // comments are not uses (URLs inside strings keep their //)
    const src = fn.toString().replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:'"\\])\/\/.*$/gm, '$1');
    for (const h of helperNames) {
      const used = new RegExp('(?<![.\\w$])' + h.replace(/\$/g, '\\$') + '(?![\\w$])').test(src);
      if (used && !allowed.has(h) && key !== '$' + h) {
        throw new Error(`jslib ${key} uses $${h} but does not list it in ${key}__deps (a real link would not emit it)`);
      }
    }
  };

  const exported = {};
  for (const [key, value] of Object.entries(library)) {
    if (key.startsWith('$')) {
      if (key.endsWith('__postset')) postsets.push(value);
      else if (!isMeta(key)) {
        if (typeof value === 'function') checkDeps(key, value);
        sandbox[key.slice(1)] = recreate(value);
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
    /** Make a C# string argument: the harness UTF8ToString is identity on JS strings. */
    str: (s) => s,
  };
}

/** Let pending promise callbacks (.then / .catch chains) run. */
export async function flush() {
  for (let i = 0; i < 5; i++) await new Promise((r) => setImmediate(r));
}
