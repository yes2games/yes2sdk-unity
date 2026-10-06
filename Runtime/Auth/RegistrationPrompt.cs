using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    /// <summary>
    /// Color theme of the platform's registration prompt.
    /// </summary>
    public enum RegistrationPromptTheme
    {
        /// <summary>Light theme.</summary>
        Light,

        /// <summary>Dark theme.</summary>
        Dark
    }

    /// <summary>
    /// Options for <see cref="Yes2SDKAuth.ShowRegistrationPrompt"/>. Every
    /// field is optional; unset fields are left to the platform default.
    /// </summary>
    public class RegistrationPromptOptions
    {
        /// <summary>Prompt theme. Null uses the platform default.</summary>
        public RegistrationPromptTheme? Theme;

        /// <summary>
        /// Data available from Session.GetEntryPointData() after the player
        /// registers and the game opens again.
        /// </summary>
        public Dictionary<string, object> Data;

        /// <summary>
        /// Pre-filled message the player sends to register. It must not be
        /// blank, must be at most 140 characters, and must contain
        /// <c>{{registrationCode}}</c> exactly once, separated from
        /// neighbouring letters or digits by a space or punctuation.
        /// </summary>
        public string Message;

        /// <summary>Serializes the set fields only (unset fields are omitted, never null).</summary>
        internal string ToJson()
        {
            var json = new JObject();
            if (Theme.HasValue)
            {
                json["theme"] = Theme.Value == RegistrationPromptTheme.Light ? "light" : "dark";
            }
            if (Data != null)
            {
                json["data"] = JToken.FromObject(Data);
            }
            if (Message != null)
            {
                json["message"] = Message;
            }
            return json.ToString(Formatting.None);
        }
    }

    /// <summary>
    /// Handle to an open registration prompt, returned by
    /// <see cref="Yes2SDKAuth.ShowRegistrationPrompt"/>. Wire
    /// <see cref="Login"/> and <see cref="Close"/> to the game's own buttons.
    /// </summary>
    public sealed class RegistrationPrompt
    {
        internal RegistrationPrompt(int id, System.Action onClose)
        {
            Id = id;
            OnClose = onClose;
            IsOpen = true;
        }

        internal int Id { get; }

        internal System.Action OnClose { get; }

        /// <summary>
        /// True until the prompt closes or <see cref="Close"/> is called.
        /// </summary>
        public bool IsOpen { get; internal set; }

        /// <summary>
        /// Start the platform's registration flow. Registration may finish
        /// outside the game, so check <see cref="Yes2SDKAuth.IsAuthenticated"/>
        /// the next time the game opens. Does nothing on a closed prompt.
        /// </summary>
        public void Login()
        {
            Yes2SDKAuth.LoginRegistrationPrompt(this);
        }

        /// <summary>
        /// Close the prompt. The onClose callback fires when the platform
        /// reports the close, at most once. Does nothing on a closed prompt.
        /// </summary>
        public void Close()
        {
            Yes2SDKAuth.CloseRegistrationPrompt(this);
        }
    }
}
