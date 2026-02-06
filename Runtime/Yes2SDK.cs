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
                Debug.Log($"[Yes2SDK] Initialized on platform: {CurrentPlatform}");
                onSuccess?.Invoke();
                OnInitialized?.Invoke();
            };

            Callbacks.InitializeErrorCallback = error =>
            {
                Debug.LogError($"[Yes2SDK] Initialization failed: {error}");
                onError?.Invoke(error);
                OnError?.Invoke(error);
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_InitializeJS();
#else
            Debug.Log("[Yes2SDK] Mock: InitializeAsync");
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
                Debug.Log("[Yes2SDK] Game started");
                onSuccess?.Invoke();
                OnGameStarted?.Invoke();
            };

            Callbacks.StartGameErrorCallback = error =>
            {
                Debug.LogError($"[Yes2SDK] Start game failed: {error}");
                onError?.Invoke(error);
                OnError?.Invoke(error);
            };

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_StartGameJS();
#else
            Debug.Log("[Yes2SDK] Mock: StartGameAsync");
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
            Debug.Log($"[Yes2SDK] Mock: SetLoadingProgress({progress})");
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
            Debug.Log("[Yes2SDK] Mock: PerformHapticFeedback");
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
            if (value)
            {
                CurrentPlatform = GetPlatform();
            }
        }

        /// <summary>
        /// Invokes the OnPause event. Called internally by the bridge.
        /// </summary>
        internal static void InvokePause()
        {
            Debug.Log("[Yes2SDK] Game paused by platform");
            OnPause?.Invoke();
        }

        /// <summary>
        /// Invokes the OnResume event. Called internally by the bridge.
        /// </summary>
        internal static void InvokeResume()
        {
            Debug.Log("[Yes2SDK] Game resumed");
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
