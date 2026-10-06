mergeInto(LibraryManager.library, {

    // Every referrals response is sent as "<requestId>|<payload>" so
    // Yes2SDKReferrals.cs can hand it to the call that made it, not to
    // whichever call came last.
    $__y2ref: {
        send: function(callback, requestId, payload) {
            SendMessage('Bridge', callback, requestId + '|' + payload);
        },
        sendError: function(callback, requestId, code, message, context) {
            __y2ref.send(callback, requestId, JSON.stringify({ code: code, message: message, context: context }));
        },
        handleCatch: function(callback, requestId, defaultMessage, context) {
            return function(error) {
                __y2ref.sendError(callback, requestId,
                    (error && error.code) || 'Unknown',
                    (error && error.message) || defaultMessage,
                    context);
            };
        },
        // Reports a missing SDK or module. Returns true when the call can go ahead.
        ready: function(callback, requestId, context) {
            if (!__y2h.has()) {
                __y2ref.sendError(callback, requestId, 'NotInitialized', 'Yes2SDK not loaded', context);
                return false;
            }
            if (!__y2h.has('referrals')) {
                __y2ref.sendError(callback, requestId, 'FEATURE_NOT_SUPPORTED',
                    'Referrals are not supported on the current platform', context);
                return false;
            }
            return true;
        }
    },
    $__y2ref__deps: ['$__y2h'],

    Yes2SDK_Referrals_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Referrals_IsSupportedJS: function() {
        try {
            if (!__y2h.has('referrals')) return 0;
            return window.Yes2SDK.referrals.isSupported() ? 1 : 0;
        } catch (e) {
            return 0;
        }
    },

    Yes2SDK_Referrals_ShareAsyncJS__deps: ['$__y2ref'],
    Yes2SDK_Referrals_ShareAsyncJS: function(requestId, optionsJsonPtr) {
        var context = 'Yes2SDK.Referrals.ShareAsync';
        var onError = __y2ref.handleCatch('OnReferralShareError', requestId, 'Referral share failed', context);
        try {
            if (!__y2ref.ready('OnReferralShareError', requestId, context)) return;

            var options;
            try {
                options = JSON.parse(UTF8ToString(optionsJsonPtr));
            } catch (parseError) {
                __y2ref.sendError('OnReferralShareError', requestId, 'INVALID_PARAM',
                    'Referral share options are not valid JSON', context);
                return;
            }

            window.Yes2SDK.referrals.shareAsync(options)
                .then(function(result) {
                    __y2ref.send('OnReferralShareSuccess', requestId,
                        JSON.stringify({ canceled: !!(result && result.canceled) }));
                })
                .catch(onError);
        } catch (e) {
            onError(e);
        }
    },

    Yes2SDK_Referrals_ListAsyncJS__deps: ['$__y2ref'],
    Yes2SDK_Referrals_ListAsyncJS: function(requestId) {
        var context = 'Yes2SDK.Referrals.ListAsync';
        var onError = __y2ref.handleCatch('OnReferralListError', requestId, 'Referral list failed', context);
        try {
            if (!__y2ref.ready('OnReferralListError', requestId, context)) return;

            window.Yes2SDK.referrals.listAsync()
                .then(function(list) {
                    __y2ref.send('OnReferralListSuccess', requestId, JSON.stringify(list || {}));
                })
                .catch(onError);
        } catch (e) {
            onError(e);
        }
    }

});
