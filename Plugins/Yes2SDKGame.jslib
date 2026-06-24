mergeInto(LibraryManager.library, {

    Yes2SDK_Game_GameplayStartJS__deps: ['$__y2h'],
    Yes2SDK_Game_GameplayStartJS: function() {
        if (__y2h.has('game')) window.Yes2SDK.game.gameplayStart();
    },

    Yes2SDK_Game_GameplayStopJS__deps: ['$__y2h'],
    Yes2SDK_Game_GameplayStopJS: function() {
        if (__y2h.has('game')) window.Yes2SDK.game.gameplayStop();
    },

    Yes2SDK_Game_HappyTimeJS__deps: ['$__y2h'],
    Yes2SDK_Game_HappyTimeJS: function() {
        if (__y2h.has('game')) window.Yes2SDK.game.happyTime();
    },

    Yes2SDK_Game_InviteLinkAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Game_InviteLinkAsyncJS: function(paramsJsonPtr) {
        var paramsJson = UTF8ToString(paramsJsonPtr);

        if (!__y2h.has('game')) {
            __y2h.sendError('OnInviteLinkError', 'NotInitialized', 'Yes2SDK Game module not loaded', 'Yes2SDK.Game.InviteLinkAsync');
            return;
        }

        window.Yes2SDK.game.inviteLink(paramsJson)
            .then(function(link) {
                SendMessage('Bridge', 'OnInviteLinkSuccess', link || '');
            })
            .catch(__y2h.handleCatch('OnInviteLinkError', 'InviteLink failed', 'Yes2SDK.Game.InviteLinkAsync'));
    },

    Yes2SDK_Game_GetInviteParamJS__deps: ['$__y2h'],
    Yes2SDK_Game_GetInviteParamJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        var result = '';
        if (__y2h.has('game')) result = window.Yes2SDK.game.getInviteParam(key) || '';
        return __y2h.returnStr(result);
    },

    Yes2SDK_Game_ShowInviteButtonJS__deps: ['$__y2h'],
    Yes2SDK_Game_ShowInviteButtonJS: function(paramsJsonPtr) {
        var paramsJson = UTF8ToString(paramsJsonPtr);
        if (__y2h.has('game')) window.Yes2SDK.game.showInviteButton(paramsJson);
    },

    Yes2SDK_Game_HideInviteButtonJS__deps: ['$__y2h'],
    Yes2SDK_Game_HideInviteButtonJS: function() {
        if (__y2h.has('game')) window.Yes2SDK.game.hideInviteButton();
    },

    Yes2SDK_Game_GetSettingsJS__deps: ['$__y2h'],
    Yes2SDK_Game_GetSettingsJS: function() {
        var json = '{"disableChat":false,"muteAudio":false}';
        if (__y2h.has('game')) json = window.Yes2SDK.game.getSettings() || json;
        return __y2h.returnStr(json);
    },

    Yes2SDK_Game_CopyToClipboardJS__deps: ['$__y2h'],
    Yes2SDK_Game_CopyToClipboardJS: function(textPtr) {
        var text = UTF8ToString(textPtr);
        if (__y2h.has('game')) window.Yes2SDK.game.copyToClipboard(text);
    },

    Yes2SDK_Game_GetServerTimeAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Game_GetServerTimeAsyncJS: function() {
        if (!__y2h.has('game')) {
            __y2h.sendError('OnGetServerTimeError', 'NotInitialized', 'Yes2SDK Game module not loaded', 'Yes2SDK.Game.GetServerTimeAsync');
            return;
        }

        window.Yes2SDK.game.getServerTimeAsync()
            .then(function(time) {
                SendMessage('Bridge', 'OnGetServerTimeSuccess', String(time));
            })
            .catch(__y2h.handleCatch('OnGetServerTimeError', 'GetServerTime failed', 'Yes2SDK.Game.GetServerTimeAsync'));
    }

});
