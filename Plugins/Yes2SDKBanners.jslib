mergeInto(LibraryManager.library, {

    Yes2SDK_Banners_ShowBannerJS__deps: ['$__y2h'],
    Yes2SDK_Banners_ShowBannerJS: function(idPtr, sizePtr, x, y) {
        var id = UTF8ToString(idPtr);
        var size = UTF8ToString(sizePtr);

        if (!__y2h.has('banners')) {
            __y2h.sendError('OnBannerRequestError', 'NotInitialized', 'Yes2SDK Banners module not loaded', 'Yes2SDK.Banners.ShowBanner');
            return;
        }

        window.Yes2SDK.banners.showBanner(id, size, x, y)
            .then(function() {
                SendMessage('Bridge', 'OnBannerRequestSuccess', '');
            })
            .catch(__y2h.handleCatch('OnBannerRequestError', 'ShowBanner failed', 'Yes2SDK.Banners.ShowBanner'));
    },

    Yes2SDK_Banners_HideBannerJS__deps: ['$__y2h'],
    Yes2SDK_Banners_HideBannerJS: function(idPtr) {
        var id = UTF8ToString(idPtr);
        if (__y2h.has('banners')) window.Yes2SDK.banners.hideBanner(id);
    },

    Yes2SDK_Banners_HideAllBannersJS__deps: ['$__y2h'],
    Yes2SDK_Banners_HideAllBannersJS: function() {
        if (__y2h.has('banners')) window.Yes2SDK.banners.hideAllBanners();
    },

    Yes2SDK_Banners_RefreshBannersJS__deps: ['$__y2h'],
    Yes2SDK_Banners_RefreshBannersJS: function() {
        if (__y2h.has('banners')) window.Yes2SDK.banners.refreshBanners();
    }

});
