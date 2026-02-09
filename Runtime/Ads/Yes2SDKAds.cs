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
            // Store callbacks
            _interstitialBeforeAdCallback = beforeAd;
            _interstitialAfterAdCallback = afterAd;
            _interstitialErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_ShowInterstitialJS(placement, description);
#else
            Debug.Log($"[Yes2SDK] Mock: ShowInterstitial(placement: {placement}, description: {description})");
            // Simulate ad flow in Editor
            _interstitialBeforeAdCallback?.Invoke();
            _interstitialAfterAdCallback?.Invoke();
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
        /// In the Unity Editor, passing "dismiss" as the description will trigger the adDismissed callback
        /// for testing purposes. Any other description will trigger adViewed.
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
            // Store callbacks
            _rewardedBeforeAdCallback = beforeAd;
            _rewardedAfterAdCallback = afterAd;
            _rewardedAdDismissedCallback = adDismissed;
            _rewardedAdViewedCallback = adViewed;
            _rewardedErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_ShowRewardedJS(placement, description);
#else
            Debug.Log($"[Yes2SDK] Mock: ShowRewarded(placement: {placement}, description: {description})");
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
            Debug.Log($"[Yes2SDK] Mock: ShowBanner(position: {position})");
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
            Debug.Log("[Yes2SDK] Mock: HideBanner()");
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
            Debug.Log("[Yes2SDK] Mock: IsAdBlocked() - returning false");
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
        }

        /// <summary>
        /// Called by Bridge when interstitial error callback is received from JS.
        /// </summary>
        internal static void InvokeInterstitialError(Error error)
        {
            _interstitialErrorCallback?.Invoke(error);
            ClearInterstitialCallbacks();
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
        }

        /// <summary>
        /// Called by Bridge when rewarded adViewed callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedAdViewed()
        {
            _rewardedAdViewedCallback?.Invoke();
            ClearRewardedCallbacks();
        }

        /// <summary>
        /// Called by Bridge when rewarded error callback is received from JS.
        /// </summary>
        internal static void InvokeRewardedError(Error error)
        {
            _rewardedErrorCallback?.Invoke(error);
            ClearRewardedCallbacks();
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
