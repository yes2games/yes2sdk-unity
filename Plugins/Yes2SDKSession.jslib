mergeInto(LibraryManager.library, {

    // Get locale/language code
    Yes2SDK_GetLocaleJS: function() {
        var locale = 'en';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            locale = window.Yes2SDK.session.getLocale() || 'en';
        }
        var bufferSize = lengthBytesUTF8(locale) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(locale, buffer, bufferSize);
        return buffer;
    },

    // Get country code
    Yes2SDK_GetCountryJS: function() {
        var country = '';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            country = window.Yes2SDK.session.getCountry() || '';
        }
        var bufferSize = lengthBytesUTF8(country) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(country, buffer, bufferSize);
        return buffer;
    },

    // Get device type
    Yes2SDK_GetDeviceJS: function() {
        var device = 'unknown';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            device = window.Yes2SDK.session.getDevice() || 'unknown';
        }
        var bufferSize = lengthBytesUTF8(device) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(device, buffer, bufferSize);
        return buffer;
    },

    // Get screen orientation
    Yes2SDK_GetOrientationJS: function() {
        var orientation = 'landscape';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            orientation = window.Yes2SDK.session.getOrientation() || 'landscape';
        }
        var bufferSize = lengthBytesUTF8(orientation) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(orientation, buffer, bufferSize);
        return buffer;
    },

    // Get traffic source as JSON
    Yes2SDK_GetTrafficSourceJS: function() {
        var json = '{"referrer":"","params":{}}';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            json = window.Yes2SDK.session.getTrafficSource() || json;
        }
        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    },

    // Get entry point data (URL params) as JSON
    Yes2SDK_GetEntryPointDataJS: function() {
        var json = '{}';
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            json = window.Yes2SDK.session.getEntryPointData() || json;
        }
        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    },

    // Store session data in memory
    Yes2SDK_SetSessionDataJS: function(dataJsonPtr) {
        var dataJson = UTF8ToString(dataJsonPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.session !== 'undefined') {
            window.Yes2SDK.session.setSessionData(dataJson);
        }
    },

    // Get entry point asynchronously
    Yes2SDK_GetEntryPointAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.session === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Session module not loaded',
                context: 'Yes2SDK.Session.GetEntryPointAsync'
            });
            SendMessage('Bridge', 'OnGetEntryPointError', errorJson);
            return;
        }

        window.Yes2SDK.session.getEntryPointAsync()
            .then(function(entryPoint) {
                SendMessage('Bridge', 'OnGetEntryPointSuccess', entryPoint || 'direct');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetEntryPoint failed',
                    context: 'Yes2SDK.Session.GetEntryPointAsync'
                });
                SendMessage('Bridge', 'OnGetEntryPointError', errorJson);
            });
    }

});
