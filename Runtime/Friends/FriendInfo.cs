using System;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Friend information returned by the Friends module.
    /// </summary>
    [Serializable]
    public struct FriendInfo
    {
        /// <summary>
        /// Friend's user identifier.
        /// </summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>
        /// Friend's username.
        /// </summary>
        [JsonProperty("username")]
        public string Username;

        /// <summary>
        /// URL to the friend's profile picture.
        /// </summary>
        [JsonProperty("profilePictureUrl")]
        public string ProfilePictureUrl;

        public override string ToString()
        {
            return $"[FriendInfo] Id={Id}, Username={Username ?? "null"}";
        }
    }
}
