using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Notifications API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKNotifications
    {
        /// <summary>
        /// Whether notifications are supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Schedule a notification.
        /// </summary>
        public void ScheduleAsync(string title, string body, int delaySec, string dataJson, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: ScheduleAsync({title}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Notifications.ScheduleAsync"));
        }

        /// <summary>
        /// Cancel a scheduled notification.
        /// </summary>
        public void CancelAsync(string notificationId, Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: CancelAsync({notificationId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Notifications.CancelAsync"));
        }

        /// <summary>
        /// Cancel all scheduled notifications.
        /// </summary>
        public void CancelAllAsync(Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: CancelAllAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Notifications.CancelAllAsync"));
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
            Message = "Notifications are not supported on the current platform",
            Context = context
        };
    }
}
