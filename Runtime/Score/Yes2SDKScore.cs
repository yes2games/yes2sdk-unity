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
            Debug.Log($"[Yes2SDK] Mock: Score.AddScore({score})");
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
            Debug.Log($"[Yes2SDK] Mock: Score.SubmitScore({encryptedScore})");
#endif
        }

        #endregion
    }
}
