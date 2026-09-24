mergeInto(LibraryManager.library, {

    // Styled console logger + error boundary — lazy-init in case template hasn't set it up.
    // The error boundary catches known platform SDK errors (CrazyGames GeneralError,
    // sdkDisabled, etc.) BEFORE Unity's error handler can turn them into alert() popups.
    $__y2__postset: '\
(function(){\
  if(window.__y2)return;\
  var S="background:#6C5CE7;color:#fff;padding:2px 6px;border-radius:3px;font-weight:bold";\
  function m(f){return function(){var a=[].slice.call(arguments);a.unshift("%c[Yes2SDK]%c ",S,"");console[f].apply(console,a);};}\
  window.__y2={log:m("log"),warn:m("warn"),error:m("error")};\
  function isSdkError(e){\
    return e&&typeof e==="object"&&typeof e.code==="string"&&typeof e.message==="string";\
  }\
  window.addEventListener("error",function(ev){\
    if(isSdkError(ev.error)){window.__y2.error("Platform SDK error (suppressed popup):",ev.error.code,ev.error.message);ev.preventDefault();}\
  });\
  window.addEventListener("unhandledrejection",function(ev){\
    if(isSdkError(ev.reason)){window.__y2.error("Platform SDK rejection (suppressed):",ev.reason.code,ev.reason.message);ev.preventDefault();}\
  });\
})();',
    $__y2: {},

    // Shared helpers for jslib bridges — use bare __y2h in jslib (NOT window.__y2h)
    $__y2h: {
        // Check if SDK and optional module are loaded
        has: function(mod) {
            return typeof window.Yes2SDK !== 'undefined' &&
                   (!mod || typeof window.Yes2SDK[mod] !== 'undefined');
        },
        // Send a structured error to Bridge via SendMessage
        sendError: function(callback, code, message, context) {
            SendMessage('Bridge', callback, JSON.stringify({ code: code, message: message, context: context }));
        },
        // Return a catch handler that sends error to Bridge
        handleCatch: function(callback, defaultMessage, context) {
            return function(error) {
                SendMessage('Bridge', callback, JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || defaultMessage,
                    context: context
                }));
            };
        },
        // Resume Unity's WebAudio context when an ad surface left it suspended.
        // The browser suspends the page context while the ad holds focus, and
        // nothing resumes it, so game audio stays silent until a manual tab
        // blur and refocus. resume() is idempotent, so calling this on every ad
        // exit is safe. The state is logged because the autoplay policy can
        // defer a resume that has no user gesture behind it, and the log is the
        // only way to tell that apart from a context that was never suspended.
        resumeAudio: function() {
            try {
                var ctx = (typeof WEBAudio !== 'undefined' && WEBAudio) ? WEBAudio.audioContext : null;
                if (!ctx) {
                    return;
                }
                window.__y2.log('[Audio] AudioContext state on ad exit: ' + ctx.state);
                if (ctx.state === 'suspended' && typeof ctx.resume === 'function') {
                    var pending = ctx.resume();
                    if (pending && typeof pending.catch === 'function') {
                        pending.catch(function(error) {
                            window.__y2.warn('[Audio] AudioContext resume deferred:', error);
                        });
                    }
                }
            } catch(error) {
                window.__y2.warn('[Audio] AudioContext resume failed:', error);
            }
        },
        // Allocate and return a UTF8 string to C#
        returnStr: function(str) {
            var bufferSize = lengthBytesUTF8(str) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(str, buffer, bufferSize);
            return buffer;
        }
    },

    // Initialize the SDK
    Yes2SDK_InitializeJS__deps: ['$__y2', '$__yes2PlatformInit'],
    Yes2SDK_InitializeJS: function() {
        window.__y2.log('[Init] Step 1: Yes2SDK_InitializeJS called');

        // Minimum injected Core runtime this wrapper build is compatible with.
        // Distinct from the wrapper's own version (Yes2SDK.Version) — see Yes2SDK.cs.
        // Must match the dashboard's MIN_CORE_BY_ENGINE floor for Unity.
        var REQUIRED_CORE_VERSION = '2.2.0';

        // Compare two semver strings on major.minor.patch (pre-release/build metadata ignored).
        // Returns 1 if a > b, -1 if a < b, 0 if equal.
        function compareSemver(a, b) {
            var pa = String(a).split('.');
            var pb = String(b).split('.');
            for (var i = 0; i < 3; i++) {
                var na = parseInt(pa[i], 10) || 0;
                var nb = parseInt(pb[i], 10) || 0;
                if (na > nb) return 1;
                if (na < nb) return -1;
            }
            return 0;
        }

        // Warn (non-blocking) if the injected Core is older than this build requires.
        // Reads Core's OWN version field, which is Core-only: the CrazyGames wrapper
        // exposes a second window.Yes2SDK with no .version, and pre-2.2.0 Core has no
        // getter either — both read as null, which means "can't verify", NOT a skew.
        function checkCoreVersion() {
            try {
                var coreVer = (window.Yes2SDK && typeof window.Yes2SDK.version === 'string')
                    ? window.Yes2SDK.version : null;
                if (coreVer === null) return; // CG wrapper or pre-2.2.0 Core — cannot verify, skip
                if (compareSemver(coreVer, REQUIRED_CORE_VERSION) < 0) {
                    window.__y2.warn('Injected Core v' + coreVer + ' is older than this build requires (v' +
                        REQUIRED_CORE_VERSION + '). Some SDK calls may silently no-op. ' +
                        'Update the injected Core runtime.');
                }
            } catch (error) {
                // A version probe must never break init.
                window.__y2.warn('[Init] Core version check skipped:', error);
            }
        }

        // Helper: subscribe to platform lifecycle events ONCE after init succeeds.
        // Required for YouTube Playables certification (integration #14, #21, #22).
        // The Core SDK fires these events; we forward them to Unity via SendMessage.
        function wireLifecycleEvents() {
            try {
                if (typeof window.Yes2SDK.on !== 'function') {
                    window.__y2.warn('[Lifecycle] Yes2SDK.on not available; pause/resume/audio events will not fire.');
                    return;
                }
                window.Yes2SDK.on('pause', function() {
                    SendMessage('Bridge', 'OnPause', '');
                });
                window.Yes2SDK.on('resume', function() {
                    SendMessage('Bridge', 'OnResume', '');
                });
                window.Yes2SDK.on('audioEnabledChange', function(data) {
                    var enabled = (data && data.enabled) === true;
                    SendMessage('Bridge', 'OnAudioEnabledChange', JSON.stringify({ enabled: enabled }));
                });
                window.Yes2SDK.on('accountDialogOpen', function() {
                    SendMessage('Bridge', 'OnAccountDialogOpen', '');
                });
                window.Yes2SDK.on('accountDialogClose', function() {
                    SendMessage('Bridge', 'OnAccountDialogClose', '');
                });
                window.__y2.log('[Lifecycle] pause/resume/audioEnabledChange/accountDialogOpen/accountDialogClose wired to Bridge');
            } catch(error) {
                window.__y2.error('[Lifecycle] failed to wire lifecycle events:', error);
            }
        }

        // Helper: run init once wrapper exists (try-catch prevents uncaught throws → no popup)
        function doInit() {
            // window.Yes2SDK is guaranteed defined here (paths A/B/C confirm the wrapper
            // before calling doInit), so this is the single point to verify Core skew.
            checkCoreVersion();
            try {
                window.__y2.log('[Init] doInit: calling window.Yes2SDK.initializeAsync()');
                window.Yes2SDK.initializeAsync()
                    .then(function() {
                        window.__y2.log('[Init] initializeAsync succeeded');
                        wireLifecycleEvents();
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
            } catch(error) {
                window.__y2.error('[Init] initializeAsync threw:', error);
                SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || String(error),
                    context: 'Yes2SDK.InitializeAsync'
                }));
            }
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

        // C. No wrapper found. Check for CrazyGames signals before attempting
        //    CG-specific detection. CrazyGames replaces the HTML template so
        //    window.Yes2SDK won't exist, but we must NOT blindly load the CG SDK
        //    from CDN on non-CG domains — that causes sdkDisabled crashes.

        // C.0: Dashboard config exists but SDK object failed to load
        if (typeof window.__yes2sdkConfig !== 'undefined') {
            var cfgPlatform = window.__yes2sdkConfig.platform || 'unknown';
            window.__y2.error('[Init] Dashboard config found (platform: ' + cfgPlatform +
                ') but window.Yes2SDK is not defined.',
                'The yes2sdk.umd.js file may have failed to load.');
            SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                code: 'SDKLoadFailed',
                message: 'Dashboard config found (platform: ' + cfgPlatform +
                    ') but yes2sdk.umd.js failed to load. Check browser console for script errors.',
                context: 'Yes2SDK.InitializeAsync'
            }));
            return;
        }

        // C.1: Check for CrazyGames signals before attempting CG detection
        var hasCGSignals = (
            // CG namespace exists (partially loaded — SDK injected async)
            typeof window.CrazyGames !== 'undefined' ||
            // CG ads namespace
            typeof window.CrazyGamesAds !== 'undefined' ||
            // Running on CG domain or subdomain
            (window.location.hostname &&
                window.location.hostname.indexOf('crazygames.') !== -1) ||
            // Embedded in CG iframe
            (document.referrer &&
                document.referrer.indexOf('crazygames.') !== -1)
        );

        if (!hasCGSignals) {
            // Not CrazyGames, no dashboard config, no wrapper — fail gracefully
            window.__y2.error('[Init] No platform SDK detected.',
                'window.Yes2SDK is undefined and no CrazyGames signals found.',
                'Ensure the game HTML includes the platform wrapper script',
                '(Poki, CrazyGames, Yandex, GameDistribution, etc.).',
                'Location:', window.location.href);
            SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                code: 'NoPlatformDetected',
                message: 'No platform SDK found. The game must be served with a platform wrapper ' +
                    '(via dashboard bundling or an HTML template that creates window.Yes2SDK).',
                context: 'Yes2SDK.InitializeAsync'
            }));
            return;
        }

        // C.2: CG signals found — proceed with CG-specific detection.
        //    CrazyGames injects their SDK ASYNCHRONOUSLY after framework.js loads.
        //    The platform also BLOCKS CDN loads (ERR_CONNECTION_RESET), so we
        //    primarily rely on polling for the platform-injected SDK.
        //
        //    Strategy: Poll for window.CrazyGames.SDK with auto-retry.
        //    - Round 1: poll 100ms x 150 = 15 seconds
        //    - Round 2 (auto-retry): wait 3s, then poll 100ms x 150 = 15 seconds
        //    - CDN load attempted in parallel (works on some CG environments)
        //    - Total max wait: ~35 seconds before giving up

        window.__y2.log('[Init] Step 3: CG signals detected, waiting for SDK...',
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

            try {
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
            } catch(error) {
                window.__y2.error('[Init] onSDKAvailable threw:', error);
                SendMessage('Bridge', 'OnInitializeError', JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || String(error),
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
    Yes2SDK_StartGameJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_StartGameJS: function() {
        if (!__y2h.has()) {
            window.__y2.error('Core SDK not loaded.');
            return;
        }

        try {
            window.Yes2SDK.startGameAsync()
                .then(function() {
                    SendMessage('Bridge', 'OnStartGameSuccess', '');
                })
                .catch(__y2h.handleCatch('OnStartGameError', 'Start game failed', 'Yes2SDK.StartGameAsync'));
        } catch(error) {
            __y2h.handleCatch('OnStartGameError', 'Start game failed', 'Yes2SDK.StartGameAsync')(error);
        }
    },

    // Set loading progress (0-100)
    Yes2SDK_SetLoadingProgressJS__deps: ['$__y2h'],
    Yes2SDK_SetLoadingProgressJS: function(progress) {
        if (__y2h.has()) window.Yes2SDK.setLoadingProgress(progress);
    },

    // Trigger haptic feedback
    Yes2SDK_PerformHapticFeedbackJS__deps: ['$__y2h'],
    Yes2SDK_PerformHapticFeedbackJS: function() {
        if (__y2h.has()) window.Yes2SDK.performHapticFeedback();
    },

    // Get current platform
    Yes2SDK_GetPlatformJS__deps: ['$__y2h'],
    Yes2SDK_GetPlatformJS: function() {
        if (!__y2h.has()) return __y2h.returnStr('unknown');
        var platform = window.Yes2SDK.getPlatform();
        return __y2h.returnStr(platform ? platform.toString().toLowerCase() : 'unknown');
    },

    // Check if SDK is initialized
    Yes2SDK_IsInitializedJS__deps: ['$__y2h'],
    Yes2SDK_IsInitializedJS: function() {
        return __y2h.has() && window.Yes2SDK.isInitialized === true;
    }

});
