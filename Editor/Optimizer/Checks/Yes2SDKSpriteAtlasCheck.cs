using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>Reports folders of loose sprites that would be cheaper packed into an atlas.</summary>
    public sealed class Yes2SDKSpriteAtlasCheck : IYes2SDKOptimizationCheck
    {
        private const string SourceRoot = "Assets";
        private const string AtlasOutputDirectory = "Assets/Yes2SDK/Atlases";

        private List<Yes2SDKSpriteAtlasTool.ScanResult> _lastScan;

        public string Id => "sprite-atlases";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Textures;

        public string Title => "Sprite atlases";

        public string DocsAnchor => "sprite-atlases";

        public bool CanFix => true;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            _lastScan = Yes2SDKSpriteAtlasTool.ScanForLooseSprites(new[] { SourceRoot });

            return _lastScan.Select(r => new Yes2SDKOptimizationFinding
            {
                Severity = Yes2SDKFindingSeverity.Warning,
                AssetPath = r.folderPath,
                Message = string.Format("{0} loose sprites in '{1}' are not in an atlas.", r.spritePaths.Count, r.folderName),
                Fixable = true,
            }).ToList();
        }

        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            if (_lastScan == null)
            {
                return;
            }

            var wanted = new HashSet<string>(findings.Select(f => f.AssetPath));
            var selected = _lastScan.Where(r => wanted.Contains(r.folderPath)).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var reports = Yes2SDKSpriteAtlasTool.CreateAtlases(
                selected,
                new Yes2SDKSpriteAtlasTool.Settings(),
                AtlasOutputDirectory);

            RegisterCreatedForUndo(reports.Where(r => r.created).Select(r => r.outputPath), "Create Sprite Atlases");
        }

        // AssetDatabase.CreateAsset does not put the new asset on the Undo stack, so register each
        // created asset explicitly. Without this, one Ctrl+Z would not reverse the run.
        private static void RegisterCreatedForUndo(IEnumerable<string> paths, string label)
        {
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null)
                {
                    Undo.RegisterCreatedObjectUndo(asset, label);
                }
            }
        }
    }
}
