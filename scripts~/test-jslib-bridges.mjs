#!/usr/bin/env node
// Behaviour tests for the C#->JS bridges in Plugins/*.jslib, run in plain node (no Unity needed).
//
// Usage:   node 'scripts~/test-jslib-bridges.mjs'
// Exit:    0 when every case passes, 1 on any failure. One compact line per case.
// Adding a case: call test('<group> name', async () => { ... }) below. Use
//   const sb = createSandbox(['Yes2SDKFoo.jslib'], { Yes2SDK: { foo: { ... } } });
//   sb.call('Yes2SDK_Foo_BarJS', ...args); await flush();
//   assert.deepStrictEqual(sb.sent, [['Bridge', 'OnFoo', '<payload>']]);
// and sb.readStr(handle) to read strings returned through __y2h.returnStr.
// Not wired into CI (local and reviewer tool).
import assert from 'node:assert/strict';
import { readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createSandbox, flush } from './jslib-harness.mjs';

const tests = [];
const test = (name, fn) => tests.push({ name, fn });

// ---- Yes2SDK.jslib: returnStr getters ------------------------------------------------------
test('core GetPlatformJS lower-cases the platform and returns it via returnStr', async () => {
  const sb = createSandbox([], { Yes2SDK: { getPlatform: () => 'CrazyGames' } });
  assert.equal(sb.readStr(sb.call('Yes2SDK_GetPlatformJS')), 'crazygames');
});

test('core GetPlatformJS returns "unknown" when the SDK is absent', async () => {
  const sb = createSandbox([]);
  assert.equal(sb.readStr(sb.call('Yes2SDK_GetPlatformJS')), 'unknown');
});

// ---- IAP: request-id envelope ("<id>|<payload>" to Bridge) ---------------------------------
test('iap GetCatalogAsync success sends the envelope', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { getCatalogAsync: () => Promise.resolve([{ id: 'gem' }]) } },
  });
  sb.call('Yes2SDK_IAP_GetCatalogAsyncJS', 7);
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnGetCatalogSuccess', '7|[{"id":"gem"}]']]);
});

test('iap PurchaseAsync rejection sends the error envelope with the platform code', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { purchaseAsync: () => Promise.reject({ code: 'USER_INPUT', message: 'nope' }) } },
  });
  sb.call('Yes2SDK_IAP_PurchaseAsyncJS', 3, sb.str('gem'), sb.str(''));
  await flush();
  assert.equal(sb.sent.length, 1);
  const [target, method, payload] = sb.sent[0];
  assert.deepStrictEqual([target, method], ['Bridge', 'OnPurchaseError']);
  assert.ok(payload.startsWith('3|'));
  assert.deepStrictEqual(JSON.parse(payload.slice(payload.indexOf('|') + 1)), {
    code: 'USER_INPUT', message: 'nope', context: 'Yes2SDK.IAP.PurchaseAsync',
  });
});

test('iap call without the module reports NotInitialized', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], { Yes2SDK: {} });
  sb.call('Yes2SDK_IAP_GetPurchasesAsyncJS', 9);
  const [, method, payload] = sb.sent[0];
  assert.equal(method, 'OnGetPurchasesError');
  assert.equal(JSON.parse(payload.slice(payload.indexOf('|') + 1)).code, 'NotInitialized');
});

// ---- Data: request-id envelope and string getter -------------------------------------------
test('data SetStringAsync success sends "<id>|true"', async () => {
  const sb = createSandbox(['Yes2SDKData.jslib'], {
    Yes2SDK: { data: { setStringAsync: () => Promise.resolve(true) } },
  });
  sb.call('Yes2SDK_Data_SetStringAsyncJS', 5, sb.str('k'), sb.str('v'));
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnDataSetStringSuccess', '5|true']]);
});

