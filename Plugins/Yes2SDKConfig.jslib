mergeInto(LibraryManager.library, {

    Yes2SDK_Config_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Config_IsSupportedJS: function() {
        return __y2h.has('config') && window.Yes2SDK.config.isSupported() ? 1 : 0;
    },

    Yes2SDK_Config_GetFlagsAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Config_GetFlagsAsyncJS: function(optionsJsonPtr) {
        if (!__y2h.has('config')) {
            __y2h.sendError('OnGetFlagsError', 'NotInitialized', 'Yes2SDK Config module not loaded', 'Yes2SDK.Config.GetFlagsAsync');
            return;
        }

        var options;
        try { options = JSON.parse(UTF8ToString(optionsJsonPtr)) || {}; }
        catch (e) { options = {}; }

        window.Yes2SDK.config.getFlagsAsync(options)
            .then(function(flags) {
                SendMessage('Bridge', 'OnGetFlagsSuccess', JSON.stringify(flags || {}));
            })
            .catch(__y2h.handleCatch('OnGetFlagsError', 'GetFlags failed', 'Yes2SDK.Config.GetFlagsAsync'));
    }

});
