mergeInto(LibraryManager.library, {

    // Show an interstitial (full-screen) ad
    Yes2SDK_ShowInterstitialJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        var description = UTF8ToString(descriptionPtr);

        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded. Make sure yes2sdk.umd.js is included in the WebGL template.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Core not loaded',
                context: 'Yes2SDK.Ads.ShowInterstitial'
            });
            SendMessage('Bridge', 'OnInterstitialError', errorJson);
            return;
        }

        window.Yes2SDK.ads.showInterstitial(placement, description, {
            beforeAd: function() {
                SendMessage('Bridge', 'OnInterstitialBeforeAd', '');
            },
            afterAd: function() {
                SendMessage('Bridge', 'OnInterstitialAfterAd', '');
            },
            onError: function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Interstitial ad failed',
                    context: 'Yes2SDK.Ads.ShowInterstitial'
                });
                SendMessage('Bridge', 'OnInterstitialError', errorJson);
            }
        });
    },

    // Show a rewarded video ad
    Yes2SDK_ShowRewardedJS: function(placementPtr, descriptionPtr) {
        var placement = UTF8ToString(placementPtr);
        var description = UTF8ToString(descriptionPtr);

        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded. Make sure yes2sdk.umd.js is included in the WebGL template.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Core not loaded',
                context: 'Yes2SDK.Ads.ShowRewarded'
            });
            SendMessage('Bridge', 'OnRewardedError', errorJson);
            return;
        }

        window.Yes2SDK.ads.showRewarded(placement, description, {
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
            onError: function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Rewarded ad failed',
                    context: 'Yes2SDK.Ads.ShowRewarded'
                });
                SendMessage('Bridge', 'OnRewardedError', errorJson);
            }
        });
    },

    // Show a banner ad at the specified position
    Yes2SDK_ShowBannerJS: function(position) {
        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded. Make sure yes2sdk.umd.js is included in the WebGL template.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Core not loaded',
                context: 'Yes2SDK.Ads.ShowBanner'
            });
            SendMessage('Bridge', 'OnBannerShowError', errorJson);
            return;
        }

        // Map position int to string: 0 = 'top', 1 = 'bottom'
        var positionStr = position === 0 ? 'top' : 'bottom';

        window.Yes2SDK.ads.showBanner(positionStr, {
            onShown: function() {
                SendMessage('Bridge', 'OnBannerShown', '');
            },
            onError: function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Banner show failed',
                    context: 'Yes2SDK.Ads.ShowBanner'
                });
                SendMessage('Bridge', 'OnBannerShowError', errorJson);
            }
        });
    },

    // Hide the currently displayed banner ad
    Yes2SDK_HideBannerJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            console.error('[Yes2SDK] Core SDK not loaded. Make sure yes2sdk.umd.js is included in the WebGL template.');
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Core not loaded',
                context: 'Yes2SDK.Ads.HideBanner'
            });
            SendMessage('Bridge', 'OnBannerHideError', errorJson);
            return;
        }

        window.Yes2SDK.ads.hideBanner({
            onHidden: function() {
                SendMessage('Bridge', 'OnBannerHidden', '');
            },
            onError: function(error) {
                var errorJson = JSON.stringify({
                    code: error.code || 'Unknown',
                    message: error.message || 'Banner hide failed',
                    context: 'Yes2SDK.Ads.HideBanner'
                });
                SendMessage('Bridge', 'OnBannerHideError', errorJson);
            }
        });
    },

    // Check if ads are blocked for the current session
    Yes2SDK_IsAdBlockedJS: function() {
        if (typeof window.Yes2SDK === 'undefined') {
            console.warn('[Yes2SDK] Core SDK not loaded. Returning false for isAdBlocked.');
            return false;
        }

        return window.Yes2SDK.ads.isAdBlocked();
    }

});