test('data FlushAsync rejection sends the error envelope', async () => {
  const sb = createSandbox(['Yes2SDKData.jslib'], {
    Yes2SDK: { data: { flushAsync: () => Promise.reject(new Error('boom')) } },
  });
  sb.call('Yes2SDK_Data_FlushAsyncJS', 11);
  await flush();
  const [, method, payload] = sb.sent[0];
  assert.equal(method, 'OnDataFlushError');
  assert.deepStrictEqual(JSON.parse(payload.slice(payload.indexOf('|') + 1)), {
    code: 'Unknown', message: 'boom', context: 'Yes2SDK.Data.FlushAsync',
  });
});

test('data GetString returns the stored value through returnStr', async () => {
  const sb = createSandbox(['Yes2SDKData.jslib'], {
    Yes2SDK: { data: { getString: (k, d) => (k === 'name' ? 'zoë' : d) } },
  });
  assert.equal(sb.readStr(sb.call('Yes2SDK_Data_GetStringJS', sb.str('name'), sb.str('def'))), 'zoë');
  assert.equal(sb.readStr(sb.call('Yes2SDK_Data_GetStringJS', sb.str('x'), sb.str('def'))), 'def');
});

// ---- harness self-checks -------------------------------------------------------------------
const fakeLib = (deps) => ({
  name: 'Fake.jslib',
  source: `mergeInto(LibraryManager.library, {
    Fake_PingJS: function() { __y2h.has('data'); },
    ${deps}
  });`,
});

test('harness fails a bridge that uses a $helper missing from its __deps', async () => {
  assert.throws(() => createSandbox([fakeLib('')]), /Fake_PingJS uses \$__y2h/);
});

test('harness accepts a bridge that lists its $helper in __deps', async () => {
  createSandbox([fakeLib("Fake_PingJS__deps: ['$__y2h'],")]);
});

const fakeObjectHelper = (deps) => ({
  name: 'FakeObj.jslib',
  source: `mergeInto(LibraryManager.library, {
    $__y2foo: { a: function() { return __y2h.has('data'); } },
    ${deps}
  });`,
});

test('harness fails an object-valued $helper whose method uses a $helper missing from its __deps', async () => {
  assert.throws(() => createSandbox([fakeObjectHelper('')]), /\$__y2foo uses \$__y2h/);
});

test('harness accepts an object-valued $helper that lists its $helper in __deps', async () => {
  createSandbox([fakeObjectHelper("$__y2foo__deps: ['$__y2h'],")]);
});

test('harness ignores helper names inside string literals and comments', async () => {
  createSandbox([{ name: 'Fake.jslib', source: `mergeInto(LibraryManager.library, {
    Fake_OkJS: function() { var u = 'http://x/__y2h'; /* __y2h */ return u; } // __y2h
  });` }]);
});

test('harness rejects a use hidden after a string holding comment markers', async () => {
  assert.throws(() => createSandbox([{ name: 'Fake.jslib', source: `mergeInto(LibraryManager.library, {
    Fake_PingJS: function() { var a = '/*'; __y2h.has('a'); var b = '*/'; }
  });` }]), /Fake_PingJS uses \$__y2h/);
});

test('every Plugins/*.jslib loads under the strict __deps check', async () => {
  const all = readdirSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'Plugins')).filter((f) => f.endsWith('.jslib'));
  assert.ok(all.length > 1);
  createSandbox(all);
});

test('harness exposes timers and drops closures over file-level vars', async () => {
  const sb = createSandbox([{ name: 'Fake.jslib', source: `var hidden = 1;
    mergeInto(LibraryManager.library, {
      Fake_TimerJS: function() { return typeof setTimeout + typeof clearInterval; },
      Fake_ClosureJS: function() { return typeof hidden; }
    });` }]);
  assert.equal(sb.call('Fake_TimerJS'), 'functionfunction');
  assert.equal(sb.call('Fake_ClosureJS'), 'undefined');
});

