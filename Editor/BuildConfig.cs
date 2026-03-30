using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Build configuration for Yes2SDK SuperSDK pipeline.
    /// Single template — platform selection happens in the dashboard, not Unity.
    /// </summary>
    public class BuildConfig
    {
        public string TemplateName => "Yes2SDK-SuperSDK";
        public string DisplayName => "Yes2SDK (SuperSDK)";
        public WebGLCompressionFormat Compression { get; }
        public bool DecompressionFallback { get; }
        public ManagedStrippingLevel CodeStripping { get; }
        public WebGLExceptionSupport ExceptionSupport { get; }
        public string Description { get; }

        private BuildConfig(
            WebGLCompressionFormat compression,
            bool decompressionFallback,
            ManagedStrippingLevel codeStripping,
            WebGLExceptionSupport exceptionSupport,
            string description)
        {
            Compression = compression;
            DecompressionFallback = decompressionFallback;
            CodeStripping = codeStripping;
            ExceptionSupport = exceptionSupport;
            Description = description;
        }

        /// <summary>
        /// Default build config for the SuperSDK pipeline.
        /// - No compression (dashboard/platform handles CDN compression)
        /// - Medium code stripping for size
        /// - No exceptions for production
        /// </summary>
        public static BuildConfig Default => new BuildConfig(
            compression: WebGLCompressionFormat.Disabled,
            decompressionFallback: false,
            codeStripping: ManagedStrippingLevel.Medium,
            exceptionSupport: WebGLExceptionSupport.None,
            description: "Build for Yes2SDK Dashboard.\n" +
                         "Upload the build zip to the dashboard to inject SDK and bundle for specific platforms.\n" +
                         "No compression — platforms handle CDN delivery."
        );

        /// <summary>
        /// Apply these build settings to the Unity project.
        /// </summary>
        public void ApplySettings()
        {
            PlayerSettings.WebGL.template = $"PROJECT:{TemplateName}";
            PlayerSettings.WebGL.compressionFormat = Compression;
            PlayerSettings.WebGL.decompressionFallback = DecompressionFallback;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, CodeStripping);
            PlayerSettings.WebGL.exceptionSupport = ExceptionSupport;

            UnityEngine.Debug.Log($"[Yes2SDK] Build settings applied: {DisplayName}");
            UnityEngine.Debug.Log($"[Yes2SDK]   Template: {TemplateName}");
            UnityEngine.Debug.Log($"[Yes2SDK]   Compression: {Compression}");
            UnityEngine.Debug.Log($"[Yes2SDK]   Stripping: {CodeStripping}");
        }
    }
}
