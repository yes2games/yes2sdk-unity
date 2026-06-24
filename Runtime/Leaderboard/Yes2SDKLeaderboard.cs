using System;
using System.Runtime.InteropServices;

namespace Yes2SDK
{
    /// <summary>
    /// Leaderboard API for Yes2SDK.
    /// Backed by the platform leaderboard API via the Core SDK
    /// (window.Yes2SDK.leaderboard). Result payloads are delivered to onSuccess
    /// as JSON strings. Platforms without leaderboards report FeatureNotSupported.
    /// </summary>
    public class Yes2SDKLeaderboard
    {
        #region Static Callback Fields

        private static Action<string> _getLeaderboardSuccessCallback;
        private static Action<Error> _getLeaderboardErrorCallback;
        private static Action<string> _setScoreSuccessCallback;
        private static Action<Error> _setScoreErrorCallback;
        private static Action<string> _getEntriesSuccessCallback;
        private static Action<Error> _getEntriesErrorCallback;
        private static Action<string> _getPlayerEntrySuccessCallback;
        private static Action<Error> _getPlayerEntryErrorCallback;
        private static Action<string> _getConnectedEntriesSuccessCallback;
        private static Action<Error> _getConnectedEntriesErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Leaderboard_IsSupportedJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Leaderboard_GetLeaderboardAsyncJS(string name);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Leaderboard_SetScoreAsyncJS(string name, int score, string metadata);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Leaderboard_GetEntriesAsyncJS(string name, int count, int offset);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Leaderboard_GetPlayerEntryAsyncJS(string name);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Leaderboard_GetConnectedPlayerEntriesAsyncJS(string name, int count, int offset);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Whether leaderboards are supported on the current platform.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Leaderboard_IsSupportedJS();
#else
            Yes2Log.Log("Mock: Leaderboard.IsSupported() — returning false");
            return false;
#endif
        }

