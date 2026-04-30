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
    /// Support for a single build via the Build Window's Build Mode radio.
    /// Player Settings is restored after the build, so the user's persistent
    /// configuration isn't silently mutated.
    ///
    /// callbackOrder=100 places this AFTER Yes2SDKBuildGuard (order 0) so the
    /// guard's template check runs first, and after any other order-0 SDK
    /// callbacks. Restoration in OnPostprocessBuild runs at the same order.
    ///
    /// In Production mode this is a no-op.
    /// </summary>
    public class Yes2SDKBuildModeOverride : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        private static WebGLExceptionSupport? s_savedExceptionSupport;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            var mode = Yes2SDKBuildMode.Current;
            if (mode == Yes2SDKBuildMode.Mode.Production)
            {
                s_savedExceptionSupport = null;
                return;
            }

            WebGLExceptionSupport target = mode switch
            {
                Yes2SDKBuildMode.Mode.ProductionSafe => WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                Yes2SDKBuildMode.Mode.Diagnostic => WebGLExceptionSupport.FullWithStacktrace,
                _ => PlayerSettings.WebGL.exceptionSupport,
            };

            s_savedExceptionSupport = PlayerSettings.WebGL.exceptionSupport;
            PlayerSettings.WebGL.exceptionSupport = target;

            Debug.Log(
                $"[Yes2SDK] Build mode '{Yes2SDKBuildMode.DisplayName(mode)}' — " +
                $"Exception Support overridden to {target} for this build only " +
                $"(will restore to {s_savedExceptionSupport} after).");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            if (!s_savedExceptionSupport.HasValue) return;

            PlayerSettings.WebGL.exceptionSupport = s_savedExceptionSupport.Value;
            s_savedExceptionSupport = null;
        }
    }
}
