using System;
using System.IO;
using System.Threading.Tasks;
using KtxUnity;
using UnityEngine;
using UnityEngine.UI;

namespace Yes2SDK
{
    /// <summary>
    /// Loads a KTX2 texture from StreamingAssets at runtime and assigns it to
    /// the Image, RawImage, or SpriteRenderer on the same GameObject.
    /// Requires com.unity.cloud.ktx package.
    /// </summary>
    [AddComponentMenu("Yes2SDK/KTX2 Image")]
    public class Yes2SDKKtx2Image : MonoBehaviour
    {
        [Tooltip("Relative path inside StreamingAssets (e.g. ktx2/Textures/hero.ktx2)")]
        [SerializeField] private string _ktx2Path;

        [Tooltip("Enable for normal maps or linear-space textures")]
        [SerializeField] private bool _linearColor;

        /// <summary>
        /// The KTX2 path relative to StreamingAssets.
        /// </summary>
        public string Ktx2Path
        {
            get => _ktx2Path;
            set => _ktx2Path = value;
        }

        /// <summary>
        /// Whether to load the texture in linear color space.
        /// </summary>
        public bool LinearColor
        {
            get => _linearColor;
            set => _linearColor = value;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_ktx2Path))
            {
                Debug.LogWarning($"[Yes2SDK]KTX2 path not set on {gameObject.name}");
                return;
            }

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var url = Path.Combine(Application.streamingAssetsPath, _ktx2Path);

                var ktxTexture = new KtxTexture();
                var result = await ktxTexture.LoadFromUrl(url);

                if (result == null || result.texture == null)
                {
                    Debug.LogError($"[Yes2SDK]KTX2 load failed: {_ktx2Path}");
                    return;
                }

                var texture = result.texture;
                texture.wrapMode = TextureWrapMode.Clamp;

                ApplyTexture(texture);

                Debug.Log($"[Yes2SDK]KTX2 loaded: {_ktx2Path} ({texture.width}x{texture.height})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Yes2SDK]KTX2 load error ({_ktx2Path}): {e.Message}");
            }
        }

        private void ApplyTexture(Texture2D texture)
        {
            // Try RawImage first (most direct texture assignment)
            var rawImage = GetComponent<RawImage>();
            if (rawImage != null)
            {
                rawImage.texture = texture;
                return;
            }

            // Try Image (needs Sprite wrapper)
            var image = GetComponent<Image>();
            if (image != null)
            {
                var rect = new Rect(0, 0, texture.width, texture.height);
                var pivot = new Vector2(0.5f, 0.5f);
                image.sprite = Sprite.Create(texture, rect, pivot);
                return;
            }

            // Try SpriteRenderer
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                var rect = new Rect(0, 0, texture.width, texture.height);
                var pivot = new Vector2(0.5f, 0.5f);
                spriteRenderer.sprite = Sprite.Create(texture, rect, pivot, 100f);
                return;
            }

            Debug.LogWarning($"[Yes2SDK]No Image, RawImage, or SpriteRenderer found on {gameObject.name} for KTX2 texture");
        }
    }
}
