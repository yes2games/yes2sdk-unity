using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Utility class for Yes2SDK installation and configuration.
    /// Used by Yes2SDKWindow for one-click setup.
    /// </summary>
    public static class Yes2SDKInstaller
    {
        private const string PackagePath = "Packages/com.yes2games.yes2sdk";

        /// <summary>
        /// Check if Yes2SDK is properly set up (ALL templates must exist).
        /// </summary>
        public static bool IsSetupComplete()
        {
            // Check if ALL Yes2SDK templates are installed
            bool hasDebugTemplate = Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK"));
            bool hasPokiTemplate = Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK-Poki"));
            bool hasCrazyGamesTemplate = Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK-CrazyGames"));
            bool hasYandexTemplate = Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK-Yandex"));

            // Check run in background setting
            bool hasRunInBackground = PlayerSettings.runInBackground;

            // All templates must exist
            bool allTemplatesExist = hasDebugTemplate && hasPokiTemplate && hasCrazyGamesTemplate && hasYandexTemplate;

            Debug.Log($"[Yes2SDK] Setup check - Debug:{hasDebugTemplate}, Poki:{hasPokiTemplate}, CrazyGames:{hasCrazyGamesTemplate}, Yandex:{hasYandexTemplate}, RunInBackground:{hasRunInBackground}");

            return allTemplatesExist && hasRunInBackground;
        }

        /// <summary>
        /// Perform complete Yes2SDK setup - install templates and configure settings.
        /// </summary>
        public static bool PerformSetup()
        {
            Debug.Log("[Yes2SDK] Starting setup...");

            // Install all templates
            bool templatesInstalled = InstallAllTemplates();
            if (!templatesInstalled)
            {
                return false;
            }

            // Configure project settings
            ConfigureProjectSettings();

            Debug.Log("[Yes2SDK] Setup completed successfully!");
            return true;
        }

        /// <summary>
        /// Install all Yes2SDK WebGL templates.
        /// </summary>
        public static bool InstallAllTemplates()
        {
            string packagePath = GetPackagePath();
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError("[Yes2SDK] Could not find Yes2SDK package path. Make sure the package is installed correctly.");
                return false;
            }

            Debug.Log($"[Yes2SDK] Package path: {packagePath}");

            string sourceTemplatesPath = Path.Combine(packagePath, "Assets", "WebGLTemplates");
            string destTemplatesPath = Path.Combine(Application.dataPath, "WebGLTemplates");

            Debug.Log($"[Yes2SDK] Looking for templates at: {sourceTemplatesPath}");
            Debug.Log($"[Yes2SDK] Will install to: {destTemplatesPath}");

            if (!Directory.Exists(sourceTemplatesPath))
            {
                Debug.LogError($"[Yes2SDK] Templates not found in package at: {sourceTemplatesPath}");
                Debug.LogError("[Yes2SDK] Make sure the package contains Assets/WebGLTemplates folder.");
                return false;
            }

            // Create destination directory
            if (!Directory.Exists(destTemplatesPath))
            {
                Directory.CreateDirectory(destTemplatesPath);
            }

            // Copy all templates
            var templates = new[] { "Yes2SDK", "Yes2SDK-Poki", "Yes2SDK-CrazyGames", "Yes2SDK-Yandex" };
            int installedCount = 0;

            foreach (var template in templates)
            {
                string sourcePath = Path.Combine(sourceTemplatesPath, template);
                string destPath = Path.Combine(destTemplatesPath, template);

                if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, destPath);
                    Debug.Log($"[Yes2SDK] Installed template: {template}");
                    installedCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Yes2SDK] Installed {installedCount} templates.");
            return installedCount > 0;
        }

        /// <summary>
        /// Configure recommended project settings for Yes2SDK.
        /// </summary>
        public static void ConfigureProjectSettings()
        {
            // Enable run in background (required for proper ad handling)
            PlayerSettings.runInBackground = true;
            Debug.Log("[Yes2SDK] Enabled 'Run In Background'");

            // Set default template
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates", "Yes2SDK");
            if (Directory.Exists(templatePath))
            {
                PlayerSettings.WebGL.template = "PROJECT:Yes2SDK";
                Debug.Log("[Yes2SDK] Set default WebGL template to Yes2SDK");
            }
        }

        /// <summary>
        /// Get the package installation path.
        /// </summary>
        public static string GetPackagePath()
        {
            // Method 1: Use Unity's PackageInfo API (works for UPM installed packages)
            var packageInfo = PackageInfo.FindForAssetPath("Packages/com.yes2games.yes2sdk/package.json");
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                Debug.Log($"[Yes2SDK] Found package via PackageInfo: {packageInfo.resolvedPath}");
                return packageInfo.resolvedPath;
            }

            // Method 2: Find via this script's own location (works for local development)
            string[] guids = AssetDatabase.FindAssets("Yes2SDKInstaller t:MonoScript");
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (scriptPath.EndsWith("Yes2SDKInstaller.cs"))
                {
                    // Script is at: {PackageRoot}/Editor/Yes2SDKInstaller.cs
                    // So package root is 2 levels up
                    string fullScriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scriptPath));
                    string editorDir = Path.GetDirectoryName(fullScriptPath);
                    string packageRoot = Path.GetDirectoryName(editorDir);

                    if (Directory.Exists(Path.Combine(packageRoot, "Assets", "WebGLTemplates")))
                    {
                        Debug.Log($"[Yes2SDK] Found package via script location: {packageRoot}");
                        return packageRoot;
                    }
                }
            }

            // Fallback: Check Packages folder (embedded package)
            string packagesPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "com.yes2games.yes2sdk");
            if (Directory.Exists(packagesPath))
            {
                Debug.Log($"[Yes2SDK] Found package in Packages folder: {packagesPath}");
                return packagesPath;
            }

            // Fallback: Check Library/PackageCache (installed via UPM registry)
            string packageCachePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                foreach (var dir in Directory.GetDirectories(packageCachePath))
                {
                    if (Path.GetFileName(dir).StartsWith("com.yes2games.yes2sdk"))
                    {
                        Debug.Log($"[Yes2SDK] Found package in PackageCache: {dir}");
                        return dir;
                    }
                }
            }

            Debug.LogError("[Yes2SDK] Could not locate package. Checked: PackageInfo API, script location, Packages folder, PackageCache.");
            return null;
        }

        /// <summary>
        /// Copy directory recursively.
        /// </summary>
        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, dirName));
            }
        }
    }
}
