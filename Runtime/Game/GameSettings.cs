using System;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Game settings from the platform (e.g., CrazyGames chat/audio toggles).
    /// </summary>
    [Serializable]
    public struct GameSettings
    {
        /// <summary>
        /// Whether the platform has requested chat to be disabled.
        /// </summary>
        [JsonProperty("disableChat")]
        public bool DisableChat;

        /// <summary>
        /// Whether the platform has requested audio to be muted.
        /// </summary>
        [JsonProperty("muteAudio")]
        public bool MuteAudio;

        public override string ToString()
        {
            return $"[GameSettings] DisableChat={DisableChat}, MuteAudio={MuteAudio}";
        }
    }
}
