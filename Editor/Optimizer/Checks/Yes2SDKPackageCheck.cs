using System.Collections.Generic;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Reports the optional Unity packages that cut WebGL download size or make it measurable, and
    /// offers to install the ones this project does not have. Report only: installing is a per-row
    /// action rather than a fix, because adding a package to someone's manifest is never a bulk edit.
    /// </summary>
    public sealed class Yes2SDKPackageCheck : IYes2SDKOptimizationCheck
    {
        private sealed class Recommendation
        {
            public string PackageId;
            public string Reason;
        }

        private static readonly Recommendation[] Recommended =
        {
            new Recommendation
            {
                PackageId = Yes2SDKPackages.Addressables,
                Reason = "Addressables is not installed. It moves assets out of the initial download and "
                         + "loads them on demand, which is the largest single lever on load time.",
            },
            new Recommendation
            {
                PackageId = Yes2SDKPackages.Ktx2,
                Reason = "The KTX package is not installed. It loads KTX2 and Basis textures at runtime, "
                         + "which is what lets a converted texture be referenced by a scene.",
            },
            new Recommendation
            {
                PackageId = Yes2SDKPackages.MemoryProfiler,
                Reason = "The Memory Profiler is not installed. It captures what actually occupies the heap, "
                         + "so cuts are aimed at measured cost rather than guesses.",
            },
        };

        public string Id => "packages-and-assemblies";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Packages;

        public string Title => "Packages";

        public string DocsAnchor => "packages-and-assemblies";

        public bool CanFix => false;

        /// <summary>Report only. Installing runs through the Package Manager, which owns its own undo.</summary>
        public bool FixIsUndoable => false;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            var findings = new List<Yes2SDKOptimizationFinding>();

            foreach (var recommendation in Recommended)
            {
                if (Yes2SDKPackages.IsInstalled(recommendation.PackageId))
                {
                    continue;
                }

                var packageId = recommendation.PackageId;
                findings.Add(new Yes2SDKOptimizationFinding
                {
                    Severity = Yes2SDKFindingSeverity.Info,
                    Message = recommendation.Reason,
                    Fixable = false,
                    ActionLabel = "Install",
                    Action = () => Yes2SDKPackages.Install(packageId),
                });
            }

            return findings;
        }

        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
        }
    }
}
