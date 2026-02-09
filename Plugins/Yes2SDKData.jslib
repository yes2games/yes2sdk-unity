mergeInto(LibraryManager.library, {

    Yes2SDK_Data_GetIntJS: function(keyPtr, defaultValue) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.getInt(key, defaultValue);
        }
        return defaultValue;
    },

    Yes2SDK_Data_SetIntJS: function(keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setInt(key, value);
        }
    },

    Yes2SDK_Data_GetFloatJS: function(keyPtr, defaultValue) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.getFloat(key, defaultValue);
        }
        return defaultValue;
    },

    Yes2SDK_Data_SetFloatJS: function(keyPtr, value) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setFloat(key, value);
        }
    },

    Yes2SDK_Data_GetStringJS: function(keyPtr, defaultValuePtr) {
        var key = UTF8ToString(keyPtr);
        var defaultValue = UTF8ToString(defaultValuePtr);
        var result = defaultValue;
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            result = window.Yes2SDK.data.getString(key, defaultValue);
        }
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    Yes2SDK_Data_SetStringJS: function(keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.setString(key, value);
        }
    },

    Yes2SDK_Data_HasKeyJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            return window.Yes2SDK.data.hasKey(key) ? 1 : 0;
        }
        return 0;
    },

    Yes2SDK_Data_DeleteKeyJS: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.deleteKey(key);
        }
    },

    Yes2SDK_Data_DeleteAllJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.data !== 'undefined') {
            window.Yes2SDK.data.deleteAll();
        }
    }

});
