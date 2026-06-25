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
    },

    // Whether player data persistence is supported on the current platform
    Yes2SDK_IsDataSupportedJS__deps: ['$__y2h'],
    Yes2SDK_IsDataSupportedJS: function() {
        return (__y2h.has('player') && typeof window.Yes2SDK.player.isDataSupported === 'function' && window.Yes2SDK.player.isDataSupported()) ? 1 : 0;
    },

    // Whether connected players are supported on the current platform
    Yes2SDK_IsConnectedPlayersSupportedJS__deps: ['$__y2h'],
    Yes2SDK_IsConnectedPlayersSupportedJS: function() {
        return (__y2h.has('player') && typeof window.Yes2SDK.player.isConnectedPlayersSupported === 'function' && window.Yes2SDK.player.isConnectedPlayersSupported()) ? 1 : 0;
    },

    // Get a stable unique player id
    Yes2SDK_Player_GetUniqueIdAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Player_GetUniqueIdAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetUniqueIdError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetUniqueIdAsync');
            return;
        }

        window.Yes2SDK.player.getUniqueId()
            .then(function(id) {
                SendMessage('Bridge', 'OnGetUniqueIdSuccess', id || '');
            })
            .catch(__y2h.handleCatch('OnGetUniqueIdError', 'GetUniqueId failed', 'Yes2SDK.Player.GetUniqueIdAsync'));
    },

    // Get the player's cross-game identities
    Yes2SDK_Player_GetIDsPerGameAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Player_GetIDsPerGameAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetIDsPerGameError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetIDsPerGameAsync');
            return;
        }

        window.Yes2SDK.player.getIDsPerGame()
            .then(function(ids) {
                SendMessage('Bridge', 'OnGetIDsPerGameSuccess', JSON.stringify(ids || []));
            })
            .catch(__y2h.handleCatch('OnGetIDsPerGameError', 'GetIDsPerGame failed', 'Yes2SDK.Player.GetIDsPerGameAsync'));
    },

    // Get the player's paying status
    Yes2SDK_Player_GetPayingStatusAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Player_GetPayingStatusAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetPayingStatusError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetPayingStatusAsync');
            return;
        }

        window.Yes2SDK.player.getPayingStatus()
            .then(function(status) {
                SendMessage('Bridge', 'OnGetPayingStatusSuccess', status || 'unknown');
            })
            .catch(__y2h.handleCatch('OnGetPayingStatusError', 'GetPayingStatus failed', 'Yes2SDK.Player.GetPayingStatusAsync'));
    },

    // Get the player's session mode
    Yes2SDK_Player_GetModeAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Player_GetModeAsyncJS: function() {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetModeError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetModeAsync');
            return;
        }

        window.Yes2SDK.player.getMode()
            .then(function(mode) {
                SendMessage('Bridge', 'OnGetModeSuccess', mode || 'unknown');
            })
            .catch(__y2h.handleCatch('OnGetModeError', 'GetMode failed', 'Yes2SDK.Player.GetModeAsync'));
    },

    // Get the player's avatar URL for the requested size
    Yes2SDK_Player_GetPhotoAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Player_GetPhotoAsyncJS: function(sizePtr) {
        if (!__y2h.has('player')) {
            __y2h.sendError('OnGetPhotoError', 'NotInitialized', 'Yes2SDK Player module not loaded', 'Yes2SDK.Player.GetPhotoAsync');
            return;
        }

        var size = UTF8ToString(sizePtr);

        window.Yes2SDK.player.getPhoto(size || undefined)
            .then(function(photo) {
                SendMessage('Bridge', 'OnGetPhotoSuccess', (photo === null || photo === undefined) ? 'null' : photo);
            })
            .catch(__y2h.handleCatch('OnGetPhotoError', 'GetPhoto failed', 'Yes2SDK.Player.GetPhotoAsync'));
    }

});
