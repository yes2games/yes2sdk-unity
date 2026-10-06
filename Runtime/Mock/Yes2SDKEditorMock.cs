#if UNITY_EDITOR
using System;
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
        private const string PlayerRegisteredKey = "Yes2SDK.EditorMock.PlayerRegistered";
        private const string EntryPointDataKey = "Yes2SDK.EditorMock.EntryPointData";
        private const string PlatformServicesKey = "Yes2SDK.EditorMock.PlatformServices";
        private const string ReferralShareResultKey = "Yes2SDK.EditorMock.ReferralShareResult";
        private const string ReferralConversionsKey = "Yes2SDK.EditorMock.ReferralConversions";

        /// <summary>Maximum value accepted by <see cref="ReferralConversions"/>.</summary>
        internal const int MaxReferralConversions = 20;

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

        /// <summary>What a mock referral share resolves to in Play Mode.</summary>
        public enum ShareOutcome
        {
            Shared = 0,
            Cancelled = 1,
            Error = 2
        }

        /// <summary>
        /// When enabled, the mock player counts as registered (signed in).
        /// Default off, which is a guest player.
        /// </summary>
        public static bool PlayerRegistered
        {
            get => EditorPrefs.GetBool(PlayerRegisteredKey, false);
            set => EditorPrefs.SetBool(PlayerRegisteredKey, value);
        }

        /// <summary>
        /// JSON object returned by Session.GetEntryPointData() in Play Mode.
        /// Default "{}".
        /// </summary>
        public static string EntryPointDataJson
        {
            get => EditorPrefs.GetString(EntryPointDataKey, "{}");
            set => EditorPrefs.SetString(EntryPointDataKey, string.IsNullOrEmpty(value) ? "{}" : value);
        }

        /// <summary>
        /// When enabled, the referral and notification mocks are active in
        /// Play Mode. When disabled they report unsupported. Default on.
        /// </summary>
        public static bool PlatformServicesEnabled
        {
            get => EditorPrefs.GetBool(PlatformServicesKey, true);
            set => EditorPrefs.SetBool(PlatformServicesKey, value);
        }

        /// <summary>Simulated result of a referral share in Play Mode. Default Shared.</summary>
        public static ShareOutcome ReferralShareResult
        {
            get => (ShareOutcome)EditorPrefs.GetInt(ReferralShareResultKey, (int)ShareOutcome.Shared);
            set => EditorPrefs.SetInt(ReferralShareResultKey, (int)value);
        }

        /// <summary>
        /// Conversions returned per shared reference in Play Mode, clamped
        /// to 0..20. Default 1.
        /// </summary>
        public static int ReferralConversions
        {
            get => ClampConversions(EditorPrefs.GetInt(ReferralConversionsKey, 1));
            set => EditorPrefs.SetInt(ReferralConversionsKey, ClampConversions(value));
        }

        internal static int ClampConversions(int value) =>
            Mathf.Clamp(value, 0, MaxReferralConversions);

        /// <summary>
        /// Session-only override set when a mock registration prompt login
        /// succeeds. Not persisted; reset when Play Mode starts.
        /// </summary>
        internal static bool SessionRegisteredOverride;

        /// <summary>True when the mock player is registered right now.</summary>
        internal static bool IsRegisteredNow => SessionRegisteredOverride || PlayerRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            SessionRegisteredOverride = false;
        }

        /// <summary>
        /// Simulates the platform asking the game to close (Play Mode only):
        /// raises <see cref="Yes2SDK.OnExitRequested"/>, then flushes saved
        /// data the way the platform does right after the event.
        /// </summary>
        public static void SimulateExitRequest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Yes2SDK] SimulateExitRequest only works in Play Mode.");
                return;
            }
            RaiseExitRequestAndFlush();
        }

        // Handler exceptions are logged and the flush always runs, like the
        // device path where the bridge logs a throwing handler and the SDK
        // still flushes player data afterwards.
        internal static void RaiseExitRequestAndFlush()
        {
            try
            {
                Yes2SDK.InvokeExitRequested();
            }
            catch (Exception e)
            {
                Yes2Log.Error($"OnExitRequested handler threw: {e}");
            }
            PlayerPrefs.Save();
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
