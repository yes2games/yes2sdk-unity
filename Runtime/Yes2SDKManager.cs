using UnityEngine;
using UnityEngine.Events;

namespace Yes2SDK
{
    /// <summary>
    /// Optional MonoBehaviour singleton for Yes2SDK.
    /// Auto-initializes the SDK and exposes UnityEvents for inspector wiring.
    /// The static Yes2SDK class remains the primary API.
    /// </summary>
    public class Yes2SDKManager : MonoBehaviour
    {
        public static Yes2SDKManager Instance { get; private set; }

        [Header("Events")]
        public UnityEvent OnInitialized;
        public UnityEvent<string> OnError;

        #region Module Accessors

        public Yes2SDKAds Ads => Yes2SDK.Ads;
        public Yes2SDKAnalytics Analytics => Yes2SDK.Analytics;
        public Yes2SDKSession Session => Yes2SDK.Session;
        public Yes2SDKPlayer Player => Yes2SDK.Player;
        public Yes2SDKData Data => Yes2SDK.Data;
        public Yes2SDKAuth Auth => Yes2SDK.Auth;
        public Yes2SDKGame Game => Yes2SDK.Game;
        public Yes2SDKBanners Banners => Yes2SDK.Banners;
        public Yes2SDKFriends Friends => Yes2SDK.Friends;
        public Yes2SDKScore Score => Yes2SDK.Score;
        public Yes2SDKLeaderboard Leaderboard => Yes2SDK.Leaderboard;
        public Yes2SDKIAP IAP => Yes2SDK.IAP;
        public Yes2SDKAchievements Achievements => Yes2SDK.Achievements;
        public Yes2SDKContext Context => Yes2SDK.Context;
        public Yes2SDKNotifications Notifications => Yes2SDK.Notifications;
        public Yes2SDKTournament Tournament => Yes2SDK.Tournament;
        public Yes2SDKStats Stats => Yes2SDK.Stats;

        #endregion


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (Yes2SDK.IsInitialized)
                return;

            Yes2SDK.InitializeAsync(
                onSuccess: () =>
                {
                    OnInitialized?.Invoke();
                    Yes2SDK.StartGameAsync();
                },
                onError: (error) => OnError?.Invoke(error.ToString())
            );
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
