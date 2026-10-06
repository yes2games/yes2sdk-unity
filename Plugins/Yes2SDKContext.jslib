mergeInto(LibraryManager.library, {

    // Every context response is sent as "<requestId>|<payload>" so
    // Yes2SDKContext.cs can hand it to the call that made it, not to
    // whichever call came last.
    $__y2ctx: {
        send: function(callback, requestId, payload) {
            SendMessage('Bridge', callback, requestId + '|' + payload);
        },
        sendError: function(callback, requestId, code, message, context) {
            __y2ctx.send(callback, requestId, JSON.stringify({ code: code, message: message, context: context }));
        },
        handleCatch: function(callback, requestId, defaultMessage, context) {
            return function(error) {
                __y2ctx.sendError(callback, requestId,
                    (error && error.code) || 'Unknown',
                    (error && error.message) || defaultMessage,
                    context);
            };
        }
    },

    Yes2SDK_Context_ShareAsyncJS__deps: ['$__y2ctx', '$__y2h'],
    Yes2SDK_Context_ShareAsyncJS: function(requestId, payloadJsonPtr) {
        var context = 'Yes2SDK.Context.ShareAsync';
        var onError = __y2ctx.handleCatch('OnContextShareError', requestId, 'Share failed', context);
        try {
            if (!__y2h.has()) {
                __y2ctx.sendError('OnContextShareError', requestId, 'NotInitialized', 'Yes2SDK not loaded', context);
                return;
            }
            if (!__y2h.has('context') || typeof window.Yes2SDK.context.shareAsync !== 'function') {
                __y2ctx.sendError('OnContextShareError', requestId, 'FEATURE_NOT_SUPPORTED',
                    'Sharing is not supported on the current platform', context);
                return;
            }

            var payload;
            try {
                payload = JSON.parse(UTF8ToString(payloadJsonPtr));
            } catch (parseError) {
                __y2ctx.sendError('OnContextShareError', requestId, 'INVALID_PARAM',
                    'Share payload is not valid JSON', context);
                return;
            }

            // The platform resolves without a result: no cancel signal reaches the game.
            window.Yes2SDK.context.shareAsync(payload)
                .then(function() {
                    __y2ctx.send('OnContextShareSuccess', requestId, '');
                })
                .catch(onError);
        } catch (e) {
            // Reporting must not throw either: a throw here would reach wasm.
            try { onError(e); } catch (_) {}
        }
    }

});
