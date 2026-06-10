using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Player API for Yes2SDK.
    /// Provides player info, data persistence, connected players, and signed info.
    /// Data persistence is available on every platform at runtime — platform cloud
    /// save where the platform supports it, local web storage otherwise. Identity is
    /// anonymous on platforms without an auth API (e.g. Poki, YouTube). Connected
    /// players and signed info are platform-gated; check IsConnectedPlayersSupported().
    /// </summary>
    public class Yes2SDKPlayer
    {
        #region Static Callback Fields

        private static Action<PlayerInfo> _getPlayerSuccessCallback;
        private static Action<Error> _getPlayerErrorCallback;
        private static Action<string> _getDataSuccessCallback;
        private static Action<Error> _getDataErrorCallback;
        private static Action _setDataSuccessCallback;
        private static Action<Error> _setDataErrorCallback;
        private static Action _flushDataSuccessCallback;
        private static Action<Error> _flushDataErrorCallback;
        private static Action<string> _getConnectedPlayersSuccessCallback;
        private static Action<Error> _getConnectedPlayersErrorCallback;
        private static Action<string> _getSignedPlayerInfoSuccessCallback;
        private static Action<Error> _getSignedPlayerInfoErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_GetPlayerAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_GetDataAsyncJS(string keysJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_SetDataAsyncJS(string dataJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_FlushDataAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_GetConnectedPlayersAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_GetSignedPlayerInfoAsyncJS(string payload);

        [DllImport("__Internal")]
        private static extern int Yes2SDK_IsDataSupportedJS();

        [DllImport("__Internal")]
        private static extern int Yes2SDK_IsConnectedPlayersSupportedJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Get player info asynchronously.
        /// On Poki, returns an anonymous player {id:"anonymous", name:null, photo:null}.
        /// </summary>
        public void GetPlayerAsync(Action<PlayerInfo> onSuccess = null, Action<Error> onError = null)
        {
            _getPlayerSuccessCallback = onSuccess;
            _getPlayerErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_GetPlayerAsyncJS();
#else
            Yes2Log.Log("Mock: GetPlayerAsync() — returning anonymous player");
            InvokeGetPlayerSuccess("{\"id\":\"anonymous\",\"name\":null,\"photo\":null}");
#endif
        }

        /// <summary>
        /// Get player data for the specified keys.
        /// Reads from platform cloud save where supported, local web storage otherwise.
        /// </summary>
        /// <param name="keys">Array of data keys to retrieve.</param>
        public void GetDataAsync(string[] keys, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getDataSuccessCallback = onSuccess;
            _getDataErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            var keysJson = JsonConvert.SerializeObject(keys);
            Yes2SDK_GetDataAsyncJS(keysJson);
#else
            Yes2Log.Log("Mock: GetDataAsync() — reading from PlayerPrefs store");
            var store = LoadMockStore();
            var result = new JObject();
            foreach (var key in keys)
            {
                if (store.TryGetValue(key, out var value))
                {
                    result[key] = value;
                }
            }
            InvokeGetDataSuccess(result.ToString(Formatting.None));
#endif
        }

        /// <summary>
        /// Set player data.
        /// Writes to platform cloud save where supported, local web storage otherwise.
        /// </summary>
        /// <param name="dataJson">JSON string of key-value data to store.</param>
        public void SetDataAsync(string dataJson, Action onSuccess = null, Action<Error> onError = null)
        {
            _setDataSuccessCallback = onSuccess;
            _setDataErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_SetDataAsyncJS(dataJson);
#else
            Yes2Log.Log("Mock: SetDataAsync() — writing to PlayerPrefs store");
            try
            {
                var store = LoadMockStore();
                var incoming = string.IsNullOrEmpty(dataJson) ? new JObject() : JObject.Parse(dataJson);
                store.Merge(incoming, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                SaveMockStore(store);
                InvokeSetDataSuccess();
            }
            catch (Exception e)
            {
                InvokeSetDataError(new Error
                {
                    Code = "Unknown",
                    Message = $"Failed to write mock player data: {e.Message}",
                    Context = "Yes2SDK.Player.SetDataAsync"
                });
            }
#endif
        }

        /// <summary>
        /// Flush (persist) all pending player data changes.
        /// A no-op where writes already persist immediately (local web storage).
        /// </summary>
        public void FlushDataAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            _flushDataSuccessCallback = onSuccess;
            _flushDataErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_FlushDataAsyncJS();
#else
            Yes2Log.Log("Mock: FlushDataAsync() — persisting PlayerPrefs store");
            PlayerPrefs.Save();
            InvokeFlushDataSuccess();
#endif
        }

        /// <summary>
        /// Get connected players (friends who also play this game).
        /// On Poki, returns FeatureNotSupported.
        /// </summary>
        public void GetConnectedPlayersAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getConnectedPlayersSuccessCallback = onSuccess;
            _getConnectedPlayersErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_GetConnectedPlayersAsyncJS();
#else
            Yes2Log.Log("Mock: GetConnectedPlayersAsync() — FeatureNotSupported");
            InvokeGetConnectedPlayersError(FeatureNotSupportedError("Yes2SDK.Player.GetConnectedPlayersAsync"));
#endif
        }

        /// <summary>
        /// Get a signed player info payload for server-side verification.
        /// On Poki, returns FeatureNotSupported.
        /// </summary>
        /// <param name="payload">Custom payload to include in the signed info.</param>
        public void GetSignedPlayerInfoAsync(string payload, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getSignedPlayerInfoSuccessCallback = onSuccess;
            _getSignedPlayerInfoErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_GetSignedPlayerInfoAsyncJS(payload);
#else
            Yes2Log.Log("Mock: GetSignedPlayerInfoAsync() — FeatureNotSupported");
            InvokeGetSignedPlayerInfoError(FeatureNotSupportedError("Yes2SDK.Player.GetSignedPlayerInfoAsync"));
#endif
        }

        // Task-returning overloads.

        public Task<PlayerInfo> GetPlayerAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<PlayerInfo>(
                (success, error) => GetPlayerAsync(success, error),
                cancellationToken);

        public Task<string> GetDataAsync(string[] keys, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetDataAsync(keys, success, error),
                cancellationToken);

        public Task SetDataAsync(string dataJson, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => SetDataAsync(dataJson, success, error),
                cancellationToken);

        public Task FlushDataAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask(
                (success, error) => FlushDataAsync(success, error),
                cancellationToken);

        public Task<string> GetConnectedPlayersAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetConnectedPlayersAsync(success, error),
                cancellationToken);

        public Task<string> GetSignedPlayerInfoAsync(string payload, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetSignedPlayerInfoAsync(payload, success, error),
                cancellationToken);

        /// <summary>
        /// Whether player data persistence is supported on the current platform.
        /// Delegates to the JS SDK so capability tracks the active platform adapter.
        /// </summary>
        public bool IsDataSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsDataSupportedJS() == 1;
#else
            // Editor/standalone mock persists via PlayerPrefs, mirroring the
            // SDK's runtime guarantee that data is always persistable.
            return true;
#endif
        }

        /// <summary>
        /// Whether connected players are supported on the current platform.
        /// Delegates to the JS SDK so capability tracks the active platform adapter.
        /// </summary>
        public bool IsConnectedPlayersSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsConnectedPlayersSupportedJS() == 1;
