mergeInto(LibraryManager.library, {

    Yes2SDK_Data_GetIntJS__deps: ['$__y2'],
    Yes2SDK_Data_GetIntJS: function(keyPtr, defaultValue) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.getInt(key, defaultValue);
        }
        window.__y2.warn('Data module not loaded — returning default for key:', key);
        return defaultValue;
    },

    Yes2SDK_Data_SetIntJS__deps: ['$__y2'],
    Yes2SDK_Data_SetIntJS: function(keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setInt(key, value);
            return;
        }
        window.__y2.warn('Data module not loaded — ignoring setInt for key:', key);
    },

    Yes2SDK_Data_GetFloatJS__deps: ['$__y2'],
    Yes2SDK_Data_GetFloatJS: function(keyPtr, defaultValue) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.getFloat(key, defaultValue);
        }
        window.__y2.warn('Data module not loaded — returning default for key:', key);
        return defaultValue;
    },

    Yes2SDK_Data_SetFloatJS__deps: ['$__y2'],
    Yes2SDK_Data_SetFloatJS: function(keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setFloat(key, value);
            return;
        }
        window.__y2.warn('Data module not loaded — ignoring setFloat for key:', key);
    },

    Yes2SDK_Data_GetStringJS__deps: ['$__y2'],
    Yes2SDK_Data_GetStringJS: function(keyPtr, defaultValuePtr) {
        var key = UTF8ToString(keyPtr);
        var defaultValue = UTF8ToString(defaultValuePtr);
        var result = defaultValue;
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            result = window.Yes2SDK.data.getString(key, defaultValue);
        } else {
            window.__y2.warn('Data module not loaded — returning default for key:', key);
        }
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    Yes2SDK_Data_SetStringJS__deps: ['$__y2'],
    Yes2SDK_Data_SetStringJS: function(keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setString(key, value);
            return;
        }
        window.__y2.warn('Data module not loaded — ignoring setString for key:', key);
    },

    Yes2SDK_Data_HasKeyJS__deps: ['$__y2'],
    Yes2SDK_Data_HasKeyJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.hasKey(key) ? 1 : 0;
        }
        window.__y2.warn('Data module not loaded — returning false for hasKey:', key);
        return 0;
    },

    Yes2SDK_Data_DeleteKeyJS__deps: ['$__y2'],
    Yes2SDK_Data_DeleteKeyJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.deleteKey(key);
            return;
        }
        window.__y2.warn('Data module not loaded — ignoring deleteKey for key:', key);
    },

    Yes2SDK_Data_DeleteAllJS__deps: ['$__y2'],
    Yes2SDK_Data_DeleteAllJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.deleteAll();
            return;
        }
        window.__y2.warn('Data module not loaded — ignoring deleteAll');
    }

});
