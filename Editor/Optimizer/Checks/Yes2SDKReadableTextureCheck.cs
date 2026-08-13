using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports textures imported with Read/Write enabled. The flag keeps a second copy of the texture in
    /// system memory for the lifetime of the game, on top of the copy the GPU already holds.
    /// </summary>
    public sealed class Yes2SDKReadableTextureCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "readable-textures";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Textures;

        public string Title => "Read/Write enabled textures";

        public string DocsAnchor => "textures";

        public bool CanFix => true;

        /// <summary>Import settings are not on the Undo stack.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            return Yes2SDKImporterScan.TextureImporters()
                .Where(pair => pair.Value.isReadable)
                .Select(pair => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = pair.Key,
                    Message = "Read/Write is enabled, which keeps a second copy of this texture in system memory. "
                              + "Only scripts that call GetPixels, SetPixels, or read the texture on the CPU need it.",
                    Fixable = true,
                })
                .ToList();
        }

        // Import settings live in the asset's .meta file rather than in the scene, so Ctrl+Z will not
        // reverse this. To restore one texture by hand, select it and tick Read/Write in the Inspector.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            Yes2SDKImporterScan.Apply(findings, path =>
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !importer.isReadable) return false;

                importer.isReadable = false;
                return true;
            });
        }
    }
}
