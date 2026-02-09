using UnityEngine;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Bridge component that receives callbacks from JavaScript via SendMessage.
    /// This GameObject is automatically created when Yes2SDK initializes.
    /// </summary>
    public class Bridge : MonoBehaviour
    {
        private static Bridge _instance;

        /// <summary>
        /// Gets or creates the singleton bridge instance.
        /// </summary>
        public static Bridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Bridge");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Bridge>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Ensures the bridge is created. Call this before any SDK operations.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Access Instance to ensure it's created
            var _ = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Initialization Callbacks

        /// <summary>
        /// Called from JavaScript when initialization succeeds.
        /// </summary>
        public void OnInitializeSuccess(string message)
        {
            Callbacks.InvokeInitializeSuccess();
        }

        /// <summary>
        /// Called from JavaScript when initialization fails.
        /// </summary>
        public void OnInitializeError(string errorJson)
        {
            var error = ParseError(errorJson);
            Callbacks.InvokeInitializeError(error);
        }

        #endregion

        #region Game Start Callbacks

        /// <summary>
        /// Called from JavaScript when game start succeeds.
        /// </summary>
        public void OnStartGameSuccess(string message)
        {
            Callbacks.InvokeStartGameSuccess();
        }

        /// <summary>
        /// Called from JavaScript when game start fails.
        /// </summary>
        public void OnStartGameError(string errorJson)
        {
            var error = ParseError(errorJson);
            Callbacks.InvokeStartGameError(error);
        }

        #endregion

        #region Lifecycle Callbacks

        /// <summary>
        /// Called from JavaScript when game should pause.
        /// </summary>
        public void OnPause(string message)
        {
            Callbacks.InvokePause();
        }

        /// <summary>
        /// Called from JavaScript when game can resume.
        /// </summary>
        public void OnResume(string message)
        {
            Callbacks.InvokeResume();
        }

        #endregion

        #region Ads Callbacks

        /// <summary>
        /// Called from JavaScript before an interstitial ad is shown.
        /// </summary>
        public void OnInterstitialBeforeAd(string message)
        {
            Yes2SDKAds.InvokeInterstitialBeforeAd();
        }

        /// <summary>
        /// Called from JavaScript after an interstitial ad completes.
        /// </summary>
        public void OnInterstitialAfterAd(string message)
        {
            Yes2SDKAds.InvokeInterstitialAfterAd();
        }

        /// <summary>
        /// Called from JavaScript when an interstitial ad fails.
        /// </summary>
        public void OnInterstitialError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKAds.InvokeInterstitialError(error);
        }

        /// <summary>
        /// Called from JavaScript before a rewarded ad is shown.
        /// </summary>
        public void OnRewardedBeforeAd(string message)
        {
            Yes2SDKAds.InvokeRewardedBeforeAd();
        }

        /// <summary>
        /// Called from JavaScript after a rewarded ad completes.
        /// </summary>
        public void OnRewardedAfterAd(string message)
        {
            Yes2SDKAds.InvokeRewardedAfterAd();
        }

        /// <summary>
        /// Called from JavaScript when a rewarded ad is dismissed without reward.
        /// </summary>
        public void OnRewardedAdDismissed(string message)
        {
            Yes2SDKAds.InvokeRewardedAdDismissed();
        }

        /// <summary>
        /// Called from JavaScript when a rewarded ad is fully viewed.
        /// </summary>
        public void OnRewardedAdViewed(string message)
        {
            Yes2SDKAds.InvokeRewardedAdViewed();
        }

        /// <summary>
        /// Called from JavaScript when a rewarded ad fails.
        /// </summary>
        public void OnRewardedError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKAds.InvokeRewardedError(error);
        }

        /// <summary>
        /// Called from JavaScript when a banner ad is shown.
        /// </summary>
        public void OnBannerShown(string message)
        {
            Yes2SDKAds.InvokeBannerShown();
        }

        /// <summary>
        /// Called from JavaScript when showing a banner ad fails.
        /// </summary>
        public void OnBannerShowError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKAds.InvokeBannerShowError(error);
        }

        /// <summary>
        /// Called from JavaScript when a banner ad is hidden.
        /// </summary>
        public void OnBannerHidden(string message)
        {
            Yes2SDKAds.InvokeBannerHidden();
        }

        /// <summary>
        /// Called from JavaScript when hiding a banner ad fails.
        /// </summary>
        public void OnBannerHideError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKAds.InvokeBannerHideError(error);
        }

        #endregion

        #region Session Callbacks

        /// <summary>
        /// Called from JavaScript when GetEntryPoint succeeds.
        /// </summary>
        public void OnGetEntryPointSuccess(string entryPoint)
        {
            Yes2SDKSession.InvokeGetEntryPointSuccess(entryPoint);
        }

        /// <summary>
        /// Called from JavaScript when GetEntryPoint fails.
        /// </summary>
        public void OnGetEntryPointError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKSession.InvokeGetEntryPointError(error);
        }

        #endregion

        #region Player Callbacks

        /// <summary>
        /// Called from JavaScript when GetPlayer succeeds.
        /// </summary>
        public void OnGetPlayerSuccess(string playerJson)
        {
            Yes2SDKPlayer.InvokeGetPlayerSuccess(playerJson);
        }

        /// <summary>
        /// Called from JavaScript when GetPlayer fails.
        /// </summary>
        public void OnGetPlayerError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeGetPlayerError(error);
        }

        /// <summary>
        /// Called from JavaScript when GetData succeeds.
        /// </summary>
        public void OnGetDataSuccess(string dataJson)
        {
            Yes2SDKPlayer.InvokeGetDataSuccess(dataJson);
        }

        /// <summary>
        /// Called from JavaScript when GetData fails.
        /// </summary>
        public void OnGetDataError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeGetDataError(error);
        }

        /// <summary>
        /// Called from JavaScript when SetData succeeds.
        /// </summary>
        public void OnSetDataSuccess(string message)
        {
            Yes2SDKPlayer.InvokeSetDataSuccess();
        }

        /// <summary>
        /// Called from JavaScript when SetData fails.
        /// </summary>
        public void OnSetDataError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeSetDataError(error);
        }

        /// <summary>
        /// Called from JavaScript when FlushData succeeds.
        /// </summary>
        public void OnFlushDataSuccess(string message)
        {
            Yes2SDKPlayer.InvokeFlushDataSuccess();
        }

        /// <summary>
        /// Called from JavaScript when FlushData fails.
        /// </summary>
        public void OnFlushDataError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeFlushDataError(error);
        }

        /// <summary>
        /// Called from JavaScript when GetConnectedPlayers succeeds.
        /// </summary>
        public void OnGetConnectedPlayersSuccess(string playersJson)
        {
            Yes2SDKPlayer.InvokeGetConnectedPlayersSuccess(playersJson);
        }

        /// <summary>
        /// Called from JavaScript when GetConnectedPlayers fails.
        /// </summary>
        public void OnGetConnectedPlayersError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeGetConnectedPlayersError(error);
        }

        /// <summary>
        /// Called from JavaScript when GetSignedPlayerInfo succeeds.
        /// </summary>
        public void OnGetSignedPlayerInfoSuccess(string signatureJson)
        {
            Yes2SDKPlayer.InvokeGetSignedPlayerInfoSuccess(signatureJson);
        }

        /// <summary>
        /// Called from JavaScript when GetSignedPlayerInfo fails.
        /// </summary>
        public void OnGetSignedPlayerInfoError(string errorJson)
        {
            var error = ParseError(errorJson);
            Yes2SDKPlayer.InvokeGetSignedPlayerInfoError(error);
        }

        #endregion

        #region Utility

        private Error ParseError(string errorJson)
        {
            if (string.IsNullOrEmpty(errorJson))
            {
                return new Error
                {
                    Code = "Unknown",
                    Message = "Unknown error",
                    Context = "Unknown"
                };
            }

            try
            {
                return JsonConvert.DeserializeObject<Error>(errorJson);
            }
            catch
            {
                return new Error
                {
                    Code = "Unknown",
                    Message = errorJson,
                    Context = "Unknown"
                };
            }
        }

        #endregion
    }

    /// <summary>
    /// Internal callback manager for Yes2SDK.
    /// </summary>
    internal static class Callbacks
    {
        internal static System.Action InitializeSuccessCallback;
        internal static System.Action<Error> InitializeErrorCallback;
        internal static System.Action StartGameSuccessCallback;
        internal static System.Action<Error> StartGameErrorCallback;

        internal static void InvokeInitializeSuccess()
        {
            Yes2SDK.SetInitialized(true);
            InitializeSuccessCallback?.Invoke();
            InitializeSuccessCallback = null;
        }

        internal static void InvokeInitializeError(Error error)
        {
            InitializeErrorCallback?.Invoke(error);
            InitializeErrorCallback = null;
        }

        internal static void InvokeStartGameSuccess()
        {
            StartGameSuccessCallback?.Invoke();
            StartGameSuccessCallback = null;
        }

        internal static void InvokeStartGameError(Error error)
        {
            StartGameErrorCallback?.Invoke(error);
            StartGameErrorCallback = null;
        }

        internal static void InvokePause()
        {
            Yes2SDK.InvokePause();
        }

        internal static void InvokeResume()
        {
            Yes2SDK.InvokeResume();
        }
    }
}
