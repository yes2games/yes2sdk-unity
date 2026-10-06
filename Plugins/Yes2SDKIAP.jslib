mergeInto(LibraryManager.library, {

    // Every IAP response is sent as "<requestId>|<payload>" so Yes2SDKIAP.cs can
    // hand it to the call that made it, not to whichever call came last.
    $__y2iap: {
        send: function(callback, requestId, payload) {
            SendMessage('Bridge', callback, requestId + '|' + payload);
        },
        sendError: function(callback, requestId, code, message, context) {
            __y2iap.send(callback, requestId, JSON.stringify({ code: code, message: message, context: context }));
        },
        handleCatch: function(callback, requestId, defaultMessage, context) {
            return function(error) {
                __y2iap.sendError(callback, requestId,
                    (error && error.code) || 'Unknown',
                    (error && error.message) || defaultMessage,
                    context);
            };
        }
    },

    Yes2SDK_IAP_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_IAP_IsSupportedJS: function() {
        if (!__y2h.has('iap')) return false;
        try { return window.Yes2SDK.iap.isSupported() ? 1 : 0; }
        catch (e) { return 0; }
    },

    Yes2SDK_IAP_GetCatalogAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_GetCatalogAsyncJS: function(requestId) {
        if (!__y2h.has('iap')) {
            __y2iap.sendError('OnGetCatalogError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.GetCatalogAsync');
            return;
        }

        window.Yes2SDK.iap.getCatalogAsync()
            .then(function(products) {
                __y2iap.send('OnGetCatalogSuccess', requestId, JSON.stringify(products || []));
            })
            .catch(__y2iap.handleCatch('OnGetCatalogError', requestId, 'GetCatalog failed', 'Yes2SDK.IAP.GetCatalogAsync'));
    },

    Yes2SDK_IAP_PurchaseAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_PurchaseAsyncJS: function(requestId, productIdPtr, developerPayloadPtr) {
        if (!__y2h.has('iap')) {
            __y2iap.sendError('OnPurchaseError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.PurchaseAsync');
            return;
        }

        var productId = UTF8ToString(productIdPtr);
        var developerPayload = UTF8ToString(developerPayloadPtr);
        var config = { productId: productId };
        if (developerPayload) { config.developerPayload = developerPayload; }

        window.Yes2SDK.iap.purchaseAsync(config)
            .then(function(purchase) {
                __y2iap.send('OnPurchaseSuccess', requestId, JSON.stringify(purchase));
            })
            .catch(__y2iap.handleCatch('OnPurchaseError', requestId, 'Purchase failed', 'Yes2SDK.IAP.PurchaseAsync'));
    },

    Yes2SDK_IAP_GetPurchasesAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_GetPurchasesAsyncJS: function(requestId) {
        if (!__y2h.has('iap')) {
            __y2iap.sendError('OnGetPurchasesError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.GetPurchasesAsync');
            return;
        }

        window.Yes2SDK.iap.getPurchasesAsync()
            .then(function(purchases) {
                __y2iap.send('OnGetPurchasesSuccess', requestId, JSON.stringify(purchases || []));
            })
            .catch(__y2iap.handleCatch('OnGetPurchasesError', requestId, 'GetPurchases failed', 'Yes2SDK.IAP.GetPurchasesAsync'));
    },

    Yes2SDK_IAP_ConsumePurchaseAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_ConsumePurchaseAsyncJS: function(requestId, purchaseTokenPtr) {
        if (!__y2h.has('iap')) {
            __y2iap.sendError('OnConsumePurchaseError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.ConsumePurchaseAsync');
            return;
        }

        var purchaseToken = UTF8ToString(purchaseTokenPtr);

        window.Yes2SDK.iap.consumePurchaseAsync(purchaseToken)
            .then(function() {
                __y2iap.send('OnConsumePurchaseSuccess', requestId, '');
            })
            .catch(__y2iap.handleCatch('OnConsumePurchaseError', requestId, 'ConsumePurchase failed', 'Yes2SDK.IAP.ConsumePurchaseAsync'));
    },

    // ---- Subscriptions ----------------------------------------------------------------
    // Same "<requestId>|<payload>" envelope. A runtime without the method (older Core)
    // reports FEATURE_NOT_SUPPORTED, and a synchronous throw is reported, never thrown
    // into wasm.

    Yes2SDK_IAP_IsSubscriptionSupportedJS__deps: ['$__y2h'],
    Yes2SDK_IAP_IsSubscriptionSupportedJS: function() {
        try {
            if (!__y2h.has('iap')) return 0;
            if (typeof window.Yes2SDK.iap.isSubscriptionSupported !== 'function') return 0;
            return window.Yes2SDK.iap.isSubscriptionSupported() ? 1 : 0;
        } catch (e) { return 0; }
    },

    Yes2SDK_IAP_GetSubscriptionsAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_GetSubscriptionsAsyncJS: function(requestId) {
        var context = 'Yes2SDK.IAP.GetSubscriptionsAsync';
        var onError = __y2iap.handleCatch('OnGetSubscriptionsError', requestId, 'GetSubscriptions failed', context);
        try {
            if (!__y2h.has('iap')) {
                __y2iap.sendError('OnGetSubscriptionsError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', context);
                return;
            }
            if (typeof window.Yes2SDK.iap.getSubscriptionsAsync !== 'function') {
                __y2iap.sendError('OnGetSubscriptionsError', requestId, 'FEATURE_NOT_SUPPORTED', 'Subscriptions are not supported by this runtime', context);
                return;
            }
            window.Yes2SDK.iap.getSubscriptionsAsync()
                .then(function(list) {
                    __y2iap.send('OnGetSubscriptionsSuccess', requestId, JSON.stringify(list || []));
                })
                .catch(onError);
        } catch (e) { try { onError(e); } catch (_) {} }
    },

    Yes2SDK_IAP_SubscribeAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_SubscribeAsyncJS: function(requestId, productIdPtr) {
        var context = 'Yes2SDK.IAP.SubscribeAsync';
        var onError = __y2iap.handleCatch('OnSubscribeError', requestId, 'Subscribe failed', context);
        try {
            if (!__y2h.has('iap')) {
                __y2iap.sendError('OnSubscribeError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', context);
                return;
            }
            if (typeof window.Yes2SDK.iap.subscribeAsync !== 'function') {
                __y2iap.sendError('OnSubscribeError', requestId, 'FEATURE_NOT_SUPPORTED', 'Subscriptions are not supported by this runtime', context);
                return;
            }
            var productId = UTF8ToString(productIdPtr);
            // A closed checkout resolves { status: 'cancelled' }: a success, not an error.
            window.Yes2SDK.iap.subscribeAsync(productId)
                .then(function(result) {
                    __y2iap.send('OnSubscribeSuccess', requestId, JSON.stringify(result));
                })
                .catch(onError);
        } catch (e) { try { onError(e); } catch (_) {} }
    },

    Yes2SDK_IAP_CancelSubscriptionAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_CancelSubscriptionAsyncJS: function(requestId, productIdPtr) {
        var context = 'Yes2SDK.IAP.CancelSubscriptionAsync';
        var onError = __y2iap.handleCatch('OnCancelSubscriptionError', requestId, 'CancelSubscription failed', context);
        try {
            if (!__y2h.has('iap')) {
                __y2iap.sendError('OnCancelSubscriptionError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', context);
                return;
            }
            if (typeof window.Yes2SDK.iap.cancelSubscriptionAsync !== 'function') {
                __y2iap.sendError('OnCancelSubscriptionError', requestId, 'FEATURE_NOT_SUPPORTED', 'Subscriptions are not supported by this runtime', context);
                return;
            }
            var productId = UTF8ToString(productIdPtr);
            window.Yes2SDK.iap.cancelSubscriptionAsync(productId)
                .then(function(confirmed) {
                    __y2iap.send('OnCancelSubscriptionSuccess', requestId, confirmed ? 'true' : 'false');
                })
                .catch(onError);
        } catch (e) { try { onError(e); } catch (_) {} }
    },

    Yes2SDK_IAP_ClaimRetentionOfferAsyncJS__deps: ['$__y2h', '$__y2iap'],
    Yes2SDK_IAP_ClaimRetentionOfferAsyncJS: function(requestId, productIdPtr) {
        var context = 'Yes2SDK.IAP.ClaimRetentionOfferAsync';
        var onError = __y2iap.handleCatch('OnClaimRetentionOfferError', requestId, 'ClaimRetentionOffer failed', context);
        try {
            if (!__y2h.has('iap')) {
                __y2iap.sendError('OnClaimRetentionOfferError', requestId, 'NotInitialized', 'Yes2SDK IAP module not loaded', context);
                return;
            }
            if (typeof window.Yes2SDK.iap.claimRetentionOfferAsync !== 'function') {
                __y2iap.sendError('OnClaimRetentionOfferError', requestId, 'FEATURE_NOT_SUPPORTED', 'Subscriptions are not supported by this runtime', context);
                return;
            }
            var productId = UTF8ToString(productIdPtr);
            window.Yes2SDK.iap.claimRetentionOfferAsync(productId)
                .then(function(subscription) {
                    __y2iap.send('OnClaimRetentionOfferSuccess', requestId, JSON.stringify(subscription));
                })
                .catch(onError);
        } catch (e) { try { onError(e); } catch (_) {} }
    }
    // ---- end Subscriptions ------------------------------------------------------------

});
