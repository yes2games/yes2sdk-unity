using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports objects carrying a component whose script no longer resolves. Each one is a serialized
    /// entry the build still writes out, and a hole where behaviour the scene expects will never run.
    /// </summary>
    public sealed class Yes2SDKMissingScriptCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "missing-script-references";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Code;

        public string Title => "Missing script references";

        public string DocsAnchor => "missing-script-references";

        public bool CanFix => false;

        /// <summary>Report only, so nothing is written to reverse.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            var findings = new List<Yes2SDKOptimizationFinding>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    Collect(prefab, path, findings);
                }
            }

            // Only the open scene. Sweeping every scene in Build Settings means force-opening each one,
            // which discards unsaved edits without asking, and a report must never do that.
            var scene = SceneManager.GetActiveScene();
            var scenePath = string.IsNullOrEmpty(scene.path) ? "the open scene" : scene.path;
            foreach (var root in scene.GetRootGameObjects())
            {
                Collect(root, scenePath, findings);
            }

            findings.Add(new Yes2SDKOptimizationFinding
            {
                Severity = Yes2SDKFindingSeverity.Info,
                Message = "Every prefab was scanned, but only the open scene. Open another scene and run "
                          + "Analyze again to cover it.",
                Fixable = false,
            });

            return findings;
        }

        // Report only. Removing a missing component rewrites a prefab or a scene, and the fix a project
        // wants is usually to restore the script rather than to drop what it was configured with.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
        }

        private static void Collect(GameObject root, string assetPath, List<Yes2SDKOptimizationFinding> findings)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (count == 0) continue;

                findings.Add(new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = assetPath,
                    Message = string.Format(
                        "'{0}' carries {1} component(s) whose script is missing.",
                        HierarchyPath(transform),
                        count),
                    Fixable = false,
                });
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
