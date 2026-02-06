using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Editor tools for Yes2SDK installation and configuration.
    /// </summary>
    public static class Yes2SDKInstaller
    {
        private const string PackagePath = "Packages/com.yes2games.yes2sdk";
        private const string TemplateName = "Yes2SDK";

        /// <summary>
        /// Install the Yes2SDK WebGL template to the project.
        /// </summary>
        [MenuItem("Yes2SDK/Install WebGL Template")]
        public static void InstallWebGLTemplate()
        {
            var sourceDir = Path.Combine(PackagePath, "Assets/WebGLTemplates", TemplateName);
            var targetDir = Path.Combine("Assets/WebGLTemplates", TemplateName);

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[Yes2SDK] Template source not found at: {sourceDir}");
                return;
            }

            // Create target directory
            if (!Directory.Exists("Assets/WebGLTemplates"))
            {
                Directory.CreateDirectory("Assets/WebGLTemplates");
            }

            // Copy template
            CopyDirectory(sourceDir, targetDir);

            AssetDatabase.Refresh();
            Debug.Log($"[Yes2SDK] WebGL template installed to: {targetDir}");

            // Set as active template
            PlayerSettings.WebGL.template = $"PROJECT:{TemplateName}";
            Debug.Log($"[Yes2SDK] WebGL template set to: PROJECT:{TemplateName}");
        }

        /// <summary>
        /// Configure recommended project settings for Yes2SDK.
        /// </summary>
        [MenuItem("Yes2SDK/Set Project Settings")]
        public static void SetProjectSettings()
        {
            // Enable run in background (required for proper ad handling)
            PlayerSettings.runInBackground = true;
            Debug.Log("[Yes2SDK] Enabled 'Run In Background'");

            // Set WebGL template if installed
            var templatePath = Path.Combine("Assets/WebGLTemplates", TemplateName);
            if (Directory.Exists(templatePath))
            {
                PlayerSettings.WebGL.template = $"PROJECT:{TemplateName}";
                Debug.Log($"[Yes2SDK] WebGL template set to: PROJECT:{TemplateName}");
            }
            else
            {
                Debug.LogWarning("[Yes2SDK] WebGL template not installed. Run 'Yes2SDK/Install WebGL Template' first.");
            }

            // Disable decompression fallback for better performance
            PlayerSettings.WebGL.decompressionFallback = false;
            Debug.Log("[Yes2SDK] Disabled WebGL decompression fallback");

            // Set compression format to Gzip (widely supported)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            Debug.Log("[Yes2SDK] Set WebGL compression to Gzip");

            Debug.Log("[Yes2SDK] Project settings configured successfully!");
        }

        /// <summary>
        /// Validate Yes2SDK settings on editor load.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckSettings()
        {
            // Only check when building for WebGL
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                return;

            // Check run in background
            if (!PlayerSettings.runInBackground)
            {
                Debug.LogWarning("[Yes2SDK] 'Run In Background' is disabled. This may cause issues with ads. Enable via Yes2SDK/Set Project Settings.");
            }

            // Check template
            var currentTemplate = PlayerSettings.WebGL.template;
            if (!currentTemplate.Contains(TemplateName))
            {
                Debug.LogWarning($"[Yes2SDK] WebGL template is not set to Yes2SDK (current: {currentTemplate}). Install via Yes2SDK/Install WebGL Template.");
            }
        }

        /// <summary>
        /// Open Yes2SDK documentation.
        /// </summary>
        [MenuItem("Yes2SDK/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/yes2games/yes2sdk-unity");
        }

        #region Utility Methods

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            // Copy subdirectories recursively
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, dirName));
            }
        }

        #endregion
    }
}
