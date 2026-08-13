using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports sprite textures imported with mipmaps. A sprite drawn at a fixed size on a canvas never
    /// samples the smaller levels, so the extra third of memory a mip chain costs buys nothing.
    /// </summary>
    public sealed class Yes2SDKTextureMipmapCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "texture-mipmaps";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Textures;

        public string Title => "Mipmaps on sprites";

        public string DocsAnchor => "textures";

        public bool CanFix => true;

        /// <summary>Import settings are not on the Undo stack.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            return Yes2SDKImporterScan.TextureImporters()
                .Where(pair => IsMippedSprite(pair.Value))
                .Select(pair => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = pair.Key,
                    Message = "Imported as a sprite with mipmaps, which costs about a third more memory for "
                              + "levels a sprite drawn at a fixed size never samples.",
                    Fixable = true,
                })
                .ToList();
        }

        // Import settings live in the asset's .meta file rather than in the scene, so Ctrl+Z will not
        // reverse this. To restore one texture by hand, select it and tick Generate Mip Maps in the
        // Inspector. A sprite that is scaled far down in world space is the case that wants them back.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            Yes2SDKImporterScan.Apply(findings, path =>
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !IsMippedSprite(importer)) return false;

                importer.mipmapEnabled = false;
                return true;
            });
        }

        private static bool IsMippedSprite(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled;
        }
    }
}
