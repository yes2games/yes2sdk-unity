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
// ---- Referrals: request-id envelope ("<id>|<payload>" to Bridge) --------------------------
const refPayload = (sb, i = 0) => {
  const p = sb.sent[i][2];
  return [p.slice(0, p.indexOf('|')), p.slice(p.indexOf('|') + 1)];
};

test('referrals ShareAsync passes the parsed options and sends the result envelope', async () => {
  const seen = [];
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: (o) => { seen.push(o); return Promise.resolve({ canceled: false }); } } },
  });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 4, sb.str('{"reference":"party_v1","data":{"room":"abc"},"image":"data:image/png;base64,AAAA"}'));
  await flush();
  assert.deepStrictEqual(JSON.parse(JSON.stringify(seen)), [{ reference: 'party_v1', data: { room: 'abc' }, image: 'data:image/png;base64,AAAA' }]);
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnReferralShareSuccess', '4|{"canceled":false}']]);
});

test('referrals ShareAsync reports a cancelled share', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: () => Promise.resolve({ canceled: true }) } },
  });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 6, sb.str('{"reference":"r"}'));
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnReferralShareSuccess', '6|{"canceled":true}']]);
});

test('referrals ShareAsync rejection sends the error envelope with the platform code', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: () => Promise.reject({ code: 'INVALID_PARAM', message: 'bad ref' }) } },
  });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 8, sb.str('{"reference":" "}'));
  await flush();
  assert.equal(sb.sent.length, 1);
  assert.equal(sb.sent[0][1], 'OnReferralShareError');
  const [id, body] = refPayload(sb);
  assert.equal(id, '8');
  assert.deepStrictEqual(JSON.parse(body), { code: 'INVALID_PARAM', message: 'bad ref', context: 'Yes2SDK.Referrals.ShareAsync' });
});

test('referrals ShareAsync with bad options JSON sends INVALID_PARAM and never calls the platform', async () => {
  let called = 0;
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: () => { called++; return Promise.resolve({ canceled: false }); } } },
  });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 2, sb.str('{not json'));
  await flush();
  assert.equal(called, 0);
  assert.equal(sb.sent.length, 1);
  assert.equal(sb.sent[0][1], 'OnReferralShareError');
  const [id, body] = refPayload(sb);
  assert.equal(id, '2');
  assert.equal(JSON.parse(body).code, 'INVALID_PARAM');
});

test('referrals ShareAsync turns a synchronous platform throw into an error envelope', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: () => { throw new Error('sync boom'); } } },
  });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 3, sb.str('{"reference":"r"}'));
  await flush();
  assert.equal(sb.sent[0][1], 'OnReferralShareError');
  assert.deepStrictEqual(JSON.parse(refPayload(sb)[1]), { code: 'Unknown', message: 'sync boom', context: 'Yes2SDK.Referrals.ShareAsync' });
});

test('referrals entry points never throw into wasm, even when reporting the error throws', () => {
  const boom = () => { throw new Error('sync boom'); };
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { shareAsync: boom, listAsync: boom } },
  });
  // Make SendMessage itself throw while the catch block reports the error.
  sb.sent.push = () => { throw new Error('SendMessage failed'); };
  assert.doesNotThrow(() => sb.call('Yes2SDK_Referrals_ShareAsyncJS', 4, sb.str('{"reference":"r"}')));
  assert.doesNotThrow(() => sb.call('Yes2SDK_Referrals_ListAsyncJS', 14));
});

test('iap subscription and notification entry points never throw into wasm, even when reporting the error throws', () => {
  const boom = () => { throw new Error('sync boom'); };
  const sb = createSandbox(['Yes2SDKIAP.jslib', 'Yes2SDKNotifications.jslib'], {
    Yes2SDK: { iap: { getSubscriptionsAsync: boom }, notifications: { scheduleAsync: boom } },
  });
  sb.sent.push = () => { throw new Error('SendMessage failed'); };
  assert.doesNotThrow(() => sb.call('Yes2SDK_IAP_GetSubscriptionsAsyncJS', 21));
  assert.doesNotThrow(() => sb.call('Yes2SDK_Notifications_ScheduleAsyncJS', 22, sb.str('{"title":"t"}')));
});

test('referrals ListAsync success sends the whole list', async () => {
  const list = { referrals: { party_v1: [{ playerId: 'p1', joinedAt: '2026-10-06T08:00:00.000Z' }] }, signedRequest: 'sig' };
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], { Yes2SDK: { referrals: { listAsync: () => Promise.resolve(list) } } });
  sb.call('Yes2SDK_Referrals_ListAsyncJS', 12);
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnReferralListSuccess', '12|' + JSON.stringify(list)]]);
});

