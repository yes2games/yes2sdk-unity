using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Persistent state for the Build Window's "Build Mode" radio. The user
    /// picks Production / Production Safe / Diagnostic; the choice persists
    /// across Editor sessions via EditorPrefs.
    /// </summary>
    public static class Yes2SDKBuildMode
    {
        public enum Mode
        {
            Production,
            ProductionSafe,
            Diagnostic,
        }

        private const string EditorPrefsKey = "Yes2SDK.BuildMode";

        public static Mode Current
        {
            get => (Mode)EditorPrefs.GetInt(EditorPrefsKey, (int)Mode.Production);
            set => EditorPrefs.SetInt(EditorPrefsKey, (int)value);
        }

        public static string DisplayName(Mode mode) => mode switch
        {
            Mode.Production => "Production",
            Mode.ProductionSafe => "Production Safe (Explicitly Thrown)",
            Mode.Diagnostic => "Diagnostic (Full With Stacktrace)",
            _ => mode.ToString(),
        };
    }

    /// <summary>
    /// Build-time override that lets the user temporarily swap WebGL Exception
    /// Support for a single build via the Build Window's Build Mode dropdown.
    /// Player Settings is restored after the build, so the user's persistent
    /// configuration isn't silently mutated.
    ///
    /// callbackOrder=100 places this AFTER Yes2SDKBuildGuard (order 0) so the
    /// guard's template check runs first, and after any other order-0 SDK
    /// callbacks. Restoration in OnPostprocessBuild runs at the same order.
    ///
    /// Crash safety:
    ///   The "saved" Player Settings value is persisted to EditorPrefs (not
    ///   a static field) so that an Editor crash, force-quit, domain reload,
    ///   or unhandled exception in another preprocess callback can't strand
    ///   the user with a permanently-overridden ProjectSettings.asset value.
    ///   An [InitializeOnLoadMethod] checks for stale state on Editor startup
    ///   and restores from the persisted value if it finds one.
    ///
    /// In Production mode this is a no-op.
    /// </summary>
    public class Yes2SDKBuildModeOverride : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        // The saved value persists across Editor sessions, so an interrupted
        // build (crash, force-quit, domain reload mid-build, IPreprocess
        // throw at higher callbackOrder) can still be recovered next launch.
        private const string SavedExceptionSupportKey = "Yes2SDK.SavedExceptionSupport";

        /// <summary>
        /// Detects state left behind by a build that didn't complete its
        /// postprocess callback (e.g., Editor crashed during a Diagnostic
        /// build). Runs once per domain reload at Editor startup.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RecoverInterruptedOverride()
        {
            if (!EditorPrefs.HasKey(SavedExceptionSupportKey)) return;

            var saved = (WebGLExceptionSupport)EditorPrefs.GetInt(SavedExceptionSupportKey);
            var current = PlayerSettings.WebGL.exceptionSupport;

            // If saved == current, the postprocess ran and we just have a
            // stale leftover key — clean it up silently.
            if (saved != current)
            {
                Debug.LogWarning(
                    "[Yes2SDK] Detected an interrupted build mode override " +
                    $"from a previous session. Restoring Exception Support " +
                    $"{current} -> {saved}. (If you intended {current}, edit " +
                    "WebGL Settings in the Build Window now.)");
                PlayerSettings.WebGL.exceptionSupport = saved;
            }
            EditorPrefs.DeleteKey(SavedExceptionSupportKey);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            var mode = Yes2SDKBuildMode.Current;
            if (mode == Yes2SDKBuildMode.Mode.Production) return;

            WebGLExceptionSupport target = mode switch
            {
                Yes2SDKBuildMode.Mode.ProductionSafe => WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                Yes2SDKBuildMode.Mode.Diagnostic => WebGLExceptionSupport.FullWithStacktrace,
                _ => PlayerSettings.WebGL.exceptionSupport,
            };

            // If a previous build already saved a value (because postprocess
            // didn't run yet or this is a re-entrant build), don't overwrite —
            // the existing key holds the user's true original.
            if (!EditorPrefs.HasKey(SavedExceptionSupportKey))
            {
                EditorPrefs.SetInt(SavedExceptionSupportKey, (int)PlayerSettings.WebGL.exceptionSupport);
            }
            PlayerSettings.WebGL.exceptionSupport = target;

            var saved = (WebGLExceptionSupport)EditorPrefs.GetInt(SavedExceptionSupportKey);
            Debug.Log(
                $"[Yes2SDK] Build mode '{Yes2SDKBuildMode.DisplayName(mode)}' — " +
                $"Exception Support overridden to {target} for this build only " +
                $"(will restore to {saved} after).");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            if (!EditorPrefs.HasKey(SavedExceptionSupportKey)) return;

            var saved = (WebGLExceptionSupport)EditorPrefs.GetInt(SavedExceptionSupportKey);
            PlayerSettings.WebGL.exceptionSupport = saved;
            EditorPrefs.DeleteKey(SavedExceptionSupportKey);

            Debug.Log($"[Yes2SDK] Exception Support restored to {saved} after build.");
        }
    }
}
