using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Achievements API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKAchievements
    {
        /// <summary>
        /// Whether achievements are supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get all achievements.
        /// </summary>
        public void GetAchievementsAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetAchievementsAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Achievements.GetAchievementsAsync"));
        }

        /// <summary>
        /// Unlock an achievement.
        /// </summary>
        public void UnlockAsync(string achievementId, Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: UnlockAsync({achievementId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Achievements.UnlockAsync"));
        }

        /// <summary>
        /// Set progress on an achievement (0-100).
        /// </summary>
        public void SetProgressAsync(string achievementId, int progress, Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: SetProgressAsync({achievementId}, {progress}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Achievements.SetProgressAsync"));
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
            Message = "Achievements are not supported on the current platform",
            Context = context
        };
    }
}
