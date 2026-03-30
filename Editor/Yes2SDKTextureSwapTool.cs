using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.U2D;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Scans scenes and prefabs for texture references that have KTX2 versions
    /// in StreamingAssets, and swaps them to use Yes2SDKKtx2Image at runtime.
    /// Uses reflection to access Yes2SDKKtx2Image (lives in optional Yes2SDK.Ktx2 assembly).
    /// </summary>
    public static class Yes2SDKTextureSwapTool
    {
        public enum ScanScope
        {
            ActiveScene,
            AllBuildScenes,
            PrefabsInFolder
        }

        public class SwapCandidate
        {
            public string assetPath;
            public string gameObjectPath;
            public string componentType;
            public string originalTexturePath;
            public string ktx2Path;
            public bool ktx2Exists;
            public bool inAtlas;
        }

        public class SwapReport
        {
            public int swapped;
            public int skippedMissingKtx2;
            public int skippedInAtlas;
            public int skippedAlreadySwapped;
        }

        private const string KTX2_IMAGE_TYPE = "Yes2SDK.Yes2SDKKtx2Image, Yes2SDK.Ktx2";
        // Note: KTX package assembly is "Ktx" (not "KtxUnity"), namespace is "KtxUnity"

        private static Type _ktx2ImageType;
        private static PropertyInfo _ktx2PathProperty;

        /// <summary>
        /// Returns the Yes2SDKKtx2Image type if available (com.unity.cloud.ktx installed), null otherwise.
        /// </summary>
        public static Type GetKtx2ImageType()
        {
            if (_ktx2ImageType == null)
                _ktx2ImageType = Type.GetType(KTX2_IMAGE_TYPE);
            return _ktx2ImageType;
        }

        /// <summary>
        /// Whether the KTX2 runtime loader component is available.
        /// </summary>
        public static bool IsKtx2ImageAvailable => GetKtx2ImageType() != null;

        // --- Scanning ---

        /// <summary>
        /// Scan the currently active scene for swap candidates.
        /// </summary>
        public static List<SwapCandidate> ScanActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            return ScanScene(scene);
        }

        /// <summary>
        /// Scan all scenes in Build Settings for swap candidates.
        /// </summary>
        public static List<SwapCandidate> ScanAllBuildScenes()
        {
            var candidates = new List<SwapCandidate>();
            var buildScenes = EditorBuildSettings.scenes;

            // Save current scene
            var currentScene = SceneManager.GetActiveScene().path;

            for (int i = 0; i < buildScenes.Length; i++)
            {
                var scenePath = buildScenes[i].path;
                if (string.IsNullOrEmpty(scenePath)) continue;

                EditorUtility.DisplayProgressBar("Scanning Scenes",
                    $"Scanning {Path.GetFileName(scenePath)}... ({i + 1}/{buildScenes.Length})",
                    (float)i / buildScenes.Length);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                candidates.AddRange(ScanScene(scene));
            }

            // Reopen original scene
            if (!string.IsNullOrEmpty(currentScene))
                EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);

            EditorUtility.ClearProgressBar();
            return candidates;
        }

        /// <summary>
        /// Scan a single scene for swap candidates.
        /// </summary>
        public static List<SwapCandidate> ScanScene(Scene scene)
        {
            var candidates = new List<SwapCandidate>();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                ScanGameObjectRecursive(root, scene.path, candidates);
            }

            return candidates;
        }

        /// <summary>
        /// Scan prefabs in a folder for swap candidates.
        /// </summary>
        public static List<SwapCandidate> ScanPrefabs(string folder)
        {
            var candidates = new List<SwapCandidate>();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[Yes2SDK] Invalid folder: {folder}");
                return candidates;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

            for (int i = 0; i < guids.Length; i++)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                EditorUtility.DisplayProgressBar("Scanning Prefabs",
                    $"Scanning {Path.GetFileName(prefabPath)}... ({i + 1}/{guids.Length})",
                    (float)i / guids.Length);

                // Skip prefab variants — only modify base prefabs
                var prefabType = PrefabUtility.GetPrefabAssetType(
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                if (prefabType == PrefabAssetType.Variant)
                    continue;

                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null) continue;

                ScanGameObjectRecursive(prefabRoot, prefabPath, candidates);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            EditorUtility.ClearProgressBar();
            return candidates;
        }

        private static void ScanGameObjectRecursive(GameObject go, string assetPath,
            List<SwapCandidate> candidates)
        {
            // Skip if already has KTX2 loader (via reflection)
            var ktx2Type = GetKtx2ImageType();
            if (ktx2Type != null && go.GetComponent(ktx2Type) != null)
                return;

            // Check for texture-holding components
            // Prioritize RawImage over Image if both exist (unusual but possible)
            var rawImage = go.GetComponent<RawImage>();
            var image = go.GetComponent<Image>();
            var spriteRenderer = go.GetComponent<SpriteRenderer>();

            if (rawImage != null && rawImage.texture != null)
            {
                var candidate = CreateCandidate(rawImage.texture, null, "RawImage", go, assetPath);
                if (candidate != null)
                    candidates.Add(candidate);
            }
            else if (image != null && image.sprite != null)
            {
                var texture = image.sprite.texture;
                var candidate = CreateCandidate(texture, image.sprite, "Image", go, assetPath);
                if (candidate != null)
                    candidates.Add(candidate);
            }
            else if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                var texture = spriteRenderer.sprite.texture;
                var candidate = CreateCandidate(texture, spriteRenderer.sprite, "SpriteRenderer", go, assetPath);
                if (candidate != null)
                    candidates.Add(candidate);
            }

            // Recurse into children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                ScanGameObjectRecursive(go.transform.GetChild(i).gameObject, assetPath, candidates);
            }
        }

        private static SwapCandidate CreateCandidate(Texture texture, Sprite sprite,
            string componentType, GameObject go, string assetPath)
        {
            var texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath))
                return null;

            // Skip built-in Unity textures
            if (texturePath.StartsWith("Resources/") || texturePath.StartsWith("Library/"))
                return null;

            // Check if sprite is part of a SpriteAtlas
            bool inAtlas = false;
            if (sprite != null)
            {
                inAtlas = IsSpriteInAtlas(texturePath);
            }

            // Compute expected KTX2 path: strip "Assets/" prefix, change extension, prepend "ktx2/"
            var relativePath = texturePath;
            if (relativePath.StartsWith("Assets/"))
                relativePath = relativePath.Substring("Assets/".Length);

            var ktx2RelPath = "ktx2/" + Path.ChangeExtension(relativePath, ".ktx2");

            // Check if KTX2 file exists
            var ktx2FullPath = Path.Combine(Application.streamingAssetsPath, ktx2RelPath);
            bool ktx2Exists = File.Exists(ktx2FullPath);

            return new SwapCandidate
            {
                assetPath = assetPath,
                gameObjectPath = GetHierarchyPath(go),
                componentType = componentType,
                originalTexturePath = texturePath,
                ktx2Path = ktx2RelPath,
                ktx2Exists = ktx2Exists,
                inAtlas = inAtlas
            };
        }

        private static bool IsSpriteInAtlas(string texturePath)
        {
            // Find all SpriteAtlas assets and check if any pack this texture
            var atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");
            foreach (var guid in atlasGuids)
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                var packables = atlas.GetPackables();
                foreach (var packable in packables)
                {
                    var packablePath = AssetDatabase.GetAssetPath(packable);

                    // Direct match
                    if (packablePath == texturePath)
                        return true;

                    // Folder match — if the packable is a folder containing the texture
                    if (AssetDatabase.IsValidFolder(packablePath) &&
                        texturePath.StartsWith(packablePath + "/"))
                        return true;
                }
            }

            return false;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        // --- Swapping ---

        /// <summary>
        /// Perform the swap for candidates that have KTX2 files available.
        /// Groups by asset path (scene or prefab) and processes each.
        /// </summary>
        public static SwapReport PerformSwap(List<SwapCandidate> candidates)
        {
            var report = new SwapReport();

            var ktx2Type = GetKtx2ImageType();
            if (ktx2Type == null)
            {
                Debug.LogError("[Yes2SDK] Cannot swap: Yes2SDKKtx2Image type not found. Is com.unity.cloud.ktx installed?");
                return report;
            }

            // Group by asset path
            var grouped = candidates.GroupBy(c => c.assetPath);

            int totalGroups = grouped.Count();
            int groupIndex = 0;

            foreach (var group in grouped)
            {
                var assetPath = group.Key;
                groupIndex++;

                EditorUtility.DisplayProgressBar("Swapping Textures",
                    $"Processing {Path.GetFileName(assetPath)}... ({groupIndex}/{totalGroups})",
                    (float)groupIndex / totalGroups);

                bool isPrefab = assetPath.EndsWith(".prefab");

                if (isPrefab)
                {
                    SwapInPrefab(assetPath, group.ToList(), report, ktx2Type);
                }
                else
                {
                    SwapInScene(assetPath, group.ToList(), report, ktx2Type);
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();

            return report;
        }

        private static void SwapInScene(string scenePath, List<SwapCandidate> candidates,
            SwapReport report, Type ktx2Type)
        {
            // Ensure the scene is open
            var scene = SceneManager.GetActiveScene();
            if (scene.path != scenePath)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var rootObjects = scene.GetRootGameObjects();

            foreach (var candidate in candidates)
            {
                if (candidate.inAtlas)
                {
                    report.skippedInAtlas++;
                    continue;
                }
                if (!candidate.ktx2Exists)
                {
                    report.skippedMissingKtx2++;
                    continue;
                }

                var go = FindGameObjectByPath(rootObjects, candidate.gameObjectPath);
                if (go == null)
                {
                    Debug.LogWarning($"[Yes2SDK] Could not find {candidate.gameObjectPath} in {scenePath}");
                    continue;
                }

                if (go.GetComponent(ktx2Type) != null)
                {
                    report.skippedAlreadySwapped++;
                    continue;
                }

                SwapComponent(go, candidate, ktx2Type);
                report.swapped++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SwapInPrefab(string prefabPath, List<SwapCandidate> candidates,
            SwapReport report, Type ktx2Type)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return;

            bool modified = false;

            foreach (var candidate in candidates)
            {
                if (candidate.inAtlas)
                {
                    report.skippedInAtlas++;
                    continue;
                }
                if (!candidate.ktx2Exists)
                {
                    report.skippedMissingKtx2++;
                    continue;
                }

                var go = FindGameObjectByPath(new[] { prefabRoot }, candidate.gameObjectPath);
                if (go == null)
                {
                    Debug.LogWarning($"[Yes2SDK] Could not find {candidate.gameObjectPath} in {prefabPath}");
                    continue;
                }

                if (go.GetComponent(ktx2Type) != null)
                {
                    report.skippedAlreadySwapped++;
                    continue;
                }

                SwapComponent(go, candidate, ktx2Type);
                report.swapped++;
                modified = true;
            }

            if (modified)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void SwapComponent(GameObject go, SwapCandidate candidate, Type ktx2Type)
        {
            // Add KTX2 loader via reflection
            var loader = go.AddComponent(ktx2Type);

            // Set Ktx2Path property
            if (_ktx2PathProperty == null)
                _ktx2PathProperty = ktx2Type.GetProperty("Ktx2Path");
            _ktx2PathProperty?.SetValue(loader, candidate.ktx2Path);

            // Clear original texture reference
            switch (candidate.componentType)
            {
                case "RawImage":
                    var rawImage = go.GetComponent<RawImage>();
                    if (rawImage != null)
                        rawImage.texture = null;
                    break;

                case "Image":
                    var image = go.GetComponent<Image>();
                    if (image != null)
                        image.sprite = null;
                    break;

                case "SpriteRenderer":
                    var spriteRenderer = go.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                        spriteRenderer.sprite = null;
                    break;
            }

            Debug.Log($"[Yes2SDK] Swapped: {candidate.gameObjectPath} ({candidate.componentType}) -> {candidate.ktx2Path}");
        }

        private static GameObject FindGameObjectByPath(GameObject[] roots, string hierarchyPath)
        {
            foreach (var root in roots)
            {
                var result = FindByPathRecursive(root, hierarchyPath);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static GameObject FindByPathRecursive(GameObject go, string targetPath)
        {
            if (GetHierarchyPath(go) == targetPath)
                return go;

            for (int i = 0; i < go.transform.childCount; i++)
            {
                var result = FindByPathRecursive(go.transform.GetChild(i).gameObject, targetPath);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
