using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        [DllImport("__Internal")]
        private static extern int Yes2SDK_IsAudioEnabledJS();

        [DllImport("__Internal")]
        private static extern string Yes2SDK_GetDeviceInfoJS();
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
            Yes2Log.Log("Mock: GetLocale() — returning \"en\"");
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
            Yes2Log.Log("Mock: GetCountry() — returning \"\"");
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
            Yes2Log.Log("Mock: GetDevice() — returning \"desktop\"");
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
            Yes2Log.Log("Mock: GetOrientation() — returning \"landscape\"");
            return "landscape";
#endif
        }

        /// <summary>
        /// Get the traffic source as a JSON string. The shape is platform-defined: some platforms
        /// report a referrer plus a params object, others report referrer and campaign fields.
        /// Returns a JSON object with an empty referrer when nothing is available.
        /// </summary>
        public string GetTrafficSource()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetTrafficSourceJS();
#else
            Yes2Log.Log("Mock: GetTrafficSource() — returning empty JSON");
            return "{\"referrer\":\"\",\"params\":{}}";
#endif
        }

        /// <summary>
        /// Get the entry point data as a JSON string (the payload the game was launched with).
        /// Data from the link that opened the game, including data attached to a referral share
        /// or a registration prompt. Returns "{}" when there is none.
        /// </summary>
        public string GetEntryPointData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetEntryPointDataJS();
#elif UNITY_EDITOR
            string json = Yes2SDKEditorMock.EntryPointDataJson;
            if (!IsJsonObject(json))
            {
                json = "{}";
            }
            Yes2Log.Log("Mock: GetEntryPointData() - returning " + json);
            return json;
#else
            Yes2Log.Log("Mock: GetEntryPointData() - returning empty JSON");
            return "{}";
#endif
        }

        /// <summary>
        /// Get the entry point data as a dictionary. Data from the link that opened the game,
        /// including data attached to a referral share or a registration prompt.
        /// Nested objects are returned as <c>JObject</c> and arrays as <c>JArray</c>.
        /// Numbers come back as <c>long</c> or <c>double</c>; date-like strings stay strings.
        /// Returns an empty dictionary when there is no data or it is not a JSON object.
        /// </summary>
        public Dictionary<string, object> GetEntryPointDataDictionary()
        {
            return ParseEntryPointData(GetEntryPointData());
        }

        private static JToken ParseToken(string json)
        {
            // DateParseHandling.None keeps ISO date strings as strings.
            return JsonConvert.DeserializeObject<JToken>(json, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            });
        }

        internal static bool IsJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                return ParseToken(json) is JObject;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static Dictionary<string, object> ParseEntryPointData(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                JObject obj = ParseToken(json) as JObject;
                if (obj == null) return result;
                foreach (KeyValuePair<string, JToken> pair in obj)
                {
                    JToken v = pair.Value;
                    if (v is JValue jv) result[pair.Key] = jv.Value;
                    else result[pair.Key] = v;
                }
            }
            catch (Exception)
            {
                result.Clear();
            }
            return result;
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
            Yes2Log.Log($"Mock: SetSessionData({dataJson})");
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
            Yes2Log.Log("Mock: GetEntryPointAsync() — returning \"direct\"");
            InvokeGetEntryPointSuccess("direct");
#endif
        }

        /// <summary>
        /// Check whether the platform's audio is currently enabled.
        ///
        /// Required by YouTube Playables certification (integration #14): the game
        /// MUST read this at startup to set its initial mute state, then subscribe
        /// to <see cref="Yes2SDK.OnAudioEnabledChange"/> for runtime updates:
        ///
        /// <code>
        /// if (!Yes2SDK.Session.IsAudioEnabled()) AudioListener.volume = 0f;
        /// Yes2SDK.OnAudioEnabledChange += enabled => AudioListener.volume = enabled ? 1f : 0f;
        /// </code>
        ///
        /// On platforms without a native audio-enabled signal (Poki, CrazyGames,
        /// Yandex, GameDistribution), this returns true so games don't mute by
        /// accident.
        /// </summary>
        /// <returns>True if audio is enabled.</returns>
        public bool IsAudioEnabled()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsAudioEnabledJS() != 0;
#else
            Yes2Log.Log("Mock: IsAudioEnabled() — returning true");
            return true;
#endif
        }

        /// <summary>
        /// Get detailed device information synchronously as a JSON string of
        /// shape { type, isMobile, isDesktop, isTablet, isTV }.
        /// </summary>
        public string GetDeviceInfo()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_GetDeviceInfoJS();
#else
            Yes2Log.Log("Mock: GetDeviceInfo() — returning desktop");
            return "{\"type\":\"desktop\",\"isMobile\":false,\"isDesktop\":true,\"isTablet\":false,\"isTV\":false}";
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