#else
            return false;
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetPlayerSuccess(string playerJson)
        {
            if (_getPlayerSuccessCallback != null)
            {
                try
                {
                    var player = JsonConvert.DeserializeObject<PlayerInfo>(playerJson);
                    _getPlayerSuccessCallback.Invoke(player);
                }
                catch (Exception e)
                {
                    Yes2Log.Error($"Failed to parse player JSON: {e.Message}");
                    _getPlayerErrorCallback?.Invoke(new Error
                    {
                        Code = "Unknown",
                        Message = $"Failed to parse player data: {e.Message}",
                        Context = "Yes2SDK.Player.GetPlayerAsync"
                    });
                }
            }
            _getPlayerSuccessCallback = null;
            _getPlayerErrorCallback = null;
        }

        internal static void InvokeGetPlayerError(Error error)
        {
            _getPlayerErrorCallback?.Invoke(error);
            _getPlayerSuccessCallback = null;
            _getPlayerErrorCallback = null;
        }

        internal static void InvokeGetDataSuccess(string dataJson)
        {
            _getDataSuccessCallback?.Invoke(dataJson);
            _getDataSuccessCallback = null;
            _getDataErrorCallback = null;
        }

        internal static void InvokeGetDataError(Error error)
        {
            _getDataErrorCallback?.Invoke(error);
            _getDataSuccessCallback = null;
            _getDataErrorCallback = null;
        }

        internal static void InvokeSetDataSuccess()
        {
            _setDataSuccessCallback?.Invoke();
            _setDataSuccessCallback = null;
            _setDataErrorCallback = null;
        }

        internal static void InvokeSetDataError(Error error)
        {
            _setDataErrorCallback?.Invoke(error);
            _setDataSuccessCallback = null;
            _setDataErrorCallback = null;
        }

        internal static void InvokeFlushDataSuccess()
        {
            _flushDataSuccessCallback?.Invoke();
            _flushDataSuccessCallback = null;
            _flushDataErrorCallback = null;
        }

        internal static void InvokeFlushDataError(Error error)
        {
            _flushDataErrorCallback?.Invoke(error);
            _flushDataSuccessCallback = null;
            _flushDataErrorCallback = null;
        }

        internal static void InvokeGetConnectedPlayersSuccess(string playersJson)
        {
            _getConnectedPlayersSuccessCallback?.Invoke(playersJson);
            _getConnectedPlayersSuccessCallback = null;
            _getConnectedPlayersErrorCallback = null;
        }

        internal static void InvokeGetConnectedPlayersError(Error error)
        {
            _getConnectedPlayersErrorCallback?.Invoke(error);
            _getConnectedPlayersSuccessCallback = null;
            _getConnectedPlayersErrorCallback = null;
        }

        internal static void InvokeGetSignedPlayerInfoSuccess(string signatureJson)
        {
            _getSignedPlayerInfoSuccessCallback?.Invoke(signatureJson);
            _getSignedPlayerInfoSuccessCallback = null;
            _getSignedPlayerInfoErrorCallback = null;
        }

        internal static void InvokeGetSignedPlayerInfoError(Error error)
        {
            _getSignedPlayerInfoErrorCallback?.Invoke(error);
            _getSignedPlayerInfoSuccessCallback = null;
            _getSignedPlayerInfoErrorCallback = null;
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

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        // PlayerPrefs-backed store for the editor/standalone mock. Mirrors the
        // WebGL behaviour (single merged JSON blob) so save/load can be tested
        // in the editor instead of returning FeatureNotSupported.
        private const string MockDataKey = "Yes2SDK.Player.MockData";

        private static JObject LoadMockStore()
        {
            var raw = PlayerPrefs.GetString(MockDataKey, "{}");
            try
            {
                return JObject.Parse(raw);
            }
            catch
            {
                return new JObject();
            }
        }

        private static void SaveMockStore(JObject store)
        {
            PlayerPrefs.SetString(MockDataKey, store.ToString(Formatting.None));
        }
#endif

        #endregion
    }
}
