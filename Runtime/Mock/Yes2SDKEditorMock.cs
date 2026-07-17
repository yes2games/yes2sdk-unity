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

        /// <summary>
        /// True when the interactive mocks can actually be driven: Play Mode
        /// in a visible editor. Batch mode (CI test runs) has no rendering or
        /// input, so a popup waiting for a click would hang the run — those
        /// always take the synchronous legacy path.
        /// </summary>
        internal static bool CanShowPopups =>
            Application.isPlaying && !Application.isBatchMode;
    }
}
#endif
