mergeInto(LibraryManager.library, {

    Yes2SDK_Stats_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Stats_IsSupportedJS: function() {
        if (!__y2h.has('stats')) return false;
        try { return window.Yes2SDK.stats.isSupported() ? 1 : 0; }
        catch (e) { return 0; }
    },

    Yes2SDK_Stats_GetStatsAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Stats_GetStatsAsyncJS: function(keysJsonPtr) {
        if (!__y2h.has('stats')) {
            __y2h.sendError('OnGetStatsError', 'NotInitialized', 'Yes2SDK Stats module not loaded', 'Yes2SDK.Stats.GetStatsAsync');
            return;
        }

        var keys;
        try { keys = JSON.parse(UTF8ToString(keysJsonPtr)) || []; }
        catch (e) { keys = []; }

        window.Yes2SDK.stats.getStatsAsync(keys)
            .then(function(stats) {
                SendMessage('Bridge', 'OnGetStatsSuccess', JSON.stringify(stats || {}));
            })
            .catch(__y2h.handleCatch('OnGetStatsError', 'GetStats failed', 'Yes2SDK.Stats.GetStatsAsync'));
    },

    Yes2SDK_Stats_SetStatsAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Stats_SetStatsAsyncJS: function(statsJsonPtr) {
        if (!__y2h.has('stats')) {
            __y2h.sendError('OnSetStatsError', 'NotInitialized', 'Yes2SDK Stats module not loaded', 'Yes2SDK.Stats.SetStatsAsync');
            return;
        }

        var stats;
        try { stats = JSON.parse(UTF8ToString(statsJsonPtr)) || {}; }
        catch (e) { stats = {}; }

        window.Yes2SDK.stats.setStatsAsync(stats)
            .then(function() {
                SendMessage('Bridge', 'OnSetStatsSuccess', '');
            })
            .catch(__y2h.handleCatch('OnSetStatsError', 'SetStats failed', 'Yes2SDK.Stats.SetStatsAsync'));
    },

    Yes2SDK_Stats_IncrementStatsAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Stats_IncrementStatsAsyncJS: function(incrementsJsonPtr) {
        if (!__y2h.has('stats')) {
            __y2h.sendError('OnIncrementStatsError', 'NotInitialized', 'Yes2SDK Stats module not loaded', 'Yes2SDK.Stats.IncrementStatsAsync');
            return;
        }

        var increments;
        try { increments = JSON.parse(UTF8ToString(incrementsJsonPtr)) || {}; }
        catch (e) { increments = {}; }

        window.Yes2SDK.stats.incrementStatsAsync(increments)
            .then(function(stats) {
                SendMessage('Bridge', 'OnIncrementStatsSuccess', JSON.stringify(stats || {}));
            })
            .catch(__y2h.handleCatch('OnIncrementStatsError', 'IncrementStats failed', 'Yes2SDK.Stats.IncrementStatsAsync'));
    }

});
