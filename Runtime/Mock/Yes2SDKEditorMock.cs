#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Editor-only Play Mode mock settings, surfaced as toggles in the
    /// Yes2SDK Build Window (Play Mode Testing section).
    ///
    /// Lives in the Runtime assembly (inside UNITY_EDITOR) so the mock code
    /// paths in Yes2SDKAds / Yes2SDKIAP can read the same EditorPrefs values
    /// the Build Window writes, without the Runtime assembly referencing the
    /// Editor assembly.
    /// </summary>
    public static class Yes2SDKEditorMock
    {
        private const string AdPopupKey = "Yes2SDK.EditorMock.AdPopup";
        private const string IAPKey = "Yes2SDK.EditorMock.IAP";
        private const string AdResultKey = "Yes2SDK.EditorMock.AdResult";
        private const string IAPFailKey = "Yes2SDK.EditorMock.IAPFail";

        /// <summary>
        /// What ShowInterstitial / ShowRewarded resolve to in Play Mode.
        /// Anything other than Normal fires onError immediately (no popup),
        /// with the same error shapes the WebGL bridge delivers, so error
        /// handling paths can be tested in the Editor.
        /// </summary>
        public enum AdOutcome
        {
            Normal = 0,
            NoFill = 1,
            AdBlocked = 2,
            Error = 3
        }

        /// <summary>
        /// When enabled, ShowInterstitial / ShowRewarded display a mock ad
        /// popup in Play Mode; callbacks fire from the popup's buttons so
        /// pause/resume and reward handling can be exercised interactively.
        /// When disabled, ad callbacks fire synchronously with no UI
        /// (the pre-2.6.0 behavior).
        /// </summary>
        public static bool AdPopupEnabled
        {
            get => EditorPrefs.GetBool(AdPopupKey, true);
            set => EditorPrefs.SetBool(AdPopupKey, value);
        }

        /// <summary>
        /// When enabled, IAP is mocked in Play Mode: IsSupported() returns
        /// true, GetCatalogAsync returns a sample catalog, and PurchaseAsync
        /// opens a Buy / Cancel confirmation dialog. When disabled, IAP
        /// reports unsupported (the pre-2.6.0 behavior).
        /// </summary>
        public static bool IAPEnabled
        {
            get => EditorPrefs.GetBool(IAPKey, true);
            set => EditorPrefs.SetBool(IAPKey, value);
        }

        /// <summary>Simulated result for ad calls in Play Mode. Default Normal.</summary>
        public static AdOutcome AdResult
        {
            get => (AdOutcome)EditorPrefs.GetInt(AdResultKey, (int)AdOutcome.Normal);
            set => EditorPrefs.SetInt(AdResultKey, (int)value);
        }

        /// <summary>
        /// When enabled, PurchaseAsync fails with a platform-style error
        /// instead of showing the mock purchase dialog, so shop error
        /// handling can be tested. Default off.
        /// </summary>
        public static bool IAPFailPurchases
        {
            get => EditorPrefs.GetBool(IAPFailKey, false);
            set => EditorPrefs.SetBool(IAPFailKey, value);
        }

        /// <summary>
        /// True when the interactive mocks can actually be driven: Play Mode
        /// in a visible editor. Batch mode (CI test runs) has no rendering or
        /// input, so a popup waiting for a click would hang the run — those
        /// always take the synchronous legacy path.
        /// </summary>
        internal static bool CanShowPopups =>
            Application.isPlaying && !Application.isBatchMode;

        /// <summary>
        /// Build the simulated ad error for the current AdResult setting.
        /// Error codes and messages mirror what the WebGL bridge delivers:
        /// the jslib maps a real no-fill to code "NoFill", and a blocked
        /// platform surfaces the Core SDK's "ADS_BLOCKED" code verbatim.
        /// </summary>
        internal static bool TryGetSimulatedAdError(string kindLabel, string context, out Error error)
        {
            switch (AdResult)
            {
                case AdOutcome.NoFill:
                    error = new Error
                    {
                        Code = "NoFill",
                        Message = $"No {kindLabel} ad available (mock no-fill)",
                        Context = context
                    };
                    return true;
                case AdOutcome.AdBlocked:
                    error = new Error
                    {
                        Code = "ADS_BLOCKED",
                        Message = "Ad blocker detected (mock)",
                        Context = context
                    };
                    return true;
                case AdOutcome.Error:
                    error = new Error
                    {
                        Code = "PlatformError",
                        Message = $"Simulated {kindLabel} ad error (mock)",
                        Context = context
                    };
                    return true;
                default:
                    error = default;
                    return false;
            }
        }
    }
}
#endif
