using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Options for <see cref="Yes2SDKContext.ShareImageAsync(ContextShareOptions, Action, Action{Error})"/>.
    /// Every field is optional; fields left null are not sent.
    /// </summary>
    [Serializable]
    public class ContextShareOptions
    {
        /// <summary>
        /// Image to share, as a PNG data URL ("data:image/png;base64,..."). Build one from a
        /// texture with <see cref="Yes2SDKImage.ToPngDataUrl(UnityEngine.Texture2D)"/>. Raw base64 PNG data
        /// is also accepted and gets the PNG data URL prefix. Prefer passing an image: when it
        /// is null some platforms capture the game screen instead, which can come out blank
        /// for a WebGL canvas, so verify that on the device before relying on it.
        /// </summary>
        [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageDataUrl;

        /// <summary>Optional message to share with the image. Some platforms ignore it.</summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text;

        /// <summary>
        /// Optional data delivered to the player who opens the share. They read it with
        /// <c>Yes2SDK.Session.GetEntryPointData()</c>.
        /// </summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Data;

        /// <summary>Creates empty options; set the fields you need.</summary>
        public ContextShareOptions()
        {
        }

        /// <summary>Creates options with the given image.</summary>
        public ContextShareOptions(string imageDataUrl)
        {
            ImageDataUrl = imageDataUrl;
        }
    }
}
