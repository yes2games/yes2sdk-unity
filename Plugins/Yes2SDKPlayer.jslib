mergeInto(LibraryManager.library, {

    // Get player info asynchronously
    Yes2SDK_GetPlayerAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.GetPlayerAsync'
            });
            SendMessage('Bridge', 'OnGetPlayerError', errorJson);
            return;
        }

        window.Yes2SDK.player.getPlayerAsync()
            .then(function(player) {
                SendMessage('Bridge', 'OnGetPlayerSuccess', JSON.stringify(player));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetPlayer failed',
                    context: 'Yes2SDK.Player.GetPlayerAsync'
                });
                SendMessage('Bridge', 'OnGetPlayerError', errorJson);
            });
    },

    // Get player data
    Yes2SDK_GetDataAsyncJS: function(keysJsonPtr) {
        var keysJson = UTF8ToString(keysJsonPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.GetDataAsync'
            });
            SendMessage('Bridge', 'OnGetDataError', errorJson);
            return;
        }

        window.Yes2SDK.player.getDataAsync(keysJson)
            .then(function(data) {
                SendMessage('Bridge', 'OnGetDataSuccess', typeof data === 'string' ? data : JSON.stringify(data));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetData failed',
                    context: 'Yes2SDK.Player.GetDataAsync'
                });
                SendMessage('Bridge', 'OnGetDataError', errorJson);
            });
    },

    // Set player data
    Yes2SDK_SetDataAsyncJS: function(dataJsonPtr) {
        var dataJson = UTF8ToString(dataJsonPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.SetDataAsync'
            });
            SendMessage('Bridge', 'OnSetDataError', errorJson);
            return;
        }

        window.Yes2SDK.player.setDataAsync(dataJson)
            .then(function() {
                SendMessage('Bridge', 'OnSetDataSuccess', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'SetData failed',
                    context: 'Yes2SDK.Player.SetDataAsync'
                });
                SendMessage('Bridge', 'OnSetDataError', errorJson);
            });
    },

    // Flush player data
    Yes2SDK_FlushDataAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.FlushDataAsync'
            });
            SendMessage('Bridge', 'OnFlushDataError', errorJson);
            return;
        }

        window.Yes2SDK.player.flushDataAsync()
            .then(function() {
                SendMessage('Bridge', 'OnFlushDataSuccess', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'FlushData failed',
                    context: 'Yes2SDK.Player.FlushDataAsync'
                });
                SendMessage('Bridge', 'OnFlushDataError', errorJson);
            });
    },

    // Get connected players
    Yes2SDK_GetConnectedPlayersAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.GetConnectedPlayersAsync'
            });
            SendMessage('Bridge', 'OnGetConnectedPlayersError', errorJson);
            return;
        }

        window.Yes2SDK.player.getConnectedPlayersAsync()
            .then(function(players) {
                SendMessage('Bridge', 'OnGetConnectedPlayersSuccess', JSON.stringify(players));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetConnectedPlayers failed',
                    context: 'Yes2SDK.Player.GetConnectedPlayersAsync'
                });
                SendMessage('Bridge', 'OnGetConnectedPlayersError', errorJson);
            });
    },

    // Get signed player info
    Yes2SDK_GetSignedPlayerInfoAsyncJS: function(payloadPtr) {
        var payload = UTF8ToString(payloadPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.player === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Player module not loaded',
                context: 'Yes2SDK.Player.GetSignedPlayerInfoAsync'
            });
            SendMessage('Bridge', 'OnGetSignedPlayerInfoError', errorJson);
            return;
        }

        window.Yes2SDK.player.getSignedPlayerInfoAsync(payload)
            .then(function(info) {
                SendMessage('Bridge', 'OnGetSignedPlayerInfoSuccess', typeof info === 'string' ? info : JSON.stringify(info));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetSignedPlayerInfo failed',
                    context: 'Yes2SDK.Player.GetSignedPlayerInfoAsync'
                });
                SendMessage('Bridge', 'OnGetSignedPlayerInfoError', errorJson);
            });
    }

});
