mergeInto(LibraryManager.library, {

    // Get player info asynchronously
    Yes2SDK_GetPlayerAsyncJS__deps: ['$__y2h'],
    Yes2SDK_GetPlayerAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetPlayerError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetPlayerAsync');
            return;
        }

        window.Yes2SDK.player.getPlayerAsync()
            .then(function(player) {
                SendMessage('Bridge', 'OnGetPlayerSuccess', JSON.stringify(player));
            })
            .catch(__y2h.handleCatch('OnGetPlayerError', 'GetPlayer failed', 'Yes2SDK.Player.GetPlayerAsync'));
    },

    // Get player data
    Yes2SDK_GetDataAsyncJS__deps: ['$__y2h'],
    Yes2SDK_GetDataAsyncJS: function(keysJsonPtr) {
        var keysJson = UTF8ToString(keysJsonPtr);

        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetDataError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetDataAsync');
            return;
        }

        window.Yes2SDK.player.getDataAsync(keysJson)
            .then(function(data) {
                SendMessage('Bridge', 'OnGetDataSuccess', typeof data === 'string' ? data : JSON.stringify(data));
            })
            .catch(__y2h.handleCatch('OnGetDataError', 'GetData failed', 'Yes2SDK.Player.GetDataAsync'));
    },

    // Set player data
    Yes2SDK_SetDataAsyncJS__deps: ['$__y2h'],
    Yes2SDK_SetDataAsyncJS: function(dataJsonPtr) {
        var dataJson = UTF8ToString(dataJsonPtr);

        if (!__y2h.has('player')) {
            __y2h.sendError('OnSetDataError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.SetDataAsync');
            return;
        }

        window.Yes2SDK.player.setDataAsync(dataJson)
            .then(function() {
                SendMessage('Bridge', 'OnSetDataSuccess', '');
            })
            .catch(__y2h.handleCatch('OnSetDataError', 'SetData failed', 'Yes2SDK.Player.SetDataAsync'));
    },

    // Flush player data
    Yes2SDK_FlushDataAsyncJS__deps: ['$__y2h'],
    Yes2SDK_FlushDataAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnFlushDataError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.FlushDataAsync');
            return;
        }

        window.Yes2SDK.player.flushDataAsync()
            .then(function() {
                SendMessage('Bridge', 'OnFlushDataSuccess', '');
            })
            .catch(__y2h.handleCatch('OnFlushDataError', 'FlushData failed', 'Yes2SDK.Player.FlushDataAsync'));
    },

    // Get connected players
    Yes2SDK_GetConnectedPlayersAsyncJS__deps: ['$__y2h'],
    Yes2SDK_GetConnectedPlayersAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetConnectedPlayersError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetConnectedPlayersAsync');
            return;
        }

        window.Yes2SDK.player.getConnectedPlayersAsync()
            .then(function(players) {
                SendMessage('Bridge', 'OnGetConnectedPlayersSuccess', JSON.stringify(players));
            })
            .catch(__y2h.handleCatch('OnGetConnectedPlayersError', 'GetConnectedPlayers failed', 'Yes2SDK.Player.GetConnectedPlayersAsync'));
    },

    // Get signed player info
    Yes2SDK_GetSignedPlayerInfoAsyncJS__deps: ['$__y2h'],
    Yes2SDK_GetSignedPlayerInfoAsyncJS: function(payloadPtr) {
        var payload = UTF8ToString(payloadPtr);

        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetSignedPlayerInfoError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetSignedPlayerInfoAsync');
            return;
        }

        window.Yes2SDK.player.getSignedPlayerInfoAsync(payload)
            .then(function(info) {
                SendMessage('Bridge', 'OnGetSignedPlayerInfoSuccess', typeof info === 'string' ? info : JSON.stringify(info));
            })
            .catch(__y2h.handleCatch('OnGetSignedPlayerInfoError', 'GetSignedPlayerInfo failed', 'Yes2SDK.Player.GetSignedPlayerInfoAsync'));
    }

});
