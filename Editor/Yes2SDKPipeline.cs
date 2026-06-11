using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Persistent on/off switch for Yes2SDK's WebGL build management.
    ///
    /// The build callbacks (Yes2SDKBuildGuard, Yes2SDKBuildModeOverride) run as
    /// IPreprocessBuildWithReport, so Unity invokes them on EVERY WebGL build in
    /// the project — including builds driven by another platform's pipeline that
    /// uses its own WebGL template. Those builds legitimately don't use the
    /// Yes2SDK template and shouldn't be failed by our guard.
    ///
    /// When this is disabled, the Yes2SDK build callbacks become no-ops so the
    /// SDK can stay installed (runtime API, Inspector, etc.) while a non-Yes2SDK
    /// platform drives the build. Re-enable it for Yes2Games builds.
    ///
    /// Defaults to enabled — existing users see no behavior change.
    /// </summary>
    public static class Yes2SDKPipeline
    {
        private const string EditorPrefsKey = "Yes2SDK.PipelineEnabled";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EditorPrefsKey, true);
            set => EditorPrefs.SetBool(EditorPrefsKey, value);
        }
    }
}
