using System;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Authenticated user information returned by the Auth module.
    /// </summary>
    [Serializable]
    public struct AuthUser
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>
        /// User display name.
        /// </summary>
        [JsonProperty("name")]
        public string Name;

        /// <summary>
        /// URL to the user's profile photo.
        /// </summary>
        [JsonProperty("photo")]
        public string Photo;

        /// <summary>
        /// Whether the user is authenticated (not anonymous).
        /// </summary>
        [JsonProperty("isAuthenticated")]
        public bool IsAuthenticated;

        public override string ToString()
        {
            return $"[AuthUser] Id={Id}, Name={Name ?? "null"}, IsAuthenticated={IsAuthenticated}";
        }
    }
}
