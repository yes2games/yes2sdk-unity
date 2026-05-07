using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        #region Handler Registry

        // Success handlers — either pass data through or ignore it
        private static readonly Dictionary<string, Action<string>> _handlers = new Dictionary<string, Action<string>>
        {
            // Init & Lifecycle
            ["OnInitializeSuccess"] = _ => Callbacks.InvokeInitializeSuccess(),
            ["OnStartGameSuccess"] = _ => Callbacks.InvokeStartGameSuccess(),
            ["OnPause"] = _ => Callbacks.InvokePause(),
            ["OnResume"] = _ => Callbacks.InvokeResume(),
            ["OnAudioEnabledChange"] = data => Callbacks.InvokeAudioEnabledChange(data),

            // Ads
            ["OnInterstitialBeforeAd"] = _ => Yes2SDKAds.InvokeInterstitialBeforeAd(),
            ["OnInterstitialAfterAd"] = _ => Yes2SDKAds.InvokeInterstitialAfterAd(),
            ["OnRewardedBeforeAd"] = _ => Yes2SDKAds.InvokeRewardedBeforeAd(),
            ["OnRewardedAfterAd"] = _ => Yes2SDKAds.InvokeRewardedAfterAd(),
            ["OnRewardedAdDismissed"] = _ => Yes2SDKAds.InvokeRewardedAdDismissed(),
            ["OnRewardedAdViewed"] = _ => Yes2SDKAds.InvokeRewardedAdViewed(),
            ["OnBannerShown"] = _ => Yes2SDKAds.InvokeBannerShown(),
            ["OnBannerHidden"] = _ => Yes2SDKAds.InvokeBannerHidden(),

            // Session
            ["OnGetEntryPointSuccess"] = Yes2SDKSession.InvokeGetEntryPointSuccess,

            // Player
            ["OnGetPlayerSuccess"] = Yes2SDKPlayer.InvokeGetPlayerSuccess,
            ["OnGetDataSuccess"] = Yes2SDKPlayer.InvokeGetDataSuccess,
            ["OnSetDataSuccess"] = _ => Yes2SDKPlayer.InvokeSetDataSuccess(),
            ["OnFlushDataSuccess"] = _ => Yes2SDKPlayer.InvokeFlushDataSuccess(),
            ["OnGetConnectedPlayersSuccess"] = Yes2SDKPlayer.InvokeGetConnectedPlayersSuccess,
            ["OnGetSignedPlayerInfoSuccess"] = Yes2SDKPlayer.InvokeGetSignedPlayerInfoSuccess,

            // Auth
            ["OnGetCurrentUserSuccess"] = Yes2SDKAuth.InvokeGetCurrentUserSuccess,
            ["OnSignInSuccess"] = Yes2SDKAuth.InvokeSignInSuccess,
            ["OnGetTokenSuccess"] = Yes2SDKAuth.InvokeGetTokenSuccess,
            ["OnAccountLinkSuccess"] = Yes2SDKAuth.InvokeAccountLinkSuccess,

            // Game
            ["OnInviteLinkSuccess"] = Yes2SDKGame.InvokeInviteLinkSuccess,
            ["OnSettingsChanged"] = Yes2SDKGame.InvokeSettingsChanged,

            // Banners
            ["OnBannerRequestSuccess"] = _ => Yes2SDKBanners.InvokeBannerRequestSuccess(),

            // Friends
            ["OnListFriendsSuccess"] = Yes2SDKFriends.InvokeListFriendsSuccess,
        };

        // Error handlers — receive parsed Error struct
        private static readonly Dictionary<string, Action<Error>> _errorHandlers = new Dictionary<string, Action<Error>>
        {
            // Init & Lifecycle
            ["OnInitializeError"] = Callbacks.InvokeInitializeError,
            ["OnStartGameError"] = Callbacks.InvokeStartGameError,

            // Ads
            ["OnInterstitialError"] = Yes2SDKAds.InvokeInterstitialError,
            ["OnRewardedError"] = Yes2SDKAds.InvokeRewardedError,
            ["OnBannerShowError"] = Yes2SDKAds.InvokeBannerShowError,
            ["OnBannerHideError"] = Yes2SDKAds.InvokeBannerHideError,

            // Session
            ["OnGetEntryPointError"] = Yes2SDKSession.InvokeGetEntryPointError,

            // Player
            ["OnGetPlayerError"] = Yes2SDKPlayer.InvokeGetPlayerError,
            ["OnGetDataError"] = Yes2SDKPlayer.InvokeGetDataError,
            ["OnSetDataError"] = Yes2SDKPlayer.InvokeSetDataError,
            ["OnFlushDataError"] = Yes2SDKPlayer.InvokeFlushDataError,
            ["OnGetConnectedPlayersError"] = Yes2SDKPlayer.InvokeGetConnectedPlayersError,
            ["OnGetSignedPlayerInfoError"] = Yes2SDKPlayer.InvokeGetSignedPlayerInfoError,

            // Auth
            ["OnGetCurrentUserError"] = Yes2SDKAuth.InvokeGetCurrentUserError,
            ["OnSignInError"] = Yes2SDKAuth.InvokeSignInError,
            ["OnGetTokenError"] = Yes2SDKAuth.InvokeGetTokenError,
            ["OnAccountLinkError"] = Yes2SDKAuth.InvokeAccountLinkError,

            // Game
            ["OnInviteLinkError"] = Yes2SDKGame.InvokeInviteLinkError,

            // Banners
            ["OnBannerRequestError"] = Yes2SDKBanners.InvokeBannerRequestError,

            // Friends
            ["OnListFriendsError"] = Yes2SDKFriends.InvokeListFriendsError,
        };

        #endregion

        #region Dispatch

        private void Handle(string data, [CallerMemberName] string name = null)
        {
            if (_handlers.TryGetValue(name, out var h)) { h(data); return; }
            if (_errorHandlers.TryGetValue(name, out var eh)) { eh(ParseError(data)); return; }
            Yes2Log.Warning($"Bridge: unhandled callback '{name}'");
        }

        #endregion

        #region Callbacks (called from JavaScript via SendMessage)

        // Init & Lifecycle
        public void OnInitializeSuccess(string msg) => Handle(msg);
        public void OnInitializeError(string msg) => Handle(msg);
        public void OnStartGameSuccess(string msg) => Handle(msg);
        public void OnStartGameError(string msg) => Handle(msg);
        public void OnPause(string msg) => Handle(msg);
        public void OnResume(string msg) => Handle(msg);
        public void OnAudioEnabledChange(string msg) => Handle(msg);

        // Ads
        public void OnInterstitialBeforeAd(string msg) => Handle(msg);
        public void OnInterstitialAfterAd(string msg) => Handle(msg);
        public void OnInterstitialError(string msg) => Handle(msg);
        public void OnRewardedBeforeAd(string msg) => Handle(msg);
        public void OnRewardedAfterAd(string msg) => Handle(msg);
        public void OnRewardedAdDismissed(string msg) => Handle(msg);
        public void OnRewardedAdViewed(string msg) => Handle(msg);
        public void OnRewardedError(string msg) => Handle(msg);
        public void OnBannerShown(string msg) => Handle(msg);
        public void OnBannerShowError(string msg) => Handle(msg);
        public void OnBannerHidden(string msg) => Handle(msg);
        public void OnBannerHideError(string msg) => Handle(msg);

        // Session
        public void OnGetEntryPointSuccess(string msg) => Handle(msg);
        public void OnGetEntryPointError(string msg) => Handle(msg);

        // Player
        public void OnGetPlayerSuccess(string msg) => Handle(msg);
        public void OnGetPlayerError(string msg) => Handle(msg);
        public void OnGetDataSuccess(string msg) => Handle(msg);
        public void OnGetDataError(string msg) => Handle(msg);
        public void OnSetDataSuccess(string msg) => Handle(msg);
        public void OnSetDataError(string msg) => Handle(msg);
        public void OnFlushDataSuccess(string msg) => Handle(msg);
        public void OnFlushDataError(string msg) => Handle(msg);
        public void OnGetConnectedPlayersSuccess(string msg) => Handle(msg);
        public void OnGetConnectedPlayersError(string msg) => Handle(msg);
        public void OnGetSignedPlayerInfoSuccess(string msg) => Handle(msg);
        public void OnGetSignedPlayerInfoError(string msg) => Handle(msg);

        // Auth
        public void OnGetCurrentUserSuccess(string msg) => Handle(msg);
        public void OnGetCurrentUserError(string msg) => Handle(msg);
        public void OnSignInSuccess(string msg) => Handle(msg);
        public void OnSignInError(string msg) => Handle(msg);
        public void OnGetTokenSuccess(string msg) => Handle(msg);
        public void OnGetTokenError(string msg) => Handle(msg);
        public void OnAccountLinkSuccess(string msg) => Handle(msg);
        public void OnAccountLinkError(string msg) => Handle(msg);

        // Game
        public void OnInviteLinkSuccess(string msg) => Handle(msg);
        public void OnInviteLinkError(string msg) => Handle(msg);
        public void OnSettingsChanged(string msg) => Handle(msg);

        // Banners
        public void OnBannerRequestSuccess(string msg) => Handle(msg);
        public void OnBannerRequestError(string msg) => Handle(msg);

        // Friends
        public void OnListFriendsSuccess(string msg) => Handle(msg);
        public void OnListFriendsError(string msg) => Handle(msg);

        #endregion

        #region Utility

        private static Error ParseError(string errorJson)
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
        internal static Action InitializeSuccessCallback;
        internal static Action<Error> InitializeErrorCallback;
        internal static Action StartGameSuccessCallback;
        internal static Action<Error> StartGameErrorCallback;

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

        internal static void InvokeAudioEnabledChange(string dataJson)
        {
            // JSON shape from JS bridge: { "enabled": true }
            bool enabled = true;
            if (!string.IsNullOrEmpty(dataJson))
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<AudioEnabledChangePayload>(dataJson);
                    enabled = payload?.Enabled ?? true;
                }
                catch
                {
                    // On parse failure, default to enabled — game shouldn't mute by accident.
                    enabled = true;
                }
            }
            Yes2SDK.InvokeAudioEnabledChange(enabled);
        }

        private class AudioEnabledChangePayload
        {
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }
        }
    }
}
