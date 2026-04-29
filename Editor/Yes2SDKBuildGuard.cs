using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Fails WebGL builds early when the Yes2SDK-SuperSDK template is missing or
    /// not selected in PlayerSettings. Without this guard, Unity silently falls
    /// back to its default WebGL template — the build "succeeds" but the JS
    /// bridge is never wired up, shipping a broken game.
    /// </summary>
    public class Yes2SDKBuildGuard : IPreprocessBuildWithReport
    {
        private const string ExpectedTemplate = "PROJECT:Yes2SDK-SuperSDK";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            if (!Yes2SDKInstaller.IsSetupComplete())
            {
                throw new BuildFailedException(
                    "[Yes2SDK] WebGL template is not installed. " +
                    "Open Yes2SDK > Build Window and click Install Template, " +
                    "then rebuild.");
            }

            if (PlayerSettings.WebGL.template != ExpectedTemplate)
            {
                throw new BuildFailedException(
                    $"[Yes2SDK] PlayerSettings WebGL template is '{PlayerSettings.WebGL.template}', " +
                    $"expected '{ExpectedTemplate}'. " +
                    "Open Yes2SDK > Build Window and click Apply Settings, " +
                    "or set the template manually in Project Settings > Player > WebGL " +
                    "(Unity 6+: in your Build Profile's Player Settings).");
            }
        }
    }
}