test('referrals ListAsync rejection sends the error envelope', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], {
    Yes2SDK: { referrals: { listAsync: () => Promise.reject(new Error('down')) } },
  });
  sb.call('Yes2SDK_Referrals_ListAsyncJS', 13);
  await flush();
  assert.equal(sb.sent[0][1], 'OnReferralListError');
  const [id, body] = refPayload(sb);
  assert.equal(id, '13');
  assert.deepStrictEqual(JSON.parse(body), { code: 'Unknown', message: 'down', context: 'Yes2SDK.Referrals.ListAsync' });
});

test('referrals module missing after init reports FEATURE_NOT_SUPPORTED', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib'], { Yes2SDK: {} });
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 1, sb.str('{"reference":"r"}'));
  sb.call('Yes2SDK_Referrals_ListAsyncJS', 2);
  assert.deepStrictEqual(sb.sent.map((s) => s[1]), ['OnReferralShareError', 'OnReferralListError']);
  assert.equal(JSON.parse(refPayload(sb, 0)[1]).code, 'FEATURE_NOT_SUPPORTED');
  assert.equal(JSON.parse(refPayload(sb, 1)[1]).code, 'FEATURE_NOT_SUPPORTED');
});

test('referrals without the SDK reports NotInitialized', async () => {
  const sb = createSandbox(['Yes2SDKReferrals.jslib']);
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 1, sb.str('{"reference":"r"}'));
  sb.call('Yes2SDK_Referrals_ListAsyncJS', 2);
  assert.equal(JSON.parse(refPayload(sb, 0)[1]).code, 'NotInitialized');
  assert.equal(JSON.parse(refPayload(sb, 1)[1]).code, 'NotInitialized');
});

test('referrals IsSupported reflects the platform and never throws', async () => {
  const make = (referrals) => createSandbox(['Yes2SDKReferrals.jslib'], referrals === undefined ? {} : { Yes2SDK: { referrals } });
  assert.equal(make({ isSupported: () => true }).call('Yes2SDK_Referrals_IsSupportedJS'), 1);
  assert.equal(make({ isSupported: () => false }).call('Yes2SDK_Referrals_IsSupportedJS'), 0);
  assert.equal(make({ isSupported: throwing }).call('Yes2SDK_Referrals_IsSupportedJS'), 0);
  assert.equal(make(undefined).call('Yes2SDK_Referrals_IsSupportedJS'), 0);
});

test('referrals wrapper stubs reject FEATURE_NOT_SUPPORTED through the bridge', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib', 'Yes2SDKReferrals.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn() {}, error() {} };
  sb.window.__yes2PlatformInit();
  assert.equal(sb.call('Yes2SDK_Referrals_IsSupportedJS'), 0);
  sb.call('Yes2SDK_Referrals_ShareAsyncJS', 21, sb.str('{"reference":"r"}'));
  sb.call('Yes2SDK_Referrals_ListAsyncJS', 22);
  await flush();
  assert.deepStrictEqual(sb.sent.map((s) => s[1]), ['OnReferralShareError', 'OnReferralListError']);
  // the wrapper's own stubs answer (not the bridge's module-missing path)
  assert.deepStrictEqual(JSON.parse(refPayload(sb, 0)[1]), {
    code: 'FEATURE_NOT_SUPPORTED', message: 'Referrals.shareAsync is not supported on the current platform.', context: 'Yes2SDK.Referrals.ShareAsync',
  });
  assert.deepStrictEqual(JSON.parse(refPayload(sb, 1)[1]), {
    code: 'FEATURE_NOT_SUPPORTED', message: 'Referrals.listAsync is not supported on the current platform.', context: 'Yes2SDK.Referrals.ListAsync',
  });
});
// ---- end Referrals --------------------------------------------------------------------------
// ---- Context: share an image (request-id envelope, empty success payload) -----------------
const ctxError = (sb, i = 0) => {
  const p = sb.sent[i][2];
  return [p.slice(0, p.indexOf('|')), JSON.parse(p.slice(p.indexOf('|') + 1))];
};

test('context ShareAsync passes the parsed payload and sends an empty success envelope', async () => {
  const seen = [];
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: (p) => { seen.push(p); return Promise.resolve(); } } },
  });
  sb.call('Yes2SDK_Context_ShareAsyncJS', 5, sb.str('{"intent":"SHARE","image":"data:image/png;base64,AAAA","data":{"coupon":"X"}}'));
  await flush();
  assert.deepStrictEqual(JSON.parse(JSON.stringify(seen)), [{ intent: 'SHARE', image: 'data:image/png;base64,AAAA', data: { coupon: 'X' } }]);
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnContextShareSuccess', '5|']]);
});

test('context ShareAsync ignores whatever the platform resolves with', async () => {
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: () => Promise.resolve({ canceled: true }) } },
  });
  sb.call('Yes2SDK_Context_ShareAsyncJS', 6, sb.str('{"intent":"SHARE"}'));
  await flush();
  assert.deepStrictEqual(sb.sent, [['Bridge', 'OnContextShareSuccess', '6|']]);
});

