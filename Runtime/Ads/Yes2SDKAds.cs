using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Ads API for Yes2SDK.
    /// Provides a unified interface for showing ads across all platforms.
    /// </summary>
    public class Yes2SDKAds
    {
        #region Static Callback Fields

        // Interstitial callbacks
        private static Action _interstitialBeforeAdCallback;
        private static Action _interstitialAfterAdCallback;
        private static Action<Error> _interstitialErrorCallback;

        // Rewarded callbacks
        private static Action _rewardedBeforeAdCallback;
        private static Action _rewardedAfterAdCallback;
        private static Action _rewardedAdDismissedCallback;
        private static Action _rewardedAdViewedCallback;
        private static Action<Error> _rewardedErrorCallback;

        // Banner callbacks
        private static Action _bannerShownCallback;
        private static Action<Error> _bannerShowErrorCallback;
        private static Action _bannerHiddenCallback;
        private static Action<Error> _bannerHideErrorCallback;

        // True between any ShowInterstitial / ShowRewarded call and its
        // afterAd / error completion. Used to reject concurrent ad calls and
        // exposed via IsAdShowing().
        private static bool _adInFlight;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_ShowInterstitialJS(string placement, string description);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_ShowRewardedJS(string placement, string description);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_ShowBannerJS(int position);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_HideBannerJS();

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IsAdBlockedJS();

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IsRewardedAdAvailableJS();

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IsInterstitialSupportedJS();

        [DllImport("__Internal")]
        private static extern bool Yes2SDK_IsRewardedSupportedJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Shows an interstitial (full-screen) ad.
        /// These can be shown at various points in the game such as level end, restart, or timed intervals.
        /// </summary>
        /// <param name="placement">Placement identifier for analytics tracking.</param>
        /// <param name="description">Human-readable description of the ad placement.</param>
        /// <param name="beforeAd">Called before the ad is shown. Pause your game here.</param>
        /// <param name="afterAd">Called after the ad completes (shown or not). Resume your game here.</param>
        /// <param name="onError">Called if an error occurs while showing the ad.</param>
        /// <example><code>
        /// Yes2SDK.Ads.ShowInterstitial("level-complete", "After completing level 1",
        ///     beforeAd: () => PauseGame(),
        ///     afterAd: () => ResumeGame(),
        ///     onError: (error) => Debug.LogError(error));
        /// </code></example>
        public void ShowInterstitial(
            string placement,
            string description,
            Action beforeAd = null,
            Action afterAd = null,
            Action<Error> onError = null)
        {
            if (_adInFlight)
            {
                onError?.Invoke(new Error
                {
                    Code = "InvalidParams",
                    Message = "Another ad is already in flight (AdAlreadyShowing). Wait for afterAd before calling Show* again.",
                    Context = "Yes2SDK.Ads.ShowInterstitial"
                });
                return;
            }
            _adInFlight = true;

            // Store callbacks
            _interstitialBeforeAdCallback = beforeAd;
            _interstitialAfterAdCallback = afterAd;
            _interstitialErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_ShowInterstitialJS(placement, description);
#else
            Yes2Log.Log($"Mock: ShowInterstitial(placement: {placement}, description: {description})");
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.CanShowPopups)
            {
                // Failure simulation (Ad result dropdown in the Build Window):
                // fires onError immediately, no popup, applies even with the
                // popup toggle off. A real no-fill/blocked ad never reaches
                // beforeAd, so neither does the simulated one.
                if (Yes2SDKEditorMock.TryGetSimulatedAdError("interstitial", "Yes2SDK.Ads.ShowInterstitial", out var simulatedError))
                {
                    Yes2Log.Log($"Mock: simulated interstitial failure ({simulatedError.Code})");
                    InvokeInterstitialError(simulatedError);
                    return;
                }

                // Interactive popup path: callbacks fire from the popup's
                // buttons so pause/resume wiring can be exercised. Falls
                // through to the synchronous flow when the popup is disabled
                // or unavailable.
                if (Yes2SDKEditorMock.AdPopupEnabled && Yes2SDKMockOverlay.ShowInterstitial(placement))
                {
                    return;
                }
            }
#endif
            // Simulate ad flow in Editor
            _interstitialBeforeAdCallback?.Invoke();
            _interstitialAfterAdCallback?.Invoke();
            _adInFlight = false;
#endif
        }

        /// <summary>
        /// Shows a rewarded video ad.
        /// These are longer, optional ads that the player can earn a reward for watching.
        /// The player must be notified and give permission before showing.
        /// </summary>
        /// <param name="placement">Placement identifier for analytics tracking.</param>
        /// <param name="description">Human-readable description of the ad placement.</param>
        /// <param name="beforeAd">Called before the ad is shown. Pause your game here.</param>
        /// <param name="afterAd">Called after the ad completes. Resume your game here.</param>
        /// <param name="adDismissed">Called when the player dismisses the ad before completion. Do not reward.</param>
        /// <param name="adViewed">Called when the player successfully watched the ad. Grant the reward here.</param>
        /// <param name="onError">Called if an error occurs while showing the ad.</param>
        /// <remarks>
        /// In the Unity Editor, a mock ad popup is shown by default (toggle it in
        /// Yes2SDK > Build Window > Play Mode Testing): Claim Reward triggers adViewed,
        /// Skip triggers adDismissed. With the popup disabled, callbacks fire instantly
        /// and passing "dismiss" as the description triggers adDismissed for testing;
        /// any other description triggers adViewed.
        /// </remarks>
        /// <example><code>
        /// Yes2SDK.Ads.ShowRewarded("extra-life", "Watch ad for extra life",
        ///     beforeAd: () => PauseGame(),
        ///     afterAd: () => ResumeGame(),
        ///     adDismissed: () => Debug.Log("No reward"),
        ///     adViewed: () => GrantExtraLife(),
        ///     onError: (error) => Debug.LogError(error));
        /// </code></example>
        public void ShowRewarded(
            string placement,
            string description,
            Action beforeAd = null,
            Action afterAd = null,
            Action adDismissed = null,
            Action adViewed = null,
            Action<Error> onError = null)
        {
            if (_adInFlight)
            {
                onError?.Invoke(new Error
                {
                    Code = "InvalidParams",
                    Message = "Another ad is already in flight (AdAlreadyShowing). Wait for afterAd before calling Show* again.",
                    Context = "Yes2SDK.Ads.ShowRewarded"
                });
                return;
            }
            _adInFlight = true;

            // Store callbacks
            _rewardedBeforeAdCallback = beforeAd;
            _rewardedAfterAdCallback = afterAd;
            _rewardedAdDismissedCallback = adDismissed;
            _rewardedAdViewedCallback = adViewed;
            _rewardedErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_ShowRewardedJS(placement, description);
#else
            Yes2Log.Log($"Mock: ShowRewarded(placement: {placement}, description: {description})");
#if UNITY_EDITOR
            if (Yes2SDKEditorMock.CanShowPopups)
            {
                // Failure simulation, same shape as the interstitial path.
                if (Yes2SDKEditorMock.TryGetSimulatedAdError("rewarded", "Yes2SDK.Ads.ShowRewarded", out var simulatedError))
                {
                    Yes2Log.Log($"Mock: simulated rewarded failure ({simulatedError.Code})");
                    InvokeRewardedError(simulatedError);
                    return;
                }

                // Interactive popup path: Claim Reward fires adViewed, Skip
                // fires adDismissed, so both outcomes are testable without
                // the "dismiss" description convention. Falls through to the
                // synchronous flow when the popup is disabled or unavailable.
                if (Yes2SDKEditorMock.AdPopupEnabled && Yes2SDKMockOverlay.ShowRewarded(placement))
                {
                    return;
                }
            }
#endif
            // Simulate ad flow in Editor
            _rewardedBeforeAdCallback?.Invoke();
            _rewardedAfterAdCallback?.Invoke();

            // For testing: "dismiss" description triggers dismissed callback
            if (description == "dismiss")
            {
                _rewardedAdDismissedCallback?.Invoke();
            }
            else
            {
                _rewardedAdViewedCallback?.Invoke();
            }
            _adInFlight = false;
#endif
        }

        /// <summary>
        /// Shows a banner ad at the specified position.
        /// These are small ads typically shown on menus or non-gameplay screens.
        /// </summary>
        /// <param name="position">Position of the banner on screen (Top or Bottom).</param>
        /// <param name="onShown">Called when the banner is successfully displayed.</param>
        /// <param name="onError">Called if an error occurs while showing the banner.</param>
        /// <example><code>
        /// Yes2SDK.Ads.ShowBanner(BannerPosition.Bottom,
        ///     onShown: () => Debug.Log("Banner shown"),
        ///     onError: (error) => Debug.LogError(error));
        /// </code></example>
        public void ShowBanner(
            BannerPosition position = BannerPosition.Bottom,
            Action onShown = null,
            Action<Error> onError = null)
        {
            // Store callbacks
            _bannerShownCallback = onShown;
            _bannerShowErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_ShowBannerJS((int)position);
#else
            Yes2Log.Log($"Mock: ShowBanner(position: {position})");
            _bannerShownCallback?.Invoke();
#endif
        }

        /// <summary>
        /// Hides the currently displayed banner ad.
        /// </summary>
        /// <param name="onHidden">Called when the banner is successfully hidden.</param>
        /// <param name="onError">Called if an error occurs while hiding the banner.</param>
        /// <example><code>
        /// Yes2SDK.Ads.HideBanner(
        ///     onHidden: () => Debug.Log("Banner hidden"),
        ///     onError: (error) => Debug.LogError(error));
        /// </code></example>
        public void HideBanner(
            Action onHidden = null,
            Action<Error> onError = null)
        {
            // Store callbacks
            _bannerHiddenCallback = onHidden;
            _bannerHideErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_HideBannerJS();
#else
            Yes2Log.Log("Mock: HideBanner()");
            _bannerHiddenCallback?.Invoke();
#endif
        }

        /// <summary>
        /// Returns whether ads are blocked for the current session.
        /// This can be used to determine if an alternative flow should be used
        /// instead of showing ads, or prompt the player to disable their ad blocker.
        /// </summary>
        /// <returns>True if ads are blocked, false otherwise.</returns>
        /// <example><code>
        /// if (Yes2SDK.Ads.IsAdBlocked())
        /// {
        ///     // Show message to player or use alternative flow
        ///     Debug.Log("Ads are blocked");
        /// }
        /// </code></example>
        public bool IsAdBlocked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsAdBlockedJS();
