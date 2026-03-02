using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// KTX2 texture compression tool. Batch-converts project textures to KTX2
    /// format using the toktx CLI from Khronos KTX-Software.
    /// </summary>
    public static class Yes2SDKKtx2Tool
    {
        public enum Ktx2Preset
        {
            UASTC_Zstd,
            ETC1S
        }

        [Serializable]
        public class Settings
        {
            public Ktx2Preset preset = Ktx2Preset.UASTC_Zstd;
            public int uastcQuality = 2;          // 0-4
            public int zstdLevel = 3;             // 1-22
            public int etc1sQuality = 128;        // 1-255
            public int etc1sCompressionLevel = 2; // 0-5
            public bool generateMipmaps = false;
        }

        public class ConversionResult
        {
            public string sourceAssetPath;
            public string outputPath;
            public long originalSize;
            public long ktx2Size;
            public float compressionRatio;
            public bool success;
            public string errorMessage;
        }

        private const string KTX_PACKAGE_ID = "com.unity.cloud.ktx";
        private const string TOKTX_PATH_PREF = "Yes2SDK_ToktxPath";
        private const string KTX_SOFTWARE_RELEASES_URL = "https://github.com/KhronosGroup/KTX-Software/releases";

        public static string KtxSoftwareReleasesUrl => KTX_SOFTWARE_RELEASES_URL;

        // --- Package Detection ---

        /// <summary>
        /// Check if com.unity.cloud.ktx is installed.
        /// </summary>
        public static bool IsKtxPackageInstalled()
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == StatusCode.Success)
            {
                return listRequest.Result.Any(p =>
                    p.name == KTX_PACKAGE_ID ||
                    p.name == "com.atteneder.ktx");
            }

            return false;
        }

        /// <summary>
        /// Install com.unity.cloud.ktx via Package Manager.
        /// </summary>
        public static void InstallKtxPackage()
        {
            Debug.Log($"[Yes2SDK] Installing {KTX_PACKAGE_ID}...");
            Client.Add(KTX_PACKAGE_ID);
        }

        // --- toktx Detection ---

        /// <summary>
        /// Find the toktx executable. Checks EditorPrefs, PATH, and platform-specific locations.
        /// Returns the path if found, null otherwise.
        /// </summary>
        public static string FindToktx()
        {
            // 1. Check saved path
            var saved = EditorPrefs.GetString(TOKTX_PATH_PREF, "");
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
            {
                if (ValidateToktx(saved) != null)
                    return saved;
            }

            // 2. Check PATH
            var inPath = FindInPath("toktx");
            if (inPath != null && ValidateToktx(inPath) != null)
            {
                SaveToktxPath(inPath);
                return inPath;
            }

            // 3. Platform-specific locations
            string[] candidates;
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                candidates = new[]
                {
                    "/usr/local/bin/toktx",
                    "/opt/homebrew/bin/toktx",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/toktx")
                };
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                candidates = new[]
                {
                    Path.Combine(programFiles, "KTX-Software", "bin", "toktx.exe"),
                    Path.Combine(programFiles + " (x86)", "KTX-Software", "bin", "toktx.exe")
                };
            }
            else
            {
                candidates = new[]
                {
                    "/usr/local/bin/toktx",
                    "/usr/bin/toktx"
                };
            }

            foreach (var path in candidates)
            {
                if (File.Exists(path) && ValidateToktx(path) != null)
                {
                    SaveToktxPath(path);
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Validate toktx by running --version. Returns version string or null.
        /// </summary>
        public static string ValidateToktx(string toktxPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toktxPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                var version = !string.IsNullOrEmpty(output) ? output.Trim() : error.Trim();
                return !string.IsNullOrEmpty(version) ? version : null;
            }
            catch
            {
                return null;
            }
        }

        public static void SaveToktxPath(string path)
        {
            EditorPrefs.SetString(TOKTX_PATH_PREF, path);
        }

        // --- Conversion ---

        /// <summary>
        /// Convert a single texture to KTX2.
        /// </summary>
        public static ConversionResult ConvertTexture(string assetPath, Settings settings, string toktxPath)
        {
            var result = new ConversionResult
            {
                sourceAssetPath = assetPath
            };

            try
            {
                if (string.IsNullOrEmpty(toktxPath))
                {
                    result.success = false;
                    result.errorMessage = "toktx path is not set.";
                    return result;
                }

                // Determine source file path
                var fullSourcePath = Path.GetFullPath(assetPath);
                var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();
                string inputPath = fullSourcePath;
                string tempFile = null;

                // If not PNG or JPG, export to temp PNG
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                {
                    tempFile = ExportToTempPng(assetPath);
                    if (tempFile == null)
                    {
                        result.success = false;
                        result.errorMessage = $"Failed to export {assetPath} to PNG for conversion.";
                        return result;
                    }
                    inputPath = tempFile;
                }

                result.originalSize = new FileInfo(inputPath).Length;

                // Determine output path
                var relativePath = assetPath;
                if (relativePath.StartsWith("Assets/"))
                    relativePath = relativePath.Substring("Assets/".Length);

                var ktx2RelativePath = Path.ChangeExtension(relativePath, ".ktx2");
                var outputDir = Path.Combine(Application.streamingAssetsPath, "ktx2");
                var outputFullPath = Path.Combine(outputDir, ktx2RelativePath);
                result.outputPath = $"ktx2/{ktx2RelativePath}";

                // Ensure output directory exists
                var outputFileDir = Path.GetDirectoryName(outputFullPath);
                if (!Directory.Exists(outputFileDir))
                    Directory.CreateDirectory(outputFileDir);

                // Build toktx arguments
                var args = BuildToktxArgs(inputPath, outputFullPath, settings);

                Debug.Log($"[Yes2SDK] KTX2 cmd: \"{toktxPath}\" {args}");

                // Run toktx
                var psi = new ProcessStartInfo
                {
                    FileName = toktxPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Process process;
                try
                {
                    process = Process.Start(psi);
                }
                catch (Exception ex)
                {
                    result.success = false;
                    result.errorMessage = $"Failed to start toktx at \"{toktxPath}\": {ex.Message}";
                    return result;
                }

                if (process == null)
                {
                    result.success = false;
                    result.errorMessage = $"Process.Start returned null for \"{toktxPath}\".";
                    return result;
                }

                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);

                var exitCode = process.ExitCode;
                process.Dispose();

                if (exitCode != 0)
                {
                    result.success = false;
                    result.errorMessage = $"toktx exited with code {exitCode}: {stderr}";
                    return result;
                }

                // Clean up temp file
                if (tempFile != null && File.Exists(tempFile))
                    File.Delete(tempFile);

                if (!File.Exists(outputFullPath))
                {
                    result.success = false;
                    result.errorMessage = "toktx did not produce an output file.";
                    return result;
                }

                result.ktx2Size = new FileInfo(outputFullPath).Length;
                result.compressionRatio = result.originalSize > 0
                    ? (float)result.originalSize / result.ktx2Size
                    : 0;
                result.success = true;
            }
            catch (Exception e)
            {
                result.success = false;
                result.errorMessage = e.Message;
            }

            return result;
        }

        /// <summary>
        /// Batch convert textures. Shows a cancelable progress bar.
        /// </summary>
        public static List<ConversionResult> BatchConvert(string[] assetPaths, Settings settings, string toktxPath)
        {
            if (string.IsNullOrEmpty(toktxPath))
            {
                Debug.LogError("[Yes2SDK] KTX2: toktx path is not set. Please locate toktx in Yes2SDK > Settings > Utilities.");
                return new List<ConversionResult>();
            }

            var results = new List<ConversionResult>();

            for (int i = 0; i < assetPaths.Length; i++)
            {
                var path = assetPaths[i];
                var canceled = EditorUtility.DisplayCancelableProgressBar(
                    "Converting to KTX2",
                    $"Processing {Path.GetFileName(path)}... ({i + 1}/{assetPaths.Length})",
                    (float)i / assetPaths.Length);

                if (canceled)
                {
                    Debug.Log($"[Yes2SDK] KTX2 conversion canceled after {i} of {assetPaths.Length} textures.");
                    break;
                }

                var result = ConvertTexture(path, settings, toktxPath);
                results.Add(result);

                if (result.success)
                {
                    Debug.Log($"[Yes2SDK] KTX2: {path} -> {result.outputPath} ({result.compressionRatio:F1}x)");
                }
                else
                {
                    Debug.LogWarning($"[Yes2SDK] KTX2 failed: {path}: {result.errorMessage}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            return results;
        }

        /// <summary>
        /// Find all texture assets in a folder.
        /// </summary>
        public static string[] FindTexturesInFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return Array.Empty<string>();

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.EndsWith(".ktx2") && !p.EndsWith(".basis"))
                .ToArray();
        }

        // --- Private Helpers ---

        private static string BuildToktxArgs(string inputPath, string outputPath, Settings settings)
        {
            var args = "--t2 --nowarn --assign_oetf srgb ";

            if (settings.preset == Ktx2Preset.UASTC_Zstd)
            {
                args += $"--encode uastc --uastc_quality {settings.uastcQuality} --zcmp {settings.zstdLevel} ";
            }
            else
            {
                args += $"--encode etc1s --clevel {settings.etc1sCompressionLevel} --qlevel {settings.etc1sQuality} ";
            }

            if (settings.generateMipmaps)
                args += "--genmipmap ";

            args += $"\"{outputPath}\" \"{inputPath}\"";
            return args;
        }

        private static string FindInPath(string executable)
        {
            try
            {
                var shell = Application.platform == RuntimePlatform.WindowsEditor ? "where" : "which";
                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // Take the first line (which might have multiple results)
                    var firstLine = output.Split('\n')[0].Trim();
                    return File.Exists(firstLine) ? firstLine : null;
                }
            }
            catch { }

            return null;
        }

        private static string ExportToTempPng(string assetPath)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) return null;

                // Temporarily make texture readable
                bool wasReadable = importer.isReadable;
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null) return null;

                var png = texture.EncodeToPNG();

                // Restore readable state
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }

                if (png == null) return null;

                var tempPath = Path.Combine(Path.GetTempPath(), $"yes2sdk_ktx2_{Guid.NewGuid()}.png");
                File.WriteAllBytes(tempPath, png);
                return tempPath;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Yes2SDK] Failed to export texture to PNG: {e.Message}");
                return null;
            }
        }
    }
}
