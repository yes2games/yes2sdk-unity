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

// ---- Notifications: request-id envelope (task: notification options) -----------------------
const notifPayload = (sb) => {
  const [, , payload] = sb.sent[0];
  return { id: payload.slice(0, payload.indexOf('|')), body: payload.slice(payload.indexOf('|') + 1) };
};

test('notifications ScheduleAsync passes the parsed options and sends JSON.stringify(notification)', async () => {
  const seen = [];
  const notification = { id: 'daily', title: 'T', body: 'B', scheduledAt: 1767225600000 };
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { scheduleAsync: (o) => { seen.push(o); return Promise.resolve(notification); } } },
  });
  sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 4, sb.str('{"title":"T","body":"B","scheduledInDays":0,"priority":"high"}'));
  await flush();
  // options are parsed inside the sandbox realm, so compare by value
  assert.deepStrictEqual(JSON.parse(JSON.stringify(seen)), [{ title: 'T', body: 'B', scheduledInDays: 0, priority: 'high' }]);
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnNotificationScheduleSuccess', '4|' + JSON.stringify(notification)]]);
});

test('notifications ScheduleAsync rejection sends the error envelope with the platform code', async () => {
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { scheduleAsync: () => Promise.reject({ code: 'INVALID_PARAM', message: 'bad days' }) } },
  });
  sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 6, sb.str('{"title":"T"}'));
  await flush();
  assert.equal(sb.sent[0][1], 'OnNotificationScheduleError');
  const { id, body } = notifPayload(sb);
  assert.equal(id, '6');
  assert.deepStrictEqual(JSON.parse(body), { code: 'INVALID_PARAM', message: 'bad days', context: 'Yes2SDK.Notifications.ScheduleAsync' });
});

test('notifications ScheduleAsync with bad options JSON sends INVALID_PARAM without calling the platform', async () => {
  let called = false;
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { scheduleAsync: () => { called = true; return Promise.resolve({}); } } },
  });
  sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 2, sb.str('{bad'));
  await flush();
  assert.equal(called, false);
  assert.equal(sb.sent[0][1], 'OnNotificationScheduleError');
  assert.equal(JSON.parse(notifPayload(sb).body).code, 'INVALID_PARAM');
});

test('notifications ScheduleAsync turns a synchronous throw into an error envelope', async () => {
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { scheduleAsync: () => { throw { code: 'NOT_INITIALIZED', message: 'not yet' }; } } },
  });
  sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 8, sb.str('{}'));
  await flush();
  assert.equal(sb.sent.length, 1);
  assert.equal(sb.sent[0][1], 'OnNotificationScheduleError');
  assert.equal(JSON.parse(notifPayload(sb).body).code, 'NOT_INITIALIZED');
});

test('notifications CancelAsync passes the id and sends an empty payload', async () => {
  const seen = [];
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { cancelAsync: (id) => { seen.push(id); return Promise.resolve(); } } },
  });
  sb.call('Yes2SDK_Notifications_CancelAsyncJS', 3, sb.str('daily'));
  await flush();
  assert.deepStrictEqual(seen, ['daily']);
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnNotificationCancelSuccess', '3|']]);
});

test('notifications CancelAsync rejection sends the error envelope', async () => {
  const sb = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { cancelAsync: () => Promise.reject(new Error('gone')) } },
  });
  sb.call('Yes2SDK_Notifications_CancelAsyncJS', 5, sb.str('x'));
  await flush();
  assert.equal(sb.sent[0][1], 'OnNotificationCancelError');
  assert.deepStrictEqual(JSON.parse(notifPayload(sb).body), { code: 'Unknown', message: 'gone', context: 'Yes2SDK.Notifications.CancelAsync' });
});

test('notifications CancelAllAsync success and failure use their own channel', async () => {
  const ok = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { cancelAllAsync: () => Promise.resolve() } },
  });
  ok.call('Yes2SDK_Notifications_CancelAllAsyncJS', 10);
  await flush();
  assert.deepStrictEqual(ok.sent, [['Bridge', 'OnNotificationCancelAllSuccess', '10|']]);

  const bad = createSandbox(['Yes2SDKNotifications.jslib'], {
    Yes2SDK: { notifications: { cancelAllAsync: () => Promise.reject({ code: 'PLATFORM_ERROR', message: 'no' }) } },
  });
  bad.call('Yes2SDK_Notifications_CancelAllAsyncJS', 11);
  await flush();
  assert.equal(bad.sent[0][1], 'OnNotificationCancelAllError');
  assert.equal(notifPayload(bad).id, '11');
});

test('notifications calls without the SDK report NotInitialized, without the module FEATURE_NOT_SUPPORTED', async () => {
  const none = createSandbox(['Yes2SDKNotifications.jslib']);
  none.call('Yes2SDK_Notifications_CancelAllAsyncJS', 1);
  assert.equal(JSON.parse(notifPayload(none).body).code, 'NotInitialized');

  const old = createSandbox(['Yes2SDKNotifications.jslib'], { Yes2SDK: {} });
  old.call('Yes2SDK_Notifications_ScheduleAsyncJS', 2, old.str('{}'));
  old.call('Yes2SDK_Notifications_CancelAsyncJS', 3, old.str('x'));
  assert.deepStrictEqual(old.sent.map((s) => s[1]), ['OnNotificationScheduleError', 'OnNotificationCancelError']);
  for (const [, , payload] of old.sent) {
    assert.equal(JSON.parse(payload.slice(payload.indexOf('|') + 1)).code, 'FEATURE_NOT_SUPPORTED');
  }
});

test('notifications IsSupported reflects the platform and never throws', async () => {
  const yes = createSandbox(['Yes2SDKNotifications.jslib'], { Yes2SDK: { notifications: { isSupported: () => true } } });
  assert.equal(yes.call('Yes2SDK_Notifications_IsSupportedJS'), 1);
  const no = createSandbox(['Yes2SDKNotifications.jslib'], { Yes2SDK: { notifications: { isSupported: () => false } } });
  assert.equal(no.call('Yes2SDK_Notifications_IsSupportedJS'), 0);
  const throws = createSandbox(['Yes2SDKNotifications.jslib'], { Yes2SDK: { notifications: { isSupported: throwing } } });
  assert.equal(throws.call('Yes2SDK_Notifications_IsSupportedJS'), 0);
  assert.equal(createSandbox(['Yes2SDKNotifications.jslib']).call('Yes2SDK_Notifications_IsSupportedJS'), 0);
});

test('notifications wrapper stubs report unsupported with the wrapper message', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib', 'Yes2SDKNotifications.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn() {}, error() {} };
  sb.window.__yes2PlatformInit();
  assert.equal(sb.call('Yes2SDK_Notifications_IsSupportedJS'), 0);
  sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 1, sb.str('{"title":"t","delaySeconds":5}'));
  sb.call('Yes2SDK_Notifications_CancelAsyncJS', 2, sb.str('x'));
  sb.call('Yes2SDK_Notifications_CancelAllAsyncJS', 3);
  await flush();
  assert.deepStrictEqual(sb.sent.map((s) => s[1]),
    ['OnNotificationScheduleError', 'OnNotificationCancelError', 'OnNotificationCancelAllError']);
  for (const [, , payload] of sb.sent) {
    const err = JSON.parse(payload.slice(payload.indexOf('|') + 1));
    assert.equal(err.code, 'FEATURE_NOT_SUPPORTED');
    assert.match(err.message, /^Notifications\.\w+ is not supported on the current platform\.$/);
  }
});
// ---- end Notifications ---------------------------------------------------------------------

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
