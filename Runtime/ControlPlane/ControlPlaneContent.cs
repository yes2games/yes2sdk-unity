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
            var artifact = await LoadAsync(digest, MaxTextBytes, token, "text/plain", "application/json");
            try
            {
                return StrictUtf8.GetString(File.ReadAllBytes(artifact.FilePath));
            }
            catch (DecoderFallbackException e)
            {
                throw new InvalidDataException(digest + " is not valid UTF-8", e);
            }
        }

        public async Task<Texture2D> LoadTextureAsync(string digest, CancellationToken token)
        {
            var artifact = await LoadAsync(digest, MaxImageBytes, token, "image/png", "image/jpeg");
            var bytes = File.ReadAllBytes(artifact.FilePath);
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

        private async Task<VerifiedArtifact> LoadAsync(string digest, long maxBytes, CancellationToken token, params string[] mediaTypes)
        {
            var artifact = await _controlPlane.GetArtifactAsync(digest, token);
            if (Array.IndexOf(mediaTypes, artifact.MediaType) < 0)
            {
                throw new NotSupportedException(digest + " is " + artifact.MediaType + ", not " + string.Join(" or ", mediaTypes));
            }
            if (artifact.Size > maxBytes)
            {
                throw new InvalidDataException(digest + " is " + artifact.Size + " bytes, over " + maxBytes);
            }
            token.ThrowIfCancellationRequested();
            return artifact;
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