        /// <summary>
        /// Get a leaderboard by name. onSuccess receives the leaderboard as a
        /// JSON object { name, contextId, entries }.
        /// </summary>
        public void GetLeaderboardAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getLeaderboardSuccessCallback = onSuccess;
            _getLeaderboardErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Leaderboard_GetLeaderboardAsyncJS(leaderboardId);
#else
            Yes2Log.Log($"Mock: Leaderboard.GetLeaderboardAsync('{leaderboardId}') — FeatureNotSupported");
            InvokeGetLeaderboardError(FeatureNotSupportedError("Yes2SDK.Leaderboard.GetLeaderboardAsync"));
#endif
        }

        /// <summary>
        /// Submit a score to a leaderboard. onSuccess receives the player's
        /// entry as a JSON object.
        /// </summary>
        /// <param name="leaderboardId">Leaderboard name/identifier.</param>
        /// <param name="score">Score to submit.</param>
        /// <param name="onSuccess">Called with the entry JSON on success.</param>
        /// <param name="onError">Called with an error on failure.</param>
        /// <param name="metadata">Optional metadata to attach to the entry.</param>
        public void SetScoreAsync(
            string leaderboardId,
            int score,
            Action<string> onSuccess = null,
            Action<Error> onError = null,
            string metadata = null)
        {
            _setScoreSuccessCallback = onSuccess;
            _setScoreErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Leaderboard_SetScoreAsyncJS(leaderboardId, score, metadata ?? string.Empty);
#else
            Yes2Log.Log($"Mock: Leaderboard.SetScoreAsync('{leaderboardId}', {score}) — FeatureNotSupported");
            InvokeSetScoreError(FeatureNotSupportedError("Yes2SDK.Leaderboard.SetScoreAsync"));
#endif
        }

        /// <summary>
        /// Get leaderboard entries. onSuccess receives a JSON array of entries.
        /// </summary>
        public void GetEntriesAsync(string leaderboardId, int count, int offset, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getEntriesSuccessCallback = onSuccess;
            _getEntriesErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Leaderboard_GetEntriesAsyncJS(leaderboardId, count, offset);
#else
            Yes2Log.Log($"Mock: Leaderboard.GetEntriesAsync('{leaderboardId}', {count}, {offset}) — FeatureNotSupported");
            InvokeGetEntriesError(FeatureNotSupportedError("Yes2SDK.Leaderboard.GetEntriesAsync"));
#endif
        }

        /// <summary>
        /// Get the current player's entry. onSuccess receives the entry as a
        /// JSON object, or the literal "null" if the player is not ranked.
        /// </summary>
        public void GetPlayerEntryAsync(string leaderboardId, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getPlayerEntrySuccessCallback = onSuccess;
            _getPlayerEntryErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Leaderboard_GetPlayerEntryAsyncJS(leaderboardId);
#else
            Yes2Log.Log($"Mock: Leaderboard.GetPlayerEntryAsync('{leaderboardId}') — FeatureNotSupported");
            InvokeGetPlayerEntryError(FeatureNotSupportedError("Yes2SDK.Leaderboard.GetPlayerEntryAsync"));
#endif
        }

        /// <summary>
        /// Get entries for connected players (friends). onSuccess receives a JSON
        /// array of entries. Not supported on every platform (e.g. Yandex).
        /// </summary>
        public void GetConnectedPlayerEntriesAsync(string leaderboardId, int count, int offset, Action<string> onSuccess = null, Action<Error> onError = null)
        {
            _getConnectedEntriesSuccessCallback = onSuccess;
            _getConnectedEntriesErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Leaderboard_GetConnectedPlayerEntriesAsyncJS(leaderboardId, count, offset);
#else
            Yes2Log.Log($"Mock: Leaderboard.GetConnectedPlayerEntriesAsync('{leaderboardId}', {count}, {offset}) — FeatureNotSupported");
            InvokeGetConnectedPlayerEntriesError(FeatureNotSupportedError("Yes2SDK.Leaderboard.GetConnectedPlayerEntriesAsync"));
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeGetLeaderboardSuccess(string leaderboardJson)
        {
            _getLeaderboardSuccessCallback?.Invoke(leaderboardJson);
            _getLeaderboardSuccessCallback = null;
            _getLeaderboardErrorCallback = null;
        }

        internal static void InvokeGetLeaderboardError(Error error)
        {
            _getLeaderboardErrorCallback?.Invoke(error);
            _getLeaderboardSuccessCallback = null;
            _getLeaderboardErrorCallback = null;
        }

        internal static void InvokeSetScoreSuccess(string entryJson)
        {
            _setScoreSuccessCallback?.Invoke(entryJson);
            _setScoreSuccessCallback = null;
            _setScoreErrorCallback = null;
        }

        internal static void InvokeSetScoreError(Error error)
        {
            _setScoreErrorCallback?.Invoke(error);
            _setScoreSuccessCallback = null;
            _setScoreErrorCallback = null;
        }

        internal static void InvokeGetEntriesSuccess(string entriesJson)
        {
            _getEntriesSuccessCallback?.Invoke(entriesJson);
            _getEntriesSuccessCallback = null;
            _getEntriesErrorCallback = null;
        }

        internal static void InvokeGetEntriesError(Error error)
        {
            _getEntriesErrorCallback?.Invoke(error);
            _getEntriesSuccessCallback = null;
            _getEntriesErrorCallback = null;
        }

        internal static void InvokeGetPlayerEntrySuccess(string entryJson)
        {
            _getPlayerEntrySuccessCallback?.Invoke(entryJson);
            _getPlayerEntrySuccessCallback = null;
            _getPlayerEntryErrorCallback = null;
        }

        internal static void InvokeGetPlayerEntryError(Error error)
        {
            _getPlayerEntryErrorCallback?.Invoke(error);
            _getPlayerEntrySuccessCallback = null;
            _getPlayerEntryErrorCallback = null;
        }

        internal static void InvokeGetConnectedPlayerEntriesSuccess(string entriesJson)
        {
            _getConnectedEntriesSuccessCallback?.Invoke(entriesJson);
            _getConnectedEntriesSuccessCallback = null;
            _getConnectedEntriesErrorCallback = null;
        }

        internal static void InvokeGetConnectedPlayerEntriesError(Error error)
        {
            _getConnectedEntriesErrorCallback?.Invoke(error);
            _getConnectedEntriesSuccessCallback = null;
            _getConnectedEntriesErrorCallback = null;
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

        #endregion
    }
}
