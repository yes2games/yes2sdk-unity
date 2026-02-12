using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Tournament API for Yes2SDK.
    /// Currently a stub — returns FeatureNotSupported on all platforms.
    /// </summary>
    public class Yes2SDKTournament
    {
        /// <summary>
        /// Whether tournaments are supported on the current platform.
        /// </summary>
        public bool IsSupported() => false;

        /// <summary>
        /// Get the current tournament.
        /// </summary>
        public void GetCurrentAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: GetCurrentAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Tournament.GetCurrentAsync"));
        }

        /// <summary>
        /// Get all available tournaments.
        /// </summary>
        public void GetAllAsync(Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: GetAllAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Tournament.GetAllAsync"));
        }

        /// <summary>
        /// Create a new tournament.
        /// </summary>
        public void CreateAsync(string configJson, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: CreateAsync() — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Tournament.CreateAsync"));
        }

        /// <summary>
        /// Post a score to the current tournament.
        /// </summary>
        public void PostScoreAsync(int score, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: PostScoreAsync({score}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Tournament.PostScoreAsync"));
        }

        /// <summary>
        /// Join a tournament.
        /// </summary>
        public void JoinAsync(string tournamentId, Action onSuccess = null, Action<Error> onError = null)
        {
            Yes2Log.Log($"{(IsEditor() ? "Mock" : "Stub")}: JoinAsync({tournamentId}) — FeatureNotSupported");
            onError?.Invoke(NotSupportedError("Yes2SDK.Tournament.JoinAsync"));
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
            Message = "Tournaments are not supported on the current platform",
            Context = context
        };
    }
}
