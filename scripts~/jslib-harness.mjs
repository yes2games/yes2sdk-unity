// Node harness for the Plugins/*.jslib bridges. Loads a .jslib in a node:vm sandbox the same way
// check-wrapper-parity.mjs does, then emulates the Emscripten pieces the bridges rely on:
//   - mergeInto / LibraryManager
//   - "$name" entries become globals visible to every library function (bare __y2h, __y2iap, ...)
//   - "$name__postset" strings are executed once after the library is loaded
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
 * @param {string[]} files jslib file names under Plugins/ (Yes2SDK.jslib is added automatically)
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
  };

  const library = {};
  sandbox.mergeInto = (_lib, additions) => Object.assign(library, additions);
  vm.createContext(sandbox);

  const order = [HELPERS, ...files.filter((f) => f !== HELPERS)];
  const postsets = [];
  for (const f of order) {
    vm.runInContext(readFileSync(join(pluginsDir, f), 'utf8'), sandbox, { filename: f });
  }
  const exported = {};
  for (const [key, value] of Object.entries(library)) {
    if (key.startsWith('$')) {
      if (key.endsWith('__postset')) postsets.push(value);
      else if (!isMeta(key)) sandbox[key.slice(1)] = value;
    } else if (!isMeta(key) && typeof value === 'function') {
      exported[key] = value;
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
