using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Yes2SDK.Editor
{
    [CustomEditor(typeof(Yes2SDKManager))]
    public class Yes2SDKManagerEditor : UnityEditor.Editor
    {
        private int _selectedTab;
        private readonly string[] _tabNames = { "Events", "Testing" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(5);

            switch (_selectedTab)
            {
                case 0:
                    DrawEventsTab();
                    break;
                case 1:
                    DrawTestingTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Yes2SDK Manager", style);

            // Status line
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool initialized = Application.isPlaying && Yes2SDK.IsInitialized;
            var statusColor = initialized ? Color.green : Color.gray;
            var statusText = initialized
                ? $"Initialized ({Yes2SDK.CurrentPlatform})"
                : "Not Initialized";

            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = statusColor }
            };
            GUILayout.Label(statusText, statusStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #region Events Tab

        private void DrawEventsTab()
        {
            EditorGUILayout.LabelField("UnityEvents", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Wire these events to your own methods in the Inspector, or subscribe to Yes2SDK.OnInitialized / Yes2SDK.OnError in code.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnInitialized"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnError"));

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Module Accessors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Access all SDK modules via properties (e.g., manager.Ads.ShowInterstitial(...)). See Testing tab for examples.",
                MessageType.Info);
        }

        #endregion

        #region Testing Tab

        private void DrawTestingTab()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test SDK APIs.", MessageType.Info);
                return;
            }

            if (!Yes2SDK.IsInitialized)
            {
                EditorGUILayout.HelpBox("SDK is not initialized. Initialize first.", MessageType.Warning);
                if (GUILayout.Button("Initialize SDK"))
                {
                    Yes2SDK.InitializeAsync(
                        onSuccess: () => Debug.Log("[Test] SDK initialized"),
                        onError: e => Debug.LogError($"[Test] Init failed: {e}")
                    );
                }
                return;
            }

            DrawCoreSection();
            EditorGUILayout.Space(5);
            DrawSessionSection();
            EditorGUILayout.Space(5);
            DrawAdsSection();
            EditorGUILayout.Space(5);
            DrawAnalyticsSection();
            EditorGUILayout.Space(5);
            DrawPlayerSection();
            EditorGUILayout.Space(5);
            DrawDataSection();
            EditorGUILayout.Space(5);
            DrawAuthSection();
            EditorGUILayout.Space(5);
            DrawGameSection();
            EditorGUILayout.Space(5);
            DrawBannersSection();
            EditorGUILayout.Space(5);
            DrawFriendsSection();
            EditorGUILayout.Space(5);
            DrawScoreSection();
            EditorGUILayout.Space(5);
            DrawStubsSection();
        }

        private void DrawCoreSection()
        {
            EditorGUILayout.LabelField("Core", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize"))
            {
                Yes2SDK.InitializeAsync(
                    onSuccess: () => Debug.Log("[Test] Initialized"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            if (GUILayout.Button("Start Game"))
            {
                Yes2SDK.StartGameAsync(
                    onSuccess: () => Debug.Log("[Test] Game started"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Progress 50%"))
            {
                Yes2SDK.SetLoadingProgress(50);
                Debug.Log("[Test] SetLoadingProgress(50)");
            }
            if (GUILayout.Button("Haptic"))
            {
                Yes2SDK.PerformHapticFeedback();
                Debug.Log("[Test] PerformHapticFeedback");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"Initialized={Yes2SDK.IsInitialized}  Platform={Yes2SDK.CurrentPlatform}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSessionSection()
        {
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Locale"))
            {
                var v = Yes2SDK.Session.GetLocale();
                Debug.Log($"[Test] Locale: {v}");
            }
            if (GUILayout.Button("Get Device"))
            {
                var v = Yes2SDK.Session.GetDevice();
                Debug.Log($"[Test] Device: {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Orientation"))
            {
                var v = Yes2SDK.Session.GetOrientation();
                Debug.Log($"[Test] Orientation: {v}");
            }
            if (GUILayout.Button("Get Country"))
            {
                var v = Yes2SDK.Session.GetCountry();
                Debug.Log($"[Test] Country: {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Traffic Source"))
            {
                var v = Yes2SDK.Session.GetTrafficSource();
                Debug.Log($"[Test] TrafficSource: {v}");
            }
            if (GUILayout.Button("Get Entry Point Data"))
            {
                var v = Yes2SDK.Session.GetEntryPointData();
                Debug.Log($"[Test] EntryPointData: {v}");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Get Entry Point Async"))
            {
                Yes2SDK.Session.GetEntryPointAsync(
                    onSuccess: ep => Debug.Log($"[Test] EntryPoint: {ep}"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdsSection()
        {
            EditorGUILayout.LabelField("Ads", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Interstitial"))
            {
                Yes2SDK.Ads.ShowInterstitial("test-placement", "Test interstitial",
                    beforeAd: () => Debug.Log("[Test] Interstitial beforeAd"),
                    afterAd: () => Debug.Log("[Test] Interstitial afterAd"),
                    onError: e => Debug.LogError($"[Test] Interstitial error: {e}")
                );
            }
            if (GUILayout.Button("Show Rewarded"))
            {
                Yes2SDK.Ads.ShowRewarded("test-rewarded", "Test rewarded",
                    beforeAd: () => Debug.Log("[Test] Rewarded beforeAd"),
                    afterAd: () => Debug.Log("[Test] Rewarded afterAd"),
                    adDismissed: () => Debug.Log("[Test] Rewarded dismissed"),
                    adViewed: () => Debug.Log("[Test] Rewarded viewed (grant reward)"),
                    onError: e => Debug.LogError($"[Test] Rewarded error: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Banner"))
            {
                Yes2SDK.Ads.ShowBanner(
                    onShown: () => Debug.Log("[Test] Banner shown"),
                    onError: e => Debug.LogError($"[Test] Banner error: {e}")
                );
            }
            if (GUILayout.Button("Hide Banner"))
            {
                Yes2SDK.Ads.HideBanner(
                    onHidden: () => Debug.Log("[Test] Banner hidden"),
                    onError: e => Debug.LogError($"[Test] Banner hide error: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Is Ad Blocked"))
            {
                var blocked = Yes2SDK.Ads.IsAdBlocked();
                Debug.Log($"[Test] IsAdBlocked: {blocked}");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAnalyticsSection()
        {
            EditorGUILayout.LabelField("Analytics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Log Event"))
            {
                Yes2SDK.Analytics.LogEvent("test_event", new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 42 }
                });
                Debug.Log("[Test] LogEvent('test_event')");
            }
            if (GUILayout.Button("Level Start"))
            {
                Yes2SDK.Analytics.LogLevelStart("level_1");
                Debug.Log("[Test] LogLevelStart('level_1')");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Level End"))
            {
                Yes2SDK.Analytics.LogLevelEnd("level_1", 1000, true);
                Debug.Log("[Test] LogLevelEnd('level_1', 1000, true)");
            }
            if (GUILayout.Button("Log Score"))
            {
                Yes2SDK.Analytics.LogScore(1234, "level_1");
                Debug.Log("[Test] LogScore(1234, 'level_1')");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tutorial Start"))
            {
                Yes2SDK.Analytics.LogTutorialStart();
                Debug.Log("[Test] LogTutorialStart");
            }
            if (GUILayout.Button("Tutorial End"))
            {
                Yes2SDK.Analytics.LogTutorialEnd();
                Debug.Log("[Test] LogTutorialEnd");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPlayerSection()
        {
            EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Player"))
            {
                Yes2SDK.Player.GetPlayerAsync(
                    onSuccess: p => Debug.Log($"[Test] Player: {p.Name} (id={p.Id})"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            if (GUILayout.Button("Get Data"))
            {
                Yes2SDK.Player.GetDataAsync(new[] { "testKey" },
                    onSuccess: d => Debug.Log($"[Test] GetData: {d}"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Data"))
            {
                Yes2SDK.Player.SetDataAsync("{\"testKey\":\"testValue\"}",
                    onSuccess: () => Debug.Log("[Test] SetData success"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            if (GUILayout.Button("Flush Data"))
            {
                Yes2SDK.Player.FlushDataAsync(
                    onSuccess: () => Debug.Log("[Test] FlushData success"),
                    onError: e => Debug.LogError($"[Test] {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Is Data Supported"))
            {
                var v = Yes2SDK.Player.IsDataSupported();
                Debug.Log($"[Test] IsDataSupported: {v}");
            }
            if (GUILayout.Button("Is Connected Players"))
            {
                var v = Yes2SDK.Player.IsConnectedPlayersSupported();
                Debug.Log($"[Test] IsConnectedPlayersSupported: {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDataSection()
        {
            EditorGUILayout.LabelField("Data (PlayerPrefs-style)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Int"))
            {
                Yes2SDK.Data.SetInt("test_int", 42);
                Debug.Log("[Test] Data.SetInt('test_int', 42)");
            }
            if (GUILayout.Button("Get Int"))
            {
                var v = Yes2SDK.Data.GetInt("test_int", 0);
                Debug.Log($"[Test] Data.GetInt('test_int'): {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set String"))
            {
                Yes2SDK.Data.SetString("test_str", "hello");
                Debug.Log("[Test] Data.SetString('test_str', 'hello')");
            }
            if (GUILayout.Button("Get String"))
            {
                var v = Yes2SDK.Data.GetString("test_str", "");
                Debug.Log($"[Test] Data.GetString('test_str'): {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Has Key"))
            {
                var v = Yes2SDK.Data.HasKey("test_int");
                Debug.Log($"[Test] Data.HasKey('test_int'): {v}");
            }
            if (GUILayout.Button("Delete Key"))
            {
                Yes2SDK.Data.DeleteKey("test_int");
                Debug.Log("[Test] Data.DeleteKey('test_int')");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Delete All"))
            {
                Yes2SDK.Data.DeleteAll();
                Debug.Log("[Test] Data.DeleteAll()");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAuthSection()
        {
            EditorGUILayout.LabelField("Auth (CG full, Poki stub)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Is Supported"))
            {
                var v = Yes2SDK.Auth.IsSupported();
                Debug.Log($"[Test] Auth.IsSupported: {v}");
            }
            if (GUILayout.Button("Get Current User"))
            {
                Yes2SDK.Auth.GetCurrentUserAsync(
                    onSuccess: u => Debug.Log($"[Test] Auth user: {u}"),
                    onError: e => Debug.LogWarning($"[Test] Auth: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sign In"))
            {
                Yes2SDK.Auth.SignInAsync(
                    onSuccess: u => Debug.Log($"[Test] Signed in: {u}"),
                    onError: e => Debug.LogWarning($"[Test] SignIn: {e}")
                );
            }
            if (GUILayout.Button("Get Token"))
            {
                Yes2SDK.Auth.GetTokenAsync(
                    onSuccess: t => Debug.Log($"[Test] Token: {t}"),
                    onError: e => Debug.LogWarning($"[Test] GetToken: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Account Link Prompt"))
            {
                Yes2SDK.Auth.ShowAccountLinkPromptAsync(
                    onSuccess: r => Debug.Log($"[Test] AccountLink result: {r}"),
                    onError: e => Debug.LogWarning($"[Test] AccountLink: {e}")
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGameSection()
        {
            EditorGUILayout.LabelField("Game (Lifecycle, Invite, Settings)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Gameplay Start"))
            {
                Yes2SDK.Game.GameplayStart();
                Debug.Log("[Test] Game.GameplayStart()");
            }
            if (GUILayout.Button("Gameplay Stop"))
            {
                Yes2SDK.Game.GameplayStop();
                Debug.Log("[Test] Game.GameplayStop()");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Happy Time"))
            {
                Yes2SDK.Game.HappyTime();
                Debug.Log("[Test] Game.HappyTime()");
            }
            if (GUILayout.Button("Get Settings"))
            {
                var s = Yes2SDK.Game.GetSettings();
                Debug.Log($"[Test] GameSettings: {s}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Invite Link"))
            {
                Yes2SDK.Game.InviteLinkAsync(
                    new Dictionary<string, string> { { "roomId", "test-room" } },
                    onSuccess: link => Debug.Log($"[Test] InviteLink: {link}"),
                    onError: e => Debug.LogWarning($"[Test] InviteLink: {e}")
                );
            }
            if (GUILayout.Button("Get Invite Param"))
            {
                var v = Yes2SDK.Game.GetInviteParam("roomId");
                Debug.Log($"[Test] GetInviteParam('roomId'): {v}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Invite Button"))
            {
                Yes2SDK.Game.ShowInviteButton();
                Debug.Log("[Test] Game.ShowInviteButton()");
            }
            if (GUILayout.Button("Hide Invite Button"))
            {
                Yes2SDK.Game.HideInviteButton();
                Debug.Log("[Test] Game.HideInviteButton()");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Copy To Clipboard"))
            {
                Yes2SDK.Game.CopyToClipboard("Yes2SDK test clipboard");
                Debug.Log("[Test] Game.CopyToClipboard('Yes2SDK test clipboard')");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBannersSection()
        {
            EditorGUILayout.LabelField("Banners (Multi-size, CG only)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show 728x90"))
            {
                Yes2SDK.Banners.ShowBanner("test-leaderboard", BannerSize.Leaderboard_728x90,
                    onSuccess: () => Debug.Log("[Test] Banner 728x90 shown"),
                    onError: e => Debug.LogWarning($"[Test] Banner: {e}")
                );
            }
            if (GUILayout.Button("Show 300x250"))
            {
                Yes2SDK.Banners.ShowBanner("test-medium", BannerSize.Medium_300x250,
                    onSuccess: () => Debug.Log("[Test] Banner 300x250 shown"),
                    onError: e => Debug.LogWarning($"[Test] Banner: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hide Banner"))
            {
                Yes2SDK.Banners.HideBanner("test-leaderboard");
                Debug.Log("[Test] Banners.HideBanner('test-leaderboard')");
            }
            if (GUILayout.Button("Hide All"))
            {
                Yes2SDK.Banners.HideAllBanners();
                Debug.Log("[Test] Banners.HideAllBanners()");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Banners"))
            {
                Yes2SDK.Banners.RefreshBanners();
                Debug.Log("[Test] Banners.RefreshBanners()");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFriendsSection()
        {
            EditorGUILayout.LabelField("Friends (CG only)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("List Friends (page 0, size 10)"))
            {
                Yes2SDK.Friends.ListFriendsAsync(0, 10,
                    onSuccess: page => Debug.Log($"[Test] Friends: {page}"),
                    onError: e => Debug.LogWarning($"[Test] Friends: {e}")
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScoreSection()
        {
            EditorGUILayout.LabelField("Score (CG only)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Score (1000)"))
            {
                Yes2SDK.Score.AddScore(1000f);
                Debug.Log("[Test] Score.AddScore(1000)");
            }
            if (GUILayout.Button("Submit Score"))
            {
                Yes2SDK.Score.SubmitScore("encrypted-test-score");
                Debug.Log("[Test] Score.SubmitScore('encrypted-test-score')");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStubsSection()
        {
            EditorGUILayout.LabelField("Stubs (FeatureNotSupported)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox("These modules return FeatureNotSupported on Poki. Test to verify graceful handling.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Leaderboard"))
            {
                Yes2SDK.Leaderboard.GetLeaderboardAsync("test",
                    onSuccess: d => Debug.Log($"[Test] Leaderboard: {d}"),
                    onError: e => Debug.LogWarning($"[Test] Leaderboard: {e}")
                );
            }
            if (GUILayout.Button("IAP"))
            {
                Yes2SDK.IAP.GetCatalogAsync(
                    onSuccess: d => Debug.Log($"[Test] IAP catalog: {d}"),
                    onError: e => Debug.LogWarning($"[Test] IAP: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Achievements"))
            {
                Yes2SDK.Achievements.GetAchievementsAsync(
                    onSuccess: d => Debug.Log($"[Test] Achievements: {d}"),
                    onError: e => Debug.LogWarning($"[Test] Achievements: {e}")
                );
            }
            if (GUILayout.Button("Context"))
            {
                var ctx = Yes2SDK.Context.GetContext();
                Debug.Log($"[Test] Context: {ctx}");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Notifications"))
            {
                Yes2SDK.Notifications.ScheduleAsync("Test", "body", 60, "{}",
                    onSuccess: d => Debug.Log($"[Test] Notification: {d}"),
                    onError: e => Debug.LogWarning($"[Test] Notifications: {e}")
                );
            }
            if (GUILayout.Button("Tournament"))
            {
                Yes2SDK.Tournament.GetCurrentAsync(
                    onSuccess: d => Debug.Log($"[Test] Tournament: {d}"),
                    onError: e => Debug.LogWarning($"[Test] Tournament: {e}")
                );
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Stats"))
            {
                Yes2SDK.Stats.GetStatsAsync(new[] { "testStat" },
                    onSuccess: d => Debug.Log($"[Test] Stats: {d}"),
                    onError: e => Debug.LogWarning($"[Test] Stats: {e}")
                );
            }

            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
