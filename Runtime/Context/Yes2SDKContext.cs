using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Context API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKContext
    {
        /// <summary>
        /// Whether context is supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get the current context ID.
        /// </summary>
        public string GetContext()
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: GetContext() — FeatureNotSupported");
            return null;
        }

        /// <summary>
        /// Switch to a specific context.
        /// </summary>
        public void SwitchAsync(string contextId, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: SwitchAsync({contextId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Context.SwitchAsync"));
        }

        /// <summary>
        /// Open a context chooser dialog.
        /// </summary>
        public void ChooseAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: ChooseAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Context.ChooseAsync"));
        }

        /// <summary>
        /// Create a new context.
        /// </summary>
        public void CreateAsync(string playerId, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: CreateAsync({playerId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Context.CreateAsync"));
        }

        /// <summary>
        /// Share the current context.
        /// </summary>
        public void ShareAsync(string text, string imageBase64, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: ShareAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Context.ShareAsync"));
        }

        private static bool IsEditor()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static Error NotSupportedError(string context) => new Error
        {
            Code = "FeatureNotSupported",
            Message = "Context is not supported on the current platform",
            Context = context
        };
    }
}
