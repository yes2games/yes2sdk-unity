mergeInto(LibraryManager.library, {

    // Show an interstitial (full-screen) ad
    Yes2SDK_ShowInterstitialJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_ShowInterstitialJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        // description is passed from C# but not used by the Core SDK API

        if (!__y2h.has('ads')) {
            window.__y2.error('SDK or Ads module not loaded.');
            __y2h.sendError('OnInterstitialError', 'NotInitialized', 'Yes2SDK Ads module not loaded', 'Yes2SDK.Ads.ShowInterstitial');
            return;
        }

        try {
            window.Yes2SDK.ads.showInterstitial(placement, {
                beforeAd: function() {
                    SendMessage('Bridge', 'OnInterstitialBeforeAd', '');
                },
                afterAd: function() {
                    __y2h.resumeAudio();
                    SendMessage('Bridge', 'OnInterstitialAfterAd', '');
                },
                noFill: function() {
                    __y2h.sendError('OnInterstitialError', 'NoFill', 'No interstitial ad available', 'Yes2SDK.Ads.ShowInterstitial');
                }
            }).catch(__y2h.handleCatch('OnInterstitialError', 'Interstitial ad failed', 'Yes2SDK.Ads.ShowInterstitial'))
              // afterAd does not fire on a no-fill or an error, and a strategy
              // whose promise never settles would leave audio dead, so resume on
              // both the callback and the settle. resumeAudio is idempotent.
              .then(__y2h.resumeAudio);
        } catch(error) {
            __y2h.handleCatch('OnInterstitialError', 'Interstitial ad failed', 'Yes2SDK.Ads.ShowInterstitial')(error);
        }
    },

    // Show a rewarded video ad
    Yes2SDK_ShowRewardedJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_ShowRewardedJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        // description is passed from C# but not used by the Core SDK API

        if (!__y2h.has('ads')) {
            window.__y2.error('SDK or Ads module not loaded.');
            __y2h.sendError('OnRewardedError', 'NotInitialized', 'Yes2SDK Ads module not loaded', 'Yes2SDK.Ads.ShowRewarded');
            return;
        }

        try {
            window.Yes2SDK.ads.showRewarded(placement, {
                beforeAd: function() {
                    SendMessage('Bridge', 'OnRewardedBeforeAd', '');
                },
                afterAd: function() {
                    __y2h.resumeAudio();
                    SendMessage('Bridge', 'OnRewardedAfterAd', '');
                },
                adDismissed: function() {
                    SendMessage('Bridge', 'OnRewardedAdDismissed', '');
                },
                adViewed: function() {
                    SendMessage('Bridge', 'OnRewardedAdViewed', '');
                },
                noFill: function() {
                    __y2h.sendError('OnRewardedError', 'NoFill', 'No rewarded ad available', 'Yes2SDK.Ads.ShowRewarded');
                }
            }).catch(__y2h.handleCatch('OnRewardedError', 'Rewarded ad failed', 'Yes2SDK.Ads.ShowRewarded'))
              .then(__y2h.resumeAudio);
        } catch(error) {
            __y2h.handleCatch('OnRewardedError', 'Rewarded ad failed', 'Yes2SDK.Ads.ShowRewarded')(error);
        }
    },

    // Show a banner ad at the specified position
    Yes2SDK_ShowBannerJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_ShowBannerJS: function(position) {
        if (!__y2h.has('ads')) {
            window.__y2.error('SDK or Ads module not loaded.');
            __y2h.sendError('OnBannerShowError', 'NotInitialized', 'Yes2SDK Ads module not loaded', 'Yes2SDK.Ads.ShowBanner');
            return;
        }

        // Map position int to string: 0 = 'top', 1 = 'bottom'
        var positionStr = position === 0 ? 'top' : 'bottom';

        // Core SDK showBanner(position) returns a Promise, no callbacks
        try {
            window.Yes2SDK.ads.showBanner(positionStr)
                .then(function() {
                    SendMessage('Bridge', 'OnBannerShown', '');
                })
                .catch(__y2h.handleCatch('OnBannerShowError', 'Banner show failed', 'Yes2SDK.Ads.ShowBanner'));
        } catch(error) {
            __y2h.handleCatch('OnBannerShowError', 'Banner show failed', 'Yes2SDK.Ads.ShowBanner')(error);
        }
    },

    // Hide the currently displayed banner ad
    Yes2SDK_HideBannerJS__deps: ['$__y2', '$__y2h'],
    Yes2SDK_HideBannerJS: function() {
        if (!__y2h.has('ads')) {
            window.__y2.error('SDK or Ads module not loaded.');
            __y2h.sendError('OnBannerHideError', 'NotInitialized', 'Yes2SDK Ads module not loaded', 'Yes2SDK.Ads.HideBanner');
            return;
        }

        // Core SDK hideBanner() is synchronous, no callbacks
        try {
            window.Yes2SDK.ads.hideBanner();
            SendMessage('Bridge', 'OnBannerHidden', '');
        } catch(error) {
            __y2h.handleCatch('OnBannerHideError', 'Banner hide failed', 'Yes2SDK.Ads.HideBanner')(error);
        }
    },

    // Check if ads are blocked for the current session
    Yes2SDK_IsAdBlockedJS__deps: ['$__y2h'],
    Yes2SDK_IsAdBlockedJS: function() {
        if (!__y2h.has('ads')) return false;

        // isAdBlocked exists on the Poki inline wrapper but not on the Core SDK.
        // Fall back gracefully if the method doesn't exist.
        if (typeof window.Yes2SDK.ads.isAdBlocked === 'function') {
            return window.Yes2SDK.ads.isAdBlocked();
        }

        return false;
    },

    // Best-effort check whether a rewarded ad is currently available.
    // Returns false if the platform doesn't expose readiness or the ads module isn't loaded.
    Yes2SDK_IsRewardedAdAvailableJS__deps: ['$__y2h'],
    Yes2SDK_IsRewardedAdAvailableJS: function() {
        if (!__y2h.has('ads')) return false;
        if (typeof window.Yes2SDK.ads.isRewardedAdAvailable === 'function') {
            try { return window.Yes2SDK.ads.isRewardedAdAvailable() ? 1 : 0; }
            catch (e) { return 0; }
        }
        return 0;
    }

});
