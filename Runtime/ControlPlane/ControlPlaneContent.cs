using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>Decodes verified Artifacts into UTF-8 text or caller-owned PNG/JPEG textures.</summary>
    public sealed class ControlPlaneContent
    {
        public const long MaxTextBytes = 4L * 1024 * 1024;
        public const long MaxImageBytes = 16L * 1024 * 1024;
        public const int MaxImageDimension = 4096;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Yes2SDKControlPlane _controlPlane;

        internal ControlPlaneContent(Yes2SDKControlPlane controlPlane)
        {
            _controlPlane = controlPlane;
        }

        public async Task<string> LoadTextAsync(string digest, CancellationToken token)
        {
            var bytes = await LoadAsync(digest, MaxTextBytes, token, "text/plain", "application/json");
            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException e)
            {
                throw new InvalidDataException(digest + " is not valid UTF-8", e);
            }
        }

        public async Task<Texture2D> LoadTextureAsync(string digest, CancellationToken token)
        {
            var bytes = await LoadAsync(digest, MaxImageBytes, token, "image/png", "image/jpeg");
            var declared = DeclaredSize(bytes);
            if (declared == null || !Fits(declared.Value.x) || !Fits(declared.Value.y))
            {
                throw new InvalidDataException(digest + " header does not declare a PNG or JPEG of 1.." + MaxImageDimension + " pixels per side" + (declared == null ? "" : " (" + declared.Value.x + "x" + declared.Value.y + ")"));
            }
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(bytes) || texture.width > MaxImageDimension || texture.height > MaxImageDimension)
            {
                var size = texture.width + "x" + texture.height;
                Destroy(texture);
                throw new InvalidDataException(digest + " is not a PNG or JPEG of 1.." + MaxImageDimension + " pixels per side (" + size + ")");
            }
            if (token.IsCancellationRequested)
            {
                Destroy(texture);
                token.ThrowIfCancellationRequested();
            }
            return texture;
        }

        internal static Vector2Int? DeclaredSize(byte[] bytes)
        {
            if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G' && bytes[12] == 'I' && bytes[13] == 'H' && bytes[14] == 'D' && bytes[15] == 'R')
            {
                return new Vector2Int(BigEndian(bytes, 16, 4), BigEndian(bytes, 20, 4));
            }
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            {
                return null;
            }
            for (var i = 2; i + 3 < bytes.Length;)
            {
                var marker = bytes[i + 1];
                if (bytes[i] != 0xFF || marker == 0xD9 || marker == 0xDA)
                {
                    return null;
                }
                if (marker == 0xFF || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += marker == 0xFF ? 1 : 2;
                    continue;
                }
                var length = BigEndian(bytes, i + 2, 2);
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    return length >= 7 && i + 8 < bytes.Length ? new Vector2Int(BigEndian(bytes, i + 7, 2), BigEndian(bytes, i + 5, 2)) : (Vector2Int?)null;
                }
                if (length < 2)
                {
                    return null;
                }
                i += 2 + length;
            }
            return null;
        }

        private static int BigEndian(byte[] bytes, int offset, int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = unchecked((value << 8) | bytes[offset + i]);
            }
            return value;
        }

        private static bool Fits(int pixels)
        {
            return pixels >= 1 && pixels <= MaxImageDimension;
        }

        private async Task<byte[]> LoadAsync(string digest, long maxBytes, CancellationToken token, params string[] mediaTypes)
        {
            var descriptor = _controlPlane.Descriptor(digest);
            if (descriptor != null)
            {
                var mediaType = (string)descriptor["mediaType"];
                if (Array.IndexOf(mediaTypes, mediaType) < 0)
                {
                    throw new NotSupportedException(digest + " is " + mediaType + ", not " + string.Join(" or ", mediaTypes));
                }
                if (ControlPlaneStrictJson.TryGetSafeInteger(descriptor["size"], out var size) && size > maxBytes)
                {
                    throw new InvalidDataException(digest + " is " + size + " bytes, over " + maxBytes);
                }
            }
            var artifact = await _controlPlane.GetArtifactAsync(digest, token);
            token.ThrowIfCancellationRequested();
            try
            {
                return File.ReadAllBytes(artifact.FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new ControlPlaneException(ControlPlaneErrorCategory.CacheUnavailable, e.GetType().Name + ": " + e.Message);
            }
        }

        private static void Destroy(Texture2D texture)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
