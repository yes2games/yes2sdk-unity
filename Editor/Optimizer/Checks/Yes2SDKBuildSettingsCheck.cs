using System.Collections.Generic;
using UnityEditor;

namespace Yes2SDK.Editor
{
    /// <summary>Reports WebGL player settings that differ from the pipeline's recommended values.</summary>
    public sealed class Yes2SDKBuildSettingsCheck : IYes2SDKOptimizationCheck
    {
        public string Id => "build-settings";

        public Yes2SDKOptimizationCategory Category => Yes2SDKOptimizationCategory.Build;

        public string Title => "WebGL build settings";

        public string DocsAnchor => "build-settings";

        public bool CanFix => true;

        public IReadOnlyList<Yes2SDKOptimizationFinding> Analyze()
        {
            var config = BuildConfig.Default;
            var findings = new List<Yes2SDKOptimizationFinding>();

            var wantedTemplate = "PROJECT:" + config.TemplateName;
            if (PlayerSettings.WebGL.template != wantedTemplate)
            {
                findings.Add(Drift("WebGL Template", PlayerSettings.WebGL.template, wantedTemplate));
            }

            if (PlayerSettings.WebGL.compressionFormat != config.Compression)
            {
                findings.Add(Drift("Compression Format", PlayerSettings.WebGL.compressionFormat, config.Compression));
            }

            if (PlayerSettings.WebGL.decompressionFallback != config.DecompressionFallback)
            {
                findings.Add(Drift("Decompression Fallback", PlayerSettings.WebGL.decompressionFallback, config.DecompressionFallback));
            }

            var stripping = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
            if (stripping != config.CodeStripping)
            {
                findings.Add(Drift("Managed Stripping Level", stripping, config.CodeStripping));
            }

            if (PlayerSettings.WebGL.exceptionSupport != config.ExceptionSupport)
            {
                findings.Add(Drift("Exception Support", PlayerSettings.WebGL.exceptionSupport, config.ExceptionSupport));
            }

            return findings;
        }

        // Player settings live outside Assets and are not on the Undo stack. The confirmation
        // dialog lists each setting and its current value, so the change is reversible by hand
        // from the same window that reported it.
        public void Fix(IReadOnlyList<Yes2SDKOptimizationFinding> findings)
        {
            BuildConfig.Default.ApplySettings();
        }

        private static Yes2SDKOptimizationFinding Drift(string setting, object actual, object recommended)
        {
            return new Yes2SDKOptimizationFinding
            {
                Severity = Yes2SDKFindingSeverity.Warning,
                Message = string.Format("{0} is {1}, recommended is {2}.", setting, actual, recommended),
                Fixable = true,
            };
        }
    }
}
