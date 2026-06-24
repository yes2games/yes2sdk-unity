using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Stats API for Yes2SDK.
    /// Backed by the platform stats API via the Core SDK (window.Yes2SDK.stats).
    /// Stats are name-value pairs (numbers). Maps cross the C#/JS boundary as
    /// JSON strings; results are delivered to onSuccess as JSON objects.
    /// Platforms without stats report FeatureNotSupported.
    /// </summary>
    public class Yes2SDKStats
    {
        #region Static Callback Fields

        private static Action<string> _getStatsSuccessCallback;
        private static Action<Error> _getStatsErrorCallback;
        private static Action _setStatsSuccessCallback;
        private static Action<Error> _setStatsErrorCallback;
        private static Action<string> _incrementStatsSuccessCallback;
        private static Action<Error> _incrementStatsErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Stats_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Stats_GetStatsAsyncJS(string keysJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Stats_SetStatsAsyncJS(string statsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Stats_IncrementStatsAsyncJS(string incrementsJson);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether stats are supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Stats_IsSupportedJS();
#else
            Yes2Log.Log("Mock: Stats.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Get stats by key. onSuccess receives a JSON object of name-value pairs.
        /// </summary>
        /// <param name="keys">Stat keys to retrieve.</param>
        /// <param name="onSuccess">Called with the stats JSON on success.</param>
        /// <param name="onError">Called with an error on failure.</param>
        public void GetStatsAsync(string[] keys, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getStatsSuccessCallback = onSuccess;
            _getStatsErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Stats_GetStatsAsyncJS(JsonConvert.SerializeObject(keys ?? Array.Empty<string>()));
#else
            Yes2Log.Log("Mock: Stats.GetStatsAsync() — FeatureNotSupported");
            InvokeGetStatsError(FeatureNotSupportedError("Yes2SDK.Stats.GetStatsAsync"));
#endif
        }

        /// <summary>
        /// Set stats. Pass a JSON object string of name-value pairs, e.g.
        /// {"kills":42,"level":7}.
        /// </summary>
        public void SetStatsAsync(string statsJson, Action onSuccess = null, Action<Error> onError = null)
        {
            _setStatsSuccessCallback = onSuccess;
            _setStatsErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Stats_SetStatsAsyncJS(statsJson ?? "{}");
#else
            Yes2Log.Log($"Mock: Stats.SetStatsAsync('{statsJson}') — FeatureNotSupported");
            InvokeSetStatsError(FeatureNotSupportedError("Yes2SDK.Stats.SetStatsAsync"));
#endif
        }

        /// <summary>
        /// Increment stats. Pass a JSON object string of name-delta pairs, e.g.
        /// {"kills":1}. onSuccess receives the updated stats as a JSON object.
        /// </summary>
        public void IncrementStatsAsync(string incrementsJson, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _incrementStatsSuccessCallback = onSuccess;
            _incrementStatsErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Stats_IncrementStatsAsyncJS(incrementsJson ?? "{}");
#else
            Yes2Log.Log($"Mock: Stats.IncrementStatsAsync('{incrementsJson}') — FeatureNotSupported");
            InvokeIncrementStatsError(FeatureNotSupportedError("Yes2SDK.Stats.IncrementStatsAsync"));
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetStatsSuccess(string statsJson)
        {
            _getStatsSuccessCallback?.Invoke(statsJson);
            _getStatsSuccessCallback = null;
            _getStatsErrorCallback = null;
        }

        internal static void InvokeGetStatsError(Error error)
        {
            _getStatsErrorCallback?.Invoke(error);
            _getStatsSuccessCallback = null;
            _getStatsErrorCallback = null;
        }

        internal static void InvokeSetStatsSuccess()
        {
            _setStatsSuccessCallback?.Invoke();
            _setStatsSuccessCallback = null;
            _setStatsErrorCallback = null;
        }

        internal static void InvokeSetStatsError(Error error)
        {
            _setStatsErrorCallback?.Invoke(error);
            _setStatsSuccessCallback = null;
            _setStatsErrorCallback = null;
        }

        internal static void InvokeIncrementStatsSuccess(string statsJson)
        {
            _incrementStatsSuccessCallback?.Invoke(statsJson);
            _incrementStatsSuccessCallback = null;
            _incrementStatsErrorCallback = null;
        }

        internal static void InvokeIncrementStatsError(Error error)
        {
            _incrementStatsErrorCallback?.Invoke(error);
            _incrementStatsSuccessCallback = null;
            _incrementStatsErrorCallback = null;
        }

        #endregion

        #region Private Helpers

        private static Error FeatureNotSupportedError(string context)
        {
            return new Error
            {
                Code = "FeatureNotSupported",
                Message = "This feature is not supported on the current platform",
                Context = context
            };
        }

        #endregion
    }
}