#else
#if UNITY_EDITOR
            // Matches the Ad result failure simulation: while "Ad blocked" is
            // selected, report blocked so alternative-flow UI can be tested.
            if (Yes2SDKEditorMock.CanShowPopups
                && Yes2SDKEditorMock.AdResult == Yes2SDKEditorMock.AdOutcome.AdBlocked)
            {
                Yes2Log.Log("Mock: IsAdBlocked() - returning true (simulated)");
                return true;
            }
#endif
            Yes2Log.Log("Mock: IsAdBlocked() - returning false");
            return false;
#endif
        }

        /// <summary>
        /// Returns true while a `ShowInterstitial` or `ShowRewarded` call is in
        /// progress (between the initial call and `afterAd`/error). Use this to
        /// gate UI that triggers ads — hiding the "Watch for reward" button
        /// while one is already showing prevents broken state.
        /// </summary>
        public bool IsAdShowing() => _adInFlight;

        /// <summary>
        /// Best-effort check whether a rewarded ad is currently available to show.
        /// </summary>
        /// <remarks>
        /// Most platform SDKs don't expose an explicit readiness API — this returns
        /// true when the platform's ad module appears loaded. Treat the result as a
        /// hint for gating UI; calling <see cref="ShowRewarded"/> after a true result
        /// can still fail (e.g. no fill).
        /// </remarks>
        public bool IsRewardedAdAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsRewardedAdAvailableJS();
