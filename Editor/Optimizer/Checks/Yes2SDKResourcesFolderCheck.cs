using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports assets under a Resources folder. Everything in one is packed into the initial download
    /// whether or not anything references it, so an unused asset there still costs the player a wait.
    /// </summary>
    public sealed class Yes2SDKResourcesFolderCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "resources-folders";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Build;

        public string Title => "Resources folders";

        public string DocsAnchor => "resources-folders";

        public bool CanFix => false;

        /// <summary>Report only, so nothing is written to reverse.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(IsShippedResource)
                .Select(path => new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Warning,
                    AssetPath = path,
                    Message = "Under a Resources folder, so it ships in the initial download whether or not "
                              + "anything references it.",
                    Fixable = false,
                })
                .ToList();
        }

        // Moving an asset out of Resources changes how the game loads it, from a path lookup to a direct
        // reference or an Addressable, and only the game's own code knows which one it wants. A bulk fix
        // that guessed would break loading silently, so this reports and stops there.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
        }

        // A Resources folder below an Editor folder is editor-only and never reaches a build, so it is
        // not a finding. Folders themselves are skipped: the cost is the assets inside them.
        private static bool IsShippedResource(string path)
        {
            if (!path.StartsWith("Assets/")) return false;
            if (!path.Contains("/Resources/")) return false;
            if (path.Contains("/Editor/")) return false;
            return !AssetDatabase.IsValidFolder(path);
        }
    }
}
