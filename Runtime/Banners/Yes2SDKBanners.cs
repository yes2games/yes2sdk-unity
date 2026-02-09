using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Banners API for Yes2SDK.
    /// Provides multi-size banner ad support. Fully supported on CrazyGames; stubs on Poki.
    /// </summary>
    public class Yes2SDKBanners
    {
        #region Static Callback Fields

        private static Action _bannerRequestSuccessCallback;
        private static Action<Error> _bannerRequestErrorCallback;

        #endregion

        #region JavaScript Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Yes2SDK_Banners_ShowBannerJS(string id, string size, int x, int y);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Banners_HideBannerJS(string id);

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Banners_HideAllBannersJS();

        [DllImport("__Internal")]
        private static extern void Yes2SDK_Banners_RefreshBannersJS();
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Show a banner ad with the given id and size.
        /// </summary>
        /// <param name="id">Unique identifier for the banner container.</param>
        /// <param name="size">The banner size to request.</param>
        /// <param name="onSuccess">Called when the banner is shown.</param>
        /// <param name="onError">Called if banner request fails.</param>
        public void ShowBanner(string id, BannerSize size, Action onSuccess = null, Action<Error> onError = null)
        {
            _bannerRequestSuccessCallback = onSuccess;
            _bannerRequestErrorCallback = onError;

#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Banners_ShowBannerJS(id, size.ToString(), 0, 0);
#else
            Debug.Log($"[Yes2SDK] Mock: Banners.ShowBanner({id}, {size})");
            InvokeBannerRequestSuccess();
#endif
        }

        /// <summary>
        /// Hide a specific banner by id.
        /// </summary>
        public void HideBanner(string id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Banners_HideBannerJS(id);
#else
            Debug.Log($"[Yes2SDK] Mock: Banners.HideBanner({id})");
#endif
        }

        /// <summary>
        /// Hide all currently displayed banners.
        /// </summary>
        public void HideAllBanners()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Banners_HideAllBannersJS();
#else
            Debug.Log("[Yes2SDK] Mock: Banners.HideAllBanners()");
#endif
        }

        /// <summary>
        /// Refresh all currently displayed banners.
        /// </summary>
        public void RefreshBanners()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Yes2SDK_Banners_RefreshBannersJS();
#else
            Debug.Log("[Yes2SDK] Mock: Banners.RefreshBanners()");
#endif
        }

        #endregion

        #region Internal Callback Invocations (called by Bridge)

        internal static void InvokeBannerRequestSuccess()
        {
            _bannerRequestSuccessCallback?.Invoke();
            _bannerRequestSuccessCallback = null;
            _bannerRequestErrorCallback = null;
        }

        internal static void InvokeBannerRequestError(Error error)
        {
            _bannerRequestErrorCallback?.Invoke(error);
            _bannerRequestSuccessCallback = null;
            _bannerRequestErrorCallback = null;
        }

        #endregion
    }
}
