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
            ["OnAccountDialogOpen"] = _ => Callbacks.InvokeAccountDialogOpen(),
            ["OnAccountDialogClose"] = _ => Callbacks.InvokeAccountDialogClose(),

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
            ["OnGetUniqueIdSuccess"] = Yes2SDKPlayer.InvokeGetUniqueIdSuccess,
            ["OnGetIDsPerGameSuccess"] = Yes2SDKPlayer.InvokeGetIDsPerGameSuccess,
            ["OnGetPayingStatusSuccess"] = Yes2SDKPlayer.InvokeGetPayingStatusSuccess,
            ["OnGetModeSuccess"] = Yes2SDKPlayer.InvokeGetModeSuccess,
            ["OnGetPhotoSuccess"] = Yes2SDKPlayer.InvokeGetPhotoSuccess,

            // Auth
            ["OnGetCurrentUserSuccess"] = Yes2SDKAuth.InvokeGetCurrentUserSuccess,
            ["OnSignInSuccess"] = Yes2SDKAuth.InvokeSignInSuccess,
            ["OnGetTokenSuccess"] = Yes2SDKAuth.InvokeGetTokenSuccess,
            ["OnAccountLinkSuccess"] = Yes2SDKAuth.InvokeAccountLinkSuccess,

            // Game
            ["OnInviteLinkSuccess"] = Yes2SDKGame.InvokeInviteLinkSuccess,
            ["OnSettingsChanged"] = Yes2SDKGame.InvokeSettingsChanged,
            ["OnGetServerTimeSuccess"] = Yes2SDKGame.InvokeGetServerTimeSuccess,

            // Banners
            ["OnBannerRequestSuccess"] = _ => Yes2SDKBanners.InvokeBannerRequestSuccess(),
            ["OnGetBannerStatusSuccess"] = Yes2SDKBanners.InvokeBannerStatusSuccess,

            // Friends
            ["OnListFriendsSuccess"] = Yes2SDKFriends.InvokeListFriendsSuccess,

            // IAP
            ["OnGetCatalogSuccess"] = Yes2SDKIAP.InvokeGetCatalogSuccess,
            ["OnPurchaseSuccess"] = Yes2SDKIAP.InvokePurchaseSuccess,
            ["OnGetPurchasesSuccess"] = Yes2SDKIAP.InvokeGetPurchasesSuccess,
            ["OnConsumePurchaseSuccess"] = _ => Yes2SDKIAP.InvokeConsumePurchaseSuccess(),

            // Data (async durable saves)
            ["OnDataSetStringSuccess"] = Yes2SDKData.InvokeSetStringAsyncSuccess,
            ["OnDataFlushSuccess"] = Yes2SDKData.InvokeFlushSuccess,

            // Leaderboard
            ["OnGetLeaderboardSuccess"] = Yes2SDKLeaderboard.InvokeGetLeaderboardSuccess,
            ["OnSetScoreSuccess"] = Yes2SDKLeaderboard.InvokeSetScoreSuccess,
            ["OnGetEntriesSuccess"] = Yes2SDKLeaderboard.InvokeGetEntriesSuccess,
            ["OnGetPlayerEntrySuccess"] = Yes2SDKLeaderboard.InvokeGetPlayerEntrySuccess,
            ["OnGetConnectedPlayerEntriesSuccess"] = Yes2SDKLeaderboard.InvokeGetConnectedPlayerEntriesSuccess,

            // Stats
            ["OnGetStatsSuccess"] = Yes2SDKStats.InvokeGetStatsSuccess,
            ["OnSetStatsSuccess"] = _ => Yes2SDKStats.InvokeSetStatsSuccess(),
            ["OnIncrementStatsSuccess"] = Yes2SDKStats.InvokeIncrementStatsSuccess,

            // Config
            ["OnGetFlagsSuccess"] = Yes2SDKConfig.InvokeGetFlagsSuccess,

            // Review
            ["OnCanReviewSuccess"] = Yes2SDKReview.InvokeCanReviewSuccess,
            ["OnRequestReviewSuccess"] = Yes2SDKReview.InvokeRequestReviewSuccess,
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
            ["OnGetUniqueIdError"] = Yes2SDKPlayer.InvokeGetUniqueIdError,
            ["OnGetIDsPerGameError"] = Yes2SDKPlayer.InvokeGetIDsPerGameError,
            ["OnGetPayingStatusError"] = Yes2SDKPlayer.InvokeGetPayingStatusError,
            ["OnGetModeError"] = Yes2SDKPlayer.InvokeGetModeError,
            ["OnGetPhotoError"] = Yes2SDKPlayer.InvokeGetPhotoError,

            // Auth
            ["OnGetCurrentUserError"] = Yes2SDKAuth.InvokeGetCurrentUserError,
            ["OnSignInError"] = Yes2SDKAuth.InvokeSignInError,
            ["OnGetTokenError"] = Yes2SDKAuth.InvokeGetTokenError,
            ["OnAccountLinkError"] = Yes2SDKAuth.InvokeAccountLinkError,

            // Game
            ["OnInviteLinkError"] = Yes2SDKGame.InvokeInviteLinkError,
            ["OnGetServerTimeError"] = Yes2SDKGame.InvokeGetServerTimeError,

            // Banners
            ["OnBannerRequestError"] = Yes2SDKBanners.InvokeBannerRequestError,
            ["OnGetBannerStatusError"] = Yes2SDKBanners.InvokeBannerStatusError,

            // Friends
            ["OnListFriendsError"] = Yes2SDKFriends.InvokeListFriendsError,

            // IAP
            ["OnGetCatalogError"] = Yes2SDKIAP.InvokeGetCatalogError,
            ["OnPurchaseError"] = Yes2SDKIAP.InvokePurchaseError,
            ["OnGetPurchasesError"] = Yes2SDKIAP.InvokeGetPurchasesError,
            ["OnConsumePurchaseError"] = Yes2SDKIAP.InvokeConsumePurchaseError,

            // Data (async durable saves)
            ["OnDataSetStringError"] = Yes2SDKData.InvokeSetStringAsyncError,
            ["OnDataFlushError"] = Yes2SDKData.InvokeFlushError,

            // Leaderboard
            ["OnGetLeaderboardError"] = Yes2SDKLeaderboard.InvokeGetLeaderboardError,
            ["OnSetScoreError"] = Yes2SDKLeaderboard.InvokeSetScoreError,
            ["OnGetEntriesError"] = Yes2SDKLeaderboard.InvokeGetEntriesError,
            ["OnGetPlayerEntryError"] = Yes2SDKLeaderboard.InvokeGetPlayerEntryError,
            ["OnGetConnectedPlayerEntriesError"] = Yes2SDKLeaderboard.InvokeGetConnectedPlayerEntriesError,

            // Stats
            ["OnGetStatsError"] = Yes2SDKStats.InvokeGetStatsError,
            ["OnSetStatsError"] = Yes2SDKStats.InvokeSetStatsError,
            ["OnIncrementStatsError"] = Yes2SDKStats.InvokeIncrementStatsError,

            // Config
            ["OnGetFlagsError"] = Yes2SDKConfig.InvokeGetFlagsError,

            // Review
            ["OnCanReviewError"] = Yes2SDKReview.InvokeCanReviewError,
            ["OnRequestReviewError"] = Yes2SDKReview.InvokeRequestReviewError,
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
        public void OnAccountDialogOpen(string msg) => Handle(msg);
        public void OnAccountDialogClose(string msg) => Handle(msg);

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
        public void OnGetUniqueIdSuccess(string msg) => Handle(msg);
        public void OnGetUniqueIdError(string msg) => Handle(msg);
        public void OnGetIDsPerGameSuccess(string msg) => Handle(msg);
        public void OnGetIDsPerGameError(string msg) => Handle(msg);
        public void OnGetPayingStatusSuccess(string msg) => Handle(msg);
        public void OnGetPayingStatusError(string msg) => Handle(msg);
        public void OnGetModeSuccess(string msg) => Handle(msg);
        public void OnGetModeError(string msg) => Handle(msg);
        public void OnGetPhotoSuccess(string msg) => Handle(msg);
        public void OnGetPhotoError(string msg) => Handle(msg);

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
        public void OnGetServerTimeSuccess(string msg) => Handle(msg);
        public void OnGetServerTimeError(string msg) => Handle(msg);

        // Banners
        public void OnBannerRequestSuccess(string msg) => Handle(msg);
        public void OnBannerRequestError(string msg) => Handle(msg);
        public void OnGetBannerStatusSuccess(string msg) => Handle(msg);
        public void OnGetBannerStatusError(string msg) => Handle(msg);

        // Friends
        public void OnListFriendsSuccess(string msg) => Handle(msg);
        public void OnListFriendsError(string msg) => Handle(msg);

        // IAP
        public void OnGetCatalogSuccess(string msg) => Handle(msg);
        public void OnGetCatalogError(string msg) => Handle(msg);
        public void OnPurchaseSuccess(string msg) => Handle(msg);
        public void OnPurchaseError(string msg) => Handle(msg);
        public void OnGetPurchasesSuccess(string msg) => Handle(msg);
        public void OnGetPurchasesError(string msg) => Handle(msg);
        public void OnConsumePurchaseSuccess(string msg) => Handle(msg);
        public void OnConsumePurchaseError(string msg) => Handle(msg);

        // Data (async durable saves)
        public void OnDataSetStringSuccess(string msg) => Handle(msg);
        public void OnDataSetStringError(string msg) => Handle(msg);
        public void OnDataFlushSuccess(string msg) => Handle(msg);
        public void OnDataFlushError(string msg) => Handle(msg);

        // Leaderboard
        public void OnGetLeaderboardSuccess(string msg) => Handle(msg);
        public void OnGetLeaderboardError(string msg) => Handle(msg);
        public void OnSetScoreSuccess(string msg) => Handle(msg);
        public void OnSetScoreError(string msg) => Handle(msg);
        public void OnGetEntriesSuccess(string msg) => Handle(msg);
        public void OnGetEntriesError(string msg) => Handle(msg);
        public void OnGetPlayerEntrySuccess(string msg) => Handle(msg);
        public void OnGetPlayerEntryError(string msg) => Handle(msg);
        public void OnGetConnectedPlayerEntriesSuccess(string msg) => Handle(msg);
        public void OnGetConnectedPlayerEntriesError(string msg) => Handle(msg);

        // Stats
        public void OnGetStatsSuccess(string msg) => Handle(msg);
        public void OnGetStatsError(string msg) => Handle(msg);
        public void OnSetStatsSuccess(string msg) => Handle(msg);
        public void OnSetStatsError(string msg) => Handle(msg);
        public void OnIncrementStatsSuccess(string msg) => Handle(msg);
        public void OnIncrementStatsError(string msg) => Handle(msg);

        // Config
        public void OnGetFlagsSuccess(string msg) => Handle(msg);
        public void OnGetFlagsError(string msg) => Handle(msg);

        // Review
        public void OnCanReviewSuccess(string msg) => Handle(msg);
        public void OnCanReviewError(string msg) => Handle(msg);
        public void OnRequestReviewSuccess(string msg) => Handle(msg);
        public void OnRequestReviewError(string msg) => Handle(msg);

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

        internal static void InvokeAccountDialogOpen()
        {
            Yes2SDK.InvokeAccountDialogOpen();
        }

        internal static void InvokeAccountDialogClose()
        {
            Yes2SDK.InvokeAccountDialogClose();
        }

        internal static void InvokeAudioEnabledChange(string dataJson)
        {
            // JSON shape from JS bridge: { "enabled": true } or { "enabled": false }
            // Manual parse instead of JsonConvert: under IL2CPP Code Stripping = High,
            // Newtonsoft.Json's reflection-based deserializer can throw internally even
            // with link.xml preservation, and the silent catch below would default us
            // to `enabled = true` — making mute toggles always report unmuted.
            bool enabled = true;
            if (!string.IsNullOrEmpty(dataJson))
            {
                int idx = dataJson.IndexOf("\"enabled\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // Search forward from the key for `true` / `false` literal.
                    int trueIdx = dataJson.IndexOf("true", idx, StringComparison.Ordinal);
                    int falseIdx = dataJson.IndexOf("false", idx, StringComparison.Ordinal);
                    if (falseIdx >= 0 && (trueIdx < 0 || falseIdx < trueIdx))
                        enabled = false;
                    else if (trueIdx >= 0)
                        enabled = true;
                }
            }
            Yes2SDK.InvokeAudioEnabledChange(enabled);
        }
    }
}