#else
#if UNITY_EDITOR
            // With the mock ad popup enabled, report available so "watch ad"
            // buttons gated on this can be tested in Play Mode.
            if (Yes2SDKEditorMock.AdPopupEnabled && Yes2SDKEditorMock.CanShowPopups)
            {
                return true;
            }
#endif
            return false;
#endif
        }

        /// <summary>
        /// Whether the current platform serves interstitial ads at all. Use this
        /// to feature-gate ad UI up front, rather than hard-coding which
        /// platforms have the format.
        /// </summary>
        /// <remarks>
        /// This asks whether the format exists on the platform, not whether an
        /// ad is ready right now. For readiness of a rewarded ad, see
        /// <see cref="IsRewardedAdAvailable"/>.
        /// </remarks>
        /// <example><code>
        /// if (!Yes2SDK.Ads.IsInterstitialSupported())
        /// {
        ///     // Skip the between-levels ad break entirely on this platform.
        /// }
        /// </code></example>
        public bool IsInterstitialSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsInterstitialSupportedJS();
#elif UNITY_EDITOR
            // The editor mock serves both ad formats, through the popup when it
            // is enabled and synchronously when it is not, so gated ad UI stays
            // reachable in Play Mode.
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Whether the current platform serves rewarded ads at all. Use this to
        /// feature-gate reward offers up front, rather than hard-coding which
        /// platforms have the format.
        /// </summary>
        /// <remarks>
        /// This asks whether the format exists on the platform, not whether an
        /// ad is ready right now. For readiness, see
        /// <see cref="IsRewardedAdAvailable"/>.
        /// </remarks>
        /// <example><code>
        /// // Hide the whole "double your coins" offer where the format is absent.
        /// doubleCoinsButton.gameObject.SetActive(Yes2SDK.Ads.IsRewardedSupported());
        /// </code></example>
        public bool IsRewardedSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Yes2SDK_IsRewardedSupportedJS();
