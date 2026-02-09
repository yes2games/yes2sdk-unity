using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Leaderboard API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKLeaderboard
    {
        /// <summary>
        /// Whether leaderboards are supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get a leaderboard by ID.
        /// </summary>
        public void GetLeaderboardAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetLeaderboardAsync({leaderboardId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Leaderboard.GetLeaderboardAsync"));
        }

        /// <summary>
        /// Set a score on a leaderboard.
        /// </summary>
        public void SetScoreAsync(string leaderboardId, int score, Action onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: SetScoreAsync({leaderboardId}, {score}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Leaderboard.SetScoreAsync"));
        }

        /// <summary>
        /// Get entries from a leaderboard.
        /// </summary>
        public void GetEntriesAsync(string leaderboardId, int count, int offset, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetEntriesAsync({leaderboardId}, {count}, {offset}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Leaderboard.GetEntriesAsync"));
        }

        /// <summary>
        /// Get the current player's entry on a leaderboard.
        /// </summary>
        public void GetPlayerEntryAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Debug.Log($"[Yes2SDK] {(IsEditor() ? "Mock" : "Stub")}: GetPlayerEntryAsync({leaderboardId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Leaderboard.GetPlayerEntryAsync"));
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
            Message = "Leaderboards are not supported on the current platform",
            Context = context
        };
    }
}
