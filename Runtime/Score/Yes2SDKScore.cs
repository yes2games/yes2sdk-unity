using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Score API for Yes2SDK.
    /// Provides score submission. Fully supported on CrazyGames; console.log on Poki.
    /// </summary>
    public class Yes2SDKScore
    {
        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_Score_AddScoreJS(float score);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Score_SubmitScoreJS(string encryptedScore);

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_Score_IsSupportedJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Add a score to the platform leaderboard.
        /// </summary>
        /// <param name="score">The score to add.</param>
        public void AddScore(float score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Score_AddScoreJS(score);
#else
            Yes2Log.Log($"Mock: Score.AddScore({score})");
#endif
        }

        /// <summary>
        /// Submit an encrypted score to the platform leaderboard.
        /// </summary>
        /// <param name="encryptedScore">The encrypted score string.</param>
        public void SubmitScore(string encryptedScore)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Score_SubmitScoreJS(encryptedScore);
#else
            Yes2Log.Log($"Mock: Score.SubmitScore({encryptedScore})");
#endif
        }

        /// <summary>
        /// Check if Score submission is supported on the current platform.
        /// Use this to gate score-submit UI before calling <see cref="AddScore"/> or <see cref="SubmitScore"/>.
        /// </summary>
        public bool IsSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_Score_IsSupportedJS();
#else
            return false;
#endif
        }

        #endregion
    }
}