test('context ShareAsync rejection sends the error envelope with the platform code', async () => {
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: () => Promise.reject({ code: 'PLATFORM_ERROR', message: 'sheet failed' }) } },
  });
  sb.call('Yes2SDK_Context_ShareAsyncJS', 8, sb.str('{"intent":"SHARE"}'));
  await flush();
  assert.equal(sb.sent.length, 1);
  assert.equal(sb.sent[0][1], 'OnContextShareError');
  assert.deepStrictEqual(ctxError(sb), ['8', { code: 'PLATFORM_ERROR', message: 'sheet failed', context: 'Yes2SDK.Context.ShareAsync' }]);
});

test('context ShareAsync with bad payload JSON sends INVALID_PARAM and never calls the platform', async () => {
  let called = 0;
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: () => { called++; return Promise.resolve(); } } },
  });
  sb.call('Yes2SDK_Context_ShareAsyncJS', 2, sb.str('{not json'));
  await flush();
  assert.equal(called, 0);
  assert.equal(sb.sent.length, 1);
  assert.equal(sb.sent[0][1], 'OnContextShareError');
  const [id, err] = ctxError(sb);
  assert.equal(id, '2');
  assert.equal(err.code, 'INVALID_PARAM');
});

test('context ShareAsync turns a synchronous platform throw into an error envelope', async () => {
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: () => { throw new Error('sync boom'); } } },
  });
  sb.call('Yes2SDK_Context_ShareAsyncJS', 3, sb.str('{"intent":"SHARE"}'));
  await flush();
  assert.equal(sb.sent[0][1], 'OnContextShareError');
  assert.deepStrictEqual(ctxError(sb)[1], { code: 'Unknown', message: 'sync boom', context: 'Yes2SDK.Context.ShareAsync' });
});

test('context ShareAsync never throws into wasm, even when reporting the error throws', () => {
  const sb = createSandbox(['Yes2SDKContext.jslib'], {
    Yes2SDK: { context: { shareAsync: () => { throw new Error('sync boom'); } } },
  });
  sb.sent.push = () => { throw new Error('SendMessage failed'); };
  assert.doesNotThrow(() => sb.call('Yes2SDK_Context_ShareAsyncJS', 4, sb.str('{"intent":"SHARE"}')));
});

test('context ShareAsync reports FEATURE_NOT_SUPPORTED when the module or the method is missing', async () => {
  const noModule = createSandbox(['Yes2SDKContext.jslib'], { Yes2SDK: {} });
  noModule.call('Yes2SDK_Context_ShareAsyncJS', 1, noModule.str('{"intent":"SHARE"}'));
  const noMethod = createSandbox(['Yes2SDKContext.jslib'], { Yes2SDK: { context: {} } });
  noMethod.call('Yes2SDK_Context_ShareAsyncJS', 2, noMethod.str('{"intent":"SHARE"}'));
  assert.equal(noModule.sent[0][1], 'OnContextShareError');
  assert.deepStrictEqual(ctxError(noModule).map((v, i) => (i ? v.code : v)), ['1', 'FEATURE_NOT_SUPPORTED']);
  assert.equal(noMethod.sent[0][1], 'OnContextShareError');
  assert.deepStrictEqual(ctxError(noMethod).map((v, i) => (i ? v.code : v)), ['2', 'FEATURE_NOT_SUPPORTED']);
});

test('context ShareAsync without the SDK reports NotInitialized', async () => {
  const sb = createSandbox(['Yes2SDKContext.jslib']);
  sb.call('Yes2SDK_Context_ShareAsyncJS', 1, sb.str('{"intent":"SHARE"}'));
  assert.deepStrictEqual(ctxError(sb), ['1', { code: 'NotInitialized', message: 'Yes2SDK not loaded', context: 'Yes2SDK.Context.ShareAsync' }]);
});

test('context wrapper stub rejects FEATURE_NOT_SUPPORTED through the bridge', async () => {
  const sb = createSandbox(['Yes2SDKPlatformInit.jslib', 'Yes2SDKContext.jslib']);
  sb.window.CrazyGames = { SDK: {} };
  sb.window.__y2 = { log() {}, warn() {}, error() {} };
  sb.window.__yes2PlatformInit();
  sb.call('Yes2SDK_Context_ShareAsyncJS', 31, sb.str('{"intent":"SHARE"}'));
  await flush();
  assert.deepStrictEqual(sb.sent.map((s) => s[1]), ['OnContextShareError']);
  // the wrapper's own stub answers (not the bridge's module-missing path)
  assert.deepStrictEqual(ctxError(sb), ['31', {
    code: 'FEATURE_NOT_SUPPORTED', message: 'Context.shareAsync is not supported on the current platform.', context: 'Yes2SDK.Context.ShareAsync',
  }]);
});
// ---- end Context ---------------------------------------------------------------------------

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
