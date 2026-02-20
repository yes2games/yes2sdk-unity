using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Stats API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKStats
    {
        /// <summary>
        /// Whether stats are supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get player stats for the specified keys.
        /// </summary>
        public void GetStatsAsync(string[] keys, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: GetStatsAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Stats.GetStatsAsync"));
        }

        /// <summary>
        /// Set player stats.
        /// </summary>
        public void SetStatsAsync(string statsJson, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: SetStatsAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Stats.SetStatsAsync"));
        }

        /// <summary>
        /// Increment player stats by the specified deltas.
        /// </summary>
        public void IncrementStatsAsync(string incrementsJson, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: IncrementStatsAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Stats.IncrementStatsAsync"));
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
            Message = "Stats are not supported on the current platform",
            Context = context
        };
    }
}
