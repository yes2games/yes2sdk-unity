using System;
using System.Runtime.InteropServices;

namespace Yes2SDK
{
    /// <summary>
    /// Remote configuration / feature-flag API for Yes2SDK.
    /// Backed by the platform config API via the Core SDK (window.Yes2SDK.config).
    /// Flags resolve to a flat string-to-string map delivered to onSuccess as a
    /// JSON object. Platforms without a remote-config service return the
    /// caller-provided defaults.
    /// </summary>
    public class Yes2SDKConfig
    {
        #region Static Callback Fields

        private static Action<string> _getFlagsSuccessCallback;
        private static Action<Error> _getFlagsErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Config_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Config_GetFlagsAsyncJS(string optionsJson);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether remote configuration is supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Config_IsSupportedJS();
#else
            Yes2Log.Log("Mock: Config.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Get remote feature flags. onSuccess receives a JSON object of
        /// name-value string pairs.
        /// </summary>
        /// <param name="optionsJson">
        /// JSON object string of options, e.g.
        /// {"defaults":{"theme":"dark"},"clientFeatures":[{"name":"vip","value":"1"}]}.
        /// </param>
        /// <param name="onSuccess">Called with the flags JSON on success.</param>
        /// <param name="onError">Called with an error on failure.</param>
        public void GetFlagsAsync(string optionsJson = "{}", Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getFlagsSuccessCallback = onSuccess;
            _getFlagsErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Config_GetFlagsAsyncJS(optionsJson ?? "{}");
#else
            Yes2Log.Log($"Mock: Config.GetFlagsAsync('{optionsJson}') — returning empty flags");
            InvokeGetFlagsSuccess("{}");
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetFlagsSuccess(string flagsJson)
        {
            _getFlagsSuccessCallback?.Invoke(flagsJson);
            _getFlagsSuccessCallback = null;
            _getFlagsErrorCallback = null;
        }

        internal static void InvokeGetFlagsError(Error error)
        {
            _getFlagsErrorCallback?.Invoke(error);
            _getFlagsSuccessCallback = null;
            _getFlagsErrorCallback = null;
        }

        #endregion
    }
}
