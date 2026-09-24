using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Data API for Yes2SDK.
    /// Provides PlayerPrefs-style save/load. In WebGL, uses platform cloud storage (CrazyGames SDK data).
    /// In Editor, falls back to UnityEngine.PlayerPrefs.
    /// </summary>
    public class Yes2SDKData
    {
        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Yes2SDK_Data_GetIntJS(string key, int defaultValue);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_SetIntJS(string key, int value);

        [DllImport("__Internal")]
        private static extern float Yes2SDK_Data_GetFloatJS(string key, float defaultValue);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_SetFloatJS(string key, float value);

        [DllImport("__Internal")]
        private static extern string Yes2SDK_Data_GetStringJS(string key, string defaultValue);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_SetStringJS(string key, string value);

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Data_HasKeyJS(string key);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_DeleteKeyJS(string key);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_DeleteAllJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_SetStringAsyncJS(int requestId, string key, string value);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_FlushAsyncJS(int requestId);
#endif

        #endregion

        #region Async Request Tracking

        /// <summary>The confirmed-write operation a request belongs to.</summary>
        internal enum Operation
        {
            SetString,
            Flush
        }

        private sealed class PendingRequest
        {
            public Operation Operation;
            public Action<bool> OnSuccess;
            public Action<Error> OnError;
        }

        // Every call gets its own id, carried through the JS bridge and echoed
        // back on its response, so overlapping saves each hear their own result.
        // A single shared slot let the second call receive the first call's
        // result and lose its own.
        private static readonly Dictionary<int, PendingRequest> _pending = new Dictionary<int, PendingRequest>();
        private static int _nextRequestId;

        #endregion

        #region Public API

        /// <summary>
        /// Get an integer value for the given key.
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Data_GetIntJS(key, defaultValue);
#else
            return PlayerPrefs.GetInt(key, defaultValue);
#endif
        }

        /// <summary>
        /// Set an integer value for the given key.
        /// </summary>
        public void SetInt(string key, int value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_SetIntJS(key, value);
#else
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Get a float value for the given key.
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Data_GetFloatJS(key, defaultValue);
#else
            return PlayerPrefs.GetFloat(key, defaultValue);
#endif
        }

        /// <summary>
        /// Set a float value for the given key.
        /// </summary>
        public void SetFloat(string key, float value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_SetFloatJS(key, value);
#else
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Get a string value for the given key.
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Data_GetStringJS(key, defaultValue);
#else
            return PlayerPrefs.GetString(key, defaultValue);
#endif
        }

        /// <summary>
        /// Set a string value for the given key.
        /// </summary>
        public void SetString(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_SetStringJS(key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Check if a key exists.
        /// </summary>
        public bool HasKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Data_HasKeyJS(key);
#else
            return PlayerPrefs.HasKey(key);
#endif
        }

        /// <summary>
        /// Delete a key and its value.
        /// </summary>
        public void DeleteKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_DeleteKeyJS(key);
#else
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Delete all keys and values.
        /// </summary>
        public void DeleteAll()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_DeleteAllJS();
#else
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
#endif
        }

        #endregion

        #region Async Public API (durable saves)

        /// <summary>
        /// Set a string value and await platform confirmation. On cloud-backed
        /// platforms (e.g. Yandex) onSuccess fires only after the write is
        /// confirmed; the bool reports success.
        /// </summary>
        public void SetStringAsync(string key, string value, Action<bool> onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.SetString, onSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_SetStringAsyncJS(requestId, key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            CompleteSuccess(Operation.SetString, requestId, true);
#endif
        }

        /// <summary>
        /// Force any pending/batched writes to the platform's backing store and
        /// await confirmation. Synchronous setters may be batched (Yandex
        /// debounces cloud writes), so call this at a checkpoint — or before the
        /// game may close — to guarantee progress is persisted.
        /// </summary>
        public void FlushAsync(Action<bool> onSuccess = null, Action<Error> onError = null)
        {
            int requestId = Register(Operation.Flush, onSuccess, onError);

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_FlushAsyncJS(requestId);
#else
            // PlayerPrefs writes are already durable in the editor.
            PlayerPrefs.Save();
            CompleteSuccess(Operation.Flush, requestId, true);
#endif
        }

        /// <summary>Task overload of <see cref="SetStringAsync(string,string,Action{bool},Action{Error})"/>.</summary>
        public Task<bool> SetStringAsync(string key, string value, CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<bool>(
                (success, error) => SetStringAsync(key, value, success, error),
                cancellationToken);

        /// <summary>Task overload of <see cref="FlushAsync(Action{bool},Action{Error})"/>.</summary>
        public Task<bool> FlushAsync(CancellationToken cancellationToken)
            => TaskCallbackHelper.ToTask<bool>(
                (success, error) => FlushAsync(success, error),
                cancellationToken);

        #endregion

        #region Async Completion (called by Bridge)

        /// <summary>
        /// Bridge entry for a success message: "&lt;requestId&gt;|true" or
        /// "&lt;requestId&gt;|false", as written by Yes2SDKData.jslib.
        /// </summary>
        internal static void HandleSuccessMessage(Operation operation, string message)
        {
            if (!TryParseEnvelope(operation, message, out int requestId, out string payload)) return;
            CompleteSuccess(operation, requestId, payload == "true" || payload == "1");
        }

        /// <summary>Bridge entry for an error message: "&lt;requestId&gt;|&lt;error JSON&gt;".</summary>
        internal static void HandleErrorMessage(Operation operation, string message, Func<string, Error> parseError)
        {
            if (!TryParseEnvelope(operation, message, out int requestId, out string payload)) return;
            CompleteError(operation, requestId, parseError(payload));
        }

        internal static void CompleteSuccess(Operation operation, int requestId, bool saved)
        {
            // Take the request out before running game code, so a callback that
            // starts the next save, or throws, cannot touch another request.
            if (!TryTake(operation, requestId, out PendingRequest request)) return;
            request.OnSuccess?.Invoke(saved);
        }

        internal static void CompleteError(Operation operation, int requestId, Error error)
        {
            if (!TryTake(operation, requestId, out PendingRequest request)) return;
            request.OnError?.Invoke(error);
        }

        #endregion

        #region Test Seams

        /// <summary>
        /// Registers a request without sending it, so tests can deliver its
        /// response later and in any order, as a slow platform would.
        /// </summary>
        internal static int RegisterForTests(Operation operation, Action<bool> onSuccess, Action<Error> onError)
        {
            return Register(operation, onSuccess, onError);
        }

        /// <summary>Requests still waiting for a response.</summary>
        internal static int PendingCountForTests => _pending.Count;

        /// <summary>Drops every pending request so tests start clean.</summary>
        internal static void ResetDataStateForTests()
        {
            _pending.Clear();
        }

        #endregion

        #region Private Helpers

#if UNITY_EDITOR
        // With Domain Reload disabled statics survive into the next play, so
        // drop requests left over from the previous one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _pending.Clear();
        }
#endif

        private static int Register(Operation operation, Action<bool> onSuccess, Action<Error> onError)
        {
            // Ids only need to be unique among pending requests, so wrap back to
            // 1 rather than go negative after int.MaxValue calls.
            _nextRequestId = _nextRequestId == int.MaxValue ? 1 : _nextRequestId + 1;
            _pending[_nextRequestId] = new PendingRequest
            {
                Operation = operation,
                OnSuccess = onSuccess,
                OnError = onError
            };
            return _nextRequestId;
        }

        private static bool TryTake(Operation operation, int requestId, out PendingRequest request)
        {
            if (!_pending.TryGetValue(requestId, out request) || request.Operation != operation)
            {
                Yes2Log.Warning($"Data: dropped {operation} response for request {requestId}: no such request pending (already completed or unknown)");
                request = null;
                return false;
            }

            _pending.Remove(requestId);
            return true;
        }

        private static bool TryParseEnvelope(Operation operation, string message, out int requestId, out string payload)
        {
            requestId = 0;
            payload = null;

            int separator = message?.IndexOf('|') ?? -1;
            if (separator <= 0
                || !int.TryParse(message.Substring(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out requestId))
            {
                Yes2Log.Warning($"Data: dropped {operation} response with no request id: '{message}'");
                return false;
            }

            payload = message.Substring(separator + 1);
            return true;
        }

        #endregion
    }
}