// ---- Session: entry point data, traffic source and session data as JSON --------------------
const throwing = () => { throw new Error('boom'); };
const jsonCases = [
  ['object', () => ({ a: 1 }), '{"a":1}'],
  ['string', () => '{"a":1}', '{"a":1}'],
  ['undefined', () => undefined, null],
  ['throwing', throwing, null],
];
for (const [label, getter, expected] of jsonCases) {
  test(`session GetEntryPointData with ${label} result`, async () => {
    const sb = createSandbox(['Yes2SDKSession.jslib'], { Yes2SDK: { session: { getEntryPointData: getter } } });
    assert.equal(sb.readStr(sb.call('Yes2SDK_GetEntryPointDataJS')), expected === null ? '{}' : expected);
  });
  test(`session GetTrafficSource with ${label} result`, async () => {
    const sb = createSandbox(['Yes2SDKSession.jslib'], { Yes2SDK: { session: { getTrafficSource: getter } } });
    assert.equal(sb.readStr(sb.call('Yes2SDK_GetTrafficSourceJS')), expected === null ? '{"referrer":"","params":{}}' : expected);
  });
}

test('session SetSessionData prefers setSessionDataFromJson and passes the raw JSON string', async () => {
  const calls = [];
  const sb = createSandbox(['Yes2SDKSession.jslib'], { Yes2SDK: { session: {
    setSessionDataFromJson: (j) => calls.push(['fromJson', j]),
    setSessionData: (j) => calls.push(['plain', j]),
  } } });
  sb.call('Yes2SDK_SetSessionDataJS', sb.str('{"k":2}'));
  assert.deepStrictEqual(calls, [['fromJson', '{"k":2}']]);
});

test('session SetSessionData falls back to setSessionData when the alias is absent', async () => {
  const calls = [];
  const sb = createSandbox(['Yes2SDKSession.jslib'], { Yes2SDK: { session: { setSessionData: (j) => calls.push(j) } } });
  sb.call('Yes2SDK_SetSessionDataJS', sb.str('{"k":2}'));
  assert.deepStrictEqual(calls, ['{"k":2}']);
});

test('session SetSessionData never throws when Core throws', async () => {
  const sb = createSandbox(['Yes2SDKSession.jslib'], { Yes2SDK: { session: { setSessionDataFromJson: throwing } } });
  sb.call('Yes2SDK_SetSessionDataJS', sb.str('{}'));
});

test('session wrapper setSessionDataFromJson parses a string and warns on bad JSON without throwing', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn: (...a) => warns.push(a), error() {} };
  const warns = [];
  sb.window.__yes2PlatformInit();
  const session = sb.window.Yes2SDK && sb.window.Yes2SDK.session;
  assert.ok(session, 'wrapper session module created');
  session.setSessionDataFromJson('{"k":2}');
  assert.equal(JSON.stringify(session._data), '{"k":2}');
  session.setSessionDataFromJson('{bad');
  assert.equal(warns.length, 1);
  assert.equal(JSON.stringify(session._data), '{"k":2}');
});

test('harness UTF8ToString throws on a raw string so a missing UTF8ToString cannot pass', async () => {
  const sb = createSandbox([{ name: 'Fake.jslib', source: `mergeInto(LibraryManager.library, {
    Fake_ForgetJS: function(p) { return p; },
    Fake_UseJS: function(p) { return UTF8ToString(p); }
  });` }]);
  assert.equal(sb.call('Fake_UseJS', sb.str('hi')), 'hi');
  assert.throws(() => sb.call('Fake_UseJS', 'hi'), /pointer/);
});

