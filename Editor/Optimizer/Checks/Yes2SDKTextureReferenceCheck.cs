using System.Collections.Generic;
using System.Linq;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports scene and prefab references still pointing at a source texture that has a KTX2
    /// counterpart. Report only: rewriting a scene is not something a bulk fix should do.
    /// </summary>
    public sealed class Yes2SDKTextureReferenceCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "texture-references";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Textures;

        public string Title => "Texture references";

        public string DocsAnchor => "texture-references";

        public bool CanFix => false;

        /// <summary>Report only, so nothing is written to reverse.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            if (!Yes2SDKTextureSwapTool.IsKtx2ImageAvailable)
            {
                return new List<Yes2SDKOptimizationFinding>
                {
                    new Yes2SDKOptimizationFinding
                    {
                        Severity = Yes2SDKFindingSeverity.Info,
                        Message = "The KTX2 image component is not present in this project, so references cannot be "
                                  + "checked. Install the KTX package and re-run Analyze.",
                        Fixable = false,
                        ActionLabel = "Install",
                        Action = () => Yes2SDKPackages.Install(Yes2SDKPackages.Ktx2),
                    }
                };
            }

            // Only the open scene. Sweeping every scene in Build Settings means force-opening each one,
            // which discards unsaved edits without asking, and a report must never do that.
            var candidates = Yes2SDKTextureSwapTool.ScanActiveScene();

            var findings = candidates
                .Where(c => c.ktx2Exists && !c.inAtlas)
                .Select(c => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = c.assetPath,
                    Message = string.Format(
                        "'{0}' on {1} still references '{2}' but a KTX2 version exists.",
                        c.componentType,
                        c.gameObjectPath,
                        c.originalTexturePath),
                    Fixable = false,
                })
                .ToList();

            findings.Add(new Yes2SDKOptimizationFinding
            {
                Severity = Yes2SDKFindingSeverity.Info,
                Message = "Only the open scene was scanned. Open another scene and run Analyze again to cover it.",
                Fixable = false,
            });

            return findings;
        }

        // Report only. A scene or prefab rewrite is opt-in per run with a per-object diff, so it is
        // never driven from the bulk fix button.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
        }
    }
}
