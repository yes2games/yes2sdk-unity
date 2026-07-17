#if UNITY_EDITOR
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Editor-only mock popup shown in Play Mode for ads and IAP purchases,
    /// so integrations can be tested interactively before a platform build.
    ///
    /// Ads: a full-screen overlay with a forced countdown (3s interstitial,
    /// 5s rewarded) and then Close / Claim Reward / Skip buttons. Callbacks
    /// fire through the same internal Invoke* entry points the WebGL bridge
    /// uses, so callback ordering and the in-flight guard behave exactly like
    /// a platform build.
    ///
    /// IAP: a Buy / Cancel confirmation dialog; Buy resolves PurchaseAsync
    /// with a realistic purchase payload, Cancel with a UserCancelled error.
    ///
    /// Drawn with IMGUI so the Runtime assembly needs no uGUI reference.
    /// The countdown uses realtime, not scaled time, because games typically
    /// pause (Time.timeScale = 0) in beforeAd.
    /// </summary>
    internal class Yes2SDKMockOverlay : MonoBehaviour
    {
        private enum Kind { None, Interstitial, Rewarded, Purchase }

        private const float InterstitialSeconds = 3f;
        private const float RewardedSeconds = 5f;

        private static Yes2SDKMockOverlay _instance;

        private Kind _kind = Kind.None;
        private string _placement;
        private float _canCloseAt;

        private string _productId;
        private string _productTitle;
        private string _productPrice;
        private string _developerPayload;

        private GUIStyle _badgeStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;

        #region Static entry points (called from Yes2SDKAds / Yes2SDKIAP)

        /// <summary>True while any mock popup is on screen.</summary>
        internal static bool IsBusy => _instance != null && _instance._kind != Kind.None;

        /// <summary>Show the interstitial popup. Returns false if another popup is open.</summary>
        internal static bool ShowInterstitial(string placement)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            overlay._kind = Kind.Interstitial;
            overlay._placement = placement;
            overlay._canCloseAt = Time.realtimeSinceStartup + InterstitialSeconds;
            Yes2SDKAds.InvokeInterstitialBeforeAd();
            return true;
        }

        /// <summary>Show the rewarded popup. Returns false if another popup is open.</summary>
        internal static bool ShowRewarded(string placement)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            overlay._kind = Kind.Rewarded;
            overlay._placement = placement;
            overlay._canCloseAt = Time.realtimeSinceStartup + RewardedSeconds;
            Yes2SDKAds.InvokeRewardedBeforeAd();
            return true;
        }

        /// <summary>Show the purchase dialog. Returns false if another popup is open.</summary>
        internal static bool ShowPurchase(string productId, string developerPayload)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            var product = Yes2SDKMockIAP.FindProduct(productId);
            overlay._kind = Kind.Purchase;
            overlay._productId = productId;
            overlay._productTitle = product?.Title ?? productId;
            overlay._productPrice = product?.Price ?? "$0.99 (mock price)";
            overlay._developerPayload = developerPayload;
            return true;
        }

        private static Yes2SDKMockOverlay GetOrCreate()
        {
            // Destroyed-on-play-exit instances compare equal to null, so this
            // also recovers when Domain Reload is disabled.
            if (_instance == null)
            {
                var go = new GameObject("Yes2SDKMockOverlay");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Yes2SDKMockOverlay>();
            }
            return _instance;
        }

        #endregion

        #region Completion

        private void CompleteInterstitial()
        {
            _kind = Kind.None;
            Yes2SDKAds.InvokeInterstitialAfterAd();
        }

        private void CompleteRewarded(bool viewed)
        {
            _kind = Kind.None;
            // Same ordering as the platform flow: afterAd (resume the game),
            // then the reward outcome.
            Yes2SDKAds.InvokeRewardedAfterAd();
            if (viewed)
            {
                Yes2SDKAds.InvokeRewardedAdViewed();
            }
            else
            {
                Yes2SDKAds.InvokeRewardedAdDismissed();
            }
        }

        private void CompletePurchase(bool confirmed)
        {
            string productId = _productId;
            string payload = _developerPayload;
            _kind = Kind.None;

            if (confirmed)
            {
                Yes2SDKIAP.InvokePurchaseSuccess(Yes2SDKMockIAP.RecordPurchase(productId, payload));
            }
            else
            {
                Yes2SDKIAP.InvokePurchaseError(new Error
                {
                    Code = "UserCancelled",
                    Message = "Purchase cancelled by user (mock)",
                    Context = "Yes2SDK.IAP.PurchaseAsync"
                });
            }
        }

        #endregion

        #region IMGUI

        private void OnGUI()
        {
            if (_kind == Kind.None) return;

            GUI.depth = -1000;
            EnsureStyles();

            // Dim the whole screen.
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float cardWidth = Mathf.Min(440f, Screen.width - 40f);
            float cardHeight = _kind == Kind.Purchase ? 240f : 280f;
            var cardRect = new Rect(
                (Screen.width - cardWidth) / 2f,
                (Screen.height - cardHeight) / 2f,
                cardWidth,
                cardHeight);

            GUI.Box(cardRect, GUIContent.none);
            var inner = new Rect(cardRect.x + 16f, cardRect.y + 14f, cardRect.width - 32f, cardRect.height - 28f);
            GUILayout.BeginArea(inner);

            if (_kind == Kind.Purchase)
            {
                DrawPurchase();
            }
            else
            {
                DrawAd();
            }

            GUILayout.EndArea();
        }

        private void DrawAd()
        {
            bool rewarded = _kind == Kind.Rewarded;

            GUILayout.Label(rewarded ? "MOCK REWARDED AD" : "MOCK INTERSTITIAL AD", _badgeStyle);
            GUILayout.Label($"placement: {_placement}", _hintStyle);
            GUILayout.Space(10f);

            GUILayout.Label(rewarded ? "Watch this ad to earn a reward" : "Advertisement", _titleStyle);
            GUILayout.Label("This is a mock ad from Yes2SDK. Real ads only show on platform builds.", _bodyStyle);

            GUILayout.FlexibleSpace();

            float remaining = _canCloseAt - Time.realtimeSinceStartup;
            if (remaining > 0f)
            {
                GUI.enabled = false;
                GUILayout.Button($"Ad playing... {Mathf.CeilToInt(remaining)}s", _buttonStyle, GUILayout.Height(34f));
                GUI.enabled = true;
            }
            else if (rewarded)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Claim Reward (adViewed)", _buttonStyle, GUILayout.Height(34f)))
                {
                    CompleteRewarded(viewed: true);
                }
                if (GUILayout.Button("Skip (adDismissed)", _buttonStyle, GUILayout.Height(34f)))
                {
                    CompleteRewarded(viewed: false);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("Close Ad (afterAd)", _buttonStyle, GUILayout.Height(34f)))
                {
                    CompleteInterstitial();
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label(
                rewarded
                    ? "Claim fires afterAd then adViewed (grant the reward). Skip fires afterAd then adDismissed."
                    : "Close fires afterAd. Pause your game in beforeAd, resume in afterAd.",
                _hintStyle);
        }

        private void DrawPurchase()
        {
            GUILayout.Label("MOCK PURCHASE", _badgeStyle);
            GUILayout.Space(10f);

            GUILayout.Label(_productTitle, _titleStyle);
            GUILayout.Label($"Product id: {_productId}", _bodyStyle);
            GUILayout.Label($"Price: {_productPrice}", _bodyStyle);
            if (!string.IsNullOrEmpty(_developerPayload))
            {
                GUILayout.Label($"Developer payload: {_developerPayload}", _bodyStyle);
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Buy", _buttonStyle, GUILayout.Height(34f)))
            {
                CompletePurchase(confirmed: true);
            }
            if (GUILayout.Button("Cancel (UserCancelled)", _buttonStyle, GUILayout.Height(34f)))
            {
                CompletePurchase(confirmed: false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Mock purchases last for this play session only.", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_badgeStyle != null) return;

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };
        }

        #endregion
    }
}
#endif
