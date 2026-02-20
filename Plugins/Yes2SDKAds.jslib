mergeInto(LibraryManager.library, {

    // Show an interstitial (full-screen) ad
    Yes2SDK_ShowInterstitialJS__deps: ['$__y2'],
    Yes2SDK_ShowInterstitialJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        // description is passed from C# but not used by the Core SDK API

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.ads === 'undefined') {
            window.__y2.error('SDK or Ads module not loaded. Yes2SDK:', typeof window.Yes2SDK, 'ads:', window.Yes2SDK ? typeof window.Yes2SDK.ads : 'N/A');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Ads module not loaded',
                context: 'Yes2SDK.Ads.ShowInterstitial'
            });
            SendMessage('Bridge', 'OnInterstitialError', errorJson);
            return;
        }

        window.Yes2SDK.ads.showInterstitial(placement, {
            beforeAd: function() {
                SendMessage('Bridge', 'OnInterstitialBeforeAd', '');
            },
            afterAd: function() {
                SendMessage('Bridge', 'OnInterstitialAfterAd', '');
            },
            noFill: function() {
                var errorJson = JSON.stringify({
                    code: 'NoFill',
                    message: 'No interstitial ad available',
                    context: 'Yes2SDK.Ads.ShowInterstitial'
                });
                SendMessage('Bridge', 'OnInterstitialError', errorJson);
            }
        }).catch(function(error) {
            var errorJson = JSON.stringify({
                code: (error && error.code) || 'Unknown',
                message: (error && error.message) || 'Interstitial ad failed',
                context: 'Yes2SDK.Ads.ShowInterstitial'
            });
            SendMessage('Bridge', 'OnInterstitialError', errorJson);
        });
    },

    // Show a rewarded video ad
    Yes2SDK_ShowRewardedJS__deps: ['$__y2'],
    Yes2SDK_ShowRewardedJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        // description is passed from C# but not used by the Core SDK API

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.ads === 'undefined') {
            window.__y2.error('SDK or Ads module not loaded. Yes2SDK:', typeof window.Yes2SDK, 'ads:', window.Yes2SDK ? typeof window.Yes2SDK.ads : 'N/A');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Ads module not loaded',
                context: 'Yes2SDK.Ads.ShowRewarded'
            });
            SendMessage('Bridge', 'OnRewardedError', errorJson);
            return;
        }

        window.Yes2SDK.ads.showRewarded(placement, {
            beforeAd: function() {
                SendMessage('Bridge', 'OnRewardedBeforeAd', '');
            },
            afterAd: function() {
                SendMessage('Bridge', 'OnRewardedAfterAd', '');
            },
            adDismissed: function() {
                SendMessage('Bridge', 'OnRewardedAdDismissed', '');
            },
            adViewed: function() {
                SendMessage('Bridge', 'OnRewardedAdViewed', '');
            },
            noFill: function() {
                var errorJson = JSON.stringify({
                    code: 'NoFill',
                    message: 'No rewarded ad available',
                    context: 'Yes2SDK.Ads.ShowRewarded'
                });
                SendMessage('Bridge', 'OnRewardedError', errorJson);
            }
        }).catch(function(error) {
            var errorJson = JSON.stringify({
                code: (error && error.code) || 'Unknown',
                message: (error && error.message) || 'Rewarded ad failed',
                context: 'Yes2SDK.Ads.ShowRewarded'
            });
            SendMessage('Bridge', 'OnRewardedError', errorJson);
        });
    },

    // Show a banner ad at the specified position
    Yes2SDK_ShowBannerJS__deps: ['$__y2'],
    Yes2SDK_ShowBannerJS: function(position) {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.ads === 'undefined') {
            window.__y2.error('SDK or Ads module not loaded.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Ads module not loaded',
                context: 'Yes2SDK.Ads.ShowBanner'
            });
            SendMessage('Bridge', 'OnBannerShowError', errorJson);
            return;
        }

        // Map position int to string: 0 = 'top', 1 = 'bottom'
        var positionStr = position === 0 ? 'top' : 'bottom';

        // Core SDK showBanner(position) returns a Promise, no callbacks
        window.Yes2SDK.ads.showBanner(positionStr)
            .then(function() {
                SendMessage('Bridge', 'OnBannerShown', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'Banner show failed',
                    context: 'Yes2SDK.Ads.ShowBanner'
                });
                SendMessage('Bridge', 'OnBannerShowError', errorJson);
            });
    },

    // Hide the currently displayed banner ad
    Yes2SDK_HideBannerJS__deps: ['$__y2'],
    Yes2SDK_HideBannerJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.ads === 'undefined') {
            window.__y2.error('SDK or Ads module not loaded.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Ads module not loaded',
                context: 'Yes2SDK.Ads.HideBanner'
            });
            SendMessage('Bridge', 'OnBannerHideError', errorJson);
            return;
        }

        // Core SDK hideBanner() is synchronous, no callbacks
        try {
            window.Yes2SDK.ads.hideBanner();
            SendMessage('Bridge', 'OnBannerHidden', '');
        } catch(error) {
            var errorJson = JSON.stringify({
                code: (error && error.code) || 'Unknown',
                message: (error && error.message) || 'Banner hide failed',
                context: 'Yes2SDK.Ads.HideBanner'
            });
            SendMessage('Bridge', 'OnBannerHideError', errorJson);
        }
    },

    // Check if ads are blocked for the current session
    Yes2SDK_IsAdBlockedJS__deps: ['$__y2'],
    Yes2SDK_IsAdBlockedJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.ads === 'undefined') {
            window.__y2.warn('SDK or Ads module not loaded. Returning false for isAdBlocked.');
            return false;
        }

        // isAdBlocked exists on the Poki inline wrapper but not on the Core SDK.
        // Fall back gracefully if the method doesn't exist.
        if (typeof window.Yes2SDK.ads.isAdBlocked === 'function') {
            return window.Yes2SDK.ads.isAdBlocked();
        }

        return false;
    }

});
