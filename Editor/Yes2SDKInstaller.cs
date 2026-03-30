using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Installs the Yes2SDK-SuperSDK WebGL template to the project.
    /// Simplified for SuperSDK pipeline — only one template needed.
    /// </summary>
    public static class Yes2SDKInstaller
    {
        private const string PackagePath = "Packages/com.yes2games.yes2sdk";
        private const string TemplateName = "Yes2SDK-SuperSDK";

        /// <summary>
        /// Check if the SuperSDK template is installed.
        /// </summary>
        public static bool IsSetupComplete()
        {
            return Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", TemplateName));
        }

        /// <summary>
        /// Install the template and configure project settings.
        /// </summary>
        public static bool PerformSetup()
        {
            Debug.Log("[Yes2SDK] Starting setup...");

            bool success = InstallTemplate();

            if (success)
            {
                // Configure project settings
                PlayerSettings.runInBackground = true;

                Debug.Log("[Yes2SDK] Setup complete.");
            }

            return success;
        }

        /// <summary>
        /// Install the Yes2SDK-SuperSDK template to Assets/WebGLTemplates/.
        /// </summary>
        public static bool InstallTemplate()
        {
            // Find the package
            string packageRoot = FindPackageRoot();
            if (string.IsNullOrEmpty(packageRoot))
            {
                Debug.LogError("[Yes2SDK] Could not find Yes2SDK package. Is it installed?");
                return false;
            }

            string sourceDir = Path.Combine(packageRoot, "Assets", "WebGLTemplates", TemplateName);
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[Yes2SDK] Template source not found: {sourceDir}");
                return false;
            }

            string destDir = Path.Combine(Application.dataPath, "WebGLTemplates", TemplateName);

            try
            {
                // Create destination directory
                if (!Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates")))
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "WebGLTemplates"));

                // Copy template
                CopyDirectory(sourceDir, destDir);
                Debug.Log($"[Yes2SDK] Template installed: {TemplateName}");

                AssetDatabase.Refresh();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Yes2SDK] Failed to install template: {e.Message}");
                return false;
            }
        }

        // Alias for backward compatibility
        public static bool InstallAllTemplates() => InstallTemplate();

        private static string FindPackageRoot()
        {
            // Try direct path first (local package)
            if (Directory.Exists(PackagePath))
                return PackagePath;

            // Try via Package Manager
            var packageInfo = PackageInfo.FindForAssetPath(PackagePath);
            if (packageInfo != null)
                return packageInfo.resolvedPath;

            // Try common local package paths
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string localPath = Path.Combine(projectRoot, PackagePath);
            if (Directory.Exists(localPath))
                return localPath;

            return null;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                // Skip .meta files from package — Unity generates its own
                if (file.EndsWith(".meta"))
                    continue;

                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(destDir, relativePath);

                string destFileDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destFileDir))
                    Directory.CreateDirectory(destFileDir);

                File.Copy(file, destFile, true);
            }
        }
    }
}
