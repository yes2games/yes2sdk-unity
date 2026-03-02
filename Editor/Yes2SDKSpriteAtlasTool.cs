using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Sprite Atlas automation tool. Scans for loose sprites and creates
    /// WebGL-optimized SpriteAtlas assets grouped by folder.
    /// </summary>
    public static class Yes2SDKSpriteAtlasTool
    {
        [Serializable]
        public class Settings
        {
            public int padding = 4;
            public int maxTextureSize = 2048;
            public bool enableTightPacking = true;
            public bool enableRotation = false;
            public bool generateMipMaps = false;
            public bool sRGB = true;
            public int compressionQuality = 50;
        }

        public class AtlasReport
        {
            public string atlasName;
            public string folderPath;
            public string outputPath;
            public int spriteCount;
            public bool alreadyExisted;
            public bool created;
        }

        public class ScanResult
        {
            public string folderPath;
            public string folderName;
            public List<string> spritePaths = new List<string>();
        }

        /// <summary>
        /// Scan source folders for sprites not already packed in any SpriteAtlas.
        /// Returns results grouped by parent folder.
        /// </summary>
        public static List<ScanResult> ScanForLooseSprites(string[] sourceFolders)
        {
            // Find all existing atlas packable paths for quick lookup
            var packedPaths = GetAllPackedPaths();

            var results = new Dictionary<string, ScanResult>();

            foreach (var folder in sourceFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[Yes2SDK] Sprite Atlas: Folder not found: {folder}. Skipped.");
                    continue;
                }

                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    // Skip if already packed
                    if (IsPathCoveredByAtlas(path, packedPaths))
                        continue;

                    // Group by immediate parent folder
                    var parentFolder = Path.GetDirectoryName(path).Replace('\\', '/');
                    if (!results.TryGetValue(parentFolder, out var result))
                    {
                        result = new ScanResult
                        {
                            folderPath = parentFolder,
                            folderName = Path.GetFileName(parentFolder)
                        };
                        results[parentFolder] = result;
                    }

                    if (!result.spritePaths.Contains(path))
                        result.spritePaths.Add(path);
                }
            }

            return results.Values.OrderBy(r => r.folderPath).ToList();
        }

        /// <summary>
        /// Create atlases for the given scan results.
        /// </summary>
        public static List<AtlasReport> CreateAtlases(List<ScanResult> scanResults, Settings settings, string outputDirectory)
        {
            var reports = new List<AtlasReport>();

            // Ensure output directory exists
            if (!AssetDatabase.IsValidFolder(outputDirectory))
            {
                CreateFolderRecursive(outputDirectory);
            }

            for (int i = 0; i < scanResults.Count; i++)
            {
                var scan = scanResults[i];
                EditorUtility.DisplayProgressBar(
                    "Creating Sprite Atlases",
                    $"Processing {scan.folderName}... ({i + 1}/{scanResults.Count})",
                    (float)i / scanResults.Count);

                var report = CreateAtlasForFolder(scan, settings, outputDirectory);
                reports.Add(report);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return reports;
        }

        /// <summary>
        /// Run full automation: scan + create + report.
        /// </summary>
        public static (List<ScanResult> scan, List<AtlasReport> reports) RunAutomation(
            string[] sourceFolders, Settings settings, string outputDirectory)
        {
            var scanResults = ScanForLooseSprites(sourceFolders);

            if (scanResults.Count == 0)
                return (scanResults, new List<AtlasReport>());

            var reports = CreateAtlases(scanResults, settings, outputDirectory);
            return (scanResults, reports);
        }

        private static AtlasReport CreateAtlasForFolder(ScanResult scan, Settings settings, string outputDirectory)
        {
            var atlasName = $"Atlas_{SanitizeName(scan.folderName)}";
            var outputPath = $"{outputDirectory}/{atlasName}.spriteatlas";

            var report = new AtlasReport
            {
                atlasName = atlasName,
                folderPath = scan.folderPath,
                outputPath = outputPath,
                spriteCount = scan.spritePaths.Count
            };

            // Check if atlas already exists
            var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(outputPath);
            if (existing != null)
            {
                report.alreadyExisted = true;

                // Update the existing atlas: add the folder as a packable
                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(scan.folderPath);
                if (folder != null)
                {
                    existing.Remove(existing.GetPackables());
                    existing.Add(new UnityEngine.Object[] { folder });
                    ApplySettings(existing, settings);
                    EditorUtility.SetDirty(existing);
                }

                report.created = true;
                Debug.Log($"[Yes2SDK] Updated existing atlas: {atlasName} ({scan.spritePaths.Count} sprites from {scan.folderPath})");
                return report;
            }

            // Create new atlas
            var atlas = new SpriteAtlas();
            ApplySettings(atlas, settings);

            // Add the folder as a packable (Unity will include all sprites in it)
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(scan.folderPath);
            if (folderAsset != null)
            {
                atlas.Add(new UnityEngine.Object[] { folderAsset });
            }
            else
            {
                // Fallback: add individual sprites
                var sprites = scan.spritePaths
                    .Select(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p))
                    .Where(o => o != null)
                    .ToArray();
                atlas.Add(sprites);
            }

            AssetDatabase.CreateAsset(atlas, outputPath);
            report.created = true;

            Debug.Log($"[Yes2SDK] Created atlas: {atlasName} ({scan.spritePaths.Count} sprites from {scan.folderPath})");
            return report;
        }

        private static void ApplySettings(SpriteAtlas atlas, Settings settings)
        {
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                padding = settings.padding,
                enableTightPacking = settings.enableTightPacking,
                enableRotation = settings.enableRotation,
                blockOffset = 1
            });

            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                generateMipMaps = settings.generateMipMaps,
                sRGB = settings.sRGB,
                filterMode = FilterMode.Bilinear,
                readable = false
            });

            // WebGL platform override
            var webgl = atlas.GetPlatformSettings("WebGL");
            webgl.overridden = true;
            webgl.maxTextureSize = settings.maxTextureSize;
            webgl.format = TextureImporterFormat.ETC2_RGBA8Crunched;
            webgl.compressionQuality = settings.compressionQuality;
            atlas.SetPlatformSettings(webgl);
        }

        /// <summary>
        /// Get all paths that are covered by existing sprite atlases (folders and individual assets).
        /// </summary>
        private static HashSet<string> GetAllPackedPaths()
        {
            var paths = new HashSet<string>();
            var atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");

            foreach (var guid in atlasGuids)
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                var packables = atlas.GetPackables();
                foreach (var packable in packables)
                {
                    if (packable == null) continue;
                    var path = AssetDatabase.GetAssetPath(packable);
                    if (!string.IsNullOrEmpty(path))
                        paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Check if a sprite path is covered by an atlas (either directly or via a packed folder).
        /// </summary>
        private static bool IsPathCoveredByAtlas(string spritePath, HashSet<string> packedPaths)
        {
            // Direct match
            if (packedPaths.Contains(spritePath))
                return true;

            // Check if any packed folder is a parent of this sprite
            foreach (var packedPath in packedPaths)
            {
                if (spritePath.StartsWith(packedPath + "/"))
                    return true;
            }

            return false;
        }

        private static string SanitizeName(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            var current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
