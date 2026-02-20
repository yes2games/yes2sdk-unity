using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Internal logging utility for Yes2SDK with consistent formatting.
    /// </summary>
    internal static class Yes2Log
    {
        private const string Prefix = "[Yes2SDK]";

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void Warning(string message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void Error(string message)
        {
            Debug.LogError($"{Prefix} {message}");
        }
    }
}
