using System;
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
        private static extern void Yes2SDK_Data_SetStringAsyncJS(string key, string value);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Data_FlushAsyncJS();
#endif

        #endregion

        #region Async Callback Fields

        private static Action<bool> _setStringAsyncSuccessCallback;
        private static Action<Error> _setStringAsyncErrorCallback;
        private static Action<bool> _flushSuccessCallback;
        private static Action<Error> _flushErrorCallback;

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
            _setStringAsyncSuccessCallback = onSuccess;
            _setStringAsyncErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_SetStringAsyncJS(key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            InvokeSetStringAsyncSuccess("true");
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
            _flushSuccessCallback = onSuccess;
            _flushErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Data_FlushAsyncJS();
#else
            // PlayerPrefs writes are already durable in the editor.
            PlayerPrefs.Save();
            InvokeFlushSuccess("true");
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

        #region Async Callback Invocations (called by Bridge)

        internal static void InvokeSetStringAsyncSuccess(string result)
        {
            _setStringAsyncSuccessCallback?.Invoke(result == "true" || result == "1");
            _setStringAsyncSuccessCallback = null;
            _setStringAsyncErrorCallback = null;
        }

        internal static void InvokeSetStringAsyncError(Error error)
        {
            _setStringAsyncErrorCallback?.Invoke(error);
            _setStringAsyncSuccessCallback = null;
            _setStringAsyncErrorCallback = null;
        }

        internal static void InvokeFlushSuccess(string result)
        {
            _flushSuccessCallback?.Invoke(result == "true" || result == "1");
            _flushSuccessCallback = null;
            _flushErrorCallback = null;
        }

        internal static void InvokeFlushError(Error error)
        {
            _flushErrorCallback?.Invoke(error);
            _flushSuccessCallback = null;
            _flushErrorCallback = null;
        }

        #endregion
    }
}
