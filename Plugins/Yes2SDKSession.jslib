mergeInto(LibraryManager.library, {

    // Get locale/language code
    Yes2SDK_GetLocaleJS__deps: ['$__y2h'],
    Yes2SDK_GetLocaleJS: function() {
        var locale = 'en';
        if (__y2h.has('session')) locale = window.Yes2SDK.session.getLocale() || 'en';
        return __y2h.returnStr(locale);
    },

    // Get country code
    Yes2SDK_GetCountryJS__deps: ['$__y2h'],
    Yes2SDK_GetCountryJS: function() {
        var country = '';
        if (__y2h.has('session')) country = window.Yes2SDK.session.getCountry() || '';
        return __y2h.returnStr(country);
    },

    // Get device type
    Yes2SDK_GetDeviceJS__deps: ['$__y2h'],
    Yes2SDK_GetDeviceJS: function() {
        var device = 'unknown';
        if (__y2h.has('session')) device = window.Yes2SDK.session.getDevice() || 'unknown';
        return __y2h.returnStr(device);
    },

    // Get screen orientation
    Yes2SDK_GetOrientationJS__deps: ['$__y2h'],
    Yes2SDK_GetOrientationJS: function() {
        var orientation = 'landscape';
        if (__y2h.has('session')) orientation = window.Yes2SDK.session.getOrientation() || 'landscape';
        return __y2h.returnStr(orientation);
    },

    // Get traffic source as JSON
    Yes2SDK_GetTrafficSourceJS__deps: ['$__y2h'],
    Yes2SDK_GetTrafficSourceJS: function() {
        var json = '{"referrer":"","params":{}}';
        if (__y2h.has('session')) json = window.Yes2SDK.session.getTrafficSource() || json;
        return __y2h.returnStr(json);
    },

    // Get entry point data (URL params) as JSON
    Yes2SDK_GetEntryPointDataJS__deps: ['$__y2h'],
    Yes2SDK_GetEntryPointDataJS: function() {
        var json = '{}';
        if (__y2h.has('session')) json = window.Yes2SDK.session.getEntryPointData() || json;
        return __y2h.returnStr(json);
    },

    // Store session data in memory
    Yes2SDK_SetSessionDataJS__deps: ['$__y2h'],
    Yes2SDK_SetSessionDataJS: function(dataJsonPtr) {
        var dataJson = UTF8ToString(dataJsonPtr);
        if (__y2h.has('session')) window.Yes2SDK.session.setSessionData(dataJson);
    },

    // Get entry point asynchronously
    Yes2SDK_GetEntryPointAsyncJS__deps: ['$__y2h'],
    Yes2SDK_GetEntryPointAsyncJS: function() {
        if (!__y2h.has('session')) {
            __y2h.sendError('OnGetEntryPointError', 'NotInitialized', 'Yes2SDK Session module not loaded', 'Yes2SDK.Session.GetEntryPointAsync');
            return;
        }

        window.Yes2SDK.session.getEntryPointAsync()
            .then(function(entryPoint) {
                SendMessage('Bridge', 'OnGetEntryPointSuccess', entryPoint || 'direct');
            })
            .catch(__y2h.handleCatch('OnGetEntryPointError', 'GetEntryPoint failed', 'Yes2SDK.Session.GetEntryPointAsync'));
    },

    // Check if platform audio is enabled. Required for YouTube Playables cert (#14):
    // game must read this at startup to set its initial mute state.
    // Returns 1 (true) on platforms without a native signal so games don't mute by accident.
    Yes2SDK_IsAudioEnabledJS__deps: ['$__y2h'],
    Yes2SDK_IsAudioEnabledJS: function() {
        try {
            if (__y2h.has('session') && typeof window.Yes2SDK.session.isAudioEnabled === 'function') {
                return window.Yes2SDK.session.isAudioEnabled() ? 1 : 0;
            }
        } catch (e) {}
        return 1;
    }

});