// ---- Lifecycle: exitRequested event (task: OnExitRequested) -------------------------------
test('lifecycle exitRequested sends OnExitRequested to Bridge after init', async () => {
  const handlers = {};
  const sb = createSandbox([], { Yes2SDK: {
    initializeAsync: () => Promise.resolve(),
    on: (name, fn) => { handlers[name] = fn; },
  } });
  sb.call('Yes2SDK_InitializeJS');
  await flush();
  assert.equal(typeof handlers.exitRequested, 'function', 'exitRequested is subscribed through on()');
  sb.sent.length = 0;
  handlers.exitRequested();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnExitRequested', '']]);
});
// ---- end Lifecycle: exitRequested ----------------------------------------------------------

// ---- Auth: registration prompt handle and isAuthenticated (task: registration prompt) ------
const regAuth = (overrides) => ({ auth: Object.assign({ isSupported: () => true }, overrides) });
const regOpts = (sb, obj) => sb.str(obj === undefined ? '' : JSON.stringify(obj));

test('auth ShowRegistrationPrompt success returns "" and stores the handle', async () => {
  let received = null;
  const handle = { login() {}, close() {} };
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: (o) => { received = o; return handle; },
  }) });
  const res = sb.readStr(sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 4,
    regOpts(sb, { theme: 'light', data: { level: 3 }, message: 'Hi {{registrationCode}}' })));
  assert.equal(res, '');
  assert.equal(received.theme, 'light');
  assert.deepStrictEqual(JSON.parse(JSON.stringify(received.data)), { level: 3 });
  assert.equal(received.message, 'Hi {{registrationCode}}');
  assert.equal(typeof received.onClose, 'function');
  assert.deepStrictEqual(sb.sent, []);
});

test('auth ShowRegistrationPrompt passes only the options that are set', async () => {
  let received = null;
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: (o) => { received = o; return { login() {}, close() {} }; },
  }) });
  assert.equal(sb.readStr(sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, regOpts(sb, {}))), '');
  assert.deepStrictEqual(Object.keys(received), ['onClose']);
  assert.equal(sb.readStr(sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 2, regOpts(sb))), '');
  assert.deepStrictEqual(Object.keys(received), ['onClose']);
});

test('auth ShowRegistrationPrompt returns error JSON when Core throws', async () => {
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: () => { throw { code: 'INVALID_OPERATION', message: 'already registered', context: 'core' }; },
  }) });
  const res = sb.readStr(sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 3, regOpts(sb, {})));
  assert.deepStrictEqual(JSON.parse(res), {
    code: 'INVALID_OPERATION', message: 'already registered', context: 'Yes2SDK.Auth.ShowRegistrationPrompt',
  });
  assert.deepStrictEqual(sb.sent, []);
});

test('auth ShowRegistrationPrompt returns error JSON for a plain Error, bad options JSON, no SDK, no method', async () => {
  const boom = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({ showRegistrationPrompt: () => { throw new Error('boom'); } }) });
  assert.deepStrictEqual(JSON.parse(boom.readStr(boom.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, regOpts(boom, {})))),
    { code: 'Unknown', message: 'boom', context: 'Yes2SDK.Auth.ShowRegistrationPrompt' });

  const badJson = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({ showRegistrationPrompt: () => ({}) }) });
  assert.equal(JSON.parse(badJson.readStr(badJson.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, badJson.str('{bad')))).code, 'Unknown');

  const noSdk = createSandbox(['Yes2SDKAuth.jslib']);
  assert.equal(JSON.parse(noSdk.readStr(noSdk.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, regOpts(noSdk, {})))).code, 'NotInitialized');

  const noMethod = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({}) });
  assert.equal(JSON.parse(noMethod.readStr(noMethod.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, regOpts(noMethod, {})))).code, 'FeatureNotSupported');
});

test('auth platform onClose sends OnRegistrationPromptClose with the prompt id, once', async () => {
  let onClose = null;
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: (o) => { onClose = o.onClose; return { login() {}, close() {} }; },
  }) });
  sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 12, regOpts(sb, {}));
  onClose();
  onClose();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnRegistrationPromptClose', '12']]);
});

