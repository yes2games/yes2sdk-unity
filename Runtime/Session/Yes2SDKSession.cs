using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Session API for Yes2SDK.
    /// Provides locale, device, orientation, traffic source, and entry point information.
    /// </summary>
    public class Yes2SDKSession
    {
        #region Static Callback Fields

        private static Action<string> _getEntryPointSuccessCallback;
        private static Action<Error> _getEntryPointErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetLocaleJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetCountryJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetDeviceJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetOrientationJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetTrafficSourceJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetEntryPointDataJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_SetSessionDataJS(string dataJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_GetEntryPointAsyncJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Get the user's locale/language code (e.g., "en", "fr", "ja").
        /// On Poki, this uses PokiSDK.getLanguage() or navigator.language.
        /// </summary>
        public string GetLocale()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetLocaleJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetLocale() — returning \"en\"");
            return "en";
#endif
        }

        /// <summary>
        /// Get the user's country code (e.g., "US", "JP").
        /// On Poki, this is not available and returns an empty string.
        /// </summary>
        public string GetCountry()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetCountryJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetCountry() — returning \"\"");
            return "";
#endif
        }

        /// <summary>
        /// Get the device type: "desktop", "mobile", "tablet", or "unknown".
        /// </summary>
        public string GetDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetDeviceJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetDevice() — returning \"desktop\"");
            return "desktop";
#endif
        }

        /// <summary>
        /// Get the current screen orientation: "landscape" or "portrait".
        /// </summary>
        public string GetOrientation()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetOrientationJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetOrientation() — returning \"landscape\"");
            return "landscape";
#endif
        }

        /// <summary>
        /// Get the traffic source as a JSON string containing referrer and URL parameters.
        /// </summary>
        public string GetTrafficSource()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetTrafficSourceJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetTrafficSource() — returning empty JSON");
            return "{\"referrer\":\"\",\"params\":{}}";
#endif
        }

        /// <summary>
        /// Get the entry point data (URL search params) as a JSON string.
        /// </summary>
        public string GetEntryPointData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetEntryPointDataJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetEntryPointData() — returning empty JSON");
            return "{}";
#endif
        }

        /// <summary>
        /// Store session-scoped data in memory (JS object). Not persisted.
        /// </summary>
        /// <param name="dataJson">JSON string of data to store.</param>
        public void SetSessionData(string dataJson)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_SetSessionDataJS(dataJson);
#else
            Debug.Log($"[Yes2SDK] Mock: SetSessionData({dataJson})");
#endif
        }

        /// <summary>
        /// Get the entry point asynchronously (how the user arrived at the game).
        /// On Poki, this returns "direct".
        /// </summary>
        /// <param name="onSuccess">Called with the entry point string (e.g., "direct").</param>
        /// <param name="onError">Called if an error occurs.</param>
        public void GetEntryPointAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getEntryPointSuccessCallback = onSuccess;
            _getEntryPointErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_GetEntryPointAsyncJS();
#else
            Debug.Log("[Yes2SDK] Mock: GetEntryPointAsync() — returning \"direct\"");
            InvokeGetEntryPointSuccess("direct");
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetEntryPointSuccess(string entryPoint)
        {
            _getEntryPointSuccessCallback?.Invoke(entryPoint);
            _getEntryPointSuccessCallback = null;
            _getEntryPointErrorCallback = null;
        }

        internal static void InvokeGetEntryPointError(Error error)
        {
            _getEntryPointErrorCallback?.Invoke(error);
            _getEntryPointSuccessCallback = null;
            _getEntryPointErrorCallback = null;
        }

        #endregion
    }
}
