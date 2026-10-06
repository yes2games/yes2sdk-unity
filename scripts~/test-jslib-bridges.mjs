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
  const configs = [];
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { purchaseAsync: (config) => { configs.push(config); return Promise.reject({ code: 'USER_INPUT', message: 'nope' }); } } },
  });
  sb.call('Yes2SDK_IAP_PurchaseAsyncJS', 3, sb.str('gem'), sb.str(''));
  await flush();
  // An empty developer payload is left out, and the product id arrives decoded.
  assert.equal(configs.length, 1);
  assert.deepStrictEqual(Object.keys(configs[0]), ['productId']);
  assert.equal(configs[0].productId, 'gem');
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

// ---- IAP subscriptions: request-id envelope, missing method, synchronous throw --------------
const subscriptionSample = {
  productId: 'vip', title: 'VIP', description: '', price: '4.99 USD', priceAmount: 4.99,
  priceCurrencyCode: 'USD', billingPeriod: 'monthly', isActive: true, trialEligible: false,
  introOffer: null, retentionOffer: { priceAmount: 1.99, durationPeriods: 2 }, signedRequest: 'sig',
};
const subscriptionCases = [
  // [JS function, Core method, callback base, takes a product id, Core result, expected payload]
  ['Yes2SDK_IAP_GetSubscriptionsAsyncJS', 'getSubscriptionsAsync', 'OnGetSubscriptions', false,
    [subscriptionSample], JSON.stringify([subscriptionSample])],
  ['Yes2SDK_IAP_SubscribeAsyncJS', 'subscribeAsync', 'OnSubscribe', true,
    { status: 'subscribed', subscription: subscriptionSample }, JSON.stringify({ status: 'subscribed', subscription: subscriptionSample })],
  ['Yes2SDK_IAP_CancelSubscriptionAsyncJS', 'cancelSubscriptionAsync', 'OnCancelSubscription', true, true, 'true'],
  ['Yes2SDK_IAP_ClaimRetentionOfferAsyncJS', 'claimRetentionOfferAsync', 'OnClaimRetentionOffer', true,
    subscriptionSample, JSON.stringify(subscriptionSample)],
];
const subArgs = (sb, takesId) => (takesId ? [21, sb.str('vip')] : [21]);
const errorBody = (payload) => JSON.parse(payload.slice(payload.indexOf('|') + 1));

for (const [fn, method, callback, takesId, result, expected] of subscriptionCases) {
  test(`iap ${method} success sends "<id>|<payload>"`, async () => {
    const args = [];
    const sb = createSandbox(['Yes2SDKIAP.jslib'], {
      Yes2SDK: { iap: { [method]: (...a) => { args.push(a); return Promise.resolve(result); } } },
    });
    sb.call(fn, ...subArgs(sb, takesId));
    await flush();
    assert.deepStrictEqual(sb.sent, [['Bridge', callback + 'Success', '21|' + expected]]);
    assert.deepStrictEqual(args, [takesId ? ['vip'] : []]);
  });

  test(`iap ${method} rejection sends the error envelope with the platform code`, async () => {
    const sb = createSandbox(['Yes2SDKIAP.jslib'], {
      Yes2SDK: { iap: { [method]: () => Promise.reject({ code: 'PLAYER_NOT_AUTHENTICATED', message: 'sign in' }) } },
    });
    sb.call(fn, ...subArgs(sb, takesId));
    await flush();
    assert.equal(sb.sent.length, 1);
    assert.equal(sb.sent[0][1], callback + 'Error');
    assert.ok(sb.sent[0][2].startsWith('21|'));
    assert.equal(errorBody(sb.sent[0][2]).code, 'PLAYER_NOT_AUTHENTICATED');
    assert.equal(errorBody(sb.sent[0][2]).message, 'sign in');
  });

  test(`iap ${method} missing on an older runtime sends FEATURE_NOT_SUPPORTED`, async () => {
    const sb = createSandbox(['Yes2SDKIAP.jslib'], { Yes2SDK: { iap: {} } });
    sb.call(fn, ...subArgs(sb, takesId));
    await flush();
    assert.equal(sb.sent.length, 1);
    assert.equal(sb.sent[0][1], callback + 'Error');
    assert.ok(sb.sent[0][2].startsWith('21|'));
    assert.equal(errorBody(sb.sent[0][2]).code, 'FEATURE_NOT_SUPPORTED');
  });

  test(`iap ${method} synchronous throw is reported, never thrown into wasm`, async () => {
    const sb = createSandbox(['Yes2SDKIAP.jslib'], {
      Yes2SDK: { iap: { [method]: () => { throw { code: 'NOT_INITIALIZED', message: 'early' }; } } },
    });
    sb.call(fn, ...subArgs(sb, takesId));
    await flush();
    assert.equal(sb.sent.length, 1);
    assert.equal(sb.sent[0][1], callback + 'Error');
    assert.equal(errorBody(sb.sent[0][2]).code, 'NOT_INITIALIZED');
  });

  test(`iap ${method} without the module reports NotInitialized`, async () => {
    const sb = createSandbox(['Yes2SDKIAP.jslib'], { Yes2SDK: {} });
    sb.call(fn, ...subArgs(sb, takesId));
    assert.equal(sb.sent[0][1], callback + 'Error');
    assert.equal(errorBody(sb.sent[0][2]).code, 'NotInitialized');
  });
}

