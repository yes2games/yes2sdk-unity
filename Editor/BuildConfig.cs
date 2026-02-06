using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Target platform for Yes2SDK builds.
    /// </summary>
    public enum TargetPlatform
    {
        Poki,
        CrazyGames,
        // Facebook, // Coming soon - template not yet implemented
        Debug
    }

    /// <summary>
    /// Build configuration for a specific platform.
    /// </summary>
    public class BuildConfig
    {
        public TargetPlatform Platform { get; }
        public string TemplateName { get; }
        public string DisplayName { get; }
        public WebGLCompressionFormat Compression { get; }
        public bool DecompressionFallback { get; }
        public ManagedStrippingLevel CodeStripping { get; }
        public WebGLExceptionSupport ExceptionSupport { get; }
        public string Description { get; }

        private BuildConfig(
            TargetPlatform platform,
            string templateName,
            string displayName,
            WebGLCompressionFormat compression,
            bool decompressionFallback,
            ManagedStrippingLevel codeStripping,
            WebGLExceptionSupport exceptionSupport,
            string description)
        {
            Platform = platform;
            TemplateName = templateName;
            DisplayName = displayName;
            Compression = compression;
            DecompressionFallback = decompressionFallback;
            CodeStripping = codeStripping;
            ExceptionSupport = exceptionSupport;
            Description = description;
        }

        /// <summary>
        /// Poki build configuration.
        /// - No compression (Poki handles CDN compression)
        /// - Poki-specific template with index.json
        /// </summary>
        public static BuildConfig Poki => new BuildConfig(
            platform: TargetPlatform.Poki,
            templateName: "Yes2SDK-Poki",
            displayName: "Poki",
            compression: WebGLCompressionFormat.Disabled,
            decompressionFallback: false,
            codeStripping: ManagedStrippingLevel.Low,
            exceptionSupport: WebGLExceptionSupport.None,
            description: "Poki.com - Compression disabled (CDN handles it), index.json template required"
        );

        /// <summary>
        /// CrazyGames build configuration.
        /// - Gzip compression
        /// - Standard template with CrazyGames SDK
        /// </summary>
        public static BuildConfig CrazyGames => new BuildConfig(
            platform: TargetPlatform.CrazyGames,
            templateName: "Yes2SDK-CrazyGames",
            displayName: "CrazyGames",
            compression: WebGLCompressionFormat.Gzip,
            decompressionFallback: true,
            codeStripping: ManagedStrippingLevel.Low,
            exceptionSupport: WebGLExceptionSupport.None,
            description: "CrazyGames.com - Gzip compression, standard template"
        );

        // Facebook Instant Games - Coming soon (template not yet implemented)
        // public static BuildConfig Facebook => new BuildConfig(...);

        /// <summary>
        /// Debug/local testing build configuration.
        /// - No compression for faster builds
        /// - Full exception support for debugging
        /// </summary>
        public static BuildConfig Debug => new BuildConfig(
            platform: TargetPlatform.Debug,
            templateName: "Yes2SDK",
            displayName: "Debug (Local)",
            compression: WebGLCompressionFormat.Disabled,
            decompressionFallback: false,
            codeStripping: ManagedStrippingLevel.Disabled,
            exceptionSupport: WebGLExceptionSupport.FullWithStacktrace,
            description: "Local testing - No compression, full debugging support"
        );

        /// <summary>
        /// Get build config for a specific platform.
        /// </summary>
        public static BuildConfig GetConfig(TargetPlatform platform)
        {
            return platform switch
            {
                TargetPlatform.Poki => Poki,
                TargetPlatform.CrazyGames => CrazyGames,
                // TargetPlatform.Facebook => Facebook, // Coming soon
                TargetPlatform.Debug => Debug,
                _ => Debug
            };
        }

        /// <summary>
        /// Apply this configuration to Unity Player Settings.
        /// </summary>
        public void ApplySettings()
        {
            // WebGL Template
            PlayerSettings.WebGL.template = $"PROJECT:{TemplateName}";

            // Compression
            PlayerSettings.WebGL.compressionFormat = Compression;
            PlayerSettings.WebGL.decompressionFallback = DecompressionFallback;

            // Code Stripping
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, CodeStripping);

            // Exception Support
            PlayerSettings.WebGL.exceptionSupport = ExceptionSupport;

            // Common settings
            PlayerSettings.runInBackground = true;

            UnityEngine.Debug.Log($"[Yes2SDK] Applied build settings for {DisplayName}");
        }
    }
}
