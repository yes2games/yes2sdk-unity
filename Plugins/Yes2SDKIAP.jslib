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
    }

});
