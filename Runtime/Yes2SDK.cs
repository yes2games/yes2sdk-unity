using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Supported platforms for Yes2SDK.
    /// </summary>
    public enum Platform
    {
        Unknown,
        Poki,
        CrazyGames,
        Debug
    }

    /// <summary>
    /// Main entry point for Yes2SDK.
    /// Provides unified API for WebGL games on Poki and CrazyGames platforms.
    /// </summary>
    public static class Yes2SDK
    {
        #region Events

        /// <summary>
        /// Called when the SDK has been initialized successfully.
        /// </summary>
        public static event Action OnInitialized;

        /// <summary>
        /// Called when the game has started.
        /// </summary>
        public static event Action OnGameStarted;

        /// <summary>
        /// Called when the platform requests the game to pause (e.g., ad is showing).
        /// </summary>
        public static event Action OnPause;

        /// <summary>
        /// Called when the platform allows the game to resume.
        /// </summary>
        public static event Action OnResume;

        /// <summary>
        /// Called when an SDK error occurs.
        /// </summary>
        public static event Action<Error> OnError;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the SDK has been initialized.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// The current platform the game is running on.
        /// </summary>
        public static Platform CurrentPlatform { get; private set; } = Platform.Unknown;

        /// <summary>
        /// SDK version string.
        /// </summary>
        public static string Version => "1.0.0";

        private static Yes2SDKAds _ads;

        /// <summary>
        /// Ads API for showing interstitial, rewarded, and banner ads.
        /// </summary>
        public static Yes2SDKAds Ads
        {
            get
            {
                _ads ??= new Yes2SDKAds();
                return _ads;
            }
        }

        private static Yes2SDKAnalytics _analytics;

        /// <summary>
        /// Analytics API for tracking events, levels, scores, and more.
        /// </summary>
        public static Yes2SDKAnalytics Analytics
        {
            get
            {
                _analytics ??= new Yes2SDKAnalytics();
                return _analytics;
            }
        }

        private static Yes2SDKSession _session;

        /// <summary>
        /// Session API for locale, device, orientation, traffic source, and entry point.
        /// </summary>
        public static Yes2SDKSession Session
        {
            get
            {
                _session ??= new Yes2SDKSession();
                return _session;
            }
        }

        private static Yes2SDKPlayer _player;

        /// <summary>
        /// Player API for player info, data persistence, and connected players.
        /// </summary>
        public static Yes2SDKPlayer Player
        {
            get
            {
                _player ??= new Yes2SDKPlayer();
                return _player;
            }
        }

        private static Yes2SDKLeaderboard _leaderboard;

        /// <summary>
        /// Leaderboard API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKLeaderboard Leaderboard
        {
            get
            {
                _leaderboard ??= new Yes2SDKLeaderboard();
                return _leaderboard;
            }
        }

        private static Yes2SDKIAP _iap;

        /// <summary>
        /// In-App Purchase API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKIAP IAP
        {
            get
            {
                _iap ??= new Yes2SDKIAP();
                return _iap;
            }
        }

        private static Yes2SDKAchievements _achievements;

        /// <summary>
        /// Achievements API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKAchievements Achievements
        {
            get
            {
                _achievements ??= new Yes2SDKAchievements();
                return _achievements;
            }
        }

        private static Yes2SDKContext _context;

        /// <summary>
        /// Context API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKContext Context
        {
            get
            {
                _context ??= new Yes2SDKContext();
                return _context;
            }
        }

        private static Yes2SDKNotifications _notifications;

        /// <summary>
        /// Notifications API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKNotifications Notifications
        {
            get
            {
                _notifications ??= new Yes2SDKNotifications();
                return _notifications;
            }
        }

        private static Yes2SDKTournament _tournament;

        /// <summary>
        /// Tournament API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKTournament Tournament
        {
            get
            {
                _tournament ??= new Yes2SDKTournament();
                return _tournament;
            }
        }

        private static Yes2SDKStats _stats;

        /// <summary>
        /// Stats API. Stub — returns FeatureNotSupported on Poki.
        /// </summary>
        public static Yes2SDKStats Stats
        {
            get
            {
                _stats ??= new Yes2SDKStats();
                return _stats;
            }
        }

        private static Yes2SDKData _data;

        /// <summary>
        /// Data API for PlayerPrefs-style save/load. Uses platform cloud storage on CrazyGames, localStorage on Poki, PlayerPrefs in Editor.
        /// </summary>
        public static Yes2SDKData Data
        {
            get
            {
                _data ??= new Yes2SDKData();
                return _data;
            }
        }

        private static Yes2SDKAuth _auth;

        /// <summary>
        /// Auth API for user authentication. Fully supported on CrazyGames; stubs on Poki.
        /// </summary>
        public static Yes2SDKAuth Auth
        {
            get
            {
                _auth ??= new Yes2SDKAuth();
                return _auth;
            }
        }

        private static Yes2SDKGame _game;

        /// <summary>
        /// Game API for gameplay lifecycle, invite links, settings, and clipboard.
        /// </summary>
        public static Yes2SDKGame Game
        {
            get
            {
                _game ??= new Yes2SDKGame();
                return _game;
            }
        }

        private static Yes2SDKBanners _banners;

        /// <summary>
        /// Banners API for multi-size banner ads. Fully supported on CrazyGames; stubs on Poki.
        /// </summary>
        public static Yes2SDKBanners Banners
        {
            get
            {
                _banners ??= new Yes2SDKBanners();
                return _banners;
            }
        }

        private static Yes2SDKFriends _friends;

        /// <summary>
        /// Friends API for paginated friends list. Fully supported on CrazyGames; stubs on Poki.
        /// </summary>
        public static Yes2SDKFriends Friends
        {
            get
            {
                _friends ??= new Yes2SDKFriends();
                return _friends;
            }
        }

        private static Yes2SDKScore _score;

        /// <summary>
        /// Score API for leaderboard score submission. Fully supported on CrazyGames; console.log on Poki.
        /// </summary>
        public static Yes2SDKScore Score
        {
            get
            {
                _score ??= new Yes2SDKScore();
                return _score;
            }
        }

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_InitializeJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_StartGameJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_SetLoadingProgressJS(int progress);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_PerformHapticFeedbackJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetPlatformJS();

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IsInitializedJS();
#endif

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the Yes2SDK. Call this before using any other SDK methods.
        /// </summary>
        /// <param name="onSuccess">Called when initialization succeeds.</param>
        /// <param name="onError">Called if initialization fails.</param>
        public static void InitializeAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            // Ensure bridge is created
            var _ = Bridge.Instance;

            // Store callbacks
            Callbacks.InitializeSuccessCallback = () =>
            {
                CurrentPlatform = GetPlatform();
                Yes2Log.Log($"Initialized on platform: {CurrentPlatform}");
                onSuccess?.Invoke();
                OnInitialized?.Invoke();
            };

            Callbacks.InitializeErrorCallback = error =>
            {
                Yes2Log.Error($"Initialization failed: {error}");
                onError?.Invoke(error);
                OnError?.Invoke(error);
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_InitializeJS();
#else
            Yes2Log.Log("Mock: InitializeAsync");
            // Simulate successful initialization in Editor
            SetInitialized(true);
            Callbacks.InvokeInitializeSuccess();
#endif
        }

        /// <summary>
        /// Notify the platform that the game has finished loading and is ready to play.
        /// Call this after your game assets are loaded.
        /// </summary>
        /// <param name="onSuccess">Called when the game start is acknowledged.</param>
        /// <param name="onError">Called if starting fails.</param>
        public static void StartGameAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            Callbacks.StartGameSuccessCallback = () =>
            {
                Yes2Log.Log("Game started");
                onSuccess?.Invoke();
                OnGameStarted?.Invoke();
            };

            Callbacks.StartGameErrorCallback = error =>
            {
                Yes2Log.Error($"Start game failed: {error}");
                onError?.Invoke(error);
                OnError?.Invoke(error);
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_StartGameJS();
#else
            Yes2Log.Log("Mock: StartGameAsync");
            Callbacks.InvokeStartGameSuccess();
#endif
        }

        /// <summary>
        /// Update the loading progress shown to the player.
        /// </summary>
        /// <param name="progress">Progress value from 0 to 100.</param>
        public static void SetLoadingProgress(int progress)
        {
            progress = Mathf.Clamp(progress, 0, 100);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_SetLoadingProgressJS(progress);
#else
            Yes2Log.Log($"Mock: SetLoadingProgress({progress})");
#endif
        }

        /// <summary>
        /// Trigger haptic feedback on supported devices.
        /// </summary>
        public static void PerformHapticFeedback()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_PerformHapticFeedbackJS();
#else
            Yes2Log.Log("Mock: PerformHapticFeedback");
#endif
        }

        /// <summary>
        /// Get the current platform.
        /// </summary>
        /// <returns>The detected platform.</returns>
        public static Platform GetPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var platformStr = Yes2SDK_GetPlatformJS();
            return ParsePlatform(platformStr);
#else
            return Platform.Debug;
#endif
        }

        #endregion

        #region Internal Methods (called by Bridge)

        /// <summary>
        /// Sets the initialization state. Called internally by the bridge.
        /// </summary>
        internal static void SetInitialized(bool value)
        {
            IsInitialized = value;
        }

        /// <summary>
        /// Invokes the OnPause event. Called internally by the bridge.
        /// </summary>
        internal static void InvokePause()
        {
            Yes2Log.Log("Game paused by platform");
            OnPause?.Invoke();
        }

        /// <summary>
        /// Invokes the OnResume event. Called internally by the bridge.
        /// </summary>
        internal static void InvokeResume()
        {
            Yes2Log.Log("Game resumed");
            OnResume?.Invoke();
        }

        #endregion

        #region Utility Methods

        private static Platform ParsePlatform(string platformStr)
        {
            if (string.IsNullOrEmpty(platformStr))
                return Platform.Unknown;

            return platformStr.ToLowerInvariant() switch
            {
                "poki" => Platform.Poki,
                "crazygames" => Platform.CrazyGames,
                "debug" => Platform.Debug,
                _ => Platform.Unknown
            };
        }

        #endregion
    }
}
