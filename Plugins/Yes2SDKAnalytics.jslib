mergeInto(LibraryManager.library, {

    Yes2SDK_LogEventJS: function(namePtr, paramsJsonPtr) {
        var name = UTF8ToString(namePtr);
        var paramsJson = UTF8ToString(paramsJsonPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logEvent(name, paramsJson);
    },

    Yes2SDK_LogLevelStartJS: function(levelPtr) {
        var level = UTF8ToString(levelPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logLevelStart(level);
    },

    Yes2SDK_LogLevelEndJS: function(levelPtr, score, success) {
        var level = UTF8ToString(levelPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logLevelEnd(level, score, success ? true : false);
    },

    Yes2SDK_LogScoreJS: function(score, levelPtr) {
        var level = UTF8ToString(levelPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logScore(score, level);
    },

    Yes2SDK_LogTutorialStartJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logTutorialStart();
    },

    Yes2SDK_LogTutorialEndJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logTutorialEnd();
    },

    Yes2SDK_LogPurchaseJS: function(productIdPtr, price, currencyPtr) {
        var productId = UTF8ToString(productIdPtr);
        var currency = UTF8ToString(currencyPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.analytics === 'undefined') {
            console.warn('[Yes2SDK] Analytics module not loaded');
            return;
        }

        window.Yes2SDK.analytics.logPurchase(productId, price, currency);
    }

});
