mergeInto(LibraryManager.library, {

    Yes2SDK_Game_GameplayStartJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.gameplayStart();
        }
    },

    Yes2SDK_Game_GameplayStopJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.gameplayStop();
        }
    },

    Yes2SDK_Game_HappyTimeJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.happyTime();
        }
    },

    Yes2SDK_Game_InviteLinkAsyncJS: function(paramsJsonPtr) {
        var paramsJson = UTF8ToString(paramsJsonPtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.game === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Game module not loaded',
                context: 'Yes2SDK.Game.InviteLinkAsync'
            });
            SendMessage('Bridge', 'OnInviteLinkError', errorJson);
            return;
        }

        window.Yes2SDK.game.inviteLink(paramsJson)
            .then(function(link) {
                SendMessage('Bridge', 'OnInviteLinkSuccess', link || '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'InviteLink failed',
                    context: 'Yes2SDK.Game.InviteLinkAsync'
                });
                SendMessage('Bridge', 'OnInviteLinkError', errorJson);
            });
    },

    Yes2SDK_Game_GetInviteParamJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        var result = '';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            result = window.Yes2SDK.game.getInviteParam(key) || '';
        }
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    Yes2SDK_Game_ShowInviteButtonJS: function(paramsJsonPtr) {
        var paramsJson = UTF8ToString(paramsJsonPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.showInviteButton(paramsJson);
        }
    },

    Yes2SDK_Game_HideInviteButtonJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.hideInviteButton();
        }
    },

    Yes2SDK_Game_GetSettingsJS: function() {
        var json = '{"disableChat":false,"muteAudio":false}';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            json = window.Yes2SDK.game.getSettings() || json;
        }
        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    },

    Yes2SDK_Game_CopyToClipboardJS: function(textPtr) {
        var text = UTF8ToString(textPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.game !== 'undefined') {
            window.Yes2SDK.game.copyToClipboard(text);
        }
    }

});
