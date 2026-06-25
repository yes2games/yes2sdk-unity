mergeInto(LibraryManager.library, {

    Yes2SDK_IAP_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_IAP_IsSupportedJS: function() {
        if (!__y2h.has('iap')) return false;
        try { return window.Yes2SDK.iap.isSupported() ? 1 : 0; }
        catch (e) { return 0; }
    },

    Yes2SDK_IAP_GetCatalogAsyncJS__deps: ['$__y2h'],
    Yes2SDK_IAP_GetCatalogAsyncJS: function() {
        if (!__y2h.has('iap')) {
            __y2h.sendError('OnGetCatalogError', 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.GetCatalogAsync');
            return;
        }

        window.Yes2SDK.iap.getCatalogAsync()
            .then(function(products) {
                SendMessage('Bridge', 'OnGetCatalogSuccess', JSON.stringify(products || []));
            })
            .catch(__y2h.handleCatch('OnGetCatalogError', 'GetCatalog failed', 'Yes2SDK.IAP.GetCatalogAsync'));
    },

    Yes2SDK_IAP_PurchaseAsyncJS__deps: ['$__y2h'],
    Yes2SDK_IAP_PurchaseAsyncJS: function(productIdPtr, developerPayloadPtr) {
        if (!__y2h.has('iap')) {
            __y2h.sendError('OnPurchaseError', 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.PurchaseAsync');
            return;
        }

        var productId = UTF8ToString(productIdPtr);
        var developerPayload = UTF8ToString(developerPayloadPtr);
        var config = { productId: productId };
        if (developerPayload) { config.developerPayload = developerPayload; }

        window.Yes2SDK.iap.purchaseAsync(config)
            .then(function(purchase) {
                SendMessage('Bridge', 'OnPurchaseSuccess', JSON.stringify(purchase));
            })
            .catch(__y2h.handleCatch('OnPurchaseError', 'Purchase failed', 'Yes2SDK.IAP.PurchaseAsync'));
    },

    Yes2SDK_IAP_GetPurchasesAsyncJS__deps: ['$__y2h'],
    Yes2SDK_IAP_GetPurchasesAsyncJS: function() {
        if (!__y2h.has('iap')) {
            __y2h.sendError('OnGetPurchasesError', 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.GetPurchasesAsync');
            return;
        }

        window.Yes2SDK.iap.getPurchasesAsync()
            .then(function(purchases) {
                SendMessage('Bridge', 'OnGetPurchasesSuccess', JSON.stringify(purchases || []));
            })
            .catch(__y2h.handleCatch('OnGetPurchasesError', 'GetPurchases failed', 'Yes2SDK.IAP.GetPurchasesAsync'));
    },

    Yes2SDK_IAP_ConsumePurchaseAsyncJS__deps: ['$__y2h'],
    Yes2SDK_IAP_ConsumePurchaseAsyncJS: function(purchaseTokenPtr) {
        if (!__y2h.has('iap')) {
            __y2h.sendError('OnConsumePurchaseError', 'NotInitialized', 'Yes2SDK IAP module not loaded', 'Yes2SDK.IAP.ConsumePurchaseAsync');
            return;
        }

        var purchaseToken = UTF8ToString(purchaseTokenPtr);

        window.Yes2SDK.iap.consumePurchaseAsync(purchaseToken)
            .then(function() {
                SendMessage('Bridge', 'OnConsumePurchaseSuccess', '');
            })
            .catch(__y2h.handleCatch('OnConsumePurchaseError', 'ConsumePurchase failed', 'Yes2SDK.IAP.ConsumePurchaseAsync'));
    }

});