test('auth prompt login and close reach the stored handle for that id only', async () => {
  const calls = [];
  let n = 0;
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: () => {
      const id = ++n;
      return { login: () => calls.push('login' + id), close: () => calls.push('close' + id) };
    },
  }) });
  sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 7, regOpts(sb, {}));
  sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 8, regOpts(sb, {}));
  sb.call('Yes2SDK_Auth_RegistrationPromptLoginJS', 8);
  sb.call('Yes2SDK_Auth_RegistrationPromptCloseJS', 7);
  assert.deepStrictEqual(calls, ['login2', 'close1']);
});

test('auth prompt login and close on an unknown id or a throwing handle never throw', async () => {
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: () => ({ login: throwing, close: throwing }),
  }) });
  sb.call('Yes2SDK_Auth_RegistrationPromptLoginJS', 99);
  sb.call('Yes2SDK_Auth_RegistrationPromptCloseJS', 99);
  sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 1, regOpts(sb, {}));
  sb.call('Yes2SDK_Auth_RegistrationPromptLoginJS', 1);
  sb.call('Yes2SDK_Auth_RegistrationPromptCloseJS', 1);
  const noSdk = createSandbox(['Yes2SDKAuth.jslib']);
  noSdk.call('Yes2SDK_Auth_RegistrationPromptLoginJS', 1);
  noSdk.call('Yes2SDK_Auth_RegistrationPromptCloseJS', 1);
});

test('auth prompt close drops the stored handle', async () => {
  const calls = [];
  const sb = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({
    showRegistrationPrompt: () => ({ login: () => calls.push('login'), close: () => calls.push('close') }),
  }) });
  sb.call('Yes2SDK_Auth_ShowRegistrationPromptJS', 5, regOpts(sb, {}));
  sb.call('Yes2SDK_Auth_RegistrationPromptCloseJS', 5);
  sb.call('Yes2SDK_Auth_RegistrationPromptLoginJS', 5);
  assert.deepStrictEqual(calls, ['close']);
});

test('auth IsAuthenticated returns 1/0 and 0 on throw, missing method or no SDK', async () => {
  const yes = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({ isAuthenticated: () => true }) });
  assert.equal(yes.call('Yes2SDK_Auth_IsAuthenticatedJS'), 1);
  const no = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({ isAuthenticated: () => false }) });
  assert.equal(no.call('Yes2SDK_Auth_IsAuthenticatedJS'), 0);
  const boom = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({ isAuthenticated: throwing }) });
  assert.equal(boom.call('Yes2SDK_Auth_IsAuthenticatedJS'), 0);
  const missing = createSandbox(['Yes2SDKAuth.jslib'], { Yes2SDK: regAuth({}) });
  assert.equal(missing.call('Yes2SDK_Auth_IsAuthenticatedJS'), 0);
  const noSdk = createSandbox(['Yes2SDKAuth.jslib']);
  assert.equal(noSdk.call('Yes2SDK_Auth_IsAuthenticatedJS'), 0);
});

test('auth wrapper isAuthenticated is false and showRegistrationPrompt throws FEATURE_NOT_SUPPORTED', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn() {}, error() {} };
  sb.window.__yes2PlatformInit();
  const auth = sb.window.Yes2SDK.auth;
  assert.equal(auth.isAuthenticated(), false);
  assert.throws(() => auth.showRegistrationPrompt({}), (e) => e.code === 'FEATURE_NOT_SUPPORTED');
});
// ---- end Auth: registration prompt ---------------------------------------------------------

// ---- runner --------------------------------------------------------------------------------
let failed = 0;
for (const { name, fn } of tests) {
  try {
    await fn();
    console.log('ok   ' + name);
  } catch (e) {
    failed++;
    console.log('FAIL ' + name + '\n       ' + String(e.message).split('\n').join('\n       '));
  }
}
console.log(`\n${tests.length - failed}/${tests.length} passed`);
process.exit(failed ? 1 : 0);