#elif UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        /// <summary>
        /// Called by Bridge when interstitial beforeAd callback is received from JS.
        /// </summary>
        internal static void InvokeInterstitialBeforeAd()
        {
            _interstitialBeforeAdCallback?.Invoke();
        }

        /// <summary>
        /// Called by Bridge when interstitial afterAd callback is received from JS.
        /// </summary>
        internal static void InvokeInterstitialAfterAd()
        {
            _interstitialAfterAdCallback?.Invoke();
            ClearInterstitialCallbacks();
            _adInFlight = false;
        }

        /// <summary>
        /// Called by Bridge when interstitial error callback is received from JS.
        /// </summary>
        internal static void InvokeInterstitialError(Error error)
        {
            _interstitialErrorCallback?.Invoke(error);
            ClearInterstitialCallbacks();
            _adInFlight = false;
        }

        /// <summary>
        /// Called by Bridge when rewarded beforeAd callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedBeforeAd()
        {
            _rewardedBeforeAdCallback?.Invoke();
        }

        /// <summary>
        /// Called by Bridge when rewarded afterAd callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedAfterAd()
        {
            _rewardedAfterAdCallback?.Invoke();
        }

        /// <summary>
        /// Called by Bridge when rewarded adDismissed callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedAdDismissed()
        {
            _rewardedAdDismissedCallback?.Invoke();
            ClearRewardedCallbacks();
            _adInFlight = false;
        }

        /// <summary>
        /// Called by Bridge when rewarded adViewed callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedAdViewed()
        {
            _rewardedAdViewedCallback?.Invoke();
            ClearRewardedCallbacks();
            _adInFlight = false;
        }

        /// <summary>
        /// Called by Bridge when rewarded error callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedError(Error error)
        {
            _rewardedErrorCallback?.Invoke(error);
            ClearRewardedCallbacks();
            _adInFlight = false;
        }

        /// <summary>
        /// Called by Bridge when banner shown callback is received from JS.
        /// </summary>
        internal static void InvokeBannerShown()
        {
            _bannerShownCallback?.Invoke();
            _bannerShownCallback = null;
            _bannerShowErrorCallback = null;
        }

        /// <summary>
        /// Called by Bridge when banner show error callback is received from JS.
        /// </summary>
        internal static void InvokeBannerShowError(Error error)
        {
            _bannerShowErrorCallback?.Invoke(error);
            _bannerShownCallback = null;
            _bannerShowErrorCallback = null;
        }

        /// <summary>
        /// Called by Bridge when banner hidden callback is received from JS.
        /// </summary>
        internal static void InvokeBannerHidden()
        {
            _bannerHiddenCallback?.Invoke();
            _bannerHiddenCallback = null;
            _bannerHideErrorCallback = null;
        }

        /// <summary>
        /// Called by Bridge when banner hide error callback is received from JS.
        /// </summary>
        internal static void InvokeBannerHideError(Error error)
        {
            _bannerHideErrorCallback?.Invoke(error);
            _bannerHiddenCallback = null;
            _bannerHideErrorCallback = null;
        }

        #endregion

        #region Private Helper Methods

#if UNITY_EDITOR
        // The interactive mock popup (Yes2SDKMockOverlay) can leave an ad
        // pending when Play Mode is stopped mid-ad. With Domain Reload
        // disabled (Enter Play Mode Options) statics survive into the next
        // play, so a stuck _adInFlight would reject every ad call. Reset
        // explicitly on each play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            _adInFlight = false;
            ClearInterstitialCallbacks();
            ClearRewardedCallbacks();
        }
#endif

        private static void ClearInterstitialCallbacks()
        {
            _interstitialBeforeAdCallback = null;
            _interstitialAfterAdCallback = null;
            _interstitialErrorCallback = null;
        }

        private static void ClearRewardedCallbacks()
        {
            _rewardedBeforeAdCallback = null;
            _rewardedAfterAdCallback = null;
            _rewardedAdDismissedCallback = null;
            _rewardedAdViewedCallback = null;
            _rewardedErrorCallback = null;
        }

        #endregion
    }
}
