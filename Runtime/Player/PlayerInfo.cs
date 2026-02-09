using System;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Player information returned by the platform.
    /// </summary>
    [Serializable]
    public struct PlayerInfo
    {
        /// <summary>
        /// Player identifier. "anonymous" on platforms without player support.
        /// </summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>
        /// Player display name. Null on platforms without player support.
        /// </summary>
        [JsonProperty("name")]
        public string Name;

        /// <summary>
        /// URL to the player's photo. Null on platforms without player support.
        /// </summary>
        [JsonProperty("photo")]
        public string Photo;

        public override string ToString()
        {
            return $"[PlayerInfo] Id={Id}, Name={Name ?? "null"}, Photo={Photo ?? "null"}";
        }
    }
}
