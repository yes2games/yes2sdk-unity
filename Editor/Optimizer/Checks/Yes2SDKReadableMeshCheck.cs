using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports models imported with Read/Write enabled. The flag keeps the mesh's vertex data in system
    /// memory after it has been uploaded to the GPU, which nothing but CPU-side mesh code needs.
    /// </summary>
    public sealed class Yes2SDKReadableMeshCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "readable-meshes";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Meshes;

        public string Title => "Read/Write enabled meshes";

        public string DocsAnchor => "meshes-and-models";

        public bool CanFix => true;

        /// <summary>Import settings are not on the Undo stack.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            return Yes2SDKImporterScan.ModelImporters()
                .Where(pair => pair.Value.isReadable)
                .Select(pair => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = pair.Key,
                    Message = "Read/Write is enabled, which keeps this mesh's vertex data in system memory. "
                              + "Only mesh colliders built at runtime, CPU vertex reads, and mesh combining need it.",
                    Fixable = true,
                })
                .ToList();
        }

        // Import settings live in the asset's .meta file rather than in the scene, so Ctrl+Z will not
        // reverse this. To restore one model by hand, select it and tick Read/Write in the Inspector.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            Yes2SDKImporterScan.Apply(findings, path =>
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || !importer.isReadable) return false;

                importer.isReadable = false;
                return true;
            });
        }
    }
}
