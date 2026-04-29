mergeInto(LibraryManager.library, {

    Yes2SDK_LogEventJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogEventJS: function(namePtr, paramsJsonPtr) {
        var name = UTF8ToString(namePtr);
        var paramsJson = UTF8ToString(paramsJsonPtr);
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logEvent(name, paramsJson); }
        catch(e) { window.__y2.error('analytics.logEvent failed:', e); }
    },

    Yes2SDK_LogLevelStartJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogLevelStartJS: function(levelPtr) {
        var level = UTF8ToString(levelPtr);
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logLevelStart(level); }
        catch(e) { window.__y2.error('analytics.logLevelStart failed:', e); }
    },

    Yes2SDK_LogLevelEndJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogLevelEndJS: function(levelPtr, score, success, durationSeconds) {
        var level = UTF8ToString(levelPtr);
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        var duration = durationSeconds < 0 ? undefined : durationSeconds;
        try { window.Yes2SDK.analytics.logLevelEnd(level, score, success ? true : false, duration); }
        catch(e) { window.__y2.error('analytics.logLevelEnd failed:', e); }
    },

    Yes2SDK_LogScoreJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogScoreJS: function(score, levelPtr) {
        var level = UTF8ToString(levelPtr);
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logScore(score, level); }
        catch(e) { window.__y2.error('analytics.logScore failed:', e); }
    },

    Yes2SDK_LogTutorialStartJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogTutorialStartJS: function() {
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logTutorialStart(); }
        catch(e) { window.__y2.error('analytics.logTutorialStart failed:', e); }
    },

    Yes2SDK_LogTutorialEndJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogTutorialEndJS: function() {
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logTutorialEnd(); }
        catch(e) { window.__y2.error('analytics.logTutorialEnd failed:', e); }
    },

    Yes2SDK_LogPurchaseJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_LogPurchaseJS: function(productIdPtr, price, currencyPtr) {
        var productId = UTF8ToString(productIdPtr);
        var currency = UTF8ToString(currencyPtr);
        if (!__y2h.has('analytics')) { window.__y2.warn('Analytics module not loaded'); return; }
        try { window.Yes2SDK.analytics.logPurchase(productId, price, currency); }
        catch(e) { window.__y2.error('analytics.logPurchase failed:', e); }
    }

});
