mergeInto(LibraryManager.library, {

    // Callback function pointers (set by Yes2SDK_RegisterCallbacksJS)
    $Yes2SDK_Callbacks: {
        onInitializeSuccess: null,
        onInitializeError: null,
        onStartGameSuccess: null,
        onStartGameError: null,
        onPause: null,
        onResume: null
    },

    // Register C# callback function pointers
    Yes2SDK_RegisterCallbacksJS: function() {
        // Store callback references for later use
        // These will be called via SendMessage or dynCall
    },

    // Initialize the SDK
    Yes2SDK_InitializeJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded. Make sure yes2sdk.umd.js is included in the WebGL template.');
            // Send error back to Unity
            var error = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Core not loaded',
                context: 'Yes2SDK.InitializeAsync'
            });
            SendMessage('Bridge', 'OnInitializeError', error);
            return;
        }

        window.Yes2SDK.initializeAsync()
            .then(function() {
                SendMessage('Bridge', 'OnInitializeSuccess', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Initialization failed',
                    context: 'Yes2SDK.InitializeAsync'
                });
                SendMessage('Bridge', 'OnInitializeError', errorJson);
            });
    },

    // Start the game
    Yes2SDK_StartGameJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded.');
            return;
        }

        window.Yes2SDK.startGameAsync()
            .then(function() {
                SendMessage('Bridge', 'OnStartGameSuccess', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Start game failed',
                    context: 'Yes2SDK.StartGameAsync'
                });
                SendMessage('Bridge', 'OnStartGameError', errorJson);
            });
    },

    // Set loading progress (0-100)
    Yes2SDK_SetLoadingProgressJS: function(progress) {
        if (typeof window.Yes2SDK === 'undefined') {
            return;
        }

        window.Yes2SDK.setLoadingProgress(progress);
    },

    // Trigger haptic feedback
    Yes2SDK_PerformHapticFeedbackJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            return;
        }

        window.Yes2SDK.performHapticFeedback();
    },

    // Get current platform
    Yes2SDK_GetPlatformJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            var unknownStr = 'unknown';
            var bufferSize = lengthBytesUTF8(unknownStr) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(unknownStr, buffer, bufferSize);
            return buffer;
        }

        var platform = window.Yes2SDK.getPlatform();
        var platformStr = platform ? platform.toString().toLowerCase() : 'unknown';
        var bufferSize = lengthBytesUTF8(platformStr) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformStr, buffer, bufferSize);
        return buffer;
    },

    // Check if SDK is initialized
    Yes2SDK_IsInitializedJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            return false;
        }

        return window.Yes2SDK.isInitialized === true;
    }

});
