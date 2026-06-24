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
        private static Action<string> _getUniqueIdSuccessCallback;
        private static Action<Error> _getUniqueIdErrorCallback;
        private static Action<string> _getIDsPerGameSuccessCallback;
        private static Action<Error> _getIDsPerGameErrorCallback;
        private static Action<string> _getPayingStatusSuccessCallback;
        private static Action<Error> _getPayingStatusErrorCallback;
        private static Action<string> _getModeSuccessCallback;
        private static Action<Error> _getModeErrorCallback;
        private static Action<string> _getPhotoSuccessCallback;
        private static Action<Error> _getPhotoErrorCallback;

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

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Player_GetUniqueIdAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Player_GetIDsPerGameAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Player_GetPayingStatusAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Player_GetModeAsyncJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Player_GetPhotoAsyncJS(string size);
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

        /// <summary>
        /// Get a stable unique identifier for the current player.
        /// onSuccess receives the id string.
        /// </summary>
        public void GetUniqueIdAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getUniqueIdSuccessCallback = onSuccess;
            _getUniqueIdErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Player_GetUniqueIdAsyncJS();
#else
            Yes2Log.Log("Mock: GetUniqueIdAsync() — returning \"anonymous\"");
            InvokeGetUniqueIdSuccess("anonymous");
#endif
        }

        /// <summary>
        /// Get the player's cross-game identities. onSuccess receives a JSON array
        /// of { appId, userId } objects.
        /// </summary>
        public void GetIDsPerGameAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getIDsPerGameSuccessCallback = onSuccess;
            _getIDsPerGameErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Player_GetIDsPerGameAsyncJS();
#else
            Yes2Log.Log("Mock: GetIDsPerGameAsync() — returning empty list");
            InvokeGetIDsPerGameSuccess("[]");
#endif
        }

        /// <summary>
        /// Get the player's paying status: "paying", "partially_paying",
        /// "not_paying", or "unknown". onSuccess receives the status string.
        /// </summary>
        public void GetPayingStatusAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getPayingStatusSuccessCallback = onSuccess;
            _getPayingStatusErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Player_GetPayingStatusAsyncJS();
#else
            Yes2Log.Log("Mock: GetPayingStatusAsync() — returning \"unknown\"");
            InvokeGetPayingStatusSuccess("unknown");
#endif
        }

        /// <summary>
        /// Get the player's session mode: "lite", "authorized", or "unknown".
        /// onSuccess receives the mode string.
        /// </summary>
        public void GetModeAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getModeSuccessCallback = onSuccess;
            _getModeErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Player_GetModeAsyncJS();
#else
            Yes2Log.Log("Mock: GetModeAsync() — returning \"unknown\"");
            InvokeGetModeSuccess("unknown");
#endif
        }

        /// <summary>
        /// Get the player's avatar URL for the requested size. onSuccess receives
        /// the URL string, or the literal "null" if no photo is available.
        /// </summary>
        /// <param name="size">Requested photo size (platform-defined, e.g. "medium").</param>
        public void GetPhotoAsync(string size, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getPhotoSuccessCallback = onSuccess;
            _getPhotoErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Player_GetPhotoAsyncJS(size ?? string.Empty);
#else
            Yes2Log.Log($"Mock: GetPhotoAsync('{size}') — returning null");
            InvokeGetPhotoSuccess("null");
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

        public Task<string> GetUniqueIdAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetUniqueIdAsync(success, error),
                cancellationToken);

        public Task<string> GetIDsPerGameAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetIDsPerGameAsync(success, error),
                cancellationToken);

        public Task<string> GetPayingStatusAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetPayingStatusAsync(success, error),
                cancellationToken);

        public Task<string> GetModeAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetModeAsync(success, error),
                cancellationToken);

        public Task<string> GetPhotoAsync(string size, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<string>(
                (success, error) => GetPhotoAsync(size, success, error),
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

        internal static void InvokeGetUniqueIdSuccess(string id)
        {
            _getUniqueIdSuccessCallback?.Invoke(id);
            _getUniqueIdSuccessCallback = null;
            _getUniqueIdErrorCallback = null;
        }

        internal static void InvokeGetUniqueIdError(Error error)
        {
            _getUniqueIdErrorCallback?.Invoke(error);
            _getUniqueIdSuccessCallback = null;
            _getUniqueIdErrorCallback = null;
        }

        internal static void InvokeGetIDsPerGameSuccess(string identitiesJson)
        {
            _getIDsPerGameSuccessCallback?.Invoke(identitiesJson);
            _getIDsPerGameSuccessCallback = null;
            _getIDsPerGameErrorCallback = null;
        }

        internal static void InvokeGetIDsPerGameError(Error error)
        {
            _getIDsPerGameErrorCallback?.Invoke(error);
            _getIDsPerGameSuccessCallback = null;
            _getIDsPerGameErrorCallback = null;
        }

        internal static void InvokeGetPayingStatusSuccess(string status)
        {
            _getPayingStatusSuccessCallback?.Invoke(status);
            _getPayingStatusSuccessCallback = null;
            _getPayingStatusErrorCallback = null;
        }

        internal static void InvokeGetPayingStatusError(Error error)
        {
            _getPayingStatusErrorCallback?.Invoke(error);
            _getPayingStatusSuccessCallback = null;
            _getPayingStatusErrorCallback = null;
        }

        internal static void InvokeGetModeSuccess(string mode)
        {
            _getModeSuccessCallback?.Invoke(mode);
            _getModeSuccessCallback = null;
            _getModeErrorCallback = null;
        }

        internal static void InvokeGetModeError(Error error)
        {
            _getModeErrorCallback?.Invoke(error);
            _getModeSuccessCallback = null;
            _getModeErrorCallback = null;
        }

        internal static void InvokeGetPhotoSuccess(string photoUrl)
        {
            _getPhotoSuccessCallback?.Invoke(photoUrl);
            _getPhotoSuccessCallback = null;
            _getPhotoErrorCallback = null;
        }

        internal static void InvokeGetPhotoError(Error error)
        {
            _getPhotoErrorCallback?.Invoke(error);
            _getPhotoSuccessCallback = null;
            _getPhotoErrorCallback = null;
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
