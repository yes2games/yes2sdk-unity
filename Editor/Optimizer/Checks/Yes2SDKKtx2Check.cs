using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>Reports textures that would ship smaller as KTX2, and whether the converter is available.</summary>
    public sealed class Yes2SDKKtx2Check : IYes2SDKOptimizationCheck
    {
        private const string SourceRoot = "Assets";

        public string Id => "texture-compression-ktx2";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Textures;

        public string Title => "Texture compression (KTX2)";

        public string DocsAnchor => "texture-compression-ktx2";

        public bool CanFix => true;

        /// <summary>An external process writes files that Undo never saw.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            var toktx = Yes2SDKKtx2Tool.FindToktx();
            if (string.IsNullOrEmpty(toktx))
            {
                return new List<Yes2SDKOptimizationFinding>
                {
                    new Yes2SDKOptimizationFinding
                    {
                        Severity = Yes2SDKFindingSeverity.Info,
                        Message = "The toktx command line tool was not found, so textures cannot be converted. "
                                  + "Install it, put it on PATH, then re-run Analyze.",
                        Fixable = false,
                        ActionLabel = "Get toktx",
                        Action = () => Application.OpenURL(Yes2SDKKtx2Tool.KtxSoftwareReleasesUrl),
                    }
                };
            }

            var textures = Yes2SDKKtx2Tool.FindTexturesInFolder(SourceRoot);

            return textures
                .Where(path => !Ktx2OutputExists(path))
                .Select(path => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = path,
                    Message = "Not converted to KTX2.",
                    Fixable = true,
                })
                .ToList();
        }

        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            var toktx = Yes2SDKKtx2Tool.FindToktx();
            if (string.IsNullOrEmpty(toktx))
            {
                return;
            }

            var paths = findings
                .Where(f => f.Fixable && !string.IsNullOrEmpty(f.AssetPath))
                .Select(f => f.AssetPath)
                .ToArray();

            if (paths.Length == 0)
            {
                return;
            }

            // The conversion runs an external process that writes into StreamingAssets rather than
            // creating assets, so there is nothing on the Undo stack for Ctrl+Z to reverse. The
            // confirmation dialog lists every texture before the run, and the output files can be
            // deleted from the ktx2 folder under StreamingAssets.
            Yes2SDKKtx2Tool.BatchConvert(paths, new Yes2SDKKtx2Tool.Settings(), toktx);
        }

        // The conversion writes its output under StreamingAssets, mirroring the texture's path below
        // Assets with a .ktx2 extension. A texture that already has one is converted, so it is not a
        // finding. This derivation has to stay in step with what the conversion writes.
        private static bool Ktx2OutputExists(string assetPath)
        {
            var relative = assetPath.StartsWith("Assets/")
                ? assetPath.Substring("Assets/".Length)
                : assetPath;

            var output = Path.Combine(
                Application.streamingAssetsPath,
                "ktx2",
                Path.ChangeExtension(relative, ".ktx2"));

            return File.Exists(output);
        }
    }
}
