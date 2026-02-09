mergeInto(LibraryManager.library, {

    Yes2SDK_Banners_ShowBannerJS: function(idPtr, sizePtr, x, y) {
        var id = UTF8ToString(idPtr);
        var size = UTF8ToString(sizePtr);

        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.banners === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Banners module not loaded',
                context: 'Yes2SDK.Banners.ShowBanner'
            });
            SendMessage('Bridge', 'OnBannerRequestError', errorJson);
            return;
        }

        window.Yes2SDK.banners.showBanner(id, size, x, y)
            .then(function() {
                SendMessage('Bridge', 'OnBannerRequestSuccess', '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'ShowBanner failed',
                    context: 'Yes2SDK.Banners.ShowBanner'
                });
                SendMessage('Bridge', 'OnBannerRequestError', errorJson);
            });
    },

    Yes2SDK_Banners_HideBannerJS: function(idPtr) {
        var id = UTF8ToString(idPtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.banners !== 'undefined') {
            window.Yes2SDK.banners.hideBanner(id);
        }
    },

    Yes2SDK_Banners_HideAllBannersJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.banners !== 'undefined') {
            window.Yes2SDK.banners.hideAllBanners();
        }
    },

    Yes2SDK_Banners_RefreshBannersJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.banners !== 'undefined') {
            window.Yes2SDK.banners.refreshBanners();
        }
    }

});
