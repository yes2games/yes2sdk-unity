using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Shared plumbing for the checks that read and write asset import settings: finding the importers
    /// under Assets, and reimporting only the assets a fix actually changed.
    /// </summary>
    internal static class Yes2SDKImporterScan
    {
        private static readonly string[] SearchRoots = { "Assets" };

        internal static IEnumerable<KeyValuePair<string, TextureImporter>> TextureImporters()
        {
            return Importers<TextureImporter>("t:Texture2D");
        }

        internal static IEnumerable<KeyValuePair<string, ModelImporter>> ModelImporters()
        {
            return Importers<ModelImporter>("t:Model");
        }

        internal static IEnumerable<KeyValuePair<string, AudioImporter>> AudioImporters()
        {
            return Importers<AudioImporter>("t:AudioClip");
        }

        /// <summary>
        /// Runs <paramref name="mutate"/> on every distinct asset path the given findings name, and
        /// reimports the ones it reports as changed. The callback re-reads the importer rather than
        /// trusting the finding, so a setting already corrected by hand since the scan is left alone.
        /// </summary>
        internal static void Apply(
            IReadOnlyList<Yes2SDKOptimizationFinding> findings,
            Func<string, bool> mutate)
        {
            var paths = findings
                .Where(f => f.Fixable && !string.IsNullOrEmpty(f.AssetPath))
                .Select(f => f.AssetPath)
                .Distinct()
                .ToArray();

            if (paths.Length == 0) return;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (mutate(path))
                    {
                        AssetImporter.GetAtPath(path).SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }

        // Textures with no importer of the expected kind exist: a render texture answers the t:Texture2D
        // filter but is generated rather than imported. Those are skipped rather than reported.
        private static IEnumerable<KeyValuePair<string, T>> Importers<T>(string filter) where T : AssetImporter
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, SearchRoots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as T;
                if (importer != null)
                {
                    yield return new KeyValuePair<string, T>(path, importer);
                }
            }
        }
    }
}
