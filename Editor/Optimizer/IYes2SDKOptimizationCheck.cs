using System.Collections.Generic;

namespace Yes2SDK.Editor
{
    /// <summary>Severity of a single optimization finding.</summary>
    public enum Yes2SDKFindingSeverity
    {
        /// <summary>Context rather than a defect, including a precondition the check needs.</summary>
        Info,
        /// <summary>A missed optimization. The project works but ships more than it needs to.</summary>
        Warning,
        /// <summary>A setting that will cost the project at upload or at runtime.</summary>
        Critical,
    }

    /// <summary>Grouping used by the Optimizer window's category filter.</summary>
    public enum Yes2SDKOptimizationCategory
    {
        /// <summary>Texture import settings, compression, and atlasing.</summary>
        Textures,
        /// <summary>Audio clip import settings, compression, and load type.</summary>
        Audio,
        /// <summary>Mesh and model import settings.</summary>
        Meshes,
        /// <summary>Shader variants, keywords, and stripping.</summary>
        Shaders,
        /// <summary>Managed code size: stripping level, exception support, generated code.</summary>
        Code,
        /// <summary>Installed packages and assembly definitions that pull weight into the build.</summary>
        Packages,
        /// <summary>Addressable group layout, compression, and what stays in the initial download.</summary>
        Addressables,
        /// <summary>Player and build settings that decide how the build is produced.</summary>
        Build,
        /// <summary>Behaviour after the build ships: heap use, allocation, frame cost.</summary>
        Runtime,
    }

    /// <summary>One actionable result produced by a check.</summary>
    public sealed class Yes2SDKOptimizationFinding
    {
        /// <summary>How much this matters.</summary>
        public Yes2SDKFindingSeverity Severity { get; set; }

        /// <summary>Asset path this finding is about, or null when the finding is project-wide.</summary>
        public string AssetPath { get; set; }

        /// <summary>One sentence stating what is wrong.</summary>
        public string Message { get; set; }

        /// <summary>Estimated bytes saved by fixing this, or null when not estimable.</summary>
        public long? EstimatedSaving { get; set; }

        /// <summary>True when this finding is one the owning check can fix.</summary>
        public bool Fixable { get; set; }
    }

    /// <summary>
    /// One optimization rule. Implement this and the Optimizer window picks it up automatically:
    /// discovery is by reflection, so there is no second place to register.
    /// </summary>
    public interface IYes2SDKOptimizationCheck
    {
        /// <summary>Stable identifier. Used for the mute preference key and for the docs anchor.</summary>
        string Id { get; }

        /// <summary>Category filter this check appears under.</summary>
        Yes2SDKOptimizationCategory Category { get; }

        /// <summary>Short human-readable name shown as the row header.</summary>
        string Title { get; }

        /// <summary>Anchor fragment on the optimization docs page, without the leading hash.</summary>
        string DocsAnchor { get; }

        /// <summary>Scan the project. Returns an empty list when nothing is wrong. Never throws.</summary>
        IReadOnlyList<Yes2SDKOptimizationFinding> Analyze();

        /// <summary>
        /// Apply the fix for the given findings. Only called when <see cref="CanFix"/> is true, so a
        /// report-only check leaves the body empty. Implementations that create assets register them with
        /// Undo; implementations that cannot must return false from <see cref="FixIsUndoable"/>.
        /// </summary>
        void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings);

        /// <summary>False when this check reports only and Fix must not be called.</summary>
        bool CanFix { get; }

        /// <summary>
        /// True when one Undo reverses everything Fix did. False when the fix writes something the Undo
        /// stack cannot hold, in which case the check's own source states how to reverse it by hand.
        /// </summary>
        bool FixIsUndoable { get; }
    }
}
