mergeInto(LibraryManager.library, {

    // Styled console logger — lazy-init in case template hasn't set it up
    $__y2__postset: '(function(){if(window.__y2)return;var S="background:#6C5CE7;color:#fff;padding:2px 6px;border-radius:3px;font-weight:bold";function m(f){return function(){var a=[].slice.call(arguments);a.unshift("%c[Yes2SDK]%c ",S,"");console[f].apply(console,a);};}window.__y2={log:m("log"),warn:m("warn"),error:m("error")};})();',
    $__y2: {},

    // Initialize the SDK
    Yes2SDK_InitializeJS__deps: ['$__y2', '$__yes2PlatformInit'],
    Yes2SDK_InitializeJS: function() {
        window.__y2.log('[Init] Step 1: Yes2SDK_InitializeJS called');

        // Helper: run init once wrapper exists
        function doInit() {
            window.__y2.log('[Init] doInit: calling window.Yes2SDK.initializeAsync()');
            window.Yes2SDK.initializeAsync()
                .then(function() {
                    window.__y2.log('[Init] initializeAsync succeeded');
                    SendMessage('Bridge', 'OnInitializeSuccess', '');
                })
                .catch(function(error) {
                    window.__y2.error('[Init] initializeAsync failed:', error);
                    var errorJson = JSON.stringify({
                        code: error.code || 'Unknown',
                        message: error.message || 'Initialization failed',
                        context: 'Yes2SDK.InitializeAsync'
                    });
                    SendMessage('Bridge', 'OnInitializeError', errorJson);
                });
        }

        // Helper: check if CG SDK is available and try to create wrapper
        function tryCreateWrapper() {
            if (typeof window.Yes2SDK !== 'undefined') return true;
            if (typeof window.__yes2PlatformInit === 'function') {
                window.__yes2PlatformInit();
            }
            return (typeof window.Yes2SDK !== 'undefined');
        }

        // A. Template already created wrapper (Poki, Debug, local CG with HTML)
        if (typeof window.Yes2SDK !== 'undefined') {
            window.__y2.log('[Init] Step 2a: window.Yes2SDK exists (from template)');
            doInit();
            return;
        }

        // B. Try lazy platform init (CG SDK may have loaded between postset and now)
        window.__y2.log('[Init] Step 2b: window.Yes2SDK not found, trying lazy init...');
        if (tryCreateWrapper()) {
            window.__y2.log('[Init] Step 2b: wrapper created via lazy init');
            doInit();
            return;
        }

        // C. CG SDK not available yet.
        //    CrazyGames injects their SDK ASYNCHRONOUSLY after framework.js loads.
        //    The platform also BLOCKS CDN loads (ERR_CONNECTION_RESET), so we
        //    primarily rely on polling for the platform-injected SDK.
        //
        //    Strategy: Poll for window.CrazyGames.SDK with auto-retry.
        //    - Round 1: poll 100ms x 150 = 15 seconds
        //    - Round 2 (auto-retry): wait 3s, then poll 100ms x 150 = 15 seconds
        //    - CDN load attempted in parallel (works on some CG environments)
        //    - Total max wait: ~35 seconds before giving up

        window.__y2.log('[Init] Step 3: CG SDK not available yet.',
            'Starting detection (poll + CDN)...',
            'CrazyGames:', typeof window.CrazyGames,
            'CrazyGamesAds:', typeof window.CrazyGamesAds);

        var resolved = false;
        var currentPollTimer = null;
        var retryCount = 0;
        var maxRetries = 1; // 1 auto-retry after first failure

        function onSDKAvailable(source) {
            if (resolved) return;
            resolved = true;
            if (currentPollTimer) clearInterval(currentPollTimer);

            window.__y2.log('[Init] Step 4: CG SDK detected via', source,
                'CrazyGames.SDK:', typeof (window.CrazyGames && window.CrazyGames.SDK));

            // Create the Yes2SDK wrapper
            if (tryCreateWrapper()) {
                window.__y2.log('[Init] Step 5: wrapper created via', source, '— calling doInit');
                doInit();
            } else {
                window.__y2.error('[Init] Step 5: FAILED — CG SDK found via', source,
                    'but wrapper creation failed.',
                    'CrazyGames:', typeof window.CrazyGames,
                    'CrazyGames.SDK:', window.CrazyGames ? typeof window.CrazyGames.SDK : 'N/A');
                SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                    code: 'NotInitialized',
                    message: 'CrazyGames SDK found (' + source + ') but wrapper creation failed',
                    context: 'Yes2SDK.InitializeAsync'
                }));
            }
        }

        function onAllFailed(reason) {
            if (resolved) return;
            resolved = true;
            if (currentPollTimer) clearInterval(currentPollTimer);

            window.__y2.error('[Init] FAILED:', reason,
                'CrazyGames:', typeof window.CrazyGames,
                'CrazyGamesAds:', typeof window.CrazyGamesAds);
            SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                code: 'NotInitialized',
                message: reason,
                context: 'Yes2SDK.InitializeAsync'
            }));
        }

        function startPolling(round) {
            var pollAttempts = 0;
            var maxPollAttempts = 150; // 15 seconds at 100ms
            window.__y2.log('[Init] Poll round', round + 1, 'started (150 attempts, 15s)');

            currentPollTimer = setInterval(function() {
                if (resolved) { clearInterval(currentPollTimer); return; }
                pollAttempts++;
                if (typeof window.CrazyGames !== 'undefined' && window.CrazyGames.SDK) {
                    clearInterval(currentPollTimer);
                    onSDKAvailable('platform-injection (round ' + (round + 1) + ', poll #' + pollAttempts + ')');
                } else if (pollAttempts >= maxPollAttempts) {
                    clearInterval(currentPollTimer);
                    window.__y2.warn('[Init] Poll round', round + 1, 'exhausted after', maxPollAttempts, 'attempts (15s).',
                        'CrazyGames:', typeof window.CrazyGames,
                        'CrazyGamesAds:', typeof window.CrazyGamesAds);

                    if (round < maxRetries) {
                        // Auto-retry: wait 3s then poll again
                        retryCount++;
                        window.__y2.log('[Init] Auto-retry', retryCount, '— waiting 3s before next round...');
                        setTimeout(function() {
                            if (resolved) return;
                            startPolling(round + 1);
                        }, 3000);
                    } else {
                        onAllFailed('CrazyGames SDK not found after ' + (round + 1) + ' poll rounds (~' +
                            ((round + 1) * 15 + round * 3) + 's). Platform may not have injected SDK.');
                    }
                }
            }, 100);
        }

        // Start polling (primary strategy)
        startPolling(0);

        // CDN load (secondary — may be blocked by CG platform, but try anyway)
        var script = document.createElement('script');
        script.src = 'https://sdk.crazygames.com/crazygames-sdk-v3.js';
        script.addEventListener('load', function() {
            if (resolved) return;
            window.__y2.log('[Init] CDN script loaded.',
                'CrazyGames:', typeof window.CrazyGames,
                'CrazyGames.SDK:', window.CrazyGames ? typeof window.CrazyGames.SDK : 'N/A');
            if (typeof window.CrazyGames !== 'undefined' && window.CrazyGames.SDK) {
                onSDKAvailable('CDN');
            } else {
                window.__y2.warn('[Init] CDN loaded but CrazyGames.SDK not found — continuing poll');
            }
        });
        script.addEventListener('error', function() {
            if (resolved) return;
            window.__y2.warn('[Init] CDN load failed (CG platform blocks external SDK loads).',
                'Relying on platform injection polling...');
        });
        document.head.appendChild(script);
        window.__y2.log('[Init] Step 3: CDN <script> appended + polling started');
    },

    // Start the game
    Yes2SDK_StartGameJS__deps: ['$__y2'],
    Yes2SDK_StartGameJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            window.__y2.error('Core SDK not loaded.');
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
