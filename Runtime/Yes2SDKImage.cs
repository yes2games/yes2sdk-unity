using System;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Image helpers for APIs that take a base64 data URL, such as
    /// <see cref="NotificationOptions.ImageDataUrl"/>.
    /// </summary>
    public static class Yes2SDKImage
    {
        /// <summary>Largest data URL, in characters, the platforms accept (2 MiB).</summary>
        public const int MaxDataUrlLength = 2 * 1024 * 1024;

        private const string PngPrefix = "data:image/png;base64,";

        /// <summary>
        /// Encode a texture as a PNG data URL ("data:image/png;base64,..."). Returns null and logs a
        /// warning when the texture is null, not readable (enable Read/Write in its import settings,
        /// or use a texture created at runtime), cannot be encoded, or encodes to more than 2 MiB.
        /// </summary>
        public static string ToPngDataUrl(Texture2D texture)
        {
            return ToPngDataUrl(texture, MaxDataUrlLength);
        }

        internal static string ToPngDataUrl(Texture2D texture, int maxLength)
        {
            if (texture == null)
            {
                Yes2Log.Warning("Yes2SDKImage.ToPngDataUrl: texture is null");
                return null;
            }

            if (!texture.isReadable)
            {
                Yes2Log.Warning($"Yes2SDKImage.ToPngDataUrl: texture '{texture.name}' is not readable; enable Read/Write in its import settings");
                return null;
            }

            byte[] png;
            try
            {
                png = ImageConversion.EncodeToPNG(texture);
            }
            catch (Exception ex)
            {
                Yes2Log.Warning($"Yes2SDKImage.ToPngDataUrl: could not encode texture '{texture.name}' ({ex.Message})");
                return null;
            }

            if (png == null || png.Length == 0)
            {
                Yes2Log.Warning($"Yes2SDKImage.ToPngDataUrl: could not encode texture '{texture.name}' (compressed formats cannot be encoded)");
                return null;
            }

            // Base64 grows 4 characters per 3 bytes; check before building the string.
            long length = PngPrefix.Length + 4L * ((png.Length + 2) / 3);
            if (length > maxLength)
            {
                Yes2Log.Warning($"Yes2SDKImage.ToPngDataUrl: encoded image is {length} characters, over the {maxLength} limit; use a smaller texture");
                return null;
            }

            return PngPrefix + Convert.ToBase64String(png);
        }
    }
}