test('iap CancelSubscriptionAsync sends "false" when the player dismisses', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { cancelSubscriptionAsync: () => Promise.resolve(false) } },
  });
  sb.call('Yes2SDK_IAP_CancelSubscriptionAsyncJS', 4, sb.str('vip'));
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnCancelSubscriptionSuccess', '4|false']]);
});

test('iap SubscribeAsync passes a closed checkout through as a success', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { subscribeAsync: () => Promise.resolve({ status: 'cancelled' }) } },
  });
  sb.call('Yes2SDK_IAP_SubscribeAsyncJS', 5, sb.str('vip'));
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnSubscribeSuccess', '5|{"status":"cancelled"}']]);
});

test('iap GetSubscriptionsAsync sends [] for an empty result', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], {
    Yes2SDK: { iap: { getSubscriptionsAsync: () => Promise.resolve(undefined) } },
  });
  sb.call('Yes2SDK_IAP_GetSubscriptionsAsyncJS', 6);
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnGetSubscriptionsSuccess', '6|[]']]);
});

for (const [label, iap, expected] of [
  ['true', { isSubscriptionSupported: () => true }, 1],
  ['false', { isSubscriptionSupported: () => false }, 0],
  ['missing method', {}, 0],
  ['throwing', { isSubscriptionSupported: () => { throw new Error('boom'); } }, 0],
]) {
  test(`iap IsSubscriptionSupported with ${label} result returns ${expected}`, async () => {
    const sb = createSandbox(['Yes2SDKIAP.jslib'], { Yes2SDK: { iap } });
    assert.equal(sb.call('Yes2SDK_IAP_IsSubscriptionSupportedJS'), expected);
  });
}

test('iap IsSubscriptionSupported returns 0 without the module', async () => {
  const sb = createSandbox(['Yes2SDKIAP.jslib'], { Yes2SDK: {} });
  assert.equal(sb.call('Yes2SDK_IAP_IsSubscriptionSupportedJS'), 0);
});

test('iap wrapper reports subscriptions unsupported and rejects every subscription call', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn() {}, error() {} };
  sb.window.__yes2PlatformInit();
  const iap = sb.window.Yes2SDK.iap;
  assert.equal(iap.isSubscriptionSupported(), false);
  for (const m of ['getSubscriptionsAsync', 'subscribeAsync', 'cancelSubscriptionAsync', 'claimRetentionOfferAsync']) {
    await assert.rejects(iap[m]('vip'), (e) => e.code === 'FEATURE_NOT_SUPPORTED');
  }
});
// ---- end IAP subscriptions -----------------------------------------------------------------

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
