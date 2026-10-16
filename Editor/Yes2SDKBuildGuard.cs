using System.Globalization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yes2SDK.Editor
{
    /// <summary>
    /// Fails WebGL builds early when the Yes2SDK-SuperSDK template is missing or
    /// not selected in PlayerSettings. Without this guard, Unity silently falls
    /// back to its default WebGL template — the build "succeeds" but the JS
    /// bridge is never wired up, shipping a broken game.
    ///
    /// Skipped entirely when Yes2SDKPipeline.Enabled is false, so a non-Yes2SDK
    /// platform can drive a WebGL build (with its own template) without being
    /// blocked by this guard.
    /// </summary>
    public class Yes2SDKBuildGuard : IPreprocessBuildWithReport
    {
        private const string ExpectedTemplate = "PROJECT:Yes2SDK-SuperSDK";
        private const string MobileMaxDprVariable = "MOBILE_MAX_DPR";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            if (!Yes2SDKPipeline.Enabled)
            {
                Debug.Log(
                    "[Yes2SDK] Build pipeline disabled — skipping template guard. " +
                    "Building for a non-Yes2SDK platform. Re-enable in " +
                    "Yes2SDK > Build Window for Yes2Games builds.");
                return;
            }

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

            string mobileMaxDpr = PlayerSettings.GetTemplateCustomValue(MobileMaxDprVariable);
            if (!IsValidMobileMaxDpr(mobileMaxDpr))
            {
                throw new BuildFailedException(
                    $"[Yes2SDK] WebGL template variable {MobileMaxDprVariable} is '{mobileMaxDpr}'. " +
                    "Leave it empty to keep the full device pixel ratio, or set a positive number " +
                    "such as 1.4 in Project Settings > Player > WebGL > Resolution and Presentation.");
            }
        }

        /// <summary>True when the MOBILE_MAX_DPR template variable is empty (no cap) or a positive finite number.</summary>
        public static bool IsValidMobileMaxDpr(string value)
        {
            return string.IsNullOrEmpty(value) ||
                (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float cap) &&
                 cap > 0f && !float.IsInfinity(cap));
        }
    }
}
