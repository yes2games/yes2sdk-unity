using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Analytics API for Yes2SDK.
    /// Provides a unified interface for tracking analytics events across all platforms.
    /// </summary>
    public class Yes2SDKAnalytics
    {
        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogEventJS(string name, string paramsJson);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogLevelStartJS(string level);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogLevelEndJS(string level, int score, bool success);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogScoreJS(int score, string level);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogTutorialStartJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogTutorialEndJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_LogPurchaseJS(string productId, float price, string currency);
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Log a custom analytics event.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="parameters">Optional event parameters.</param>
        public void LogEvent(string name, Dictionary<string, object> parameters = null)
        {
            var paramsJson = parameters != null ? DictionaryToJson(parameters) : "{}";

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogEventJS(name, paramsJson);
#else
            Yes2Log.Log($"Mock: LogEvent({name}, {paramsJson})");
#endif
        }

        /// <summary>
        /// Log a level start event. On Poki, this triggers gameplayStart().
        /// </summary>
        /// <param name="level">Level identifier.</param>
        public void LogLevelStart(string level)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogLevelStartJS(level);
#else
            Yes2Log.Log($"Mock: LogLevelStart({level})");
#endif
        }

        /// <summary>
        /// Log a level end event. On Poki, this triggers gameplayStop().
        /// </summary>
        /// <param name="level">Level identifier.</param>
        /// <param name="score">Score achieved.</param>
        /// <param name="success">Whether the level was completed successfully.</param>
        public void LogLevelEnd(string level, int score, bool success)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogLevelEndJS(level, score, success);
#else
            Yes2Log.Log($"Mock: LogLevelEnd({level}, {score}, {success})");
#endif
        }

        /// <summary>
        /// Log a score event.
        /// </summary>
        /// <param name="score">The score value.</param>
        /// <param name="level">Optional level context.</param>
        public void LogScore(int score, string level = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogScoreJS(score, level);
#else
            Yes2Log.Log($"Mock: LogScore({score}, {level})");
#endif
        }

        /// <summary>
        /// Log a tutorial start event.
        /// </summary>
        public void LogTutorialStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogTutorialStartJS();
#else
            Yes2Log.Log("Mock: LogTutorialStart()");
#endif
        }

        /// <summary>
        /// Log a tutorial end event.
        /// </summary>
        public void LogTutorialEnd()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogTutorialEndJS();
#else
            Yes2Log.Log("Mock: LogTutorialEnd()");
#endif
        }

        /// <summary>
        /// Log a purchase event.
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="price">Price paid.</param>
        /// <param name="currency">Currency code (e.g., "USD").</param>
        public void LogPurchase(string productId, float price, string currency)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_LogPurchaseJS(productId, price, currency);
#else
            Yes2Log.Log($"Mock: LogPurchase({productId}, {price}, {currency})");
#endif
        }

        #endregion

        #region Private Helper Methods

        private static string DictionaryToJson(Dictionary<string, object> dict)
        {
            if (dict == null || dict.Count == 0)
                return "{}";

            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(EscapeJsonString(kvp.Key));
                sb.Append("\":");
                if (kvp.Value == null)
                {
                    sb.Append("null");
                }
                else if (kvp.Value is string s)
                {
                    sb.Append('"');
                    sb.Append(EscapeJsonString(s));
                    sb.Append('"');
                }
                else if (kvp.Value is bool b)
                {
                    sb.Append(b ? "true" : "false");
                }
                else
                {
                    sb.Append(kvp.Value);
                }
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeJsonString(string str)
        {
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        #endregion
    }
}
