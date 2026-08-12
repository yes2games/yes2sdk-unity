using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

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

        /// <summary>The fix only ever creates atlases, and each is registered.</summary>
        public bool FixIsUndoable => true;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            _lastScan = Yes2SDKSpriteAtlasTool.ScanForLooseSprites(new[] { SourceRoot });

            // The output name is built from the LEAF folder name in one flat directory, while the scan
            // groups by full path, so two folders with the same name resolve to the same atlas. Packing
            // the second would strip the first from the atlas, so neither is offered.
            var contested = _lastScan
                .GroupBy(OutputPathFor)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .Select(r => r.folderPath)
                .ToList();

            return _lastScan.Select(r =>
            {
                var outputPath = OutputPathFor(r);
                var collides = contested.Contains(r.folderPath);
                var occupied = !collides && AssetDatabase.LoadAssetAtPath<SpriteAtlas>(outputPath) != null;

                var message = string.Format(
                    "{0} loose sprites in '{1}' are not in an atlas.",
                    r.spritePaths.Count,
                    r.folderName);

                if (collides)
                {
                    message += string.Format(
                        " Another folder of the same name would produce '{0}' too, so pack these by hand.",
                        outputPath);
                }
                else if (occupied)
                {
                    message += string.Format(
                        " '{0}' already exists, so add this folder to it by hand rather than replacing it.",
                        outputPath);
                }

                return new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = r.folderPath,
                    Message = message,
                    Fixable = !collides && !occupied,
                };
            }).ToList();
        }

        // Mirrors how the atlas tool names its output: the leaf folder name with every character that is
        // not a letter, a digit, or an underscore replaced. This has to stay in step with that naming.
        private static string OutputPathFor(Yes2SDKSpriteAtlasTool.ScanResult scan)
        {
            var chars = scan.folderName.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                {
                    chars[i] = '_';
                }
            }

            return AtlasOutputDirectory + "/Atlas_" + new string(chars) + ".spriteatlas";
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

            // Only atlases this run brought into existence. An atlas at the same output path that
            // already existed was updated, not created, and registering it as created would let one
            // Ctrl+Z delete an asset that predates the run.
            RegisterCreatedForUndo(
                reports.Where(r => r.created && !r.alreadyExisted).Select(r => r.outputPath),
                "Create Sprite Atlases");
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
