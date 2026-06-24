mergeInto(LibraryManager.library, {

    Yes2SDK_Leaderboard_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_IsSupportedJS: function() {
        return __y2h.has('leaderboard') && window.Yes2SDK.leaderboard.isSupported() ? 1 : 0;
    },

    Yes2SDK_Leaderboard_GetLeaderboardAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_GetLeaderboardAsyncJS: function(namePtr) {
        if (!__y2h.has('leaderboard')) {
            __y2h.sendError('OnGetLeaderboardError', 'NotInitialized', 'Yes2SDK Leaderboard module not loaded', 'Yes2SDK.Leaderboard.GetLeaderboardAsync');
            return;
        }

        var name = UTF8ToString(namePtr);

        window.Yes2SDK.leaderboard.getLeaderboardAsync(name)
            .then(function(leaderboard) {
                SendMessage('Bridge', 'OnGetLeaderboardSuccess', JSON.stringify(leaderboard));
            })
            .catch(__y2h.handleCatch('OnGetLeaderboardError', 'GetLeaderboard failed', 'Yes2SDK.Leaderboard.GetLeaderboardAsync'));
    },

    Yes2SDK_Leaderboard_SetScoreAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_SetScoreAsyncJS: function(namePtr, score, metadataPtr) {
        if (!__y2h.has('leaderboard')) {
            __y2h.sendError('OnSetScoreError', 'NotInitialized', 'Yes2SDK Leaderboard module not loaded', 'Yes2SDK.Leaderboard.SetScoreAsync');
            return;
        }

        var name = UTF8ToString(namePtr);
        var metadata = UTF8ToString(metadataPtr);

        window.Yes2SDK.leaderboard.setScoreAsync(name, score, metadata || undefined)
            .then(function(entry) {
                SendMessage('Bridge', 'OnSetScoreSuccess', JSON.stringify(entry));
            })
            .catch(__y2h.handleCatch('OnSetScoreError', 'SetScore failed', 'Yes2SDK.Leaderboard.SetScoreAsync'));
    },

    Yes2SDK_Leaderboard_GetEntriesAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_GetEntriesAsyncJS: function(namePtr, count, offset) {
        if (!__y2h.has('leaderboard')) {
            __y2h.sendError('OnGetEntriesError', 'NotInitialized', 'Yes2SDK Leaderboard module not loaded', 'Yes2SDK.Leaderboard.GetEntriesAsync');
            return;
        }

        var name = UTF8ToString(namePtr);

        window.Yes2SDK.leaderboard.getEntriesAsync(name, count, offset)
            .then(function(entries) {
                SendMessage('Bridge', 'OnGetEntriesSuccess', JSON.stringify(entries || []));
            })
            .catch(__y2h.handleCatch('OnGetEntriesError', 'GetEntries failed', 'Yes2SDK.Leaderboard.GetEntriesAsync'));
    },

    Yes2SDK_Leaderboard_GetPlayerEntryAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_GetPlayerEntryAsyncJS: function(namePtr) {
        if (!__y2h.has('leaderboard')) {
            __y2h.sendError('OnGetPlayerEntryError', 'NotInitialized', 'Yes2SDK Leaderboard module not loaded', 'Yes2SDK.Leaderboard.GetPlayerEntryAsync');
            return;
        }

        var name = UTF8ToString(namePtr);

        window.Yes2SDK.leaderboard.getPlayerEntryAsync(name)
            .then(function(entry) {
                SendMessage('Bridge', 'OnGetPlayerEntrySuccess', JSON.stringify(entry === undefined ? null : entry));
            })
            .catch(__y2h.handleCatch('OnGetPlayerEntryError', 'GetPlayerEntry failed', 'Yes2SDK.Leaderboard.GetPlayerEntryAsync'));
    },

    Yes2SDK_Leaderboard_GetConnectedPlayerEntriesAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Leaderboard_GetConnectedPlayerEntriesAsyncJS: function(namePtr, count, offset) {
        if (!__y2h.has('leaderboard')) {
            __y2h.sendError('OnGetConnectedPlayerEntriesError', 'NotInitialized', 'Yes2SDK Leaderboard module not loaded', 'Yes2SDK.Leaderboard.GetConnectedPlayerEntriesAsync');
            return;
        }

        var name = UTF8ToString(namePtr);

        window.Yes2SDK.leaderboard.getConnectedPlayerEntriesAsync(name, count, offset)
            .then(function(entries) {
                SendMessage('Bridge', 'OnGetConnectedPlayerEntriesSuccess', JSON.stringify(entries || []));
            })
            .catch(__y2h.handleCatch('OnGetConnectedPlayerEntriesError', 'GetConnectedPlayerEntries failed', 'Yes2SDK.Leaderboard.GetConnectedPlayerEntriesAsync'));
    }

});
